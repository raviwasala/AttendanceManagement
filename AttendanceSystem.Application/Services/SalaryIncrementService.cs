using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Domain.Interfaces;

namespace AttendanceSystem.Application.Services;

/// <summary>
/// Salary increments — one employee, or a whole department or grade at once.
///
/// An increment writes <see cref="EmployeePayrollInfo.BasicSalaryOverride"/>, because that is
/// where the payroll reads a basic from. That has a consequence worth being explicit about:
/// incrementing somebody who was on their grade's figure detaches them from the grade, and
/// their salary stops following it thereafter. Unavoidable — a personal raise is by
/// definition a departure from the grade — but it surprises people, so the preview says so
/// per row before anything is written.
/// </summary>
public class SalaryIncrementService : ISalaryIncrementService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _currentUser;
    private readonly IApprovalScopeService _scopes;
    private readonly IPayrollPeriodService _periods;

    public SalaryIncrementService(IUnitOfWork uow, IAuditService audit,
                                  ICurrentUserContext currentUser, IApprovalScopeService scopes,
                                  IPayrollPeriodService periods)
    {
        _uow = uow;
        _audit = audit;
        _currentUser = currentUser;
        _scopes = scopes;
        _periods = periods;
    }

    /// <summary>
    /// How many already-paid months a raise is back-dated across.
    ///
    /// Whole months only, counted from the effective month up to but excluding the open
    /// payroll month — the open month is paid at the new rate directly, so counting it would
    /// pay the raise twice for that month. An increment effective on the 15th still counts
    /// its whole month: payroll is monthly, and part-month raises are not something this
    /// system offers.
    ///
    /// Static and pure so it can be checked against a hand count, which is the only way
    /// anybody will trust a figure that appears on a payslip as one lump sum.
    /// </summary>
    public static int ArrearsMonthsBetween(DateTime effectiveDate, int openYearMonth)
    {
        var effective = effectiveDate.Year * 100 + effectiveDate.Month;
        if (effective >= openYearMonth) return 0;

        var months = (openYearMonth / 100 - effectiveDate.Year) * 12
                   + (openYearMonth % 100 - effectiveDate.Month);

        return months > 0 ? months : 0;
    }

    /// <summary>
    /// Rounded to the cent as it is computed, not left to the database.
    ///
    /// 7.5% of 48,333.33 is 3,624.99975. Storing the unrounded figure and rounding on display
    /// would make the payslip and the increment record disagree in the last cent, which is
    /// exactly the sort of difference somebody spends an afternoon chasing.
    /// </summary>
    private static decimal Rise(decimal basic, decimal value, IncrementBasis basis) =>
        basis == IncrementBasis.Percentage
            ? Math.Round(basic * value / 100m, 2, MidpointRounding.AwayFromZero)
            : Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public async Task<Result<IncrementPreviewDto>> PreviewAsync(ApplyIncrementDto dto)
    {
        try
        {
            if (dto.Value <= 0)
                return Result<IncrementPreviewDto>.Failure("Enter an increment greater than zero.");

            if (dto.Basis == IncrementBasis.Percentage && dto.Value > 100)
                return Result<IncrementPreviewDto>.Failure(
                    "A percentage over 100 would more than double the salary. " +
                    "Switch to an amount if that is really intended.");

            var scope = await _scopes.GetDataScopeAsync();

            var employees = (await _uow.Employees.FindAsync(e => e.IsActive && !e.IsDeleted))
                .Where(e => scope.Allows(e.Id, e.DepartmentId))
                .ToList();

            employees = dto.Target switch
            {
                IncrementTarget.Department =>
                    employees.Where(e => e.DepartmentId == dto.DepartmentId).ToList(),
                IncrementTarget.Grade =>
                    employees.ToList(),   // narrowed below, once the payroll info is loaded
                _ => employees.Where(e => dto.EmployeeIds.Contains(e.Id)).ToList()
            };

            var infos = (await _uow.EmployeePayrollInfos.GetAllAsync())
                .ToDictionary(i => i.EmployeeId);
            var grades = (await _uow.SalaryGrades.GetAllAsync()).ToDictionary(g => g.Id);
            var departments = (await _uow.Departments.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);

            if (dto.Target == IncrementTarget.Grade)
                employees = employees
                    .Where(e => infos.TryGetValue(e.Id, out var i) && i.SalaryGradeId == dto.SalaryGradeId)
                    .ToList();

            var rows = new List<IncrementPreviewRowDto>();

            foreach (var e in employees.OrderBy(e => e.EmployeeCode))
            {
                infos.TryGetValue(e.Id, out var info);

                decimal? gradeBasic = info?.SalaryGradeId != null
                    && grades.TryGetValue(info.SalaryGradeId.Value, out var g) ? g.BasicSalary : null;

                var current = info?.BasicSalaryOverride ?? gradeBasic;

                var row = new IncrementPreviewRowDto
                {
                    EmployeeId = e.Id,
                    EmployeeCode = e.EmployeeCode ?? "",
                    EmployeeName = e.FullName ?? "",
                    DepartmentName = departments.TryGetValue(e.DepartmentId, out var dn) ? dn : "",
                    GradeName = info?.SalaryGradeId != null && grades.TryGetValue(info.SalaryGradeId.Value, out var g2)
                        ? g2.Name : "",
                    FromGrade = info?.BasicSalaryOverride == null && gradeBasic != null
                };

                // Nobody is silently skipped. A row with no salary is listed with the reason,
                // because "39 of 40 were incremented" is worthless without knowing which one.
                if (current == null || current == 0m)
                {
                    row.Blocked = "No basic salary set — give them a grade or a salary first.";
                    rows.Add(row);
                    continue;
                }

                row.CurrentBasic = current.Value;
                row.IncrementAmount = Rise(current.Value, dto.Value, dto.Basis);
                row.NewBasic = current.Value + row.IncrementAmount;
                rows.Add(row);
            }

            var eligible = rows.Where(r => r.Blocked == null).ToList();

            return Result<IncrementPreviewDto>.Success(new IncrementPreviewDto
            {
                Rows = rows,
                EligibleCount = eligible.Count,
                BlockedCount = rows.Count - eligible.Count,
                TotalCurrent = eligible.Sum(r => r.CurrentBasic),
                TotalNew = eligible.Sum(r => r.NewBasic),
                MonthlyCostIncrease = eligible.Sum(r => r.IncrementAmount)
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("PreviewAsync failed", ex);
            return Result<IncrementPreviewDto>.Failure("Could not work out the increment.");
        }
    }

    /// <summary>
    /// Proposes the increment. No salary changes here.
    ///
    /// Recomputed rather than taking the figures the browser previewed. The preview is for
    /// the person, not the machine: between looking and pressing the button somebody else may
    /// have changed a salary, and trusting the posted numbers would quietly propose a raise
    /// derived from a stale one.
    /// </summary>
    public async Task<Result<string>> ProposeAsync(ApplyIncrementDto dto)
    {
        try
        {
            var preview = await PreviewAsync(dto);
            if (!preview.IsSuccess) return Result<string>.Failure(preview.ErrorMessage!);

            var rows = preview.Data!.Rows.Where(r => r.Blocked == null).ToList();
            if (!rows.Any())
                return Result<string>.Failure(
                    "Nobody in that selection has a basic salary to increment.");

            // A second pending proposal for the same person would confirm twice and raise
            // them twice, and the second would be computed from a salary that had not moved
            // yet — so it would also be the wrong amount.
            var alreadyPending = (await _uow.SalaryIncrements.FindAsync(i =>
                    i.Status == IncrementStatus.Pending && !i.IsDeleted))
                .Select(i => i.EmployeeId)
                .ToHashSet();

            var clash = rows.Where(r => alreadyPending.Contains(r.EmployeeId)).ToList();
            if (clash.Any())
                return Result<string>.Failure(
                    $"{clash.Count} of them already have an increment waiting to be confirmed "
                    + $"({string.Join(", ", clash.Take(3).Select(c => c.EmployeeCode))}"
                    + (clash.Count > 3 ? "…" : "") + "). "
                    + "Confirm or reject that first.");

            // One batch id across the set, so a group raise reads as one act later rather
            // than as forty unexplained individual ones.
            var batch = rows.Count > 1 ? Guid.NewGuid() : (Guid?)null;

            foreach (var row in rows)
            {
                await _uow.SalaryIncrements.AddAsync(new SalaryIncrement
                {
                    EmployeeId = row.EmployeeId,
                    EffectiveDate = dto.EffectiveDate.Date,
                    PreviousBasic = row.CurrentBasic,
                    NewBasic = row.NewBasic,
                    IncrementValue = dto.Value,
                    Basis = dto.Basis,
                    Reason = dto.Reason,
                    BatchId = batch,
                    Status = IncrementStatus.Pending,
                    CreatedBy = _currentUser.UserId,
                    CreatedAt = DateTime.Now
                });
            }

            await _uow.SaveChangesAsync();

            var summary = $"{rows.Count} increment(s) proposed by "
                        + (dto.Basis == IncrementBasis.Percentage ? $"{dto.Value:0.##}%" : $"{dto.Value:N2}")
                        + $" from {dto.EffectiveDate:dd-MMM-yyyy}, worth {preview.Data.MonthlyCostIncrease:N2} "
                        + "a month. No salary has changed yet — confirm them on Increment Confirmation.";

            await _audit.LogAsync("Payroll", "ProposeIncrement", _currentUser.UserId,
                "SalaryIncrement", null, null, summary);

            return Result<string>.Success(summary);
        }
        catch (Exception ex)
        {
            AppLogger.Error("ProposeAsync failed", ex);
            return Result<string>.Failure("Could not propose the increment.");
        }
    }

    /// <summary>Everything waiting to be confirmed.</summary>
    public async Task<Result<IEnumerable<IncrementConfirmationRowDto>>> GetPendingAsync()
    {
        try
        {
            var scope = await _scopes.GetDataScopeAsync();

            var employees = (await _uow.Employees.FindAsync(e => !e.IsDeleted))
                .Where(e => scope.Allows(e.Id, e.DepartmentId))
                .ToDictionary(e => e.Id);

            var departments = (await _uow.Departments.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);

            var pending = (await _uow.SalaryIncrements.FindAsync(i =>
                    i.Status == IncrementStatus.Pending && !i.IsDeleted))
                .Where(i => employees.ContainsKey(i.EmployeeId))
                .OrderBy(i => i.EffectiveDate)
                .ThenBy(i => employees[i.EmployeeId].EmployeeCode)
                .Select(i =>
                {
                    var e = employees[i.EmployeeId];
                    return new IncrementConfirmationRowDto
                    {
                        Id = i.Id,
                        EmployeeId = i.EmployeeId,
                        EmployeeCode = e.EmployeeCode ?? "",
                        EmployeeName = e.FullName ?? "",
                        DepartmentName = departments.TryGetValue(e.DepartmentId, out var dn) ? dn : "",
                        BasicSalary = i.PreviousBasic,
                        JoiningDate = e.JoiningDate,
                        // Whole years to the effective date, which is what "due an annual
                        // increment" is judged on — not years to today.
                        YearsOfService = (int)((i.EffectiveDate - e.JoiningDate).TotalDays / 365.25),
                        EffectiveDate = i.EffectiveDate,
                        Condition = i.Reason ?? "—",
                        IncrementAmount = i.NewBasic - i.PreviousBasic,
                        NewBasic = i.NewBasic,
                        BasisDisplay = i.Basis == IncrementBasis.Percentage
                            ? $"{i.IncrementValue:0.##}%" : i.IncrementValue.ToString("N2"),
                        BatchId = i.BatchId,
                        ProposedAt = i.CreatedAt
                    };
                })
                .ToList();

            return Result<IEnumerable<IncrementConfirmationRowDto>>.Success(pending);
        }
        catch (Exception ex)
        {
            AppLogger.Error("GetPendingAsync failed", ex);
            return Result<IEnumerable<IncrementConfirmationRowDto>>.Failure("Could not load pending increments.");
        }
    }

    /// <summary>
    /// Confirms proposals — this is where the salary actually changes.
    ///
    /// The stored PreviousBasic is re-checked against what the employee is paid now. If a
    /// salary moved between proposal and confirmation, the proposed NewBasic was computed
    /// from a figure that no longer exists, and applying it would silently undo whatever the
    /// other change was. Those rows are refused by name rather than quietly recalculated:
    /// the amount somebody approved is the amount that should take effect, or nothing should.
    /// </summary>
    public async Task<Result<string>> ConfirmAsync(List<int> ids)
    {
        try
        {
            if (ids == null || !ids.Any())
                return Result<string>.Failure("Nothing was selected to confirm.");

            var increments = (await _uow.SalaryIncrements.FindAsync(i =>
                    ids.Contains(i.Id) && !i.IsDeleted)).ToList();

            if (!increments.Any()) return Result<string>.Failure("Those increments no longer exist.");

            if (increments.Any(i => i.Status != IncrementStatus.Pending))
                return Result<string>.Failure("Some of those have already been dealt with. Reload the page.");

            // The open payroll month decides what "back-dated" means. Without it there is no
            // reference point, and applying a raise dated three months ago would silently
            // forgive three months of back-pay.
            var period = await _periods.GetCurrentAsync();
            if (!period.IsSuccess || period.Data == null)
                return Result<string>.Failure(
                    "No payroll month is open, so a back-dated increment cannot be worked out. "
                    + "Open one under Payroll Months first.");

            var openYm = period.Data.YearMonth;

            // A raise that starts next month must not take effect now. Refused rather than
            // queued, because queueing needs something to run later and there is no scheduler
            // here — a refusal with the date is honest, an applied-early raise is not.
            var early = increments
                .Where(i => i.EffectiveDate.Year * 100 + i.EffectiveDate.Month > openYm)
                .ToList();

            if (early.Any())
                return Result<string>.Failure(
                    $"{early.Count} of these take effect after {period.Data.MonthDisplay} "
                    + $"(earliest {early.Min(i => i.EffectiveDate):MMMM yyyy}). "
                    + "Confirm them in the month they start, or they would be paid early.");

            var scope = await _scopes.GetDataScopeAsync();
            var employees = (await _uow.Employees.FindAsync(e => !e.IsDeleted)).ToDictionary(e => e.Id);
            var infos = (await _uow.EmployeePayrollInfos.GetAllAsync()).ToDictionary(i => i.EmployeeId);
            var grades = (await _uow.SalaryGrades.GetAllAsync()).ToDictionary(g => g.Id);

            var stale = new List<string>();

            foreach (var inc in increments)
            {
                if (!employees.TryGetValue(inc.EmployeeId, out var emp) ||
                    !scope.Allows(emp.Id, emp.DepartmentId))
                    return Result<string>.Failure("One of those employees is outside your access.");

                infos.TryGetValue(inc.EmployeeId, out var info);

                decimal? gradeBasic = info?.SalaryGradeId != null
                    && grades.TryGetValue(info.SalaryGradeId.Value, out var g) ? g.BasicSalary : null;

                var currentBasic = info?.BasicSalaryOverride ?? gradeBasic ?? 0m;

                if (currentBasic != inc.PreviousBasic)
                    stale.Add($"{emp.EmployeeCode} (was {inc.PreviousBasic:N2}, now {currentBasic:N2})");
            }

            if (stale.Any())
                return Result<string>.Failure(
                    "These salaries changed after the increment was proposed, so the approved "
                    + "figure no longer follows on: " + string.Join("; ", stale.Take(5))
                    + (stale.Count > 5 ? $" and {stale.Count - 5} more" : "")
                    + ". Reject and re-propose them.");

            foreach (var inc in increments)
            {
                if (!infos.TryGetValue(inc.EmployeeId, out var info))
                {
                    info = new EmployeePayrollInfo
                    {
                        EmployeeId = inc.EmployeeId,
                        CreatedBy = _currentUser.UserId,
                        CreatedAt = DateTime.Now
                    };
                    await _uow.EmployeePayrollInfos.AddAsync(info);
                }

                info.BasicSalaryOverride = inc.NewBasic;
                info.ModifiedBy = _currentUser.UserId;
                info.ModifiedAt = DateTime.Now;
                if (info.Id != 0) await _uow.EmployeePayrollInfos.UpdateAsync(info);

                // Back-pay for the months already paid at the old rate. Computed here, once,
                // and left unpaid until a payroll run picks it up.
                inc.ArrearsMonths = ArrearsMonthsBetween(inc.EffectiveDate, openYm);
                inc.ArrearsAmount = inc.ArrearsMonths > 0
                    ? Math.Round((inc.NewBasic - inc.PreviousBasic) * inc.ArrearsMonths, 2,
                                 MidpointRounding.AwayFromZero)
                    : 0m;

                inc.Status = IncrementStatus.Confirmed;
                inc.ConfirmedAt = DateTime.Now;
                inc.ConfirmedBy = _currentUser.UserId;
                inc.ModifiedBy = _currentUser.UserId;
                inc.ModifiedAt = DateTime.Now;
                await _uow.SalaryIncrements.UpdateAsync(inc);
            }

            await _uow.SaveChangesAsync();

            var total = increments.Sum(i => i.NewBasic - i.PreviousBasic);
            var arrears = increments.Sum(i => i.ArrearsAmount);

            var summary = $"{increments.Count} increment(s) confirmed. "
                        + $"Monthly cost up {total:N2}."
                        + (arrears > 0
                            ? $" Back-pay of {arrears:N2} is owed for earlier months and will "
                              + $"be paid in {period.Data.MonthDisplay}."
                            : "");

            await _audit.LogAsync("Payroll", "ConfirmIncrement", _currentUser.UserId,
                "SalaryIncrement", null, null, summary);

            return Result<string>.Success(summary);
        }
        catch (Exception ex)
        {
            AppLogger.Error("ConfirmAsync failed", ex);
            return Result<string>.Failure("Could not confirm the increments.");
        }
    }

    /// <summary>Turns proposals down. Kept rather than deleted, with the reason.</summary>
    public async Task<Result<string>> RejectAsync(List<int> ids, string reason)
    {
        try
        {
            if (ids == null || !ids.Any())
                return Result<string>.Failure("Nothing was selected to reject.");

            if (string.IsNullOrWhiteSpace(reason))
                return Result<string>.Failure("Give a reason for turning these down.");

            var increments = (await _uow.SalaryIncrements.FindAsync(i =>
                    ids.Contains(i.Id) && !i.IsDeleted)).ToList();

            if (increments.Any(i => i.Status != IncrementStatus.Pending))
                return Result<string>.Failure("Some of those have already been dealt with. Reload the page.");

            foreach (var inc in increments)
            {
                inc.Status = IncrementStatus.Rejected;
                inc.RejectionReason = reason;
                inc.ModifiedBy = _currentUser.UserId;
                inc.ModifiedAt = DateTime.Now;
                await _uow.SalaryIncrements.UpdateAsync(inc);
            }

            await _uow.SaveChangesAsync();

            var summary = $"{increments.Count} increment(s) rejected: {reason}";
            await _audit.LogAsync("Payroll", "RejectIncrement", _currentUser.UserId,
                "SalaryIncrement", null, null, summary);

            return Result<string>.Success(summary);
        }
        catch (Exception ex)
        {
            AppLogger.Error("RejectAsync failed", ex);
            return Result<string>.Failure("Could not reject the increments.");
        }
    }

    public async Task<Result<IEnumerable<SalaryIncrementDto>>> GetHistoryAsync(int? employeeId)
    {
        try
        {
            var scope = await _scopes.GetDataScopeAsync();

            var employees = (await _uow.Employees.FindAsync(e => !e.IsDeleted))
                .Where(e => scope.Allows(e.Id, e.DepartmentId))
                .ToDictionary(e => e.Id);

            var increments = (await _uow.SalaryIncrements.FindAsync(i => !i.IsDeleted))
                .Where(i => employees.ContainsKey(i.EmployeeId))
                .Where(i => employeeId == null || i.EmployeeId == employeeId)
                .OrderByDescending(i => i.EffectiveDate)
                .ThenByDescending(i => i.Id)
                .Take(500)
                .Select(i => new SalaryIncrementDto
                {
                    Id = i.Id,
                    EmployeeId = i.EmployeeId,
                    EmployeeCode = employees[i.EmployeeId].EmployeeCode ?? "",
                    EmployeeName = employees[i.EmployeeId].FullName ?? "",
                    EffectiveDate = i.EffectiveDate,
                    PreviousBasic = i.PreviousBasic,
                    NewBasic = i.NewBasic,
                    IncrementValue = i.IncrementValue,
                    Basis = i.Basis,
                    BasisDisplay = i.Basis == IncrementBasis.Percentage
                        ? $"{i.IncrementValue:0.##}%" : i.IncrementValue.ToString("N2"),
                    Status = i.Status,
                    StatusDisplay = i.Status switch
                    {
                        IncrementStatus.Pending => "Awaiting confirmation",
                        IncrementStatus.Rejected => "Rejected",
                        _ => "Confirmed"
                    },
                    Reason = i.Reason,
                    BatchId = i.BatchId
                })
                .ToList();

            return Result<IEnumerable<SalaryIncrementDto>>.Success(increments);
        }
        catch (Exception ex)
        {
            AppLogger.Error("GetHistoryAsync failed", ex);
            return Result<IEnumerable<SalaryIncrementDto>>.Failure("Could not load the history.");
        }
    }
}

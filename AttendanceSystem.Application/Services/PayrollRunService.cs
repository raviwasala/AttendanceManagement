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
/// Runs a payroll month: gathers every input, hands each employee to
/// <see cref="PayrollCalculator"/>, and stores the result.
///
/// This class does the fetching and the storing. It does no arithmetic beyond assembling
/// inputs — every figure that reaches a payslip is decided in the calculator, which is pure
/// and tested. The split is the point: when a payslip is wrong, the question is whether the
/// wrong number was fetched or the right number was miscalculated, and those are answered in
/// two different files.
///
/// A run is repeatable while the period is Draft and refused once it is Approved. The first
/// runs of any month find data problems — a missing grade, an employee with no bank — and
/// each fix wants a clean re-run rather than a patch.
/// </summary>
public class PayrollRunService : IPayrollRunService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _currentUser;
    private readonly IApprovalScopeService _scopes;

    public PayrollRunService(IUnitOfWork uow, IAuditService audit,
                             ICurrentUserContext currentUser, IApprovalScopeService scopes)
    {
        _uow = uow;
        _audit = audit;
        _currentUser = currentUser;
        _scopes = scopes;
    }

    public async Task<Result<PayrollRunResultDto>> RunAsync(int payrollPeriodId)
    {
        try
        {
            var period = await _uow.PayrollPeriods.GetByIdAsync(payrollPeriodId);
            if (period == null) return Result<PayrollRunResultDto>.Failure("That payroll month no longer exists.");

            if (period.Status != PayrollStatus.Draft)
                return Result<PayrollRunResultDto>.Failure(
                    $"{MonthName(period)} is {period.Status}. Reopen it before running payroll again — "
                    + "re-running an approved month would change figures that have been signed off.");

            var yearMonth = period.Year * 100 + period.Month;
            var monthStart = new DateTime(period.Year, period.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var result = new PayrollRunResultDto
            {
                PayrollPeriodId = period.Id,
                MonthDisplay = MonthName(period)
            };

            // ── Everything the month needs, loaded once ───────────────────────
            //
            // A few hundred employees against a dozen lookups is a few thousand round trips
            // if each row fetches its own. Loaded up front and indexed instead.

            var scope = await _scopes.GetDataScopeAsync();

            var employees = (await _uow.Employees.FindAsync(e => !e.IsDeleted))
                .Where(e => scope.Allows(e.Id, e.DepartmentId))
                .Where(e => e.IsActive || e.ResignationDate >= monthStart)   // leavers are still paid
                .OrderBy(e => e.EmployeeCode)
                .ToList();

            var infos = (await _uow.EmployeePayrollInfos.GetAllAsync()).ToDictionary(i => i.EmployeeId);
            var grades = (await _uow.SalaryGrades.GetAllAsync()).ToDictionary(g => g.Id);
            var components = (await _uow.SalaryComponents.GetAllAsync()).ToDictionary(c => c.Id);
            var departments = (await _uow.Departments.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);
            var branches = (await _uow.BranchPayrollSettings.GetAllAsync()).ToDictionary(b => b.BranchId);
            var bankBranches = (await _uow.BankBranches.GetAllAsync()).ToDictionary(b => b.Id);
            var banks = (await _uow.Banks.GetAllAsync()).ToDictionary(b => b.Id, b => b.Name);

            var standing = (await _uow.EmployeeSalaryComponents.FindAsync(c => !c.IsDeleted))
                .Where(c => c.EffectiveFrom <= monthEnd && (c.EffectiveTo == null || c.EffectiveTo >= monthStart))
                .GroupBy(c => c.EmployeeId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var oneOffs = (await _uow.MonthlyTransactions.FindAsync(t =>
                    t.YearMonth == yearMonth && !t.IsDeleted))
                .GroupBy(t => t.EmployeeId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var attendance = (await _uow.Attendance.FindAsync(a =>
                    a.AttendanceDate >= monthStart && a.AttendanceDate <= monthEnd && !a.IsDeleted))
                .GroupBy(a => a.EmployeeId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Approved overtime only. Pending or rejected claims are not pay.
            var overtime = (await _uow.OvertimeRecords.FindAsync(o =>
                    o.OvertimeDate >= monthStart && o.OvertimeDate <= monthEnd && !o.IsDeleted))
                .Where(o => o.Status == OvertimeStatus.Approved)
                .GroupBy(o => o.EmployeeId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var loans = (await _uow.EmployeeLoans.FindAsync(l =>
                    l.Status == LoanStatus.Active && !l.IsDeleted))
                .GroupBy(l => l.EmployeeId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var otRules = (await _uow.OvertimeRules.GetAllAsync()).ToDictionary(r => r.Id);
            var loanTypes = (await _uow.LoanTypes.GetAllAsync()).ToDictionary(t => t.Id);
            var loanTx = (await _uow.LoanTransactions.FindAsync(t => !t.IsDeleted)).ToList();

            var arrears = (await _uow.SalaryIncrements.FindAsync(i =>
                    i.Status == IncrementStatus.Confirmed && i.ArrearsAmount > 0
                    && i.ArrearsPaidInYearMonth == null && !i.IsDeleted))
                .GroupBy(i => i.EmployeeId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var rate = (await _uow.EpfEtfRates.FindAsync(r => !r.IsDeleted && r.EffectiveFrom <= monthEnd))
                .OrderByDescending(r => r.EffectiveFrom)
                .FirstOrDefault();

            var taxTables = (await _uow.ApitTaxTables.GetAllAsync()).ToList();
            var taxBands = (await _uow.ApitTaxBrackets.FindAsync(b => !b.IsDeleted && b.EffectiveFrom <= monthEnd))
                .GroupBy(b => b.ApitTaxTableId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Last month's payslip carries this month's opening balance.
            var previous = monthStart.AddMonths(-1);
            var previousPeriod = (await _uow.PayrollPeriods.FindAsync(p =>
                    p.Year == previous.Year && p.Month == previous.Month && !p.IsDeleted))
                .FirstOrDefault();

            var broughtForward = previousPeriod == null
                ? new Dictionary<int, decimal>()
                : (await _uow.Payslips.FindAsync(p => p.PayrollPeriodId == previousPeriod.Id && !p.IsDeleted))
                    .Where(p => p.CarriedForward > 0)
                    .ToDictionary(p => p.EmployeeId, p => p.CarriedForward);

            // ── Clear the previous attempt ────────────────────────────────────
            //
            // A re-run replaces rather than adds. Without this a second run would double
            // every payslip in the month, and the register would look plausible.

            foreach (var old in await _uow.Payslips.FindAsync(p => p.PayrollPeriodId == period.Id && !p.IsDeleted))
            {
                old.IsDeleted = true;
                old.ModifiedBy = _currentUser.UserId;
                old.ModifiedAt = DateTime.Now;
                await _uow.Payslips.UpdateAsync(old);
            }

            // ── One employee at a time ────────────────────────────────────────

            foreach (var e in employees)
            {
                infos.TryGetValue(e.Id, out var info);

                if (info?.IsPayrollSuspended == true
                    && (info.SuspendedFrom == null || info.SuspendedFrom <= monthEnd)
                    && (info.SuspendedTo == null || info.SuspendedTo >= monthStart))
                {
                    result.Suspended++;
                    continue;
                }

                decimal? gradeBasic = info?.SalaryGradeId != null
                    && grades.TryGetValue(info.SalaryGradeId.Value, out var g) ? g.BasicSalary : null;

                var basic = info?.BasicSalaryOverride ?? gradeBasic ?? 0m;

                if (basic <= 0m)
                {
                    // Named, not counted. "237 skipped" tells nobody which 237 or why.
                    result.Skipped.Add($"{e.EmployeeCode} {e.FullName} — no basic salary");
                    continue;
                }

                var branch = branches.TryGetValue(e.BranchId, out var b) ? b : null;
                var days = attendance.TryGetValue(e.Id, out var att) ? att : [];

                var input = BuildInput(
                    e, info, basic, branch, days,
                    overtime.TryGetValue(e.Id, out var ot) ? ot : [],
                    standing.TryGetValue(e.Id, out var st) ? st : [],
                    oneOffs.TryGetValue(e.Id, out var oo) ? oo : [],
                    otRules,
                    loans.TryGetValue(e.Id, out var ln) ? ln : [],
                    loanTypes, loanTx,
                    arrears.TryGetValue(e.Id, out var ar) ? ar : [],
                    broughtForward.TryGetValue(e.Id, out var bf) ? bf : 0m,
                    components, rate, taxTables, taxBands, period);

                var calc = PayrollCalculator.Calculate(input);

                var payslip = ToPayslip(period, e, info, calc, input, bankBranches, banks);
                await _uow.Payslips.AddAsync(payslip);

                // Arrears are marked paid against this month, so reopening and re-running
                // cannot pay the same back-pay twice.
                foreach (var a in arrears.TryGetValue(e.Id, out var mine) ? mine : [])
                {
                    a.ArrearsPaidInYearMonth = yearMonth;
                    a.ModifiedBy = _currentUser.UserId;
                    a.ModifiedAt = DateTime.Now;
                    await _uow.SalaryIncrements.UpdateAsync(a);
                }

                result.PayslipCount++;
                result.TotalGross += calc.GrossPay;
                result.TotalDeductions += calc.TotalDeductions;
                result.TotalNet += calc.NetPay;
                result.TotalCostToCompany += calc.CostToCompany;
                if (calc.Notes.Count > 0) result.WithNotes++;
            }

            period.ProcessedAt = DateTime.Now;
            period.ProcessedBy = _currentUser.UserId;
            period.ModifiedBy = _currentUser.UserId;
            period.ModifiedAt = DateTime.Now;
            await _uow.PayrollPeriods.UpdateAsync(period);

            await _uow.SaveChangesAsync();

            await _audit.LogAsync("Payroll", "PayrollRun", _currentUser.UserId,
                "PayrollPeriod", period.Id, null,
                $"{result.MonthDisplay}: {result.PayslipCount} payslip(s), "
                + $"net {result.TotalNet:N2}, {result.Skipped.Count} skipped.");

            return Result<PayrollRunResultDto>.Success(result);
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollRunService.RunAsync", ex);
            return Result<PayrollRunResultDto>.Failure("The payroll run failed. See the log for details.");
        }
    }

    /// <summary>Assembles one employee's month. No arithmetic beyond totalling what was fetched.</summary>
    private static PayrollInput BuildInput(
        Employee e, EmployeePayrollInfo? info, decimal basic, BranchPayrollSettings? branch,
        List<AttendanceLog> days, List<OvertimeRecord> ot,
        List<EmployeeSalaryComponent> standing, List<MonthlyTransaction> oneOffs,
        Dictionary<int, OvertimeRule> otRules,
        List<EmployeeLoan> loans, Dictionary<int, LoanType> loanTypes, List<LoanTransaction> loanTx,
        List<SalaryIncrement> arrears, decimal broughtForward,
        Dictionary<int, SalaryComponent> components, EpfEtfRate? rate,
        List<ApitTaxTable> taxTables, Dictionary<int, List<ApitTaxBracket>> taxBands,
        PayrollPeriod period)
    {
        var daysPerMonth = branch?.DaysPerMonth ?? 30;
        var hoursPerDay = branch?.HoursPerDay ?? 8m;

        var noPayDays = days.Count(d => d.Status == AttendanceStatus.Absent);
        var leaveDays = days.Count(d => d.Status == AttendanceStatus.OnLeave);
        var presentDays = days.Count(d => d.Status is AttendanceStatus.Present or AttendanceStatus.Late);

        // ── Overtime ──────────────────────────────────────────────────────────
        //
        // Priced per record at its own multiplier AND its own divisor, not as one total at an
        // average rate. Two things make that necessary:
        //
        //   - a month mixing normal, weekly-off and holiday overtime has no single multiplier,
        //     and averaging would quietly move money between the categories;
        //
        //   - the divisor belongs to the rule, not to the site. The legacy system this
        //     replaces prices general staff over 25 days and nursing and medical staff over
        //     22.5, and both run in the same month. A single site-wide divisor cannot express
        //     that, and using one would misprice every hour for one of the two groups.
        //
        // The rule's divisor wins; the branch is the fallback for rules that do not set one.
        var otRateBase = basic + standing
            .Where(s => components.TryGetValue(s.SalaryComponentId, out var c) && c.IncludeInOtRate)
            .Sum(s => s.Value);

        var otMinutes = ot.Sum(o => o.ApprovedMinutes ?? 0);

        var otAmount = Math.Round(ot.Sum(o =>
        {
            var rule = o.OvertimeRuleId != null && otRules.TryGetValue(o.OvertimeRuleId.Value, out var r)
                ? r : null;

            var ruleDays = rule?.DaysPerMonth ?? daysPerMonth;
            var ruleHours = rule?.HoursPerDay ?? hoursPerDay;
            var monthlyHours = ruleDays * ruleHours;

            if (monthlyHours <= 0) return 0m;

            // The multiplier is taken from the record rather than the rule: it was captured
            // when the overtime was approved, and a rule edited since must not silently
            // re-price hours somebody already signed off.
            return (o.ApprovedMinutes ?? 0) / 60m * (otRateBase / monthlyHours) * o.RateMultiplier;
        }), 2, MidpointRounding.AwayFromZero);

        // ── Components ────────────────────────────────────────────────────────

        var list = new List<PayComponentInput>();

        foreach (var s in standing)
        {
            if (!components.TryGetValue(s.SalaryComponentId, out var c) || !c.IsActive) continue;

            list.Add(new PayComponentInput
            {
                SalaryComponentId = c.Id, Code = c.Code, Name = c.Name, Type = c.ComponentType,
                // A percentage component is a percentage of basic, resolved here so the
                // calculator never has to know what the value means.
                Amount = c.CalculationType == ComponentCalculationType.PercentOfBasic
                    ? Math.Round(basic * s.Value / 100m, 2, MidpointRounding.AwayFromZero)
                    : s.Value,
                IsEpfLiable = c.IsEpfLiable,
                IsApitLiable = c.IsApitLiable,
                IncludeInGrossPay = c.IncludeInGrossPay,
                IncludeInNoPay = c.IncludeInNoPay,
                IsOneOff = false,
                SortOrder = c.SortOrder
            });
        }

        foreach (var t in oneOffs)
        {
            if (!components.TryGetValue(t.SalaryComponentId, out var c)) continue;

            list.Add(new PayComponentInput
            {
                SalaryComponentId = c.Id, Code = c.Code, Name = c.Name, Type = c.ComponentType,
                Amount = t.Amount,
                IsEpfLiable = c.IsEpfLiable,
                IsApitLiable = c.IsApitLiable,
                IncludeInGrossPay = c.IncludeInGrossPay,
                // A one-off is entered for the month it belongs to and is not pro-rated for
                // absence — a bonus for last quarter does not shrink because somebody was ill
                // in this one.
                IncludeInNoPay = false,
                IsOneOff = true,
                SortOrder = c.SortOrder + 500
            });
        }

        // ── Loans ─────────────────────────────────────────────────────────────

        var instalments = loans.Select(l =>
        {
            var paid = loanTx.Where(t => t.EmployeeLoanId == l.Id).Sum(t => t.Amount);
            var outstanding = l.TotalPayable - paid;
            if (outstanding <= 0m) return null;

            // The last instalment is whatever is left, so a loan clears exactly rather than
            // over-recovering by the rounding on the final payment.
            var due = Math.Min(l.MonthlyInstallment, outstanding);

            return new LoanInstalmentInput
            {
                LoanId = l.Id,
                Code = loanTypes.TryGetValue(l.LoanTypeId, out var lt) ? lt.Code : "LOAN",
                Name = loanTypes.TryGetValue(l.LoanTypeId, out var lt2) ? lt2.Description : "Loan",
                Instalment = due,
                OpeningBalance = outstanding
            };
        }).Where(x => x != null).Select(x => x!).ToList();

        // ── Tax table ─────────────────────────────────────────────────────────

        var table = taxTables.FirstOrDefault(t => t.Id == info?.ApitTaxTableId)
                 ?? taxTables.FirstOrDefault(t => t.TableType == TaxTableType.Monthly && t.IsDefault);

        var bands = table != null && taxBands.TryGetValue(table.Id, out var bs)
            ? bs.OrderBy(x => x.FromAmount)
                .Select(x => new TaxBandInput
                {
                    FromAmount = x.FromAmount, ToAmount = x.ToAmount,
                    RatePercent = x.Rate, Relief = x.Relief
                })
                .ToList()
            : [];

        return new PayrollInput
        {
            Year = period.Year, Month = period.Month,
            WorkingDays = daysPerMonth,
            PresentDays = presentDays,
            LeaveDays = leaveDays,
            NoPayDays = noPayDays,
            OvertimeHours = Math.Round(otMinutes / 60m, 2),
            BasicSalary = basic,
            OvertimeAmount = otAmount,
            Components = list,
            SalaryArrears = arrears.Sum(a => a.ArrearsAmount),

            IsEpfMember = info?.IsEpfMember ?? true,
            IsEtfMember = info?.IsEtfMember ?? true,
            IsApitApplicable = info?.IsApitApplicable ?? true,

            // Branch overrides beat the national rate; the period's captured rate beats a
            // rate table edited halfway through the month.
            EmployeeEpfPercent = info?.EmployeeEpfPercentOverride
                              ?? branch?.EmployeeEpfPercent ?? period.EmployeeEpfPercent,
            EmployerEpfPercent = info?.EmployerEpfPercentOverride
                              ?? branch?.EmployerEpfPercent ?? period.EmployerEpfPercent,
            EmployerEtfPercent = info?.EmployerEtfPercentOverride
                              ?? branch?.EmployerEtfPercent ?? period.EmployerEtfPercent,

            TaxBands = bands,
            AdditionalTaxAmount = info?.AdditionalTaxAmount ?? 0m,

            Loans = instalments,
            BroughtForward = broughtForward,
            CarryForwardMinusSalary = branch?.CarryForwardMinusSalary ?? true,

            DaysPerMonth = daysPerMonth,
            EpfRounding = branch?.EpfRounding ?? RoundingMode.Decimal,
            EtfRounding = branch?.EtfRounding ?? RoundingMode.Decimal,
            NoPayRounding = branch?.NoPayRounding ?? RoundingMode.Decimal,
            RoundOffNetPay = branch?.RoundOffSalaryPayable ?? false,
            RoundNearest = branch?.RoundNearest ?? 1m,

            IsBankTransfer = info?.IsBankTransfer ?? true
        };
    }

    /// <summary>
    /// Copies the calculation onto a payslip.
    ///
    /// Every figure is stored, and the bank details are copied rather than joined: an
    /// employee who changes bank in September must not change where the August payslip says
    /// they were paid.
    /// </summary>
    private Payslip ToPayslip(
        PayrollPeriod period, Employee e, EmployeePayrollInfo? info,
        PayrollResult calc, PayrollInput input,
        Dictionary<int, BankBranch> bankBranches, Dictionary<int, string> banks)
    {
        BankBranch? branch = info?.BankBranchId != null
            && bankBranches.TryGetValue(info.BankBranchId.Value, out var bb) ? bb : null;

        var payslip = new Payslip
        {
            PayrollPeriodId = period.Id,
            EmployeeId = e.Id,

            WorkingDays = (int)input.WorkingDays,
            PresentDays = (int)input.PresentDays,
            LeaveDays = (int)input.LeaveDays,
            NoPayDays = input.NoPayDays,
            OvertimeHours = input.OvertimeHours,

            BasicSalary = calc.BasicSalary,
            NoPayDeduction = calc.NoPayDeduction,
            EarnedBasic = calc.EarnedBasic,
            TotalFixedAllowances = calc.TotalFixedAllowances,
            TotalVariableAllowances = calc.TotalVariableAllowances,
            OvertimeAmount = calc.OvertimeAmount,
            SalaryArrears = calc.SalaryArrears,
            GrossPay = calc.GrossPay,

            EpfLiableEarnings = calc.EpfLiableEarnings,
            EmployeeEpf = calc.EmployeeEpf,
            EmployerEpf = calc.EmployerEpf,
            EmployerEtf = calc.EmployerEtf,

            ApitLiableEarnings = calc.ApitLiableEarnings,
            Apit = calc.Apit,

            StampDuty = calc.StampDuty,
            SrLevy = calc.SrLevy,
            TotalLoanInstalments = calc.TotalLoanInstalments,
            BroughtForward = calc.BroughtForward,
            TotalOtherDeductions = calc.TotalOtherDeductions,
            TotalDeductions = calc.TotalDeductions,

            NetPay = calc.NetPay,
            CarriedForward = calc.CarriedForward,
            CostToCompany = calc.CostToCompany,

            IsBankTransfer = calc.IsBankTransfer,
            Notes = calc.Notes.Count > 0 ? string.Join(" ", calc.Notes) : null,

            BankName = branch != null && banks.TryGetValue(branch.BankId, out var bn) ? bn : null,
            BankBranchName = branch?.Name,
            AccountNumber = info?.AccountNumber,
            EpfNumber = info?.EpfNumber,

            CreatedBy = _currentUser.UserId,
            CreatedAt = DateTime.Now
        };

        foreach (var line in calc.Lines)
        {
            payslip.Lines.Add(new PayslipLine
            {
                SalaryComponentId = line.SalaryComponentId,
                ComponentCode = line.Code,
                ComponentName = line.Name,
                ComponentType = line.Type,
                Amount = line.Amount,
                IsRecurring = !line.IsOneOff,
                IsEpfLiable = line.IsEpfLiable,
                SortOrder = line.SortOrder,
                CreatedBy = _currentUser.UserId,
                CreatedAt = DateTime.Now
            });
        }

        return payslip;
    }

    private static string MonthName(PayrollPeriod p) =>
        new DateTime(p.Year, p.Month, 1).ToString("MMMM yyyy");
}

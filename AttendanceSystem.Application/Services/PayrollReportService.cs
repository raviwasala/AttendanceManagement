using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Domain.Interfaces;

namespace AttendanceSystem.Application.Services;

/// <summary>
/// The three ways a payroll month is read: the register, the summary and the payslip.
///
/// All three read stored payslips and recompute nothing. That is what makes them agree with
/// each other and with what actually left the bank — if any of them re-ran the calculation,
/// a later change to a grade or an allowance would silently rewrite a month that had already
/// been paid, and the copy the employee holds would stop matching the system.
/// </summary>
public class PayrollReportService : IPayrollReportService
{
    private readonly IUnitOfWork _uow;
    private readonly IApprovalScopeService _scopes;

    public PayrollReportService(IUnitOfWork uow, IApprovalScopeService scopes)
    {
        _uow = uow;
        _scopes = scopes;
    }

    /// <summary>
    /// One row per employee, one column per component actually used this month.
    ///
    /// The columns are discovered from the data rather than fixed, so a code introduced this
    /// month appears without anybody editing a report — and a code nobody used does not leave
    /// an empty column to scan past.
    /// </summary>
    public async Task<Result<PayRegisterDto>> GetRegisterAsync(int payrollPeriodId, int? departmentId)
    {
        try
        {
            var period = await _uow.PayrollPeriods.GetByIdAsync(payrollPeriodId);
            if (period == null) return Result<PayRegisterDto>.Failure("That payroll month no longer exists.");

            var scope = await _scopes.GetDataScopeAsync();

            var employees = (await _uow.Employees.FindAsync(e => !e.IsDeleted))
                .Where(e => scope.Allows(e.Id, e.DepartmentId))
                .ToDictionary(e => e.Id);

            var departments = (await _uow.Departments.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);

            var payslips = (await _uow.Payslips.FindAsync(p =>
                    p.PayrollPeriodId == payrollPeriodId && !p.IsDeleted))
                .Where(p => employees.ContainsKey(p.EmployeeId))
                .Where(p => departmentId == null || employees[p.EmployeeId].DepartmentId == departmentId)
                .ToList();

            var ids = payslips.Select(p => p.Id).ToHashSet();
            var lines = (await _uow.PayslipLines.FindAsync(l => !l.IsDeleted))
                .Where(l => ids.Contains(l.PayslipId))
                .ToList();

            var dto = new PayRegisterDto
            {
                PayrollPeriodId = period.Id,
                MonthDisplay = new DateTime(period.Year, period.Month, 1).ToString("MMMM yyyy"),
                StatusDisplay = period.Status switch
                {
                    PayrollStatus.Draft => "Open",
                    PayrollStatus.Approved => "Approved",
                    _ => "Paid"
                }
            };

            // Column order follows the payslip's own sort order, so the register reads in the
            // same sequence as the payslip somebody is checking it against.
            dto.EarningColumns = lines
                .Where(l => l.ComponentType == SalaryComponentType.Earning)
                .GroupBy(l => l.ComponentCode ?? l.ComponentName)
                .OrderBy(g => g.Min(l => l.SortOrder))
                .Select(g => g.Key)
                .ToList();

            dto.DeductionColumns = lines
                .Where(l => l.ComponentType == SalaryComponentType.Deduction)
                .GroupBy(l => l.ComponentCode ?? l.ComponentName)
                .OrderBy(g => g.Min(l => l.SortOrder))
                .Select(g => g.Key)
                .ToList();

            var byPayslip = lines.GroupBy(l => l.PayslipId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var p in payslips.OrderBy(p => employees[p.EmployeeId].EmployeeCode))
            {
                var e = employees[p.EmployeeId];

                var row = new PayRegisterRowDto
                {
                    PayslipId = p.Id,
                    EmployeeId = e.Id,
                    EmployeeCode = e.EmployeeCode ?? "",
                    EmployeeName = e.FullName ?? "",
                    DepartmentName = departments.TryGetValue(e.DepartmentId, out var dn) ? dn : "",

                    EarnedBasic = p.EarnedBasic,
                    NoPayDeduction = p.NoPayDeduction,
                    OvertimeAmount = p.OvertimeAmount,
                    SalaryArrears = p.SalaryArrears,
                    GrossPay = p.GrossPay,

                    EmployeeEpf = p.EmployeeEpf,
                    Apit = p.Apit,
                    TotalLoanInstalments = p.TotalLoanInstalments,
                    TotalOtherDeductions = p.TotalOtherDeductions,
                    BroughtForward = p.BroughtForward,
                    TotalDeductions = p.TotalDeductions,

                    NetPay = p.NetPay,
                    CarriedForward = p.CarriedForward,

                    EmployerEpf = p.EmployerEpf,
                    EmployerEtf = p.EmployerEtf,
                    CostToCompany = p.CostToCompany,

                    IsBankTransfer = p.IsBankTransfer,
                    Notes = p.Notes
                };

                if (byPayslip.TryGetValue(p.Id, out var mine))
                {
                    foreach (var l in mine)
                    {
                        var key = l.ComponentCode ?? l.ComponentName;
                        // Summed rather than assigned: the same code can legitimately appear
                        // twice on one payslip — a standing allowance and a one-off top-up of
                        // the same thing — and the register must show the total, not the last.
                        row.Components[key] = row.Components.TryGetValue(key, out var v)
                            ? v + l.Amount : l.Amount;
                    }
                }

                dto.Rows.Add(row);
            }

            dto.Totals = Total(dto.Rows, dto.EarningColumns.Concat(dto.DeductionColumns));

            dto.BankCount = dto.Rows.Count(r => r.IsBankTransfer);
            dto.CashCount = dto.Rows.Count(r => !r.IsBankTransfer);
            dto.BankTotal = dto.Rows.Where(r => r.IsBankTransfer).Sum(r => r.NetPay);
            dto.CashTotal = dto.Rows.Where(r => !r.IsBankTransfer).Sum(r => r.NetPay);

            return Result<PayRegisterDto>.Success(dto);
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollReportService.GetRegisterAsync", ex);
            return Result<PayRegisterDto>.Failure("Could not build the pay register.");
        }
    }

    private static PayRegisterRowDto Total(List<PayRegisterRowDto> rows, IEnumerable<string> columns)
    {
        var t = new PayRegisterRowDto
        {
            EmployeeCode = "", EmployeeName = $"{rows.Count} employee(s)",
            EarnedBasic = rows.Sum(r => r.EarnedBasic),
            NoPayDeduction = rows.Sum(r => r.NoPayDeduction),
            OvertimeAmount = rows.Sum(r => r.OvertimeAmount),
            SalaryArrears = rows.Sum(r => r.SalaryArrears),
            GrossPay = rows.Sum(r => r.GrossPay),
            EmployeeEpf = rows.Sum(r => r.EmployeeEpf),
            Apit = rows.Sum(r => r.Apit),
            TotalLoanInstalments = rows.Sum(r => r.TotalLoanInstalments),
            TotalOtherDeductions = rows.Sum(r => r.TotalOtherDeductions),
            BroughtForward = rows.Sum(r => r.BroughtForward),
            TotalDeductions = rows.Sum(r => r.TotalDeductions),
            NetPay = rows.Sum(r => r.NetPay),
            CarriedForward = rows.Sum(r => r.CarriedForward),
            EmployerEpf = rows.Sum(r => r.EmployerEpf),
            EmployerEtf = rows.Sum(r => r.EmployerEtf),
            CostToCompany = rows.Sum(r => r.CostToCompany)
        };

        foreach (var c in columns)
            t.Components[c] = rows.Sum(r => r.Components.TryGetValue(c, out var v) ? v : 0m);

        return t;
    }

    /// <summary>
    /// One line per department — the management view, and the source for the GL journal.
    /// </summary>
    public async Task<Result<PaySummaryDto>> GetSummaryAsync(int payrollPeriodId)
    {
        try
        {
            var register = await GetRegisterAsync(payrollPeriodId, null);
            if (!register.IsSuccess) return Result<PaySummaryDto>.Failure(register.ErrorMessage!);

            var r = register.Data!;

            var dto = new PaySummaryDto
            {
                PayrollPeriodId = r.PayrollPeriodId,
                MonthDisplay = r.MonthDisplay,
                Rows = r.Rows
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.DepartmentName)
                                ? "(no department)" : x.DepartmentName)
                    .OrderBy(g => g.Key)
                    .Select(g => new PaySummaryRowDto
                    {
                        DepartmentName = g.Key,
                        Headcount = g.Count(),
                        GrossPay = g.Sum(x => x.GrossPay),
                        EmployeeEpf = g.Sum(x => x.EmployeeEpf),
                        Apit = g.Sum(x => x.Apit),
                        TotalDeductions = g.Sum(x => x.TotalDeductions),
                        NetPay = g.Sum(x => x.NetPay),
                        EmployerEpf = g.Sum(x => x.EmployerEpf),
                        EmployerEtf = g.Sum(x => x.EmployerEtf),
                        CostToCompany = g.Sum(x => x.CostToCompany)
                    })
                    .ToList()
            };

            dto.Totals = new PaySummaryRowDto
            {
                DepartmentName = "All departments",
                Headcount = dto.Rows.Sum(x => x.Headcount),
                GrossPay = dto.Rows.Sum(x => x.GrossPay),
                EmployeeEpf = dto.Rows.Sum(x => x.EmployeeEpf),
                Apit = dto.Rows.Sum(x => x.Apit),
                TotalDeductions = dto.Rows.Sum(x => x.TotalDeductions),
                NetPay = dto.Rows.Sum(x => x.NetPay),
                EmployerEpf = dto.Rows.Sum(x => x.EmployerEpf),
                EmployerEtf = dto.Rows.Sum(x => x.EmployerEtf),
                CostToCompany = dto.Rows.Sum(x => x.CostToCompany)
            };

            return Result<PaySummaryDto>.Success(dto);
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollReportService.GetSummaryAsync", ex);
            return Result<PaySummaryDto>.Failure("Could not build the pay summary.");
        }
    }

    /// <summary>
    /// Every payslip in a month, fully built.
    ///
    /// One call rather than one per employee. Printing 239 payslips is the normal end of a
    /// payroll month, and doing it as 239 requests would take minutes and fail halfway
    /// through often enough to matter — a half-printed run is worse than none, because
    /// nobody can tell which half.
    /// </summary>
    public async Task<Result<IEnumerable<PayslipDto>>> GetPayslipsAsync(int payrollPeriodId, int? departmentId)
    {
        try
        {
            var register = await GetRegisterAsync(payrollPeriodId, departmentId);
            if (!register.IsSuccess) return Result<IEnumerable<PayslipDto>>.Failure(register.ErrorMessage!);

            var list = new List<PayslipDto>();

            foreach (var row in register.Data!.Rows)
            {
                var one = await GetPayslipAsync(row.PayslipId);
                if (one.IsSuccess && one.Data != null) list.Add(one.Data);
            }

            return Result<IEnumerable<PayslipDto>>.Success(list);
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollReportService.GetPayslipsAsync", ex);
            return Result<IEnumerable<PayslipDto>>.Failure("Could not load the payslips.");
        }
    }

    public async Task<Result<PayslipDto>> GetPayslipAsync(int payslipId)
    {
        try
        {
            var p = await _uow.Payslips.GetByIdAsync(payslipId);
            if (p == null || p.IsDeleted) return Result<PayslipDto>.Failure("That payslip no longer exists.");

            var employee = await _uow.Employees.GetByIdAsync(p.EmployeeId);
            if (employee == null) return Result<PayslipDto>.Failure("Employee not found.");

            var scope = await _scopes.GetDataScopeAsync();
            if (!scope.Allows(employee.Id, employee.DepartmentId))
                return Result<PayslipDto>.Failure("That payslip is outside your access.");

            var period = await _uow.PayrollPeriods.GetByIdAsync(p.PayrollPeriodId);
            var departments = (await _uow.Departments.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);
            var designations = (await _uow.Designations.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);

            var lines = (await _uow.PayslipLines.FindAsync(l => l.PayslipId == p.Id && !l.IsDeleted))
                .OrderBy(l => l.SortOrder)
                .ToList();

            var dto = new PayslipDto
            {
                Id = p.Id,
                MonthDisplay = period != null
                    ? new DateTime(period.Year, period.Month, 1).ToString("MMMM yyyy") : "",

                EmployeeCode = employee.EmployeeCode ?? "",
                EmployeeName = employee.FullName ?? "",
                DepartmentName = departments.TryGetValue(employee.DepartmentId, out var dn) ? dn : "",
                DesignationName = designations.TryGetValue(employee.DesignationId, out var dg) ? dg : "",
                EpfNumber = p.EpfNumber,
                JoiningDate = employee.JoiningDate,

                WorkingDays = p.WorkingDays,
                PresentDays = p.PresentDays,
                LeaveDays = p.LeaveDays,
                NoPayDays = p.NoPayDays,
                NoPayDeduction = p.NoPayDeduction,
                OvertimeHours = p.OvertimeHours,

                Earnings = lines.Where(l => l.ComponentType == SalaryComponentType.Earning)
                    .Select(l => new PayslipLineDto
                    { Code = l.ComponentCode ?? "", Name = l.ComponentName, Amount = l.Amount })
                    .ToList(),

                Deductions = lines.Where(l => l.ComponentType == SalaryComponentType.Deduction)
                    .Select(l => new PayslipLineDto
                    { Code = l.ComponentCode ?? "", Name = l.ComponentName, Amount = l.Amount })
                    .ToList(),

                GrossPay = p.GrossPay,
                EmployeeEpf = p.EmployeeEpf,
                Apit = p.Apit,
                BroughtForward = p.BroughtForward,
                TotalDeductions = p.TotalDeductions,
                NetPay = p.NetPay,
                CarriedForward = p.CarriedForward,

                EmployerEpf = p.EmployerEpf,
                EmployerEtf = p.EmployerEtf,

                IsBankTransfer = p.IsBankTransfer,
                BankName = p.BankName,
                BankBranchName = p.BankBranchName,
                AccountNumber = p.AccountNumber,
                Notes = p.Notes
            };

            // A no-pay reduction is shown as a deduction line even though it reduced earnings,
            // because an employee looking at a short payslip needs to see WHY it is short. The
            // basic line already carries the reduced figure, so this is presentation only —
            // it is not added into the deduction total.
            if (p.NoPayDeduction > 0)
                dto.Deductions.Insert(0, new PayslipLineDto
                { Code = "N0001", Name = $"No Pay ({p.NoPayDays:0.##} day(s)) — already deducted above",
                  Amount = 0m });

            return Result<PayslipDto>.Success(dto);
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollReportService.GetPayslipAsync", ex);
            return Result<PayslipDto>.Failure("Could not load the payslip.");
        }
    }
}

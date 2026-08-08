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
/// Which month payroll is currently working on.
///
/// Every data-entry screen needs to agree on this, and until now each one defaulted to
/// today's calendar month — which is wrong for most of the month it matters. On the 3rd of
/// August a clerk is still finishing July: an incentive keyed that morning would have
/// silently landed in an August that nobody has opened yet, and turned up on the wrong
/// payslip a month later. Nothing would have flagged it.
///
/// So the current month is state the system holds, not something derived from the clock or
/// re-chosen on every screen. It moves forward only when somebody closes a month, which is
/// a deliberate act with a date and a name against it.
/// </summary>
public class PayrollPeriodService : IPayrollPeriodService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _currentUser;

    public PayrollPeriodService(IUnitOfWork uow, IAuditService audit, ICurrentUserContext currentUser)
    {
        _uow = uow;
        _audit = audit;
        _currentUser = currentUser;
    }

    private static PayrollPeriodDto ToDto(PayrollPeriod p) => new()
    {
        Id = p.Id,
        Year = p.Year,
        Month = p.Month,
        YearMonth = p.Year * 100 + p.Month,
        MonthDisplay = new DateTime(p.Year, p.Month, 1).ToString("MMMM yyyy"),
        Status = p.Status,
        StatusDisplay = p.Status switch
        {
            PayrollStatus.Draft => "Open",
            PayrollStatus.Approved => "Approved",
            _ => "Paid"
        },
        ProcessedAt = p.ProcessedAt,
        ApprovedAt = p.ApprovedAt,
        Notes = p.Notes
    };

    /// <summary>
    /// The open month, or null when none has been opened.
    ///
    /// Returns null rather than inventing one from today's date. A screen that quietly
    /// invented a month would be back to the bug this class exists to prevent; the caller
    /// is expected to say "no payroll month is open" and offer to open one.
    /// </summary>
    public async Task<Result<PayrollPeriodDto?>> GetCurrentAsync()
    {
        try
        {
            var open = (await _uow.PayrollPeriods.FindAsync(p =>
                    p.Status == PayrollStatus.Draft && !p.IsDeleted))
                .OrderByDescending(p => p.Year * 100 + p.Month)
                .FirstOrDefault();

            return Result<PayrollPeriodDto?>.Success(open == null ? null : ToDto(open));
        }
        catch (Exception ex)
        {
            AppLogger.Error("GetCurrentAsync failed", ex);
            return Result<PayrollPeriodDto?>.Failure("Could not read the payroll month.");
        }
    }

    public async Task<Result<IEnumerable<PayrollPeriodDto>>> GetAllAsync()
    {
        try
        {
            var all = (await _uow.PayrollPeriods.FindAsync(p => !p.IsDeleted))
                .OrderByDescending(p => p.Year * 100 + p.Month)
                .Select(ToDto)
                .ToList();

            return Result<IEnumerable<PayrollPeriodDto>>.Success(all);
        }
        catch (Exception ex)
        {
            AppLogger.Error("GetAllAsync failed", ex);
            return Result<IEnumerable<PayrollPeriodDto>>.Failure("Could not load payroll months.");
        }
    }

    /// <summary>
    /// Opens a month for entry.
    ///
    /// Only one may be open at a time. Two open months would put the question "which month
    /// does this incentive belong to?" back on every screen, which is the thing this class
    /// removes.
    /// </summary>
    public async Task<Result<PayrollPeriodDto>> OpenAsync(OpenPayrollPeriodDto dto)
    {
        try
        {
            if (dto.Month < 1 || dto.Month > 12)
                return Result<PayrollPeriodDto>.Failure("Month must be between 1 and 12.");

            var all = (await _uow.PayrollPeriods.FindAsync(p => !p.IsDeleted)).ToList();

            var open = all.FirstOrDefault(p => p.Status == PayrollStatus.Draft);
            if (open != null)
                return Result<PayrollPeriodDto>.Failure(
                    $"{new DateTime(open.Year, open.Month, 1):MMMM yyyy} is still open. " +
                    "Close it before opening another — two open months would make it ambiguous " +
                    "which one a new entry belongs to.");

            var ym = dto.Year * 100 + dto.Month;

            if (all.Any(p => p.Year * 100 + p.Month == ym))
                return Result<PayrollPeriodDto>.Failure(
                    $"{new DateTime(dto.Year, dto.Month, 1):MMMM yyyy} has been run before. " +
                    "Reopen that month rather than creating a second one.");

            // Refuses to skip a month. A gap is nearly always a typo in the year, and it would
            // not be noticed until somebody looked for the missing month's payslips.
            var last = all.OrderByDescending(p => p.Year * 100 + p.Month).FirstOrDefault();
            if (last != null)
            {
                var expected = new DateTime(last.Year, last.Month, 1).AddMonths(1);
                if (dto.Year != expected.Year || dto.Month != expected.Month)
                    return Result<PayrollPeriodDto>.Failure(
                        $"The next payroll month is {expected:MMMM yyyy}. " +
                        $"Opening {new DateTime(dto.Year, dto.Month, 1):MMMM yyyy} would leave a gap.");
            }

            var rates = (await _uow.EpfEtfRates.FindAsync(r => !r.IsDeleted))
                .OrderByDescending(r => r.EffectiveFrom)
                .FirstOrDefault();

            var period = new PayrollPeriod
            {
                Year = dto.Year,
                Month = dto.Month,
                Status = PayrollStatus.Draft,
                // Captured now so the month is costed on the rates in force when it opened,
                // even if somebody edits the rate table halfway through.
                EmployeeEpfPercent = rates?.EmployeeEpfPercent ?? 8m,
                EmployerEpfPercent = rates?.EmployerEpfPercent ?? 12m,
                EmployerEtfPercent = rates?.EmployerEtfPercent ?? 3m,
                Notes = dto.Notes,
                CreatedBy = _currentUser.UserId,
                CreatedAt = DateTime.Now
            };

            await _uow.PayrollPeriods.AddAsync(period);
            await _uow.SaveChangesAsync();

            await _audit.LogAsync("Payroll", "OpenPeriod", _currentUser.UserId,
                "PayrollPeriod", period.Id, null,
                $"Opened {new DateTime(dto.Year, dto.Month, 1):MMMM yyyy}");

            return Result<PayrollPeriodDto>.Success(ToDto(period));
        }
        catch (Exception ex)
        {
            AppLogger.Error("OpenAsync failed", ex);
            return Result<PayrollPeriodDto>.Failure("Could not open the payroll month.");
        }
    }

    /// <summary>
    /// Closes the open month and opens the next one.
    ///
    /// Done as one action because that is what actually happens: work does not stop while
    /// somebody remembers to open September. Leaving no month open would have every entry
    /// screen refusing input until an administrator noticed.
    /// </summary>
    public async Task<Result<PayrollPeriodDto>> CloseAndAdvanceAsync()
    {
        try
        {
            var open = (await _uow.PayrollPeriods.FindAsync(p =>
                    p.Status == PayrollStatus.Draft && !p.IsDeleted))
                .OrderByDescending(p => p.Year * 100 + p.Month)
                .FirstOrDefault();

            if (open == null)
                return Result<PayrollPeriodDto>.Failure("No payroll month is open.");

            open.Status = PayrollStatus.Approved;
            open.ApprovedAt = DateTime.Now;
            open.ApprovedBy = _currentUser.UserId;
            open.ModifiedBy = _currentUser.UserId;
            open.ModifiedAt = DateTime.Now;
            await _uow.PayrollPeriods.UpdateAsync(open);

            var next = new DateTime(open.Year, open.Month, 1).AddMonths(1);

            var rates = (await _uow.EpfEtfRates.FindAsync(r => !r.IsDeleted))
                .OrderByDescending(r => r.EffectiveFrom)
                .FirstOrDefault();

            var period = new PayrollPeriod
            {
                Year = next.Year,
                Month = next.Month,
                Status = PayrollStatus.Draft,
                EmployeeEpfPercent = rates?.EmployeeEpfPercent ?? 8m,
                EmployerEpfPercent = rates?.EmployerEpfPercent ?? 12m,
                EmployerEtfPercent = rates?.EmployerEtfPercent ?? 3m,
                CreatedBy = _currentUser.UserId,
                CreatedAt = DateTime.Now
            };

            await _uow.PayrollPeriods.AddAsync(period);
            await _uow.SaveChangesAsync();

            await _audit.LogAsync("Payroll", "ClosePeriod", _currentUser.UserId,
                "PayrollPeriod", open.Id, null,
                $"Closed {new DateTime(open.Year, open.Month, 1):MMMM yyyy}, opened {next:MMMM yyyy}");

            return Result<PayrollPeriodDto>.Success(ToDto(period));
        }
        catch (Exception ex)
        {
            AppLogger.Error("CloseAndAdvanceAsync failed", ex);
            return Result<PayrollPeriodDto>.Failure("Could not close the payroll month.");
        }
    }

    /// <summary>
    /// Reopens a closed month. Recorded, because reopening a month that has been paid is
    /// how figures and a bank file stop agreeing.
    /// </summary>
    public async Task<Result> ReopenAsync(int id, string reason)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reason))
                return Result.Failure("Give a reason — reopening a closed month is not routine.");

            var period = await _uow.PayrollPeriods.GetByIdAsync(id);
            if (period == null) return Result.Failure("That payroll month no longer exists.");

            if (period.Status == PayrollStatus.Draft)
                return Result.Failure("That month is already open.");

            if (period.Status == PayrollStatus.Paid)
                return Result.Failure(
                    "That month has been paid. Reopening it would put the payslips out of step " +
                    "with what actually left the bank — post a correction in the open month instead.");

            var open = (await _uow.PayrollPeriods.FindAsync(p =>
                p.Status == PayrollStatus.Draft && !p.IsDeleted)).ToList();

            if (open.Any())
                return Result.Failure(
                    $"{new DateTime(open[0].Year, open[0].Month, 1):MMMM yyyy} is open. " +
                    "Only one month may be open at a time.");

            period.Status = PayrollStatus.Draft;
            period.ApprovedAt = null;
            period.ApprovedBy = null;
            period.Notes = (period.Notes + "\nReopened: " + reason).Trim();
            period.ModifiedBy = _currentUser.UserId;
            period.ModifiedAt = DateTime.Now;

            await _uow.PayrollPeriods.UpdateAsync(period);
            await _uow.SaveChangesAsync();

            await _audit.LogAsync("Payroll", "ReopenPeriod", _currentUser.UserId,
                "PayrollPeriod", period.Id, null,
                $"Reopened {new DateTime(period.Year, period.Month, 1):MMMM yyyy}: {reason}");

            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("ReopenAsync failed", ex);
            return Result.Failure("Could not reopen the payroll month.");
        }
    }
}

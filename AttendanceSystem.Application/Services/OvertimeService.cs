using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Constants;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Domain.Interfaces;

namespace AttendanceSystem.Application.Services;

/// <summary>
/// Overtime management.
///
/// The division of labour is deliberate. A <see cref="Shift"/> answers "do minutes past the end
/// count, and from when" — that is a working-time question and it belongs with the shift, where
/// <see cref="AttendanceCalculator"/> already answers it. An <see cref="OvertimeRule"/> answers
/// "what are those minutes worth, how few are worth claiming, how many are allowed" — a finance
/// question that changes on its own schedule.
///
/// Claims are generated from attendance rather than typed in, so the hours on a payslip and the
/// hours on the attendance screen cannot drift apart. Regenerating is safe: a claim someone has
/// already approved or rejected is never overwritten.
/// </summary>
public class OvertimeService : IOvertimeService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _currentUser;

    public OvertimeService(IUnitOfWork uow, IAuditService audit, ICurrentUserContext currentUser)
    {
        _uow = uow;
        _audit = audit;
        _currentUser = currentUser;
    }

    // ── Rules ────────────────────────────────────────────────────────────────────

    public async Task<Result<IEnumerable<OvertimeRuleDto>>> GetRulesAsync()
    {
        try
        {
            var rules = (await _uow.OvertimeRules.GetAllAsync()).Where(r => !r.IsDeleted).ToList();
            var departments = (await _uow.Departments.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);
            var shifts = (await _uow.Shifts.GetAllAsync()).ToDictionary(s => s.Id, s => s.Name);

            var dtos = rules
                .OrderBy(r => r.Priority).ThenBy(r => r.Name)
                .Select(r => Map(r, departments, shifts))
                .ToList();

            return Result<IEnumerable<OvertimeRuleDto>>.Success(dtos);
        }
        catch (Exception ex)
        {
            AppLogger.Error("OvertimeService.GetRulesAsync", ex);
            return Result<IEnumerable<OvertimeRuleDto>>.Failure("Failed to load overtime rules.");
        }
    }

    public async Task<Result<OvertimeRuleDto>> GetRuleByIdAsync(int id)
    {
        try
        {
            var rule = await _uow.OvertimeRules.GetByIdAsync(id);
            if (rule == null || rule.IsDeleted)
                return Result<OvertimeRuleDto>.Failure("Overtime rule not found.");

            var departments = (await _uow.Departments.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);
            var shifts = (await _uow.Shifts.GetAllAsync()).ToDictionary(s => s.Id, s => s.Name);
            return Result<OvertimeRuleDto>.Success(Map(rule, departments, shifts));
        }
        catch (Exception ex)
        {
            AppLogger.Error("OvertimeService.GetRuleByIdAsync", ex);
            return Result<OvertimeRuleDto>.Failure("Failed to load the overtime rule.");
        }
    }

    public async Task<Result<OvertimeRuleDto>> SaveRuleAsync(SaveOvertimeRuleDto dto)
    {
        try
        {
            var name = dto.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(name))
                return Result<OvertimeRuleDto>.Failure("Rule name is required.");

            if (dto.MaxMinutesPerDay is > 0 && dto.MaxMinutesPerDay < dto.MinimumMinutes)
                return Result<OvertimeRuleDto>.Failure(
                    "The daily maximum cannot be below the minimum — no claim could ever qualify.");

            var existing = (await _uow.OvertimeRules.GetAllAsync())
                .FirstOrDefault(r => !r.IsDeleted && r.Id != dto.Id &&
                                     string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                return Result<OvertimeRuleDto>.Failure($"A rule named '{name}' already exists.");

            OvertimeRule entity;
            if (dto.Id == 0)
            {
                entity = new OvertimeRule
                {
                    CreatedBy = _currentUser.UserId,
                    CreatedAt = DateTime.Now
                };
                ApplyTo(entity, dto, name);
                await _uow.OvertimeRules.AddAsync(entity);
            }
            else
            {
                entity = (await _uow.OvertimeRules.GetByIdAsync(dto.Id))!;
                if (entity == null || entity.IsDeleted)
                    return Result<OvertimeRuleDto>.Failure("Overtime rule not found.");

                ApplyTo(entity, dto, name);
                entity.ModifiedBy = _currentUser.UserId;
                entity.ModifiedAt = DateTime.Now;
                await _uow.OvertimeRules.UpdateAsync(entity);
            }

            await _uow.SaveChangesAsync();
            await _audit.LogAsync(AppConstants.Modules.Overtime, dto.Id == 0 ? "CreateRule" : "UpdateRule",
                _currentUser.UserId, nameof(OvertimeRule), entity.Id);

            return await GetRuleByIdAsync(entity.Id);
        }
        catch (Exception ex)
        {
            AppLogger.Error("OvertimeService.SaveRuleAsync", ex);
            return Result<OvertimeRuleDto>.Failure("Failed to save the overtime rule.");
        }
    }

    public async Task<Result> DeleteRuleAsync(int id)
    {
        try
        {
            var rule = await _uow.OvertimeRules.GetByIdAsync(id);
            if (rule == null || rule.IsDeleted) return Result.Failure("Overtime rule not found.");

            // Claims keep their copied RuleName and RateMultiplier, so deleting a rule cannot
            // restate overtime that was already approved under it.
            rule.IsDeleted = true;
            rule.ModifiedBy = _currentUser.UserId;
            rule.ModifiedAt = DateTime.Now;
            await _uow.OvertimeRules.UpdateAsync(rule);
            await _uow.SaveChangesAsync();

            await _audit.LogAsync(AppConstants.Modules.Overtime, "DeleteRule",
                _currentUser.UserId, nameof(OvertimeRule), id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("OvertimeService.DeleteRuleAsync", ex);
            return Result.Failure("Failed to delete the overtime rule.");
        }
    }

    // ── Generation ───────────────────────────────────────────────────────────────

    public async Task<Result<GenerateOvertimeResultDto>> GenerateAsync(GenerateOvertimeDto dto)
    {
        try
        {
            var from = dto.From.Date;
            var to = dto.To.Date;
            if (to < from) (from, to) = (to, from);
            if ((to - from).TotalDays > 366)
                return Result<GenerateOvertimeResultDto>.Failure("Generate at most one year at a time.");

            var employees = (await _uow.Employees.FindAsync(e => !e.IsDeleted &&
                    (dto.DepartmentId == null || e.DepartmentId == dto.DepartmentId) &&
                    (dto.EmployeeId == null || e.Id == dto.EmployeeId)))
                .ToDictionary(e => e.Id);
            if (employees.Count == 0)
                return Result<GenerateOvertimeResultDto>.Success(new GenerateOvertimeResultDto());

            // Any log with overtime on it. Zero-minute days are not claims and are skipped
            // rather than stored as empty rows nobody will ever action.
            var logs = (await _uow.Attendance.FindAsync(a => !a.IsDeleted &&
                    a.AttendanceDate >= from && a.AttendanceDate <= to &&
                    a.OvertimeMinutes != null && a.OvertimeMinutes > 0))
                .Where(a => employees.ContainsKey(a.EmployeeId))
                .ToList();

            var rules = (await _uow.OvertimeRules.GetAllAsync())
                .Where(r => !r.IsDeleted && r.IsActive)
                .OrderBy(r => r.Priority).ThenBy(r => r.Id)
                .ToList();

            var shifts = (await _uow.Shifts.GetAllAsync()).ToDictionary(s => s.Id);
            var holidays = (await _uow.Holidays.FindAsync(h => !h.IsDeleted &&
                    h.HolidayDate >= from && h.HolidayDate <= to))
                .Select(h => h.HolidayDate.Date)
                .ToHashSet();

            var assignments = (await _uow.EmployeeShifts.FindAsync(es => !es.IsDeleted &&
                    es.EffectiveFrom <= to && (es.EffectiveTo == null || es.EffectiveTo >= from)))
                .ToList();

            var existing = (await _uow.OvertimeRecords.FindAsync(r => !r.IsDeleted &&
                    r.OvertimeDate >= from && r.OvertimeDate <= to))
                .ToDictionary(r => (r.EmployeeId, r.OvertimeDate.Date));

            var result = new GenerateOvertimeResultDto { Scanned = logs.Count };
            var now = DateTime.Now;

            foreach (var log in logs)
            {
                var employee = employees[log.EmployeeId];
                var date = log.AttendanceDate.Date;

                var shift = ResolveShift(assignments, shifts, log, date);
                var dayType = ClassifyDay(date, shift, holidays);
                var rule = MatchRule(rules, employee.DepartmentId, shift?.Id, dayType);

                if (rule == null) { result.SkippedNoRule++; continue; }

                var claimed = ApplyRule(log.OvertimeMinutes!.Value, rule);
                if (claimed <= 0) { result.SkippedBelowMinimum++; continue; }

                if (existing.TryGetValue((log.EmployeeId, date), out var record))
                {
                    // A decision is a human act; regenerating must never undo one.
                    if (record.Status != OvertimeStatus.Pending) { result.SkippedAlreadyDecided++; continue; }

                    record.AttendanceLogId = log.Id;
                    record.ShiftId = shift?.Id;
                    record.RawMinutes = log.OvertimeMinutes.Value;
                    record.ClaimedMinutes = claimed;
                    record.OvertimeRuleId = rule.Id;
                    record.RuleName = rule.Name;
                    record.RateMultiplier = rule.RateMultiplier;
                    record.DayType = dayType;
                    record.ModifiedBy = _currentUser.UserId;
                    record.ModifiedAt = now;

                    if (!rule.RequiresApproval) AutoApprove(record, claimed, now, result);

                    await _uow.OvertimeRecords.UpdateAsync(record);
                    result.Updated++;
                }
                else
                {
                    record = new OvertimeRecord
                    {
                        EmployeeId = log.EmployeeId,
                        OvertimeDate = date,
                        AttendanceLogId = log.Id,
                        ShiftId = shift?.Id,
                        RawMinutes = log.OvertimeMinutes.Value,
                        ClaimedMinutes = claimed,
                        OvertimeRuleId = rule.Id,
                        RuleName = rule.Name,
                        RateMultiplier = rule.RateMultiplier,
                        DayType = dayType,
                        Status = OvertimeStatus.Pending,
                        CreatedBy = _currentUser.UserId,
                        CreatedAt = now
                    };

                    if (!rule.RequiresApproval) AutoApprove(record, claimed, now, result);

                    await _uow.OvertimeRecords.AddAsync(record);
                    existing[(log.EmployeeId, date)] = record;
                    result.Created++;
                }
            }

            await _uow.SaveChangesAsync();
            await _audit.LogAsync(AppConstants.Modules.Overtime, "Generate", _currentUser.UserId,
                newValues: $"{from:yyyy-MM-dd}..{to:yyyy-MM-dd}: {result.Created} created, {result.Updated} updated");

            return Result<GenerateOvertimeResultDto>.Success(result);
        }
        catch (Exception ex)
        {
            AppLogger.Error("OvertimeService.GenerateAsync", ex);
            return Result<GenerateOvertimeResultDto>.Failure("Failed to generate overtime claims.");
        }
    }

    // ── Register ─────────────────────────────────────────────────────────────────

    public async Task<Result<OvertimeRegisterDto>> GetRegisterAsync(DateTime from, DateTime to,
        int? employeeId = null, int? departmentId = null, OvertimeStatus? status = null,
        PageRequest? page = null)
    {
        try
        {
            var start = from.Date;
            var end = to.Date;
            if (end < start) (start, end) = (end, start);

            // Page 1 of everything when the caller does not ask for a page — which is what the
            // summary does, since it has to aggregate the whole range itself.
            page ??= new PageRequest { Page = 1, PageSize = 0 };

            // Department lives on the employee, not the claim, so it is resolved to ids first
            // and pushed into the query rather than filtered out after the fact.
            IReadOnlyCollection<int>? departmentEmployeeIds = null;
            if (departmentId.HasValue)
            {
                departmentEmployeeIds = (await _uow.Employees.FindAsync(
                        e => !e.IsDeleted && e.DepartmentId == departmentId.Value))
                    .Select(e => e.Id)
                    .ToList();
            }

            var (records, totals) = await _uow.OvertimeRecords.GetRegisterPageAsync(
                start, end, employeeId, departmentEmployeeIds, status, page.Skip, page.PageSize);

            records = records.ToList();

            var employees = (await _uow.Employees.GetAllAsync()).ToDictionary(e => e.Id);
            var departments = (await _uow.Departments.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);
            var shifts = (await _uow.Shifts.GetAllAsync()).ToDictionary(s => s.Id, s => s.Name);
            var users = (await _uow.Users.GetAllAsync()).ToDictionary(u => u.Id, u => u.Username);

            var logIds = records.Where(r => r.AttendanceLogId.HasValue)
                                .Select(r => r.AttendanceLogId!.Value).ToHashSet();
            var logs = logIds.Count == 0
                ? new Dictionary<int, AttendanceLog>()
                : (await _uow.Attendance.FindAsync(a => logIds.Contains(a.Id))).ToDictionary(a => a.Id);

            var rows = new List<OvertimeRecordDto>();
            foreach (var r in records)
            {
                // Department is already applied in SQL via departmentEmployeeIds.
                if (!employees.TryGetValue(r.EmployeeId, out var emp)) continue;

                logs.TryGetValue(r.AttendanceLogId ?? 0, out var log);

                rows.Add(new OvertimeRecordDto
                {
                    Id = r.Id,
                    EmployeeId = r.EmployeeId,
                    EmployeeCode = emp.EmployeeCode,
                    EmployeeName = $"{emp.FirstName} {emp.LastName}",
                    Department = departments.TryGetValue(emp.DepartmentId, out var dn) ? dn : string.Empty,
                    OvertimeDate = r.OvertimeDate,
                    ShiftName = r.ShiftId.HasValue && shifts.TryGetValue(r.ShiftId.Value, out var sn) ? sn : null,
                    CheckInDisplay = log?.CheckIn?.ToString("HH:mm"),
                    CheckOutDisplay = log?.CheckOut?.ToString("HH:mm"),
                    RawMinutes = r.RawMinutes,
                    ClaimedMinutes = r.ClaimedMinutes,
                    ApprovedMinutes = r.ApprovedMinutes,
                    RuleName = r.RuleName,
                    RateMultiplier = r.RateMultiplier,
                    DayType = r.DayType,
                    Status = r.Status,
                    ApprovedByName = r.ApprovedBy.HasValue && users.TryGetValue(r.ApprovedBy.Value, out var un) ? un : null,
                    ApprovedAt = r.ApprovedAt,
                    Remarks = r.Remarks,
                    RejectionReason = r.RejectionReason,
                    IsManual = r.IsManual
                });
            }

            // Row order is already set by the database — re-sorting here would shuffle the page
            // against the ORDER BY that produced it and make paging skip records.
            var dto = new OvertimeRegisterDto
            {
                From = start,
                To = end,
                Rows = rows,
                Page = page.Page,
                PageSize = page.PageSize,
                TotalCount = totals.TotalCount,
                RangePendingCount = totals.PendingCount,
                RangeApprovedCount = totals.ApprovedCount,
                RangeRejectedCount = totals.RejectedCount,
                RangeClaimedMinutes = totals.ClaimedMinutes,
                RangeApprovedMinutes = totals.ApprovedMinutes,
                RangeWeightedHours = Math.Round(totals.WeightedHours, 2),
                RangeClaimedWeightedHours = Math.Round(totals.ClaimedWeightedHours, 2)
            };

            return Result<OvertimeRegisterDto>.Success(dto);
        }
        catch (Exception ex)
        {
            AppLogger.Error("OvertimeService.GetRegisterAsync", ex);
            return Result<OvertimeRegisterDto>.Failure("Failed to load the overtime register.");
        }
    }

    // ── Decisions ────────────────────────────────────────────────────────────────

    public async Task<Result<int>> DecideAsync(OvertimeDecisionDto dto)
    {
        try
        {
            if (dto.Ids == null || dto.Ids.Count == 0)
                return Result<int>.Failure("Select at least one claim.");

            if (!dto.Approve && string.IsNullOrWhiteSpace(dto.Reason))
                return Result<int>.Failure("A reason is required when rejecting overtime.");

            // Granting a specific number of minutes only makes sense for a single claim;
            // applied across a selection it would silently pay everyone the same.
            if (dto.Approve && dto.ApprovedMinutes.HasValue && dto.Ids.Count > 1)
                return Result<int>.Failure("Adjusted minutes can only be applied to one claim at a time.");

            var ids = dto.Ids.Distinct().ToList();
            var records = (await _uow.OvertimeRecords.FindAsync(r => !r.IsDeleted && ids.Contains(r.Id)))
                .ToList();
            if (records.Count == 0) return Result<int>.Failure("No matching overtime claims found.");

            var now = DateTime.Now;
            var changed = 0;

            foreach (var r in records)
            {
                if (dto.Approve)
                {
                    var minutes = dto.ApprovedMinutes ?? r.ClaimedMinutes;
                    if (minutes > r.ClaimedMinutes)
                        return Result<int>.Failure(
                            $"Cannot approve {minutes} minutes — only {r.ClaimedMinutes} were claimed.");

                    r.Status = OvertimeStatus.Approved;
                    r.ApprovedMinutes = minutes;
                    r.RejectionReason = null;
                }
                else
                {
                    r.Status = OvertimeStatus.Rejected;
                    r.ApprovedMinutes = 0;
                    r.RejectionReason = dto.Reason?.Trim();
                }

                r.ApprovedBy = _currentUser.UserId;
                r.ApprovedAt = now;
                if (!string.IsNullOrWhiteSpace(dto.Reason) && dto.Approve) r.Remarks = dto.Reason.Trim();
                r.ModifiedBy = _currentUser.UserId;
                r.ModifiedAt = now;

                await _uow.OvertimeRecords.UpdateAsync(r);
                changed++;
            }

            await _uow.SaveChangesAsync();
            await _audit.LogAsync(AppConstants.Modules.Overtime, dto.Approve ? "Approve" : "Reject",
                _currentUser.UserId, nameof(OvertimeRecord),
                newValues: $"{changed} claim(s): {string.Join(",", ids)}");

            return Result<int>.Success(changed);
        }
        catch (Exception ex)
        {
            AppLogger.Error("OvertimeService.DecideAsync", ex);
            return Result<int>.Failure("Failed to record the overtime decision.");
        }
    }

    // ── Summary ──────────────────────────────────────────────────────────────────

    public async Task<Result<OvertimeSummaryDto>> GetSummaryAsync(DateTime from, DateTime to,
        int? departmentId = null, int? employeeId = null)
    {
        try
        {
            var register = await GetRegisterAsync(from, to, employeeId, departmentId);
            if (!register.IsSuccess) return Result<OvertimeSummaryDto>.Failure(register.ErrorMessage!);

            var rows = register.Data!.Rows
                .GroupBy(r => r.EmployeeId)
                .Select(g =>
                {
                    var first = g.First();
                    return new OvertimeSummaryRowDto
                    {
                        EmployeeId = g.Key,
                        EmployeeCode = first.EmployeeCode,
                        EmployeeName = first.EmployeeName,
                        Department = first.Department,
                        Days = g.Count(),
                        PendingMinutes = g.Where(x => x.Status == OvertimeStatus.Pending).Sum(x => x.ClaimedMinutes),
                        ApprovedMinutes = g.Where(x => x.Status == OvertimeStatus.Approved).Sum(x => x.ApprovedMinutes ?? 0),
                        RejectedMinutes = g.Where(x => x.Status == OvertimeStatus.Rejected).Sum(x => x.ClaimedMinutes),
                        WeightedHours = Math.Round(g.Sum(x => x.WeightedHours), 2),
                        PremiumMinutes = g.Where(x => x.Status == OvertimeStatus.Approved &&
                                                      x.DayType is OvertimeDayType.WeeklyOff or OvertimeDayType.Holiday)
                                           .Sum(x => x.ApprovedMinutes ?? 0)
                    };
                })
                .OrderByDescending(r => r.ApprovedMinutes)
                .ThenBy(r => r.EmployeeName)
                .ToList();

            return Result<OvertimeSummaryDto>.Success(new OvertimeSummaryDto
            {
                From = register.Data.From,
                To = register.Data.To,
                Rows = rows
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("OvertimeService.GetSummaryAsync", ex);
            return Result<OvertimeSummaryDto>.Failure("Failed to build the overtime summary.");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static void ApplyTo(OvertimeRule entity, SaveOvertimeRuleDto dto, string name)
    {
        entity.Name = name;
        entity.Description = dto.Description?.Trim();
        entity.IsActive = dto.IsActive;
        entity.Priority = dto.Priority;
        entity.DepartmentId = dto.DepartmentId;
        entity.ShiftId = dto.ShiftId;
        entity.DayType = dto.DayType;
        entity.RateMultiplier = dto.RateMultiplier;
        entity.MinimumMinutes = dto.MinimumMinutes;
        entity.MaxMinutesPerDay = dto.MaxMinutesPerDay is > 0 ? dto.MaxMinutesPerDay : null;
        entity.RoundToMinutes = dto.RoundToMinutes;
        entity.RequiresApproval = dto.RequiresApproval;
    }

    private static void AutoApprove(OvertimeRecord record, int minutes, DateTime now,
        GenerateOvertimeResultDto result)
    {
        record.Status = OvertimeStatus.Approved;
        record.ApprovedMinutes = minutes;
        record.ApprovedAt = now;
        record.Remarks = "Auto-approved: the matching rule does not require approval.";
        result.AutoApproved++;
    }

    /// <summary>
    /// The shift in force on that date. Same rule the rest of the system uses: among the
    /// assignments covering the day, the one with the latest EffectiveFrom wins.
    /// </summary>
    private static Shift? ResolveShift(List<EmployeeShift> assignments, Dictionary<int, Shift> shifts,
        AttendanceLog log, DateTime date)
    {
        var assignment = assignments
            .Where(a => a.EmployeeId == log.EmployeeId &&
                        a.EffectiveFrom.Date <= date &&
                        (a.EffectiveTo == null || a.EffectiveTo.Value.Date >= date))
            .OrderByDescending(a => a.EffectiveFrom)
            .FirstOrDefault();

        return assignment != null && shifts.TryGetValue(assignment.ShiftId, out var s) ? s : null;
    }

    /// <summary>
    /// Holiday beats weekly off: a public holiday falling on a Sunday is still a holiday, and
    /// that is normally the dearer of the two.
    /// </summary>
    private static OvertimeDayType ClassifyDay(DateTime date, Shift? shift, HashSet<DateTime> holidays)
    {
        if (holidays.Contains(date)) return OvertimeDayType.Holiday;
        if (shift != null && AttendanceCalculator.IsWeeklyOff(shift, date)) return OvertimeDayType.WeeklyOff;
        return OvertimeDayType.WorkingDay;
    }

    /// <summary>
    /// The most specific rule that matches, preferring lower Priority. Specificity is scored so
    /// that a department+shift+holiday rule beats a bare holiday rule at the same priority,
    /// rather than the winner depending on insertion order.
    /// </summary>
    private static OvertimeRule? MatchRule(List<OvertimeRule> rules, int departmentId, int? shiftId,
        OvertimeDayType dayType)
    {
        return rules
            .Where(r => r.DepartmentId == null || r.DepartmentId == departmentId)
            .Where(r => r.ShiftId == null || r.ShiftId == shiftId)
            .Where(r => r.DayType == OvertimeDayType.Any || r.DayType == dayType)
            .OrderBy(r => r.Priority)
            .ThenByDescending(r => (r.DepartmentId.HasValue ? 4 : 0)
                                 + (r.ShiftId.HasValue ? 2 : 0)
                                 + (r.DayType != OvertimeDayType.Any ? 1 : 0))
            .ThenBy(r => r.Id)
            .FirstOrDefault();
    }

    /// <summary>Minimum, then cap, then round down to a whole block.</summary>
    private static int ApplyRule(int rawMinutes, OvertimeRule rule)
    {
        if (rawMinutes < rule.MinimumMinutes) return 0;

        var minutes = rawMinutes;
        if (rule.MaxMinutesPerDay is > 0) minutes = Math.Min(minutes, rule.MaxMinutesPerDay.Value);
        if (rule.RoundToMinutes > 0) minutes = minutes / rule.RoundToMinutes * rule.RoundToMinutes;

        // Rounding can drop a qualifying claim below the minimum (35 minutes, round to 30,
        // minimum 35). Treating that as nothing would be a surprise, so keep the minimum.
        if (minutes < rule.MinimumMinutes) minutes = Math.Min(rawMinutes, rule.MinimumMinutes);

        return minutes;
    }

    private static OvertimeRuleDto Map(OvertimeRule r, Dictionary<int, string> departments,
        Dictionary<int, string> shifts) => new()
    {
        Id = r.Id,
        Name = r.Name,
        Description = r.Description,
        IsActive = r.IsActive,
        Priority = r.Priority,
        DepartmentId = r.DepartmentId,
        DepartmentName = r.DepartmentId.HasValue && departments.TryGetValue(r.DepartmentId.Value, out var dn) ? dn : null,
        ShiftId = r.ShiftId,
        ShiftName = r.ShiftId.HasValue && shifts.TryGetValue(r.ShiftId.Value, out var sn) ? sn : null,
        DayType = r.DayType,
        RateMultiplier = r.RateMultiplier,
        MinimumMinutes = r.MinimumMinutes,
        MaxMinutesPerDay = r.MaxMinutesPerDay,
        RoundToMinutes = r.RoundToMinutes,
        RequiresApproval = r.RequiresApproval
    };
}

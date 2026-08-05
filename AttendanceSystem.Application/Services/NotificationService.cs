using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Constants;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Domain.Interfaces;

namespace AttendanceSystem.Application.Services;

/// <summary>
/// Builds the header notification list from live data.
///
/// Replaces markup that was hardcoded to "5" with a static list of invented messages — which
/// looked like a working feature and was therefore worse than an empty bell.
///
/// Nothing is stored. Every item answers "is this true right now?", so a persisted
/// notification would need invalidating the moment someone approved the leave or fixed the
/// device. Deriving on request costs a handful of counts and can never be stale.
///
/// Each item is filtered by the permission needed to act on it: there is no point telling
/// someone about pending leave they cannot approve.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _currentUser;

    public NotificationService(IUnitOfWork uow, ICurrentUserContext currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<NotificationsDto>> GetAsync()
    {
        try
        {
            var dto = new NotificationsDto { GeneratedAt = DateTime.Now };
            var today = DateTime.Today;

            var canSeeLeave = _currentUser.HasPermission(AppConstants.Modules.Leave, AppConstants.Actions.View);
            var canSeeAttendance = _currentUser.HasPermission(AppConstants.Modules.Attendance, AppConstants.Actions.View);
            var canSeeEmployees = _currentUser.HasPermission(AppConstants.Modules.Employees, AppConstants.Actions.View);
            var canSeeDevices = _currentUser.HasPermission(AppConstants.Modules.Devices, AppConstants.Actions.View);

            // ── Leave awaiting approval ──────────────────────────────────────
            if (canSeeLeave)
            {
                var pending = (await _uow.Leaves.GetPendingAsync()).ToList();
                if (pending.Count > 0)
                {
                    var oldestDays = (int)(today - pending.Min(p => p.CreatedAt).Date).TotalDays;
                    dto.Items.Add(new NotificationDto
                    {
                        Key = "leave.pending",
                        Title = "Leave awaiting approval",
                        Message = pending.Count == 1
                            ? "1 request is waiting"
                            : $"{pending.Count} requests are waiting"
                              + (oldestDays > 2 ? $", oldest for {oldestDays} days" : ""),
                        Icon = "icon-calendar",
                        Severity = oldestDays > 3 ? NotificationSeverity.Warning : NotificationSeverity.Info,
                        Url = "/Admin/Leave",
                        Count = pending.Count
                    });
                }
            }

            // ── Today's attendance exceptions ────────────────────────────────
            if (canSeeAttendance)
            {
                var todayLogs = (await _uow.Attendance.GetByDateAsync(today)).ToList();

                var late = todayLogs.Count(l => l.IsLate);
                if (late > 0)
                {
                    dto.Items.Add(new NotificationDto
                    {
                        Key = "attendance.late",
                        Title = "Late arrivals today",
                        Message = late == 1 ? "1 employee arrived late" : $"{late} employees arrived late",
                        Icon = "icon-clock",
                        Severity = NotificationSeverity.Info,
                        Url = "/Admin/AttendanceReview",
                        Count = late
                    });
                }

                // Yesterday and earlier, so people still at work today are not flagged.
                var openRecords = (await _uow.Attendance.FindAsync(a =>
                    !a.IsDeleted && a.CheckIn != null && a.CheckOut == null && a.AttendanceDate < today))
                    .ToList();

                if (openRecords.Count > 0)
                {
                    dto.Items.Add(new NotificationDto
                    {
                        Key = "attendance.nocheckout",
                        Title = "Missing check-out",
                        Message = $"{openRecords.Count} record(s) have no check-out — working hours cannot be calculated",
                        Icon = "icon-alert-triangle",
                        Severity = NotificationSeverity.Warning,
                        Url = "/Admin/AttendanceReview",
                        Count = openRecords.Count
                    });
                }
            }

            // ── Setup problems that silently break attendance ────────────────
            if (canSeeEmployees)
            {
                var activeEmployees = (await _uow.Employees.FindAsync(e => e.IsActive && !e.IsDeleted)).ToList();

                var noEnrollId = activeEmployees.Count(e => !e.BiometricEnrollId.HasValue);
                if (noEnrollId > 0)
                {
                    dto.Items.Add(new NotificationDto
                    {
                        Key = "employees.noenroll",
                        Title = "Employees without a biometric ID",
                        Message = $"{noEnrollId} employee(s) cannot be matched by any device import",
                        Icon = "icon-user-x",
                        Severity = NotificationSeverity.Warning,
                        Url = "/Admin/Employees",
                        Count = noEnrollId
                    });
                }

                var assignments = (await _uow.EmployeeShifts.FindAsync(es =>
                    !es.IsDeleted && es.EffectiveFrom <= today &&
                    (es.EffectiveTo == null || es.EffectiveTo >= today))).ToList();

                var assigned = assignments.Select(a => a.EmployeeId).ToHashSet();
                var noShift = activeEmployees.Count(e => !assigned.Contains(e.Id));
                if (noShift > 0)
                {
                    dto.Items.Add(new NotificationDto
                    {
                        Key = "employees.noshift",
                        Title = "Employees without a shift",
                        Message = $"{noShift} employee(s) are never marked late or early",
                        Icon = "icon-clock",
                        Severity = NotificationSeverity.Warning,
                        Url = "/Admin/ShiftRoster",
                        Count = noShift
                    });
                }
            }

            // ── Devices needing attention ────────────────────────────────────
            if (canSeeDevices)
            {
                var faulty = (await _uow.Devices.FindAsync(d =>
                    !d.IsDeleted && d.IsActive && d.Status == DeviceStatus.Error)).ToList();

                if (faulty.Count > 0)
                {
                    dto.Items.Add(new NotificationDto
                    {
                        Key = "devices.error",
                        Title = "Device not responding",
                        Message = string.Join(", ", faulty.Take(3).Select(d => d.Name))
                                  + (faulty.Count > 3 ? $" and {faulty.Count - 3} more" : ""),
                        Icon = "icon-cpu",
                        Severity = NotificationSeverity.Critical,
                        Url = "/Admin/Devices",
                        Count = faulty.Count
                    });
                }
            }

            dto.Items = dto.Items
                .OrderByDescending(i => i.Severity)
                .ThenByDescending(i => i.Count)
                .ToList();

            // The badge counts *things needing attention*, not notification cards — "3" next
            // to a single card reading "3 requests waiting" is what a user expects.
            dto.TotalCount = dto.Items.Sum(i => i.Count);

            return Result<NotificationsDto>.Success(dto);
        }
        catch (Exception ex)
        {
            AppLogger.Error("NotificationService.GetAsync", ex);
            return Result<NotificationsDto>.Failure("Failed to build notifications.");
        }
    }
}

using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Application.Services;

/// <summary>
/// The single place attendance figures are derived from a punch pair and a shift.
///
/// Check-in, manual edit and the review screen all call this. They previously each had their
/// own arithmetic, which is how the system ended up recomputing working hours on edit but not
/// lateness. One implementation means the three can no longer disagree.
///
/// Pure and static: no database, no clock. That makes the rules testable in isolation, which
/// matters most for the night-shift and overtime cases that are easy to get subtly wrong.
/// </summary>
public static class AttendanceCalculator
{
    public sealed class Result
    {
        public bool IsLate { get; init; }
        public int? LateMinutes { get; init; }
        public bool IsEarlyLeave { get; init; }
        public int? EarlyLeaveMinutes { get; init; }

        /// <summary>Check-out minus check-in, before the break deduction.</summary>
        public double? GrossHours { get; init; }

        /// <summary>Paid time, after the break deduction.</summary>
        public double? WorkingHours { get; init; }

        public int? OvertimeMinutes { get; init; }
        public AttendanceStatus Status { get; init; }
    }

    /// <summary>
    /// Derives every figure for one attendance record.
    /// </summary>
    /// <param name="shift">Shift in force on <paramref name="attendanceDate"/>, or null when
    /// the employee has no assignment — nothing can then be judged late or early.</param>
    /// <param name="isHoliday">Holiday takes precedence over lateness in the status.</param>
    public static Result Calculate(
        Shift? shift,
        DateTime attendanceDate,
        DateTime? checkIn,
        DateTime? checkOut,
        bool isHoliday,
        bool isOnLeave = false)
    {
        var date = attendanceDate.Date;

        if (!checkIn.HasValue)
        {
            return new Result
            {
                Status = isOnLeave ? AttendanceStatus.OnLeave
                       : isHoliday ? AttendanceStatus.Holiday
                       : shift != null && IsWeeklyOff(shift, date) ? AttendanceStatus.WeeklyOff
                       : AttendanceStatus.Absent
            };
        }

        // ── Expected boundaries ──────────────────────────────────────────────
        // For a night shift the end lands on the following day. Without this the expected
        // end is *before* the start and every duration goes negative.
        var expectedStart = date.Add(shift?.StartTime ?? TimeSpan.Zero);
        var expectedEnd = shift == null
            ? (DateTime?)null
            : date.Add(shift.EndTime).AddDays(CrossesMidnight(shift) ? 1 : 0);

        // ── Late ─────────────────────────────────────────────────────────────
        var isLate = false;
        int? lateMinutes = null;
        if (shift != null)
        {
            var late = (int)(checkIn.Value - expectedStart.AddMinutes(shift.GraceMinutes)).TotalMinutes;
            if (late > 0) { isLate = true; lateMinutes = late; }
        }

        // ── Early leave ──────────────────────────────────────────────────────
        var isEarly = false;
        int? earlyMinutes = null;
        if (shift != null && checkOut.HasValue && expectedEnd.HasValue)
        {
            var early = (int)(expectedEnd.Value.AddMinutes(-shift.GraceOutMinutes) - checkOut.Value).TotalMinutes;
            if (early > 0) { isEarly = true; earlyMinutes = early; }
        }

        // ── Hours ────────────────────────────────────────────────────────────
        double? grossHours = null;
        double? workingHours = null;
        if (checkOut.HasValue)
        {
            var span = checkOut.Value - checkIn.Value;

            // A night-shift out-punch recorded against the same calendar date reads as
            // negative; roll it forward a day, which is what actually happened.
            if (span < TimeSpan.Zero && shift != null && CrossesMidnight(shift))
                span = span.Add(TimeSpan.FromDays(1));

            if (span >= TimeSpan.Zero)
            {
                grossHours = Math.Round(span.TotalHours, 2);
                var breakHours = (shift?.BreakMinutes ?? 0) / 60.0;
                workingHours = Math.Round(Math.Max(0, span.TotalHours - breakHours), 2);
            }
        }

        // ── Overtime ─────────────────────────────────────────────────────────
        int? overtimeMinutes = null;
        if (shift is { IsOtEnabled: true } && checkOut.HasValue && workingHours.HasValue)
        {
            double otMinutes;

            // On a day the employee was not rostered to work at all, every hour is overtime.
            //
            // Measuring from the shift end would say otherwise: someone called in for four
            // hours on their Sunday off leaves long before the shift's nominal end and earns
            // nothing, which is the opposite of what a day off is worth. The shift's end time
            // is meaningless on a day the shift does not run.
            // shift is non-null here: the enclosing `shift is { IsOtEnabled: true }` guarantees it.
            var isNonWorkingDay = isHoliday || IsWeeklyOff(shift, date);

            if (isNonWorkingDay)
            {
                otMinutes = workingHours.Value * 60;
            }
            else if (shift.OtCountsFromShiftEnd && expectedEnd.HasValue)
            {
                // Only time past the shift end (plus the threshold) counts. Arriving early
                // does not earn overtime under this rule.
                var otFrom = expectedEnd.Value.AddMinutes(shift.OtStartAfterMinutes);
                otMinutes = (checkOut.Value - otFrom).TotalMinutes;
            }
            else
            {
                // Anything worked beyond the standard day, wherever it fell.
                otMinutes = (workingHours.Value - shift.EffectiveStandardHours) * 60;
            }

            overtimeMinutes = otMinutes > 0 ? (int)Math.Floor(otMinutes) : 0;
        }

        // ── Status ───────────────────────────────────────────────────────────
        // Precedence: leave, holiday and weekly off describe the *day* and outrank how the
        // person performed on it.
        var status = isOnLeave ? AttendanceStatus.OnLeave
                   : isHoliday ? AttendanceStatus.Holiday
                   : shift != null && IsWeeklyOff(shift, date) ? AttendanceStatus.WeeklyOff
                   : isLate ? AttendanceStatus.Late
                   : AttendanceStatus.Present;

        return new Result
        {
            IsLate = isLate,
            LateMinutes = lateMinutes,
            IsEarlyLeave = isEarly,
            EarlyLeaveMinutes = earlyMinutes,
            GrossHours = grossHours,
            WorkingHours = workingHours,
            OvertimeMinutes = overtimeMinutes,
            Status = status
        };
    }

    /// <summary>Applies a calculation onto a record.</summary>
    public static void Apply(AttendanceLog log, Result r, AttendanceStatus? explicitStatus = null)
    {
        log.IsLate = r.IsLate;
        log.LateMinutes = r.LateMinutes;
        log.IsEarlyLeave = r.IsEarlyLeave;
        log.EarlyLeaveMinutes = r.EarlyLeaveMinutes;
        log.GrossHours = r.GrossHours;
        log.WorkingHours = r.WorkingHours;
        log.OvertimeMinutes = r.OvertimeMinutes;

        // An explicit status is an operator override — marking someone On Leave regardless
        // of what the times say.
        log.Status = explicitStatus ?? r.Status;
    }

    /// <summary>
    /// Whether the shift runs past midnight. Trusts the flag, but also honours times that
    /// plainly cross over, so a shift saved before the flag existed still calculates right.
    /// </summary>
    public static bool CrossesMidnight(Shift shift) => shift.IsNightShift || shift.EndTime <= shift.StartTime;

    public static bool IsWeeklyOff(Shift shift, DateTime date) =>
        shift.WeeklyOffDays
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(d => d.Trim())
            .Contains(date.DayOfWeek.ToString(), StringComparer.OrdinalIgnoreCase);
}

namespace AttendanceSystem.Application.Services;

/// <summary>
/// The arithmetic of a leave request, separated from the fetching.
///
/// Pure and static, like <see cref="AttendanceCalculator"/> and <see cref="PayrollCalculator"/>,
/// and for the same reason: how many days a request costs somebody's entitlement is the sort
/// of thing that has to be checkable by counting on a calendar. It was previously a private
/// method inside <see cref="LeaveService"/> with a database on either side of it, which meant
/// the rule could not be exercised at all.
/// </summary>
public static class LeaveCalculator
{
    /// <summary>
    /// Working days between two dates, inclusive, skipping holidays and weekly off days.
    ///
    /// Both ends are inclusive because that is how somebody writes a leave request: "the 5th
    /// to the 7th" is three days, not two. Off-by-one here is a day of entitlement per
    /// request, in the employee's favour or the company's depending which way it slips.
    /// </summary>
    /// <param name="isWeeklyOff">
    /// Whether a given date is a weekly off for this employee. Passed in rather than resolved
    /// here because it depends on the shift in force on that date, and a request can span a
    /// shift change.
    /// </param>
    public static int CountWorkingDays(
        DateTime from, DateTime to,
        IReadOnlySet<DateTime> holidays,
        Func<DateTime, bool> isWeeklyOff)
    {
        var fromDate = from.Date;
        var toDate = to.Date;

        // A backwards range is a data error, not zero days. Returning 0 would let a request
        // with the dates the wrong way round cost nothing and be approved.
        if (toDate < fromDate) return 0;

        var days = 0;

        for (var day = fromDate; day <= toDate; day = day.AddDays(1))
        {
            if (holidays.Contains(day)) continue;
            if (isWeeklyOff(day)) continue;
            days++;
        }

        return days;
    }

    /// <summary>
    /// Whether two date ranges overlap. Both are inclusive at each end.
    ///
    /// Used to stop a second leave request covering days already booked. The comparison is
    /// deliberately not strict: two requests that merely touch — one ending on the 7th, the
    /// next starting on the 7th — DO overlap, because the 7th is in both.
    /// </summary>
    public static bool Overlaps(DateTime aFrom, DateTime aTo, DateTime bFrom, DateTime bTo) =>
        aFrom.Date <= bTo.Date && bFrom.Date <= aTo.Date;

    /// <summary>
    /// What is left of an entitlement.
    ///
    /// Never negative: an entitlement already overdrawn reads as zero remaining rather than
    /// as a negative balance that a later screen might treat as available.
    /// </summary>
    public static decimal Remaining(decimal entitled, decimal committed) =>
        Math.Max(0m, entitled - committed);
}

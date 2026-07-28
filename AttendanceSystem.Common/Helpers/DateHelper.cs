namespace AttendanceSystem.Common.Helpers;

/// <summary>Date/time utility helpers.</summary>
public static class DateHelper
{
    public static string ToDisplayDate(this DateTime date) =>
        date.ToString(Constants.AppConstants.DateFormats.Display);

    public static string ToDisplayDateTime(this DateTime dt) =>
        dt.ToString(Constants.AppConstants.DateFormats.DateTimeDisplay);

    public static string ToTimeDisplay(this TimeSpan time) =>
        DateTime.Today.Add(time).ToString(Constants.AppConstants.DateFormats.TimeDisplay);

    public static int GetWorkingDays(DateTime from, DateTime to, IEnumerable<DateTime> holidays)
    {
        var days = 0;
        for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
        {
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday
                && !holidays.Any(h => h.Date == d.Date))
                days++;
        }
        return days;
    }

    public static int CalculateLateMinutes(TimeSpan checkIn, TimeSpan shiftStart, int graceMinutes)
    {
        var diff = (checkIn - shiftStart).TotalMinutes;
        return diff > graceMinutes ? (int)(diff - graceMinutes) : 0;
    }

    public static double CalculateWorkingHours(DateTime? checkIn, DateTime? checkOut)
    {
        if (checkIn == null || checkOut == null) return 0;
        return (checkOut.Value - checkIn.Value).TotalHours;
    }
}

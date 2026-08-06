using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Interfaces;
using AttendanceSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Repositories;

/// <summary>Holiday-specific repository implementation.</summary>
public class HolidayRepository : Repository<Holiday>, IHolidayRepository
{
    public HolidayRepository(AttendanceDbContext context) : base(context) { }

    /// <summary>
    /// Whether a date is a holiday, honouring the recurring flag.
    ///
    /// This used to compare the full date only, which meant "Recurring every year" was stored,
    /// displayed with a tick, and never read: Christmas entered once in 2025 left 25 December
    /// 2026 an ordinary working day. Attendance was not marked Holiday and — because all time
    /// worked on a holiday counts as overtime — the day was underpaid, silently.
    /// </summary>
    public async Task<bool> IsHolidayAsync(DateTime date) =>
        await _dbSet.AnyAsync(h =>
            h.HolidayDate.Date == date.Date ||
            (h.IsRecurring &&
             h.HolidayDate.Month == date.Month &&
             h.HolidayDate.Day == date.Day &&
             // A recurrence starts from the year it was declared, not retroactively —
             // otherwise adding a company holiday today would rewrite last year's attendance.
             h.HolidayDate.Year <= date.Year));

    /// <summary>
    /// The holiday dates falling inside a range, with recurring entries projected onto the
    /// years the range covers.
    ///
    /// Exists so callers that need to test many dates — counting the working days in a leave
    /// request, for one — do not issue a query per day, and more importantly do not reimplement
    /// the recurrence rule. That rule has already been wrong once here; one copy of it is the
    /// point.
    /// </summary>
    public async Task<HashSet<DateTime>> GetHolidayDatesAsync(DateTime from, DateTime to)
    {
        var fromDate = from.Date;
        var toDate = to.Date;

        var candidates = await _dbSet.Where(h =>
                (h.HolidayDate.Date >= fromDate && h.HolidayDate.Date <= toDate) ||
                (h.IsRecurring && h.HolidayDate.Year <= toDate.Year))
            .ToListAsync();

        var dates = new HashSet<DateTime>();
        foreach (var h in candidates)
        {
            if (h.HolidayDate.Date >= fromDate && h.HolidayDate.Date <= toDate)
                dates.Add(h.HolidayDate.Date);

            if (!h.IsRecurring) continue;

            // Projected onto each year the range touches, but never before the year it was
            // declared — the same rule IsHolidayAsync applies.
            for (var year = fromDate.Year; year <= toDate.Year; year++)
            {
                if (year < h.HolidayDate.Year) continue;

                // 29 February recurs only in leap years; DateTime would otherwise throw.
                if (h.HolidayDate.Month == 2 && h.HolidayDate.Day == 29 && !DateTime.IsLeapYear(year))
                    continue;

                var projected = new DateTime(year, h.HolidayDate.Month, h.HolidayDate.Day);
                if (projected >= fromDate && projected <= toDate) dates.Add(projected);
            }
        }
        return dates;
    }

    /// <summary>
    /// Holidays in force for a year: those actually dated in it, plus recurring ones declared
    /// in an earlier year. The second group is returned with its original date — the caller
    /// projects it forward, so the stored row is never mistaken for a row of its own.
    /// </summary>
    public async Task<IEnumerable<Holiday>> GetByYearAsync(int year) =>
        await _dbSet.Where(h =>
                        h.HolidayDate.Year == year ||
                        (h.IsRecurring && h.HolidayDate.Year < year))
                    .OrderBy(h => h.HolidayDate.Month).ThenBy(h => h.HolidayDate.Day)
                    .ToListAsync();
}

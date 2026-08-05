using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Services;

/// <summary>
/// Resolves which dashboard widgets a user sees, in three falling-back layers:
///
///   1. the user's own choices
///   2. the company default
///   3. the defaults in the catalogue
///
/// Absence is meaningful at every layer, which is what keeps the dashboard working for
/// somebody who has never opened the customise dialog — and what makes "reset to default" a
/// deletion rather than a copy.
/// </summary>
public class DashboardPreferenceService : IDashboardPreferenceService
{
    private readonly AttendanceDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public DashboardPreferenceService(AttendanceDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<IEnumerable<DashboardWidgetDto>>> GetMyWidgetsAsync()
    {
        try
        {
            var userId = _currentUser.UserId;
            if (!userId.HasValue)
                return Result<IEnumerable<DashboardWidgetDto>>.Failure("Not signed in.");

            var mine = await LoadAsync(userId.Value);
            var company = await LoadAsync(null);

            var list = Allowed().Select(w => new DashboardWidgetDto
            {
                Key = w.Key, Title = w.Title, Description = w.Description,
                Module = w.Module, Action = w.Action, IsDefault = w.IsDefault,
                IsVisible = Resolve(w, mine, company)
            }).ToList();

            return Result<IEnumerable<DashboardWidgetDto>>.Success(list);
        }
        catch (Exception ex)
        {
            AppLogger.Error("DashboardPreferenceService.GetMyWidgetsAsync", ex);
            return Result<IEnumerable<DashboardWidgetDto>>.Failure("Could not load your dashboard settings.");
        }
    }

    public async Task<Result<IEnumerable<DashboardWidgetDto>>> GetCompanyDefaultAsync()
    {
        try
        {
            var company = await LoadAsync(null);

            var list = Allowed().Select(w => new DashboardWidgetDto
            {
                Key = w.Key, Title = w.Title, Description = w.Description,
                Module = w.Module, Action = w.Action, IsDefault = w.IsDefault,
                IsVisible = company.TryGetValue(w.Key, out var v) ? v : w.IsDefault
            }).ToList();

            return Result<IEnumerable<DashboardWidgetDto>>.Success(list);
        }
        catch (Exception ex)
        {
            AppLogger.Error("DashboardPreferenceService.GetCompanyDefaultAsync", ex);
            return Result<IEnumerable<DashboardWidgetDto>>.Failure("Could not load the company default.");
        }
    }

    public Task<Result> SaveMyPreferencesAsync(SaveDashboardPreferencesDto dto) =>
        SaveAsync(_currentUser.UserId, dto);

    public Task<Result> SaveCompanyDefaultAsync(SaveDashboardPreferencesDto dto) =>
        SaveAsync(null, dto);

    public async Task<Result> ResetMineAsync()
    {
        try
        {
            var userId = _currentUser.UserId;
            if (!userId.HasValue) return Result.Failure("Not signed in.");

            var mine = await _db.DashboardPreferences.Where(p => p.UserId == userId).ToListAsync();
            _db.DashboardPreferences.RemoveRange(mine);
            await _db.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("DashboardPreferenceService.ResetMineAsync", ex);
            return Result.Failure("Could not reset your dashboard.");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────

    private async Task<Result> SaveAsync(int? userId, SaveDashboardPreferencesDto dto)
    {
        try
        {
            if (userId == null && !_currentUser.IsAuthenticated)
                return Result.Failure("Not signed in.");

            var wanted = (dto.VisibleKeys ?? new List<string>())
                .Where(DashboardWidgetCatalogue.IsKnown)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Only widgets this user may see are written. Otherwise saving would silently drop
            // preferences for widgets they cannot currently load — a permission granted later
            // would come back switched off with no explanation.
            var allowed = Allowed().Select(w => w.Key).ToList();

            var existing = await _db.DashboardPreferences
                .Where(p => p.UserId == userId)
                .ToListAsync();

            foreach (var key in allowed)
            {
                var visible = wanted.Contains(key);
                var row = existing.FirstOrDefault(p => string.Equals(p.WidgetKey, key, StringComparison.OrdinalIgnoreCase));

                if (row == null)
                {
                    _db.DashboardPreferences.Add(new DashboardPreference
                    {
                        UserId = userId, WidgetKey = key, IsVisible = visible,
                        CreatedAt = DateTime.Now, CreatedBy = _currentUser.UserId
                    });
                }
                else if (row.IsVisible != visible)
                {
                    row.IsVisible = visible;
                    row.ModifiedAt = DateTime.Now;
                    row.ModifiedBy = _currentUser.UserId;
                }
            }

            await _db.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("DashboardPreferenceService.SaveAsync", ex);
            return Result.Failure("Could not save the dashboard settings.");
        }
    }

    /// <summary>Catalogue entries the signed-in user holds the permission for.</summary>
    private IEnumerable<DashboardWidgetDto> Allowed() =>
        DashboardWidgetCatalogue.All.Where(w => _currentUser.HasPermission(w.Module, w.Action));

    private async Task<Dictionary<string, bool>> LoadAsync(int? userId) =>
        (await _db.DashboardPreferences.AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync())
        // Unknown keys are dropped here, so a widget removed in a later version cannot
        // resurrect itself through an old preference row.
        .Where(p => DashboardWidgetCatalogue.IsKnown(p.WidgetKey))
        .GroupBy(p => p.WidgetKey, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First().IsVisible, StringComparer.OrdinalIgnoreCase);

    private static bool Resolve(
        DashboardWidgetDto widget, Dictionary<string, bool> mine, Dictionary<string, bool> company) =>
        mine.TryGetValue(widget.Key, out var m) ? m
      : company.TryGetValue(widget.Key, out var c) ? c
      : widget.IsDefault;

    // ──────────────────────────────────────────────────────────────────────────
    // Custom tiles
    // ──────────────────────────────────────────────────────────────────────────

    public Result<IEnumerable<DashboardMetricDto>> GetMetrics() =>
        Result<IEnumerable<DashboardMetricDto>>.Success(
            DashboardMetricCatalogue.All.Where(m => _currentUser.HasPermission(m.Module, m.Action)));

    public async Task<Result<IEnumerable<DashboardTileDto>>> GetMyTilesAsync()
    {
        try
        {
            var userId = _currentUser.UserId;
            if (!userId.HasValue) return Result<IEnumerable<DashboardTileDto>>.Failure("Not signed in.");

            var tiles = await _db.UserDashboardTiles
                .Include(t => t.Department).Include(t => t.Branch)
                .Where(t => t.UserId == userId)
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Id)
                .ToListAsync();

            var result = new List<DashboardTileDto>();
            foreach (var t in tiles)
            {
                var metric = DashboardMetricCatalogue.Find(t.MetricKey);

                // Permission is re-checked on every read, not just on save. A role changed
                // after a tile was created must stop producing a number, or a demotion would
                // leave the old figures on screen.
                if (metric == null || !_currentUser.HasPermission(metric.Module, metric.Action))
                    continue;

                result.Add(new DashboardTileDto
                {
                    Id = t.Id, Title = t.Title, MetricKey = t.MetricKey,
                    DepartmentId = t.DepartmentId, BranchId = t.BranchId,
                    Period = t.Period, Colour = t.Colour, SortOrder = t.SortOrder,
                    Suffix = metric.Suffix,
                    Value = await EvaluateAsync(t, metric),
                    ScopeDisplay = Describe(t, metric),
                    Url = await BuildUrlAsync(t, metric)
                });
            }

            return Result<IEnumerable<DashboardTileDto>>.Success(result);
        }
        catch (Exception ex)
        {
            AppLogger.Error("DashboardPreferenceService.GetMyTilesAsync", ex);
            return Result<IEnumerable<DashboardTileDto>>.Failure("Could not load your tiles.");
        }
    }

    public async Task<Result<DashboardTileDto>> SaveTileAsync(SaveDashboardTileDto dto)
    {
        try
        {
            var userId = _currentUser.UserId;
            if (!userId.HasValue) return Result<DashboardTileDto>.Failure("Not signed in.");

            var metric = DashboardMetricCatalogue.Find(dto.MetricKey);
            if (metric == null) return Result<DashboardTileDto>.Failure("That metric does not exist.");

            // The gate that makes user-composed tiles safe: a tile can only be built from a
            // number its owner is already permitted to read.
            if (!_currentUser.HasPermission(metric.Module, metric.Action))
                return Result<DashboardTileDto>.Failure("You do not have access to that metric.");

            if (!ValidPeriods.Contains(dto.Period)) dto.Period = "today";
            if (!ValidColours.Contains(dto.Colour)) dto.Colour = "bg-c-blue";

            var tile = dto.Id > 0
                ? await _db.UserDashboardTiles.FirstOrDefaultAsync(t => t.Id == dto.Id && t.UserId == userId)
                : null;

            if (dto.Id > 0 && tile == null)
                return Result<DashboardTileDto>.Failure("That tile was not found.");

            if (tile == null)
            {
                // A dashboard is a glance, not a report. Twenty tiles is not a dashboard.
                var count = await _db.UserDashboardTiles.CountAsync(t => t.UserId == userId);
                if (count >= 8)
                    return Result<DashboardTileDto>.Failure("You already have 8 custom tiles. Remove one first.");

                tile = new UserDashboardTile
                {
                    UserId = userId.Value, SortOrder = count,
                    CreatedAt = DateTime.Now, CreatedBy = userId
                };
                _db.UserDashboardTiles.Add(tile);
            }
            else
            {
                tile.ModifiedAt = DateTime.Now;
                tile.ModifiedBy = userId;
            }

            tile.Title = dto.Title.Trim();
            tile.MetricKey = metric.Key;
            tile.DepartmentId = dto.DepartmentId;
            tile.BranchId = dto.BranchId;
            tile.Period = metric.SupportsPeriod ? dto.Period : "today";
            tile.Colour = dto.Colour;

            await _db.SaveChangesAsync();

            return Result<DashboardTileDto>.Success(new DashboardTileDto
            {
                Id = tile.Id, Title = tile.Title, MetricKey = tile.MetricKey,
                DepartmentId = tile.DepartmentId, BranchId = tile.BranchId,
                Period = tile.Period, Colour = tile.Colour, Suffix = metric.Suffix,
                Value = await EvaluateAsync(tile, metric)
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("DashboardPreferenceService.SaveTileAsync", ex);
            return Result<DashboardTileDto>.Failure("Could not save that tile.");
        }
    }

    public async Task<Result> DeleteTileAsync(int tileId)
    {
        try
        {
            var userId = _currentUser.UserId;
            var tile = await _db.UserDashboardTiles.FirstOrDefaultAsync(t => t.Id == tileId && t.UserId == userId);

            // Scoped to the owner: a guessed id must not remove somebody else's tile.
            if (tile == null) return Result.Failure("That tile was not found.");

            _db.UserDashboardTiles.Remove(tile);
            await _db.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("DashboardPreferenceService.DeleteTileAsync", ex);
            return Result.Failure("Could not delete that tile.");
        }
    }

    private static readonly HashSet<string> ValidPeriods =
        new(["today", "week", "month", "lastmonth"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ValidColours =
        new(["bg-c-blue", "bg-c-green", "bg-c-yellow", "bg-c-pink"], StringComparer.OrdinalIgnoreCase);

    private static (DateTime From, DateTime To) RangeFor(string period)
    {
        var today = DateTime.Today;
        return period.ToLowerInvariant() switch
        {
            "week"      => (today.AddDays(-(int)((int)today.DayOfWeek + 6) % 7), today),
            "month"     => (new DateTime(today.Year, today.Month, 1), today),
            "lastmonth" => (new DateTime(today.Year, today.Month, 1).AddMonths(-1),
                            new DateTime(today.Year, today.Month, 1).AddDays(-1)),
            _           => (today, today)
        };
    }

    /// <summary>
    /// Computes one tile. Every branch is a fixed query over a scope the caller chose from a
    /// list — there is no user-supplied SQL, column or table anywhere in here.
    /// </summary>
    private async Task<double?> EvaluateAsync(UserDashboardTile tile, DashboardMetricDto metric)
    {
        var (from, to) = RangeFor(tile.Period);

        IQueryable<Domain.Entities.AttendanceLog> Logs() =>
            _db.AttendanceLogs
               .Where(a => a.AttendanceDate >= from && a.AttendanceDate <= to)
               .Where(a => tile.DepartmentId == null || a.Employee.DepartmentId == tile.DepartmentId)
               .Where(a => tile.BranchId == null || a.Employee.BranchId == tile.BranchId);

        IQueryable<Domain.Entities.Employee> Employees() =>
            _db.Employees
               .Where(e => e.IsActive)
               .Where(e => tile.DepartmentId == null || e.DepartmentId == tile.DepartmentId)
               .Where(e => tile.BranchId == null || e.BranchId == tile.BranchId);

        return metric.Key switch
        {
            "present"    => await Logs().CountAsync(a => a.Status == AttendanceStatus.Present),
            "late"       => await Logs().CountAsync(a => a.Status == AttendanceStatus.Late),
            "absent"     => await Logs().CountAsync(a => a.Status == AttendanceStatus.Absent),
            "onleave"    => await Logs().CountAsync(a => a.Status == AttendanceStatus.OnLeave),
            "nocheckout" => await Logs().CountAsync(a => a.CheckIn != null && a.CheckOut == null),

            "othours"    => Math.Round((await _db.OvertimeRecords
                                .Where(o => o.OvertimeDate >= from && o.OvertimeDate <= to)
                                .Where(o => o.Status == OvertimeStatus.Approved)
                                .Where(o => tile.DepartmentId == null || o.Employee.DepartmentId == tile.DepartmentId)
                                .Where(o => tile.BranchId == null || o.Employee.BranchId == tile.BranchId)
                                .SumAsync(o => (int?)(o.ApprovedMinutes ?? o.ClaimedMinutes)) ?? 0) / 60.0, 1),

            "otpending"  => await _db.OvertimeRecords
                                .Where(o => o.Status == OvertimeStatus.Pending)
                                .Where(o => tile.DepartmentId == null || o.Employee.DepartmentId == tile.DepartmentId)
                                .Where(o => tile.BranchId == null || o.Employee.BranchId == tile.BranchId)
                                .CountAsync(),

            "leavepending" => await _db.LeaveRequests
                                .Where(l => l.Status == LeaveStatus.Pending)
                                .Where(l => tile.DepartmentId == null || l.Employee.DepartmentId == tile.DepartmentId)
                                .Where(l => tile.BranchId == null || l.Employee.BranchId == tile.BranchId)
                                .CountAsync(),

            "headcount"     => await Employees().CountAsync(),
            "missingenroll" => await Employees().CountAsync(e => e.BiometricEnrollId == null),

            _ => null
        };
    }

    /// <summary>
    /// The screen that explains the number, pre-filtered to the tile's own scope and period.
    ///
    /// Built here rather than in the page script so the mapping sits beside the metric it
    /// belongs to — a number you cannot open is a number nobody can act on, and a link that
    /// lands on an unfiltered screen is worse than none because it looks like it worked.
    ///
    /// Branch is deliberately not passed to Attendance Review: that screen has no branch
    /// filter, and sending a parameter it ignores would show a wider figure than the tile.
    /// </summary>
    private async Task<string?> BuildUrlAsync(UserDashboardTile tile, DashboardMetricDto metric)
    {
        var (from, to) = RangeFor(tile.Period);
        var dept = tile.DepartmentId.HasValue ? $"&departmentId={tile.DepartmentId}" : "";
        var range = $"from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";

        return metric.Key switch
        {
            "present" or "late" or "absent" or "onleave" or "nocheckout"
                => $"/Admin/AttendanceReview?{range}{dept}&filter={metric.Key}",

            "othours"   => $"/Admin/OvertimeRegister?{range}{dept}&status=2",

            // Not the tile's period. This metric counts every pending record regardless of
            // date, so a link carrying "today" opened a screen showing none of them — the
            // tile said 5 and the page said 0. The link has to span the same set the number
            // was counted over, so it is derived from the data rather than from the period.
            "otpending" => await PendingOvertimeUrlAsync(tile, dept),

            "leavepending" => "/Admin/Leave?status=Pending",

            "headcount" or "missingenroll" => "/Admin/Employees",

            _ => null
        };
    }

    /// <summary>
    /// Overtime Approval filtered to the whole span of pending records, so the screen shows
    /// exactly what the tile counted. Its own default is the current month, which would hide
    /// anything older — and something pending from two months ago is precisely what a tile
    /// like this exists to surface.
    /// </summary>
    private async Task<string> PendingOvertimeUrlAsync(UserDashboardTile tile, string dept)
    {
        var pending = _db.OvertimeRecords
            .Where(o => o.Status == OvertimeStatus.Pending)
            .Where(o => tile.DepartmentId == null || o.Employee.DepartmentId == tile.DepartmentId)
            .Where(o => tile.BranchId == null || o.Employee.BranchId == tile.BranchId);

        var earliest = await pending.MinAsync(o => (DateTime?)o.OvertimeDate);
        var latest = await pending.MaxAsync(o => (DateTime?)o.OvertimeDate);

        // Nothing pending: fall back to today rather than an empty range, so the screen opens
        // on a sensible day instead of 01-01-0001.
        var from = (earliest ?? DateTime.Today).Date;

        // A record dated ahead of today is unusual but possible after a manual entry; the
        // range has to reach it or the count and the list disagree again.
        var to = (latest ?? DateTime.Today).Date;
        if (to < DateTime.Today) to = DateTime.Today;

        return $"/Admin/OvertimeApproval?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}{dept}";
    }

    private static string Describe(UserDashboardTile tile, DashboardMetricDto metric)
    {
        var parts = new List<string>();
        if (tile.Department != null) parts.Add(tile.Department.Name);
        if (tile.Branch != null) parts.Add(tile.Branch.Name);

        if (metric.SupportsPeriod)
        {
            parts.Add(tile.Period.ToLowerInvariant() switch
            {
                "week" => "this week",
                "month" => "this month",
                "lastmonth" => "last month",
                _ => "today"
            });
        }

        return parts.Count == 0 ? "All employees" : string.Join(" · ", parts);
    }
}

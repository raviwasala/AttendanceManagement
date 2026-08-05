using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Entities;
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
}

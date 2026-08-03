using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AttendanceSystem.Application;

/// <summary>DI registration for all Application-layer services.</summary>
public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IDesignationService, DesignationService>();
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<ILeaveService, LeaveService>();
        services.AddScoped<IHolidayService, HolidayService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        return services;
    }
}

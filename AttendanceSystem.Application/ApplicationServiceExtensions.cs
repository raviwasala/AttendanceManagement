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
        // Shared by leave and overtime — both ask "may this person decide this request".
        services.AddScoped<IApprovalScopeService, ApprovalScopeService>();
        services.AddScoped<IMonthEndService, MonthEndService>();
        services.AddScoped<ILeaveService, LeaveService>();
        services.AddScoped<IHolidayService, HolidayService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IShiftRosterService, ShiftRosterService>();
        services.AddScoped<IAttendanceReviewService, AttendanceReviewService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ISelfServiceService, SelfServiceService>();
        services.AddScoped<IOvertimeService, OvertimeService>();
        services.AddScoped<IPayrollSetupService, PayrollSetupService>();
        services.AddScoped<IEmployeePayrollService, EmployeePayrollService>();
        services.AddScoped<IMonthlyTransactionService, MonthlyTransactionService>();
        services.AddScoped<ILoanService, LoanService>();
        return services;
    }
}

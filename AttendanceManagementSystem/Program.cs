using AttendanceSystem.Application;
using AttendanceSystem.Common.Configuration;
using AttendanceSystem.Common.Exceptions;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Infrastructure;
using AttendanceManagementSystem.UI.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AttendanceManagementSystem;

internal static class Program
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        AppConfiguration.Initialize(config);
        AppLogger.Initialize(config["Serilog:LogFilePath"] ?? "Logs/attendance-.log");
        AppLogger.Info("Application starting...");
        GlobalExceptionHandler.SetupUnhandledExceptionHandlers();

        // Wire WinForms thread exception handler here in the UI entry point
        Application.ThreadException += (s, e) =>
        {
            GlobalExceptionHandler.HandleUI(e.Exception, "ThreadException");
            MessageBox.Show($"An unexpected error occurred:\n\n{e.Exception.Message}",
                "Application Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);
        services.AddApplication();

        // Register all UI Forms for DI
        services.AddTransient<LoginForm>();
        services.AddTransient<MainForm>();
        services.AddTransient<DashboardForm>();
        services.AddTransient<EmployeeListForm>();
        services.AddTransient<EmployeeEditForm>();
        services.AddTransient<DepartmentForm>();
        services.AddTransient<DesignationForm>();
        services.AddTransient<BranchForm>();
        services.AddTransient<ShiftForm>();
        services.AddTransient<AssignShiftForm>();
        services.AddTransient<AttendanceForm>();
        services.AddTransient<AttendanceHistoryForm>();
        services.AddTransient<LeaveTypeForm>();
        services.AddTransient<LeaveRequestForm>();
        services.AddTransient<LeaveApprovalForm>();
        services.AddTransient<HolidayForm>();
        services.AddTransient<UserForm>();
        services.AddTransient<RoleForm>();
        services.AddTransient<ReportForm>();
        services.AddTransient<SettingsForm>();
        services.AddTransient<AuditLogForm>();
        services.AddTransient<ChangePasswordForm>();

        ServiceProvider = services.BuildServiceProvider();

        using (var scope = ServiceProvider.CreateScope())
        {
            try
            {
                var ctx = scope.ServiceProvider
                    .GetRequiredService<AttendanceSystem.Infrastructure.Data.AttendanceDbContext>();
                ctx.Database.EnsureCreated();
                AppLogger.Info("Database initialised.");
            }
            catch (Exception ex)
            {
                AppLogger.Fatal("Database initialisation failed.", ex);
                MessageBox.Show($"Database connection failed:\n\n{ex.Message}\n\nCheck connection string in appsettings.json.",
                    "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        Application.Run(ServiceProvider.GetRequiredService<LoginForm>());
        AppLogger.Info("Application shut down.");
    }
}
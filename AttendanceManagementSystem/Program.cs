using AttendanceSystem.Application;
using AttendanceSystem.Common.Configuration;
using AttendanceSystem.Common.Exceptions;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Infrastructure;
using AttendanceManagementSystem.UI.Forms;
using Microsoft.EntityFrameworkCore;
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

        // Later sources win. appsettings.json holds non-secret defaults only; the
        // connection string and any other credentials come from user-secrets during
        // development and from ATTENDANCE_-prefixed environment variables when deployed.
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly(), optional: true)
            .AddEnvironmentVariables("ATTENDANCE_")
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

        // The desktop client serves exactly one user, so the current-user context is a
        // singleton backed by DesktopSession. The web host registers a per-request
        // implementation instead — see AttendanceSystem.Web/Session.
        services.AddSingleton<AttendanceSystem.Common.Session.ICurrentUserContext,
                              AttendanceManagementSystem.Session.DesktopUserContext>();

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
                // See the note in AttendanceSystem.Web/Program.cs — Migrate(), not EnsureCreated().
                ctx.Database.Migrate();
                AppLogger.Info("Database migrated to the latest version.");
            }
            catch (Exception ex)
            {
                AppLogger.Fatal("Database migration failed.", ex);
                MessageBox.Show($"Database connection failed:\n\n{ex.Message}\n\nCheck that the connection string is configured (see README).",
                    "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        Application.Run(ServiceProvider.GetRequiredService<LoginForm>());
        AppLogger.Info("Application shut down.");
    }
}
using AttendanceSystem.Application;
using AttendanceSystem.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ──────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/attendance-web-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();
builder.Services.AddSerilog();

// ── MVC ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = null; // keep PascalCase
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// ── Session ──────────────────────────────────────────────────────────────────
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromMinutes(AttendanceSystem.Common.Constants.AppConstants.SessionTimeoutMinutes);
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    // The session cookie authenticates every request — it must not travel over plain HTTP.
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Lax;
});
builder.Services.AddHttpContextAccessor();

// Who the current request is acting as. Scoped, so concurrent requests never see
// each other's user — the previous static AppSession was shared across all of them.
builder.Services.AddScoped<AttendanceSystem.Common.Session.ICurrentUserContext,
                           AttendanceSystem.Web.Session.HttpSessionUserContext>();

// ── Application + Infrastructure layers ─────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// ── Pipeline ─────────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Serve the /assets folder (theme, icons, JS) which lives outside wwwroot
var assetsPath = Path.Combine(builder.Environment.ContentRootPath, "assets");
if (Directory.Exists(assetsPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(assetsPath),
        RequestPath  = "/assets"
    });
}

app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}")
    .WithStaticAssets();

// Bring the database up to the latest migration.
// Migrate() rather than EnsureCreated(): EnsureCreated builds the schema once and then
// ignores every later model change, so the app would silently run against a stale schema.
using (var scope = app.Services.CreateScope())
{
    try
    {
        var ctx = scope.ServiceProvider
            .GetRequiredService<AttendanceSystem.Infrastructure.Data.AttendanceDbContext>();
        ctx.Database.Migrate();
        Log.Information("Database migrated to the latest version.");
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Database initialisation failed.");
        throw;
    }
}

try
{
    Log.Information("Starting Attendance Management System Web Application");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}


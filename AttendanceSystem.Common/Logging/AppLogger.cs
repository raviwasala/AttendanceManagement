using Serilog;

namespace AttendanceSystem.Common.Logging;

/// <summary>Centralized application logger using Serilog.</summary>
public static class AppLogger
{
    private static ILogger? _logger;

    public static void Initialize(string logFilePath = "logs/app-.log")
    {
        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(logFilePath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Logger = _logger;
    }

    public static void Info(string message, params object[] args) =>
        Log.Information(message, args);

    public static void Warning(string message, params object[] args) =>
        Log.Warning(message, args);

    public static void Error(string message, Exception? ex = null) =>
        Log.Error(ex, message);

    public static void Debug(string message, params object[] args) =>
        Log.Debug(message, args);

    public static void Fatal(string message, Exception? ex = null) =>
        Log.Fatal(ex, message);
}

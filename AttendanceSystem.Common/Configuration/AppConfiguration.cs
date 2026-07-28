using Microsoft.Extensions.Configuration;

namespace AttendanceSystem.Common.Configuration;

/// <summary>Provides access to appsettings.json configuration values.</summary>
public static class AppConfiguration
{
    private static IConfiguration? _config;

    public static void Initialize(IConfiguration configuration) =>
        _config = configuration;

    public static string GetConnectionString(string name = "DefaultConnection") =>
        _config?.GetConnectionString(name) ?? throw new InvalidOperationException("Configuration not initialised.");

    public static string Get(string key) =>
        _config?[key] ?? string.Empty;

    public static T? GetSection<T>(string sectionName) =>
        _config != null ? _config.GetSection(sectionName).Get<T>() : default;
}

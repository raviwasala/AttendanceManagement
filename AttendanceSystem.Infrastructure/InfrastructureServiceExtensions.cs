using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Domain.Interfaces;
using AttendanceSystem.Infrastructure.Data;
using AttendanceSystem.Infrastructure.Dapper;
using AttendanceSystem.Infrastructure.Services;
using AttendanceSystem.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AttendanceSystem.Infrastructure;

/// <summary>DI registration for all Infrastructure services.</summary>
public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Fail loudly at startup rather than with an opaque SqlException on the first
            // query — a blank connection string almost always means secrets were not configured.
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured. Set it with " +
                "`dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"<value>\"` for local " +
                "development, or via the ConnectionStrings__DefaultConnection environment variable. " +
                "See README.md → \"Configuration & secrets\".");
        }

        services.AddDbContext<AttendanceDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3)));

        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();
        services.AddSingleton<DapperContext>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IBiometricImportService, BiometricImportService>();
        services.AddScoped<IEmailService, EmailService>();

        // Device protocol client. Swapping in a full ZKTeco implementation (phase 2) is a
        // one-line change here — nothing outside Infrastructure names the concrete type.
        services.AddScoped<IFingerprintDeviceClient, Devices.TcpFingerprintDeviceClient>();

        return services;
    }
}

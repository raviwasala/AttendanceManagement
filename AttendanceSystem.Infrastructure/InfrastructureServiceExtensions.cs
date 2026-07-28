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
        services.AddDbContext<AttendanceDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"),
                sql => sql.EnableRetryOnFailure(3)));

        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();
        services.AddSingleton<DapperContext>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IBiometricImportService, BiometricImportService>();

        return services;
    }
}

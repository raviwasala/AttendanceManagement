using AttendanceSystem.Common.Session;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AttendanceSystem.Infrastructure.Data;

/// <summary>
/// Lets <c>dotnet ef</c> construct the context without starting a host.
///
/// Scaffolding a migration only needs the model, not a reachable server, so the design-time
/// connection string is a placeholder unless one is supplied. This keeps migration authoring
/// working on a machine that has no secrets configured. Commands that DO touch the database
/// (<c>database update</c>, <c>dbcontext script</c>) need a real value — set
/// <c>ATTENDANCE_ConnectionStrings__DefaultConnection</c> before running them.
/// </summary>
public class AttendanceDbContextFactory : IDesignTimeDbContextFactory<AttendanceDbContext>
{
    private const string PlaceholderConnection =
        "Server=(localdb)\\MSSQLLocalDB;Database=AttendanceDB_DesignTime;Trusted_Connection=True;TrustServerCertificate=True";

    public AttendanceDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ATTENDANCE_ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? PlaceholderConnection;

        var options = new DbContextOptionsBuilder<AttendanceDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        // Design-time operations act on behalf of nobody, so audit columns stay unattributed.
        return new AttendanceDbContext(options, AnonymousUserContext.Instance);
    }
}

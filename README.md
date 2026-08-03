# Attendance Management System

Employee attendance, leave and shift management. Two front-ends share one application core:

| Project | Purpose |
| --- | --- |
| `AttendanceSystem.Domain` | Entities, enums, repository interfaces |
| `AttendanceSystem.Application` | Services, DTOs, use-case logic |
| `AttendanceSystem.Infrastructure` | EF Core, repositories, biometric import, e-mail, reports |
| `AttendanceSystem.Common` | Cross-cutting helpers, constants, current-user abstraction |
| `AttendanceSystem.Web` | ASP.NET Core MVC + JSON API |
| `AttendanceManagementSystem` | WinForms desktop client |

## Documentation

| Document | Audience |
|---|---|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Layering, data access, authorization model, domain rules, known issues |
| [docs/DEVELOPER-GUIDE.md](docs/DEVELOPER-GUIDE.md) | Setup, migrations, adding a feature, conventions, troubleshooting |
| [docs/USER-GUIDE.md](docs/USER-GUIDE.md) | Day-to-day use for administrators, HR staff and employees |

---

## Configuration & secrets

**No credentials are stored in the repository.** The committed `appsettings.json` files hold
non-secret defaults with blank placeholders; the application fails fast at startup with a
descriptive error if the connection string has not been supplied.

### Local development — user secrets

Web:

```powershell
cd AttendanceSystem.Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.;Database=AttendanceDB;Trusted_Connection=True;TrustServerCertificate=True"
dotnet user-secrets set "Smtp:Host" "smtp.example.com"
dotnet user-secrets set "Smtp:Username" "no-reply@example.com"
dotnet user-secrets set "Smtp:Password" "<app-password>"
dotnet user-secrets set "Smtp:FromAddress" "no-reply@example.com"
```

Desktop:

```powershell
cd AttendanceManagementSystem
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.;Database=AttendanceDB;Trusted_Connection=True;TrustServerCertificate=True"
```

### Deployment — environment variables

| Host | Variable |
| --- | --- |
| Web | `ConnectionStrings__DefaultConnection`, `Smtp__Host`, `Smtp__Username`, `Smtp__Password` |
| Desktop | `ATTENDANCE_ConnectionStrings__DefaultConnection` |

Use a least-privilege SQL login — not `sa` — and leave `Encrypt` at its secure default rather
than turning it off.

---

## Database migrations

The schema is managed by EF Core migrations. Both hosts call `Database.Migrate()` on startup,
so a normal run applies anything outstanding.

```powershell
dotnet tool restore                      # once, restores dotnet-ef from .config/dotnet-tools.json

# add a migration after changing the model
dotnet ef migrations add <Name> --project AttendanceSystem.Infrastructure --startup-project AttendanceSystem.Infrastructure --output-dir Data\Migrations

# apply manually (needs a real connection string)
$env:ATTENDANCE_ConnectionStrings__DefaultConnection = "<connection string>"
dotnet ef database update --project AttendanceSystem.Infrastructure --startup-project AttendanceSystem.Infrastructure
```

Scaffolding uses `AttendanceDbContextFactory`, so authoring a migration needs no configured
secrets; only commands that touch the server do.

### Upgrading a database created before migrations existed

Earlier builds used `EnsureCreated()`, which leaves no `__EFMigrationsHistory` table. Running
the new code against such a database makes `Migrate()` try to create tables that already exist.
Pick one:

- **Development** — drop the database and let the app recreate it:
  `dotnet ef database drop --project AttendanceSystem.Infrastructure --startup-project AttendanceSystem.Infrastructure`
- **Data you need to keep** — baseline it by marking the initial migration as already applied,
  then apply the delta by hand:
  ```sql
  CREATE TABLE [__EFMigrationsHistory](
      [MigrationId] nvarchar(150) NOT NULL PRIMARY KEY,
      [ProductVersion] nvarchar(32) NOT NULL);
  INSERT INTO [__EFMigrationsHistory] VALUES (N'20260803110129_InitialCreate', N'10.0.10');
  ```
  Then add the columns and permission rows the initial migration would have created —
  `dotnet ef migrations script` prints exactly what they are.

---

## Access control

Authorization is permission-based. A permission is a `{Module}.{Action}` pair
(`Employees.Delete`, `Leave.Approve`, …); permissions are granted to roles, and roles to users.

- The catalogue and the default per-role grants are seeded in `AttendanceDbContext.SeedPermissions`.
- On sign-in the user's permission set is written into their session and read back per request
  through `ICurrentUserContext`.
- Endpoints are guarded with `[SessionAuthorize(Modules.X, Actions.Y)]`. The parameterless
  `[SessionAuthorize]` only proves *someone* is signed in — it is not an authorization decision.
- Views use `ViewContext.HasPermission(...)` to hide controls, which is presentation only. The
  authoritative check is always the one on the endpoint.

Default seeded roles: **Administrator** (everything), **HR Manager** (operations, but no user,
role or audit-log access), **Employee** (self-service only).

Default sign-in is `admin` / `Admin@123` — change it immediately after first run.

### Current user

`ICurrentUserContext` reports who the current operation acts for. The web host registers a
**scoped** implementation backed by `HttpContext.Session`; the desktop host registers a
singleton over `DesktopSession`, which is correct there because the process serves one user.
Do not reintroduce process-wide user state in a shared project — in the web host it is shared
by every concurrent request.

---

## Running

```powershell
dotnet run --project AttendanceSystem.Web          # web
dotnet run --project AttendanceManagementSystem    # desktop
```

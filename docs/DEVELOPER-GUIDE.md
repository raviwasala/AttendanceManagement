# Developer Guide

How to build, run, extend and debug the Attendance Management System.
For *why* it is shaped the way it is, see [ARCHITECTURE.md](ARCHITECTURE.md).

---

## 1. Prerequisites

| Requirement | Version used |
|---|---|
| .NET SDK | 10.0.301 |
| SQL Server | 2014+ (developed against a local default instance) |
| OS | Windows — the desktop client is WinForms and `.mdb` import uses OleDb |
| IDE | Visual Studio 2022+ or VS Code + C# Dev Kit |

Optional but usually needed: the **Microsoft Access Database Engine (64-bit)** redistributable,
required only to import biometric `.mdb`/`.accdb` files. Check whether it is already present:

```powershell
(New-Object System.Data.OleDb.OleDbEnumerator).GetElements() |
  Where-Object { $_.SOURCES_NAME -like 'Microsoft.ACE*' }
```

---

## 2. First-time setup

```powershell
git clone <repo>
cd AttendanceManagementSystem

# EF Core CLI (pinned in .config/dotnet-tools.json)
dotnet tool restore

# Secrets — nothing sensitive is committed, so this step is mandatory
cd AttendanceSystem.Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.;Database=AttendanceDB;Trusted_Connection=True;TrustServerCertificate=True"

cd ..\AttendanceManagementSystem
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.;Database=AttendanceDB;Trusted_Connection=True;TrustServerCertificate=True"

cd ..
dotnet build AttendanceManagementSystem.slnx
```

If the connection string is missing, the app fails fast at startup with a message saying so — it
does not fall through to an opaque `SqlException` on the first query.

### Run

```powershell
dotnet run --project AttendanceSystem.Web --launch-profile https   # https://localhost:7151
dotnet run --project AttendanceSystem.Web --launch-profile http    # http://localhost:5086
dotnet run --project AttendanceManagementSystem                    # desktop client
```

The database is created and migrated automatically on first run. Sign in with
**`admin` / `Admin@123`** and change it immediately.

> **Prefer the `https` profile.** Session and remember-me cookies are marked `Secure`
> (`CookieSecurePolicy.Always`), so over plain HTTP you appear to sign in and are immediately
> signed out again. The `http` profile is fine for hitting API endpoints or checking static
> assets, but not for a login flow.

---

## 3. Solution layout

```
AttendanceManagementSystem.slnx
├── .config/dotnet-tools.json          dotnet-ef pin
├── docs/                              this documentation
│   └── legacy/AttendanceDB_Setup.sql  pre-migrations full schema script, kept for reference
├── AttendanceSystem.Domain/           entities, enums, repository interfaces
├── AttendanceSystem.Application/      services, DTOs, service interfaces, AttendanceCalculator
├── AttendanceSystem.Infrastructure/   EF Core, Dapper, email, import, reports, device client
│   └── Data/Migrations/               EF migrations
├── AttendanceSystem.Common/           helpers, constants, Result, ICurrentUserContext
├── AttendanceSystem.Web/              MVC + JSON API + Adminty theme
│   ├── assets/                        purchased theme — outside wwwroot, served at /assets
│   └── wwwroot/js/pages/              one script per screen
└── AttendanceManagementSystem/        WinForms desktop client
```

> `AttendanceSystem.Web/assets/` is a **third-party UI framework kept whole**. Much of it is not
> referenced by any current view. That is expected — do not prune it by reference-checking.

---

## 4. Database work

### Adding a migration

```powershell
dotnet ef migrations add <Name> `
  --project AttendanceSystem.Infrastructure `
  --startup-project AttendanceSystem.Infrastructure `
  --output-dir Data\Migrations
```

`AttendanceDbContextFactory` supplies a design-time context, so scaffolding needs no configured
secrets. Commands that touch the server do:

```powershell
$env:ATTENDANCE_ConnectionStrings__DefaultConnection = "<connection string>"
dotnet ef database update --project AttendanceSystem.Infrastructure --startup-project AttendanceSystem.Infrastructure
```

Check for drift before committing:

```powershell
dotnet ef migrations has-pending-model-changes --project AttendanceSystem.Infrastructure --startup-project AttendanceSystem.Infrastructure
```

### Current migrations

| Migration | Adds |
|---|---|
| `InitialCreate` | core schema |
| `AddFingerprintDevices` | `Device`, `DevicePunch`, `DeviceSyncLog`, `DeviceUserMapping` |
| `AddShiftOvertimeAndNightShift` | night-shift flag, OT fields on `Shift` |
| `AddPagingAndConfirmSettings` | `DefaultPageSize`, `ConfirmBeforeDelete` on settings |
| `AddOvertimeManagement` | `OvertimeRule`, `OvertimeRecord` |
| `AddShiftLateAllowanceAndWorkingDays` | `AllowedLateDaysPerMonth`, `WorkingDaysPerMonth` |
| `AddEmployeeUserCodeNicAndInitials` | `UserCode`, `Nic`, `NameWithInitials` on `Employee` |

### Upgrading a pre-migrations database

Databases created by the old `EnsureCreated()` code have no `__EFMigrationsHistory`, so
`Migrate()` tries to recreate existing tables and fails. Back up, then baseline: generate the
initial migration's script, apply only its history row, and let later migrations run normally.

---

## 5. Adding a feature

Worked example: a hypothetical **Training Records** module. (Overtime, Devices and Shift Roster
already exist — read those for a real reference implementation.)

### 5.1 Domain

```csharp
// AttendanceSystem.Domain/Entities/TrainingRecord.cs
public class TrainingRecord : BaseEntity      // BaseEntity gives Id, IsDeleted, audit fields
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateTime CompletedOn { get; set; }
    public string Course { get; set; } = string.Empty;
}
```

### 5.2 Map it

In `AttendanceDbContext.OnModelCreating` — and **always add the soft-delete filter**, or deleted
rows keep appearing:

```csharp
modelBuilder.Entity<TrainingRecord>(e =>
{
    e.Property(x => x.Course).HasMaxLength(200).IsRequired();
    e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId)
     .OnDelete(DeleteBehavior.Restrict);
    e.HasQueryFilter(x => !x.IsDeleted);
});
```

Then add the migration (§4).

### 5.3 Permissions

Append to `PermissionCatalogue` — **append only, never reorder or remove.** Ids come from
position, and existing `RolePermission` rows point at them. Add the module name to
`AppConstants.Modules`, and a migration granting the new permissions to the roles that need them.

### 5.4 Service

Interface in `AttendanceSystem.Application/Interfaces/IServices.cs`, implementation in
`Services/`. Return `Result`/`Result<T>`; never throw for an expected failure. Register it in
`ApplicationServiceExtensions`.

### 5.5 API + view

```csharp
[Route("api/training")]
[SessionAuthorize]
public class TrainingApiController : ApiControllerBase
{
    [HttpGet]
    [SessionAuthorize(Modules.Training, Actions.View)]
    public async Task<IActionResult> GetAll() { … }
}
```

Every endpoint gets `[SessionAuthorize(Module, Action)]` with the *right* action — class-level
`[SessionAuthorize]` alone only proves someone is signed in.

Add a Razor view under `Views/Admin/`, a script under `wwwroot/js/pages/admin/`, and a sidebar
entry in `_Layout.cshtml` wrapped in `@if (CanView(...))`.

### 5.6 Checklist

- [ ] Soft-delete query filter on the entity
- [ ] Migration added and applied
- [ ] Permissions appended (not reordered) + granted by migration
- [ ] Service returns `Result<T>`, registered in DI
- [ ] Every endpoint carries `[SessionAuthorize(Module, Action)]`
- [ ] Every database value rendered in JS passes through `esc()`
- [ ] Sidebar entry gated by `CanView`
- [ ] No new build warnings

---

## 6. Front-end conventions

- **PascalCase JSON.** `Program.cs` sets `PropertyNamingPolicy = null`, so read `e.EmployeeCode`,
  not `e.employeeCode`.
- **`esc()` everything from the database.** Table renderers concatenate strings; an unescaped
  name is stored XSS, and names can arrive from a device file rather than a vetted form.
- **`amsPage`** (`site.js`) renders tables with paging; pass `server: { total, page, pageSize,
  onPage }` for server-side paging.
- **Toasts** — `notifySuccess` / `notifyError` / `notifyConfirm`.
- **Sidebar visibility** uses `@if (CanView(...))`, never the `hidden` attribute — the theme's
  `display:block` rule beats `[hidden]`.

---

## 7. Build and diagnostics

```powershell
dotnet build AttendanceManagementSystem.slnx --no-incremental
```

### Warning baseline

| Project set | Warnings |
|---|---|
| `AttendanceSystem.Web` and its dependencies | **0** |
| Full solution (adds the WinForms desktop project) | ~88 |

The web chain is clean; keep it that way, since a new warning is easier to fix the day it
appears than in a batch of a hundred.

The desktop project's ~88 are pre-existing: `CS8618` (non-nullable WinForms fields declared
`= null!`), `CS8600/8602/8604` (`SelectedRows[0].DataBoundItem` cast without a null check) and
`CS0414`. They are a known baseline, not a regression.

Platform warnings (`CA1416`) are *not* suppressed. OleDb entry points check
`OperatingSystem.IsWindows()` once and delegate to `[SupportedOSPlatform("windows")]` methods.
Follow that pattern rather than adding a suppression.

### Common problems

| Symptom | Cause |
|---|---|
| Startup throws about `DefaultConnection` | user-secrets not set — §2 |
| `Migrate()` fails, "There is already an object named…" | pre-migrations database; baseline it — §4 |
| Login succeeds then bounces back to login | running over HTTP; cookies are `Secure` — use the `https` profile |
| Build fails with `MSB3027 … locked by` | the app or a debugger is running; stop it first |
| Menu item visible but page 403s | permission check on the endpoint but not on the nav entry |
| Icons render as empty boxes | feather webfont blocked — check it is same-origin |
| Toasts fall back to `alert()` | CDN unreachable |
| Import says "no punches in range" | the date window misses the file's data — the message states the file's actual coverage |

---

## 8. Security checklist for changes

- [ ] Every new endpoint has `[SessionAuthorize(Module, Action)]` with the *right* action.
- [ ] Acting user comes from `CurrentUserId`, never the request body.
- [ ] New permissions appended to `PermissionCatalogue` (not reordered) + migration.
- [ ] Any database value rendered in JS goes through `esc()`.
- [ ] No secrets in `appsettings.json`; no tokens or passwords in logs.
- [ ] State-changing MVC form posts carry `[ValidateAntiForgeryToken]`.
- [ ] New file-upload types are safe to serve (`.svg` can carry script — see
      `SettingsApiController.UploadLogo`, which currently allows it).

---

## 9. Known issues / backlog

1. **No tests.** See [ARCHITECTURE.md](ARCHITECTURE.md) §11 for the priority order.
   `AttendanceCalculator` is first: pure, static, and every other number depends on it.
2. **`esc()` sweep incomplete** — several page scripts still interpolate raw values.
3. **`.svg` logo upload** is a stored-XSS vector; drop it from the allow-list.
4. **Time handling** mixes local and UTC; unify on `DateTimeOffset`/`TimeProvider`.
5. **Session id not rotated** on sign-in; needs cookie authentication for a full fix.
6. **CDN assets** (toastr, SweetAlert2, Chart.js) should be vendored for on-premise deployment.
7. **`Infrastructure` → `Application`** reference inverts the intended dependency direction.
8. **Action buttons are not permission-gated** in views — users see Edit/Delete, then get a 403
   toast. Server-side is safe; this is polish.
9. **Device integration is phase 1 only** — probe only, no punch pulling. See
   [DEVICE-INTEGRATION-DESIGN.md](DEVICE-INTEGRATION-DESIGN.md).
10. **Employee photos are not shown in the employee list** — `EmployeeListItemDto` omits
    `Photo` deliberately, so list queries do not carry image bytes. A
    `/api/employees/{id}/photo` endpoint with cache headers is the right fix.

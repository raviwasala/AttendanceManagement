# Developer Guide

How to build, run, extend and debug the Attendance Management System.
For *why* it is shaped the way it is, see [ARCHITECTURE.md](ARCHITECTURE.md).

---

## 1. Prerequisites

| Requirement | Version used |
|---|---|
| .NET SDK | 10.0.301 |
| SQL Server | 2014+ (developed against a local default instance) |
| OS | Windows — the desktop client is WinForms and biometric `.mdb` import uses OleDb |
| IDE | Visual Studio 2022+ or VS Code + C# Dev Kit |

Optional: the **Microsoft Access Database Engine** redistributable, required only for importing
biometric `.mdb`/`.accdb` files.

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

If the connection string is missing the app fails fast at startup with a message telling you
this — it does not fall through to an opaque `SqlException` on the first query.

### Run

```powershell
dotnet run --project AttendanceSystem.Web            # https://localhost:7443 (or launchSettings port)
dotnet run --project AttendanceManagementSystem      # desktop client
```

The database is created and migrated automatically on first run. Sign in with
**`admin` / `Admin@123`** and change it immediately.

> **Use HTTPS.** Session and remember-me cookies are marked `Secure`, so sessions do not
> persist over plain HTTP — you will appear to log in and immediately be logged out again.

---

## 3. Solution layout

```
AttendanceManagementSystem.slnx
├── .config/dotnet-tools.json          dotnet-ef pin
├── docs/                              this documentation
├── AttendanceSystem.Domain/           entities, enums, repository interfaces
├── AttendanceSystem.Application/      services, DTOs, service interfaces
├── AttendanceSystem.Infrastructure/   EF Core, Dapper, email, import, reports
│   └── Data/Migrations/               EF migrations
├── AttendanceSystem.Common/           helpers, constants, Result, ICurrentUserContext
├── AttendanceSystem.Web/              MVC + JSON API + Adminty theme
└── AttendanceManagementSystem/        WinForms desktop client
```

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

### Upgrading a pre-migrations database

Databases created by the old `EnsureCreated()` code have no `__EFMigrationsHistory`, so
`Migrate()` tries to recreate existing tables and fails. Back up, then baseline — see the
"Upgrading a database created before migrations existed" section of `README.md`.

---

## 5. Adding a feature

Worked example: a new **Overtime** module.

### 5.1 Domain

```csharp
// AttendanceSystem.Domain/Entities/OvertimeRequest.cs
public class OvertimeRequest : BaseEntity      // BaseEntity gives Id, IsDeleted, audit fields
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateTime Date { get; set; }
    public double Hours { get; set; }
    public OvertimeStatus Status { get; set; }
}
```

### 5.2 Map it

In `AttendanceDbContext.OnModelCreating` — and **always add the soft-delete filter**, otherwise
deleted rows keep appearing:

```csharp
modelBuilder.Entity<OvertimeRequest>(e =>
{
    e.HasKey(x => x.Id);
    e.Property(x => x.Hours).HasPrecision(5, 2);
    e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
    e.HasQueryFilter(x => !x.IsDeleted);
});
```

Expose it on `IUnitOfWork` / `UnitOfWork` (`IRepository<OvertimeRequest> Overtime`), then add a
migration.

### 5.3 Permissions

Add the module to `AppConstants.Modules`, then **append** to `PermissionCatalogue` in
`AttendanceDbContext` — never reorder or remove existing entries, ids are positional:

```csharp
(AppConstants.Modules.Overtime, [Actions.View, Actions.Create, Actions.Edit, Actions.Approve]),
```

Grant it to roles in `SeedPermissions`, and add a migration so existing databases get the rows.

### 5.4 Application service

```csharp
public class OvertimeService : IOvertimeService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _currentUser;   // never a static

    public async Task<Result<OvertimeDto>> SaveAsync(SaveOvertimeDto dto)
    {
        try
        {
            if (dto.Hours <= 0) return Result<OvertimeDto>.Failure("Hours must be greater than zero.");

            var entity = new OvertimeRequest { /* ... */ CreatedBy = _currentUser.UserId };
            await _uow.Overtime.AddAsync(entity);
            await _uow.SaveChangesAsync();                // the service commits, not the repository
            await _audit.LogAsync(Modules.Overtime, "Create", _currentUser.UserId, nameof(OvertimeRequest), entity.Id);

            return Result<OvertimeDto>.Success(Map(entity));
        }
        catch (Exception ex)
        {
            AppLogger.Error("OvertimeService.SaveAsync", ex);
            return Result<OvertimeDto>.Failure("An error occurred while saving.");
        }
    }
}
```

Register in `ApplicationServiceExtensions.AddApplication()` as `AddScoped`.

### 5.5 API controller

Derive from `ApiControllerBase` and put a permission on **every** action:

```csharp
[Route("api/overtime")]
[SessionAuthorize]
public class OvertimeApiController : ApiControllerBase
{
    [HttpGet]
    [SessionAuthorize(Modules.Overtime, Actions.View)]
    public async Task<IActionResult> GetAll() { ... }

    [HttpPost("{id}/approve")]
    [SessionAuthorize(Modules.Overtime, Actions.Approve)]
    public async Task<IActionResult> Approve(int id)
    {
        var r = await _svc.ApproveAsync(id, CurrentUserId);   // never from the request body
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }
}
```

`CurrentUserId` throws if unauthenticated — that is intentional. Never accept the acting user
id as a parameter from the client.

### 5.6 Page + nav

- View: `Views/Admin/Overtime.cshtml`, script `wwwroot/js/pages/admin/overtime.js`.
- Action on `AdminController` with `[SessionAuthorize(Modules.Overtime, Actions.View)]`.
- Sidebar entry in `_Layout.cshtml`, wrapped in `@if (CanView(AppConstants.Modules.Overtime))`
  — see §7 for why not `hidden`.
- Add the module to the relevant group's `CanViewAny(...)` so the heading appears with it.

---

## 6. Coding conventions

| Rule | Notes |
|---|---|
| Return `Result`/`Result<T>` | No exceptions for expected failures. Catch at the service boundary, log, return a failure. |
| Inject `ICurrentUserContext` | Never a static, never a client-supplied user id. |
| `_uow.SaveChangesAsync()` in services | Repositories stage only. |
| Soft delete | Set `IsDeleted`; the `DbContext` also rewrites hard deletes. Add the query filter when mapping. |
| `PermissionKey.For()` | Never hand-format `"Module.Action"`. |
| `esc()` in page JS | Every database value interpolated into HTML. |
| Nullable enabled | Everywhere. Don't add `!` to silence a genuine warning. |

### File encoding — important on Windows

Source files are UTF-8 and contain em-dashes in comment banners (`// ── Section ──`) and emoji
in WinForms labels. **Do not bulk-edit them with PowerShell `Get-Content`/`Set-Content`:**
Windows PowerShell 5.1 decodes UTF-8-without-BOM as CP1252, silently corrupting those
characters, and the result still compiles. Use your editor, or explicit encodings:

```powershell
$s = [System.IO.File]::ReadAllText($p, [Text.Encoding]::UTF8)
[System.IO.File]::WriteAllText($p, $s, (New-Object System.Text.UTF8Encoding($false)))
```

The same caution applies to regex-editing `.gitignore` — `(?s)` makes `.` match newlines and
will happily eat the whole file.

---

## 7. Web UI notes

**Theme:** Adminty (`pcoded`) + Bootstrap 5 + jQuery. Assets live in `wwwroot/assets`
(outside `wwwroot`'s normal static-files root; `Program.cs` maps them via a
`PhysicalFileProvider` at `/assets`).

**Hiding nav items — use `@if`, not `hidden`.** The theme ships
`.pcoded .pcoded-navbar .pcoded-item>li{display:block}`, an author-level rule that beats the
browser's `[hidden]{display:none}`. An element with `hidden` stays visible. Render conditionally
instead, which also avoids emitting links to pages the user cannot open.

**Colours:** use the theme's gradient classes (`bg-c-blue`, `bg-c-green`, `bg-c-pink`,
`bg-c-yellow`, plus `bg-c-purple`/`bg-c-grey` added in `site.css`) rather than defining new
ones, so tiles track the template's palette.

**Escaping:** page scripts build rows by string concatenation. Wrap every database value in
`esc()`. `employees.js` is the reference; **`branches.js`, `designations.js`, `holidays.js`,
`leave.js`, `reports.js`, `shifts.js`, `users.js` and `roles.js` still need this applied.**

**CDN dependencies:** toastr, SweetAlert2 and Google Fonts load from public CDNs while
everything else is local. On an isolated network these degrade silently (`notifyConfirm` falls
back to `confirm()`). Vendoring them into `wwwroot/lib` is recommended for on-premise installs.

---

## 8. Build, clean, troubleshoot

```powershell
dotnet build AttendanceManagementSystem.slnx
dotnet build AttendanceManagementSystem.slnx -t:Rebuild     # surfaces all warnings
```

**Stop the running app before building** — a live `AttendanceSystem.exe` locks the output and
the build fails with `MSB3027`.

`dotnet clean` leaves NuGet assets and static-web-asset caches behind. For a true clean:

```powershell
Get-ChildItem -Recurse -Directory -Include bin,obj |
  Where-Object { $_.FullName -notmatch '\\\.vs\\' } | Remove-Item -Recurse -Force
dotnet restore AttendanceManagementSystem.slnx
dotnet build   AttendanceManagementSystem.slnx
```

### Current warning baseline: **0 warnings, 0 errors** on a full rebuild

The former ~102-warning baseline is gone. What cleared it:

| Was | Code | Resolution |
|---|---|---|
| 68 / 54 / 46 / 2 / 6 | CS8618, CS8600/8602/8604, CS0414 | all lived in the WinForms desktop project, which has been removed |
| 22 | CA1416 | OleDb entry points now check `OperatingSystem.IsWindows()` once and delegate to `[SupportedOSPlatform("windows")]` readers, so the constraint is declared rather than suppressed |
| 4 | NU1608 | AutoMapper removed — it was referenced by three projects and used by none |
| 2 | CS8321 | unused `NavSection` local function dropped from `_Layout.cshtml` |
| 3 | CS8619 | dashboard tile `Href` values given one consistent nullability |
| 1 | CS8602 | redundant `shift != null` removed in `AttendanceCalculator`; the enclosing pattern already proves it |

Keep it at zero: a new warning is easier to fix the day it appears than in a batch of a hundred.

### Common problems

| Symptom | Cause |
|---|---|
| Startup throws about `DefaultConnection` | user-secrets not set — §2 |
| `Migrate()` fails, "There is already an object named…" | pre-migrations database; baseline it |
| Login succeeds then bounces to login | running over HTTP; cookies are `Secure` |
| Menu item visible but page 403s | permission check on the endpoint but not on the nav entry |
| Icons render as empty boxes | feather webfont blocked — check it is same-origin |
| Toasts fall back to `alert()` | CDN unreachable |

---

## 9. Security checklist for changes

- [ ] Every new endpoint has `[SessionAuthorize(Module, Action)]` with the *right* action.
- [ ] Acting user comes from `CurrentUserId`, never the request.
- [ ] New permissions appended to `PermissionCatalogue` (not reordered) + migration.
- [ ] Any database value rendered in JS goes through `esc()`.
- [ ] No secrets in `appsettings.json`; no tokens or passwords in logs.
- [ ] State-changing MVC form posts carry `[ValidateAntiForgeryToken]`.
- [ ] New file-upload types are safe to serve (`.svg` can carry script — see
      `SettingsApiController.UploadLogo`, which currently allows it).

---

## 10. Known issues / backlog

1. **No tests.** See ARCHITECTURE §9 for the priority order.
2. **`esc()` sweep** incomplete — 8 page scripts still interpolate raw values.
3. **`.svg` logo upload** is a stored-XSS vector; drop it from the allow-list.
4. **Time handling** mixes local and UTC; unify on `DateTimeOffset`/`TimeProvider`.
5. **Session id not rotated** on sign-in; needs cookie auth for a full fix.
6. **CDN assets** should be vendored for on-premise deployments.
7. ~~**AutoMapper** unused — remove.~~ Done — removed from all three projects.
8. **`Infrastructure` → `Application`** reference inverts the intended dependency direction.
9. **Action buttons are not permission-gated** in views — users see Edit/Delete, then get a 403
   toast. Server-side is safe; this is polish.

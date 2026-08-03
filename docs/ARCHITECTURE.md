# Architecture

Attendance Management System — .NET 10, SQL Server, two front-ends over one application core.

---

## 1. Overview

The solution is a layered ("clean architecture"-style) monolith. Two hosts — an ASP.NET Core
MVC web application and a WinForms desktop client — sit on top of the same Application layer,
so business rules exist in exactly one place regardless of which UI invokes them.

```
┌─────────────────────────────┐   ┌─────────────────────────────┐
│   AttendanceSystem.Web      │   │  AttendanceManagementSystem │
│   ASP.NET Core MVC + API    │   │  WinForms desktop client    │
│   (Adminty/pcoded theme)    │   │                             │
└──────────────┬──────────────┘   └──────────────┬──────────────┘
               │            both depend on       │
               └───────────────┬─────────────────┘
                               ▼
                 ┌─────────────────────────────┐
                 │ AttendanceSystem.Application│  services, DTOs, use cases
                 │  IAuthService, IUserService │  service interfaces
                 └──────────────┬──────────────┘
                                ▼
                 ┌─────────────────────────────┐
                 │   AttendanceSystem.Domain   │  entities, enums,
                 │   IRepository, IUnitOfWork  │  repository contracts
                 └─────────────────────────────┘
                                ▲
                 ┌──────────────┴──────────────┐
                 │AttendanceSystem.Infrastructure│ EF Core, Dapper, SMTP,
                 │ repositories, DbContext, IO  │ biometric import, reports
                 └─────────────────────────────┘

                 ┌─────────────────────────────┐
                 │   AttendanceSystem.Common   │  referenced by all layers:
                 │ helpers, constants, Result, │  cross-cutting only
                 │ ICurrentUserContext         │
                 └─────────────────────────────┘
```

### Project responsibilities

| Project | Target | Contains |
|---|---|---|
| `AttendanceSystem.Domain` | `net10.0` | Entities, enums, `IRepository<T>`, `IUnitOfWork`, specific repository interfaces. No dependencies on other projects. |
| `AttendanceSystem.Application` | `net10.0` | Service implementations, DTOs, service interfaces. Depends on Domain + Common. |
| `AttendanceSystem.Infrastructure` | `net10.0` | `AttendanceDbContext`, EF repositories, `UnitOfWork`, Dapper context, `EmailService`, `ReportService`, `BiometricImportService`. |
| `AttendanceSystem.Common` | `net10.0` | `Result`/`Result<T>`, helpers, `AppConstants`, `ICurrentUserContext`, `PermissionKey`. |
| `AttendanceSystem.Web` | `net10.0` | MVC controllers, JSON API controllers, Razor views, static assets. |
| `AttendanceManagementSystem` | `net10.0-windows` | WinForms client, `DesktopSession`. |

**Known deviation:** `Infrastructure` references `Application` (to implement `IEmailService`,
`IReportService`, `IBiometricImportService`, whose interfaces live in Application). A stricter
arrangement would place those ports in Application and have Infrastructure depend only on
Domain + Common. It works as-is; it is noted so the inconsistency is not mistaken for intent.

---

## 2. Request flow

A typical web write request:

```
Browser
  │  POST /api/employees                     (jQuery, JSON)
  ▼
SessionAuthorizeAttribute                    ← authentication + permission gate
  │  resolves ICurrentUserContext from DI
  ▼
EmployeesApiController : ApiControllerBase
  │  CurrentUserId (throws if unauthenticated)
  ▼
IEmployeeService (Application)
  │  validation, business rules, returns Result<T>
  ▼
IUnitOfWork → IEmployeeRepository (Infrastructure)
  │
  ▼
AttendanceDbContext.SaveChangesAsync()
  │  ApplyAuditAndSoftDelete() stamps CreatedBy/ModifiedBy
  │  from ICurrentUserContext, converts Delete → soft delete
  ▼
SQL Server
```

### The `Result<T>` convention

Application services never throw for expected failures. They return
`Result.Success()` / `Result<T>.Success(data)` / `Result.Failure("message")`. Controllers map
this to HTTP:

```csharp
var r = await _users.GetAllAsync();
return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
```

Unexpected exceptions are caught at the service boundary, logged via `AppLogger`, and returned
as a failed `Result` so an internal error message never reaches the client verbatim.

---

## 3. Data access

### EF Core (primary)

`AttendanceDbContext` maps every entity in `OnModelCreating`. Two behaviours are enforced
centrally in `ApplyAuditAndSoftDelete()`, called from both `SaveChanges` overloads:

- **Audit stamping** — `CreatedAt`/`CreatedBy` on insert, `ModifiedAt`/`ModifiedBy` on update,
  taken from `ICurrentUserContext.UserId`. This is `null` when nobody is signed in (background
  work, seeding, design-time tooling) rather than defaulting to a real user id.
- **Soft delete** — `EntityState.Deleted` is rewritten to `Modified` with `IsDeleted = true`.
  Every entity that inherits `BaseEntity` has a matching global query filter
  (`e.HasQueryFilter(x => !x.IsDeleted)`), so deleted rows disappear from normal queries.

### Dapper (reporting only)

`ReportService` uses `DapperContext` and hand-written SQL. Reports are read-only, join across
several tables and benefit from projection straight into a DTO; EF is used for everything else.
Keep this boundary — Dapper here is a deliberate choice for read models, not a general escape
hatch.

### Repository + Unit of Work

`IRepository<T>` provides `GetByIdAsync`, `GetAllAsync`, `FindAsync`, `AddAsync`,
`UpdateAsync`, `DeleteAsync`, `ExistsAsync`, `CountAsync`. Entity-specific queries live on
interfaces like `IAttendanceRepository`. `IUnitOfWork` exposes every repository over a single
`DbContext` and owns the `SaveChangesAsync()` call, so one request commits atomically.

Repository methods that stage changes (`AddAsync`, `UpdateAsync`, `SavePermissionsAsync`) do
**not** save. The caller commits. This is what allows a service to make several changes in one
transaction.

---

## 4. Identity, sessions and authorization

### `ICurrentUserContext`

Defined in `AttendanceSystem.Common.Session`. Answers "who is this operation acting for?"

| Host | Implementation | Lifetime |
|---|---|---|
| Web | `HttpSessionUserContext` (reads `HttpContext.Session`) | **Scoped** — per request |
| Desktop | `DesktopUserContext` over `DesktopSession` | Singleton |

The desktop client is single-user per process, so process-wide state is correct there — and
`DesktopSession` therefore lives in the desktop executable, not in a shared project. **Never
put mutable current-user state in a shared library:** in the web host it would be shared by
every concurrent request and would misattribute audit data across users.

### Permission model

A permission is a `{Module}.{Action}` pair, e.g. `Employees.Delete`, `Leave.Approve`. Format
it only via `PermissionKey.For(module, action)`; comparisons are case-insensitive
(`PermissionKey.Comparer`).

```
Permission ──< RolePermission >── Role ──< User
```

`AttendanceDbContext.SeedPermissions` seeds the catalogue and default grants:

| Role | Grants |
|---|---|
| Administrator | all |
| HR Manager | all except Users, Roles, AuditLogs, and `Settings.Edit` |
| Employee | `Dashboard.View`, `Attendance.View/Create`, `Leave.View/Create`, `Holidays.View` |

Ids are assigned by position in `PermissionCatalogue`. **Entries may be appended but never
reordered or removed** — existing `RolePermission` rows point at those ids.

### Enforcement

On sign-in, `AuthService` returns the user's permission set; the host stores it
(`HttpSessionUserContext.SignIn` writes it into session). Per request:

- **Endpoints** — `[SessionAuthorize(Modules.X, Actions.Y)]`. The parameterless
  `[SessionAuthorize]` only proves *someone* is signed in; it is not an authorization decision.
- **Views** — `ViewContext.HasPermission(module, action)` hides controls the user cannot use.
  This is presentation only. The authoritative check is always the endpoint's.

`PermissionExtensions.HasPermission` **denies by default**. There is deliberately no
"administrator" role-name short-circuit: the Administrator role passes because it genuinely
holds every permission.

### Credentials

| Secret | Storage |
|---|---|
| Password | BCrypt, work factor 12 (`PasswordHelper`) |
| Remember-me token | 256-bit CSPRNG, stored **SHA-256 hashed**, 30-day expiry, rotated on every use |
| Password-reset token | same treatment, 24-hour expiry |

Raw tokens are returned to the client exactly once, in `AuthResultDto`, and never persisted.
SHA-256 rather than BCrypt is correct here precisely because the input is 256 bits of entropy —
there is nothing to brute-force, and BCrypt's work factor on every request would be a DoS
surface. Verification is constant-time (`TokenHelper.Verify`).

Tokens are revoked on logout, password change and password reset.

**Known limitation:** the session *identifier* is not rotated on sign-in. `Session.Clear()`
drops pre-authentication content, but ASP.NET Core's session middleware offers no supported way
to regenerate the id — a complete session-fixation defence requires cookie authentication
rather than raw session state.

---

## 5. Domain rules

### Attendance

Check-in (`AttendanceService.CheckInAsync`):

1. Reject if the employee already has a record for that date.
2. Resolve the shift from `EmployeeShift` where `EffectiveFrom <= date` and
   `EffectiveTo` is null or `>= date`; if several match, the latest `EffectiveFrom` wins.
3. Derive status, in this precedence order:

   ```
   Holiday      if the date is a holiday
   WeeklyOff    if the day is in Shift.WeeklyOffDays
   Late         if lateMinutes > 0
   Present      otherwise
   ```

   where `lateMinutes = (checkIn − shift.StartTime) − shift.GraceMinutes`, floored at 0.

Check-out sets `WorkingHours = checkOut − checkIn` (decimal hours) and flags
`IsEarlyLeave` when `checkOut < shift.EndTime`, recording the shortfall in minutes.

With no shift assigned, an employee is never marked late or early — status falls through to
`Present`.

**Today's roster.** `GetTodayAsync` returns a row for every active employee, not just those
with an `AttendanceLog`. Employees without a log get a derived row (`Id = 0`, so callers can
tell it is not persisted) whose status follows the same precedence as check-in:
approved leave → holiday → weekly off → `Absent`.

This matters because "absent" is not a stored state — it is the absence of a record. Returning
only logs meant the view could never show absentees, and disagreed with the dashboard's
`Absent` count (`total − present − onLeave`). Note also that the dashboard counts
`Present + Late` as present, which is why the UI offers a **Checked In** grouping rather than
only the raw statuses.

### Leave

`ApplyLeaveAsync` rejects `ToDate < FromDate`, computes
`TotalDays = (ToDate − FromDate) + 1` (**inclusive, calendar days — weekends and holidays are
not excluded**), and refuses when `usedDays + totalDays > LeaveType.TotalDays` for the year of
`FromDate`. Requests start `Pending` and move to `Approved`/`Rejected`, or `Cancelled`.

### Biometric import

`BiometricImportService` reads MS Access (`.mdb`/`.accdb` via OleDb), Excel (`.xlsx`/`.xls`)
and CSV/TXT. Punches are grouped by enroll id + date; **the first punch of the day becomes
check-in and the last becomes check-out.** Employees are matched on
`Employee.BiometricEnrollId`. A preview step returns parsed rows for correction before anything
is written.

> The OleDb path is Windows-only (`CA1416` warnings) while `Infrastructure` targets plain
> `net10.0`. Both hosts are Windows today, so it works; targeting `net10.0-windows` or guarding
> with `OperatingSystem.IsWindows()` would make it explicit.

---

## 6. Web front-end

Server-rendered Razor for page structure; data loaded client-side from the JSON API.

- **Theme** — Adminty ("pcoded") + Bootstrap 5 + jQuery. Assets in `wwwroot/assets`, served
  from a `PhysicalFileProvider` mapped to `/assets` (it sits outside `wwwroot`).
- **Layout** — `Views/Shared/_Layout.cshtml`. Sidebar items and groups are rendered inside
  `@if (CanView(...))` blocks. Do **not** use the `hidden` attribute for this: the theme's
  `.pcoded .pcoded-navbar .pcoded-item>li{display:block}` is an author-level rule and overrides
  the browser's `[hidden]{display:none}`, so hidden items stay visible.
- **Page scripts** — one file per screen under `wwwroot/js/pages/admin/`.
- **Shared script** — `wwwroot/js/site.js` provides `esc()` (HTML escaping), `notifySuccess`,
  `notifyError`, `notifyConfirm`.

### Escaping

Table renderers build markup by string concatenation. **Every value that came from the database
must pass through `esc()`.** An employee named `<img src=x onerror=…>` otherwise runs script in
every admin's browser — and names can arrive from a biometric device file, not just a vetted
form. `employees.js` is the reference implementation; several other page scripts still need
this treatment.

---

## 7. Configuration and deployment

No credentials in the repository. Committed `appsettings.json` files hold non-secret defaults
with blank placeholders; `AddInfrastructure` throws at startup with a descriptive message if
the connection string is empty.

| Host | Development | Deployment |
|---|---|---|
| Web | `dotnet user-secrets` | `ConnectionStrings__DefaultConnection`, `Smtp__*` |
| Desktop | `dotnet user-secrets` | `ATTENDANCE_`-prefixed environment variables |

### Migrations

Schema is managed by EF Core migrations in `AttendanceSystem.Infrastructure/Data/Migrations`.
Both hosts call `Database.Migrate()` at startup — **not** `EnsureCreated()`, which builds the
schema once and then silently ignores every later model change.

`AttendanceDbContextFactory` (an `IDesignTimeDbContextFactory`) lets `dotnet ef` construct the
context without starting a host, so authoring a migration needs no configured secrets.

Databases created before migrations existed have no `__EFMigrationsHistory` and must be
baselined — see `README.md`.

### Logging

Serilog to console and rolling daily files (`logs/attendance-web-.txt`, desktop
`Logs/attendance-.log`). Credentials and tokens must never be logged.

---

## 8. Cross-cutting conventions

| Concern | Convention |
|---|---|
| Service results | `Result` / `Result<T>`, never exceptions for expected failures |
| Permissions | `PermissionKey.For()`, `AppConstants.Modules` / `.Actions` |
| Current user | inject `ICurrentUserContext`; never a static |
| Deletes | soft delete via `BaseEntity.IsDeleted` + query filter |
| Saving | services call `_uow.SaveChangesAsync()`; repositories only stage |
| Time | `DateTime.Now` (local) throughout — see below |

**Known issue — time handling.** The codebase mixes `DateTime.Now` (local) and
`DateTime.UtcNow`; `AuditService` writes UTC while entity audit stamps use local. For a system
whose whole purpose is recording *when* people arrived, this should be unified on
`DateTimeOffset`/`TimeProvider` before any deployment that crosses a time zone or a DST
boundary.

---

## 9. Testing

There is currently **no test project**. The highest-value targets, in order:

1. `BiometricImportService` punch pairing — file parsing and first/last-punch logic.
2. `AttendanceService` late/early/status derivation, including the no-shift path.
3. `LeaveService` balance arithmetic.
4. `AuthService` token issue/verify/rotate/revoke.
5. `SessionAuthorizeAttribute` and `PermissionExtensions` — assert that a permission-less user
   is refused, so the deny-by-default behaviour cannot silently regress.

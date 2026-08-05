# Architecture

Attendance Management System — .NET 10, SQL Server, two front-ends over one application core.

Companion documents: [DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md) (how to work on it),
[DEVICE-INTEGRATION-DESIGN.md](DEVICE-INTEGRATION-DESIGN.md) (fingerprint hardware),
[USER-GUIDE.md](USER-GUIDE.md) (how to operate it).

---

## 1. Overview

A layered ("clean architecture"-style) monolith. Two hosts — an ASP.NET Core MVC web
application and a WinForms desktop client — sit on the same Application layer, so a business
rule exists in exactly one place regardless of which UI invokes it.

```
┌─────────────────────────────┐   ┌─────────────────────────────┐
│   AttendanceSystem.Web      │   │  AttendanceManagementSystem │
│   ASP.NET Core MVC + API    │   │  WinForms desktop client    │
│   (Adminty/pcoded theme)    │   │                             │
└──────────────┬──────────────┘   └──────────────┬──────────────┘
               │            both depend on       │
               └───────────────┬─────────────────┘
                               ▼
                 ┌──────────────────────────────┐
                 │ AttendanceSystem.Application │  services, DTOs, service
                 │  IAuthService, IUserService… │  interfaces, calculators
                 └──────────────┬───────────────┘
                                ▼
                 ┌──────────────────────────────┐
                 │   AttendanceSystem.Domain    │  entities, enums,
                 │   IRepository, IUnitOfWork   │  repository contracts
                 └──────────────────────────────┘
                                ▲
                 ┌──────────────┴───────────────┐
                 │AttendanceSystem.Infrastructure│ EF Core, Dapper, SMTP,
                 │ repositories, DbContext, IO   │ biometric import, devices
                 └──────────────────────────────┘

                 ┌──────────────────────────────┐
                 │   AttendanceSystem.Common    │  referenced by all layers:
                 │ helpers, constants, Result,  │  cross-cutting only
                 │ ICurrentUserContext          │
                 └──────────────────────────────┘
```

### Project responsibilities

| Project | Target | Contains |
|---|---|---|
| `AttendanceSystem.Domain` | `net10.0` | Entities, enums, `IRepository<T>`, `IUnitOfWork`. No dependency on other projects. |
| `AttendanceSystem.Application` | `net10.0` | Service implementations, DTOs, service interfaces, `AttendanceCalculator`. Depends on Domain + Common. |
| `AttendanceSystem.Infrastructure` | `net10.0` | `AttendanceDbContext`, EF repositories, `UnitOfWork`, Dapper context, `EmailService`, `ReportService`, `BiometricImportService`, `TcpFingerprintDeviceClient`. |
| `AttendanceSystem.Common` | `net10.0` | `Result`/`Result<T>`, helpers, `AppConstants`, `ICurrentUserContext`, `PermissionKey`. |
| `AttendanceSystem.Web` | `net10.0` | MVC controllers, JSON API controllers, Razor views, static assets. |
| `AttendanceManagementSystem` | `net10.0-windows` | WinForms client, `DesktopSession`. |

**Known deviation:** `Infrastructure` references `Application`, because it implements
`IEmailService`, `IReportService`, `IBiometricImportService` and `IFingerprintDeviceClient`,
whose interfaces live in Application. A stricter arrangement would put those ports in
Application and have Infrastructure depend only on Domain + Common. It works as-is; it is
recorded so the inconsistency is not mistaken for intent.

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
  ▼
AttendanceDbContext.SaveChangesAsync()
  │  ApplyAuditAndSoftDelete() stamps CreatedBy/ModifiedBy
  │  from ICurrentUserContext, converts Delete → soft delete
  ▼
SQL Server
```

### The `Result<T>` convention

Application services never throw for expected failures. They return `Result.Success()`,
`Result<T>.Success(data)` or `Result.Failure("message")`. Controllers map that to HTTP:

```csharp
var r = await _users.GetAllAsync();
return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
```

Unexpected exceptions are caught at the service boundary, logged via `AppLogger`, and returned
as a failed `Result`, so an internal error message never reaches the client verbatim.

### JSON shape

`Program.cs` sets `PropertyNamingPolicy = null` — **the API speaks PascalCase**, matching the
DTO property names. Page scripts rely on this (`e.EmployeeCode`, not `e.employeeCode`). Enums
serialise as strings via `JsonStringEnumConverter`. A `byte[]` (employee photo) serialises as
bare base64.

---

## 3. Data access

### EF Core (primary)

`AttendanceDbContext` maps every entity in `OnModelCreating`. Two behaviours are enforced
centrally in `ApplyAuditAndSoftDelete()`, called from both `SaveChanges` overloads:

- **Audit stamping** — `CreatedAt`/`CreatedBy` on insert, `ModifiedAt`/`ModifiedBy` on update,
  from `ICurrentUserContext.UserId`. Null when nobody is signed in (background work, seeding,
  design-time tooling) rather than defaulting to a real user id.
- **Soft delete** — `EntityState.Deleted` is rewritten to `Modified` with `IsDeleted = true`.
  Every `BaseEntity` has a matching global query filter (`e.HasQueryFilter(x => !x.IsDeleted)`),
  so deleted rows disappear from normal queries.

> **Query-filter warning.** `Device` has a query filter and is the *required* end of its
> relationships with `DevicePunch` and `DeviceSyncLog`. EF warns about this at startup. A
> soft-deleted device therefore filters its punches out of navigation loads. Do not treat the
> warning as noise if you add another required relationship to a filtered entity.

### Dapper (reporting only)

`ReportService` uses `DapperContext` and hand-written SQL. Reports are read-only, join across
several tables, and project straight into a DTO. Keep this boundary: Dapper here is a
deliberate choice for read models, not a general escape hatch from EF.

### Repository + Unit of Work

`IRepository<T>` provides `GetByIdAsync`, `GetAllAsync`, `FindAsync`, `AddAsync`,
`UpdateAsync`, `DeleteAsync`, `ExistsAsync`, `CountAsync`. Entity-specific queries live on
interfaces like `IAttendanceRepository`. `IUnitOfWork` exposes every repository over a single
`DbContext` and owns `SaveChangesAsync()`, so one request commits atomically.

Repository methods that stage changes do **not** save. The caller commits. That is what lets a
service make several changes in one transaction.

---

## 4. Identity, sessions and authorization

### `ICurrentUserContext`

Defined in `AttendanceSystem.Common.Session`. Answers "who is this operation acting for?"

| Host | Implementation | Lifetime |
|---|---|---|
| Web | `HttpSessionUserContext` (reads `HttpContext.Session`) | **Scoped** — per request |
| Desktop | `DesktopUserContext` over `DesktopSession` | Singleton |

The desktop client is single-user per process, so process-wide state is correct there — and
`DesktopSession` therefore lives in the desktop executable, not a shared project. **Never put
mutable current-user state in a shared library:** in the web host it would be shared across
concurrent requests and would misattribute audit data between users.

### Permission model

A permission is a `{Module}.{Action}` pair — `Employees.Delete`, `Leave.Approve`. Format it
only via `PermissionKey.For(module, action)`; comparisons are case-insensitive
(`PermissionKey.Comparer`).

```
Permission ──< RolePermission >── Role ──< User
```

Modules and actions are constants in `AppConstants`:

| | Values |
|---|---|
| **Modules** | Dashboard, Employees, Departments, Designations, Branches, Shifts, Attendance, Leave, Holidays, Reports, Users, Roles, Settings, Import, Devices, AuditLogs, Overtime |
| **Actions** | View, Create, Edit, Delete, Export, Approve, Sync |

`Sync` is separate from `Edit` deliberately: an operator can be allowed to pull attendance from
a device without being able to reconfigure the hardware.

`AttendanceDbContext.SeedPermissions` seeds the catalogue and the default grants:

| Role | Grants |
|---|---|
| Administrator | all |
| HR Manager | all except Users, Roles, AuditLogs, and `Settings.Edit` |
| Employee | `Dashboard.View`, `Attendance.View/Create`, `Leave.View/Create`, `Holidays.View` |

Ids are assigned by position in `PermissionCatalogue`. **Entries may be appended but never
reordered or removed** — existing `RolePermission` rows point at those ids.

### Enforcement

On sign-in `AuthService` returns the user's permission set, and the host stores it
(`HttpSessionUserContext.SignIn` writes it into session). Per request:

- **Endpoints** — `[SessionAuthorize(Modules.X, Actions.Y)]`. The parameterless
  `[SessionAuthorize]` only proves *someone* is signed in; an Employee-role session satisfies it
  just as well as an admin. It is not an authorization decision.
- **Views** — `ViewContext.HasPermission(module, action)` hides controls the user cannot use.
  Presentation only. The authoritative check is always the endpoint's.

`PermissionExtensions.HasPermission` **denies by default**. There is deliberately no
"administrator" role-name short-circuit: the Administrator role passes because it genuinely
holds every permission.

`SessionAuthorizeAttribute` answers differently by caller. An API request (path under `/api`,
`X-Requested-With: XMLHttpRequest`, or an `application/json` Accept header) gets 401/403 JSON;
a browser navigation gets a redirect to Login or AccessDenied. AccessDenied rather than the
dashboard, because a user lacking dashboard access would otherwise bounce between the two
forever.

### Credentials

| Secret | Storage |
|---|---|
| Password | BCrypt, work factor 12 (`PasswordHelper`) |
| Remember-me token | 256-bit CSPRNG, stored **SHA-256 hashed**, 30-day expiry, rotated on every use |
| Password-reset token | same treatment, 24-hour expiry |

Raw tokens are returned to the client exactly once, in `AuthResultDto`, and never persisted.
SHA-256 rather than BCrypt is correct here precisely because the input is 256 bits of entropy:
there is nothing to brute-force, and BCrypt's work factor on every request would be a DoS
surface. Verification is constant-time (`TokenHelper.Verify`). Tokens are revoked on logout,
password change and password reset.

Sessions idle out after `AppConstants.SessionTimeoutMinutes` (60). The session cookie is
`HttpOnly`, `SameSite=Lax` and **`SecurePolicy.Always`** — it authenticates every request, so it
must not travel over plain HTTP. That has a practical consequence in development: see
[DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md) §Common problems.

**Known limitation:** the session *identifier* is not rotated on sign-in. `Session.Clear()`
drops pre-authentication content, but ASP.NET Core's session middleware offers no supported way
to regenerate the id. A complete session-fixation defence requires cookie authentication rather
than raw session state.

---

## 5. Attendance rules

### One calculator

`AttendanceCalculator` (Application) is **the single place** attendance figures are derived
from a punch pair and a shift. Check-in, manual edit, the review screen and biometric import all
call it.

It is pure and static — no database, no clock — which is what makes the night-shift and
overtime cases testable in isolation. Those are the ones easy to get subtly wrong.

```csharp
AttendanceCalculator.Calculate(shift, date, checkIn, checkOut, isHoliday, isOnLeave)
    → Result { IsLate, LateMinutes, IsEarlyLeave, EarlyLeaveMinutes,
               GrossHours, WorkingHours, OvertimeMinutes, Status }
```

This consolidation was a fix, not a refactor: the three call sites previously had their own
arithmetic, which is how the system came to recompute working hours on edit but not lateness.

### The rules it applies

**Late** — `checkIn > shiftStart + GraceMinutes`. **Early leave** —
`checkOut < shiftEnd − GraceOutMinutes`. `GraceOutMinutes` is separate from `GraceMinutes`
because sites commonly tolerate a late arrival but not an early exit.

**Hours** — `GrossHours` is check-out minus check-in. `WorkingHours` is gross minus
`Shift.BreakMinutes`, floored at zero. The break deduction is why the two differ.

**Night shifts** — when a shift crosses midnight the expected end is the *next* calendar day.
Without that, every duration on a 22:00–06:00 shift goes negative. `CrossesMidnight` trusts
`Shift.IsNightShift` but also honours times that plainly cross over, so a shift saved before the
flag existed still calculates correctly. A negative span on such a shift is rolled forward a
day, which is what actually happened.

**Overtime** — only when `Shift.IsOtEnabled`, and by one of three routes:

| Case | Overtime is |
|---|---|
| Holiday or weekly off | *all* worked time |
| `OtCountsFromShiftEnd = true` | time past `shiftEnd + OtStartAfterMinutes` |
| `OtCountsFromShiftEnd = false` | time past `EffectiveStandardHours`, whenever worked |

The non-working-day case is deliberate. Measuring from the shift end would mean someone called
in for four hours on their Sunday off leaves long before the shift's nominal end and earns
nothing — the opposite of what a day off is worth. The shift's end time is meaningless on a day
the shift does not run.

`EffectiveStandardHours` is `Shift.StandardWorkingHours` when set, otherwise span minus break.

**Status precedence** — leave, holiday and weekly off describe the *day* and outrank how the
person performed on it:

```
OnLeave  →  Holiday  →  WeeklyOff  →  Late  →  Present
```

With no shift assigned, nobody is marked late or early; status falls through to `Present`.

**Late allowance.** `Shift.AllowedLateDaysPerMonth` flags days beyond the monthly tolerance.
It is **reporting only** — status, working hours and overtime of an over-allowance day are
untouched. Anything that changes what a person is paid should be a decision somebody makes, not
a side effect of a counter.

### Today's roster

`GetTodayAsync` returns a row for every active employee, not just those with an `AttendanceLog`.
Employees without a log get a derived row (`Id = 0`, so callers can tell it is not persisted)
following the same precedence: approved leave → holiday → weekly off → `Absent`.

This matters because **"absent" is not a stored state** — it is the absence of a record.
Returning only logs meant the view could never show absentees, and disagreed with the
dashboard's `Absent` count (`total − present − onLeave`). The dashboard counts `Present + Late`
as present, which is why the UI offers a **Checked In** grouping rather than only raw statuses.

### Leave

`ApplyLeaveAsync` rejects `ToDate < FromDate`, computes `TotalDays = (ToDate − FromDate) + 1`
(**inclusive, calendar days — weekends and holidays are not excluded**), and refuses when
`usedDays + totalDays > LeaveType.TotalDays` for the year of `FromDate`. Requests start
`Pending` and move to `Approved`/`Rejected`, or `Cancelled`.

---

## 6. Biometric import

`BiometricImportService` reads MS Access (`.mdb`/`.accdb` via OleDb), Excel (`.xlsx`/`.xls`)
and CSV/TXT. The file extension selects the reader; `.mdb` needs no separate entry point.

Punches map to employees on `Employee.BiometricEnrollId` — **an employee with that field unset
can never be matched**, which is the single most common cause of "the import did nothing". The
Employees grid flags it for that reason.

Three behaviours are deliberate and were each once wrong:

1. **Import runs the same `AttendanceCalculator` as everything else.** Imported rows previously
   arrived as "Present, not late, no overtime" with raw clock-difference hours, so every
   lateness and overtime figure was absent until somebody re-saved the row by hand.
2. **An existing day is refreshed, not skipped.** A day imported at 3pm has a check-in and no
   check-out; skipping on re-import meant the check-out never arrived. Rows a person corrected
   by hand are still left alone — the device must not overwrite a human decision.
3. **An early-morning punch is attributed to the night shift it belongs to.** A 22:00–06:30
   shift produces punches on two calendar dates; grouping naively by date turned one shift into
   two half-days, neither computing sensibly.

**The enrolment table is never a punch source.** There used to be a fallback that, finding no
punches, read the device's `Enroll` roster and fabricated one punch per user at `DateTime.Now` —
producing a full day's attendance for everyone, timestamped at the moment of import and marked
late by however long after shift start it ran. That is invented payroll data, which is worse
than importing nothing.

### Access table discovery

The `.mdb` reader discovers the punch table rather than assuming a schema, because vendors
differ. It looks for a table with both a recognised user-id column and a recognised timestamp
column, preferring known log-table names, and **preferring a table that actually has rows** —
these databases ship empty summary tables next to the real one.

Recognised timestamp columns include `CHECKTIME`, `PunchTime`, `RecordTime`, `AttTime` and
`KqDate` (Realand/EBKQ). Add to that list rather than special-casing a vendor.

OleDb is Windows-only. `ReadFromAccessAsync` and `ReadEnrollTableAsync` check
`OperatingSystem.IsWindows()` once and delegate to `[SupportedOSPlatform("windows")]` readers,
so the constraint is declared rather than suppressed. On a non-Windows host they throw
`PlatformNotSupportedException` pointing at CSV/Excel export.

---

## 7. Fingerprint devices

Implemented to **phase 1 only: a reachability probe.** `IFingerprintDeviceClient` exposes
`ProbeAsync`; `TcpFingerprintDeviceClient` implements it over TCP. Punch pulling is designed but
not built — see [DEVICE-INTEGRATION-DESIGN.md](DEVICE-INTEGRATION-DESIGN.md).

`Device` carries its own sync state (`LastPunchTimeSynced`, `ConsecutiveFailures`, `Status`)
rather than a separate table, because there is exactly one current position per device and it
makes "when did this last work?" answerable without a join. Historical detail belongs in
`DeviceSyncLog`.

No ZKTeco type, SDK handle or protocol constant may appear in a service, controller or view.
The moment one does, adding a device vendor stops being a contained change.

---

## 8. Web front-end

Server-rendered Razor for page structure; data loaded client-side from the JSON API.

- **Theme** — Adminty ("pcoded") + Bootstrap 5 + jQuery. The theme lives in **`assets/` at the
  content root, outside `wwwroot`**, and is served by a `PhysicalFileProvider` mapped to
  `/assets` in `Program.cs`. Application CSS/JS live in `wwwroot/` as normal.
- **Layout** — `Views/Shared/_Layout.cshtml`. Sidebar items are rendered inside
  `@if (CanView(...))` blocks. Do **not** use the `hidden` attribute for this: the theme's
  `.pcoded .pcoded-navbar .pcoded-item>li{display:block}` is an author-level rule that overrides
  the browser's `[hidden]{display:none}`, so hidden items stay visible.
- **Page scripts** — one file per screen under `wwwroot/js/pages/`.
- **Shared script** — `wwwroot/js/site.js` provides `esc()`, `notifySuccess`, `notifyError`,
  `notifyConfirm`, and the `amsPage` table/paging helper.

### Escaping

Table renderers build markup by string concatenation. **Every value that came from the database
must pass through `esc()`.** An employee named `<img src=x onerror=…>` otherwise runs script in
every admin's browser — and names can arrive from a biometric device file, not just a vetted
form. `employees.js` is the reference implementation; several page scripts still need this
treatment (see the backlog in [DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md)).

---

## 9. Configuration and deployment

No credentials in the repository. Committed `appsettings.json` files hold non-secret defaults
with blank placeholders; `AddInfrastructure` throws at startup with a descriptive message if the
connection string is empty.

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
baselined — see [DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md).

### Logging

Serilog to console and rolling daily files (`logs/attendance-web-.txt`, desktop
`Logs/attendance-.log`). Credentials and tokens must never be logged.

---

## 10. Cross-cutting conventions

| Concern | Convention |
|---|---|
| Service results | `Result` / `Result<T>`, never exceptions for expected failures |
| Permissions | `PermissionKey.For()`, `AppConstants.Modules` / `.Actions` |
| Current user | inject `ICurrentUserContext`; never a static |
| Deletes | soft delete via `BaseEntity.IsDeleted` + query filter |
| Saving | services call `_uow.SaveChangesAsync()`; repositories only stage |
| JSON | PascalCase; enums as strings |
| Attendance maths | `AttendanceCalculator` only — never inline arithmetic |
| Time | `DateTime.Now` (local) throughout — see below |

**Known issue — time handling.** The codebase mixes `DateTime.Now` (local) and
`DateTime.UtcNow`; `AuditService` writes UTC while entity audit stamps use local. For a system
whose whole purpose is recording *when* people arrived, this should be unified on
`DateTimeOffset`/`TimeProvider` before any deployment that crosses a time zone or a DST
boundary.

---

## 11. Testing

There is currently **no test project**. Highest-value targets, in order:

1. `AttendanceCalculator` — late/early/hours/overtime/status, especially night shifts and the
   holiday and weekly-off overtime branches. It is pure and static, so this is cheap and it
   underpins every other number in the system.
2. `BiometricImportService` — punch pairing, night-shift attribution, the refresh-not-skip rule,
   and Access table discovery.
3. `LeaveService` balance arithmetic.
4. `AuthService` token issue/verify/rotate/revoke.
5. `SessionAuthorizeAttribute` and `PermissionExtensions` — assert a permission-less user is
   refused, so deny-by-default cannot silently regress.

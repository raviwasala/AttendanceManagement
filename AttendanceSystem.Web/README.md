# AttendanceSystem.Web

The ASP.NET Core MVC web front-end. Server-rendered Razor for page structure, data loaded
client-side from a JSON API on the same host.

This file covers what is specific to *this project*. Everything cross-cutting lives one level up:

| For | See |
|---|---|
| Why the solution is shaped this way | [../docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md) |
| Setup, migrations, adding a feature | [../docs/DEVELOPER-GUIDE.md](../docs/DEVELOPER-GUIDE.md) |
| Deployment | [../SETUP_COMPLETE.md](../SETUP_COMPLETE.md) |
| Using the application | [../docs/USER-GUIDE.md](../docs/USER-GUIDE.md) |

---

## Running it

```powershell
dotnet run --launch-profile https   # https://localhost:7151
dotnet run --launch-profile http    # http://localhost:5086
```

**Use the `https` profile to sign in.** The session cookie is issued with
`CookieSecurePolicy.Always`, so over plain HTTP you appear to sign in and are returned straight
to the login page. The `http` profile is fine for hitting API endpoints or checking static
assets.

Requires a connection string in user-secrets — see the developer guide. The app fails fast at
startup with a clear message if it is missing.

---

## Layout

```
AttendanceSystem.Web/
├── Controllers/
│   ├── AuthController.cs          login, logout, profile, password reset
│   ├── AdminController.cs         renders the admin screens
│   ├── MeController.cs            employee self-service pages
│   └── Api/                       JSON API, one controller per module
├── Views/
│   ├── Admin/                     one view per admin screen
│   ├── Auth/  Me/  Home/
│   └── Shared/_Layout.cshtml      sidebar, theme wiring, permission gating
├── Filters/SessionAuthorizeAttribute.cs    authentication + permission gate
├── Session/HttpSessionUserContext.cs       ICurrentUserContext over HttpContext.Session
├── assets/                        purchased Adminty theme — OUTSIDE wwwroot
└── wwwroot/
    ├── js/site.js                 esc(), notify*, amsPage table helper
    ├── js/pages/admin/            one script per admin screen
    └── css/site.css
```

### `assets/` is not in `wwwroot`

The Adminty theme lives at the **content root**, and `Program.cs` maps it to `/assets` with a
`PhysicalFileProvider`. Reference it as `~/assets/...`.

> It is a third-party UI framework **kept whole**. Much of it is not referenced by any current
> view; that is expected. Do not prune it by reference-checking.

---

## Conventions specific to this project

### The API speaks PascalCase

`Program.cs` sets `PropertyNamingPolicy = null`, so JSON property names match the DTO property
names exactly. Page scripts read `e.EmployeeCode`, **not** `e.employeeCode`. Enums serialise as
strings; `byte[]` (employee photo) serialises as bare base64.

### Every endpoint carries a permission

```csharp
[Route("api/employees")]
[SessionAuthorize]                                   // signed in — NOT an authorization decision
public class EmployeesApiController : ApiControllerBase
{
    [HttpGet]
    [SessionAuthorize(Modules.Employees, Actions.View)]   // the actual check
    public async Task<IActionResult> GetAll() { … }
}
```

Class-level `[SessionAuthorize]` alone only proves *someone* is signed in — an Employee-role
session satisfies it as well as an administrator's. The per-action attribute is the real gate.

The filter answers differently by caller: API requests get 401/403 JSON, browser navigations get
a redirect to Login or AccessDenied.

### Escape everything from the database

Table renderers build markup by string concatenation, so every database value must pass through
`esc()` from `site.js`. An employee named `<img src=x onerror=…>` otherwise runs script in every
admin's browser — and names can arrive from a biometric device file, not just a vetted form.

`employees.js` is the reference implementation. Several page scripts still need this treatment.

### Sidebar visibility

Wrap nav entries in `@if (CanView(...))`. Do **not** use the `hidden` attribute: the theme's
`.pcoded .pcoded-navbar .pcoded-item>li{display:block}` is an author-level rule that beats the
browser's `[hidden]{display:none}`, so hidden items stay visible.

### Shared front-end helpers (`wwwroot/js/site.js`)

| Helper | Purpose |
|---|---|
| `esc(value)` | HTML-escape a database value before concatenating it into markup |
| `notifySuccess` / `notifyError` | Toasts |
| `notifyConfirm(opts, cb)` | Confirmation dialog, honours the Confirm-before-delete setting |
| `amsPage(sel, items, rowFn, opts)` | Renders a table with paging; pass `server: { total, page, pageSize, onPage }` for server-side paging |
| `amsInitSelects(scope)` | Applies Select2 to `<select>` elements in `scope`. Called automatically |

### Searchable dropdowns (`wwwroot/js/ams-select2.js`)

Every `<select>` becomes a type-to-filter dropdown automatically. **No page script was changed
to add this**, and none needs to know it exists.

Select2 **4.0.3** is Adminty's own component, vendored from the theme package into
`wwwroot/lib/select2/` — not loaded from a CDN, because this product is deployed on-premise and
`assets/css/style.css` already carries the theme's select2 overrides. Its stylesheet is linked
*before* `style.css` so those overrides win.

Select2 keeps the native `<select>` as the source of truth, so `.val()`, `.val(id)`,
`.html(options)` and `change` handlers behave exactly as before. Two details make that hold:

- **`dropdownParent` is set to the containing `.modal`.** Bootstrap keeps focus inside a dialog,
  so a dropdown appended to `<body>` renders but its search box cannot be typed into — it looks
  broken rather than erroring. Most of this app's dropdowns are in modals.
- **jQuery's `.val()` and `.html()` setters are wrapped** to fire `change.select2`. Those setters
  raise no event, so without this the widget shows a stale label after an edit loads a record.
  The `.select2` namespace updates the widget *without* re-entering the page's own change
  handlers.

Options: opt out per control with `data-no-search`; the search box is hidden below 8 options
(`minimumResultsForSearch`), and `data-search` forces it on. Multi-selects are left native.

---

## Screens

| Group | Screens |
|---|---|
| Dashboard | Index |
| People | Employees, Departments, Designations, Branches, Users, Roles |
| Attendance & Leave | Attendance, Attendance Review, Biometric Import, Devices, Leave, Holidays |
| Shifts | Shifts, Shift Roster |
| Overtime | Overtime Rules, Register, Approval, Summary |
| Reports & Admin | Reports, Audit Logs, Settings |
| Self-service | My Attendance, My Leave, My Profile |

Each admin screen is a Razor view under `Views/Admin/` plus one script under
`wwwroot/js/pages/admin/` with the same name.

---

## Third-party assets

Bootstrap 5, jQuery, jQuery Validation and the Adminty theme are vendored locally. **Toastr,
SweetAlert2 and Chart.js load from a CDN** — on a restricted or air-gapped network, toasts
degrade to browser `alert()` and charts do not render. Vendoring them is on the backlog.

---

## Warnings

This project and its dependencies build with **0 warnings**. Keep it that way — a new warning is
easier to fix the day it appears than in a batch of a hundred. (The WinForms desktop project in
the same solution carries a separate pre-existing baseline of ~88; that is expected.)

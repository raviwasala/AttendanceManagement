# Installation & Deployment

Getting the Attendance Management System onto a server and running it in production.

For a development machine see [docs/DEVELOPER-GUIDE.md](docs/DEVELOPER-GUIDE.md) §2 — that is a
different, simpler path. For day-to-day operation see [docs/USER-GUIDE.md](docs/USER-GUIDE.md).

> **Filename note:** this file was previously a build-status snapshot. The name is kept so
> existing links still resolve; `DEPLOYMENT.md` would describe it better if you want to rename it.

---

## 1. What you are deploying

| Component | Required | Notes |
|---|---|---|
| `AttendanceSystem.Web` | Yes | The web application everyone uses |
| SQL Server | Yes | 2014 or later |
| `AttendanceManagementSystem` (desktop) | Optional | WinForms client, same database |
| Microsoft Access Database Engine (64-bit) | Only for `.mdb` import | See §6 |
| SMTP server | Only for password reset | Without it, admins set passwords directly |

The web host must be **Windows** if you use `.mdb` biometric import — that path uses OleDb, which
is Windows-only. CSV and Excel import work anywhere.

If you plan to use fingerprint devices, the web host must be able to reach them on **TCP 4370**,
which in practice means on-premise on the same LAN. See
[docs/DEVICE-INTEGRATION-DESIGN.md](docs/DEVICE-INTEGRATION-DESIGN.md) §7.

---

## 2. Database

Create an empty database and a **least-privilege SQL login** — not `sa`. The application needs
DDL rights on first run, because it applies EF Core migrations at startup.

```sql
CREATE DATABASE AttendanceDB;
```

Leave `Encrypt` at its secure default rather than turning it off. Use
`TrustServerCertificate=True` only if the server presents a self-signed certificate and you
accept that.

The schema is created and upgraded automatically: both hosts call `Database.Migrate()` on
startup. There is no separate schema script to run.

> **Upgrading a database created before migrations existed** — it has no `__EFMigrationsHistory`
> table, so `Migrate()` tries to recreate existing tables and fails. Back it up, then baseline it
> as described in [README.md](README.md). `docs/legacy/AttendanceDB_Setup.sql` is the old
> full-schema script, kept for reference only — do not run it against a migrated database.

---

## 3. Configuration

**No credentials live in the repository.** The committed `appsettings.json` holds non-secret
defaults with blank placeholders, and the application **fails fast at startup with a descriptive
message** if the connection string is missing — it does not fall through to an opaque
`SqlException` on the first query.

Supply configuration by environment variable:

| Host | Variable |
|---|---|
| Web | `ConnectionStrings__DefaultConnection` |
| Web (email) | `Smtp__Host`, `Smtp__Username`, `Smtp__Password`, `Smtp__FromAddress` |
| Desktop | `ATTENDANCE_ConnectionStrings__DefaultConnection` |

Note the **double underscore** — that is how .NET maps a flat environment variable onto nested
configuration. The desktop client additionally uses the `ATTENDANCE_` prefix.

---

## 4. HTTPS is not optional

The session cookie authenticates every request and is issued with `CookieSecurePolicy.Always`.
**Over plain HTTP users appear to sign in and are immediately returned to the login page.**

Terminate TLS at the host or at a reverse proxy in front of it. If you use a proxy, forward the
scheme (`X-Forwarded-Proto`) so the application knows the original request was HTTPS.

Development ports, for reference: HTTPS `7151`, HTTP `5086`.

---

## 5. First run

1. Start the web application.
2. Watch the log for `Database migrated to the latest version.` — this confirms both the
   connection and the schema.
3. Browse to the HTTPS address.
4. Sign in as **`admin` / `Admin@123`**.
5. **Change that password immediately.**
6. Work through the setup order in
   [DEPARTMENT_SHIFT_MANAGEMENT.md](DEPARTMENT_SHIFT_MANAGEMENT.md) §6: branches → departments
   and designations → shifts → holidays → employees → shift roster.

The default administrator is seeded on an empty database. Leaving its password unchanged leaves
the entire system open.

---

## 6. Microsoft Access Database Engine

Needed only to import `.mdb`/`.accdb` biometric exports. Install the **64-bit** redistributable,
matching the process bitness of the web application.

Check whether it is already present:

```powershell
(New-Object System.Data.OleDb.OleDbEnumerator).GetElements() |
  Where-Object { $_.SOURCES_NAME -like 'Microsoft.ACE*' }
```

A row named `Microsoft.ACE.OLEDB.12.0` (or `.16.0`) means you are set.

> If an import reports *"No punches between … This file covers …"*, the engine is **not** the
> problem — the date range is. The engine message only appears when the file genuinely could not
> be opened. See [docs/USER-GUIDE.md](docs/USER-GUIDE.md) §6.

---

## 7. Logging

Serilog writes to the console and to rolling daily files:

| Host | Path |
|---|---|
| Web | `logs/attendance-web-.txt` |
| Desktop | `Logs/attendance-.log` |

The account running the application needs write access to that folder. Include these when
diagnosing anything; they carry the startup migration result, authorisation failures and import
outcomes.

Credentials and tokens are never logged, and must stay that way.

---

## 8. Backups

Back up the SQL Server database. Everything that matters lives there — attendance, employees,
audit logs, company settings, and employee photos (stored as bytes on the employee row, not as
files on disk).

There is no separate file store to back up. Uploaded company logos are the one exception; check
`wwwroot` if you have customised branding.

---

## 9. Post-deployment checklist

- [ ] Default `admin` password changed
- [ ] HTTPS working; signing in does not bounce back to the login page
- [ ] `Database migrated to the latest version.` seen in the log
- [ ] SQL login is least-privilege, not `sa`
- [ ] Connection string supplied by environment variable, not committed
- [ ] Log directory writable, and included in whatever collects logs
- [ ] Database backup scheduled and a restore tested
- [ ] Access Database Engine installed if `.mdb` import is used
- [ ] SMTP configured, or staff told that admins reset passwords directly
- [ ] Organisation structure set up in the order given in §5

---

## 10. Known limitations to plan around

These are documented in full in [docs/DEVELOPER-GUIDE.md](docs/DEVELOPER-GUIDE.md) §9. The ones
that affect a deployment decision:

- **Time is stored as local `DateTime` throughout.** Do not deploy across time zones, and expect
  ambiguity at daylight-saving transitions, until this is moved to `DateTimeOffset`.
- **Some front-end assets load from a CDN** (toastr, SweetAlert2, Chart.js). On an air-gapped or
  restricted network, notifications degrade to browser `alert()` and charts will not render.
  Vendor them locally if that matters.
- **Fingerprint devices cannot yet pull attendance.** Device records and reachability testing
  work; collection is still by file import.
- **Session ids are not rotated on sign-in**, pending a move to cookie authentication.

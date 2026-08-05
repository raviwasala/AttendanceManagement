# User Guide

Attendance Management System — for administrators, HR staff and employees.
For how it is built, see [ARCHITECTURE.md](ARCHITECTURE.md).

---

## 1. Signing in

Open the address your administrator gave you and enter your username and password.

The first administrator account is **`admin` / `Admin@123`**. **Change it immediately** —
Profile → Change Password.

**Use the HTTPS address.** The sign-in cookie is marked secure, so over plain `http://` you will
appear to sign in and be returned straight to the login page.

- **Remember me** keeps you signed in on that device for 30 days. Only use it on a device that is
  yours. Signing out, or changing your password, ends it immediately.
- Sessions expire after **60 minutes** of inactivity.
- After **5 failed attempts your account locks.** An administrator must unlock it — waiting will
  not clear it.
- **Forgot Password?** sends a reset link to your registered email, valid for **24 hours**, usable
  once. For privacy the page always says a link has been sent, whether or not the address is
  registered. It only works if SMTP is configured; otherwise ask an administrator to set a new
  password directly.

### What you can see

The menu shows only what your role allows, so your sidebar may be shorter than a colleague's. If
you open a page you do not have rights to you get an **Access Denied** screen — that is normal,
not a fault.

| Role | Typical access |
|---|---|
| Administrator | Everything, including users, roles and settings |
| HR Manager | Everything except users, roles, audit logs and changing settings |
| Employee | Dashboard, own attendance and leave, holidays |

---

## 2. Setting up a new site

Order matters — later screens depend on earlier ones.

1. **Settings** — company name, logo, work start/end times, weekend days.
2. **Branches** — physical locations.
3. **Departments** and **Designations**.
4. **Shifts** — see §3. This is the screen that most affects your numbers.
5. **Holidays** — for the current year.
6. **Employees** — see §4.
7. **Shift Roster** — assign employees to shifts.
8. **Users** and **Roles** — logins for whoever needs them.

---

## 3. Shifts

A shift decides who is late, how long they worked, and what counts as overtime. Get it right
before importing attendance.

| Field | What it does |
|---|---|
| **Start / End time** | The rostered day. End before start means an overnight shift. |
| **Night shift** | Tick for a shift crossing midnight (22:00–06:00). Without it, hours come out negative. |
| **Grace (in)** | Minutes after start before an arrival counts as late. |
| **Grace (out)** | Minutes before end that leaving still is not "early". Separate from grace in — most sites tolerate a late arrival but not an early exit. |
| **Break minutes** | Unpaid, deducted from worked time. This is why *gross* and *working* hours differ. |
| **Standard working hours** | Paid hours in a normal day; the overtime threshold. Left at 0, the system uses span minus break. |
| **Weekly off days** | Days this shift does not run. |
| **Allowed late days per month** | Tolerance before a day is flagged. **Reporting only** — it changes no hours and no pay. |
| **Working days per month** | Recorded for payroll's daily-rate divisor. Does not affect attendance. |

### Overtime settings

Overtime is only recorded when **OT enabled** is ticked. Then:

- **OT counts from shift end** — only time past the shift end plus the **OT starts after**
  threshold counts. Arriving early earns nothing. This is the usual choice.
- **Unticked** — anything beyond standard working hours counts, whenever it was worked.

**On a holiday or weekly off, all worked time is overtime**, regardless of the above. Someone
called in for four hours on their day off is credited four hours.

---

## 4. Employees

**Employees → Add Employee.**

- **Full Name** is the required one, and fills in automatically from *Name with Initials* +
  *Last Name*. Type in it directly to set it yourself; clear it to go back to automatic. Names
  imported from a device often are not "initials + surname", which is why editing wins.
- **Employee Code** is generated if left blank.
- **User ID** is your site's own identifier and need not be unique.
- **Photo** — Upload takes a JPG/PNG/WebP up to 5 MB and stores a square 400×400 version.
  Remove clears it back to the default silhouette.

> ### Biometric Enroll ID — the field that breaks imports
>
> This is the ID the person is enrolled under on the fingerprint device. **If it is blank, no
> biometric import can ever match that employee** and their attendance silently will not appear.
>
> The Employees list shows a yellow **not set** badge for exactly this reason. Chase those badges
> before running an import, not after.

---

## 5. Daily attendance

**Attendance** shows one row per active employee for the chosen date — not only those with a
record. Absent is not something stored; it is the *absence* of a record, so employees with no
punch appear with a derived status.

Statuses, in the order they take precedence:

| Status | Meaning |
|---|---|
| **On Leave** | Approved leave covers the date |
| **Holiday** | The date is a holiday |
| **Weekly Off** | The day is in the shift's weekly off days |
| **Late** | Arrived after start + grace |
| **Present** | Everything else |
| **Absent** | No record at all |

The dashboard counts **Present + Late** as present, which is why the filter offers **Checked In**
rather than only the raw statuses.

**Attendance Review** is where you correct records. Fix a time and every derived figure —
lateness, hours, overtime — is recalculated by the same rules used everywhere else. A record you
edit by hand is marked as manually corrected, and **a later import will not overwrite it**.

---

## 6. Biometric import

**Biometric Import** loads punches from a device export: CSV, Excel, or an MS Access
`.mdb`/`.accdb`.

1. Choose the file.
2. **Set the date range.** It defaults to the 1st of the current month to today.
3. **Preview & Edit Punches** — parses the file and shows what it found, without saving.
4. Correct or untick rows, then **Import Selected Punches**. Or **Import Directly** to skip the
   review.

### Reading the result

| Line | Meaning |
|---|---|
| Punches read | Rows found in the file within the date range |
| Days created | New attendance records |
| Days updated | Existing days refreshed — usually a check-out that had not happened yet |
| Unchanged | Same punches as last time |
| Left as manually corrected | Someone edited these by hand, so the device did not overwrite them |
| **Punches matching no employee** | **The Biometric Enroll ID is missing — see §4** |

### When it says there is nothing to import

> *"No punches between 2026-08-01 and 2026-08-05. This file covers 2018-02-04 to 2026-07-01 —
> widen the date range and try again."*

This is the most common outcome and it is not a fault. The default range starts on the 1st of the
current month; if your export ends earlier, everything falls outside it. Set the **From Date**
back and re-run.

If you are told to install the **Microsoft Access Database Engine**, that is different: it means
the `.mdb` genuinely could not be opened — a real missing-driver problem, not a date-range one.

The first punch of a day becomes check-in and the last becomes check-out. Early-morning punches
belonging to an overnight shift are attributed to the shift that started the previous evening,
rather than treated as a separate day.

---

## 7. Leave

Employees apply under **My Leave**; approvers use **Leave**.

- Days are counted **inclusive of both dates, as calendar days**. Weekends and holidays inside a
  range are *not* excluded — a Friday-to-Monday request is 4 days.
- An application is refused if it would exceed the leave type's yearly entitlement.
- Requests are Pending until Approved or Rejected, and can be Cancelled.

Approved leave changes that day's attendance status to **On Leave**.

---

## 8. Overtime

| Screen | Purpose |
|---|---|
| **Overtime Rules** | Rate multipliers and how overtime is paid |
| **Overtime Register** | Every overtime record, filterable |
| **Overtime Approval** | Approve or reject claims |
| **Overtime Summary** | Totals per employee or period, for payroll |

Overtime minutes are calculated automatically from attendance using the shift's settings (§3).
Approval is a separate, human decision — calculating overtime does not authorise paying it.

---

## 9. Devices

**Devices** records your fingerprint terminals: name, branch, IP address, port (4370 by default)
and comm key.

**Test Connection** checks the terminal answers and reports its clock. Clock drift is the most
common real cause of "the attendance is wrong" — if the device clock is minutes out, so are the
punches.

> **Devices cannot yet pull attendance automatically.** This release records devices and tests
> reachability only. Attendance still arrives through **Biometric Import** (§6). See
> [DEVICE-INTEGRATION-DESIGN.md](DEVICE-INTEGRATION-DESIGN.md) for what is planned.

---

## 10. Reports and audit

**Reports** covers daily attendance, monthly summaries, late arrivals, absentees, leave and the
employee list. Filter by date range and department, then export.

**Audit Logs** records who changed what and when — administrators only.

---

## 11. For employees

| Screen | Purpose |
|---|---|
| **My Attendance** | Your own records and monthly totals |
| **My Leave** | Apply for leave, see balances and request status |
| **My Profile** | Update your full name and email; change your password |

You cannot change your own role or permissions.

---

## 12. Troubleshooting

| Symptom | Cause and fix |
|---|---|
| Sign in succeeds, returns to login | Using `http://`. Use the HTTPS address. |
| Account locked | 5 failed attempts. An administrator unlocks it in Users. |
| Menu item missing | Your role lacks that permission. |
| Import found nothing | Date range misses the file's data. The message states the file's actual coverage — widen the From Date. |
| Employee missing from import | Biometric Enroll ID not set on their record (§4). |
| Hours look short | The shift's break minutes are deducted from worked time. |
| Night-shift hours negative or wrong | The shift is not ticked as a night shift. |
| Overtime always zero | OT not enabled on the shift, or nobody passed the OT threshold. |
| Someone marked late unfairly | Check the shift's grace-in minutes, and that they are on the right shift for that date. |
| Edited record reverted | It should not — manual corrections are protected from import. Report it. |
| Password reset email never arrives | SMTP is not configured. Ask an administrator to set the password directly. |
| Icons show as empty boxes | The icon font failed to load — a browser or network issue, not data loss. |

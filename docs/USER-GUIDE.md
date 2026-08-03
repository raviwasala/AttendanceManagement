# User Guide

Attendance Management System — for administrators, HR staff and employees.

---

## 1. Signing in

Open the address your administrator gave you (for example `https://attendance.yourcompany.com`)
and enter your username and password.

- **Remember me** keeps you signed in on that device for 30 days. Only use it on a device that
  is yours. Signing out, or changing your password, ends it immediately.
- After **5 failed attempts your account locks**. An administrator must unlock it — waiting will
  not clear it.
- **Forgot Password?** sends a reset link to your registered email. The link is valid for
  **24 hours** and works once. For privacy the page always says a link has been sent, whether or
  not the address is registered.

### What you can see

The menu shows only what your role allows, so your sidebar may be shorter than a colleague's.
If you open a page you do not have rights to, you get an **Access Denied** screen — that is
normal, not a fault. Ask your administrator if you need access.

Typical roles:

| Role | Typical access |
|---|---|
| Administrator | everything, including users, roles and settings |
| HR Manager | employees, attendance, leave, shifts, reports — not user or role administration |
| Employee | dashboard, own attendance, leave requests, holidays |

---

## 2. Dashboard

The landing page after sign-in.

- **Four tiles** — Total Employees, Present Today, Absent Today, On Leave Today.
- **Today's Attendance** — a doughnut showing present / absent / late / on-leave, with the
  attendance percentage in the centre.
- **Recent Attendance** — today's check-ins and check-outs. *View All* opens the full record.
- **Quick Links** — shortcuts to the main screens.

*Absent* is calculated as total active employees minus those present minus those on approved
leave — so it counts anyone with no record today, not only people explicitly marked absent.

---

## 3. Setting up (administrators)

Do this in order — later screens depend on earlier ones.

### 3.1 Company settings

**Settings** — company name, address, contact details, logo, standard working hours, weekend
days and the maximum late minutes tolerated.

### 3.2 Organisation structure

| Screen | Purpose |
|---|---|
| **Branches** | physical locations |
| **Departments** | e.g. Administration, IT, HR, Finance |
| **Designations** | job titles, e.g. Manager, Software Engineer |

Each screen works the same way: **Add** to create, the pencil to edit, the bin to delete.
Records are never truly erased — they are hidden and remain in history.

### 3.3 Shifts

**Shifts** defines working patterns:

| Field | Meaning |
|---|---|
| Name | e.g. "General Shift" |
| Start / End time | e.g. 09:00 – 18:00 |
| **Grace minutes** | lateness tolerated before someone is marked Late |
| Weekly off days | e.g. Saturday, Sunday |

With grace 15 and start 09:00, arriving at 09:14 is **Present**; 09:20 is **Late by 5 minutes**
— lateness is counted from the *end* of the grace period, not from the start time.

Assign shifts on the **Assignments** tab, with an effective-from date (and optional
effective-to). Change someone's shift by adding a new assignment with a later effective-from —
the most recent one applies. Do not edit history.

> An employee with **no shift assigned is never marked late or early** — every check-in records
> as Present. Assign shifts to everyone whose punctuality you want tracked.

### 3.4 Employees

**Employees → Add Employee.** Required: first name, last name, department, designation, branch,
joining date. Employee code is generated if you leave it blank.

**Biometric Enroll ID** is the number the fingerprint/face device knows the person by. It must
match the device exactly or their punches will not import.

Use the search box and the department/status filters to find people. The toggle button
activates or deactivates someone without deleting them — deactivate leavers so they drop out of
attendance counts while their history is kept.

### 3.5 Users and roles

**System Users** creates login accounts, optionally linked to an employee record.
**Role & Access Control** defines what each role may do, as a grid of modules against actions
(View, Create, Edit, Delete, Export, Approve).

Changes take effect the **next time the affected user signs in**, since permissions are read
into their session at sign-in.

> Give each person their own account. Shared logins make the audit trail worthless — every
> action is recorded against whoever was signed in.

---

## 4. Daily use

### 4.1 Attendance records

**Attendance Records → Today** lists **every active employee**, not only those who have
punched. Anyone without a record is shown greyed out with a derived status — Absent, On Leave,
Holiday or Weekly Off — and a **Check In** button to record them manually. This is why the
totals here agree with the dashboard.

The status filter includes **Checked In (Present + Late)**, which matches how the dashboard
counts "Present Today" — someone who arrived late is still present.

Clicking a dashboard tile opens this page already filtered to the people behind that number.

| Status | Meaning |
|---|---|
| Present | checked in within the grace period |
| Late | checked in after start time + grace |
| Absent | no record for a working day |
| On Leave | approved leave covers the date |
| Holiday | date is a configured holiday |
| Weekly Off | day is a weekly off day for the shift |

You can add or correct a record manually — useful for a forgotten punch or a device outage.
Manual records are flagged as such, and every edit is recorded against your account.

**Working hours** = check-out minus check-in. It does not deduct breaks. **Early leave** is
flagged when someone checks out before their shift ends.

### 4.2 Biometric import

**Biometric Import** brings punches in from the device.

1. Export from your device software as **CSV, Excel (.xlsx/.xls) or MS Access (.mdb/.accdb)**.
2. Choose the file and the date range.
3. **Preview** — check the parsed rows before anything is saved. You can correct them here.
4. **Import**.

For each person each day, the **earliest punch becomes check-in and the latest becomes
check-out**. Intermediate punches — lunch, etc. — are not stored as separate entries.

If rows are skipped, the usual cause is a **Biometric Enroll ID that does not match any
employee**. Fix it on the employee record and re-import.

> Import after the working day is complete. Importing mid-afternoon records that moment as
> check-out, and re-importing will report the day as already present.

### 4.3 Leave

**Leave Management** has two parts.

**Leave Types** — name, days allowed per year, whether paid. Seeded: Annual (14), Sick (10),
Casual (7), Unpaid (30).

**Requests** — apply on behalf of an employee, or approve/reject pending ones.

Applying needs employee, leave type, from/to dates and a reason. The system rejects the request
if the balance is insufficient and tells you how many days remain.

> **Leave days are counted as whole calendar days, inclusive of both ends.** Monday to Friday is
> 5 days; Friday to Monday is **4 days, including the weekend**. Weekends and public holidays
> are *not* deducted automatically. Bear this in mind when setting annual entitlements.

Balances are tracked per calendar year, based on the **from** date.

Approving or rejecting is recorded with your name and the time. Rejection takes a reason —
the employee sees it.

### 4.4 Holidays

**Holidays** — the company calendar. Each entry has a date, name, type (Public or Company) and
an optional "recurring" flag for fixed-date annual holidays.

Attendance on a holiday is recorded with status *Holiday*. Keep the calendar current before the
year starts, so reports classify days correctly from the outset.

---

## 5. Reports

**Reports** offers:

| Report | Shows |
|---|---|
| Daily Attendance | everyone's record for one date |
| Monthly Summary | per-employee totals for a month |
| Late Report | late arrivals over a date range |
| Absent Report | absences over a date range |
| Leave Report | leave taken over a date range |
| Employee List | current employee register |

Most can be filtered by department. Choose the report, set the date range and filter, then
**Generate**.

**Excel and PDF export are available in the desktop client.** The web Reports screen displays
results on screen.

---

## 6. Your profile

The menu under your name (top right):

- **My Profile** — update your full name and email. You cannot change your own role — that is
  deliberate.
- **Change Password** — needs your current password. Minimum 8 characters with at least one
  uppercase letter, one lowercase letter and one digit. Changing it signs out "remember me" on
  every device.
- **Logout**.

---

## 7. Desktop client

The Windows desktop application covers the same ground with two additions:

- **Excel and PDF export** from the Reports screen.
- Direct reading of biometric `.mdb` files on the local machine.

Sign-in and permissions work identically.

---

## 8. Troubleshooting

| Problem | What to do |
|---|---|
| "Invalid username or password" | Check caps lock. After 5 tries the account locks. |
| "Your account is locked" | Ask an administrator to unlock it. |
| "Your account is inactive" | An administrator deactivated it; ask them to reactivate. |
| Signed out immediately after signing in | You may be on `http://` instead of `https://`. Use the secure address. |
| Signed out after a while | Sessions expire after 60 minutes of inactivity. |
| Access Denied | Your role lacks that permission. Ask an administrator. |
| Menu shorter than a colleague's | Expected — the menu reflects your role. |
| Reset email never arrived | Check spam. If nothing, ask an administrator — email may not be configured. |
| Import skipped rows | Biometric Enroll IDs don't match employee records. |
| Someone shows Absent but was at work | No punch imported, or no shift assigned. Check both, then correct manually. |
| Nobody is ever marked Late | Shifts are probably not assigned. See §3.3. |
| Leave balance lower than expected | Weekends and holidays inside a leave range are counted. See §4.3. |

### For administrators

Every create, update, delete, sign-in and approval is written to the **audit log** with the
user and timestamp — useful when reconstructing what happened to a record.

**Immediately after installation:** sign in as `admin` / `Admin@123` and change that password.
If accounts were created in bulk they may share a default password — verify, and require
everyone to change theirs on first sign-in.

# Organisation & Shift Structure

How employees are organised, how shifts are defined, and how the two are joined by the roster.

This is the reference for the structural data every attendance figure depends on. For the
arithmetic those structures drive, see [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) §5. For
operating the screens, see [docs/USER-GUIDE.md](docs/USER-GUIDE.md).

---

## 1. The shape of it

```
Branch ─────┐
            │
Department ─┼──► Employee ──< EmployeeShift >── Shift
            │                 (dated assignment)
Designation ┘
```

- **Branch** — a physical location. Devices belong to a branch too.
- **Department** — an organisational unit.
- **Designation** — a job title.
- **Shift** — a rostered working pattern.
- **EmployeeShift** — *which* shift an employee works, *from when*.

An employee carries exactly one branch, one department and one designation at a time. Shifts are
different: they are dated, because people change shift and history must stay correct.

All of these inherit `BaseEntity`, so all are **soft-deleted** — a deleted department disappears
from lists but existing employees keep pointing at it, and historic records stay readable.

---

## 2. Branch, Department, Designation

Simple lookups: a name, a code, an active flag. They exist to filter and group — nearly every
report and list can be sliced by them. They do **not** scope permissions.

Set these up **before** creating employees: all three are required on an employee.

Deleting one is a soft delete. It stops appearing in dropdowns; employees already assigned are
not reassigned automatically and should be moved first.

---

## 3. Shift

The shift is the most consequential record in the system. Nothing about lateness, worked hours or
overtime can be computed without one, and a wrong shift produces wrong numbers *silently* — they
look like ordinary attendance.

### Fields

| Field | Type | Effect |
|---|---|---|
| `ShiftCode` | string | Short code for rosters and reports, e.g. `GEN`, `NGT` |
| `Name` | string | Display name |
| `StartTime` / `EndTime` | TimeSpan | The rostered day |
| `IsNightShift` | bool | The shift crosses midnight |
| `GraceMinutes` | int | Minutes after start before an arrival is late |
| `GraceOutMinutes` | int | Minutes before end that leaving is still not early |
| `BreakMinutes` | int | Unpaid, deducted from worked time |
| `StandardWorkingHours` | double | Paid hours per day; the overtime threshold. 0 = derive it |
| `IsOtEnabled` | bool | Whether overtime is recorded at all |
| `OtCountsFromShiftEnd` | bool | How overtime is measured — see §3.3 |
| `OtStartAfterMinutes` | int | Minutes past shift end before overtime accrues |
| `WeeklyOffDays` | string | Comma-separated day names, default `Saturday,Sunday` |
| `AllowedLateDaysPerMonth` | int | Monthly tolerance. **Reporting only.** 0 = no limit |
| `WorkingDaysPerMonth` | int | Payroll's daily-rate divisor. Does not affect attendance |

### 3.1 Two grace periods, not one

`GraceMinutes` and `GraceOutMinutes` are deliberately separate. Sites commonly tolerate a late
arrival but not an early exit, and a single symmetric grace cannot express that.

### 3.2 Night shifts

`IsNightShift` marks a shift running past midnight. It matters because the check-out lands on the
*following* calendar day: without it the expected end is before the start and every duration goes
negative.

Two derived properties handle the arithmetic:

- `SpanHours` — 22:00–06:00 is **8 hours, not −16**.
- `TimesCrossMidnight` — true when `EndTime <= StartTime`, used to warn on save when the flag and
  the times disagree.

The calculator trusts the flag but also honours times that plainly cross over, so a shift saved
before the flag existed still computes correctly.

### 3.3 Overtime measurement

Only when `IsOtEnabled`. Then one of three routes:

| Condition | Overtime is |
|---|---|
| The day is a holiday or a weekly off | **all** worked time |
| `OtCountsFromShiftEnd = true` | time past `EndTime + OtStartAfterMinutes` |
| `OtCountsFromShiftEnd = false` | time past `EffectiveStandardHours`, whenever worked |

The non-working-day rule overrides the others by design. Measuring from the shift end would mean
someone called in for four hours on their Sunday off leaves long before the shift's nominal end
and earns nothing — the opposite of what a day off is worth. **The shift's end time is
meaningless on a day the shift does not run.**

`EffectiveStandardHours` = `StandardWorkingHours` when set, otherwise `SpanHours − BreakMinutes`.
Leaving `StandardWorkingHours` at 0 is normal and safe.

### 3.4 Late allowance is reporting only

`AllowedLateDaysPerMonth` flags days beyond the monthly tolerance. It changes **nothing** about
status, working hours or overtime.

That restraint is deliberate: anything that changes what a person is paid should be a decision
somebody makes, not a side effect of a counter.

### 3.5 Weekly off days

Stored as a comma-separated string of `DayOfWeek` names (`"Saturday,Sunday"`). Matching is
case-insensitive and tolerates whitespace. A day listed here yields status **Weekly Off** and —
per §3.3 — makes all worked time overtime.

---

## 4. EmployeeShift — the dated assignment

```csharp
public class EmployeeShift : BaseEntity
{
    public int EmployeeId { get; set; }
    public int ShiftId { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }   // null = open-ended
}
```

Resolution for a given date: the assignment where `EffectiveFrom <= date` and `EffectiveTo` is
null or `>= date`. **If several match, the latest `EffectiveFrom` wins.**

That tie-break is what makes a shift change safe. Moving someone from day to night shift is a new
row with a new `EffectiveFrom`; last month's attendance still resolves against the shift they
actually worked, so historic lateness and overtime do not silently change.

> **Never edit a shift's times to reflect a change of pattern.** Editing the `Shift` row rewrites
> history: every past record recalculated against it produces different numbers. Create a new
> shift and a new `EmployeeShift` row instead.

### No shift assigned

An employee with no assignment covering a date is **never marked late or early**; status falls
through to Present. Hours are still computed from the punches, without a break deduction and
without overtime.

This is a legitimate state, not an error — but it is worth auditing, because it silently disables
every rule above.

---

## 5. Shift Roster

The **Shift Roster** screen manages `EmployeeShift` rows: assign a shift to employees from a
date, and close the previous assignment.

Typical sequence for a shift change:

1. Open Shift Roster and filter to the employees affected.
2. Assign the new shift with `EffectiveFrom` = the first day of the new pattern.
3. Close the previous assignment with `EffectiveTo` = the day before.

Do this **before** importing attendance for the new period, so imported punches are judged
against the right shift.

---

## 6. Setup order

Each step depends on those above it:

1. **Branches**
2. **Departments**, **Designations**
3. **Shifts**
4. **Holidays** — needed before attendance, since holiday status and holiday overtime depend on it
5. **Employees** — branch, department and designation are required
6. **Shift Roster** — assign shifts
7. Only then import or record attendance

Getting 3 and 4 wrong is the usual cause of numbers that look plausible but are not.

---

## 7. Checklist before trusting the numbers

- [ ] Every shift crossing midnight has **Night shift** ticked
- [ ] Break minutes reflect the actual unpaid break
- [ ] Weekly off days match reality for each shift
- [ ] Holidays are entered for the period being calculated
- [ ] Every active employee has an `EmployeeShift` covering the period
- [ ] Shift changes were made as new assignments, not by editing shift times
- [ ] `OtCountsFromShiftEnd` and `OtStartAfterMinutes` match how the site actually pays overtime
- [ ] Every employee has a **Biometric Enroll ID** if attendance arrives by import

# Fingerprint Device Integration — Design

ZKTeco TCP/IP device integration for the Attendance Management System.
Companion to [ARCHITECTURE.md](ARCHITECTURE.md).

## Status

**Partly built.** Phases 1–2 have landed; punch collection has not.

| Phase | Deliverable | State |
|---|---|---|
| 1 | Entities, migration, `Devices` CRUD + branch assignment, permissions | **Done** |
| 2 | `IFingerprintDeviceClient` + Test Connection + status | **Done** (probe only) |
| 3 | Mapping screen — pull users, auto-match | Not started |
| 4 | Manual sync — stage 1 + stage 2 + sync history | Not started |
| 5 | `BackgroundService` auto-sync + error log screen | Not started |
| 6 | `ReprocessAsync` | Not started |

What exists today: `Device`, `DevicePunch`, `DeviceSyncLog`, `DeviceUserMapping` entities and
the `AddFingerprintDevices` migration; `DeviceService` (CRUD, branch assignment,
`TestConnectionAsync`); `IFingerprintDeviceClient.ProbeAsync` implemented by
`TcpFingerprintDeviceClient`; the `Devices` module and its `Sync` action in `AppConstants`; the
Devices screen.

What does not exist: `IDeviceSyncService`, `IDeviceMappingService`, any reading of attendance
logs or enrolled users from a device, and the background scheduler. **`DevicePunch` is currently
an empty table** — the file importer is still the only route punches take into the system.

Until phase 4 lands, attendance from devices arrives via
**Biometric Import** ([ARCHITECTURE.md](ARCHITECTURE.md) §6).

---

## 1. The governing idea

The system already has a working punch-processing pipeline:
`BiometricImportService.ProcessPunchesAsync` takes `List<BiometricPunchDto>`, maps device enroll
ids to employees, groups by (employee, date), and writes `AttendanceLog` rows.

**The device module's responsibility ends at producing `BiometricPunchDto` records.**

```
File (.mdb/.xlsx/.csv) ─┐
                        ├─→ BiometricPunchDto[] ─→ [ existing pipeline ] ─→ AttendanceLog
ZKTeco device (TCP/IP) ─┘
```

Everything downstream — enroll-id mapping, punch pairing, night-shift attribution, late/early
derivation, the unique `(EmployeeId, AttendanceDate)` index — is reused unchanged. This is the
single most important decision in the design: it keeps the new module small, and it guarantees a
punch behaves identically whether it arrived by cable or by USB stick.

Design constraints, in priority order:

1. **Never lose a punch.** A device offline for three days must catch up.
2. **Never double-count a punch.** Re-running a sync must be a no-op.
3. **Device failure must not affect the web application.** A dead device is a row with a red dot.
4. **Simple enough to debug from the UI** at 2am when payroll is due.

---

## 2. Protocol and library — the decision that shapes everything

ZKTeco devices speak a proprietary protocol over TCP/UDP port 4370. Two routes:

| Option | Reality |
|---|---|
| **Official `zkemkeeper.dll`** (COM SDK) | Feature-complete, vendor-supported. But it is **32-bit COM**: requires `regsvr32` on the host, forces the process to **x86**, and is Windows-only. Hosting it inside ASP.NET Core means an x86 web app and native crashes taking down the site. |
| **Managed protocol client** (pure C#) | No registration, no bitness constraint, no COM. Less complete — but this module needs only *read attendance logs*, *read users*, *get device time*, *ping*. |

**Decision: a managed client, behind an interface.**

The feature list needs a small fraction of the SDK. Paying the full SDK's deployment pain for
features we do not use is a bad trade. More importantly the interface makes the choice
reversible — if a customer's device model needs the official SDK, implement the same interface
over `zkemkeeper` and register it for that device, with nothing else changing.

```csharp
// AttendanceSystem.Application/Interfaces/IFingerprintDeviceClient.cs  (as built)
public interface IFingerprintDeviceClient
{
    Task<Result<DeviceProbeResult>> ProbeAsync(DeviceConnection cx, CancellationToken ct = default);
}
```

Phase 4 adds `ReadAttendanceLogsAsync(cx, from, ct)` and `ReadUsersAsync(cx, ct)` to the same
interface.

> **No ZKTeco type, SDK handle or protocol constant may appear in a service, controller or
> view.** The moment one does, swapping or adding a device vendor stops being a contained change.

---

## 3. The two-stage pipeline

```
Device ──TCP:4370──► [Download] ──► DevicePunch (append-only)
                                          │
File import ──────────────────────────────┤
                                          ▼
                              [Map user → employee]
                                          ▼
                             [Group by employee + date]
                                          ▼
                                    AttendanceLog
```

**Stage 1 — Download.** Read punches, insert into `DevicePunch`, ignore rows that violate the
unique index. Nothing is interpreted. Idempotent by construction.

**Stage 2 — Process.** Take unprocessed punches, map to employees, hand to the existing
`ProcessPunchesAsync`, mark processed.

Splitting these is what makes the module operable:

- A bug in punch pairing is fixed by **replaying stage 2** — no device contact, no data loss.
- An employee mapped a week late gets their history by re-processing their punches.
- "The device says he clocked in at 08:55 but the system says absent" is answerable, because the
  raw record sits next to the derived one.

A single-stage design that writes `AttendanceLog` directly is simpler to build and materially
worse to run. **Do not collapse these.**

---

## 4. Incremental sync and duplicate prevention

Two mechanisms, deliberately overlapping.

### Watermark — "new logs only"

Each device stores `LastPunchTimeSynced`. A sync requests punches from
`LastPunchTimeSynced − overlap`, where **overlap defaults to 2 days**.

The overlap is not redundancy, it is correctness insurance:

- Device clocks drift, and a corrected clock can make "new" punches appear *before* the watermark.
- A punch written to the device during the previous download window can be missed.
- A device offline for a week is caught up automatically on next contact.

For a first-ever sync `LastPunchTimeSynced` is null and the initial window is configurable
(default 30 days), so a new device does not drag in years of history.

> ZKTeco devices expose a record index, and it is tempting to track "last index read". **Do not.**
> The index resets when device storage is cleared, which silently re-imports everything or skips
> everything. A timestamp watermark plus the unique index degrades gracefully; an index does not.

### Unique index — the actual guarantee

`(DeviceId, DeviceUserId, PunchTime)` unique. The watermark decides *what to ask for*; the index
decides *what gets stored*. Overlapping windows are therefore free.

Even if two syncs race, the database rejects the second insert. Catch the unique violation and
count it as a **skip, never an error**.

---

## 5. Services (phases 3–6)

```
IDeviceService            CRUD, branch assignment, test connection, status   ← built
IDeviceSyncService        download + process, one device or all              ← phase 4
IDeviceMappingService     device users ↔ employees                           ← phase 3
IFingerprintDeviceClient  protocol boundary (§2)  ── Infrastructure only     ← built (probe)
```

All return `Result`/`Result<T>` and take `ICurrentUserContext` for attribution, matching the
existing services.

```csharp
public interface IDeviceSyncService
{
    /// <summary>Download + process one device. Used by Sync Now and the scheduler.</summary>
    Task<Result<DeviceSyncResultDto>> SyncDeviceAsync(int deviceId, SyncTrigger trigger, CancellationToken ct = default);

    /// <summary>Every active auto-sync device. Failures are per-device and do not abort the run.</summary>
    Task<Result<IReadOnlyList<DeviceSyncResultDto>>> SyncAllAsync(CancellationToken ct = default);

    /// <summary>Stage 2 only — re-derive attendance from punches already downloaded.</summary>
    Task<Result<DeviceSyncResultDto>> ReprocessAsync(int deviceId, DateTime from, DateTime to, CancellationToken ct = default);
}
```

`ReprocessAsync` is the operational escape hatch. It costs almost nothing to add and is the
difference between a five-minute fix and a database restore.

---

## 6. Employee ID mapping

`Employee.BiometricEnrollId` (an `int?`) exists and is what the file importer matches on. It is
kept, but it is **not** sufficient for a multi-device deployment:

- The same person can hold different enroll ids on two devices.
- Two people at different branches can hold the *same* enroll id.

With a single global column the second case silently attributes one employee's attendance to
another — a payroll error that is very hard to spot.

**Design:** `DeviceUserMapping (DeviceId, DeviceUserId → EmployeeId)` becomes authoritative;
`BiometricEnrollId` becomes the auto-match hint. The mapping screen pulls users from the device,
auto-matches on the hint, and shows unmapped entries first for manual confirmation.

---

## 7. Automatic synchronisation (phase 5)

A `BackgroundService` in the web host. Points that are easy to get wrong:

- **Create a DI scope per cycle.** `BackgroundService` is a singleton; capturing a scoped
  `DbContext` gives one that lives for the life of the process and leaks memory.
- **Catch everything inside the loop.** An escaped exception ends the service silently and
  attendance stops being collected, with no visible symptom until payroll.
- **Sync devices sequentially**, with a per-device timeout. Ten devices hammering one SQL Server
  every 5 minutes is a self-inflicted load problem; the work is tiny and latency does not matter.
- **Guard against overlap.** If a cycle runs long the next must not start on the same device —
  `LastSyncStartedAt` with a staleness threshold is sufficient at this scale.

Interval and initial window belong in `CompanySettings` (5 minutes is a sensible default).

### Deployment note

This assumes the web host can reach the devices on port 4370 — true when the application runs
on-premise on the same LAN, the normal deployment for this product.

**If the application is ever hosted off-site this breaks**, and the fix is not to punch a hole in
the firewall: move the sync service into a small on-premise worker talking to the same database,
or have it push to an API. The service boundary above already allows this — it is why device
communication sits behind an interface and the scheduler owns no business logic.

---

## 8. Device status

Status is **derived from sync outcomes**, plus a lightweight poll for idle devices:

| Condition | Status |
|---|---|
| Last contact succeeded within 2 sync intervals | **Online** |
| Contact failed, `ConsecutiveFailures` < 3 | **Offline** |
| `ConsecutiveFailures` ≥ 3 | **Error** (with `LastError`) |
| Never contacted | **Unknown** |

Do not hold persistent connections. ZKTeco devices tolerate few concurrent sessions and hold
them poorly: connect, do the work, disconnect. "Online" means *reachable when last asked*, and
the UI should say when that was rather than implying live monitoring.

**Test Connection** is the same call invoked on demand, reporting device time alongside — clock
drift is the most common real cause of "the attendance is wrong" and should be visible where the
operator is already looking. Flag drift over ~2 minutes.

---

## 9. Permissions

The `Devices` module is in `AppConstants.Modules`, with `View`, `Create`, `Edit`, `Delete` and
the custom `Sync` action. `Sync` is separate from `Edit` so an operator can pull attendance
without being able to reconfigure hardware.

| Endpoint | Permission |
|---|---|
| `GET /api/devices` | `Devices.View` |
| `POST /api/devices` | `Devices.Create` |
| `PUT /api/devices/{id}` | `Devices.Edit` |
| `DELETE /api/devices/{id}` | `Devices.Delete` |
| `POST /api/devices/{id}/test` | `Devices.View` |
| `POST /api/devices/{id}/sync` | `Devices.Sync` |
| `GET /api/devices/{id}/sync-history` | `Devices.View` |
| `GET/POST /api/devices/{id}/mappings` | `Devices.Edit` |

> **Deployment warning, learned the hard way on this system.** Adding a module to
> `PermissionCatalogue` only affects *new* databases — the seed does not re-run on a database
> whose migration is already recorded. The `Import` module was added this way and the page 403'd
> for every user, including Administrator, because the permission row did not exist and therefore
> could not be granted.
>
> **Ship a data migration that inserts the permission rows and grants them**, and append to
> `PermissionCatalogue` without reordering existing entries — their ids are positional.

---

## 10. Screens

**Devices** (list) — name, branch, IP:port, status dot, last successful sync, unmapped count.
Actions: Test, Sync Now, Edit, Delete. *Built, minus Sync Now and the unmapped count.*

**Device detail** — three tabs, all phase 3+:
- *Settings* — name, IP, port, comm key, branch, active, auto-sync
- *Employee Mapping* — pull users, auto-match, confirm; unmapped shown first
- *Sync History* — `DeviceSyncLog` rows, failures highlighted, expandable error text

Both sit under the **Attendance & Leave** sidebar group, beside Biometric Import. The file
importer stays — it is the fallback when a device is unreachable, and it will share the same
punch store.

---

## 11. Failure modes this design handles

| Failure | Behaviour |
|---|---|
| Device offline for days | Watermark + overlap catches up on reconnect; no loss |
| Sync runs twice concurrently | Unique index rejects duplicates; counted as skips |
| Employee mapped after the fact | Punches retained unmapped; `ReprocessAsync` recovers history |
| Device clock drift | Surfaced by Test Connection; overlap window absorbs small drift |
| Pairing logic bug | Fix and replay stage 2 — raw punches are intact |
| Network drop mid-download | Transaction rolls back; watermark unchanged; next cycle retries |
| Device storage cleared | Timestamp watermark unaffected (an index-based one would not survive) |
| One device faulty | Per-device failure isolation; others continue |

Two it does **not** solve, stated plainly:

- **Employee not enrolled on the device.** No software fix; the unmapped count makes it visible.
- **Daylight-saving transitions.** The system stores local `DateTime` throughout
  ([ARCHITECTURE.md](ARCHITECTURE.md) §10). Punches during a DST shift can be ambiguous. Out of
  scope here, but an argument for moving to `DateTimeOffset`.

---

## 12. Out of scope for v1

Deliberately excluded to keep the module small: pushing employees/fingerprints *to* devices,
remote door control, live event streaming, real-time push (ADMS/WebSocket), remote firmware
management, and non-ZKTeco vendors.

Each is a genuine feature; none is needed to collect attendance reliably, and every one enlarges
the protocol surface that must be maintained. `IFingerprintDeviceClient` is where they would go.

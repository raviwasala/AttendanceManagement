/* ── Admin Attendance Management JavaScript ── */

let todayRows = [];

// ── tab switching ──────────────────────────────────────────────
function showTab(t) {
    document.getElementById('pane-today').style.display   = t === 'today'   ? '' : 'none';
    document.getElementById('pane-monthly').style.display = t === 'monthly' ? '' : 'none';
    document.getElementById('tab-today').classList.toggle('active',   t === 'today');
    document.getElementById('tab-monthly').classList.toggle('active', t === 'monthly');
}

// ── status badge helper ────────────────────────────────────────
function badge(s) {
    const map = {
        Present : 'success', Absent : 'danger',
        Late    : 'warning', OnLeave: 'info', Holiday: 'secondary',
        WeeklyOff: 'secondary', HalfDay: 'warning'
    };
    const cls = map[s] ?? 'light';
    return `<span class="badge bg-${cls} ${cls==='warning'?'text-dark':''}">${s}</span>`;
}

function fmtDT(v) { return v ? new Date(v).toLocaleTimeString([], {hour:'2-digit',minute:'2-digit'}) : '—'; }
function toLocal(dt) {
    if (!dt) return '';
    const d = new Date(dt);
    d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
    return d.toISOString().slice(0,16);
}

// ── TODAY ──────────────────────────────────────────────────────
async function loadToday() {
    const tbody = document.getElementById('todayBody');
    tbody.innerHTML = '<tr><td colspan="9" class="text-center py-3 text-muted">Loading…</td></tr>';
    try {
        const r = await fetch('/api/attendance/today');
        todayRows = r.ok ? await r.json() : [];
        renderToday(todayRows);
    } catch { tbody.innerHTML = '<tr><td colspan="9" class="text-danger text-center py-3">Failed to load.</td></tr>'; }
}

function renderToday(rows) {
    const tbody = document.getElementById('todayBody');
    if (!rows.length) {
        tbody.innerHTML = '<tr><td colspan="9" class="text-center py-3 text-muted">No records for today.</td></tr>';
        return;
    }
    tbody.innerHTML = rows.map(r => {
        // Id === 0 means the row was derived (no attendance record exists yet), so there is
        // nothing to check out, edit or delete — offer a check-in instead.
        const hasRecord = (r.Id ?? 0) > 0;
        const name = esc(r.EmployeeName ?? '');
        const actions = hasRecord
            ? `${!r.CheckOut ? `<button class="btn btn-xs btn-success btn-sm py-0 px-2 me-1" onclick="openCheckOut(${r.Id},'${(r.EmployeeName??'').replace(/'/g,"\\'")}')">Out</button>` : ''}
               <button class="btn btn-xs btn-outline-primary btn-sm py-0 px-2" onclick="openEdit(${r.Id},'${toLocal(r.CheckIn)}','${toLocal(r.CheckOut)}',${r.Status ?? 0},'${(r.Remarks??'').replace(/'/g,"\\'")}')">Edit</button>`
            : `<button class="btn btn-xs btn-outline-success btn-sm py-0 px-2" onclick="openCheckIn(${r.EmployeeId})">Check In</button>`;

        return `
        <tr${hasRecord ? '' : ' class="table-light text-muted"'}>
            <td>${esc(r.EmployeeCode) || '—'}</td>
            <td>${name || '—'}</td>
            <td>${esc(r.Department)   || '—'}</td>
            <td>${fmtDT(r.CheckIn)}</td>
            <td>${fmtDT(r.CheckOut)}</td>
            <td>${r.WorkingHours != null ? r.WorkingHours.toFixed(1) + ' h' : '—'}</td>
            <td>${badge(r.StatusDisplay ?? r.Status)}</td>
            <td>${r.IsLate ? `<span class="badge bg-warning text-dark">${r.LateMinutes} min</span>` : '—'}</td>
            <td>${actions}</td>
        </tr>`;
    }).join('');
}

function filterToday() {
    const q  = (document.getElementById('searchToday').value  ?? '').toLowerCase();
    const st = (document.getElementById('statusFilter').value ?? '').toLowerCase();

    // "checkedin" is a grouping, not a stored status — it matches anyone who actually
    // turned up, which is how the dashboard counts "Present Today".
    const matchesStatus = r => {
        if (!st) return true;
        const s = (r.StatusDisplay ?? r.Status ?? '').toString().toLowerCase();
        return st === 'checkedin' ? (s === 'present' || s === 'late') : s === st;
    };

    renderToday(todayRows.filter(r =>
        (!q  || (r.EmployeeName??'').toLowerCase().includes(q) ||
                 (r.EmployeeCode??'').toLowerCase().includes(q) ||
                 (r.Department??'').toLowerCase().includes(q)) &&
        matchesStatus(r)
    ));
}

// ── MONTHLY ───────────────────────────────────────────────────
async function loadMonthly() {
    const m = document.getElementById('mMonth').value;
    const y = document.getElementById('mYear').value;
    const tbody = document.getElementById('monthlyBody');
    tbody.innerHTML = '<tr><td colspan="11" class="text-center py-3 text-muted">Loading…</td></tr>';
    try {
        const r = await fetch(`/api/attendance/monthly?month=${m}&year=${y}`);
        const data = r.ok ? await r.json() : [];
        if (!data.length) {
            tbody.innerHTML = '<tr><td colspan="11" class="text-center py-3 text-muted">No data found.</td></tr>';
            return;
        }
        tbody.innerHTML = data.map(d => `
            <tr>
                <td>${d.EmployeeCode ?? '—'}</td>
                <td>${d.EmployeeName ?? '—'}</td>
                <td>${d.Department   ?? '—'}</td>
                <td>${d.TotalDays    ?? 0}</td>
                <td><span class="badge bg-success">${d.PresentDays ?? 0}</span></td>
                <td><span class="badge bg-danger">${d.AbsentDays  ?? 0}</span></td>
                <td><span class="badge bg-warning text-dark">${d.LateDays ?? 0}</span></td>
                <td><span class="badge bg-info">${d.LeaveDays ?? 0}</span></td>
                <td><span class="badge bg-secondary">${d.HolidayDays ?? 0}</span></td>
                <td>${d.TotalWorkingHours != null ? d.TotalWorkingHours.toFixed(1) + ' h' : '—'}</td>
                <td>
                    <div class="progress" style="min-width:60px;height:18px;">
                        <div class="progress-bar ${d.AttendancePercentage>=75?'bg-success':'bg-warning'}"
                             style="width:${d.AttendancePercentage??0}%">
                            ${(d.AttendancePercentage??0).toFixed(0)}%
                        </div>
                    </div>
                </td>
            </tr>`).join('');
    } catch { tbody.innerHTML = '<tr><td colspan="11" class="text-danger text-center py-3">Failed to load.</td></tr>'; }
}

// ── CHECK-IN ──────────────────────────────────────────────────
/**
 * Opens the manual check-in dialog.
 * @param {number} [preselectEmployeeId] pre-selects an employee — used by the "Check In"
 *        button on a derived (absent) row, so the user doesn't re-pick someone they just clicked.
 */
async function openCheckIn(preselectEmployeeId) {
    document.getElementById('ciError').textContent = '';
    document.getElementById('ciTime').value   = toLocal(new Date().toISOString());
    document.getElementById('ciRemarks').value = '';

    const sel = document.getElementById('ciEmployee');
    sel.innerHTML = '<option value="">Loading…</option>';
    try {
        const r = await fetch('/api/employees');
        const emps = r.ok ? await r.json() : [];
        sel.innerHTML = '<option value="">— Select Employee —</option>' +
            emps.map(e => `<option value="${e.Id}">${esc(e.EmployeeCode)} – ${esc(e.FullName)}</option>`).join('');
        if (preselectEmployeeId) sel.value = String(preselectEmployeeId);
    } catch { sel.innerHTML = '<option value="">Failed to load employees</option>'; }

    new bootstrap.Modal(document.getElementById('checkInModal')).show();
}

async function saveCheckIn() {
    const empId = parseInt(document.getElementById('ciEmployee').value);
    const time  = document.getElementById('ciTime').value;
    const err   = document.getElementById('ciError');
    err.textContent = '';
    if (!empId) { err.textContent = 'Please select an employee.'; return; }
    if (!time)  { err.textContent = 'Please enter check-in time.'; return; }

    const r = await fetch('/api/attendance/checkin', {
        method : 'POST',
        headers: { 'Content-Type': 'application/json' },
        body   : JSON.stringify({ EmployeeId: empId, CheckInTime: time,
                                  Remarks: document.getElementById('ciRemarks').value })
    });
    if (r.ok) {
        bootstrap.Modal.getInstance(document.getElementById('checkInModal')).hide();
        loadToday();
    } else {
        err.textContent = await r.text();
    }
}

// ── CHECK-OUT ─────────────────────────────────────────────────
function openCheckOut(logId, empName) {
    document.getElementById('coLogId').value   = logId;
    document.getElementById('coEmpName').textContent = empName;
    document.getElementById('coTime').value    = toLocal(new Date().toISOString());
    document.getElementById('coRemarks').value = '';
    document.getElementById('coError').textContent = '';
    new bootstrap.Modal(document.getElementById('checkOutModal')).show();
}

async function saveCheckOut() {
    const logId = parseInt(document.getElementById('coLogId').value);
    const time  = document.getElementById('coTime').value;
    const err   = document.getElementById('coError');
    err.textContent = '';
    if (!time) { err.textContent = 'Please enter check-out time.'; return; }

    const r = await fetch('/api/attendance/checkout', {
        method : 'POST',
        headers: { 'Content-Type': 'application/json' },
        body   : JSON.stringify({ AttendanceLogId: logId, CheckOutTime: time,
                                  Remarks: document.getElementById('coRemarks').value })
    });
    if (r.ok) {
        bootstrap.Modal.getInstance(document.getElementById('checkOutModal')).hide();
        loadToday();
    } else {
        err.textContent = await r.text();
    }
}

// ── EDIT ──────────────────────────────────────────────────────
function openEdit(id, checkIn, checkOut, status, remarks) {
    document.getElementById('editId').value       = id;
    document.getElementById('editCheckIn').value  = checkIn;
    document.getElementById('editCheckOut').value = checkOut;
    document.getElementById('editStatus').value   = status;
    document.getElementById('editRemarks').value  = remarks;
    document.getElementById('editError').textContent = '';
    new bootstrap.Modal(document.getElementById('editModal')).show();
}

async function saveEdit() {
    const id  = parseInt(document.getElementById('editId').value);
    const err = document.getElementById('editError');
    err.textContent = '';
    // No modifiedBy on the wire: the server attributes the change to the session user.
    const r = await fetch(`/api/attendance/${id}`, {
        method : 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body   : JSON.stringify({
            CheckIn  : document.getElementById('editCheckIn').value  || null,
            CheckOut : document.getElementById('editCheckOut').value || null,
            Status   : parseInt(document.getElementById('editStatus').value),
            Remarks  : document.getElementById('editRemarks').value
        })
    });
    if (r.ok) {
        bootstrap.Modal.getInstance(document.getElementById('editModal')).hide();
        loadToday();
    } else {
        err.textContent = await r.text();
    }
}

/*
 * Applies a ?status= value from the URL to the status dropdown.
 *
 * The dashboard tiles deep-link here (e.g. /Admin/Attendance?status=Absent) so the user
 * lands on the rows behind the number they clicked rather than an unfiltered list.
 * Unrecognised values are ignored, leaving the default "All Statuses".
 */
function applyStatusFromQuery() {
    const wanted = new URLSearchParams(window.location.search).get('status');
    if (!wanted) return;

    const select = document.getElementById('statusFilter');
    const match = Array.from(select.options)
        .find(o => o.value.toLowerCase() === wanted.toLowerCase());
    if (!match) return;

    select.value = match.value;
    filterToday();
}

// ── Init ──────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', async function () {
    // Await the load so the filter runs against populated rows, not an empty table.
    await loadToday();
    applyStatusFromQuery();
});

/* ── Admin Dashboard JavaScript ── */

let pieChart = null;

async function loadDashboard() {
    try {
        const res = await fetch('/api/attendance/dashboard');
        if (!res.ok) throw new Error('Failed to load dashboard data');
        const d = await res.json();

        // Stat cards — grouped digits so larger headcounts stay readable (1,240 not 1240)
        const num = v => (v ?? 0).toLocaleString();
        document.getElementById('statTotal').textContent   = num(d.TotalEmployees);
        document.getElementById('statPresent').textContent = num(d.PresentToday);
        document.getElementById('statAbsent').textContent  = num(d.AbsentToday);
        document.getElementById('statLeave').textContent   = num(d.OnLeaveToday);

        // Pie labels
        const pct = d.AttendancePercentage ?? 0;
        document.getElementById('pctLabel').textContent  = pct.toFixed(1) + '%';
        document.getElementById('lblPresent').textContent = d.PresentToday  ?? 0;
        document.getElementById('lblAbsent').textContent  = d.AbsentToday   ?? 0;
        document.getElementById('lblLate').textContent    = d.LateToday     ?? 0;
        document.getElementById('lblLeave').textContent   = d.OnLeaveToday  ?? 0;

        // Doughnut chart
        const ctx = document.getElementById('attendancePie').getContext('2d');
        if (pieChart) pieChart.destroy();
        pieChart = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: ['Present', 'Absent', 'Late', 'On Leave'],
                datasets: [{
                    data: [d.PresentToday, d.AbsentToday, d.LateToday, d.OnLeaveToday],
                    backgroundColor: ['#28a745','#dc3545','#ffc107','#17a2b8'],
                    borderWidth: 2
                }]
            },
            options: {
                cutout: '70%',
                plugins: { legend: { display: false } },
                animation: { duration: 600 }
            }
        });

        // Recent attendance table
        const tbody = document.getElementById('recentBody');
        const rows  = d.RecentAttendance ?? [];
        if (rows.length === 0) {
            tbody.innerHTML = '<tr><td colspan="5" class="text-center py-3 text-muted">No attendance records for today.</td></tr>';
            return;
        }
        tbody.innerHTML = rows.slice(0, 15).map(r => {
            const statusBadge = {
                'Present' : '<span class="badge bg-success">Present</span>',
                'Absent'  : '<span class="badge bg-danger">Absent</span>',
                'Late'    : '<span class="badge bg-warning text-dark">Late</span>',
                'OnLeave' : '<span class="badge bg-info">On Leave</span>',
                'Holiday' : '<span class="badge bg-secondary">Holiday</span>'
            }[r.Status] ?? `<span class="badge bg-light text-dark">${r.Status ?? ''}</span>`;

            const fmt = v => v ? new Date(v).toLocaleTimeString([], {hour:'2-digit',minute:'2-digit'}) : '—';
            const date = r.Date ? new Date(r.Date).toLocaleDateString() : '—';
            return `<tr>
                <td>${r.EmployeeName ?? '—'}</td>
                <td>${date}</td>
                <td>${fmt(r.CheckIn)}</td>
                <td>${fmt(r.CheckOut)}</td>
                <td>${statusBadge}</td>
            </tr>`;
        }).join('');
    } catch (e) {
        document.getElementById('recentBody').innerHTML =
            '<tr><td colspan="5" class="text-center text-danger py-3">Failed to load data.</td></tr>';
        console.error(e);
    }
}

/* ══════════════════════════════════════════════════════════════════════════
   Analytics panels
   Each loads independently and only if its container is on the page — the
   panels are permission-gated server-side, so a role without Leave access
   simply has no Leave panel, and nothing here should assume otherwise.
   ══════════════════════════════════════════════════════════════════════════ */

let trendChart = null, weekdayChart = null;

const fmtPct  = v => (v ?? 0).toFixed(1) + '%';
const fmtDate = v => v ? new Date(v).toLocaleDateString(undefined, { day: '2-digit', month: 'short' }) : '—';

/** Fetches JSON, returning null on any non-OK response so one failed panel can't break the rest. */
async function getJson(url) {
    try {
        const r = await fetch(url);
        return r.ok ? await r.json() : null;
    } catch { return null; }
}

// ── Attendance trend ──────────────────────────────────────────────────────
async function loadTrend(days) {
    const canvas = document.getElementById('trendChart');
    if (!canvas) return;

    document.getElementById('trendBtn7').classList.toggle('active', days === 7);
    document.getElementById('trendBtn30').classList.toggle('active', days === 30);

    const d = await getJson(`/api/analytics/attendance-trend?days=${days}`);
    if (!d) return;

    document.getElementById('trendAvg').textContent =
        `Average attendance ${fmtPct(d.AverageAttendancePercentage)}`;

    // Be explicit when there is barely any history — a two-point line looks like a
    // trend but isn't one.
    const warn = document.getElementById('trendWarning');
    if (d.DaysWithData < 5) {
        warn.textContent = `Only ${d.DaysWithData} day(s) in this range have attendance records, so this is not yet a meaningful trend.`;
        warn.classList.remove('d-none');
    } else {
        warn.classList.add('d-none');
    }

    const labels = d.Points.map(p => p.Label);
    if (trendChart) trendChart.destroy();
    trendChart = new Chart(canvas.getContext('2d'), {
        type: 'bar',
        data: {
            labels,
            datasets: [
                { label: 'Present',  data: d.Points.map(p => p.Present),  backgroundColor: '#0ac282' },
                { label: 'Late',     data: d.Points.map(p => p.Late),     backgroundColor: '#fe9365' },
                { label: 'Absent',   data: d.Points.map(p => p.Absent),   backgroundColor: '#fe5d70' },
                { label: 'On Leave', data: d.Points.map(p => p.OnLeave),  backgroundColor: '#2DCEE3' }
            ]
        },
        options: {
            responsive: true, maintainAspectRatio: false,
            scales: {
                x: { stacked: true, grid: { display: false } },
                y: { stacked: true, beginAtZero: true, ticks: { precision: 0 } }
            },
            plugins: {
                legend: { position: 'bottom' },
                tooltip: {
                    callbacks: {
                        afterTitle: items => {
                            const p = d.Points[items[0].dataIndex];
                            const bits = [`Attendance ${fmtPct(p.AttendancePercentage)}`];
                            if (p.IsHoliday) bits.push('Holiday');
                            else if (p.NonWorking > 0) bits.push(`${p.NonWorking} on weekly off`);
                            return bits.join(' · ');
                        }
                    }
                }
            }
        }
    });
}

// ── Punctuality ───────────────────────────────────────────────────────────
async function loadPunctuality() {
    if (!document.getElementById('weekdayChart')) return;

    const d = await getJson('/api/analytics/punctuality');
    if (!d) return;

    document.getElementById('punLatePct').textContent = fmtPct(d.LatePercentage);
    document.getElementById('punAvgMins').textContent = (d.AverageLateMinutes ?? 0) + ' min';
    document.getElementById('punEarly').textContent   = d.TotalEarlyLeave ?? 0;

    const body = document.getElementById('topLateBody');
    body.innerHTML = d.TopLate.length
        ? d.TopLate.map(e => `
            <tr>
                <td class="ps-3">${esc(e.EmployeeName)}<br><small class="text-muted">${esc(e.EmployeeCode)}</small></td>
                <td class="text-muted">${esc(e.Department) || '—'}</td>
                <td class="text-end"><span class="badge bg-warning text-dark">${e.LateCount}</span></td>
                <td class="text-end">${e.AverageLateMinutes}</td>
                <td class="text-end pe-3">${e.TotalLateMinutes}</td>
            </tr>`).join('')
        : '<tr><td colspan="5" class="text-center py-3 text-muted">No late arrivals in this period.</td></tr>';

    if (weekdayChart) weekdayChart.destroy();
    weekdayChart = new Chart(document.getElementById('weekdayChart').getContext('2d'), {
        type: 'bar',
        data: {
            labels: d.ByWeekday.map(w => w.Day.slice(0, 3)),
            datasets: [{
                label: 'Late %',
                data: d.ByWeekday.map(w => w.LatePercentage),
                backgroundColor: '#fe9365'
            }]
        },
        options: {
            responsive: true, maintainAspectRatio: false,
            scales: { y: { beginAtZero: true, max: 100, ticks: { callback: v => v + '%' } },
                      x: { grid: { display: false } } },
            plugins: {
                legend: { display: false },
                tooltip: {
                    callbacks: {
                        label: i => {
                            const w = d.ByWeekday[i.dataIndex];
                            return `${w.LateCount} of ${w.CheckIns} check-ins late (${fmtPct(w.LatePercentage)})`;
                        }
                    }
                }
            }
        }
    });
}

// ── Leave overview ────────────────────────────────────────────────────────
async function loadLeaveOverview() {
    const pendingBody = document.getElementById('pendingLeaveBody');
    if (!pendingBody) return;

    const d = await getJson('/api/analytics/leave-overview');
    if (!d) return;

    pendingBody.innerHTML = d.PendingRequests.length
        ? d.PendingRequests.map(r => `
            <tr>
                <td class="ps-3">${esc(r.EmployeeName)}</td>
                <td>${esc(r.LeaveTypeName)}</td>
                <td class="text-muted small">${fmtDate(r.FromDate)} – ${fmtDate(r.ToDate)}</td>
                <td class="text-end pe-3">${r.TotalDays}</td>
            </tr>`).join('')
        : '<tr><td colspan="4" class="text-center py-3 text-muted">Nothing awaiting approval.</td></tr>';

    const util = document.getElementById('leaveUtilBody');
    util.innerHTML = d.Utilisation.length
        ? d.Utilisation.map(u => `
            <div class="mb-3">
                <div class="d-flex justify-content-between small mb-1">
                    <span class="fw-medium">${esc(u.LeaveType)}</span>
                    <span class="text-muted">${u.DaysTaken} / ${u.TotalEntitlement} days</span>
                </div>
                <div class="progress" style="height:6px;">
                    <div class="progress-bar bg-primary" role="progressbar"
                         style="width:${Math.min(100, u.UtilisationPercentage)}%"
                         aria-valuenow="${u.UtilisationPercentage}" aria-valuemin="0" aria-valuemax="100"></div>
                </div>
            </div>`).join('') +
            `<div class="small text-muted mt-3">
                ${d.PendingCount} pending${d.OldestPendingDays > 0 ? ` · oldest waiting ${d.OldestPendingDays} day(s)` : ''}
                · ${d.OnLeaveToday} on leave today
             </div>`
        : '<div class="text-muted small">No active leave types.</div>';
}

// ── Operations health ─────────────────────────────────────────────────────
async function loadOperations() {
    const body = document.getElementById('opsBody');
    if (!body) return;

    const d = await getJson('/api/analytics/operations');
    if (!d) return;

    /* Zero is the good outcome, so a count of 0 is muted and anything above it is
       highlighted — the panel should be visually quiet when nothing needs doing. */
    const card = (count, title, detail, names) => `
        <div class="col-md-4">
            <div class="border rounded p-3 h-100 ${count > 0 ? 'border-warning' : ''}">
                <div class="d-flex justify-content-between align-items-start">
                    <div class="fw-medium">${title}</div>
                    <span class="badge ${count > 0 ? 'bg-warning text-dark' : 'bg-light text-muted'}">${count}</span>
                </div>
                <div class="text-muted small mt-1">${detail}</div>
                ${names.length ? `<div class="small mt-2">${names.slice(0, 5).map(esc).join('<br>')}${names.length > 5 ? `<br><span class="text-muted">+${names.length - 5} more</span>` : ''}</div>` : ''}
            </div>
        </div>`;

    body.innerHTML = `
        <div class="row g-3">
            ${card(d.MissingBiometricId, 'No biometric ID',
                   'Device punches can never match these employees.',
                   d.MissingBiometricEmployees.map(e => e.EmployeeName))}
            ${card(d.WithoutShift, 'No shift assigned',
                   'Never flagged late or early, so punctuality data is meaningless for them.',
                   d.WithoutShiftEmployees.map(e => e.EmployeeName))}
            ${card(d.MissingCheckOut, 'Missing check-out',
                   'Checked in on a past day but never out — working hours cannot be calculated.',
                   d.MissingCheckOutRecords.map(e => `${e.EmployeeName} (${e.Detail})`))}
        </div>
        <div class="small text-muted mt-3">
            Last 30 days: ${d.ManualRecords} manual, ${d.DeviceRecords} from device (${fmtPct(d.ManualPercentage)} manual)${
            d.LastDeviceRecordAt ? ` · last device record ${new Date(d.LastDeviceRecordAt).toLocaleString()}` : ' · no device records yet'}
        </div>`;
}

/* Nothing is fetched here any more. Every loader below hits an endpoint gated on a module
   permission, so firing them all on load meant a plain employee's dashboard issued five
   requests that each came back 403. The widget list says what this user may see; the
   loaders now run from that answer instead of ahead of it. */
document.addEventListener('DOMContentLoaded', function () {
    loadWidgetPrefs();
});

/* ── Widget visibility ────────────────────────────────────────────────────────
   Applied on load and after saving. Hiding is a preference, not a permission:
   the server has already dropped any widget this user cannot load, and each
   widget's data endpoint enforces its own permission regardless. */
var dashWidgets = [];

// Initialised from the DOMContentLoaded handler above — a second call here would fetch the
// widget list twice and run every loader twice with it.

function loadWidgetPrefs() {
    $.getJSON('/api/dashboard-widgets', function (list) {
        dashWidgets = list || [];
        applyWidgetVisibility();
    });
}

/* Deny by default, matching the server.

   This used to iterate the returned widgets and toggle each one — but the server *filters
   out* widgets the user has no permission for rather than returning them as hidden. A block
   the user was not allowed to see therefore appeared in neither the show nor the hide pass,
   and simply stayed rendered. Every block is now hidden unless the server named it. */
function applyWidgetVisibility() {
    var allowed = {};
    dashWidgets.forEach(function (w) { allowed[w.Key] = !!w.IsVisible; });

    $('[data-widget]').each(function () {
        var key = $(this).attr('data-widget');
        $(this).toggle(Object.prototype.hasOwnProperty.call(allowed, key) && allowed[key]);
    });

    // Each loader runs only when a block that displays its data survived. The endpoints
    // enforce this too — skipping the call just avoids a pointless 403 and an empty card.
    if (allowed.stats || allowed.todayattendance) loadDashboard();
    if (allowed.trend) loadTrend(7);
    if (allowed.punctuality || allowed.toplate) loadPunctuality();
    if (allowed.pendingleave || allowed.leaveutil) loadLeaveOverview();
    if (allowed.datahealth) loadOperations();
}

function openCustomise() {
    $('#cuList').html('<div class="text-muted small">Loading…</div>');
    new bootstrap.Modal('#customiseModal').show();

    $.getJSON('/api/dashboard-widgets', function (list) {
        dashWidgets = list || [];
        if (!dashWidgets.length) {
            $('#cuList').html('<div class="text-muted small">'
                + 'There are no dashboard widgets available to your role.</div>');
            return;
        }
        $('#cuList').html(dashWidgets.map(function (w) {
            return '<div class="form-check mb-2">'
                 + '<input class="form-check-input cu-chk" type="checkbox" id="cu-' + esc(w.Key) + '"'
                 + ' value="' + esc(w.Key) + '"' + (w.IsVisible ? ' checked' : '') + '>'
                 + '<label class="form-check-label" for="cu-' + esc(w.Key) + '">'
                 + '<strong>' + esc(w.Title) + '</strong>'
                 + '<div class="text-muted small">' + esc(w.Description) + '</div>'
                 + '</label></div>';
        }).join(''));
    }).fail(function (xhr) {
        $('#cuList').html('<div class="alert alert-danger py-2 mb-0 small">'
            + esc(xhr.responseText || 'Could not load the widget list.') + '</div>');
    });
}

function selectedWidgetKeys() {
    return $('.cu-chk:checked').map(function () { return $(this).val(); }).get();
}

function saveCustomise() {
    $.ajax({
        url: '/api/dashboard-widgets', type: 'POST', contentType: 'application/json',
        data: JSON.stringify({ VisibleKeys: selectedWidgetKeys() }),
        success: function () {
            bootstrap.Modal.getInstance('#customiseModal').hide();
            notifySuccess('Dashboard updated.');
            loadWidgetPrefs();
        },
        error: function (xhr) { notifyError(xhr.responseText || 'Could not save.'); }
    });
}

function saveAsDefault() {
    notifyConfirm({
        title: 'Save as company default',
        text: 'New users will start with this selection. People who have already customised '
            + 'their own dashboard keep their choices.',
        confirmText: 'Save default', icon: 'question'
    }, function () {
        $.ajax({
            url: '/api/dashboard-widgets/default', type: 'POST', contentType: 'application/json',
            data: JSON.stringify({ VisibleKeys: selectedWidgetKeys() }),
            success: function () { notifySuccess('Company default saved.'); },
            error: function (xhr) { notifyError(xhr.responseText || 'Could not save the default.'); }
        });
    });
}

function resetCustomise() {
    notifyConfirm({
        title: 'Reset dashboard',
        text: 'Your choices are removed and you follow the company default again.',
        confirmText: 'Reset', icon: 'warning'
    }, function () {
        $.post('/api/dashboard-widgets/reset')
            .done(function () {
                bootstrap.Modal.getInstance('#customiseModal').hide();
                notifySuccess('Dashboard reset to the default.');
                loadWidgetPrefs();
            })
            .fail(function (xhr) { notifyError(xhr.responseText || 'Could not reset.'); });
    });
}

/* ── Custom tiles ─────────────────────────────────────────────────────────────
   Composed from a fixed metric catalogue, never a query. The server re-checks
   the metric's permission on every read, so a tile stops producing a number the
   moment its owner loses access rather than leaving a stale figure on screen. */
var tileMetrics = [];

$(function () {
    loadCustomTiles();

    $.getJSON('/api/departments', function (d) {
        (d || []).filter(function (x) { return x.IsActive; }).forEach(function (x) {
            $('#tlDept').append('<option value="' + esc(x.Id) + '">' + esc(x.Name) + '</option>');
        });
    });
    $.getJSON('/api/branches', function (d) {
        (d || []).filter(function (x) { return x.IsActive; }).forEach(function (x) {
            $('#tlBranch').append('<option value="' + esc(x.Id) + '">' + esc(x.Name) + '</option>');
        });
    });
});

function loadCustomTiles() {
    $.getJSON('/api/dashboard-widgets/tiles', function (tiles) {
        if (!tiles || !tiles.length) { $('#customTiles').empty(); return; }

        $('#customTiles').html(tiles.map(function (t) {
            var value = t.Value == null ? '—' : t.Value + (t.Suffix || '');

            // The tile drills through to the screen that explains the number, carrying its
            // own scope and period. A figure you cannot open is one nobody can act on.
            var inner =
                  '<div class="card stat-card ' + esc(t.Colour) + ' h-100">'
                + '<div class="card-body stat-card-body">'
                + '<div class="stat-card-text">'
                + '<p class="stat-card-label mb-0">' + esc(t.Title) + '</p>'
                + '<h3 class="stat-card-value" style="font-size:1.35rem;">' + esc(value) + '</h3>'
                + '<div style="font-size:.68rem;opacity:.85;">' + esc(t.ScopeDisplay) + '</div>'
                + '</div></div></div>';

            return '<div class="col-xl-3 col-md-6">'
                 + '<div class="position-relative h-100">'
                 + (t.Url
                     ? '<a href="' + esc(t.Url) + '" class="stat-card-link d-block h-100"'
                       + ' title="Open ' + esc(t.Title) + '">' + inner + '</a>'
                     : inner)
                 // Remove sits on the tile rather than in a separate manage screen: it is the
                 // only other action a tile has. Outside the anchor, so removing one does not
                 // also navigate.
                 + '<button class="btn btn-sm position-absolute top-0 end-0 m-1 p-0 px-1 text-white"'
                 + ' style="background:rgba(0,0,0,.15);z-index:2;" title="Remove tile"'
                 + ' onclick="deleteTile(' + t.Id + ')"><i class="fa fa-times"></i></button>'
                 + '</div></div>';
        }).join(''));
    });
}

function openTileBuilder() {
    $('#tlId').val(0);
    $('#tlTitle').val('');
    $('#tlDept').val(''); $('#tlBranch').val(''); $('#tlPeriod').val('today');
    $('#tlColour').val('bg-c-blue');

    $.getJSON('/api/dashboard-widgets/metrics', function (list) {
        tileMetrics = list || [];
        if (!tileMetrics.length) {
            notifyError('There are no metrics available to your role.');
            return;
        }
        $('#tlMetric').html(tileMetrics.map(function (m) {
            return '<option value="' + esc(m.Key) + '">' + esc(m.Title) + '</option>';
        }).join(''));
        onMetricChange();
        new bootstrap.Modal('#tileModal').show();
    });
}

function currentMetric() {
    var key = $('#tlMetric').val();
    return tileMetrics.filter(function (m) { return m.Key === key; })[0];
}

function onMetricChange() {
    var m = currentMetric();
    if (!m) return;

    $('#tlMetricHint').text(m.Description);

    // A headcount is a state now, not something with a period. Showing the picker
    // would offer a choice that changes nothing.
    $('#tlPeriodRow').toggle(!!m.SupportsPeriod);

    suggestTitle();
    $('#tlDept, #tlBranch, #tlPeriod').off('change.tl').on('change.tl', suggestTitle);
}

/* Fills the label from the choices, until the user types their own. */
function suggestTitle() {
    var $title = $('#tlTitle');
    if ($title.data('touched')) return;

    var m = currentMetric();
    if (!m) return;

    var parts = [];
    var dept = $('#tlDept option:selected').text();
    var branch = $('#tlBranch option:selected').text();
    if ($('#tlDept').val()) parts.push(dept);
    if ($('#tlBranch').val()) parts.push(branch);

    var label = m.Title;
    if (parts.length) label = parts.join(' ') + ' — ' + m.Title.toLowerCase();
    $title.val(label.substring(0, 60));
}

$(document).on('input', '#tlTitle', function () { $(this).data('touched', true); });

function saveTile() {
    var title = $('#tlTitle').val().trim();
    if (!title) { notifyError('Give the tile a label.'); return; }

    $.ajax({
        url: '/api/dashboard-widgets/tiles', type: 'POST', contentType: 'application/json',
        data: JSON.stringify({
            Id: parseInt($('#tlId').val()) || 0,
            Title: title,
            MetricKey: $('#tlMetric').val(),
            DepartmentId: $('#tlDept').val() ? parseInt($('#tlDept').val()) : null,
            BranchId: $('#tlBranch').val() ? parseInt($('#tlBranch').val()) : null,
            Period: $('#tlPeriod').val(),
            Colour: $('#tlColour').val()
        }),
        success: function () {
            bootstrap.Modal.getInstance('#tileModal').hide();
            $('#tlTitle').data('touched', false);
            notifySuccess('Tile added.');
            loadCustomTiles();
        },
        error: function (xhr) { notifyError(xhr.responseText || 'Could not add that tile.'); }
    });
}

function deleteTile(id) {
    notifyConfirm({
        title: 'Remove tile', text: 'This removes the tile from your dashboard only.',
        confirmText: 'Remove', icon: 'warning'
    }, function () {
        $.ajax({
            url: '/api/dashboard-widgets/tiles/' + id, type: 'DELETE',
            success: function () { notifySuccess('Tile removed.'); loadCustomTiles(); },
            error: function (xhr) { notifyError(xhr.responseText || 'Could not remove that tile.'); }
        });
    });
}

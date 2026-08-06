/* ── Admin Attendance Review ── */

var review = null;

function iso(d) { return d.toISOString().split('T')[0]; }

/** 95 -> "1h 35m"; overtime is easier to sanity-check in hours than in raw minutes. */
function fmtMins(m) {
    if (!m) return '—';
    var h = Math.floor(m / 60), r = m % 60;
    return h ? h + 'h ' + (r ? r + 'm' : '') : r + 'm';
}

/* Arriving from a dashboard tile: the tile carries its own range, department and
   row filter, and the screen has to honour them or the drill-down lands on a
   different figure than the one that was clicked. */
function applyQueryFilters() {
    var q = new URLSearchParams(window.location.search);
    var from = q.get('from'), to = q.get('to');
    var applied = false;

    if (from && to) { $('#arFrom').val(from); $('#arTo').val(to); applied = true; }
    if (q.get('filter')) { $('#arFilter').val(q.get('filter')); applied = true; }

    return { applied: applied, departmentId: q.get('departmentId') };
}

$(function () {
    preset('today');

    // Read before the dropdowns load, applied after — the department cannot be
    // selected until its options exist.
    var incoming = applyQueryFilters();

    // Both lookups must finish before the first load: loadReview reads the department
    // filter, and firing it when only the employee list had arrived would query with
    // an empty department and show a wider figure than the tile that was clicked.
    $.when(
        $.getJSON('/api/departments', function (d) {
            (d || []).filter(function (x) { return x.IsActive; }).forEach(function (x) {
                $('#arDept').append('<option value="' + esc(x.Id) + '">' + esc(x.Name) + '</option>');
            });
            if (incoming.departmentId) $('#arDept').val(incoming.departmentId);
        }),
        $.getJSON('/api/employees', function (d) {
            (d || []).forEach(function (e) {
                $('#arEmployee').append('<option value="' + esc(e.Id) + '">'
                    + esc(e.FullName) + ' (' + esc(e.EmployeeCode) + ')</option>');
            });
        })
    ).always(function () { loadReview(); });

    // Filtering is a server round trip now, so it reloads rather than re-rendering.
    $('#arFilter').on('change', function () { loadReview(1); });

    // Picking one person is usually followed by wanting a longer window, so widen it once.
    $('#arEmployee').on('change', function () {
        if ($(this).val() && $('#arFrom').val() === $('#arTo').val()) preset('thismonth');
        loadReview();
    });

    $('#arDept').on('change', loadReview);
});

function preset(which) {
    var now = new Date(), from, to;
    switch (which) {
        case 'yesterday':
            from = new Date(now); from.setDate(now.getDate() - 1); to = new Date(from); break;
        case 'thisweek':
            from = new Date(now); from.setDate(now.getDate() - ((now.getDay() + 6) % 7)); to = new Date(now); break;
        case 'thismonth':
            from = new Date(now.getFullYear(), now.getMonth(), 1);
            to = new Date(now.getFullYear(), now.getMonth() + 1, 0); break;
        case 'lastmonth':
            from = new Date(now.getFullYear(), now.getMonth() - 1, 1);
            to = new Date(now.getFullYear(), now.getMonth(), 0); break;
        default:
            from = new Date(now); to = new Date(now);
    }
    $('#arFrom').val(iso(from));
    $('#arTo').val(iso(to));
    if (review !== null) loadReview();
}

var arPage = 1;

function loadReview(page) {
    // Called from the pager with a page number, and from the filters with nothing — a filter
    // change goes back to page 1.
    arPage = amsPageNo(page);

    var from = $('#arFrom').val(), to = $('#arTo').val();
    if (!from || !to) { notifyError('Choose both a start and an end date.'); return; }
    if (to < from) { notifyError('End date must be on or after the start date.'); return; }

    var emp = $('#arEmployee').val(), dept = $('#arDept').val();

    $('#arBody').html('<tr><td colspan="11" class="text-center py-4 text-muted">Loading…</td></tr>');

    var rowFilter = $('#arFilter').val() || '';

    var url = '/api/attendance-review?from=' + from + '&to=' + to
            + '&page=' + arPage + '&pageSize=' + (amsPageSize() || 25)
            + (emp ? '&employeeId=' + emp : '')
            + (dept ? '&departmentId=' + dept : '')
            + (rowFilter ? '&rowFilter=' + encodeURIComponent(rowFilter) : '');

    $.getJSON(url, function (d) { review = d; renderSummary(); renderRows(); })
     .fail(function (xhr) {
         $('#arBody').html('<tr><td colspan="11" class="text-danger text-center py-3">' +
             esc(xhr.responseText || 'Failed to load.') + '</td></tr>');
     });
}

function renderSummary() {
    $('#arRangeLabel').text(review.RangeDisplay);

    var active = review.RowFilter || '';

    /*
     * A tile with a `filter` is a button: clicking it narrows the table to those rows, and
     * clicking the active one again clears the filter. Tiles with no filter (totals like
     * "Hours") stay inert rather than pretending to be clickable.
     */
    var tile = function (label, value, colour, filter) {
        var clickable = !!filter;
        var isOn = clickable && filter === active;

        return '<div class="col-6 col-md">'
             + '<div class="card stat-card ' + colour + (clickable ? ' ar-tile' : '')
             + (isOn ? ' ar-tile-on' : '') + '"'
             + (clickable ? ' data-filter="' + esc(filter) + '" title="'
                          + (isOn ? 'Click to clear this filter' : 'Show only these rows') + '"' : '')
             + '><div class="card-body stat-card-body py-2">'
             + '<div class="stat-card-text"><p class="stat-card-label mb-0">' + label
             + (isOn ? ' <i class="feather icon-x-circle"></i>' : '') + '</p>'
             + '<h3 class="stat-card-value" style="font-size:1.35rem;">' + value + '</h3></div>'
             + '</div></div></div>';
    };

    // Over a range, counts of day-rows are more meaningful than a headcount.
    var html = tile(review.IsRange ? 'Day records' : 'Employees',
                    review.IsRange ? review.TotalRows : review.TotalEmployees, 'bg-c-yellow', '')
             + tile('Present', review.Present, 'bg-c-green', 'present')
             + tile('Late', review.Late, 'bg-c-yellow', 'late')
             + tile('Absent', review.Absent, 'bg-c-pink', 'absent')
             + tile('No check-out', review.MissingCheckOut, 'bg-c-blue', 'nocheckout');

    if (review.IsRange) {
        html += tile('Late mins', review.TotalLateMinutes, 'bg-c-purple', '')
              + tile('Hours', review.TotalWorkingHours, 'bg-c-grey', '')
              + tile('Overtime', fmtMins(review.TotalOvertimeMinutes), 'bg-c-blue', 'overtime');
    }
    $('#arSummary').html(html);

    // Delegated so it survives every re-render.
    //
    // getAttribute rather than jQuery's .data(): .data() caches its first read, and these tiles
    // are rebuilt on every load, so reading the attribute directly is the safer of the two.
    // An empty result also has to fall through to "no filter" rather than to undefined, which
    // would clear the dropdown instead of setting it.
    $('#arSummary').off('click.artile').on('click.artile', '.ar-tile', function () {
        var f = this.getAttribute('data-filter') || '';
        // Clicking the tile that is already on clears it — the same gesture both ways.
        var next = (f === (review.RowFilter || '')) ? '' : f;
        $('#arFilter').val(next);
        loadReview(1);
    });

    if (review.Truncated) {
        notifyError('Too many rows — showing the first 5000. Narrow the range, employee or department.');
    }
}

function badge(s) {
    var map = {
        Present:'success', Late:'warning', Absent:'danger',
        OnLeave:'info', Holiday:'secondary', WeeklyOff:'secondary', HalfDay:'warning'
    };
    var cls = map[s] || 'light text-dark';
    return '<span class="badge bg-' + cls + '">' + esc(s) + '</span>';
}

/*
 * The row filter runs on the server now.
 *
 * It used to filter review.Rows here, which was correct while that array held the whole grid.
 * Once the screen was paged it silently became "filter this page": choosing Late narrowed the
 * 25 rows in front of you while the pager still counted all 110, and later pages showed rows
 * the filter should have excluded.
 */
function renderRows() {
    if (!review) return;
    var rows = review.Rows;
    var single = !review.IsRange;

    // Hide whichever column carries no information for this query.
    var oneEmployee = !!$('#arEmployee').val();
    $('.ar-date-col').toggle(!single);
    $('.ar-emp-col').toggle(!oneEmployee);

    var canEdit = window.reviewPerms.edit;

    amsPage('#arBody', rows, function (r) {
        var rowCls = r.HasNoShift ? ' class="ar-row-noshift"' : '';

        return '<tr' + rowCls + ' data-employee="' + r.EmployeeId + '" data-date="' + r.Date.split('T')[0] + '">'

            + '<td class="ps-3 small ar-date-col"' + (single ? ' style="display:none"' : '') + '>'
              + esc(r.DateDisplay)
              + (r.IsHoliday ? '<div class="text-warning" style="font-size:.68rem;">' + esc(r.HolidayName) + '</div>' : '')
              + '</td>'

            + '<td class="ar-emp-col"' + (oneEmployee ? ' style="display:none"' : '') + '>'
              + '<div class="fw-semibold small">' + esc(r.EmployeeName) + '</div>'
              + '<div class="text-muted" style="font-size:.72rem;">' + esc(r.EmployeeCode)
              + ' · ' + esc(r.Department) + '</div></td>'

            + '<td class="small">' + (r.ShiftName
                ? esc(r.ShiftName) + (r.GraceMinutes ? '<div class="text-muted" style="font-size:.7rem;">grace ' + r.GraceMinutes + 'm</div>' : '')
                : '<span class="text-danger">no shift</span>') + '</td>'

            + '<td class="text-center small text-muted">'
              + (r.ExpectedIn ? esc(r.ExpectedIn) + ' – ' + esc(r.ExpectedOut) : '—') + '</td>'

            + '<td class="text-center"><input type="time" class="form-control form-control-sm ar-time ar-in"'
              + ' value="' + esc(r.CheckInTime || '') + '"' + (canEdit ? '' : ' disabled') + '></td>'

            + '<td class="text-center"><input type="time" class="form-control form-control-sm ar-time ar-out"'
              + ' value="' + esc(r.CheckOutTime || '') + '"' + (canEdit ? '' : ' disabled') + '>'
              // Without this a 07:30 out time against a 22:00 shift looks like a mistake.
              + (r.IsNightShift && r.CheckOutTime
                    ? '<div class="text-muted" style="font-size:.64rem;">next day</div>' : '')
              + '</td>'

            // Late and early leave share one column. Both are labelled, because a bare "15m"
            // in a merged column cannot say which end of the day it belongs to — and a row
            // can carry both when someone arrives late and still leaves early.
            //
            // Late is red rather than amber once the month's allowance is used up, with the
            // count so the reason is visible without opening a report. Reporting only — the
            // status, hours and overtime of the day are untouched.
            + '<td class="text-center small ar-flag-col">'
              + (r.IsLate
                ? '<span class="badge bg-' + (r.IsOverLateAllowance ? 'danger' : 'warning text-dark') + '">'
                  + 'Late ' + esc(r.LateMinutes) + 'm</span>'
                  + (r.LateAllowance
                        ? '<div class="' + (r.IsOverLateAllowance ? 'text-danger' : 'text-muted')
                          + '" style="font-size:.64rem;">' + esc(r.LateOccurrence) + ' of '
                          + esc(r.LateAllowance) + '</div>'
                        : '')
                : '')
              + (r.IsEarlyLeave
                ? '<span class="badge bg-info' + (r.IsLate ? ' mt-1' : '') + '">'
                  + 'Early ' + esc(r.EarlyLeaveMinutes) + 'm</span>'
                : '')
              + (!r.IsLate && !r.IsEarlyLeave ? '—' : '')
              + '</td>'

            + '<td class="text-center small">'
              + (r.WorkingHours != null ? r.WorkingHours.toFixed(2) : '—')
              // Show the pre-break figure too, so a deduction is visible rather than looking
              // like the clock is wrong.
              + (r.BreakMinutes && r.GrossHours != null
                    ? '<div class="text-muted" style="font-size:.66rem;">gross ' + r.GrossHours.toFixed(2) + '</div>'
                    : '')
              + '</td>'

            + '<td class="text-center small">' + (r.OvertimeMinutes
                ? '<span class="badge bg-info">' + fmtMins(r.OvertimeMinutes) + '</span>' : '—') + '</td>'

            + '<td>' + badge(r.StatusDisplay)
              + (r.IsManual ? ' <span class="badge bg-light text-muted" title="Entered or corrected by a person">manual</span>' : '')
              + '</td>'

            + '<td class="text-end pe-3">' + (canEdit
                ? '<button class="btn btn-sm btn-outline-primary ar-save" disabled>Save</button>'
                : '') + '</td>'
            + '</tr>';
    }, {
        colspan: 11,
        empty: 'Nothing matches this filter.',
        label: 'row',
        server: {
            total: review.TotalRows,
            page: review.Page,
            pageSize: review.PageSize,
            onPage: loadReview
        }
    });

    $('#arBody').off('input.ar').on('input.ar', '.ar-time', function () {
        var tr = $(this).closest('tr');
        tr.find('.ar-time').addClass('ar-dirty');
        tr.find('.ar-save').prop('disabled', false);
    });

    $('#arBody').off('click.ar').on('click.ar', '.ar-save', function () {
        saveRow($(this).closest('tr'));
    });
}

function saveRow(tr) {
    var employeeId = parseInt(tr.data('employee'));
    var date = tr.data('date');
    var btn = tr.find('.ar-save');
    btn.prop('disabled', true).text('…');

    $.ajax({
        url: '/api/attendance-review/entry',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({
            EmployeeId: employeeId,
            Date: date,
            CheckInTime: tr.find('.ar-in').val() || null,
            CheckOutTime: tr.find('.ar-out').val() || null
        }),
        success: function (updated) {
            // Replace the matching row in the cached model. Matched on employee AND date,
            // since a range holds many rows per employee.
            for (var i = 0; i < review.Rows.length; i++) {
                if (review.Rows[i].EmployeeId === employeeId &&
                    review.Rows[i].Date.split('T')[0] === date) {
                    review.Rows[i] = updated;
                    break;
                }
            }
            recount();
            renderSummary();
            renderRows();
            notifySuccess('Updated ' + updated.EmployeeName + ' — ' + updated.DateDisplay + '.');
        },
        error: function (xhr) {
            btn.prop('disabled', false).text('Save');
            notifyError(xhr.responseText || 'Save failed.');
        }
    });
}

function recount() {
    var count = function (st) { return review.Rows.filter(function (r) { return r.StatusDisplay === st; }).length; };
    review.Present = count('Present');
    review.Late = count('Late');
    review.Absent = count('Absent');
    review.OnLeave = count('OnLeave');
    review.MissingCheckOut = review.Rows.filter(function (r) { return r.CheckIn && !r.CheckOut; }).length;
    review.TotalLateMinutes = review.Rows.reduce(function (a, r) { return a + (r.LateMinutes || 0); }, 0);
    review.TotalWorkingHours = Math.round(review.Rows.reduce(function (a, r) { return a + (r.WorkingHours || 0); }, 0) * 10) / 10;
    review.TotalOvertimeMinutes = review.Rows.reduce(function (a, r) { return a + (r.OvertimeMinutes || 0); }, 0);
}

/* ── Reprocess ────────────────────────────────────────────────────────────────
   Recalculates the loaded range from the times already stored, against the shift
   settings in force now. Correcting a shift changes nothing on its own — every
   day keeps the figures derived from the settings that were current when it was
   imported, until something recomputes them. */
function openReprocess() {
    var from = $('#arFrom').val(), to = $('#arTo').val();
    if (!from || !to) { notifyError('Choose a date range first.'); return; }

    $('#rpRange').text(from + ' to ' + to);
    $('#rpIncludeManual').prop('checked', false);
    $('#rpResult').html('');
    new bootstrap.Modal('#reprocessModal').show();
}

function submitReprocess() {
    var payload = {
        FromDate: $('#arFrom').val(),
        ToDate: $('#arTo').val(),
        DepartmentId: $('#arDept').val() ? parseInt($('#arDept').val()) : null,
        EmployeeId: $('#arEmployee').val() ? parseInt($('#arEmployee').val()) : null,
        IncludeManual: $('#rpIncludeManual').is(':checked')
    };

    $('#rpResult').html('<div class="text-muted small"><i class="fa fa-spinner fa-spin me-2"></i>Recalculating…</div>');

    $.ajax({
        url: '/api/attendance-lock/reprocess', type: 'POST',
        contentType: 'application/json', data: JSON.stringify(payload),
        success: function (r) {
            var row = function (label, value, cls) {
                return '<tr><td>' + esc(label) + '</td><td class="text-end fw-bold ' + (cls || '') + '">'
                     + esc(value) + '</td></tr>';
            };
            var html = '<table class="table table-sm border mb-2">'
                     + row('Records examined', r.Examined)
                     + row('Recalculated', r.Updated, 'text-success')
                     + row('Already correct', r.Unchanged, 'text-muted')
                     + row('Left as manually corrected', r.SkippedManual, r.SkippedManual ? 'text-warning' : 'text-muted')
                     + row('In a locked period', r.SkippedLocked, r.SkippedLocked ? 'text-danger' : 'text-muted')
                     + row('No shift assigned', r.SkippedNoShift, r.SkippedNoShift ? 'text-danger' : 'text-muted')
                     + '</table>';

            if (r.Warnings && r.Warnings.length) {
                html += '<div class="alert alert-warning py-2 mb-0 small">'
                      + r.Warnings.map(esc).join('<br>') + '</div>';
            }
            $('#rpResult').html(html);

            notifySuccess('Recalculated ' + r.Updated + ' record(s).');
            if (r.Updated > 0) loadReview(arPage);
        },
        error: function (xhr) {
            $('#rpResult').html('<div class="alert alert-danger py-2 mb-0 small">'
                + esc(xhr.responseText || 'Reprocessing failed.') + '</div>');
        }
    });
}

/* ── Locked periods ───────────────────────────────────────────────────────── */

function openLocks() {
    // Prefilled from the loaded range: closing the month you have just checked is
    // the reason anybody opens this.
    $('#lkFrom').val($('#arFrom').val());
    $('#lkTo').val($('#arTo').val());
    $('#lkReason').val('');

    $.getJSON('/api/branches', function (d) {
        var o = '<option value="">All branches</option>';
        (d || []).filter(function (b) { return b.IsActive; }).forEach(function (b) {
            o += '<option value="' + esc(b.Id) + '">' + esc(b.Name) + '</option>';
        });
        $('#lkBranch').html(o);
    });

    loadLocks();
    new bootstrap.Modal('#lockModal').show();
}

function loadLocks() {
    $.getJSON('/api/attendance-lock', function (rows) {
        if (!rows || !rows.length) {
            $('#lockBody').html('<tr><td colspan="5" class="text-center py-3 text-muted">No locked periods.</td></tr>');
            return;
        }
        $('#lockBody').html(rows.map(function (l) {
            return '<tr>'
                + '<td class="ps-3 small">' + esc(l.RangeDisplay) + '</td>'
                + '<td class="small text-muted">' + esc(l.BranchName) + '</td>'
                // What the lock is protecting — the weight of unlocking it.
                + '<td class="text-end small">' + esc(l.RecordCount) + '</td>'
                + '<td class="small text-muted">' + esc(l.Reason)
                + (l.LockedByName ? '<div style="font-size:.72rem;">by ' + esc(l.LockedByName) + '</div>' : '')
                + '</td>'
                + '<td class="pe-3 text-center">'
                + '<button class="btn btn-sm btn-outline-danger py-0 px-2" title="Unlock"'
                + ' onclick="unlockPeriod(' + l.Id + ', ' + l.RecordCount + ')"><i class="fa fa-unlock"></i></button>'
                + '</td></tr>';
        }).join(''));
    }).fail(function () {
        $('#lockBody').html('<tr><td colspan="5" class="text-danger text-center py-3">Failed to load.</td></tr>');
    });
}

function submitLock() {
    var reason = $('#lkReason').val().trim();
    if (!$('#lkFrom').val() || !$('#lkTo').val()) { notifyError('Choose both dates.'); return; }
    if (!reason) { notifyError('A reason is required — it is what explains the lock later.'); return; }

    $.ajax({
        url: '/api/attendance-lock', type: 'POST', contentType: 'application/json',
        data: JSON.stringify({
            FromDate: $('#lkFrom').val(), ToDate: $('#lkTo').val(),
            BranchId: $('#lkBranch').val() ? parseInt($('#lkBranch').val()) : null,
            Reason: reason
        }),
        success: function () { notifySuccess('Period locked.'); $('#lkReason').val(''); loadLocks(); },
        error: function (xhr) { notifyError(xhr.responseText || 'Could not lock that period.'); }
    });
}

function unlockPeriod(id, count) {
    notifyConfirm({
        title: 'Unlock this period?',
        text: count + ' attendance record(s) become editable again, and a biometric import '
            + 'will overwrite them. Only do this if the period genuinely needs correcting.',
        confirmText: 'Unlock', icon: 'warning'
    }, function () {
        var reason = window.prompt('Why is this period being reopened?', '');
        if (reason === null) return;
        if (!reason.trim()) { notifyError('A reason is required to unlock.'); return; }

        $.ajax({
            url: '/api/attendance-lock/unlock', type: 'POST', contentType: 'application/json',
            data: JSON.stringify({ Id: id, Reason: reason.trim() }),
            success: function () { notifySuccess('Period unlocked.'); loadLocks(); },
            error: function (xhr) { notifyError(xhr.responseText || 'Could not unlock.'); }
        });
    });
}

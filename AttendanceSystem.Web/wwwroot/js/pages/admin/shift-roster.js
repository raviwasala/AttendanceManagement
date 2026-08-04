/* ── Admin Shift Roster ── */

var roster = null;

/* Distinct colours per shift so a pattern is visible across the month at a glance.
   Assigned by position, not by shift id, so the palette stays stable per load. */
var SHIFT_COLOURS = ['#01a9ac', '#0ac282', '#fe9365', '#7759de', '#2DCEE3', '#fe5d70', '#64707d'];
var shiftColour = {};

$(function () {
    var now = new Date();

    var months = ['January','February','March','April','May','June',
                  'July','August','September','October','November','December'];
    $('#rMonth').html(months.map(function (m, i) {
        return '<option value="' + (i + 1) + '"' + (i === now.getMonth() ? ' selected' : '') + '>' + m + '</option>';
    }).join(''));

    var years = '';
    for (var y = now.getFullYear() - 2; y <= now.getFullYear() + 2; y++) {
        years += '<option value="' + y + '"' + (y === now.getFullYear() ? ' selected' : '') + '>' + y + '</option>';
    }
    $('#rYear').html(years);

    $.getJSON('/api/departments', function (d) {
        (d || []).filter(function (x) { return x.IsActive; }).forEach(function (x) {
            $('#rDept').append('<option value="' + esc(x.Id) + '">' + esc(x.Name) + '</option>');
        });
    }).always(loadRoster);
});

function loadRoster() {
    var y = $('#rYear').val(), m = $('#rMonth').val(), dept = $('#rDept').val();
    $('#rosterBody').html('<tr><td class="text-center py-4 text-muted">Loading…</td></tr>');

    var url = '/api/shift-roster?year=' + y + '&month=' + m + (dept ? '&departmentId=' + dept : '');
    $.getJSON(url, function (d) { roster = d; renderRoster(); })
     .fail(function (xhr) {
         $('#rosterBody').html('<tr><td class="text-danger text-center py-3">' +
             esc(xhr.responseText || 'Failed to load roster.') + '</td></tr>');
     });
}

function renderRoster() {
    if (!roster) return;

    shiftColour = {};
    roster.AvailableShifts.forEach(function (s, i) {
        shiftColour[s.Id] = SHIFT_COLOURS[i % SHIFT_COLOURS.length];
    });

    // Header: employee column + one per day, showing day number over weekday initial.
    var head = '<th class="emp-col">Employee</th>';
    for (var i = 0; i < roster.DaysInMonth; i++) {
        var day = roster.Employees.length ? roster.Employees[0].Days[i] : null;
        var dow = day ? day.DayOfWeek : '';
        head += '<th class="roster-head-day">' + (i + 1) + '<small>' + esc(dow) + '</small></th>';
    }
    $('#rosterHead').html(head);

    if (!roster.Employees.length) {
        $('#rosterBody').html('<tr><td colspan="' + (roster.DaysInMonth + 1) +
            '" class="text-center py-4 text-muted">No active employees for this filter.</td></tr>');
        $('#rosterLegend').empty();
        return;
    }

    var html = '';
    roster.Employees.forEach(function (e) {
        html += '<tr>';
        html += '<td class="emp-col">'
              + '<div class="fw-semibold small">' + esc(e.EmployeeName) + '</div>'
              + '<div class="text-muted" style="font-size:.7rem;">' + esc(e.EmployeeCode)
              + (e.DefaultShiftName
                    ? ' · ' + esc(e.DefaultShiftName)
                    : ' · <span class="text-danger">no shift</span>')
              + '</div>';
        if (window.rosterPerms.edit) {
            html += '<button class="btn btn-link btn-sm p-0 mt-1" style="font-size:.7rem;" '
                  + 'onclick="openRange(' + e.EmployeeId + ')">Apply to range…</button>';
        }
        html += '</td>';

        e.Days.forEach(function (d) {
            var cls = 'roster-day';
            if (d.IsOverride) cls += ' is-override';
            if (d.IsWeeklyOff) cls += ' is-weeklyoff';
            if (d.IsHoliday) cls += ' is-holiday';
            if (!d.ShiftId) cls += ' is-none';

            var style = '';
            if (d.ShiftId && !d.IsWeeklyOff) {
                var c = shiftColour[d.ShiftId] || '#adb5bd';
                // Override gets a solid chip; inherited days get a soft tint, so the days
                // that were deliberately changed stand out from the routine.
                style = d.IsOverride
                    ? 'background:' + c + ';color:#fff;'
                    : 'background:' + c + '22;color:#495057;';
            }

            var label = d.ShiftId ? initials(d.ShiftName) : '—';
            var title = (d.ShiftName || 'No shift assigned')
                      + (d.ShiftTimes ? ' (' + d.ShiftTimes + ')' : '')
                      + (d.IsOverride ? ' — changed for this day' : '')
                      + (d.IsWeeklyOff ? ' — weekly off' : '')
                      + (d.IsHoliday ? ' — holiday: ' + d.HolidayName : '');

            html += '<td class="' + cls + '" style="' + style + '" title="' + esc(title) + '"'
                  + (window.rosterPerms.edit
                        ? ' onclick="openDay(' + e.EmployeeId + ',\'' + d.Date + '\')"'
                        : '')
                  + '>' + esc(label) + '</td>';
        });

        html += '</tr>';
    });
    $('#rosterBody').html(html);

    // Legend
    var legend = roster.AvailableShifts.map(function (s) {
        return '<span><span class="badge" style="background:' + shiftColour[s.Id] + ';">&nbsp;</span> '
             + esc(s.Name) + ' <span class="text-muted">' + esc(s.StartTimeDisplay) + '</span></span>';
    }).join('');
    legend += '<span><span class="badge bg-secondary">&nbsp;</span> solid = changed for that day</span>';
    legend += '<span><span class="badge" style="background:#f1f3f5;">&nbsp;</span> weekly off</span>';
    legend += '<span><span class="badge" style="background:#fff4e6;">&nbsp;</span> holiday</span>';
    $('#rosterLegend').html(legend);

    var warn = $('#rosterWarning');
    if (roster.EmployeesWithoutAssignment > 0) {
        warn.removeClass('d-none').html('<i class="feather icon-alert-triangle me-1"></i>'
            + roster.EmployeesWithoutAssignment + ' employee(s) have no shift assignment at all. '
            + 'They are never marked late or early — assign a shift from the Shifts screen.');
    } else {
        warn.addClass('d-none');
    }
}

/** Compact cell label: "General Shift" -> "GS", "Night" -> "NI". */
function initials(name) {
    if (!name) return '—';
    var parts = name.trim().split(/\s+/);
    if (parts.length === 1) return parts[0].substring(0, 2).toUpperCase();
    return (parts[0][0] + parts[1][0]).toUpperCase();
}

function findEmployee(id) {
    return roster.Employees.filter(function (e) { return e.EmployeeId === id; })[0];
}

function openDay(employeeId, dateIso) {
    var emp = findEmployee(employeeId);
    var day = emp.Days.filter(function (d) { return d.Date === dateIso; })[0];
    var date = new Date(dateIso);

    $('#dayEmployeeId').val(employeeId);
    $('#dayDate').val(dateIso);
    $('#dayModalTitle').text('Change Shift');
    $('#dayContext').html('<strong>' + esc(emp.EmployeeName) + '</strong><br>'
        + date.toLocaleDateString(undefined, { weekday:'long', day:'numeric', month:'long', year:'numeric' })
        + (day.IsHoliday ? '<br><span class="text-warning">Holiday: ' + esc(day.HolidayName) + '</span>' : ''));

    var opts = '<option value="">Use normal shift'
             + (emp.DefaultShiftName ? ' (' + esc(emp.DefaultShiftName) + ')' : '')
             + '</option>';
    roster.AvailableShifts.forEach(function (s) {
        opts += '<option value="' + esc(s.Id) + '">' + esc(s.Name) + ' — '
              + esc(s.StartTimeDisplay) + ' to ' + esc(s.EndTimeDisplay) + '</option>';
    });
    $('#dayShift').html(opts).val(day.IsOverride ? String(day.ShiftId) : '');

    new bootstrap.Modal('#dayModal').show();
}

function saveDay() {
    var val = $('#dayShift').val();
    $.ajax({
        url: '/api/shift-roster/day',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({
            EmployeeId: parseInt($('#dayEmployeeId').val()),
            Date: $('#dayDate').val(),
            ShiftId: val === '' ? null : parseInt(val)
        }),
        success: function () {
            bootstrap.Modal.getInstance('#dayModal').hide();
            notifySuccess(val === '' ? 'Day reset to the normal shift.' : 'Shift updated for that day.');
            loadRoster();
        },
        error: function (xhr) { notifyError(xhr.responseText || 'Update failed.'); }
    });
}

function openRange(employeeId) {
    var emp = findEmployee(employeeId);
    $('#rangeEmployeeId').val(employeeId);
    $('#rangeContext').html('<strong>' + esc(emp.EmployeeName) + '</strong> · ' + esc(emp.EmployeeCode));

    // Default the range to the month currently on screen.
    var y = roster.Year, m = roster.Month;
    var pad = function (n) { return (n < 10 ? '0' : '') + n; };
    $('#rangeFrom').val(y + '-' + pad(m) + '-01');
    $('#rangeTo').val(y + '-' + pad(m) + '-' + pad(roster.DaysInMonth));

    var opts = '<option value="">Use normal shift (clear changes in range)</option>';
    roster.AvailableShifts.forEach(function (s) {
        opts += '<option value="' + esc(s.Id) + '">' + esc(s.Name) + ' — '
              + esc(s.StartTimeDisplay) + ' to ' + esc(s.EndTimeDisplay) + '</option>';
    });
    $('#rangeShift').html(opts);

    new bootstrap.Modal('#rangeModal').show();
}

function saveRange() {
    var val = $('#rangeShift').val();
    var from = $('#rangeFrom').val(), to = $('#rangeTo').val();
    if (!from || !to) { notifyError('Choose both a start and an end date.'); return; }

    $.ajax({
        url: '/api/shift-roster/range',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({
            EmployeeId: parseInt($('#rangeEmployeeId').val()),
            FromDate: from,
            ToDate: to,
            ShiftId: val === '' ? null : parseInt(val),
            SkipWeeklyOff: $('#rangeSkipOff').is(':checked')
        }),
        success: function () {
            bootstrap.Modal.getInstance('#rangeModal').hide();
            notifySuccess('Range updated.');
            loadRoster();
        },
        error: function (xhr) { notifyError(xhr.responseText || 'Update failed.'); }
    });
}

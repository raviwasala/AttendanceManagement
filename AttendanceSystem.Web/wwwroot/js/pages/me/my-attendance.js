/* ── Employee self-service: My Attendance ── */

function fmtMins(m) {
    if (!m) return '—';
    var h = Math.floor(m / 60), r = m % 60;
    return h ? h + 'h ' + (r ? r + 'm' : '') : r + 'm';
}

$(function () {
    var now = new Date();
    var months = ['January','February','March','April','May','June',
                  'July','August','September','October','November','December'];
    $('#myMonth').html(months.map(function (m, i) {
        return '<option value="' + (i + 1) + '"' + (i === now.getMonth() ? ' selected' : '') + '>' + m + '</option>';
    }).join(''));

    var years = '';
    for (var y = now.getFullYear() - 2; y <= now.getFullYear(); y++) {
        years += '<option value="' + y + '"' + (y === now.getFullYear() ? ' selected' : '') + '>' + y + '</option>';
    }
    $('#myYear').html(years);

    $.getJSON('/api/me/profile', function (p) {
        $('#myProfileLine').text(p.FullName + ' · ' + p.EmployeeCode + ' · ' + p.Designation
            + (p.ShiftName ? ' · ' + p.ShiftName + ' (' + p.ShiftTimes + ')' : ''));
    }).fail(function (xhr) {
        // The likely cause is a user account with no linked employee record; say so plainly
        // instead of leaving every panel empty with no explanation.
        $('#myNotLinked').removeClass('d-none').text(xhr.responseText || 'Could not load your profile.');
    });

    loadMyAttendance();
});

function loadMyAttendance() {
    var y = $('#myYear').val(), m = $('#myMonth').val();
    $('#myBody').html('<tr><td colspan="9" class="text-center py-4 text-muted">Loading…</td></tr>');

    $.getJSON('/api/me/attendance?year=' + y + '&month=' + m, function (d) {
        renderMySummary(d);
        renderMyDays(d);
    }).fail(function (xhr) {
        $('#myBody').html('<tr><td colspan="9" class="text-danger text-center py-3">' +
            esc(xhr.responseText || 'Failed to load.') + '</td></tr>');
        $('#mySummary').empty();
    });
}

function renderMySummary(d) {
    var tile = function (label, value, colour) {
        return '<div class="col-6 col-md">'
             + '<div class="card stat-card ' + colour + '"><div class="card-body stat-card-body py-2">'
             + '<div class="stat-card-text"><p class="stat-card-label mb-0">' + label + '</p>'
             + '<h3 class="stat-card-value" style="font-size:1.35rem;">' + value + '</h3></div>'
             + '</div></div></div>';
    };
    $('#mySummary').html(
        tile('Present', d.PresentDays, 'bg-c-green') +
        tile('Late', d.LateDays, 'bg-c-yellow') +
        tile('Absent', d.AbsentDays, 'bg-c-pink') +
        tile('Leave', d.LeaveDays, 'bg-c-blue') +
        tile('Hours', d.TotalWorkingHours, 'bg-c-grey') +
        tile('Overtime', fmtMins(d.TotalOvertimeMinutes), 'bg-c-purple')
    );
}

function badge(s) {
    var map = {
        Present:'success', Late:'warning', Absent:'danger',
        OnLeave:'info', Holiday:'secondary', WeeklyOff:'secondary', HalfDay:'warning'
    };
    return '<span class="badge bg-' + (map[s] || 'light text-dark') + '">' + esc(s) + '</span>';
}

function renderMyDays(d) {
    if (!d.Days.length) {
        $('#myBody').html('<tr><td colspan="9" class="text-center py-4 text-muted">Nothing recorded for this month yet.</td></tr>');
        return;
    }

    $('#myBody').html(d.Days.map(function (r) {
        var muted = (r.IsWeeklyOff || r.IsHoliday) ? ' class="text-muted"' : '';
        return '<tr' + muted + '>'
            + '<td class="ps-3 small">' + esc(r.DateDisplay)
              + (r.IsHoliday ? '<div class="text-warning" style="font-size:.68rem;">' + esc(r.HolidayName) + '</div>' : '')
              + '</td>'
            + '<td class="small">' + (r.ShiftName ? esc(r.ShiftName) : '—') + '</td>'
            + '<td class="text-center small text-muted">' + (r.ExpectedIn ? esc(r.ExpectedIn) + '–' + esc(r.ExpectedOut) : '—') + '</td>'
            + '<td class="text-center small">' + (r.CheckIn ? esc(r.CheckIn) : '—') + '</td>'
            + '<td class="text-center small">' + (r.CheckOut ? esc(r.CheckOut) : '—') + '</td>'
            + '<td class="text-center small">' + (r.LateMinutes ? '<span class="badge bg-warning text-dark">' + r.LateMinutes + 'm</span>' : '—') + '</td>'
            + '<td class="text-center small">' + (r.WorkingHours != null ? r.WorkingHours.toFixed(2) : '—') + '</td>'
            + '<td class="text-center small">' + (r.OvertimeMinutes ? '<span class="badge bg-info">' + fmtMins(r.OvertimeMinutes) + '</span>' : '—') + '</td>'
            + '<td class="pe-3">' + badge(r.StatusDisplay) + '</td>'
            + '</tr>';
    }).join(''));
}

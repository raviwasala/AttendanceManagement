/* ── Admin Attendance Review ── */

var review = null;

function iso(d) { return d.toISOString().split('T')[0]; }

/** 95 -> "1h 35m"; overtime is easier to sanity-check in hours than in raw minutes. */
function fmtMins(m) {
    if (!m) return '—';
    var h = Math.floor(m / 60), r = m % 60;
    return h ? h + 'h ' + (r ? r + 'm' : '') : r + 'm';
}

$(function () {
    preset('today');

    $.getJSON('/api/departments', function (d) {
        (d || []).filter(function (x) { return x.IsActive; }).forEach(function (x) {
            $('#arDept').append('<option value="' + esc(x.Id) + '">' + esc(x.Name) + '</option>');
        });
    });

    $.getJSON('/api/employees', function (d) {
        (d || []).forEach(function (e) {
            $('#arEmployee').append('<option value="' + esc(e.Id) + '">'
                + esc(e.FullName) + ' (' + esc(e.EmployeeCode) + ')</option>');
        });
    }).always(loadReview);

    $('#arFilter').on('change', renderRows);

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

function loadReview() {
    var from = $('#arFrom').val(), to = $('#arTo').val();
    if (!from || !to) { notifyError('Choose both a start and an end date.'); return; }
    if (to < from) { notifyError('End date must be on or after the start date.'); return; }

    var emp = $('#arEmployee').val(), dept = $('#arDept').val();

    $('#arBody').html('<tr><td colspan="11" class="text-center py-4 text-muted">Loading…</td></tr>');

    var url = '/api/attendance-review?from=' + from + '&to=' + to
            + (emp ? '&employeeId=' + emp : '')
            + (dept ? '&departmentId=' + dept : '');

    $.getJSON(url, function (d) { review = d; renderSummary(); renderRows(); })
     .fail(function (xhr) {
         $('#arBody').html('<tr><td colspan="11" class="text-danger text-center py-3">' +
             esc(xhr.responseText || 'Failed to load.') + '</td></tr>');
     });
}

function renderSummary() {
    $('#arRangeLabel').text(review.RangeDisplay);

    var tile = function (label, value, colour) {
        return '<div class="col-6 col-md">'
             + '<div class="card stat-card ' + colour + '"><div class="card-body stat-card-body py-2">'
             + '<div class="stat-card-text"><p class="stat-card-label mb-0">' + label + '</p>'
             + '<h3 class="stat-card-value" style="font-size:1.35rem;">' + value + '</h3></div>'
             + '</div></div></div>';
    };

    // Over a range, counts of day-rows are more meaningful than a headcount.
    var html = tile(review.IsRange ? 'Day records' : 'Employees',
                    review.IsRange ? review.Rows.length : review.TotalEmployees, 'bg-c-yellow')
             + tile('Present', review.Present, 'bg-c-green')
             + tile('Late', review.Late, 'bg-c-yellow')
             + tile('Absent', review.Absent, 'bg-c-pink')
             + tile('No check-out', review.MissingCheckOut, 'bg-c-blue');

    if (review.IsRange) {
        html += tile('Late mins', review.TotalLateMinutes, 'bg-c-purple')
              + tile('Hours', review.TotalWorkingHours, 'bg-c-grey')
              + tile('Overtime', fmtMins(review.TotalOvertimeMinutes), 'bg-c-blue');
    }
    $('#arSummary').html(html);

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

function visibleRows() {
    var f = $('#arFilter').val();
    if (!f) return review.Rows;
    return review.Rows.filter(function (r) {
        switch (f) {
            case 'late':       return r.StatusDisplay === 'Late';
            case 'absent':     return r.StatusDisplay === 'Absent';
            case 'nocheckout': return r.CheckIn && !r.CheckOut;
            case 'recorded':   return !!r.CheckIn;
            case 'exceptions':
                return r.StatusDisplay === 'Late' || r.StatusDisplay === 'Absent'
                    || (r.CheckIn && !r.CheckOut) || r.HasNoShift || r.IsEarlyLeave;
            default: return true;
        }
    });
}

function renderRows() {
    if (!review) return;
    var rows = visibleRows();
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

            + '<td class="text-center small">' + (r.IsLate
                ? '<span class="badge bg-warning text-dark">' + r.LateMinutes + 'm</span>' : '—') + '</td>'

            + '<td class="text-center small">' + (r.IsEarlyLeave
                ? '<span class="badge bg-warning text-dark">' + r.EarlyLeaveMinutes + 'm</span>' : '—') + '</td>'

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
    }, { colspan: 11, empty: 'Nothing matches this filter.', label: 'row' });

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

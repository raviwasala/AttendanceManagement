/* ── Month End & Payroll ── */

var meStatus = null;

$(function () { loadStatus(); });

function mePeriod() {
    return { month: parseInt($('#meMonth').val(), 10), year: parseInt($('#meYear').val(), 10) };
}

function loadStatus() {
    var p = mePeriod();
    $('#meChecks').html('<div class="text-muted small">Checking…</div>');
    $('#meCloseBtn').prop('disabled', true);

    $.getJSON('/api/month-end/status?month=' + p.month + '&year=' + p.year, function (d) {
        meStatus = d;
        renderStatus();
    }).fail(function (xhr) {
        $('#meChecks').html('<div class="text-danger small">'
            + esc(xhr.responseText || 'Could not check the month.') + '</div>');
    });
}

function renderStatus() {
    var d = meStatus;

    if (d.IsClosed) {
        $('#meStatusBanner').html('<div class="alert alert-success py-2 mb-3">'
            + '<i class="feather icon-lock me-1"></i><strong>' + esc(d.PeriodDisplay) + ' is closed.</strong> '
            + esc(d.ClosedReason || '')
            + (d.ClosedBy ? ' — closed by ' + esc(d.ClosedBy) : '')
            + (d.ClosedAt ? ' on ' + new Date(d.ClosedAt).toLocaleDateString() : '')
            + '<div class="small mt-1">Attendance in this month cannot be changed. '
            + 'To reopen it, remove the lock on Attendance Review.</div>'
            + '</div>');
    } else if (d.Blockers.length) {
        $('#meStatusBanner').html('<div class="alert alert-danger py-2 mb-3">'
            + '<i class="feather icon-alert-octagon me-1"></i><strong>Not ready to close.</strong> '
            + 'Clear the items marked below first.</div>');
    } else if (d.Warnings.length) {
        $('#meStatusBanner').html('<div class="alert alert-warning py-2 mb-3">'
            + '<i class="feather icon-alert-triangle me-1"></i><strong>Ready, with warnings.</strong> '
            + 'You can close the month, but read the warnings first — they change what gets paid.</div>');
    } else {
        $('#meStatusBanner').html('<div class="alert alert-success py-2 mb-3">'
            + '<i class="feather icon-check-circle me-1"></i><strong>Ready to close.</strong> '
            + 'Every check passed for ' + esc(d.EmployeeCount) + ' employee(s).</div>');
    }

    // Blockers first, then warnings, then what passed — the order somebody needs to act in.
    var order = function (c) { return c.Passed ? 2 : (c.IsBlocking ? 0 : 1); };
    var checks = (d.Checks || []).slice().sort(function (a, b) { return order(a) - order(b); });

    $('#meChecks').html(checks.map(function (c) {
        var icon, cls;
        if (c.Passed)          { icon = 'check-circle';   cls = 'text-success'; }
        else if (c.IsBlocking) { icon = 'x-circle';       cls = 'text-danger'; }
        else                   { icon = 'alert-triangle'; cls = 'text-warning'; }

        return '<div class="d-flex align-items-start py-2 border-bottom">'
             + '<i class="feather icon-' + icon + ' ' + cls + ' me-2 mt-1"></i>'
             + '<div class="flex-grow-1">'
             + '<div class="fw-semibold small">' + esc(c.Title)
             + (c.Passed ? '' : ' <span class="badge bg-' + (c.IsBlocking ? 'danger' : 'warning text-dark')
                                + ' ms-1">' + esc(c.Count) + '</span>')
             + '</div>'
             + '<div class="text-muted small">' + esc(c.Detail) + '</div>'
             + '</div>'
             + (!c.Passed && c.ActionUrl
                  ? '<a href="' + esc(c.ActionUrl) + '" class="btn btn-sm btn-outline-secondary ms-2">'
                    + esc(c.ActionLabel || 'Fix') + '</a>'
                  : '')
             + '</div>';
    }).join(''));

    $('#meCloseBtn').prop('disabled', !d.CanClose);
}

function closeMonth() {
    var reason = $('#meReason').val().trim();
    if (!reason) { notifyError('Give a reason — it is recorded on the lock.', 'Reason required'); return; }
    if (!meStatus || !meStatus.CanClose) return;

    var warnings = meStatus.Warnings || [];
    var text = 'This locks every day in ' + meStatus.PeriodDisplay
             + '. Nobody will be able to add, edit or delete attendance in it — including imports.';

    if (warnings.length) {
        text += '\n\nWarnings you are accepting:\n'
              + warnings.map(function (w) { return '• ' + w.Detail; }).join('\n');
    }

    notifyConfirm({
        title: 'Close ' + meStatus.PeriodDisplay + '?',
        text: text,
        confirmText: 'Close the month',
        icon: 'warning'
    }, function () {
        $.ajax({
            url: '/api/month-end/close', type: 'POST', contentType: 'application/json',
            // Acknowledged explicitly rather than assumed: the server refuses while warnings
            // stand unless the caller has actually been shown them.
            data: JSON.stringify({
                Month: meStatus.Month, Year: meStatus.Year,
                Reason: reason, AcknowledgeWarnings: true
            }),
            success: function () {
                notifySuccess(meStatus.PeriodDisplay + ' is closed.');
                $('#meReason').val('');
                loadStatus();
            },
            error: function (xhr) { notifyError(xhr.responseText || 'Could not close the month.'); }
        });
    });
}

function loadPayroll() {
    var p = mePeriod();
    $('#mePayrollBody').html('<tr><td colspan="11" class="text-center py-3 text-muted">Loading…</td></tr>');

    $.getJSON('/api/month-end/payroll?month=' + p.month + '&year=' + p.year, function (d) {
        // Stated on screen as well as in the file: figures from an open month can still
        // change, and that is the difference between a draft and a payroll run.
        $('#mePayrollNote').html(d.IsClosed
            ? '<div class="alert alert-success py-2 small"><i class="feather icon-lock me-1"></i>'
              + esc(d.PeriodDisplay) + ' is closed — these figures are final.</div>'
            : '<div class="alert alert-warning py-2 small"><i class="feather icon-alert-triangle me-1"></i>'
              + esc(d.PeriodDisplay) + ' is <strong>not closed</strong>. These figures can still change. '
              + 'Close the month before paying from them.</div>');

        amsPage('#mePayrollBody', d.Rows, function (r) {
            return '<tr>'
                 + '<td class="ps-3 fw-semibold">' + esc(r.EmployeeCode) + '</td>'
                 + '<td>' + esc(r.EmployeeName) + '</td>'
                 + '<td class="text-muted small">' + esc(r.Department) + '</td>'
                 + '<td class="text-center">' + esc(r.WorkingDays) + '</td>'
                 + '<td class="text-center"><span class="badge bg-success">' + esc(r.PresentDays) + '</span></td>'
                 + '<td class="text-center"><span class="badge bg-danger">' + esc(r.AbsentDays) + '</span></td>'
                 + '<td class="text-center"><span class="badge bg-info">' + esc(r.LeaveDays) + '</span></td>'
                 + '<td class="text-center">' + esc(r.LateDays) + '</td>'
                 + '<td class="text-center">' + r.WorkingHours.toFixed(2) + '</td>'
                 + '<td class="text-center">' + (r.ApprovedOtHours
                        ? '<span class="badge bg-primary">' + r.ApprovedOtHours.toFixed(2) + ' h</span>'
                        : '—') + '</td>'
                 + '<td class="text-center pe-3">' + (r.AttendancePercentage || 0).toFixed(0) + '%</td>'
                 + '</tr>';
        }, { colspan: 11, empty: 'No employees found for this month.', label: 'employee' });
    }).fail(function (xhr) {
        $('#mePayrollBody').html('<tr><td colspan="11" class="text-danger text-center py-3">'
            + esc(xhr.responseText || 'Failed to load.') + '</td></tr>');
    });
}

function downloadPayroll() {
    var p = mePeriod();
    // Built server-side so the file holds every employee, not the page on screen.
    window.location = '/api/month-end/payroll.csv?month=' + p.month + '&year=' + p.year;
}

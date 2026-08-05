/* ── Admin Overtime Register ── */

var regData = null;

$(function () {
    $.when(
        $.getJSON('/api/departments', function (d) {
            (d || []).filter(function (x) { return x.IsActive; }).forEach(function (x) {
                $('#regDept').append('<option value="' + esc(x.Id) + '">' + esc(x.Name) + '</option>');
            });
        }),
        $.getJSON('/api/employees', function (d) {
            (d || []).filter(function (x) { return x.IsActive; }).forEach(function (x) {
                $('#regEmployee').append('<option value="' + esc(x.Id) + '">'
                    + esc(x.EmployeeCode) + ' — ' + esc(x.FullName) + '</option>');
            });
        })
    ).always(loadRegister);

    $('#regDept, #regEmployee, #regStatus').on('change', loadRegister);
});

function loadRegister() {
    var q = '?from=' + $('#regFrom').val() + '&to=' + $('#regTo').val();
    if ($('#regDept').val())     q += '&departmentId=' + encodeURIComponent($('#regDept').val());
    if ($('#regEmployee').val()) q += '&employeeId=' + encodeURIComponent($('#regEmployee').val());
    if ($('#regStatus').val())   q += '&status=' + encodeURIComponent($('#regStatus').val());

    $('#regBody').html('<tr><td colspan="12" class="text-center py-4 text-muted">Loading…</td></tr>');

    $.getJSON('/api/overtime/register' + q, function (d) { regData = d; renderRegister(); })
     .fail(function (xhr) {
         $('#regBody').html('<tr><td colspan="12" class="text-danger text-center py-3">'
             + esc(xhr.responseText || 'Failed to load the overtime register.') + '</td></tr>');
     });
}

function regTile(label, value, colour) {
    return '<div class="col-6 col-md-3"><div class="card mb-0"><div class="card-body py-2 px-3">'
         + '<div class="text-muted" style="font-size:.72rem;">' + esc(label) + '</div>'
         + '<div class="h5 mb-0 text-' + colour + '">' + esc(value) + '</div>'
         + '</div></div></div>';
}

function statusBadge(s) {
    var map = { Pending: 'warning text-dark', Approved: 'success', Rejected: 'danger' };
    return '<span class="badge bg-' + (map[s] || 'secondary') + '">' + esc(s) + '</span>';
}

function renderRegister() {
    if (!regData) return;

    $('#regStats').html(
        regTile('Claims', regData.Rows.length, 'primary')
      + regTile('Pending', regData.PendingCount, 'warning')
      + regTile('Approved', regData.TotalApprovedDisplay, 'success')
      + regTile('Weighted hours', regData.TotalWeightedHours.toFixed(2) + ' h', 'info'));

    amsPage('#regBody', regData.Rows, function (r) {
        var dayBadge = r.DayTypeDisplay === 'Holiday'
            ? '<span class="badge bg-danger">Holiday</span>'
            : r.DayTypeDisplay === 'Weekly off'
                ? '<span class="badge bg-warning text-dark">Weekly off</span>'
                : '<span class="badge bg-light text-muted">Working</span>';

        return '<tr>'
            + '<td class="ps-3 small">' + esc(r.DateDisplay)
              + '<div class="text-muted" style="font-size:.68rem;">' + esc(r.DayName) + '</div></td>'
            + '<td><div class="fw-semibold small">' + esc(r.EmployeeName) + '</div>'
              + '<div class="text-muted" style="font-size:.7rem;">' + esc(r.EmployeeCode) + '</div></td>'
            + '<td class="small text-muted">' + esc(r.Department) + '</td>'
            + '<td class="small text-muted">' + (r.ShiftName ? esc(r.ShiftName) : '—') + '</td>'
            + '<td class="text-center small text-muted">'
              + (r.CheckInDisplay ? esc(r.CheckInDisplay) + ' – ' + esc(r.CheckOutDisplay || '?') : '—') + '</td>'
            + '<td class="text-center">' + dayBadge + '</td>'
            + '<td class="text-center small">' + esc(r.ClaimedDisplay) + '</td>'
            + '<td class="text-center fw-semibold">' + esc(r.ApprovedDisplay) + '</td>'
            + '<td class="text-center small text-muted">&times;' + esc(r.RateMultiplier) + '</td>'
            + '<td class="text-center small">' + (r.WeightedHours ? r.WeightedHours.toFixed(2) + ' h' : '—') + '</td>'
            + '<td>' + statusBadge(r.StatusDisplay)
              + (r.RejectionReason
                    ? '<div class="text-danger" style="font-size:.66rem;" title="' + esc(r.RejectionReason) + '">'
                      + esc(r.RejectionReason.substring(0, 40)) + '</div>'
                    : '')
              + '</td>'
            + '<td class="pe-3 small text-muted">' + (r.ApprovedByName ? esc(r.ApprovedByName) : '—')
              + (r.ApprovedAt
                    ? '<div style="font-size:.66rem;">' + new Date(r.ApprovedAt).toLocaleDateString() + '</div>'
                    : '')
              + '</td>'
            + '</tr>';
    }, { colspan: 12, empty: 'No overtime recorded in this range.', label: 'claim' });
}

/* Built in the browser from what is already on screen — the register is a filtered view,
   and exporting anything other than exactly what the user is looking at invites mistakes. */
function exportRegister() {
    if (!regData || !regData.Rows.length) { notifyError('Nothing to export.'); return; }

    var header = ['Date', 'Day', 'Employee Code', 'Employee', 'Department', 'Shift',
                  'Check In', 'Check Out', 'Day Type', 'Raw Minutes', 'Claimed Minutes',
                  'Approved Minutes', 'Rate', 'Weighted Hours', 'Status', 'Rule',
                  'Decided By', 'Decided At', 'Reason'];

    var rows = regData.Rows.map(function (r) {
        return [r.DateDisplay, r.DayName, r.EmployeeCode, r.EmployeeName, r.Department,
                r.ShiftName || '', r.CheckInDisplay || '', r.CheckOutDisplay || '',
                r.DayTypeDisplay, r.RawMinutes, r.ClaimedMinutes,
                r.ApprovedMinutes == null ? '' : r.ApprovedMinutes,
                r.RateMultiplier, r.WeightedHours, r.StatusDisplay, r.RuleName || '',
                r.ApprovedByName || '', r.ApprovedAt ? new Date(r.ApprovedAt).toLocaleString() : '',
                r.RejectionReason || r.Remarks || ''];
    });

    // A leading = + - or @ makes Excel treat the cell as a formula, so those are prefixed
    // with a quote. Quotes are doubled and every field is wrapped, per RFC 4180.
    var cell = function (v) {
        var s = v == null ? '' : String(v);
        if (/^[=+\-@]/.test(s)) s = "'" + s;
        return '"' + s.replace(/"/g, '""') + '"';
    };

    var csv = [header].concat(rows).map(function (r) { return r.map(cell).join(','); }).join('\r\n');
    // BOM so Excel reads it as UTF-8 rather than the system codepage.
    var blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8;' });

    var a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'overtime-register-' + $('#regFrom').val() + '-to-' + $('#regTo').val() + '.csv';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(a.href);
}

/* ── Admin Overtime Summary ── */

var sumData = null;
var useRange = false;

$(function () {
    $.getJSON('/api/departments', function (d) {
        (d || []).filter(function (x) { return x.IsActive; }).forEach(function (x) {
            $('#sumDept').append('<option value="' + esc(x.Id) + '">' + esc(x.Name) + '</option>');
        });
    }).always(loadSummary);

    $('#sumMonth, #sumYear, #sumDept').on('change', loadSummary);
});

function toggleRange() {
    useRange = !useRange;
    $('#sumRangeRow').toggleClass('d-none', !useRange);
    $('#rangeToggle').text(useRange ? 'Back to whole months' : 'Use a custom date range…');

    if (useRange && !$('#sumFrom').val()) {
        // Seed the range from the month currently selected, so switching does not blank the screen.
        var r = monthRange();
        $('#sumFrom').val(r.from);
        $('#sumTo').val(r.to);
    }
    loadSummary();
}

function monthRange() {
    var m = parseInt($('#sumMonth').val(), 10);
    var y = parseInt($('#sumYear').val(), 10);
    var pad = function (n) { return (n < 10 ? '0' : '') + n; };
    var last = new Date(y, m, 0).getDate();
    return { from: y + '-' + pad(m) + '-01', to: y + '-' + pad(m) + '-' + pad(last) };
}

function loadSummary() {
    var range = useRange
        ? { from: $('#sumFrom').val(), to: $('#sumTo').val() }
        : monthRange();
    if (!range.from || !range.to) return;

    var q = '?from=' + range.from + '&to=' + range.to;
    if ($('#sumDept').val()) q += '&departmentId=' + encodeURIComponent($('#sumDept').val());

    $('#sumBody').html('<tr><td colspan="8" class="text-center py-4 text-muted">Loading…</td></tr>');

    $.getJSON('/api/overtime/summary' + q, function (d) { sumData = d; renderSummary(); })
     .fail(function (xhr) {
         $('#sumBody').html('<tr><td colspan="8" class="text-danger text-center py-3">'
             + esc(xhr.responseText || 'Failed to load the overtime summary.') + '</td></tr>');
     });
}

function sumTile(label, value, colour) {
    return '<div class="col-6 col-md-3"><div class="card mb-0"><div class="card-body py-2 px-3">'
         + '<div class="text-muted" style="font-size:.72rem;">' + esc(label) + '</div>'
         + '<div class="h5 mb-0 text-' + colour + '">' + esc(value) + '</div>'
         + '</div></div></div>';
}

function renderSummary() {
    if (!sumData) return;

    $('#sumStats').html(
        sumTile('Period', sumData.PeriodDisplay, 'dark')
      + sumTile('Employees with OT', sumData.EmployeesWithOvertime, 'primary')
      + sumTile('Approved', sumData.TotalApprovedDisplay, 'success')
      + sumTile('Weighted hours', sumData.TotalWeightedHours.toFixed(2) + ' h', 'info'));

    amsPage('#sumBody', sumData.Rows, function (r) {
        return '<tr>'
            + '<td class="ps-3"><div class="fw-semibold small">' + esc(r.EmployeeName) + '</div>'
              + '<div class="text-muted" style="font-size:.7rem;">' + esc(r.EmployeeCode) + '</div></td>'
            + '<td class="small text-muted">' + esc(r.Department) + '</td>'
            + '<td class="text-center small">' + esc(r.Days) + '</td>'
            + '<td class="text-center small">' + (r.PendingMinutes
                ? '<span class="badge bg-warning text-dark">' + esc(r.PendingDisplay) + '</span>'
                : '<span class="text-muted">—</span>') + '</td>'
            + '<td class="text-center fw-semibold text-success">' + esc(r.ApprovedDisplay) + '</td>'
            + '<td class="text-center small">' + (r.RejectedMinutes
                ? '<span class="text-danger">' + esc(r.RejectedDisplay) + '</span>'
                : '<span class="text-muted">—</span>') + '</td>'
            + '<td class="text-center small">' + (r.PremiumMinutes
                ? '<span class="badge bg-danger">' + esc(r.PremiumDisplay) + '</span>'
                : '<span class="text-muted">—</span>') + '</td>'
            + '<td class="text-center pe-3 fw-semibold">' + r.WeightedHours.toFixed(2) + ' h</td>'
            + '</tr>';
    }, { colspan: 8, empty: 'No overtime in this period.', label: 'employee' });
}

function exportSummary() {
    if (!sumData || !sumData.Rows.length) { notifyError('Nothing to export.'); return; }

    var header = ['Employee Code', 'Employee', 'Department', 'OT Days', 'Pending Minutes',
                  'Approved Minutes', 'Rejected Minutes', 'Premium Minutes', 'Weighted Hours'];

    var rows = sumData.Rows.map(function (r) {
        return [r.EmployeeCode, r.EmployeeName, r.Department, r.Days, r.PendingMinutes,
                r.ApprovedMinutes, r.RejectedMinutes, r.PremiumMinutes, r.WeightedHours];
    });

    // Same escaping as the register: leading = + - @ would otherwise be read as a formula.
    var cell = function (v) {
        var s = v == null ? '' : String(v);
        if (/^[=+\-@]/.test(s)) s = "'" + s;
        return '"' + s.replace(/"/g, '""') + '"';
    };

    var csv = [header].concat(rows).map(function (r) { return r.map(cell).join(','); }).join('\r\n');
    var blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8;' });

    var a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'overtime-summary-' + sumData.From.substring(0, 10) + '-to-' + sumData.To.substring(0, 10) + '.csv';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(a.href);
}

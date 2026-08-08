/* ── Pay Register ───────────────────────────────────────────────────────────────
   One row per employee, one column per component the run actually produced.

   The columns come from the data rather than a fixed list, so a code introduced this month
   appears without anybody editing a report — and a code nobody used leaves no empty column
   to scan past. That matters at 239 rows: every column you do not need is one more your eye
   has to cross to reach the one you do.
   ───────────────────────────────────────────────────────────────────────────── */

var prData = null;

$(function () {
    $.getJSON('/api/payroll-period', function (d) {
        var periods = d || [];

        if (!periods.length) {
            $('#prAlert').html('<div class="alert alert-warning py-2 small">'
                + 'No payroll month exists yet. Open one under '
                + '<a href="/Admin/PayrollPeriods">Payroll Months</a>.</div>');
            return;
        }

        $('#prPeriod').html(periods.map(function (p) {
            return '<option value="' + esc(p.Id) + '">' + esc(p.MonthDisplay)
                 + ' — ' + esc(p.StatusDisplay) + '</option>';
        }).join(''));

        prLoad();
    });

    $.getJSON('/api/departments', function (d) {
        (d || []).filter(function (x) { return x.IsActive; }).forEach(function (x) {
            $('#prDept').append('<option value="' + esc(x.Id) + '">' + esc(x.Name) + '</option>');
        });
    });
});

function money(v) {
    var n = parseFloat(v) || 0;
    return n === 0 ? '' : n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function prLoad() {
    var id = parseInt($('#prPeriod').val(), 10);
    if (!id) return;

    var dept = parseInt($('#prDept').val(), 10) || null;

    $('#prBody').html('<tr><td class="text-center py-4 text-muted">Loading…</td></tr>');

    $.getJSON('/api/payroll-report/register/' + id, dept ? { departmentId: dept } : {})
        .done(function (d) {
            prData = d;
            prRender();
        })
        .fail(function (xhr) {
            $('#prBody').html('<tr><td class="text-danger text-center py-4">'
                + esc(xhr.responseText || 'Failed to load.') + '</td></tr>');
        });
}

function prRender() {
    var d = prData;

    $('#prTitle').text('Pay Register — ' + d.MonthDisplay);
    $('#prCount').text(d.Rows.length + ' employee(s), ' + d.StatusDisplay);

    if (!d.Rows.length) {
        $('#prHead').html('');
        $('#prFoot').html('');
        $('#prTiles').html('');
        $('#prBody').html('<tr><td class="text-center py-4 text-muted">'
            + 'No payslips for this month. Run the payroll from '
            + '<a href="/Admin/PayrollPeriods">Payroll Months</a>.</td></tr>');
        return;
    }

    // Bank and cash are split out because they become two separate payment runs.
    $('#prTiles').html(
        tile('Gross', d.Totals.GrossPay, 'bg-light')
      + tile('Deductions', d.Totals.TotalDeductions, 'bg-light')
      + tile('Net pay', d.Totals.NetPay, 'bg-light', 'fw-bold')
      + tile('To bank (' + d.BankCount + ')', d.BankTotal, 'bg-light')
      + tile('In cash (' + d.CashCount + ')', d.CashTotal, d.CashCount ? 'bg-warning-subtle' : 'bg-light')
      + tile('Cost to company', d.Totals.CostToCompany, 'bg-light'));

    var head = '<th class="ps-2" style="position:sticky;left:0;background:#f8f9fa;">Code</th>'
             + '<th style="min-width:170px;">Employee</th>';

    d.EarningColumns.forEach(function (c) { head += '<th class="text-end">' + esc(c) + '</th>'; });
    head += '<th class="text-end table-secondary">GROSS</th>';
    head += '<th class="text-end">EPF 8%</th><th class="text-end">APIT</th>';
    d.DeductionColumns.forEach(function (c) { head += '<th class="text-end">' + esc(c) + '</th>'; });
    head += '<th class="text-end">B/F</th>'
          + '<th class="text-end table-secondary">DEDUCT</th>'
          + '<th class="text-end table-secondary">NET</th>'
          + '<th class="text-end">C/F</th>'
          + '<th class="text-center">Pay</th>';

    $('#prHead').html(head);

    $('#prBody').html(d.Rows.map(function (r) {
        var cells = '<td class="ps-2 fw-semibold" style="position:sticky;left:0;background:#fff;">'
                  + esc(r.EmployeeCode) + '</td>'
                  + '<td>' + esc(r.EmployeeName)
                  + '<div class="text-muted" style="font-size:.7rem;">' + esc(r.DepartmentName) + '</div></td>';

        d.EarningColumns.forEach(function (c) {
            cells += '<td class="text-end">' + money(r.Components[c]) + '</td>';
        });

        cells += '<td class="text-end table-secondary fw-semibold">' + money(r.GrossPay) + '</td>'
               + '<td class="text-end">' + money(r.EmployeeEpf) + '</td>'
               + '<td class="text-end">' + money(r.Apit) + '</td>';

        d.DeductionColumns.forEach(function (c) {
            cells += '<td class="text-end">' + money(r.Components[c]) + '</td>';
        });

        cells += '<td class="text-end">' + money(r.BroughtForward) + '</td>'
               + '<td class="text-end table-secondary">' + money(r.TotalDeductions) + '</td>'
               + '<td class="text-end table-secondary fw-semibold">' + money(r.NetPay) + '</td>'
               // Anything carried is called out in red: it means somebody was paid nothing,
               // and that should never pass a register unnoticed.
               + '<td class="text-end' + (r.CarriedForward > 0 ? ' text-danger fw-bold' : '') + '">'
               + money(r.CarriedForward) + '</td>'
               + '<td class="text-center">'
               + (r.IsBankTransfer
                    ? '<span class="badge bg-info">Bank</span>'
                    : '<span class="badge bg-warning text-dark">Cash</span>')
               + '</td>';

        return '<tr' + (r.Notes ? ' class="table-warning" title="' + esc(r.Notes) + '"' : '')
             + ' onclick="prOpen(' + r.PayslipId + ')" style="cursor:pointer;">' + cells + '</tr>';
    }).join(''));

    var t = d.Totals;
    var foot = '<td class="ps-2" style="position:sticky;left:0;background:#f8f9fa;"></td>'
             + '<td>' + esc(t.EmployeeName) + '</td>';
    d.EarningColumns.forEach(function (c) {
        foot += '<td class="text-end">' + money(t.Components[c]) + '</td>';
    });
    foot += '<td class="text-end">' + money(t.GrossPay) + '</td>'
          + '<td class="text-end">' + money(t.EmployeeEpf) + '</td>'
          + '<td class="text-end">' + money(t.Apit) + '</td>';
    d.DeductionColumns.forEach(function (c) {
        foot += '<td class="text-end">' + money(t.Components[c]) + '</td>';
    });
    foot += '<td class="text-end">' + money(t.BroughtForward) + '</td>'
          + '<td class="text-end">' + money(t.TotalDeductions) + '</td>'
          + '<td class="text-end">' + money(t.NetPay) + '</td>'
          + '<td class="text-end">' + money(t.CarriedForward) + '</td>'
          + '<td></td>';

    $('#prFoot').html(foot);
}

function tile(label, value, cls, extra) {
    return '<div class="col"><div class="card mb-0"><div class="card-body py-2 ' + cls + '">'
         + '<div class="text-muted" style="font-size:.7rem;">' + esc(label) + '</div>'
         + '<div class="' + (extra || '') + '">'
         + (parseFloat(value) || 0).toLocaleString(undefined,
               { minimumFractionDigits: 2, maximumFractionDigits: 2 })
         + '</div></div></div></div>';
}

function prOpen(payslipId) {
    window.location.href = '/Admin/Payslip?id=' + payslipId;
}

/* Exported from the data already loaded rather than re-fetched, so the file is exactly what
   was checked on screen — including the filter that was applied to it. */
function prExport() {
    if (!prData || !prData.Rows.length) { notifyError('Nothing to export.'); return; }

    var d = prData;
    var cols = ['Code', 'Employee', 'Department']
        .concat(d.EarningColumns)
        .concat(['Gross', 'EPF', 'APIT'])
        .concat(d.DeductionColumns)
        .concat(['Brought Forward', 'Total Deductions', 'Net Pay', 'Carried Forward', 'Paid By']);

    var q = function (v) { return '"' + String(v === null || v === undefined ? '' : v).replace(/"/g, '""') + '"'; };
    var n = function (v) { return (parseFloat(v) || 0).toFixed(2); };

    var lines = [cols.map(q).join(',')];

    d.Rows.forEach(function (r) {
        var row = [q(r.EmployeeCode), q(r.EmployeeName), q(r.DepartmentName)];
        d.EarningColumns.forEach(function (c) { row.push(n(r.Components[c])); });
        row.push(n(r.GrossPay), n(r.EmployeeEpf), n(r.Apit));
        d.DeductionColumns.forEach(function (c) { row.push(n(r.Components[c])); });
        row.push(n(r.BroughtForward), n(r.TotalDeductions), n(r.NetPay), n(r.CarriedForward),
                 q(r.IsBankTransfer ? 'Bank' : 'Cash'));
        lines.push(row.join(','));
    });

    var blob = new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8;' });
    var a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'pay-register-' + d.MonthDisplay.replace(/\s+/g, '-').toLowerCase() + '.csv';
    a.click();
    URL.revokeObjectURL(a.href);
}

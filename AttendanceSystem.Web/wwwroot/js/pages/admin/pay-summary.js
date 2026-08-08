/* ── Pay Summary ────────────────────────────────────────────────────────────────
   Department totals, and the journal they post to.
   ───────────────────────────────────────────────────────────────────────────── */

$(function () {
    $.getJSON('/api/payroll-period', function (d) {
        var periods = d || [];
        if (!periods.length) return;

        $('#psmPeriod').html(periods.map(function (p) {
            return '<option value="' + esc(p.Id) + '">' + esc(p.MonthDisplay) + '</option>';
        }).join(''));

        psmLoad();
    });
});

function psmMoney(v) {
    return (parseFloat(v) || 0).toLocaleString(undefined,
        { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function psmLoad() {
    var id = parseInt($('#psmPeriod').val(), 10);
    if (!id) return;

    $.getJSON('/api/payroll-report/summary/' + id, function (d) {
        $('#psmTitle').text('Pay Summary — ' + d.MonthDisplay);

        if (!d.Rows.length) {
            $('#psmBody').html('<tr><td colspan="10" class="text-center py-4 text-muted">'
                + 'No payslips for this month.</td></tr>');
            $('#psmFoot').html('');
            $('#psmJournal').html('<tr><td colspan="3" class="text-center py-3 text-muted">—</td></tr>');
            return;
        }

        $('#psmBody').html(d.Rows.map(function (r) {
            return '<tr>'
                 + '<td class="ps-3">' + esc(r.DepartmentName) + '</td>'
                 + '<td class="text-center">' + esc(r.Headcount) + '</td>'
                 + '<td class="text-end">' + psmMoney(r.GrossPay) + '</td>'
                 + '<td class="text-end">' + psmMoney(r.EmployeeEpf) + '</td>'
                 + '<td class="text-end">' + psmMoney(r.Apit) + '</td>'
                 + '<td class="text-end">' + psmMoney(r.TotalDeductions) + '</td>'
                 + '<td class="text-end fw-semibold">' + psmMoney(r.NetPay) + '</td>'
                 + '<td class="text-end text-muted">' + psmMoney(r.EmployerEpf) + '</td>'
                 + '<td class="text-end text-muted">' + psmMoney(r.EmployerEtf) + '</td>'
                 + '<td class="text-end pe-3">' + psmMoney(r.CostToCompany) + '</td>'
                 + '</tr>';
        }).join(''));

        var t = d.Totals;

        $('#psmFoot').html('<td class="ps-3">' + esc(t.DepartmentName) + '</td>'
            + '<td class="text-center">' + esc(t.Headcount) + '</td>'
            + '<td class="text-end">' + psmMoney(t.GrossPay) + '</td>'
            + '<td class="text-end">' + psmMoney(t.EmployeeEpf) + '</td>'
            + '<td class="text-end">' + psmMoney(t.Apit) + '</td>'
            + '<td class="text-end">' + psmMoney(t.TotalDeductions) + '</td>'
            + '<td class="text-end">' + psmMoney(t.NetPay) + '</td>'
            + '<td class="text-end">' + psmMoney(t.EmployerEpf) + '</td>'
            + '<td class="text-end">' + psmMoney(t.EmployerEtf) + '</td>'
            + '<td class="text-end pe-3">' + psmMoney(t.CostToCompany) + '</td>');

        psmJournal(t);
    });
}

/* Debits are what the company incurs; credits are what it now owes out. The two must agree,
   and the check is displayed rather than assumed — an out-of-balance journal means a figure
   is missing from one side, and finding that after it is posted is far more work. */
function psmJournal(t) {
    var otherDeductions = t.TotalDeductions - t.EmployeeEpf - t.Apit;

    var debit = t.GrossPay + t.EmployerEpf + t.EmployerEtf;
    var credit = t.NetPay + t.EmployeeEpf + t.EmployerEpf + t.EmployerEtf + t.Apit + otherDeductions;

    var row = function (name, dr, cr) {
        return '<tr><td class="ps-3">' + esc(name) + '</td>'
             + '<td class="text-end">' + (dr ? psmMoney(dr) : '') + '</td>'
             + '<td class="text-end pe-3">' + (cr ? psmMoney(cr) : '') + '</td></tr>';
    };

    var balanced = Math.abs(debit - credit) < 0.01;

    $('#psmJournal').html(
        row('Salaries and wages (expense)', t.GrossPay, 0)
      + row('EPF — employer contribution (expense)', t.EmployerEpf, 0)
      + row('ETF — employer contribution (expense)', t.EmployerEtf, 0)
      + row('Net salaries payable', 0, t.NetPay)
      + row('EPF payable (employee 8% + employer 12%)', 0, t.EmployeeEpf + t.EmployerEpf)
      + row('ETF payable', 0, t.EmployerEtf)
      + row('APIT payable', 0, t.Apit)
      + (otherDeductions !== 0 ? row('Other deductions payable', 0, otherDeductions) : '')
      + '<tr class="table-light fw-bold"><td class="ps-3">'
      + (balanced
            ? 'Balanced'
            : '<span class="text-danger">OUT OF BALANCE by ' + psmMoney(Math.abs(debit - credit)) + '</span>')
      + '</td><td class="text-end">' + psmMoney(debit) + '</td>'
      + '<td class="text-end pe-3">' + psmMoney(credit) + '</td></tr>');
}

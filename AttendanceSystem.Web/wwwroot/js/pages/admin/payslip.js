/* ── Payslips ───────────────────────────────────────────────────────────────────
   One employee's month, or every employee's, rendered from stored payslips.

   Nothing here calculates. Every figure was decided when the run happened, so a payslip
   reprinted next year shows what was actually paid rather than what today's rules would pay
   — which is the whole reason the run stores figures instead of a recipe.
   ───────────────────────────────────────────────────────────────────────────── */

var psRows = [], psCompany = {}, psLast = null;

/* Which layout the sheets render in. Remembered across visits, because a site prints the
   same format every month and re-choosing it thirty times a year is thirty chances to
   print the wrong one. */
function psFormat() { return localStorage.getItem('amsPayslipFormat') || 'compact'; }

function psSetFormat(f) {
    localStorage.setItem('amsPayslipFormat', f);
    $('#psFormat').val(f);
    if (psLast) psRender(psLast);
}

$(function () {
    $('#psFormat').val(psFormat());

    // Company name and address for the slip header. Read from settings rather than typed
    // into the template, so it is right on every document the system prints.
    $.getJSON('/api/settings', function (s) {
        psCompany = {
            Name: (s && (s.CompanyName || s.companyName)) || '',
            Address: (s && (s.Address || s.address)) || ''
        };
        if (psLast) psRender(psLast);
    });
});

$(function () {
    // An id in the URL means somebody arrived from the register. Show that one directly
    // rather than making them find it again in a dropdown.
    var direct = parseInt(new URLSearchParams(window.location.search).get('id'), 10);

    $.getJSON('/api/departments', function (d) {
        (d || []).filter(function (x) { return x.IsActive; }).forEach(function (x) {
            $('#psDept').append('<option value="' + esc(x.Id) + '">' + esc(x.Name) + '</option>');
        });
    });

    $.getJSON('/api/payroll-period', function (d) {
        var periods = d || [];

        if (!periods.length) {
            $('#psAlert').html('<div class="alert alert-warning py-2 small">'
                + 'No payroll month exists yet.</div>');
            return;
        }

        $('#psPeriod').html(periods.map(function (p) {
            return '<option value="' + esc(p.Id) + '">' + esc(p.MonthDisplay) + '</option>';
        }).join(''));

        if (direct) psOne(direct); else psLoadList();
    });
});

function n(v) {
    return (parseFloat(v) || 0).toLocaleString(undefined,
        { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

/* The one place sheets are put on the page, so switching format re-renders what is already
   loaded rather than re-fetching a few hundred payslips to change a layout. */
function psRender(list) {
    psLast = list;

    var compact = psFormat() === 'compact';

    $('#psSheets')
        .toggleClass('ps-sheet-wrap', compact)
        .html(list.map(compact ? compactSheet : sheet).join(''));

    $('#psPrintBtn').prop('disabled', false);

    if (compact) psFlagOverflow();
}

/* Marks any compact slip whose content runs past its fixed height.
 *
 * Measured after rendering rather than guessed from a line count, because what actually
 * fits depends on the font, the browser and how long the component names are. A slip that
 * overflows still prints in full — it just pushes the next one down the page — so this is a
 * warning to switch to the detailed format, not a failure. */
function psFlagOverflow() {
    var over = 0;

    $('#psSheets .ps-compact').each(function () {
        // scrollHeight exceeds clientHeight when the content is taller than the box.
        if (this.scrollHeight > this.clientHeight + 2) {
            $(this).addClass('ps-overflow')
                   .append('<div class="ps-overflow-note">Too many lines for a compact slip — '
                         + 'print this one as Detailed.</div>');
            over++;
        }
    });

    if (over) {
        $('#psAlert').html('<div class="alert alert-warning py-2 small mb-2">'
            + '<strong>' + over + '</strong> payslip(s) have more lines than fit a compact slip '
            + 'and are outlined in red. They will still print in full, but they push the page '
            + 'layout out — print those as <strong>Detailed</strong>.</div>');
    } else {
        $('#psAlert').empty();
    }
}

function line(name, amount, cls) {
    return '<div class="ps-line ' + (cls || '') + '"><span>' + esc(name) + '</span>'
         + '<span>' + n(amount) + '</span></div>';
}

/* The employee list comes from the register rather than the employee master: only people
   with a payslip this month can have one shown, and offering the rest would produce a
   "no payslip" error the user could have been spared. */
function psLoadList() {
    var id = parseInt($('#psPeriod').val(), 10);
    if (!id) return;

    var dept = parseInt($('#psDept').val(), 10) || null;

    $.getJSON('/api/payroll-report/register/' + id, dept ? { departmentId: dept } : {})
        .done(function (d) {
            psRows = d.Rows || [];

            if (!psRows.length) {
                $('#psEmployee').html('<option value="">— no payslips —</option>');
                $('#psHint').text('No payslips for this month. Run the payroll first.');
                $('#psPrintBtn').prop('disabled', true);
                $('#psSheets').html('<div class="card mx-auto" style="max-width:820px;">'
                    + '<div class="card-body text-muted small">Nothing to show.</div></div>');
                return;
            }

            $('#psEmployee').html('<option value="">— choose an employee —</option>'
                + psRows.map(function (r) {
                      return '<option value="' + esc(r.PayslipId) + '">'
                           + esc(r.EmployeeCode) + ' — ' + esc(r.EmployeeName) + '</option>';
                  }).join(''));

            $('#psHint').html(esc(psRows.length) + ' payslip(s) for ' + esc(d.MonthDisplay)
                + '. <strong>All</strong> loads every payslip for printing.');
        });
}

function psShowOne() {
    var id = parseInt($('#psEmployee').val(), 10);
    if (id) psOne(id);
}

function psOne(payslipId) {
    $('#psSheets').html('<div class="card mx-auto" style="max-width:820px;">'
        + '<div class="card-body text-muted small">Loading…</div></div>');

    $.getJSON('/api/payroll-report/payslip/' + payslipId)
        .done(function (p) {
            document.title = 'Payslip — ' + p.EmployeeName + ' — ' + p.MonthDisplay;
            psRender([p]);
        })
        .fail(function (xhr) {
            $('#psSheets').html('<div class="alert alert-danger py-2">'
                + esc(xhr.responseText || 'Could not load the payslip.') + '</div>');
        });
}

function psShowAll() {
    var id = parseInt($('#psPeriod').val(), 10);
    if (!id) return;

    var dept = parseInt($('#psDept').val(), 10) || null;

    // Says what is coming. Rendering a few hundred payslips takes a moment, and a screen
    // that simply sits there invites a second click and a second render.
    $('#psSheets').html('<div class="card mx-auto" style="max-width:820px;">'
        + '<div class="card-body text-muted small">'
        + '<i class="fa fa-spinner fa-spin me-2"></i>Building every payslip for this month…'
        + '</div></div>');

    $.getJSON('/api/payroll-report/payslips/' + id, dept ? { departmentId: dept } : {})
        .done(function (list) {
            if (!list || !list.length) {
                notifyError('No payslips for this month.');
                return;
            }

            psRender(list);
            $('#psHint').html('<strong>' + esc(list.length) + '</strong> payslips ready. '
                + 'Press Print.');
        })
        .fail(function (xhr) {
            $('#psSheets').html('<div class="alert alert-danger py-2">'
                + esc(xhr.responseText || 'Could not load the payslips.') + '</div>');
        });
}

/* ── Compact slip: 10cm x 14.85cm, two to an A4 page ──────────────────────────

   One note on the layout as specified. The mock puts "No Pay" among the deductions,
   but in this system no-pay REDUCES earnings rather than being deducted from them —
   the basic line already carries the reduced figure, and EPF and tax are charged on
   what was actually earned. Listing it again under deductions would subtract it twice
   and the slip would not foot.

   So the day count and its value sit in the ATTENDANCE block, where the mock already
   has "No Pay : 1", with the amount beside it. The employee still sees exactly what
   the absence cost; the arithmetic still adds up.
   ───────────────────────────────────────────────────────────────────────────── */
function compactSheet(p) {
    var row = function (label, amount, cls) {
        return '<div class="c-row ' + (cls || '') + '"><span>' + esc(label) + '</span>'
             + '<span>' + n(amount) + '</span></div>';
    };

    var field = function (label, value) {
        return '<div class="c-row"><span>' + esc(label) + '</span>'
             + '<span>' + esc(value) + '</span></div>';
    };

    var earnings = p.Earnings.map(function (l) { return row(l.Name, l.Amount); }).join('');

    var deductions =
        p.Deductions.filter(function (l) { return l.Amount > 0; })
                    .map(function (l) { return row(l.Name, l.Amount); }).join('')
      + (p.EmployeeEpf > 0 ? row('EPF 8%', p.EmployeeEpf) : '')
      + (p.Apit > 0 ? row('APIT', p.Apit) : '')
      + (p.BroughtForward > 0 ? row('Balance b/f', p.BroughtForward) : '');

    return '<div class="ps-compact">'

      + '<div class="c-band c-centre">'
      + '<div class="c-title">' + esc(psCompany.Name || 'COMPANY NAME') + '</div>'
      + (psCompany.Address ? '<div>' + esc(psCompany.Address) + '</div>' : '')
      + '</div>'

      + '<div class="c-band c-centre">'
      + '<div class="c-title">SALARY SLIP</div>'
      + '<div>' + esc(p.MonthDisplay.toUpperCase()) + '</div>'
      + '</div>'

      + '<div class="c-band">'
      + '<div class="c-head">Employee</div>'
      + field('No', p.EmployeeCode)
      + field('Name', p.EmployeeName)
      + field('Dept', p.DepartmentName || '-')
      + field('EPF No', p.EpfNumber || '-')
      + '</div>'

      + '<div class="c-band">'
      + '<div class="c-head">Attendance</div>'
      + field('Working Days', p.WorkingDays)
      + field('Paid Days', p.PresentDays + p.LeaveDays)
      + field('No Pay', p.NoPayDays > 0
            ? p.NoPayDays + '  (' + n(p.NoPayDeduction) + ')' : '0')
      + field('OT Hours', p.OvertimeHours)
      + '</div>'

      + '<div class="c-band">'
      + '<div class="c-row c-head"><span>Earnings</span><span>LKR</span></div>'
      + earnings
      + '<div class="c-rule"></div>'
      + row('GROSS', p.GrossPay, 'c-strong')
      + '</div>'

      + '<div class="c-band">'
      + '<div class="c-row c-head"><span>Deductions</span><span>LKR</span></div>'
      + (deductions || '<div class="c-row"><span>None</span><span></span></div>')
      + '<div class="c-rule"></div>'
      + row('TOTAL', p.TotalDeductions, 'c-strong')
      + '</div>'

      + '<div class="c-band c-net">'
      + '<div>NET SALARY</div>'
      + '<div class="c-amount">LKR ' + n(p.NetPay) + '</div>'
      + (p.CarriedForward > 0
            ? '<div style="font-weight:normal;">c/f ' + n(p.CarriedForward) + '</div>' : '')
      + '</div>'

      + '<div class="c-band">'
      + '<div class="c-head">Employer Contributions</div>'
      + row('EPF 12%', p.EmployerEpf)
      + row('ETF 3%', p.EmployerEtf)
      + '</div>'

      + '<div class="c-foot">'
      + '<div>Payment: ' + (p.IsBankTransfer ? 'Bank Transfer' : 'Cash') + '</div>'
      + '<div>Generated: ' + esc(psToday()) + '</div>'
      + '<div>Computer generated document</div>'
      + '</div>'

      + '</div>';
}

function psToday() {
    var d = new Date();
    var m = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
    return String(d.getDate()).padStart(2, '0') + '-' + m[d.getMonth()] + '-' + d.getFullYear();
}

function sheet(p) {
    var earnings = p.Earnings.map(function (l) { return line(l.Name, l.Amount); }).join('')
                 + line('Gross Pay', p.GrossPay, 'ps-total');

    // EPF and APIT are listed with the entered deductions rather than in a separate block:
    // the employee cares what came off, not which subsystem computed it.
    var deductions = p.Deductions
            .filter(function (l) { return l.Amount > 0; })
            .map(function (l) { return line(l.Name, l.Amount); }).join('')
        + (p.EmployeeEpf > 0 ? line('EPF — employee 8%', p.EmployeeEpf) : '')
        + (p.Apit > 0 ? line('APIT (PAYE)', p.Apit) : '')
        + (p.BroughtForward > 0 ? line('Balance brought forward', p.BroughtForward) : '')
        + line('Total Deductions', p.TotalDeductions, 'ps-total');

    // Shown, and explicitly labelled as not deducted. Employees ask what the employer pays,
    // and leaving it off invites the belief that it comes out of their salary.
    var employer = '<div class="mt-3 p-2 bg-light rounded">'
        + '<div class="small text-muted mb-1">Employer contributions — not deducted from your pay</div>'
        + line('EPF — employer 12%', p.EmployerEpf)
        + line('ETF — employer 3%', p.EmployerEtf)
        + '</div>';

    return '<div class="card mx-auto mb-3 ps-detailed" style="max-width:820px;"><div class="card-body">'

      + '<div class="d-flex justify-content-between border-bottom pb-2 mb-3">'
      + '<div><div class="h6 mb-0">Attendance Management System</div>'
      + '<div class="text-muted small">Pay Slip for ' + esc(p.MonthDisplay) + '</div></div>'
      + '<div class="text-end small">'
      + '<div class="fw-semibold">' + esc(p.EmployeeCode) + '</div>'
      + (p.EpfNumber ? '<div class="text-muted">EPF ' + esc(p.EpfNumber) + '</div>' : '')
      + '</div></div>'

      + '<div class="row g-2 mb-3 small">'
      + '<div class="col-6"><strong>' + esc(p.EmployeeName) + '</strong>'
      + '<div class="text-muted">' + esc(p.DesignationName || '—') + ', '
      + esc(p.DepartmentName || '—') + '</div></div>'
      + '<div class="col-6 text-end text-muted">'
      + 'Worked ' + esc(p.PresentDays) + ' / ' + esc(p.WorkingDays) + ' days'
      + (p.LeaveDays ? ' · leave ' + esc(p.LeaveDays) : '')
      + (p.NoPayDays ? ' · <span class="text-danger">no pay ' + esc(p.NoPayDays) + '</span>' : '')
      + (p.OvertimeHours ? ' · OT ' + esc(p.OvertimeHours) + ' hrs' : '')
      + '</div></div>'

      + '<div class="row g-4">'
      + '<div class="col-6"><div class="fw-semibold small text-uppercase text-muted mb-1">Earnings</div>'
      + earnings + '</div>'
      + '<div class="col-6"><div class="fw-semibold small text-uppercase text-muted mb-1">Deductions</div>'
      + deductions + '</div>'
      + '</div>'

      + '<div class="d-flex justify-content-between align-items-center mt-3 p-2 border rounded '
      + (p.NetPay > 0 ? 'bg-success-subtle' : 'bg-danger-subtle') + '">'
      + '<div><div class="fw-bold">NET PAY</div>'
      + '<div class="small text-muted">'
      + (p.IsBankTransfer
            ? esc([p.BankName, p.BankBranchName, p.AccountNumber].filter(Boolean).join(' · ')
                  || 'Bank transfer')
            : 'Paid in cash')
      + '</div></div>'
      + '<div class="h4 mb-0">' + n(p.NetPay) + '</div></div>'

      // Only shown when it happened. A permanent "carried forward: 0.00" line trains people
      // to ignore the one month it is not zero.
      + (p.CarriedForward > 0
            ? '<div class="alert alert-warning py-2 small mt-2 mb-0">'
              + 'Deductions exceeded pay. <strong>' + n(p.CarriedForward)
              + '</strong> is carried forward and will be recovered next month.</div>'
            : '')

      + employer

      + (p.Notes ? '<div class="alert alert-light border py-2 small mt-2 mb-0">'
                 + esc(p.Notes) + '</div>' : '')

      + '<div class="text-muted mt-3 pt-2 border-top" style="font-size:.7rem;">'
      + 'This is a computer-generated payslip and needs no signature.</div>'
      + '</div></div>';
}

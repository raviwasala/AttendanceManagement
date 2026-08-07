/* ── Payroll Setup: Branch Parameters ─────────────────────────────────────────

   Its own file because this tab is a form rather than a list, and payroll-setup.js
   is already six CRUD grids. */

/* Each figure rounds independently — one global rule would force EPF (kept to the cent
   for the return) and net pay (usually whole rupees) into the same treatment. */
var bpRoundFields = [
    { id: 'bpEpfRound',   key: 'EpfRounding',      label: 'EPF' },
    { id: 'bpEtfRound',   key: 'EtfRounding',      label: 'ETF' },
    { id: 'bpNoPayRound', key: 'NoPayRounding',    label: 'No Pay' },
    { id: 'bpTaxRound',   key: 'TaxRounding',      label: 'Tax' },
    { id: 'bpLoanRound',  key: 'LoanRounding',     label: 'Loan' },
    { id: 'bpOtRound',    key: 'OvertimeRounding', label: 'Overtime' }
];

$(function () { psLoadBranchList(); });

function psLoadBranchList() {
    $.getJSON('/api/branches', function (d) {
        var active = (d || []).filter(function (b) { return b.IsActive; });

        $('#bpBranch').html(active.map(function (b) {
            return '<option value="' + esc(b.Id) + '">' + esc(b.Name) + '</option>';
        }).join(''));

        // Bank branches are loaded before the first settings read, so the saved account can
        // be selected into a list that already has its options.
        $.getJSON('/api/payroll-setup/bank-branches', function (bb) {
            $('#bpBankBranch').html('<option value="">— none —</option>'
                + (bb || []).filter(function (x) { return x.IsActive; }).map(function (x) {
                      return '<option value="' + esc(x.Id) + '">'
                           + esc(x.BankName) + ' — ' + esc(x.Name) + '</option>';
                  }).join(''));

            if (active.length) psLoadBranchSettings();
        });
    });
}

function psLoadBranchSettings() {
    var id = parseInt($('#bpBranch').val(), 10);
    if (!id) return;

    $.getJSON('/api/payroll-setup/branch-settings/' + id, function (d) {
        // Said plainly, so defaults on screen are not mistaken for a configuration
        // somebody actually made.
        $('#bpStatus').html(d.IsNew
            ? '<div class="alert alert-warning py-2 small"><i class="feather icon-alert-triangle me-1"></i>'
              + esc(d.BranchName) + ' has no payroll parameters saved. These are defaults — press '
              + 'Save Parameters to adopt them.</div>'
            : '');

        $('#bpEpfReg').val(d.EpfEmployerNumber || '');
        $('#bpEtfReg').val(d.EtfEmployerNumber || '');
        $('#bpDCode').val(d.EpfDCode || '');
        $('#bpPaye').val(d.PayeRegistrationNo || '');
        $('#bpContact').val(d.EpfContactPerson || '');
        $('#bpPhone').val(d.EpfContactPhone || '');
        $('#bpNonCitizen').val(d.NonCitizenTaxYears);

        // Blank, not zero: empty means "the company rate", while 0 would mean
        // "contributes nothing" — a different instruction entirely.
        $('#bpEpfEmp').val(d.EmployeeEpfPercent === null ? '' : d.EmployeeEpfPercent);
        $('#bpEpfEr').val(d.EmployerEpfPercent === null ? '' : d.EmployerEpfPercent);
        $('#bpEtf').val(d.EmployerEtfPercent === null ? '' : d.EmployerEtfPercent);

        $('#bpRateHint').text('Company rates: EPF '
            + d.CompanyEmployeeEpfPercent.toFixed(2) + '% employee / '
            + d.CompanyEmployerEpfPercent.toFixed(2) + '% employer, ETF '
            + d.CompanyEmployerEtfPercent.toFixed(2) + '%.');

        $('#bpDays').val(d.DaysPerMonth);
        $('#bpHours').val(d.HoursPerDay);
        $('#bpGratPct').val(d.GratuityPercentOfBasic);
        $('#bpGratYears').val(d.GratuityQualifyingYears);

        $('#bpBankBranch').val(d.BankBranchId || '');
        $('#bpAccount').val(d.AccountNumber || '');

        $('#bpRoundPayable').prop('checked', d.RoundOffSalaryPayable);
        $('#bpRoundNearest').val(d.RoundNearest);
        $('#bpCfMinus').prop('checked', d.CarryForwardMinusSalary);
        $('#bpCfCoins').prop('checked', d.CarryForwardCoins);

        $('#bpRounding').html(bpRoundFields.map(function (f) {
            return '<div class="col-md-4 col-lg-2">'
                 + '<label class="form-label small">' + esc(f.label) + '</label>'
                 + '<select id="' + f.id + '" class="form-select form-select-sm">'
                 + '<option value="1"' + (d[f.key] === 1 ? ' selected' : '') + '>Decimal</option>'
                 + '<option value="2"' + (d[f.key] === 2 ? ' selected' : '') + '>Round off</option>'
                 + '<option value="3"' + (d[f.key] === 3 ? ' selected' : '') + '>Nearest 10</option>'
                 + '</select></div>';
        }).join(''));
    }).fail(function (xhr) {
        $('#bpStatus').html('<div class="alert alert-danger py-2 small">'
            + esc(xhr.responseText || 'Could not load the parameters.') + '</div>');
    });
}

function psSaveBranchSettings() {
    var id = parseInt($('#bpBranch').val(), 10);
    if (!id) { notifyError('Choose a branch first.'); return; }

    // Blank stays null so the company rate keeps applying.
    var num = function (sel) {
        var v = ($(sel).val() || '').trim();
        return v === '' ? null : parseFloat(v);
    };

    var dto = {
        BranchId: id,
        EpfEmployerNumber: $('#bpEpfReg').val().trim() || null,
        EtfEmployerNumber: $('#bpEtfReg').val().trim() || null,
        EpfDCode: $('#bpDCode').val().trim() || null,
        PayeRegistrationNo: $('#bpPaye').val().trim() || null,
        EpfContactPerson: $('#bpContact').val().trim() || null,
        EpfContactPhone: $('#bpPhone').val().trim() || null,
        NonCitizenTaxYears: parseInt($('#bpNonCitizen').val(), 10) || 0,

        EmployeeEpfPercent: num('#bpEpfEmp'),
        EmployerEpfPercent: num('#bpEpfEr'),
        EmployerEtfPercent: num('#bpEtf'),

        DaysPerMonth: parseInt($('#bpDays').val(), 10) || 30,
        HoursPerDay: parseFloat($('#bpHours').val()) || 8,
        GratuityPercentOfBasic: parseFloat($('#bpGratPct').val()) || 0,
        GratuityQualifyingYears: parseInt($('#bpGratYears').val(), 10) || 0,

        BankBranchId: parseInt($('#bpBankBranch').val(), 10) || null,
        AccountNumber: $('#bpAccount').val().trim() || null,

        RoundOffSalaryPayable: $('#bpRoundPayable').is(':checked'),
        RoundNearest: parseFloat($('#bpRoundNearest').val()) || 1,
        CarryForwardMinusSalary: $('#bpCfMinus').is(':checked'),
        CarryForwardCoins: $('#bpCfCoins').is(':checked')
    };

    bpRoundFields.forEach(function (f) {
        dto[f.key] = parseInt($('#' + f.id).val(), 10) || 1;
    });

    $.ajax({ url: '/api/payroll-setup/branch-settings', type: 'POST',
             contentType: 'application/json', data: JSON.stringify(dto) })
        .done(function () { psOk('Branch parameters saved.'); psLoadBranchSettings(); })
        .fail(function (xhr) { psErr(xhr); });
}

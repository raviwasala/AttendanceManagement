/* ── Staff Loans ── */

var lnLoans = [], lnEmployees = [], lnTypes = [], lnEditId = 0;

$(function () {
    $.when(
        $.getJSON('/api/employee-payroll/list'),
        $.getJSON('/api/payroll-setup/loan-types')
    ).done(function (empRes, typeRes) {
        lnEmployees = empRes[0] || [];
        lnTypes = (typeRes[0] || []).filter(function (t) { return t.IsActive; });
        lnLoad();
    });

    $('#lnDate').val(new Date().toISOString().substring(0, 10));
});

function lnOk(msg) {
    $('#lnAlert').html('<div class="alert alert-success alert-dismissible fade show py-2">' + esc(msg)
        + '<button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>');
}

function lnEmpOptions(selected, blank) {
    return '<option value="">' + (blank || '— choose —') + '</option>'
         + lnEmployees.map(function (e) {
               return '<option value="' + esc(e.EmployeeId) + '"'
                    + (String(e.EmployeeId) === String(selected) ? ' selected' : '') + '>'
                    + esc(e.EmployeeCode) + ' — ' + esc(e.EmployeeName) + '</option>';
           }).join('');
}

function lnLoad() {
    $.getJSON('/api/loans', function (rows) {
        lnLoans = rows || [];
        lnRenderSummary();
        lnFilter();
    }).fail(function (xhr) {
        $('#lnBody').html('<tr><td colspan="11" class="text-danger text-center py-4">'
            + esc(xhr.responseText || 'Failed to load.') + '</td></tr>');
    });
}

function lnRenderSummary() {
    var active = lnLoans.filter(function (l) { return isLoanActive(l); });
    var outstanding = active.reduce(function (a, l) { return a + l.Balance; }, 0);
    var monthly = active.reduce(function (a, l) { return a + l.MonthlyInstallment; }, 0);

    var tile = function (label, value, colour) {
        return '<div class="col-6 col-md-3"><div class="card stat-card ' + colour + '">'
             + '<div class="card-body stat-card-body py-2"><div class="stat-card-text">'
             + '<p class="stat-card-label mb-0">' + label + '</p>'
             + '<h3 class="stat-card-value" style="font-size:1.3rem;">' + value + '</h3>'
             + '</div></div></div></div>';
    };

    $('#lnSummary').html(
        tile('Active loans', active.length, 'bg-c-blue')
      + tile('Outstanding', outstanding.toFixed(2), 'bg-c-pink')
      + tile('Monthly recovery', monthly.toFixed(2), 'bg-c-green')
      + tile('Settled', lnLoans.filter(function (l) { return isLoanSettled(l); }).length, 'bg-c-grey'));
}

function lnFilter() {
    var q = ($('#lnSearch').val() || '').toLowerCase();
    var status = $('#lnStatus').val();

    lnRender(lnLoans.filter(function (l) {
        return (!q || (l.EmployeeName || '').toLowerCase().indexOf(q) >= 0
                   || (l.EmployeeCode || '').toLowerCase().indexOf(q) >= 0)
            && (status === '' || String(l.Status) === status);
    }));
}

function lnRender(rows) {
    amsPage('#lnBody', rows, function (l) {
        var statusBadge = { 1: 'warning text-dark', 2: 'success', 3: 'secondary', 4: 'info' }[l.Status];

        return '<tr>'
             + '<td class="ps-3"><span class="fw-semibold">' + esc(l.EmployeeCode) + '</span><br>'
             + '<span class="small text-muted">' + esc(l.EmployeeName) + '</span></td>'
             + '<td class="small">' + esc(l.LoanTypeName) + '<br>'
             + '<span class="text-muted">' + esc(l.InterestTypeDisplay)
             + (l.InterestRate > 0 ? ' ' + l.InterestRate.toFixed(2) + '%' : ' · interest free')
             + '</span></td>'
             + '<td class="small">' + new Date(l.LoanDate).toLocaleDateString() + '</td>'
             + '<td class="text-end">' + l.LoanAmount.toFixed(2) + '</td>'
             + '<td class="text-end text-muted">' + l.InterestAmount.toFixed(2) + '</td>'
             + '<td class="text-end">' + l.MonthlyInstallment.toFixed(2) + '</td>'
             + '<td class="text-end text-success">' + l.Recovered.toFixed(2) + '</td>'
             + '<td class="text-end fw-semibold">' + l.Balance.toFixed(2) + '</td>'
             // Guarantor count with a tooltip naming them — four names would not fit a column.
             + '<td class="text-center">' + (l.Guarantors.length
                    ? '<span class="badge bg-info" title="'
                      + esc(l.Guarantors.map(function (g) { return g.GuarantorName; }).join(', '))
                      + '">' + l.Guarantors.length + '</span>'
                    : '<span class="text-muted">—</span>') + '</td>'
             + '<td class="text-center"><span class="badge bg-' + statusBadge + '">'
             + esc(l.StatusDisplay) + '</span></td>'
             + '<td class="text-end pe-3">'
             + '<button class="btn btn-sm btn-outline-secondary me-1" onclick="lnHistory(' + l.Id + ')"'
             + ' title="History"><i class="fa fa-list"></i></button>'
             + (isLoanActive(l)
                ? '<button class="btn btn-sm btn-outline-success me-1" onclick="lnSettleModal(' + l.Id + ')"'
                  + ' title="Settle">Settle</button>'
                  + '<button class="btn btn-sm btn-outline-primary" onclick="lnLoanModal(' + l.Id + ')"'
                  + ' title="Edit"><i class="fa fa-pencil"></i></button>'
                : '')
             + '</td></tr>';
    }, { colspan: 11, empty: 'No loans match these filters.', label: 'loan' });
}

// ── Grant / edit ──────────────────────────────────────────────────────────────

function lnLoanModal(id) {
    lnEditId = id || 0;
    var l = lnLoans.filter(function (x) { return x.Id === id; })[0];

    if (!lnTypes.length) {
        notifyError('Add a loan type first, under Payroll Setup → Loan Types.');
        return;
    }

    $('#lnModalTitle').text(id ? 'Edit Loan' : 'Grant a Loan');

    $('#lnEmployee').html(lnEmpOptions(l ? l.EmployeeId : ''));
    $('#lnType').html(lnTypes.map(function (t) {
        return '<option value="' + esc(t.Id) + '" data-rate="' + esc(t.InterestRate) + '"'
             + ' data-type="' + esc(t.InterestType) + '"'
             + (l && l.LoanTypeId === t.Id ? ' selected' : '') + '>'
             + esc(t.Code) + ' — ' + esc(t.Description) + '</option>';
    }).join(''));

    $('#lnDate').val(l ? String(l.LoanDate).substring(0, 10) : new Date().toISOString().substring(0, 10));
    $('#lnRate').val(l ? l.InterestRate : '');
    $('#lnAmount').val(l ? l.LoanAmount : '');
    $('#lnMonths').val(l ? l.NumberOfInstallments : '');
    $('#lnThisMonth').prop('checked', l ? l.ReduceThisMonth : true);
    $('#lnAllowGrant').prop('checked', l ? l.AllowGuarantorsToGrantLoans : false);
    $('#lnNotes').val(l ? (l.Notes || '') : '');

    [1, 2, 3, 4].forEach(function (i) {
        var g = l && l.Guarantors[i - 1];
        $('#lnG' + i).html(lnEmpOptions(g ? g.GuarantorEmployeeId : '', '— none —'));
    });

    if (!l) lnTypeChanged(); else lnPreview();

    new bootstrap.Modal('#lnModal').show();
}

/* Selecting a type fills in its default rate. The rate stays editable, because the loan
   records what it was actually granted at rather than following the type. */
function lnTypeChanged() {
    var opt = $('#lnType').find('option:selected');
    if (!lnEditId) $('#lnRate').val(opt.attr('data-rate') || 0);
    lnPreview();
}

function lnPreview() {
    var amount = parseFloat($('#lnAmount').val()) || 0;
    var rate = parseFloat($('#lnRate').val()) || 0;
    var months = parseInt($('#lnMonths').val(), 10) || 0;
    var interestType = parseInt($('#lnType').find('option:selected').attr('data-type'), 10) || 1;

    if (amount <= 0 || months <= 0) {
        $('#lnPreviewBox').html('Enter an amount and a number of instalments to see the schedule.');
        return;
    }

    $.getJSON('/api/loans/preview', {
        amount: amount, rate: rate, months: months, interestType: interestType
    }, function (s) {
        $('#lnPreviewBox').html(
            '<div class="row text-center">'
          + '<div class="col"><div class="text-muted small">Interest</div>'
          + '<div class="fw-semibold">' + s.InterestAmount.toFixed(2) + '</div></div>'
          + '<div class="col"><div class="text-muted small">Total payable</div>'
          + '<div class="fw-semibold">' + s.TotalPayable.toFixed(2) + '</div></div>'
          + '<div class="col"><div class="text-muted small">Monthly instalment</div>'
          + '<div class="fw-semibold text-primary">' + s.MonthlyInstallment.toFixed(2) + '</div></div>'
          + '</div>'
          // Stated rather than hidden: rounding leaves the last payment uneven, and a
          // borrower who spots it should find it was intended.
          + (s.HasUnevenFinal
                ? '<div class="text-muted small mt-1 text-center">Final instalment '
                  + s.FinalInstallment.toFixed(2) + ' — it absorbs the rounding so the '
                  + 'payments sum exactly to the total.</div>'
                : ''));
    });
}

function lnSave() {
    var guarantors = [1, 2, 3, 4]
        .map(function (i) { return parseInt($('#lnG' + i).val(), 10) || 0; })
        .filter(function (v) { return v > 0; });

    var dto = {
        Id: lnEditId,
        EmployeeId: parseInt($('#lnEmployee').val(), 10),
        LoanTypeId: parseInt($('#lnType').val(), 10),
        LoanDate: $('#lnDate').val(),
        InterestRate: parseFloat($('#lnRate').val()) || 0,
        LoanAmount: parseFloat($('#lnAmount').val()) || 0,
        NumberOfInstallments: parseInt($('#lnMonths').val(), 10) || 0,
        ReduceThisMonth: $('#lnThisMonth').is(':checked'),
        AllowGuarantorsToGrantLoans: $('#lnAllowGrant').is(':checked'),
        Notes: $('#lnNotes').val().trim() || null,
        GuarantorEmployeeIds: guarantors
    };

    if (!dto.EmployeeId) { notifyError('Choose an employee.'); return; }
    if (dto.LoanAmount <= 0) { notifyError('Enter the loan amount.'); return; }
    if (dto.NumberOfInstallments <= 0) { notifyError('Enter the number of instalments.'); return; }

    // Caught here as well as on the server so the message arrives before the round trip.
    if (guarantors.indexOf(dto.EmployeeId) >= 0) {
        notifyError('An employee cannot be their own guarantor.'); return;
    }

    $.ajax({ url: '/api/loans', type: 'POST', contentType: 'application/json',
             data: JSON.stringify(dto) })
        .done(function () {
            bootstrap.Modal.getInstance('#lnModal').hide();
            lnOk(lnEditId ? 'Loan updated.' : 'Loan granted.');
            lnLoad();
        })
        .fail(function (xhr) { notifyError(xhr.responseText || 'Could not save the loan.'); });
}

// ── Settlement ────────────────────────────────────────────────────────────────

function lnSettleModal(id) {
    var l = lnLoans.filter(function (x) { return x.Id === id; })[0];
    if (!l) return;

    $('#lnSettleId').val(id);
    $('#lnSettleDate').val(new Date().toISOString().substring(0, 10));
    $('#lnSettleAmount').val(l.Balance.toFixed(2));
    $('#lnSettleMonths').val('');
    $('#lnSettleNotes').val('');

    $('#lnSettleStatus').html(
        '<div class="row small">'
      + '<div class="col-6"><span class="text-muted">Employee</span><br>'
      + '<span class="fw-semibold">' + esc(l.EmployeeCode) + ' ' + esc(l.EmployeeName) + '</span></div>'
      + '<div class="col-6"><span class="text-muted">Loan</span><br>' + esc(l.LoanTypeName) + '</div>'
      + '<div class="col-4 mt-2"><span class="text-muted">Total payable</span><br>'
      + l.TotalPayable.toFixed(2) + '</div>'
      + '<div class="col-4 mt-2"><span class="text-muted">Recovered</span><br>'
      + '<span class="text-success">' + l.Recovered.toFixed(2) + '</span></div>'
      + '<div class="col-4 mt-2"><span class="text-muted">Balance</span><br>'
      + '<span class="fw-semibold">' + l.Balance.toFixed(2) + '</span></div>'
      + '</div>');

    lnSettlePreview();
    new bootstrap.Modal('#lnSettleModal').show();
}

function lnSettlePreview() {
    var id = parseInt($('#lnSettleId').val(), 10);
    var l = lnLoans.filter(function (x) { return x.Id === id; })[0];
    if (!l) return;

    var paying = parseFloat($('#lnSettleAmount').val()) || 0;
    var months = parseInt($('#lnSettleMonths').val(), 10) || 0;
    var remaining = Math.round((l.Balance - paying) * 100) / 100;

    if (paying > l.Balance) {
        $('#lnSettlePreviewBox').html('<span class="text-danger">Only ' + l.Balance.toFixed(2)
            + ' is outstanding — paying more would leave the loan in credit.</span>');
        return;
    }

    $('#lnSettlePreviewBox').html(remaining <= 0
        ? '<span class="text-success"><strong>This clears the loan.</strong> '
          + 'It will be marked settled and no further instalments deducted.</span>'
        : 'Balance after this payment: <strong>' + remaining.toFixed(2) + '</strong>'
          + (months > 0
                ? ' over ' + months + ' instalment(s) of <strong>'
                  + (Math.round(remaining / months * 100) / 100).toFixed(2) + '</strong>.'
                : ' — the instalment stays at ' + l.MonthlyInstallment.toFixed(2) + '.'));
}

function lnSettle() {
    var dto = {
        EmployeeLoanId: parseInt($('#lnSettleId').val(), 10),
        SettlementDate: $('#lnSettleDate').val(),
        AmountPaying: parseFloat($('#lnSettleAmount').val()) || 0,
        NewNumberOfInstallments: parseInt($('#lnSettleMonths').val(), 10) || null,
        ReduceThisMonth: true,
        Notes: $('#lnSettleNotes').val().trim() || null
    };

    if (dto.AmountPaying <= 0) { notifyError('Enter the amount being paid.'); return; }

    $.ajax({ url: '/api/loans/settle', type: 'POST', contentType: 'application/json',
             data: JSON.stringify(dto) })
        .done(function () {
            bootstrap.Modal.getInstance('#lnSettleModal').hide();
            lnOk('Settlement recorded.');
            lnLoad();
        })
        .fail(function (xhr) { notifyError(xhr.responseText || 'Could not record the settlement.'); });
}

// ── History ───────────────────────────────────────────────────────────────────

function lnHistory(id) {
    $('#lnHistoryBody').html('<tr><td colspan="5" class="text-center py-3 text-muted">Loading…</td></tr>');
    new bootstrap.Modal('#lnHistoryModal').show();

    $.getJSON('/api/loans/' + id + '/transactions', function (rows) {
        $('#lnHistoryBody').html((rows && rows.length)
            ? rows.map(function (t) {
                  return '<tr>'
                       + '<td class="ps-3">' + new Date(t.TransactionDate).toLocaleDateString() + '</td>'
                       + '<td>' + esc(t.PeriodDisplay) + '</td>'
                       + '<td><span class="badge bg-secondary">' + esc(t.TypeDisplay) + '</span></td>'
                       + '<td class="text-end">' + t.Amount.toFixed(2) + '</td>'
                       + '<td class="pe-3 text-muted small">' + esc(t.Notes || '—') + '</td>'
                       + '</tr>';
              }).join('')
            : '<tr><td colspan="5" class="text-center py-4 text-muted">'
              + 'Nothing recovered yet. Instalments appear here once payroll runs.</td></tr>');
    });
}

/* ── Loan Settlement ── */

var lsLoans = [], lsCurrent = null;

$(function () {
    $('#lsDate').val(new Date().toISOString().substring(0, 10));

    // Only active loans — a settled or cancelled one has nothing left to settle, and
    // offering it would only produce a refusal.
    $.getJSON('/api/loans', { status: 1 }, function (rows) {
        lsLoans = rows || [];

        // Employees are derived from the loans rather than fetched separately: somebody with
        // no active loan has nothing to settle, so listing them would be a dead end.
        var seen = {};
        var options = lsLoans.filter(function (l) {
            if (seen[l.EmployeeId]) return false;
            seen[l.EmployeeId] = true;
            return true;
        }).map(function (l) {
            return '<option value="' + esc(l.EmployeeId) + '">'
                 + esc(l.EmployeeCode) + ' — ' + esc(l.EmployeeName) + '</option>';
        }).join('');

        $('#lsEmployee').html('<option value="">— choose an employee —</option>' + options);

        if (!lsLoans.length) {
            $('#lsEmpty').removeClass('alert-light').addClass('alert-warning')
                .text('There are no active loans to settle.');
        }
    });
});

function lsEmployeeChanged() {
    var employeeId = parseInt($('#lsEmployee').val(), 10);
    var mine = lsLoans.filter(function (l) { return l.EmployeeId === employeeId; });

    $('#lsLoan').html('<option value="">— choose a loan —</option>'
        + mine.map(function (l) {
              return '<option value="' + esc(l.Id) + '">'
                   + esc(l.LoanTypeName) + ' — granted '
                   + new Date(l.LoanDate).toLocaleDateString()
                   + ', balance ' + l.Balance.toFixed(2) + '</option>';
          }).join(''));

    // Picked automatically when there is only one — the extra click answers nothing.
    if (mine.length === 1) { $('#lsLoan').val(mine[0].Id); lsLoanChanged(); }
    else lsClear();
}

function lsClear() {
    lsCurrent = null;
    $('#lsPanels').addClass('d-none');
    $('#lsEmpty').removeClass('d-none');
}

function lsLoanChanged() {
    var id = parseInt($('#lsLoan').val(), 10);
    lsCurrent = lsLoans.filter(function (l) { return l.Id === id; })[0] || null;

    if (!lsCurrent) { lsClear(); return; }

    $('#lsEmpty').addClass('d-none');
    $('#lsPanels').removeClass('d-none');

    var l = lsCurrent;
    $('#lsCurDate').text(new Date(l.LoanDate).toLocaleDateString());
    $('#lsCurAmount').text(l.LoanAmount.toFixed(2));
    $('#lsCurInterest').text(l.InterestAmount.toFixed(2));
    $('#lsCurMonths').text(l.NumberOfInstallments);
    $('#lsCurInstalment').text(l.MonthlyInstallment.toFixed(2));
    $('#lsCurRecovered').text(l.Recovered.toFixed(2));
    $('#lsCurBalance').text(l.Balance.toFixed(2));
    $('#lsCurBalIns').text(l.RemainingInstallments);

    // Defaults to settling in full, which is the common case; anything less is typed over it.
    $('#lsAmount').val(l.Balance.toFixed(2));
    $('#lsMonths').val('');
    $('#lsNotes').val('');
    lsPreview();
}

function lsPreview() {
    if (!lsCurrent) return;

    var paying = parseFloat($('#lsAmount').val()) || 0;
    var months = parseInt($('#lsMonths').val(), 10) || 0;
    var remaining = Math.round((lsCurrent.Balance - paying) * 100) / 100;

    if (paying > lsCurrent.Balance) {
        $('#lsNewBalance').val('—');
        $('#lsNewInstalment').val('—');
        $('#lsOutcome').html('<span class="text-danger">Only '
            + lsCurrent.Balance.toFixed(2) + ' is outstanding — paying more would leave the '
            + 'loan in credit.</span>');
        $('#lsSaveBtn').prop('disabled', true);
        return;
    }

    $('#lsSaveBtn').prop('disabled', paying <= 0);
    $('#lsNewBalance').val(remaining.toFixed(2));

    if (remaining <= 0) {
        $('#lsNewInstalment').val('0.00');
        $('#lsOutcome').html('<span class="text-success"><strong>This clears the loan.</strong> '
            + 'It will be marked settled and no further instalments deducted.</span>');
        return;
    }

    var instalment = months > 0
        ? Math.round(remaining / months * 100) / 100
        : lsCurrent.MonthlyInstallment;

    $('#lsNewInstalment').val(instalment.toFixed(2));
    $('#lsOutcome').html('Part settlement. Balance of <strong>' + remaining.toFixed(2)
        + '</strong> continues at <strong>' + instalment.toFixed(2) + '</strong> per month'
        + (months > 0 ? ' over ' + months + ' instalment(s).'
                      : ' — the existing instalment is unchanged, so the loan simply ends sooner.'));
}

function lsSettle() {
    if (!lsCurrent) return;

    var dto = {
        EmployeeLoanId: lsCurrent.Id,
        SettlementDate: $('#lsDate').val(),
        AmountPaying: parseFloat($('#lsAmount').val()) || 0,
        NewNumberOfInstallments: parseInt($('#lsMonths').val(), 10) || null,
        ReduceThisMonth: $('#lsThisMonth').is(':checked'),
        Notes: $('#lsNotes').val().trim() || null
    };

    if (dto.AmountPaying <= 0) { notifyError('Enter the amount being paid.'); return; }

    var clears = dto.AmountPaying >= lsCurrent.Balance;

    notifyConfirm({
        title: clears ? 'Settle this loan in full?' : 'Record a part settlement?',
        text: clears
            ? 'The loan will be marked settled and no further instalments deducted.'
            : 'A payment of ' + dto.AmountPaying.toFixed(2) + ' will be recorded against this loan.',
        confirmText: 'Record settlement', icon: 'warning'
    }, function () {
        $.ajax({ url: '/api/loans/settle', type: 'POST', contentType: 'application/json',
                 data: JSON.stringify(dto) })
            .done(function () {
                $('#lsAlert').html('<div class="alert alert-success alert-dismissible fade show py-2">'
                    + 'Settlement recorded.'
                    + '<button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>');
                // Reloaded rather than patched: a fully settled loan drops out of the active
                // list entirely, and the pickers have to reflect that.
                location.reload();
            })
            .fail(function (xhr) { notifyError(xhr.responseText || 'Could not record the settlement.'); });
    });
}

/* ── Common Value Entry ───────────────────────────────────────────────────────

   Shares the component list with group-assign-component.js, which loads first.
   Kept separate because the two answer different questions and neither should have
   to be read to understand the other. */

$(function () {
    // Waits for the component list the other tab fetched, rather than requesting it twice.
    var wait = setInterval(function () {
        if (!gaComponents || !gaComponents.length) return;
        clearInterval(wait);
        cvBuildComponents();
    }, 100);

    // Gives up rather than polling forever if there are no components at all.
    setTimeout(function () { clearInterval(wait); }, 10000);
});

function cvBuildComponents() {
    var group = function (label, match) {
        var items = gaComponents.filter(match);
        if (!items.length) return '';
        return '<optgroup label="' + label + '">'
             + items.map(function (c) {
                   return '<option value="' + esc(c.Id) + '">'
                        + esc(c.Code) + ' — ' + esc(c.Name) + '</option>';
               }).join('') + '</optgroup>';
    };

    $('#cvComponent').html('<option value="">— choose a code —</option>'
        + group('Earnings', isEarning) + group('Deductions', isDeduction));
}

function cvSelected() {
    var id = parseInt($('#cvComponent').val(), 10);
    return gaComponents.filter(function (c) { return c.Id === id; })[0];
}

function cvScope() {
    return parseInt($('input[name="cvScope"]:checked').val(), 10) || 2;
}

/* Asks the server how many employees the chosen scope reaches, before anything is applied.
   "All active employees" and "those who already have it" can differ by two hundred people,
   and the radio labels alone do not make that visible. */
function cvChanged() {
    var c = cvSelected();

    if (!c) {
        $('#cvComponentHint').text('');
        $('#cvImpact').html('Choose a code to see how many employees this would reach.');
        return;
    }

    $('#cvComponentHint').html('<strong>' + esc(c.ComponentTypeDisplay) + '</strong>, default '
        + esc(c.ValueDisplay)
        + (isPctOfBasic(c)
            ? ' — this is a percentage of basic, so the amount below is a percentage.'
            : ''));

    $.getJSON('/api/employee-payroll/common-value/count',
        { componentId: c.Id, scope: cvScope() }, function (n) {
            var scope = cvScope();

            $('#cvImpact').html(n === 0
                ? '<span class="text-muted">No employee matches this scope — nothing would change.</span>'
                : 'This will set <strong>' + esc(c.Name) + '</strong> for <strong>' + n
                  + '</strong> employee(s)'
                  + (scope === 1
                        ? ', <span class="text-danger">including anyone who does not currently receive it</span>.'
                        : ' who already receive it.'));

            $('#cvApplyBtn').prop('disabled', n === 0);
        });
}

function cvApply() {
    var c = cvSelected();
    if (!c) { notifyError('Choose a code first.'); return; }

    var raw = ($('#cvAmount').val() || '').trim();
    if (raw === '') { notifyError('Enter the amount.'); return; }

    var dto = {
        SalaryComponentId: c.Id,
        Amount: parseFloat(raw),
        Scope: cvScope()
    };

    // The confirmation names the scope in words. Picking the wrong radio is the one mistake
    // this screen makes easy, and it would put an allowance on everybody's payslip.
    var scopeText = dto.Scope === 1
        ? 'every active employee, including those who do not currently receive it'
        : 'employees who already receive it';

    notifyConfirm({
        title: 'Replace the amount?',
        text: c.Name + ' will be set to ' + dto.Amount + ' for ' + scopeText
            + '. Existing values are overwritten.',
        confirmText: 'Replace', icon: 'warning'
    }, function () {
        $.ajax({ url: '/api/employee-payroll/common-value', type: 'POST',
                 contentType: 'application/json', data: JSON.stringify(dto) })
            .done(function (res) {
                $('#gaAlert').html('<div class="alert alert-success alert-dismissible fade show py-2">'
                    + esc(res.Summary)
                    + '<button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>');
                $('#cvAmount').val('');
                cvChanged();
            })
            .fail(function (xhr) { notifyError(xhr.responseText || 'Could not apply the value.'); });
    });
}

/* ── Item wise Transaction ──────────────────────────────────────────────────────
   One code, one month, an amount per employee.

   The grid holds every employee, not only those with a figure, because it is an entry
   sheet: the people with nothing yet are exactly who you are looking for. Filters hide
   rows without discarding their values, so narrowing to one department and saving cannot
   wipe the rest of the month.
   ───────────────────────────────────────────────────────────────────────────── */

var iwComponents = [], iwRows = [], iwLocked = null;

$(function () {
    $.getJSON('/api/departments', function (d) {
        (d || []).filter(function (x) { return x.IsActive; }).forEach(function (x) {
            $('#iwDept').append('<option value="' + esc(x.Id) + '">' + esc(x.Name) + '</option>');
        });
    });

    $.getJSON('/api/payroll-setup/components', function (d) {
        iwComponents = (d || []).filter(function (c) { return c.IsActive; });

        if (!iwComponents.length) {
            $('#iwComponent').html('<option value="">No items defined</option>');
            $('#iwItemHint').html('Add one under <a href="/Admin/PayrollSetup">Payroll Setup</a> first.');
            return;
        }

        var group = function (label, match) {
            var items = iwComponents.filter(match);
            if (!items.length) return '';
            return '<optgroup label="' + label + '">'
                 + items.map(function (c) {
                       return '<option value="' + esc(c.Id) + '">'
                            + esc(c.Code) + ' — ' + esc(c.Name) + '</option>';
                   }).join('') + '</optgroup>';
        };

        $('#iwComponent').html('<option value="">— choose an item —</option>'
            + group('Earnings', isEarning) + group('Deductions', isDeduction));
    });

    // Defaults to the open payroll month, not to today's calendar month. In early August
    // the claim sheet on the desk is July's.
    amsBindPayrollMonth('#iwMonth', '#iwMonthNote');

    // Enter walks down the Amount column; Tab still crosses to Remarks.
    amsEnterMovesDown('#iwBody');
});

function iwYearMonth() {
    var v = $('#iwMonth').val();
    return v ? parseInt(v.replace('-', ''), 10) : 0;
}

function iwMonthLabel(yyyymm) {
    var y = Math.floor(yyyymm / 100), m = yyyymm % 100;
    return new Date(y, m - 1, 1).toLocaleString(undefined, { month: 'long', year: 'numeric' });
}

function iwLoad() {
    var id = parseInt($('#iwComponent').val(), 10);
    var ym = iwYearMonth();

    if (!id || !ym) {
        iwRows = [];
        $('#iwBody').html('<tr><td colspan="6" class="text-center py-4 text-muted">'
            + 'Choose an item and a month.</td></tr>');
        $('#iwSaveBtn, #iwClearBtn').prop('disabled', true);
        $('#iwItemHint').text('Choose an item to begin.');
        $('#iwTotal').text('0.00');
        return;
    }

    $('#iwBody').html('<tr><td colspan="6" class="text-center py-4 text-muted">Loading…</td></tr>');

    $.getJSON('/api/monthly-transactions/item-wise', { componentId: id, yearMonth: ym })
        .done(function (g) {
            iwRows = g.Rows || [];
            iwLocked = g.LockedReason || null;

            var c = iwComponents.filter(function (x) { return x.Id === id; })[0];
            $('#iwItemHint').html('<strong>' + esc(g.ComponentTypeDisplay) + '</strong> — '
                + esc(g.ComponentName) + ', ' + esc(iwMonthLabel(g.YearMonth))
                + (c && isPctOfBasic(c)
                    ? '<br><span class="text-warning">This item is normally a percentage of basic. '
                      + 'Amounts entered here are money, not percentages.</span>'
                    : ''));

            // A closed month is shown, not hidden. Somebody looking for last month's incentive
            // needs to see it; they just cannot change it.
            if (iwLocked) {
                $('#iwAlert').html('<div class="alert alert-warning py-2 small mb-3">'
                    + '<i class="feather icon-lock me-1"></i>' + esc(iwLocked)
                    + ' Figures below are read-only.</div>');
            } else {
                $('#iwAlert').empty();
            }

            $('#iwSaveBtn').prop('disabled', !!iwLocked);
            $('#iwClearBtn').prop('disabled', !!iwLocked);

            iwFilter();
        })
        .fail(function (xhr) {
            $('#iwBody').html('<tr><td colspan="6" class="text-danger text-center py-4">'
                + esc(xhr.responseText || 'Failed to load.') + '</td></tr>');
        });
}

function iwFilter() {
    var term = ($('#iwSearch').val() || '').trim().toLowerCase();
    var dept = $('#iwDept option:selected').text();
    var deptId = $('#iwDept').val();
    var enteredOnly = $('#iwEnteredOnly').is(':checked');

    var shown = iwRows.filter(function (r) {
        if (term && (r.EmployeeCode + ' ' + r.EmployeeName).toLowerCase().indexOf(term) === -1) return false;
        if (deptId && r.DepartmentName !== dept) return false;
        if (enteredOnly && !(parseFloat(r.Amount) > 0)) return false;
        return true;
    });

    if (!shown.length) {
        $('#iwBody').html('<tr><td colspan="6" class="text-center py-4 text-muted">'
            + (iwRows.length ? 'No employee matches these filters.' : 'No employees.')
            + '</td></tr>');
    } else {
        var ro = iwLocked ? ' disabled' : '';

        $('#iwBody').html(shown.map(function (r) {
            return '<tr>'
                 + '<td class="ps-3 fw-semibold">' + esc(r.EmployeeCode) + '</td>'
                 + '<td>' + esc(r.EmployeeName) + '</td>'
                 + '<td class="small text-muted">' + esc(r.DepartmentName) + '</td>'
                 + '<td class="text-end small text-muted">'
                 + (r.StandingValue !== null && r.StandingValue !== undefined
                        ? parseFloat(r.StandingValue).toFixed(2)
                        : '—')
                 + '</td>'
                 + '<td class="text-end">'
                 + '<input type="number" step="0.01" min="0" class="form-control form-control-sm text-end"'
                 + ' value="' + (parseFloat(r.Amount) ? parseFloat(r.Amount).toFixed(2) : '') + '"'
                 + ' data-emp="' + esc(r.EmployeeId) + '" oninput="iwAmountChanged(this)"' + ro + '>'
                 + '</td>'
                 + '<td>'
                 + '<input type="text" maxlength="250" class="form-control form-control-sm"'
                 + ' value="' + esc(r.Remarks || '') + '"'
                 + ' data-emp-remark="' + esc(r.EmployeeId) + '" oninput="iwRemarkChanged(this)"' + ro + '>'
                 + '</td>'
                 + '</tr>';
        }).join(''));
    }

    $('#iwGridCount').text(shown.length + ' of ' + iwRows.length + ' shown');
    iwUpdateTotal();
}

/* Values live on the row objects, not in the inputs. A filter re-renders the table, and
   anything held only in the DOM would be lost the moment somebody typed in the search box. */
function iwAmountChanged(el) {
    var id = parseInt($(el).data('emp'), 10);
    var row = iwRows.filter(function (r) { return r.EmployeeId === id; })[0];
    if (row) row.Amount = parseFloat(el.value) || 0;
    iwUpdateTotal();
}

function iwRemarkChanged(el) {
    var id = parseInt($(el).data('emp-remark'), 10);
    var row = iwRows.filter(function (r) { return r.EmployeeId === id; })[0];
    if (row) row.Remarks = el.value;
}

function iwUpdateTotal() {
    var total = 0, n = 0;
    iwRows.forEach(function (r) {
        var v = parseFloat(r.Amount) || 0;
        if (v) { total += v; n++; }
    });
    $('#iwTotal').text(total.toFixed(2));
    $('#iwGridTitle').text(n ? n + ' employee(s) with an amount' : 'Employees');
}

function iwClearAll() {
    notifyConfirm({
        title: 'Clear every amount?',
        text: 'All amounts for this item and month are set to zero. Nothing is saved until '
            + 'you press Save, so this can still be abandoned.',
        confirmText: 'Clear', icon: 'warning'
    }, function () {
        iwRows.forEach(function (r) { r.Amount = 0; r.Remarks = null; });
        iwFilter();
    });
}

function iwSave() {
    var id = parseInt($('#iwComponent').val(), 10);
    var ym = iwYearMonth();
    if (!id || !ym) { notifyError('Choose an item and a month.'); return; }

    var dto = {
        SalaryComponentId: id,
        YearMonth: ym,
        // Every loaded row is sent, including the zeros — a zero is how the server is told
        // to remove a figure somebody has just cleared.
        Rows: iwRows.map(function (r) {
            return {
                EmployeeId: r.EmployeeId,
                Amount: parseFloat(r.Amount) || 0,
                Remarks: r.Remarks || null
            };
        })
    };

    var withAmount = dto.Rows.filter(function (r) { return r.Amount > 0; });
    var total = withAmount.reduce(function (s, r) { return s + r.Amount; }, 0);
    var c = iwComponents.filter(function (x) { return x.Id === id; })[0];

    // The confirmation states the total, because that is the number somebody can check
    // against the claim sheet in front of them — a count of rows cannot be checked at all.
    notifyConfirm({
        title: 'Save this month?',
        text: (c ? c.Name : 'This item') + ' for ' + iwMonthLabel(ym) + ': '
            + withAmount.length + ' employee(s), total ' + total.toFixed(2) + '. '
            + 'Employees left blank will have any existing figure removed.',
        confirmText: 'Save', icon: 'question'
    }, function () {
        $.ajax({ url: '/api/monthly-transactions/item-wise', type: 'POST',
                 contentType: 'application/json', data: JSON.stringify(dto) })
            .done(function (res) {
                $('#iwAlert').html('<div class="alert alert-success alert-dismissible fade show py-2">'
                    + esc(res.Summary)
                    + '<button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>');
                iwLoad();
            })
            .fail(function (xhr) { notifyError(xhr.responseText || 'Could not save.'); });
    });
}

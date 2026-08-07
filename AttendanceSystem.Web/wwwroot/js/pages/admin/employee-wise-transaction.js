/* ── Employee Wise Transactions ─────────────────────────────────────────────────
   One employee, one month, the codes that apply to them.

   The transpose of item-wise entry, over the same rows. Only codes with a figure are
   listed — this is a payslip working, not an entry sheet, and listing every code in the
   system against one person would bury the three that matter.
   ───────────────────────────────────────────────────────────────────────────── */

var ewComponents = [], ewRows = [], ewLocked = null;

$(function () {
    $.when(
        $.getJSON('/api/employee-payroll/list'),
        $.getJSON('/api/payroll-setup/components')
    ).done(function (empRes, compRes) {
        $('#ewEmployee').html('<option value="">— choose an employee —</option>'
            + (empRes[0] || []).map(function (e) {
                  return '<option value="' + esc(e.EmployeeId) + '">'
                       + esc(e.EmployeeCode) + ' — ' + esc(e.EmployeeName) + '</option>';
              }).join(''));

        ewComponents = (compRes[0] || []).filter(function (c) { return c.IsActive; });
    });

    var now = new Date();
    $('#ewMonth').val(now.getFullYear() + '-' + String(now.getMonth() + 1).padStart(2, '0'));
});

function ewYearMonth() {
    var v = $('#ewMonth').val();
    return v ? parseInt(v.replace('-', ''), 10) : 0;
}

function ewMonthLabel(yyyymm) {
    var y = Math.floor(yyyymm / 100), m = yyyymm % 100;
    return new Date(y, m - 1, 1).toLocaleString(undefined, { month: 'long', year: 'numeric' });
}

function ewLoad() {
    var id = parseInt($('#ewEmployee').val(), 10);
    var ym = ewYearMonth();

    if (!id || !ym) {
        ewRows = [];
        $('#ewBody').html('<tr><td colspan="6" class="text-center py-4 text-muted">'
            + 'Choose an employee and a month.</td></tr>');
        $('#ewAddBtn, #ewSaveBtn').prop('disabled', true);
        $('#ewEmpHint').text('Choose an employee.');
        ewTotals();
        return;
    }

    $('#ewBody').html('<tr><td colspan="6" class="text-center py-4 text-muted">Loading…</td></tr>');

    $.getJSON('/api/monthly-transactions/employee-wise', { employeeId: id, yearMonth: ym })
        .done(function (g) {
            ewRows = g.Rows || [];
            ewLocked = g.LockedReason || null;

            $('#ewEmpHint').html('<strong>' + esc(g.EmployeeName) + '</strong><br>'
                + esc(g.DepartmentName || '—') + ', ' + esc(ewMonthLabel(g.YearMonth)));

            if (ewLocked) {
                $('#ewAlert').html('<div class="alert alert-warning py-2 small mb-3">'
                    + '<i class="feather icon-lock me-1"></i>' + esc(ewLocked)
                    + ' Figures below are read-only.</div>');
            } else {
                $('#ewAlert').empty();
            }

            $('#ewAddBtn, #ewSaveBtn').prop('disabled', !!ewLocked);
            ewRender();
        })
        .fail(function (xhr) {
            $('#ewBody').html('<tr><td colspan="6" class="text-danger text-center py-4">'
                + esc(xhr.responseText || 'Failed to load.') + '</td></tr>');
        });
}

function ewRender() {
    if (!ewRows.length) {
        $('#ewBody').html('<tr><td colspan="6" class="text-center py-4 text-muted">'
            + 'Nothing for this employee this month. Use <strong>Add Line</strong> to enter one.'
            + '</td></tr>');
        ewTotals();
        return;
    }

    var ro = ewLocked ? ' disabled' : '';

    $('#ewBody').html(ewRows.map(function (r, i) {
        // Codes already used are removed from the other lines' pickers, so the same code
        // cannot be chosen twice — the server refuses it, but by then the typing is done.
        var used = ewRows.filter(function (x, j) { return j !== i; })
                         .map(function (x) { return x.SalaryComponentId; });

        var group = function (label, match) {
            var items = ewComponents.filter(function (c) {
                return match(c) && (c.Id === r.SalaryComponentId || used.indexOf(c.Id) === -1);
            });
            if (!items.length) return '';
            return '<optgroup label="' + label + '">'
                 + items.map(function (c) {
                       return '<option value="' + esc(c.Id) + '"'
                            + (c.Id === r.SalaryComponentId ? ' selected' : '') + '>'
                            + esc(c.Code) + ' — ' + esc(c.Name) + '</option>';
                   }).join('') + '</optgroup>';
        };

        var c = ewComponents.filter(function (x) { return x.Id === r.SalaryComponentId; })[0];

        return '<tr>'
             + '<td class="ps-3">'
             + '<select class="form-select form-select-sm" data-row="' + i + '"'
             + ' onchange="ewCodeChanged(this)"' + ro + '>'
             + '<option value="">— choose —</option>'
             + group('Earnings', isEarning) + group('Deductions', isDeduction)
             + '</select></td>'
             + '<td class="small">' + esc(r.Description || (c ? c.Name : ''))
             + (r.ComponentTypeDisplay
                    ? ' <span class="badge bg-' + (r.ComponentTypeDisplay === 'Earning' ? 'success' : 'danger')
                      + '">' + esc(r.ComponentTypeDisplay) + '</span>'
                    : '')
             + '</td>'
             + '<td class="text-end">'
             + '<input type="number" step="0.01" min="0" class="form-control form-control-sm text-end"'
             + ' value="' + (parseFloat(r.Amount) ? parseFloat(r.Amount).toFixed(2) : '') + '"'
             + ' data-row="' + i + '" oninput="ewFieldChanged(this,\'Amount\')"' + ro + '></td>'
             + '<td class="text-end">'
             + '<input type="number" step="0.01" min="0" class="form-control form-control-sm text-end"'
             + ' value="' + (r.Hours !== null && r.Hours !== undefined ? parseFloat(r.Hours) : '') + '"'
             + ' data-row="' + i + '" oninput="ewFieldChanged(this,\'Hours\')"' + ro + '></td>'
             + '<td><input type="text" maxlength="250" class="form-control form-control-sm"'
             + ' value="' + esc(r.Remarks || '') + '"'
             + ' data-row="' + i + '" oninput="ewFieldChanged(this,\'Remarks\')"' + ro + '></td>'
             + '<td class="text-center">'
             + '<button class="btn btn-sm btn-outline-danger" onclick="ewRemove(' + i + ')"'
             + (ewLocked ? ' disabled' : '') + '><i class="fa fa-trash"></i></button></td>'
             + '</tr>';
    }).join(''));

    if (window.amsInitSelects) window.amsInitSelects('#ewBody');
    ewTotals();
}

function ewCodeChanged(el) {
    var i = parseInt($(el).data('row'), 10);
    var id = parseInt(el.value, 10) || 0;
    var c = ewComponents.filter(function (x) { return x.Id === id; })[0];

    ewRows[i].SalaryComponentId = id;
    ewRows[i].Code = c ? c.Code : '';
    ewRows[i].Description = c ? c.Name : '';
    ewRows[i].ComponentTypeDisplay = c ? (isEarning(c) ? 'Earning' : 'Deduction') : '';

    ewRender();
}

/* Values are held on the row objects, not read back out of the inputs: adding or removing
   a line re-renders the table, and anything living only in the DOM would be lost. */
function ewFieldChanged(el, field) {
    var i = parseInt($(el).data('row'), 10);
    if (field === 'Remarks') {
        ewRows[i].Remarks = el.value;
    } else {
        ewRows[i][field] = el.value === '' ? null : parseFloat(el.value);
    }
    if (field === 'Amount') ewTotals();
}

function ewAddRow() {
    ewRows.push({
        Id: 0, SalaryComponentId: 0, Code: '', Description: '',
        ComponentTypeDisplay: '', Amount: 0, Hours: null, Remarks: null
    });
    ewRender();
}

function ewRemove(i) {
    ewRows.splice(i, 1);
    ewRender();
}

function ewTotals() {
    var earn = 0, ded = 0;
    ewRows.forEach(function (r) {
        var v = parseFloat(r.Amount) || 0;
        if (r.ComponentTypeDisplay === 'Deduction') ded += v; else earn += v;
    });
    $('#ewTotalEarn').text(earn.toFixed(2));
    $('#ewTotalDed').text(ded.toFixed(2));
}

function ewSave() {
    var id = parseInt($('#ewEmployee').val(), 10);
    var ym = ewYearMonth();
    if (!id || !ym) { notifyError('Choose an employee and a month.'); return; }

    if (ewRows.some(function (r) { return !r.SalaryComponentId; })) {
        notifyError('Every line needs a code. Remove the blank line or choose one.');
        return;
    }

    var dto = {
        EmployeeId: id,
        YearMonth: ym,
        Rows: ewRows.map(function (r) {
            return {
                SalaryComponentId: r.SalaryComponentId,
                Amount: parseFloat(r.Amount) || 0,
                Hours: (r.Hours === null || r.Hours === '' || isNaN(r.Hours)) ? null : parseFloat(r.Hours),
                Remarks: r.Remarks || null
            };
        })
    };

    var earn = 0, ded = 0;
    ewRows.forEach(function (r) {
        var v = parseFloat(r.Amount) || 0;
        if (r.ComponentTypeDisplay === 'Deduction') ded += v; else earn += v;
    });

    // Says plainly that this replaces the month, because the grid removing a line is how
    // a figure gets deleted — and that is not obvious from pressing Save.
    notifyConfirm({
        title: 'Save this month?',
        text: ewMonthLabel(ym) + ': earnings ' + earn.toFixed(2) + ', deductions ' + ded.toFixed(2)
            + '. This replaces the employee\'s whole month — any line removed from the grid '
            + 'is deleted.',
        confirmText: 'Save', icon: 'question'
    }, function () {
        $.ajax({ url: '/api/monthly-transactions/employee-wise', type: 'POST',
                 contentType: 'application/json', data: JSON.stringify(dto) })
            .done(function (res) {
                $('#ewAlert').html('<div class="alert alert-success alert-dismissible fade show py-2">'
                    + esc(res.Summary)
                    + '<button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>');
                ewLoad();
            })
            .fail(function (xhr) { notifyError(xhr.responseText || 'Could not save.'); });
    });
}

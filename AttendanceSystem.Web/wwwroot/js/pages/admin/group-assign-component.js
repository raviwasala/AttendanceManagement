/* ── Group Employees for Item ── */

var gaEmployees = [], gaComponents = [], gaSelected = {};

$(function () {
    $.getJSON('/api/departments', function (d) {
        (d || []).filter(function (x) { return x.IsActive; }).forEach(function (x) {
            $('#gaDept').append('<option value="' + esc(x.Id) + '">' + esc(x.Name) + '</option>');
        });
    });

    $.getJSON('/api/payroll-setup/grades', function (d) {
        (d || []).filter(function (g) { return g.IsActive; }).forEach(function (g) {
            $('#gaGrade').append('<option value="' + esc(g.Name) + '">' + esc(g.Name) + '</option>');
        });
    });

    $.getJSON('/api/payroll-setup/components', function (d) {
        gaComponents = (d || []).filter(function (c) { return c.IsActive; });

        if (!gaComponents.length) {
            $('#gaComponent').html('<option value="">No components defined</option>');
            $('#gaComponentHint').html('Add one under <a href="/Admin/PayrollSetup">Payroll Setup</a> first.');
            return;
        }

        // Grouped so an earning is never picked when a deduction was meant — they sit far
        // apart in consequence and close together in a flat list.
        var group = function (label, match) {
            var items = gaComponents.filter(match);
            if (!items.length) return '';
            return '<optgroup label="' + label + '">'
                 + items.map(function (c) {
                       return '<option value="' + esc(c.Id) + '">'
                            + esc(c.Code) + ' — ' + esc(c.Name) + '</option>';
                   }).join('') + '</optgroup>';
        };

        $('#gaComponent').html(group('Earnings', isEarning) + group('Deductions', isDeduction));
        gaComponentChanged();
    });

    gaLoad();
});

function gaComponentChanged() {
    var id = parseInt($('#gaComponent').val(), 10);
    var c = gaComponents.filter(function (x) { return x.Id === id; })[0];
    if (!c) { $('#gaComponentHint').text(''); return; }

    // Naming the default and the flags here means the consequence of applying is visible
    // before it is applied to two hundred people.
    var flags = [];
    if (c.IsEpfLiable) flags.push('EPF');
    if (c.IsApitLiable) flags.push('tax');
    if (c.IncludeInOtRate) flags.push('OT rate');

    $('#gaComponentHint').html(
        '<strong>' + esc(c.ComponentTypeDisplay) + '</strong>, default ' + esc(c.ValueDisplay)
        + (flags.length ? ' · counts toward ' + flags.join(', ') : ' · counts toward nothing')
        + (isPctOfBasic(c)
            ? '<br>This is a percentage of basic — the value below is a percentage, not an amount.'
            : ''));
}

function gaLoad() {
    // The payroll list already carries department and grade, so one call feeds the picker.
    $.getJSON('/api/employee-payroll/list', function (rows) {
        gaEmployees = rows || [];
        gaFilter();
    }).fail(function (xhr) {
        $('#gaBody').html('<tr><td colspan="5" class="text-danger text-center py-4">'
            + esc(xhr.responseText || 'Failed to load.') + '</td></tr>');
    });
}

function gaFilter() {
    var q = ($('#gaSearch').val() || '').toLowerCase();
    var deptName = $('#gaDept').val() ? $('#gaDept').find('option:selected').text() : '';
    var grade = $('#gaGrade').val();

    var shown = gaEmployees.filter(function (r) {
        return (!q || (r.EmployeeName || '').toLowerCase().indexOf(q) >= 0
                   || (r.EmployeeCode || '').toLowerCase().indexOf(q) >= 0)
            && (!deptName || r.Department === deptName)
            && (!grade || r.GradeName === grade);
    });

    $('#gaBody').html(shown.length
        ? shown.map(function (r) {
              return '<tr>'
                   + '<td class="ps-3"><input type="checkbox" class="form-check-input ga-pick"'
                   + ' data-id="' + esc(r.EmployeeId) + '"'
                   + (gaSelected[r.EmployeeId] ? ' checked' : '') + '></td>'
                   + '<td class="fw-semibold">' + esc(r.EmployeeCode) + '</td>'
                   + '<td>' + esc(r.EmployeeName) + '</td>'
                   + '<td class="text-muted small">' + esc(r.Department) + '</td>'
                   + '<td class="text-muted small">' + esc(r.GradeName || '—') + '</td>'
                   + '</tr>';
          }).join('')
        : '<tr><td colspan="5" class="text-center py-4 text-muted">No employees match these filters.</td></tr>');

    gaUpdateSelection();
}

/* Selection survives filtering — narrowing the list to one department, ticking those, then
   switching to another is the natural way to build a group, and losing the first set would
   make the screen useless for exactly that. */
$(document).on('change', '.ga-pick', function () {
    var id = parseInt($(this).attr('data-id'), 10);
    if ($(this).is(':checked')) gaSelected[id] = true;
    else delete gaSelected[id];
    gaUpdateSelection();
});

function gaSelectAll(select) {
    if (!select) {
        gaSelected = {};
    } else {
        $('.ga-pick').each(function () {
            gaSelected[parseInt($(this).attr('data-id'), 10)] = true;
        });
    }
    $('.ga-pick').prop('checked', function () {
        return !!gaSelected[parseInt($(this).attr('data-id'), 10)];
    });
    gaUpdateSelection();
}

function gaUpdateSelection() {
    var n = Object.keys(gaSelected).length;

    $('#gaSelectionNote').html(n
        ? '<strong>' + n + '</strong> employee(s) selected.'
          + '<div class="text-muted">Selection is kept while you change the filters.</div>'
        : 'No employees selected.');

    $('#gaApplyBtn').prop('disabled', n === 0 || !$('#gaComponent').val());
}

function gaApply() {
    var componentId = parseInt($('#gaComponent').val(), 10);
    var ids = Object.keys(gaSelected).map(Number);
    if (!componentId || !ids.length) return;

    var raw = ($('#gaValue').val() || '').trim();
    var value = raw === '' ? null : parseFloat(raw);

    var c = gaComponents.filter(function (x) { return x.Id === componentId; })[0];

    var text = value === null
        ? 'This clears any personal value for ' + ids.length + ' employee(s), returning them to '
          + 'the default for ' + c.Name + '.'
        : 'This sets ' + c.Name + ' to ' + value + ' for ' + ids.length + ' employee(s), '
          + 'replacing any value they already have.';

    notifyConfirm({
        title: 'Apply to ' + ids.length + ' employee(s)?',
        text: text, confirmText: 'Apply', icon: 'warning'
    }, function () {
        $.ajax({
            url: '/api/employee-payroll/bulk-component', type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ SalaryComponentId: componentId, EmployeeIds: ids, Value: value })
        }).done(function (res) {
            $('#gaAlert').html('<div class="alert alert-success alert-dismissible fade show py-2">'
                + esc(res.Summary)
                + '<button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>');
            // Cleared so the same set is not applied twice by accident.
            gaSelected = {};
            $('#gaValue').val('');
            gaFilter();
        }).fail(function (xhr) {
            notifyError(xhr.responseText || 'Could not apply the component.');
        });
    });
}

/* ── Salary Details ── */

var sdRows = [];

$(function () {
    sdLoad();
    $(document).on('change', '#sdEmployee', sdSelect);
});

function sdLoad() {
    // One call feeds both the picker and the override list — the payroll list already
    // carries the grade, the effective salary and whether it is overridden.
    $.getJSON('/api/employee-payroll/list', function (rows) {
        sdRows = rows || [];

        $('#sdEmployee').html('<option value="">— choose an employee —</option>'
            + sdRows.map(function (r) {
                  return '<option value="' + esc(r.EmployeeId) + '">'
                       + esc(r.EmployeeCode) + ' — ' + esc(r.EmployeeName) + '</option>';
              }).join(''));

        sdRenderOverrides();
    }).fail(function (xhr) {
        $('#sdAlert').html('<div class="alert alert-danger py-2">'
            + esc(xhr.responseText || 'Could not load employees.') + '</div>');
    });
}

function sdRenderOverrides() {
    var overridden = sdRows.filter(function (r) { return r.IsSalaryOverridden; });

    if (!overridden.length) {
        $('#sdOverrideBody').html('<tr><td colspan="4" class="text-center py-4 text-muted">'
            + 'Nobody has their own salary — everyone follows their grade.</td></tr>');
        return;
    }

    $('#sdOverrideBody').html(overridden.map(function (r) {
        return '<tr style="cursor:pointer" onclick="sdPick(' + esc(r.EmployeeId) + ')">'
             + '<td class="ps-3 fw-semibold">' + esc(r.EmployeeCode) + '</td>'
             + '<td>' + esc(r.EmployeeName) + '</td>'
             + '<td class="text-muted small">' + esc(r.GradeName || '— none —') + '</td>'
             + '<td class="text-end pe-3 fw-semibold">' + r.BasicSalary.toFixed(2) + '</td>'
             + '</tr>';
    }).join(''));
}

function sdPick(employeeId) {
    $('#sdEmployee').val(String(employeeId)).trigger('change');
}

/* Reads the employee's current position before anything is typed, so the box is filled with
   what they actually have and the note says where that figure came from. */
function sdSelect() {
    var id = parseInt($('#sdEmployee').val(), 10);

    if (!id) {
        $('#sdContext').addClass('d-none');
        $('#sdSalary').val('');
        $('#sdSaveBtn, #sdClearBtn').prop('disabled', true);
        return;
    }

    $.getJSON('/api/employee-payroll/' + id, function (d) {
        // Only the override goes in the box. A grade figure shown here would be saved back
        // as an override on the next Save, quietly detaching them from their grade.
        $('#sdSalary').val(d.BasicSalaryOverride === null ? '' : d.BasicSalaryOverride);

        var note;
        if (d.IsSalaryOverridden) {
            note = '<strong>Own salary:</strong> ' + d.BasicSalary.toFixed(2)
                 + (d.SalaryGradeName
                     ? ' — their grade (' + esc(d.SalaryGradeName) + ') would pay '
                       + d.GradeBasicSalary.toFixed(2) + '.'
                     : ' — no grade assigned.');
        } else if (d.SalaryGradeName) {
            note = '<strong>Follows grade ' + esc(d.SalaryGradeName) + ':</strong> '
                 + d.GradeBasicSalary.toFixed(2) + '. Typing a figure below overrides it.';
        } else {
            note = '<span class="text-danger">No grade and no salary set</span> — '
                 + 'this employee cannot be paid until one of them exists.';
        }

        $('#sdContext').removeClass('d-none').html(note);
        $('#sdSaveBtn').prop('disabled', false);
        $('#sdClearBtn').prop('disabled', !d.IsSalaryOverridden);
    }).fail(function (xhr) {
        notifyError(xhr.responseText || 'Could not load that employee.');
    });
}

function sdSave() {
    var id = parseInt($('#sdEmployee').val(), 10);
    if (!id) { notifyError('Choose an employee first.'); return; }

    var raw = ($('#sdSalary').val() || '').trim();

    sdPost(id, raw === '' ? null : parseFloat(raw),
        raw === '' ? 'Salary cleared — this employee now follows their grade.' : 'Salary saved.');
}

function sdClear() {
    var id = parseInt($('#sdEmployee').val(), 10);
    if (!id) return;

    notifyConfirm({
        title: 'Use the grade instead?',
        text: 'This employee will follow their grade, and will move whenever the grade changes.',
        confirmText: 'Use the grade', icon: 'warning'
    }, function () {
        sdPost(id, null, 'This employee now follows their grade.');
    });
}

function sdPost(employeeId, salary, message) {
    $.ajax({
        url: '/api/employee-payroll/salary', type: 'POST', contentType: 'application/json',
        data: JSON.stringify({ EmployeeId: employeeId, Salary: salary })
    }).done(function () {
        notifySuccess(message);
        // Reloaded rather than patched in memory: the override list and the note both
        // depend on what the server now holds.
        sdLoad();
        sdSelect();
    }).fail(function (xhr) {
        notifyError(xhr.responseText || 'Could not save the salary.');
    });
}

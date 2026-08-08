/* ── Salary Increments ──────────────────────────────────────────────────────────
   One employee, or a whole department or grade.

   Everything is previewed before anything is written. A raise is permanent and repeats
   every month, so "40 employees updated" after the fact is no use — the before-and-after
   has to be checkable while it can still be abandoned.
   ───────────────────────────────────────────────────────────────────────────── */

var siEmployees = [], siSelected = {}, siTarget = 1, siPreviewTimer = null;

$(function () {
    $('#siDate').val(new Date().toISOString().substring(0, 10));

    $.getJSON('/api/employee-payroll/list', function (d) {
        siEmployees = d || [];
        siRenderEmployees();
    });

    $.getJSON('/api/departments', function (d) {
        $('#siDept').html('<option value="">— choose a department —</option>'
            + (d || []).filter(function (x) { return x.IsActive; }).map(function (x) {
                  return '<option value="' + esc(x.Id) + '">' + esc(x.Name) + '</option>';
              }).join(''));
    });

    $.getJSON('/api/payroll-setup/grades', function (d) {
        $('#siGrade').html('<option value="">— choose a grade —</option>'
            + (d || []).filter(function (g) { return g.IsActive; }).map(function (g) {
                  return '<option value="' + esc(g.Id) + '">'
                       + esc(g.Code) + ' — ' + esc(g.Name)
                       + ' (' + parseFloat(g.BasicSalary).toFixed(2) + ')</option>';
              }).join(''));
    });

    siLoadHistory();
});

function siTargetChanged(t) {
    siTarget = t;
    siPreview();
}

function siRenderEmployees() {
    var term = ($('#siEmpSearch').val() || '').trim().toLowerCase();

    var shown = siEmployees.filter(function (e) {
        return !term || (e.EmployeeCode + ' ' + e.EmployeeName).toLowerCase().indexOf(term) !== -1;
    });

    $('#siEmpCount').text(shown.length + ' shown, ' + Object.keys(siSelected).length + ' selected');

    if (!shown.length) {
        $('#siEmpList').html('<div class="text-muted small">No employee matches.</div>');
        return;
    }

    $('#siEmpList').html(shown.map(function (e) {
        return '<div class="form-check">'
             + '<input class="form-check-input" type="checkbox" id="siE' + esc(e.EmployeeId) + '"'
             + ' value="' + esc(e.EmployeeId) + '"'
             + (siSelected[e.EmployeeId] ? ' checked' : '')
             + ' onchange="siToggle(this)">'
             + '<label class="form-check-label small" for="siE' + esc(e.EmployeeId) + '">'
             + esc(e.EmployeeCode) + ' — ' + esc(e.EmployeeName) + '</label></div>';
    }).join(''));
}

/* Selection is kept in an object, not read from the checkboxes: the list re-renders on every
   keystroke in the search box, and anything held only in the DOM would be lost as soon as
   somebody narrowed the list to find the next person. */
function siToggle(el) {
    var id = parseInt(el.value, 10);
    if (el.checked) siSelected[id] = true; else delete siSelected[id];

    $('#siEmpCount').text($('#siEmpList .form-check').length + ' shown, '
        + Object.keys(siSelected).length + ' selected');
    siPreview();
}

function siSelectAll(on) {
    var term = ($('#siEmpSearch').val() || '').trim().toLowerCase();

    siEmployees.filter(function (e) {
        return !term || (e.EmployeeCode + ' ' + e.EmployeeName).toLowerCase().indexOf(term) !== -1;
    }).forEach(function (e) {
        if (on) siSelected[e.EmployeeId] = true; else delete siSelected[e.EmployeeId];
    });

    siRenderEmployees();
    siPreview();
}

function siDto() {
    return {
        Target: siTarget,
        EmployeeIds: Object.keys(siSelected).map(Number),
        DepartmentId: parseInt($('#siDept').val(), 10) || null,
        SalaryGradeId: parseInt($('#siGrade').val(), 10) || null,
        Value: parseFloat($('#siValue').val()) || 0,
        Basis: parseInt($('#siBasis').val(), 10),
        EffectiveDate: $('#siDate').val(),
        Reason: $('#siReason').val() || null
    };
}

function siReady(dto) {
    if (!dto.Value || dto.Value <= 0) return false;
    if (siTarget === 1) return dto.EmployeeIds.length > 0;
    if (siTarget === 2) return !!dto.DepartmentId;
    return !!dto.SalaryGradeId;
}

/* Debounced because it fires on every keystroke in the amount box. Without it, typing
   "2500" is four round trips and the answers can arrive out of order. */
function siPreview() {
    clearTimeout(siPreviewTimer);
    siPreviewTimer = setTimeout(siPreviewNow, 250);
}

function siPreviewNow() {
    var dto = siDto();

    if (!siReady(dto)) {
        $('#siBody').html('<tr><td colspan="6" class="text-center py-4 text-muted">'
            + 'Choose people and an amount.</td></tr>');
        $('#siSummary').text('Choose people and an amount.');
        $('#siApplyBtn').prop('disabled', true);
        return;
    }

    $.ajax({ url: '/api/salary-increment/preview', type: 'POST',
             contentType: 'application/json', data: JSON.stringify(dto) })
        .done(function (p) {
            if (!p.Rows.length) {
                $('#siBody').html('<tr><td colspan="6" class="text-center py-4 text-muted">'
                    + 'Nobody matches that selection.</td></tr>');
                $('#siSummary').text('Nobody matches.');
                $('#siApplyBtn').prop('disabled', true);
                return;
            }

            $('#siBody').html(p.Rows.map(function (r) {
                if (r.Blocked) {
                    return '<tr class="table-warning">'
                         + '<td class="ps-3 fw-semibold">' + esc(r.EmployeeCode) + '</td>'
                         + '<td>' + esc(r.EmployeeName) + '</td>'
                         + '<td colspan="4" class="small text-danger pe-3">' + esc(r.Blocked) + '</td>'
                         + '</tr>';
                }

                return '<tr>'
                     + '<td class="ps-3 fw-semibold">' + esc(r.EmployeeCode) + '</td>'
                     + '<td>' + esc(r.EmployeeName) + '</td>'
                     + '<td class="small text-muted">' + esc(r.GradeName || '—')
                     // Flagged per row, because this is the surprising part: a graded
                     // employee stops following their grade the moment they are incremented.
                     + (r.FromGrade
                            ? ' <span class="badge bg-info" title="Currently paid the grade '
                              + 'figure. After this they will have a personal salary and will '
                              + 'no longer follow the grade.">leaves grade</span>'
                            : '')
                     + '</td>'
                     + '<td class="text-end">' + parseFloat(r.CurrentBasic).toFixed(2) + '</td>'
                     + '<td class="text-end text-success">+' + parseFloat(r.IncrementAmount).toFixed(2) + '</td>'
                     + '<td class="text-end fw-semibold pe-3">' + parseFloat(r.NewBasic).toFixed(2) + '</td>'
                     + '</tr>';
            }).join(''));

            $('#siSummary').html(esc(p.EligibleCount) + ' to increment'
                + (p.BlockedCount ? ', <span class="text-danger">' + esc(p.BlockedCount)
                                    + ' blocked</span>' : '')
                + ' · monthly cost <strong>+' + parseFloat(p.MonthlyCostIncrease).toFixed(2) + '</strong>');

            $('#siApplyBtn').prop('disabled', p.EligibleCount === 0);
        })
        .fail(function (xhr) {
            $('#siBody').html('<tr><td colspan="6" class="text-danger text-center py-4">'
                + esc(xhr.responseText || 'Could not work out the increment.') + '</td></tr>');
            $('#siApplyBtn').prop('disabled', true);
        });
}

function siApply() {
    var dto = siDto();
    if (!dto.EffectiveDate) { notifyError('Choose an effective date.'); return; }

    $.ajax({ url: '/api/salary-increment/preview', type: 'POST',
             contentType: 'application/json', data: JSON.stringify(dto) })
        .done(function (p) {
            // Says outright that no pay changes here. Somebody who pressed this expecting
            // the raise to be done would otherwise never look at the confirmation screen,
            // and the increment would sit pending for a month.
            notifyConfirm({
                title: 'Propose this increment?',
                text: p.EligibleCount + ' employee(s) would be raised, costing '
                    + parseFloat(p.MonthlyCostIncrease).toFixed(2) + ' more per month — '
                    + (p.MonthlyCostIncrease * 12).toFixed(2) + ' a year. '
                    + 'No salary changes yet: this goes to Increment Confirmation for approval.',
                confirmText: 'Propose', icon: 'question'
            }, function () {
                $.ajax({ url: '/api/salary-increment/propose', type: 'POST',
                         contentType: 'application/json', data: JSON.stringify(dto) })
                    .done(function (res) {
                        $('#siAlert').html('<div class="alert alert-success alert-dismissible '
                            + 'fade show py-2">' + esc(res.Summary)
                            + '<button type="button" class="btn-close" data-bs-dismiss="alert">'
                            + '</button></div>');
                        siSelected = {};
                        $('#siValue').val('');
                        $('#siReason').val('');
                        siRenderEmployees();
                        siPreviewNow();
                        siLoadHistory();
                    })
                    .fail(function (xhr) {
                        notifyError(xhr.responseText || 'Could not apply the increment.');
                    });
            });
        });
}

function siLoadHistory() {
    $.getJSON('/api/salary-increment/history', function (d) {
        if (!d || !d.length) {
            $('#siHistory').html('<tr><td colspan="6" class="text-center py-3 text-muted">'
                + 'No increment has been applied yet.</td></tr>');
            return;
        }

        $('#siHistory').html(d.map(function (i) {
            return '<tr>'
                 + '<td class="ps-3 small">' + esc(new Date(i.EffectiveDate).toLocaleDateString()) + '</td>'
                 + '<td class="small">' + esc(i.EmployeeCode) + ' — ' + esc(i.EmployeeName) + '</td>'
                 + '<td class="text-end small">' + parseFloat(i.PreviousBasic).toFixed(2) + '</td>'
                 + '<td class="text-end small fw-semibold">' + parseFloat(i.NewBasic).toFixed(2) + '</td>'
                 + '<td class="small">' + esc(i.BasisDisplay)
                 + (i.BatchId ? ' <span class="badge bg-light text-dark">batch</span>' : '') + '</td>'
                 + '<td class="small text-muted pe-3">' + esc(i.Reason || '—') + '</td>'
                 + '</tr>';
        }).join(''));
    });
}

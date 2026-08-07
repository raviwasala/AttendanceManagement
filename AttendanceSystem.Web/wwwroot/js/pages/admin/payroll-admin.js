/* ── Payroll Administration ── */

var paEmployees = [], paAdjustments = [], paNonEff = [];

$(function () {
    // The payroll list carries code, name and department — enough for every picker here.
    $.getJSON('/api/employee-payroll/list', function (rows) {
        paEmployees = rows || [];

        var opts = '<option value="">— choose an employee —</option>'
            + paEmployees.map(function (r) {
                  return '<option value="' + esc(r.EmployeeId) + '">'
                       + esc(r.EmployeeCode) + ' — ' + esc(r.EmployeeName) + '</option>';
              }).join('');

        $('#ccEmployee').html(opts);
    });

    paLoadAdjustments();
    paLoadNonEffective();
});

function paOk(msg) {
    $('#paAlert').html('<div class="alert alert-success alert-dismissible fade show py-2">' + esc(msg)
        + '<button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>');
}

function paEmployeeOptions(selected) {
    return '<option value="">— choose —</option>'
         + paEmployees.map(function (r) {
               return '<option value="' + esc(r.EmployeeId) + '"'
                    + (String(r.EmployeeId) === String(selected) ? ' selected' : '') + '>'
                    + esc(r.EmployeeCode) + ' — ' + esc(r.EmployeeName) + '</option>';
           }).join('');
}

function paShowModal(title, body, onSave) {
    $('#paModalTitle').text(title);
    $('#paModalBody').html(body);
    $('#paModalSave').off('click').on('click', onSave);
    new bootstrap.Modal('#paModal').show();
}

function paHide() { bootstrap.Modal.getInstance('#paModal').hide(); }

// ── EPF adjustments ───────────────────────────────────────────────────────────

function paLoadAdjustments() {
    $.getJSON('/api/employee-payroll/epf-adjustments', function (d) {
        paAdjustments = d || [];
        amsPage('#paAdjustBody', paAdjustments, function (a) {
            return '<tr>'
                 + '<td class="ps-3"><span class="fw-semibold">' + esc(a.EmployeeCode) + '</span> '
                 + esc(a.EmployeeName) + '</td>'
                 + '<td>' + esc(a.PeriodDisplay) + '</td>'
                 + '<td class="small">' + esc(a.TargetDisplay) + '</td>'
                 // Signed and coloured: recovering an over-contribution and collecting arrears
                 // look identical as bare numbers.
                 + '<td class="text-end fw-semibold ' + (a.Amount < 0 ? 'text-danger' : 'text-success') + '">'
                 + (a.Amount > 0 ? '+' : '') + a.Amount.toFixed(2) + '</td>'
                 + '<td class="text-muted small">' + esc(a.Reason) + '</td>'
                 + '<td class="text-center">' + (a.AffectsReturn
                        ? '<i class="feather icon-check text-success"></i>'
                        : '<span class="text-muted">—</span>') + '</td>'
                 + '<td class="text-center">' + (a.IsApplied
                        ? '<span class="badge bg-success">Applied</span>'
                        : '<span class="badge bg-warning text-dark">Pending</span>') + '</td>'
                 + '<td class="text-end pe-3">'
                 // An applied adjustment has changed a payslip, so it is read-only from here.
                 + (a.IsApplied ? '<span class="text-muted small">locked</span>'
                    : '<button class="btn btn-sm btn-outline-primary me-1" onclick="paAdjustModal(' + a.Id + ')">'
                      + '<i class="fa fa-pencil"></i></button>'
                      + '<button class="btn btn-sm btn-outline-danger" onclick="paAdjustDelete(' + a.Id + ')">'
                      + '<i class="fa fa-trash"></i></button>')
                 + '</td></tr>';
        }, { colspan: 8, empty: 'No adjustments recorded.', label: 'adjustment' });
    });
}

function paAdjustModal(id) {
    var a = paAdjustments.filter(function (x) { return x.Id === id; })[0];
    var now = new Date();

    var monthOpts = '';
    for (var m = 1; m <= 12; m++) {
        var sel = (a ? a.Month : now.getMonth() + 1) === m ? ' selected' : '';
        monthOpts += '<option value="' + m + '"' + sel + '>'
                   + new Date(2000, m - 1, 1).toLocaleString(undefined, { month: 'long' }) + '</option>';
    }

    var targets = [
        { v: 1, t: 'EPF — employee (changes net pay)' },
        { v: 2, t: 'EPF — employer (changes cost only)' },
        { v: 3, t: 'ETF — employer' }
    ];

    paShowModal(id ? 'Edit Adjustment' : 'Add Adjustment',
        '<div class="col-md-12 mb-2"><label class="form-label small">Employee <span class="text-danger">*</span></label>'
      + '<select id="adEmp" class="form-select">' + paEmployeeOptions(a ? a.EmployeeId : '') + '</select></div>'

      + '<div class="col-md-6 mb-2"><label class="form-label small">Month</label>'
      + '<select id="adMonth" class="form-select">' + monthOpts + '</select></div>'

      + '<div class="col-md-6 mb-2"><label class="form-label small">Year</label>'
      + '<input type="number" id="adYear" class="form-control" value="'
      + (a ? a.Year : now.getFullYear()) + '"></div>'

      + '<div class="col-md-6 mb-2"><label class="form-label small">Contribution</label>'
      + '<select id="adTarget" class="form-select">'
      + targets.map(function (t) {
            return '<option value="' + t.v + '"' + ((a ? a.Target : 1) === t.v ? ' selected' : '') + '>'
                 + t.t + '</option>';
        }).join('') + '</select></div>'

      + '<div class="col-md-6 mb-2"><label class="form-label small">Amount <span class="text-danger">*</span></label>'
      + '<input type="number" step="0.01" id="adAmount" class="form-control" value="'
      + (a ? a.Amount : '') + '">'
      + '<div class="form-text small">Negative recovers an over-contribution.</div></div>'

      + '<div class="col-12 mb-2"><label class="form-label small">Reason <span class="text-danger">*</span></label>'
      + '<input type="text" id="adReason" class="form-control" maxlength="300" value="'
      + esc(a ? a.Reason : '') + '">'
      + '<div class="form-text small">Printed on the supplementary return.</div></div>'

      + '<div class="col-12"><div class="form-check form-switch">'
      + '<input class="form-check-input" type="checkbox" id="adReturn"'
      + (!a || a.AffectsReturn ? ' checked' : '') + '>'
      + '<label class="form-check-label small" for="adReturn">Include in the statutory return</label></div>'
      + '<div class="form-text small">Off for a correction that never reached the fund.</div></div>',
        function () {
            var dto = {
                Id: id || 0,
                EmployeeId: parseInt($('#adEmp').val(), 10),
                Month: parseInt($('#adMonth').val(), 10),
                Year: parseInt($('#adYear').val(), 10),
                Target: parseInt($('#adTarget').val(), 10),
                Amount: parseFloat($('#adAmount').val()) || 0,
                Reason: $('#adReason').val().trim(),
                AffectsReturn: $('#adReturn').is(':checked')
            };
            if (!dto.EmployeeId) { notifyError('Choose an employee.'); return; }
            if (!dto.Amount) { notifyError('An adjustment of zero would do nothing.'); return; }
            if (!dto.Reason) { notifyError('A reason is required — it goes on the return.'); return; }

            $.ajax({ url: '/api/employee-payroll/epf-adjustments', type: 'POST',
                     contentType: 'application/json', data: JSON.stringify(dto) })
                .done(function () { paHide(); paOk('Adjustment saved.'); paLoadAdjustments(); })
                .fail(function (xhr) { notifyError(xhr.responseText || 'Could not save.'); });
        });
}

function paAdjustDelete(id) {
    notifyConfirm({ title: 'Delete this adjustment?', text: 'This cannot be undone.',
                    confirmText: 'Delete', icon: 'warning' }, function () {
        $.ajax({ url: '/api/employee-payroll/epf-adjustments/' + id, type: 'DELETE' })
            .done(function () { paOk('Adjustment deleted.'); paLoadAdjustments(); })
            .fail(function (xhr) { notifyError(xhr.responseText || 'Could not delete.'); });
    });
}

// ── Non-effective employees ───────────────────────────────────────────────────

function paLoadNonEffective() {
    $.getJSON('/api/employee-payroll/non-effective', function (d) {
        paNonEff = d || [];
        amsPage('#paNonEffBody', paNonEff, function (r) {
            var date = function (v) { return v ? new Date(v).toLocaleDateString() : '—'; };
            return '<tr>'
                 + '<td class="ps-3 fw-semibold">' + esc(r.EmployeeCode) + '</td>'
                 + '<td>' + esc(r.EmployeeName) + '</td>'
                 + '<td class="text-muted small">' + esc(r.Department) + '</td>'
                 + '<td><span class="badge bg-' + (r.CanRestore ? 'warning text-dark' : 'secondary') + '">'
                 + esc(r.Category) + '</span></td>'
                 + '<td class="text-muted small">' + esc(r.Reason) + '</td>'
                 + '<td class="small">' + date(r.FromDate) + '</td>'
                 + '<td class="small">' + date(r.ToDate) + '</td>'
                 + '<td class="text-end pe-3">'
                 + (r.CanRestore
                    ? '<button class="btn btn-sm btn-outline-success" onclick="paRestore(' + r.EmployeeId + ')">'
                      + 'Restore to payroll</button>'
                    : '<span class="text-muted small">not a suspension</span>')
                 + '</td></tr>';
        }, { colspan: 8, empty: 'Everyone is being paid.', label: 'employee' });
    });
}

function paSuspendModal() {
    var today = new Date().toISOString().substring(0, 10);

    paShowModal('Suspend from Payroll',
        '<div class="col-12 mb-2"><label class="form-label small">Employee <span class="text-danger">*</span></label>'
      + '<select id="suEmp" class="form-select">' + paEmployeeOptions('') + '</select></div>'
      + '<div class="col-md-6 mb-2"><label class="form-label small">From</label>'
      + '<input type="date" id="suFrom" class="form-control" value="' + today + '"></div>'
      + '<div class="col-md-6 mb-2"><label class="form-label small">Until (optional)</label>'
      + '<input type="date" id="suTo" class="form-control">'
      + '<div class="form-text small">Leave blank if the return date is unknown.</div></div>'
      + '<div class="col-12"><label class="form-label small">Reason <span class="text-danger">*</span></label>'
      + '<input type="text" id="suReason" class="form-control" maxlength="300"'
      + ' placeholder="e.g. unpaid leave, overseas posting"></div>',
        function () {
            var dto = {
                EmployeeId: parseInt($('#suEmp').val(), 10),
                Suspend: true,
                SuspendedFrom: $('#suFrom').val() || null,
                SuspendedTo: $('#suTo').val() || null,
                Reason: $('#suReason').val().trim()
            };
            if (!dto.EmployeeId) { notifyError('Choose an employee.'); return; }
            if (!dto.Reason) { notifyError('A reason is required.'); return; }

            $.ajax({ url: '/api/employee-payroll/suspend', type: 'POST',
                     contentType: 'application/json', data: JSON.stringify(dto) })
                .done(function () { paHide(); paOk('Employee suspended from payroll.'); paLoadNonEffective(); })
                .fail(function (xhr) { notifyError(xhr.responseText || 'Could not suspend.'); });
        });
}

function paRestore(employeeId) {
    notifyConfirm({
        title: 'Restore to payroll?',
        text: 'This employee will be included in payroll runs again.',
        confirmText: 'Restore', icon: 'warning'
    }, function () {
        $.ajax({ url: '/api/employee-payroll/suspend', type: 'POST', contentType: 'application/json',
                 data: JSON.stringify({ EmployeeId: employeeId, Suspend: false }) })
            .done(function () { paOk('Employee restored to payroll.'); paLoadNonEffective(); })
            .fail(function (xhr) { notifyError(xhr.responseText || 'Could not restore.'); });
    });
}

// ── Change employee code ──────────────────────────────────────────────────────

function paCodeSelected() {
    var id = parseInt($('#ccEmployee').val(), 10);
    var e = paEmployees.filter(function (x) { return x.EmployeeId === id; })[0];
    $('#ccCurrent').val(e ? e.EmployeeCode : '');
}

function paChangeCode() {
    var dto = {
        EmployeeId: parseInt($('#ccEmployee').val(), 10),
        NewCode: $('#ccNew').val().trim(),
        Reason: $('#ccReason').val().trim()
    };

    if (!dto.EmployeeId) { notifyError('Choose an employee.'); return; }
    if (!dto.NewCode) { notifyError('Enter the new code.'); return; }
    if (!dto.Reason) { notifyError('A reason is required — the change is recorded with it.'); return; }

    notifyConfirm({
        title: 'Change ' + $('#ccCurrent').val() + ' to ' + dto.NewCode + '?',
        text: 'Payslips and returns already issued keep the old code. The change is recorded '
            + 'so they can still be traced to this employee.',
        confirmText: 'Change the code', icon: 'warning'
    }, function () {
        $.ajax({ url: '/api/employee-payroll/change-code', type: 'POST',
                 contentType: 'application/json', data: JSON.stringify(dto) })
            .done(function () {
                paOk('Employee code changed to ' + dto.NewCode + '.');
                $('#ccNew').val(''); $('#ccReason').val('');
                // Reloaded so the pickers show the new code rather than the old one.
                location.reload();
            })
            .fail(function (xhr) { notifyError(xhr.responseText || 'Could not change the code.'); });
    });
}

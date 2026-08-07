/* ── Payroll Setup ── */

var psBanks = [], psDepartments = [];

$(function () {
    // Departments and banks feed the pickers in two of the modals, so they are loaded once
    // rather than on every modal open.
    $.getJSON('/api/departments', function (d) {
        psDepartments = (d || []).filter(function (x) { return x.IsActive; });
    });

    psLoadGrades();
    psLoadComponents();
    psLoadGroups();
    psLoadSubDepts();
    psLoadBanks();
    psLoadRates();
    psLoadApitTables();
    psLoadApit();
    psLoadLoans();
    psLoadThirdParties();
});

function psOk(msg) {
    $('#psAlert').html('<div class="alert alert-success alert-dismissible fade show py-2">' + esc(msg)
        + '<button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>');
}

function psErr(xhr, fallback) {
    notifyError((xhr && xhr.responseText) || fallback || 'Save failed.');
}

function psYesNo(v) {
    return v ? '<i class="feather icon-check text-success"></i>'
             : '<span class="text-muted">—</span>';
}

function psStatus(v) {
    return v ? '<span class="badge bg-success">Active</span>'
             : '<span class="badge bg-secondary">Inactive</span>';
}

function psActions(editFn, id, deleteFn) {
    return '<button class="btn btn-sm btn-outline-primary me-1" onclick="' + editFn + '(' + id + ')" title="Edit">'
         + '<i class="fa fa-pencil"></i></button>'
         + (deleteFn
            ? '<button class="btn btn-sm btn-outline-danger" onclick="' + deleteFn + '(' + id + ')" title="Delete">'
              + '<i class="fa fa-trash"></i></button>'
            : '');
}

/* Deletes are refused server-side while a row is in use, so the message shown is the
   server's — it names what is using it, which a generic "cannot delete" would not. */
function psDelete(url, label, reload) {
    notifyConfirm({
        title: 'Delete ' + label + '?',
        text: 'This cannot be undone.',
        confirmText: 'Delete', icon: 'warning'
    }, function () {
        $.ajax({ url: url, type: 'DELETE' })
            .done(function () { psOk(label + ' deleted.'); reload(); })
            .fail(function (xhr) { psErr(xhr, 'Could not delete.'); });
    });
}

// ── Modal plumbing ────────────────────────────────────────────────────────────

function psField(label, id, type, value, opts) {
    opts = opts || {};
    var col = opts.col || 6;
    var v = value === null || value === undefined ? '' : value;

    var input;
    if (type === 'select') {
        input = '<select id="' + id + '" class="form-select">'
              + (opts.options || []).map(function (o) {
                    return '<option value="' + esc(o.value) + '"'
                         + (String(o.value) === String(v) ? ' selected' : '') + '>'
                         + esc(o.text) + '</option>';
                }).join('') + '</select>';
    } else if (type === 'checkbox') {
        input = '<div class="form-check form-switch mt-2">'
              + '<input class="form-check-input" type="checkbox" id="' + id + '"'
              + (v ? ' checked' : '') + '>'
              + '<label class="form-check-label" for="' + id + '">' + esc(opts.checkLabel || label) + '</label></div>';
    } else {
        input = '<input type="' + type + '" id="' + id + '" class="form-control" value="' + esc(v) + '"'
              + (opts.step ? ' step="' + opts.step + '"' : '')
              + (opts.maxlength ? ' maxlength="' + opts.maxlength + '"' : '') + '>';
    }

    return '<div class="col-md-' + col + ' mb-2">'
         + (type === 'checkbox' ? '<label class="form-label small d-block">&nbsp;</label>'
                                : '<label class="form-label small">' + esc(label)
                                  + (opts.required ? ' <span class="text-danger">*</span>' : '') + '</label>')
         + input
         + (opts.help ? '<div class="form-text small">' + opts.help + '</div>' : '')
         + '</div>';
}

function psShowModal(title, bodyHtml, onSave) {
    $('#psModalTitle').text(title);
    $('#psModalBody').html(bodyHtml);
    $('#psModalSave').off('click').on('click', onSave);
    new bootstrap.Modal('#psModal').show();
}

function psHide() { bootstrap.Modal.getInstance('#psModal').hide(); }

function psPost(url, dto, label, reload) {
    $.ajax({ url: url, type: 'POST', contentType: 'application/json', data: JSON.stringify(dto) })
        .done(function () { psHide(); psOk(label + ' saved.'); reload(); })
        .fail(function (xhr) { psErr(xhr); });
}

// ── Grades ────────────────────────────────────────────────────────────────────

var psGrades = [];

function psLoadGrades() {
    $.getJSON('/api/payroll-setup/grades', function (d) {
        psGrades = d || [];
        amsPage('#psGradeBody', psGrades, function (g) {
            return '<tr>'
                 + '<td class="ps-3 fw-semibold">' + esc(g.Code) + '</td>'
                 + '<td>' + esc(g.Name) + '</td>'
                 + '<td class="text-end">' + g.BasicSalary.toFixed(2) + '</td>'
                 + '<td class="text-center"><span class="badge bg-secondary">' + esc(g.EmployeeCount) + '</span></td>'
                 + '<td class="text-center">' + psStatus(g.IsActive) + '</td>'
                 + '<td class="text-end pe-3">' + psActions('psGradeModal', g.Id, 'psGradeDelete') + '</td>'
                 + '</tr>';
        }, { colspan: 6, empty: 'No grades yet. Add one — every employee needs a grade for their basic salary.', label: 'grade' });
    }).fail(function () {
        $('#psGradeBody').html('<tr><td colspan="6" class="text-danger text-center py-3">Failed to load.</td></tr>');
    });
}

function psGradeModal(id) {
    var g = psGrades.find(function (x) { return x.Id === id; }) || { IsActive: true, BasicSalary: 0 };

    psShowModal(id ? 'Edit Grade' : 'Add Grade',
        psField('Code', 'gCode', 'text', g.Code, { required: true, maxlength: 20, col: 4 })
      + psField('Name', 'gName', 'text', g.Name, { required: true, maxlength: 100, col: 8 })
      + psField('Basic Salary', 'gBasic', 'number', g.BasicSalary, {
            required: true, step: '0.01', col: 6,
            help: 'Everyone on this grade receives this basic. Individual differences belong in an allowance.'
        })
      + psField('Active', 'gActive', 'checkbox', g.IsActive, { col: 6, checkLabel: 'Active' }),
        function () {
            var dto = {
                Id: id || 0,
                Code: $('#gCode').val().trim(),
                Name: $('#gName').val().trim(),
                BasicSalary: parseFloat($('#gBasic').val()) || 0,
                IsActive: $('#gActive').is(':checked')
            };
            if (!dto.Code || !dto.Name) { notifyError('Code and name are required.'); return; }
            psPost('/api/payroll-setup/grades', dto, 'Grade', psLoadGrades);
        });
}

function psGradeDelete(id) {
    psDelete('/api/payroll-setup/grades/' + id, 'grade', psLoadGrades);
}

// ── Components ────────────────────────────────────────────────────────────────

var psComponents = [];

function psLoadComponents() {
    $.getJSON('/api/payroll-setup/components', function (d) {
        psComponents = d || [];
        amsPage('#psComponentBody', psComponents, function (c) {
            return '<tr>'
                 + '<td class="ps-3 fw-semibold">' + esc(c.Code) + '</td>'
                 + '<td>' + esc(c.Name) + '</td>'
                 + '<td><span class="badge bg-' + (isEarning(c) ? 'success' : 'danger') + '">'
                 + esc(c.ComponentTypeDisplay) + '</span></td>'
                 + '<td class="text-end">' + esc(c.ValueDisplay) + '</td>'
                 + '<td class="text-center small">' + esc(c.RecurrenceDisplay) + '</td>'
                 + '<td class="text-center">' + psYesNo(c.IsEpfLiable) + '</td>'
                 + '<td class="text-center">' + psYesNo(c.IsApitLiable) + '</td>'
                 + '<td class="text-center">' + psYesNo(c.IncludeInOtRate) + '</td>'
                 + '<td class="text-center">' + psYesNo(c.IncludeInNoPay) + '</td>'
                 + '<td class="text-center">' + psStatus(c.IsActive) + '</td>'
                 + '<td class="text-end pe-3">' + psActions('psComponentModal', c.Id, 'psComponentDelete') + '</td>'
                 + '</tr>';
        }, { colspan: 11, empty: 'No allowances or deductions defined yet.', label: 'component' });
    }).fail(function () {
        $('#psComponentBody').html('<tr><td colspan="11" class="text-danger text-center py-3">Failed to load.</td></tr>');
    });
}

function psComponentModal(id) {
    var c = psComponents.find(function (x) { return x.Id === id; })
         || { ComponentType: 1, Recurrence: 1, IsApitLiable: true, IncludeInNoPay: true,
              IncludeInGrossPay: true, CalculationType: 1, DefaultValue: 0,
              IsActive: true, SortOrder: 0 };

    psShowModal(id ? 'Edit Component' : 'Add Component',
        psField('Code', 'cCode', 'text', c.Code, { required: true, maxlength: 20, col: 3 })
      + psField('Name', 'cName', 'text', c.Name, { required: true, maxlength: 100, col: 5 })
      + psField('Type', 'cType', 'select', enumNum(c.ComponentType, ['Earning','Deduction']), {
            col: 4, options: [{ value: 1, text: 'Earning' }, { value: 2, text: 'Deduction' }]
        })
      + psField('Recurrence', 'cRecur', 'select', enumNum(c.Recurrence, ['Monthly','OneOff']), {
            col: 4, options: [{ value: 1, text: 'Monthly — every month' },
                              { value: 2, text: 'One-off — a single month' }]
        })
      + psField('Calculation', 'cCalc', 'select', enumNum(c.CalculationType, ['FixedAmount','PercentOfBasic']), {
            col: 4, options: [{ value: 1, text: 'Fixed amount' }, { value: 2, text: '% of basic' }]
        })
      + psField('Rate / Amount', 'cValue', 'number', c.DefaultValue, { step: '0.01', col: 4 })

      + '<div class="col-12"><hr class="my-2"><div class="small text-muted mb-1">'
      + '<strong>What this component counts toward.</strong> Each is independent — an allowance '
      + 'can be taxable but outside EPF, or paid but excluded from the overtime rate.'
      + '</div></div>'

      + psField('EPF', 'cEpf', 'checkbox', c.IsEpfLiable, {
            col: 4, checkLabel: 'EPF / ETF calculation' })
      + psField('APIT', 'cApit', 'checkbox', c.IsApitLiable, {
            col: 4, checkLabel: 'Tax calculation' })
      + psField('OT', 'cOt', 'checkbox', c.IncludeInOtRate, {
            col: 4, checkLabel: 'Overtime rate base' })
      + psField('Gross', 'cGross', 'checkbox', c.IncludeInGrossPay, {
            col: 4, checkLabel: 'Gross pay' })
      + psField('No-pay', 'cNoPay', 'checkbox', c.IncludeInNoPay, {
            col: 4, checkLabel: 'No-pay calculation' })
      + psField('No-pay (allowance only)', 'cNoPayAllow', 'checkbox', c.IncludeInAllowanceOnlyNoPay, {
            col: 4, checkLabel: 'No-pay — allowances only' })
      + psField('Working days', 'cWorkDays', 'checkbox', c.BasedOnWorkingDays, {
            col: 4, checkLabel: 'Rate is per working day',
            help: 'Amount is rate × working days, rather than a monthly figure.' })
      + psField('Order', 'cOrder', 'number', c.SortOrder, { col: 4, help: 'Position on the payslip.' })
      + psField('Active', 'cActive', 'checkbox', c.IsActive, { col: 4, checkLabel: 'Active' }),
        function () {
            var dto = {
                Id: id || 0,
                Code: $('#cCode').val().trim(),
                Name: $('#cName').val().trim(),
                ComponentType: parseInt($('#cType').val(), 10),
                Recurrence: parseInt($('#cRecur').val(), 10),
                CalculationType: parseInt($('#cCalc').val(), 10),
                DefaultValue: parseFloat($('#cValue').val()) || 0,
                SortOrder: parseInt($('#cOrder').val(), 10) || 0,
                IsEpfLiable: $('#cEpf').is(':checked'),
                IsApitLiable: $('#cApit').is(':checked'),
                IncludeInOtRate: $('#cOt').is(':checked'),
                IncludeInGrossPay: $('#cGross').is(':checked'),
                IncludeInNoPay: $('#cNoPay').is(':checked'),
                IncludeInAllowanceOnlyNoPay: $('#cNoPayAllow').is(':checked'),
                BasedOnWorkingDays: $('#cWorkDays').is(':checked'),
                IsActive: $('#cActive').is(':checked')
            };
            if (!dto.Code || !dto.Name) { notifyError('Code and name are required.'); return; }
            psPost('/api/payroll-setup/components', dto, 'Component', psLoadComponents);
        });
}

function psComponentDelete(id) {
    psDelete('/api/payroll-setup/components/' + id, 'component', psLoadComponents);
}

// ── Groups ────────────────────────────────────────────────────────────────────

var psGroups = [];

function psLoadGroups() {
    $.getJSON('/api/payroll-setup/groups', function (d) {
        psGroups = d || [];
        amsPage('#psGroupBody', psGroups, function (g) {
            return '<tr>'
                 + '<td class="ps-3 fw-semibold">' + esc(g.Name) + '</td>'
                 + '<td class="text-muted small">' + esc(g.Description || '—') + '</td>'
                 + '<td class="text-center"><span class="badge bg-secondary">' + esc(g.EmployeeCount) + '</span></td>'
                 + '<td class="text-center">' + psStatus(g.IsActive) + '</td>'
                 + '<td class="text-end pe-3">' + psActions('psGroupModal', g.Id, 'psGroupDelete') + '</td>'
                 + '</tr>';
        }, { colspan: 5, empty: 'No salary groups defined.', label: 'group' });
    });
}

function psGroupModal(id) {
    var g = psGroups.find(function (x) { return x.Id === id; }) || { IsActive: true };

    psShowModal(id ? 'Edit Group' : 'Add Group',
        psField('Name', 'grName', 'text', g.Name, { required: true, maxlength: 100, col: 8 })
      + psField('Active', 'grActive', 'checkbox', g.IsActive, { col: 4, checkLabel: 'Active' })
      + psField('Description', 'grDesc', 'text', g.Description, { maxlength: 300, col: 12 }),
        function () {
            var dto = {
                Id: id || 0,
                Name: $('#grName').val().trim(),
                Description: $('#grDesc').val().trim() || null,
                IsActive: $('#grActive').is(':checked')
            };
            if (!dto.Name) { notifyError('Name is required.'); return; }
            psPost('/api/payroll-setup/groups', dto, 'Group', psLoadGroups);
        });
}

function psGroupDelete(id) { psDelete('/api/payroll-setup/groups/' + id, 'group', psLoadGroups); }

// ── Sub-departments ───────────────────────────────────────────────────────────

var psSubDepts = [];

function psLoadSubDepts() {
    $.getJSON('/api/payroll-setup/sub-departments', function (d) {
        psSubDepts = d || [];
        amsPage('#psSubDeptBody', psSubDepts, function (s) {
            return '<tr>'
                 + '<td class="ps-3">' + esc(s.DepartmentName) + '</td>'
                 + '<td class="fw-semibold">' + esc(s.Name) + '</td>'
                 + '<td class="text-center"><span class="badge bg-secondary">' + esc(s.EmployeeCount) + '</span></td>'
                 + '<td class="text-center">' + psStatus(s.IsActive) + '</td>'
                 + '<td class="text-end pe-3">' + psActions('psSubDeptModal', s.Id, 'psSubDeptDelete') + '</td>'
                 + '</tr>';
        }, { colspan: 5, empty: 'No sub-departments defined.', label: 'sub-department' });
    });
}

function psSubDeptModal(id) {
    var s = psSubDepts.find(function (x) { return x.Id === id; }) || { IsActive: true };

    psShowModal(id ? 'Edit Sub-department' : 'Add Sub-department',
        psField('Department', 'sdDept', 'select', s.DepartmentId, {
            required: true, col: 6,
            options: psDepartments.map(function (d) { return { value: d.Id, text: d.Name }; })
        })
      + psField('Sub-department', 'sdName', 'text', s.Name, { required: true, maxlength: 150, col: 6 })
      + psField('Active', 'sdActive', 'checkbox', s.IsActive, { col: 6, checkLabel: 'Active' }),
        function () {
            var dto = {
                Id: id || 0,
                DepartmentId: parseInt($('#sdDept').val(), 10),
                Name: $('#sdName').val().trim(),
                IsActive: $('#sdActive').is(':checked')
            };
            if (!dto.DepartmentId || !dto.Name) { notifyError('Department and name are required.'); return; }
            psPost('/api/payroll-setup/sub-departments', dto, 'Sub-department', psLoadSubDepts);
        });
}

function psSubDeptDelete(id) {
    psDelete('/api/payroll-setup/sub-departments/' + id, 'sub-department', psLoadSubDepts);
}

// ── Banks ─────────────────────────────────────────────────────────────────────

function psLoadBanks() {
    $.getJSON('/api/payroll-setup/banks', function (d) {
        psBanks = d || [];
        amsPage('#psBankBody', psBanks, function (b) {
            return '<tr>'
                 + '<td class="ps-3 fw-semibold">' + esc(b.Code) + '</td>'
                 + '<td>' + esc(b.Name) + '</td>'
                 + '<td class="text-center"><span class="badge bg-secondary">' + esc(b.BranchCount) + '</span></td>'
                 + '<td class="text-end pe-3">' + psActions('psBankModal', b.Id, 'psBankDelete') + '</td>'
                 + '</tr>';
        }, { colspan: 4, empty: 'No banks yet.', label: 'bank' });
    });
    psLoadBankBranches();
}

function psBankModal(id) {
    var b = psBanks.find(function (x) { return x.Id === id; }) || { IsActive: true };

    psShowModal(id ? 'Edit Bank' : 'Add Bank',
        psField('Bank Code', 'bkCode', 'text', b.Code, {
            required: true, maxlength: 20, col: 4,
            help: 'SLIPS code, e.g. 7010 for Bank of Ceylon.'
        })
      + psField('Name', 'bkName', 'text', b.Name, { required: true, maxlength: 150, col: 8 })
      + psField('Active', 'bkActive', 'checkbox', b.IsActive, { col: 6, checkLabel: 'Active' }),
        function () {
            var dto = {
                Id: id || 0,
                Code: $('#bkCode').val().trim(),
                Name: $('#bkName').val().trim(),
                IsActive: $('#bkActive').is(':checked')
            };
            if (!dto.Code || !dto.Name) { notifyError('Code and name are required.'); return; }
            psPost('/api/payroll-setup/banks', dto, 'Bank', psLoadBanks);
        });
}

function psBankDelete(id) { psDelete('/api/payroll-setup/banks/' + id, 'bank', psLoadBanks); }

var psBankBranches = [];

function psLoadBankBranches() {
    $.getJSON('/api/payroll-setup/bank-branches', function (d) {
        psBankBranches = d || [];
        amsPage('#psBankBranchBody', psBankBranches, function (b) {
            return '<tr>'
                 + '<td class="ps-3">' + esc(b.BankName) + '</td>'
                 + '<td class="fw-semibold">' + esc(b.Name) + '</td>'
                 + '<td><code>' + esc(b.FullCode) + '</code></td>'
                 + '<td class="text-end pe-3">' + psActions('psBankBranchModal', b.Id, 'psBankBranchDelete') + '</td>'
                 + '</tr>';
        }, { colspan: 4, empty: 'No bank branches yet.', label: 'branch' });
    });
}

function psBankBranchModal(id) {
    var b = psBankBranches.find(function (x) { return x.Id === id; }) || { IsActive: true };

    if (!psBanks.length) { notifyError('Add a bank first.'); return; }

    psShowModal(id ? 'Edit Bank Branch' : 'Add Bank Branch',
        psField('Bank', 'bbBank', 'select', b.BankId, {
            required: true, col: 6,
            options: psBanks.map(function (x) { return { value: x.Id, text: x.Name }; })
        })
      + psField('Branch Code', 'bbCode', 'text', b.Code, { required: true, maxlength: 20, col: 6 })
      + psField('Branch Name', 'bbName', 'text', b.Name, { required: true, maxlength: 150, col: 8 })
      + psField('Active', 'bbActive', 'checkbox', b.IsActive, { col: 4, checkLabel: 'Active' }),
        function () {
            var dto = {
                Id: id || 0,
                BankId: parseInt($('#bbBank').val(), 10),
                Code: $('#bbCode').val().trim(),
                Name: $('#bbName').val().trim(),
                IsActive: $('#bbActive').is(':checked')
            };
            if (!dto.BankId || !dto.Code || !dto.Name) { notifyError('Bank, code and name are required.'); return; }
            psPost('/api/payroll-setup/bank-branches', dto, 'Bank branch', psLoadBankBranches);
        });
}

function psBankBranchDelete(id) {
    psDelete('/api/payroll-setup/bank-branches/' + id, 'bank branch', psLoadBankBranches);
}

// ── Statutory rates ───────────────────────────────────────────────────────────

var psRates = [];

function psLoadRates() {
    $.getJSON('/api/payroll-setup/rates', function (d) {
        psRates = d || [];
        amsPage('#psRateBody', psRates, function (r) {
            return '<tr' + (r.IsCurrent ? ' class="table-success"' : '') + '>'
                 + '<td class="ps-3">' + new Date(r.EffectiveFrom).toLocaleDateString()
                 + (r.IsCurrent ? ' <span class="badge bg-success ms-1">In force</span>' : '') + '</td>'
                 + '<td class="text-end">' + r.EmployeeEpfPercent.toFixed(2) + '%</td>'
                 + '<td class="text-end">' + r.EmployerEpfPercent.toFixed(2) + '%</td>'
                 + '<td class="text-end">' + r.EmployerEtfPercent.toFixed(2) + '%</td>'
                 + '<td class="text-muted small">' + esc(r.Notes || '—') + '</td>'
                 + '<td class="text-end pe-3">' + psActions('psRateModal', r.Id) + '</td>'
                 + '</tr>';
        }, { colspan: 6, empty: 'No rates configured.', label: 'rate' });
    });
}

function psRateModal(id) {
    var r = psRates.find(function (x) { return x.Id === id; })
         || { EmployeeEpfPercent: 8, EmployerEpfPercent: 12, EmployerEtfPercent: 3,
              EffectiveFrom: new Date().toISOString() };

    psShowModal(id ? 'Edit Rate' : 'Add Rate',
        psField('Effective From', 'rFrom', 'date', String(r.EffectiveFrom).substring(0, 10), {
            required: true, col: 6,
            help: 'Payslips before this date keep the previous rates.'
        })
      + psField('EPF — Employee %', 'rEmpEpf', 'number', r.EmployeeEpfPercent, { step: '0.01', col: 4 })
      + psField('EPF — Employer %', 'rErEpf', 'number', r.EmployerEpfPercent, { step: '0.01', col: 4 })
      + psField('ETF — Employer %', 'rEtf', 'number', r.EmployerEtfPercent, { step: '0.01', col: 4 })
      + psField('Notes', 'rNotes', 'text', r.Notes, { maxlength: 300, col: 12 }),
        function () {
            var dto = {
                Id: id || 0,
                EffectiveFrom: $('#rFrom').val(),
                EmployeeEpfPercent: parseFloat($('#rEmpEpf').val()) || 0,
                EmployerEpfPercent: parseFloat($('#rErEpf').val()) || 0,
                EmployerEtfPercent: parseFloat($('#rEtf').val()) || 0,
                Notes: $('#rNotes').val().trim() || null
            };
            if (!dto.EffectiveFrom) { notifyError('An effective date is required.'); return; }
            psPost('/api/payroll-setup/rates', dto, 'Rate', psLoadRates);
        });
}

// ── APIT ──────────────────────────────────────────────────────────────────────

var psApit = [];

var psApitTables = [];

function psLoadApitTables() {
    $.getJSON('/api/payroll-setup/apit-tables', function (d) {
        psApitTables = d || [];
        amsPage('#psApitTableBody', psApitTables, function (t) {
            return '<tr>'
                 + '<td class="ps-3 fw-semibold">' + esc(t.Code) + '</td>'
                 + '<td>' + esc(t.Name) + '</td>'
                 + '<td class="text-muted small">' + esc(t.Description || '—') + '</td>'
                 // A table with no bands taxes nobody, so an empty one is called out.
                 + '<td class="text-center">' + (t.BandCount
                        ? '<span class="badge bg-secondary">' + esc(t.BandCount) + '</span>'
                        : '<span class="text-danger small">none</span>') + '</td>'
                 + '<td class="text-center">' + (t.IsDefault
                        ? '<span class="badge bg-success">Default</span>' : '—') + '</td>'
                 + '<td class="text-center">' + psStatus(t.IsActive) + '</td>'
                 + '<td class="text-end pe-3">' + psActions('psApitTableModal', t.Id) + '</td>'
                 + '</tr>';
        }, { colspan: 7, empty: 'No tax tables configured.', label: 'table' });
    });
}

function psApitTableModal(id) {
    var t = psApitTables.find(function (x) { return x.Id === id; }) || { IsActive: true };

    psShowModal(id ? 'Edit Tax Table' : 'Add Tax Table',
        psField('Code', 'atCode', 'text', t.Code, { required: true, maxlength: 20, col: 4 })
      + psField('Name', 'atName', 'text', t.Name, { required: true, maxlength: 100, col: 8 })
      + psField('Description', 'atDesc', 'text', t.Description, { maxlength: 300, col: 12 })
      + psField('Default', 'atDefault', 'checkbox', t.IsDefault, {
            col: 6, checkLabel: 'Use for employees with no table assigned',
            help: 'Setting this clears the flag on any other table — only one can be the default.'
        })
      + psField('Active', 'atActive', 'checkbox', t.IsActive, { col: 6, checkLabel: 'Active' }),
        function () {
            var dto = {
                Id: id || 0,
                Code: $('#atCode').val().trim(),
                Name: $('#atName').val().trim(),
                Description: $('#atDesc').val().trim() || null,
                IsDefault: $('#atDefault').is(':checked'),
                IsActive: $('#atActive').is(':checked')
            };
            if (!dto.Code || !dto.Name) { notifyError('Code and name are required.'); return; }
            psPost('/api/payroll-setup/apit-tables', dto, 'Tax table', function () {
                psLoadApitTables(); psLoadApit();
            });
        });
}

function psLoadApit() {
    $.getJSON('/api/payroll-setup/apit', function (d) {
        psApit = d || [];
        amsPage('#psApitBody', psApit, function (b) {
            return '<tr>'
                 + '<td class="ps-3"><span class="badge bg-info">' + esc(b.TaxTableName) + '</span></td>'
                 + '<td>' + new Date(b.EffectiveFrom).toLocaleDateString() + '</td>'
                 + '<td>' + esc(b.RangeDisplay) + '</td>'
                 + '<td class="text-end">' + b.Rate.toFixed(2) + '%</td>'
                 + '<td class="text-end">' + b.Relief.toFixed(2) + '</td>'
                 + '<td class="text-end pe-3">' + psActions('psApitModal', b.Id, 'psApitDelete') + '</td>'
                 + '</tr>';
        }, { colspan: 6, empty: 'No APIT bands configured.', label: 'band' });
    });
}

function psApitModal(id) {
    var b = psApit.find(function (x) { return x.Id === id; })
         || { EffectiveFrom: new Date().toISOString(), FromAmount: 0, Rate: 0, Relief: 0, SortOrder: 0 };

    if (!psApitTables.length) {
        notifyError('Add a tax table first — a band has to belong to one.');
        return;
    }

    // Defaults to the default table, which is the one most bands will belong to.
    var selectedTable = b.ApitTaxTableId
        || (psApitTables.filter(function (t) { return t.IsDefault; })[0] || psApitTables[0]).Id;

    psShowModal(id ? 'Edit Band' : 'Add Band',
        psField('Tax Table', 'aTable', 'select', selectedTable, {
            required: true, col: 12,
            options: psApitTables.map(function (t) {
                return { value: t.Id, text: t.Name + (t.IsDefault ? ' (default)' : '') };
            })
        })
      + psField('Effective From', 'aFrom', 'date', String(b.EffectiveFrom).substring(0, 10),
            { required: true, col: 6,
              help: 'Bands before this date keep applying to earlier months.' })
      + psField('Order', 'aOrder', 'number', b.SortOrder, { col: 6 })
      + psField('From (monthly)', 'aFromAmt', 'number', b.FromAmount, { step: '0.01', col: 6 })
      + psField('To (blank = no upper limit)', 'aToAmt', 'number', b.ToAmount, { step: '0.01', col: 6 })
      + psField('Rate %', 'aRate', 'number', b.Rate, { step: '0.01', col: 6 })
      + psField('Relief', 'aRelief', 'number', b.Relief, {
            step: '0.01', col: 6,
            help: 'Subtracted after the rate — the constant from the published table.'
        }),
        function () {
            var to = $('#aToAmt').val();
            var dto = {
                Id: id || 0,
                ApitTaxTableId: parseInt($('#aTable').val(), 10),
                EffectiveFrom: $('#aFrom').val(),
                FromAmount: parseFloat($('#aFromAmt').val()) || 0,
                ToAmount: to === '' ? null : parseFloat(to),
                Rate: parseFloat($('#aRate').val()) || 0,
                Relief: parseFloat($('#aRelief').val()) || 0,
                SortOrder: parseInt($('#aOrder').val(), 10) || 0
            };
            if (!dto.EffectiveFrom) { notifyError('An effective date is required.'); return; }
            psPost('/api/payroll-setup/apit', dto, 'Band', psLoadApit);
        });
}

function psApitDelete(id) { psDelete('/api/payroll-setup/apit/' + id, 'band', psLoadApit); }

// ── Loan types ────────────────────────────────────────────────────────────────

var psLoans = [];

function psLoadLoans() {
    $.getJSON('/api/payroll-setup/loan-types', function (d) {
        psLoans = d || [];
        amsPage('#psLoanBody', psLoans, function (t) {
            return '<tr>'
                 + '<td class="ps-3 fw-semibold">' + esc(t.Code) + '</td>'
                 + '<td>' + esc(t.Description) + '</td>'
                 + '<td><span class="badge bg-' + (isFlatRate(t) ? 'secondary' : 'info') + '">'
                 + esc(t.InterestTypeDisplay) + '</span></td>'
                 + '<td class="text-end">' + esc(t.RateDisplay) + '</td>'
                 + '<td class="text-center">' + psStatus(t.IsActive) + '</td>'
                 + '<td class="text-end pe-3">' + psActions('psLoanModal', t.Id, 'psLoanDelete') + '</td>'
                 + '</tr>';
        }, { colspan: 6, empty: 'No loan types defined.', label: 'loan type' });
    });
}

function psLoanModal(id) {
    var t = psLoans.find(function (x) { return x.Id === id; })
         || { InterestType: 1, InterestRate: 0, IsActive: true };

    psShowModal(id ? 'Edit Loan Type' : 'Add Loan Type',
        psField('Code', 'lCode', 'text', t.Code, { required: true, maxlength: 20, col: 4 })
      + psField('Description', 'lDesc', 'text', t.Description, { required: true, maxlength: 150, col: 8 })
      + psField('Interest Type', 'lType', 'select', enumNum(t.InterestType, ['Fixed','Reducing']), {
            col: 6,
            options: [{ value: 1, text: 'Fixed — flat, on the original amount' },
                      { value: 2, text: 'Reducing — on the outstanding balance' }],
            help: 'The same rate costs the borrower more under Fixed.'
        })
      + psField('Interest Rate %', 'lRate', 'number', t.InterestRate, {
            step: '0.01', col: 3, help: '0 for an interest-free loan.'
        })
      + psField('Active', 'lActive', 'checkbox', t.IsActive, { col: 3, checkLabel: 'Active' }),
        function () {
            var dto = {
                Id: id || 0,
                Code: $('#lCode').val().trim(),
                Description: $('#lDesc').val().trim(),
                InterestType: parseInt($('#lType').val(), 10),
                InterestRate: parseFloat($('#lRate').val()) || 0,
                IsActive: $('#lActive').is(':checked')
            };
            if (!dto.Code || !dto.Description) { notifyError('Code and description are required.'); return; }
            psPost('/api/payroll-setup/loan-types', dto, 'Loan type', psLoadLoans);
        });
}

function psLoanDelete(id) { psDelete('/api/payroll-setup/loan-types/' + id, 'loan type', psLoadLoans); }

// ── Third-party deductions ────────────────────────────────────────────────────

var psThirdParties = [];

function psLoadThirdParties() {
    $.getJSON('/api/payroll-setup/third-parties', function (d) {
        psThirdParties = d || [];
        amsPage('#psThirdPartyBody', psThirdParties, function (p) {
            return '<tr>'
                 + '<td class="ps-3 fw-semibold">' + esc(p.Code) + '</td>'
                 + '<td>' + esc(p.CompanyName) + '</td>'
                 // A payee with nothing feeding it will never receive a remittance, so it is
                 // called out rather than shown as an empty cell.
                 + '<td>' + (p.HasNoDeduction
                        ? '<span class="text-danger small">no deduction linked</span>'
                        : '<span class="badge bg-danger">' + esc(p.DeductionCode) + '</span> '
                          + '<span class="small">' + esc(p.DeductionName) + '</span>') + '</td>'
                 + '<td class="text-muted small">' + esc(p.Address || '—') + '</td>'
                 + '<td class="text-center">' + psStatus(p.IsActive) + '</td>'
                 + '<td class="text-end pe-3">'
                 + psActions('psThirdPartyModal', p.Id, 'psThirdPartyDelete') + '</td>'
                 + '</tr>';
        }, { colspan: 6, empty: 'No third parties defined.', label: 'third party' });
    });
}

function psThirdPartyModal(id) {
    var p = psThirdParties.find(function (x) { return x.Id === id; }) || { IsActive: true };

    // Only deductions are offered — an earning here would collect nothing, and the server
    // refuses it anyway. Filtering the list makes the rule visible rather than punitive.
    var deductions = psComponents
        .filter(function (c) { return isDeduction(c) && c.IsActive; })
        .map(function (c) { return { value: c.Id, text: c.Code + ' — ' + c.Name }; });

    if (!deductions.length) {
        notifyError('Add a deduction under Allowances & Deductions first — a third party needs '
                  + 'something to collect.');
        return;
    }

    psShowModal(id ? 'Edit Third Party' : 'Add Third Party',
        psField('Code', 'tpCode', 'text', p.Code, { required: true, maxlength: 20, col: 4 })
      + psField('Company Name', 'tpName', 'text', p.CompanyName, { required: true, maxlength: 200, col: 8 })
      + psField('Deduction Code', 'tpDeduction', 'select', p.SalaryComponentId, {
            col: 8, options: [{ value: '', text: '— none yet —' }].concat(deductions),
            help: 'Which deduction this party receives. Only deductions are listed.'
        })
      + psField('Active', 'tpActive', 'checkbox', p.IsActive, { col: 4, checkLabel: 'Active' })
      + psField('Address', 'tpAddress', 'text', p.Address, { maxlength: 500, col: 12,
            help: 'Where the remittance and its schedule are sent.' }),
        function () {
            var dto = {
                Id: id || 0,
                Code: $('#tpCode').val().trim(),
                CompanyName: $('#tpName').val().trim(),
                Address: $('#tpAddress').val().trim() || null,
                SalaryComponentId: parseInt($('#tpDeduction').val(), 10) || null,
                IsActive: $('#tpActive').is(':checked')
            };
            if (!dto.Code || !dto.CompanyName) {
                notifyError('Code and company name are required.'); return;
            }
            psPost('/api/payroll-setup/third-parties', dto, 'Third party', psLoadThirdParties);
        });
}

function psThirdPartyDelete(id) {
    psDelete('/api/payroll-setup/third-parties/' + id, 'third party', psLoadThirdParties);
}

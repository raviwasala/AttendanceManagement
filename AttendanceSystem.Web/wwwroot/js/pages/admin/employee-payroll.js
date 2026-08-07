/* ── Employee Profile: Payroll tab ────────────────────────────────────────────

   Its own file rather than appended to employee-profile.js: the profile script is
   already long, and this tab is only rendered for users who may see pay — keeping
   it separate means it is not even downloaded by everyone else.

   Loaded lazily on first open of the tab. Most visits to a profile are to read a
   phone number or check attendance, and fetching grades, banks and components
   every time would be four requests nobody asked for. */

var pfPayrollLoaded = false;

$(function () {
    $('a[href="#pf-payroll"]').on('shown.bs.tab', function () {
        if (!pfPayrollLoaded) { pfPayrollLoaded = true; pfLoadPayroll(); }
    });
});

/* Blank returns null, not 0 — these are optional overrides where "not set" and "zero"
   mean different things. parseFloat('') is NaN, which would serialise as null anyway,
   but relying on that is the kind of accident that stops being true after a refactor. */
function pfNum(raw) {
    var v = (raw || '').trim();
    return v === '' ? null : parseFloat(v);
}

function pfOpt(list, selected, blank) {
    return '<option value="">' + (blank || '— none —') + '</option>'
         + (list || []).map(function (x) {
               return '<option value="' + esc(x.Id) + '"'
                    + (String(x.Id) === String(selected) ? ' selected' : '') + '>'
                    + esc(x.Text) + '</option>';
           }).join('');
}

function pfLoadPayroll() {
    // All in flight together, then rendered once: the pickers need their options before
    // the saved values can be selected into them.
    $.when(
        $.getJSON('/api/employee-payroll/' + window.profileId),
        $.getJSON('/api/payroll-setup/grades'),
        $.getJSON('/api/payroll-setup/groups'),
        $.getJSON('/api/payroll-setup/sub-departments'),
        $.getJSON('/api/payroll-setup/bank-branches'),
        $.getJSON('/api/payroll-setup/categories'),
        $.getJSON('/api/payroll-setup/apit-tables'),
        $.getJSON('/api/payroll-setup/rates'),
        $.getJSON('/api/branches')
    ).done(function (infoRes, gradeRes, groupRes, subRes, branchRes,
                     catRes, taxRes, rateRes, coBranchRes) {
        var d = infoRes[0];

        var grades = (gradeRes[0] || []).filter(function (g) { return g.IsActive; })
            .map(function (g) { return { Id: g.Id, Text: g.Name + ' — ' + g.BasicSalary.toFixed(2) }; });

        var groups = (groupRes[0] || []).filter(function (g) { return g.IsActive; })
            .map(function (g) { return { Id: g.Id, Text: g.Name }; });

        var subs = (subRes[0] || []).filter(function (s) { return s.IsActive; })
            .map(function (s) { return { Id: s.Id, Text: s.DepartmentName + ' → ' + s.Name }; });

        var branches = (branchRes[0] || []).filter(function (b) { return b.IsActive; })
            .map(function (b) { return { Id: b.Id, Text: b.BankName + ' — ' + b.Name }; });

        $('#pfGrade').html(pfOpt(grades, d.SalaryGradeId, '— no grade —'));
        $('#pfGroup').html(pfOpt(groups, d.SalaryGroupId));
        $('#pfSubDept').html(pfOpt(subs, d.SubDepartmentId));
        $('#pfBankBranch').html(pfOpt(branches, d.BankBranchId));

        var categories = (catRes[0] || []).filter(function (c) { return c.IsActive; })
            .map(function (c) {
                return { Id: c.Id, Text: c.Name + (c.IsEpfEligible ? '' : ' — no EPF/ETF') };
            });

        var taxTables = (taxRes[0] || []).filter(function (t) { return t.IsActive; })
            .map(function (t) {
                return { Id: t.Id, Text: t.Name + (t.IsDefault ? ' (default)' : '') };
            });

        var coBranches = (coBranchRes[0] || []).filter(function (b) { return b.IsActive; })
            .map(function (b) { return { Id: b.Id, Text: b.Name }; });

        $('#pfCategory').html(pfOpt(categories, d.EmploymentCategoryId));
        $('#pfTaxTable').html(pfOpt(taxTables, d.ApitTaxTableId, '— use the default table —'));
        $('#pfEpfRegBranch').html(pfOpt(coBranches, d.EpfRegistrationBranchId, '— own branch —'));

        $('#pfEpfNo').val(d.EpfNumber || '');
        $('#pfEtfNo').val(d.EtfNumber || '');
        $('#pfEpfMember').prop('checked', d.IsEpfMember);
        $('#pfEtfMember').prop('checked', d.IsEtfMember);
        $('#pfEpfStatus').val(d.EpfStatus || '');
        $('#pfOtLimit').val(d.OtLimitHours || 0);

        // Only the override goes in the box — putting the grade figure here would save it
        // back as an override and quietly detach them from the grade.
        $('#pfOwnSalary').val(d.BasicSalaryOverride === null ? '' : d.BasicSalaryOverride);

        $('#pfSalaryHint').html(d.IsSalaryOverridden
            ? 'Overrides the grade'
              + (d.SalaryGradeName
                    ? ' — ' + esc(d.SalaryGradeName) + ' pays ' + d.GradeBasicSalary.toFixed(2)
                    : '') + '.'
            : (d.SalaryGradeName
                    ? 'Paying ' + d.GradeBasicSalary.toFixed(2) + ' from the grade.'
                    : 'No grade — set a salary here or assign a grade.'));

        // Blank rather than 0: an empty box means "use the company rate", and showing 0
        // would read as "contributes nothing", which is a different instruction entirely.
        $('#pfEpfEmpPct').val(d.EmployeeEpfPercentOverride === null ? '' : d.EmployeeEpfPercentOverride);
        $('#pfEpfErPct').val(d.EmployerEpfPercentOverride === null ? '' : d.EmployerEpfPercentOverride);
        $('#pfEtfPct').val(d.EmployerEtfPercentOverride === null ? '' : d.EmployerEtfPercentOverride);

        // Naming the company rates makes it obvious what a blank box falls back to.
        var current = (rateRes[0] || []).filter(function (r) { return r.IsCurrent; })[0];
        $('#pfRateHint').text(current
            ? 'Company rates in force: EPF ' + current.EmployeeEpfPercent.toFixed(2) + '% employee / '
              + current.EmployerEpfPercent.toFixed(2) + '% employer, ETF '
              + current.EmployerEtfPercent.toFixed(2) + '%.'
            : 'No company rates configured — set them under Payroll Setup.');

        $('#pfApit').prop('checked', d.IsApitApplicable);
        $('#pfTaxOnTax').prop('checked', d.IsTaxOnTax);
        $('#pfAddTax').val(d.AdditionalTaxAmount || 0);

        $('#pfAccountNo').val(d.AccountNumber || '');
        $('#pfAccountName').val(d.AccountName || '');
        $('#pfBankTransfer').prop('checked', d.IsBankTransfer);

        pfRenderPayrollWarnings(d);
        pfLoadComponents();
    }).fail(function (xhr) {
        $('#pfPayrollWarnings').html('<div class="alert alert-danger py-2 small">'
            + esc((xhr && xhr.responseText) || 'Could not load payroll details.') + '</div>');
    });
}

/* Listed up front rather than discovered during a payroll run — a run that stops
   halfway to report a missing bank account has already half-processed the month. */
function pfRenderPayrollWarnings(d) {
    if (!d.MissingForPayroll || !d.MissingForPayroll.length) {
        $('#pfPayrollWarnings').html('<div class="alert alert-success py-2 small mb-3">'
            + '<i class="feather icon-check-circle me-1"></i>'
            + 'This employee is ready to be included in a payroll run.</div>');
        return;
    }

    $('#pfPayrollWarnings').html('<div class="alert alert-warning py-2 small mb-3">'
        + '<i class="feather icon-alert-triangle me-1"></i><strong>Not ready for payroll:</strong>'
        + '<ul class="mb-0 mt-1">'
        + d.MissingForPayroll.map(function (m) { return '<li>' + esc(m) + '</li>'; }).join('')
        + '</ul></div>');
}

function pfSavePayroll() {
    var dto = {
        EmployeeId: window.profileId,
        EpfNumber: $('#pfEpfNo').val().trim() || null,
        EtfNumber: $('#pfEtfNo').val().trim() || null,
        IsEpfMember: $('#pfEpfMember').is(':checked'),
        IsEtfMember: $('#pfEtfMember').is(':checked'),
        EpfStatus: $('#pfEpfStatus').val().trim() || null,
        EpfRegistrationBranchId: parseInt($('#pfEpfRegBranch').val(), 10) || null,

        // Blank stays null so the company rate keeps applying. Sending 0 would mean
        // "contributes nothing", which is a different instruction.
        EmployeeEpfPercentOverride: pfNum($('#pfEpfEmpPct').val()),
        EmployerEpfPercentOverride: pfNum($('#pfEpfErPct').val()),
        EmployerEtfPercentOverride: pfNum($('#pfEtfPct').val()),

        IsApitApplicable: $('#pfApit').is(':checked'),
        ApitTaxTableId: parseInt($('#pfTaxTable').val(), 10) || null,
        IsTaxOnTax: $('#pfTaxOnTax').is(':checked'),
        AdditionalTaxAmount: parseFloat($('#pfAddTax').val()) || 0,

        EmploymentCategoryId: parseInt($('#pfCategory').val(), 10) || null,
        OtLimitHours: parseFloat($('#pfOtLimit').val()) || 0,

        SalaryGradeId: parseInt($('#pfGrade').val(), 10) || null,
        BasicSalaryOverride: pfNum($('#pfOwnSalary').val()),
        SalaryGroupId: parseInt($('#pfGroup').val(), 10) || null,
        SubDepartmentId: parseInt($('#pfSubDept').val(), 10) || null,
        BankBranchId: parseInt($('#pfBankBranch').val(), 10) || null,
        AccountNumber: $('#pfAccountNo').val().trim() || null,
        AccountName: $('#pfAccountName').val().trim() || null,
        IsBankTransfer: $('#pfBankTransfer').is(':checked')
    };

    $.ajax({ url: '/api/employee-payroll', type: 'POST', contentType: 'application/json',
             data: JSON.stringify(dto) })
        .done(function () {
            notifySuccess('Payroll details saved.');
            // Re-read rather than assume: the readiness warnings and the effective component
            // values both depend on what was just saved.
            $.getJSON('/api/employee-payroll/' + window.profileId, pfRenderPayrollWarnings);
            pfLoadComponents();
        })
        .fail(function (xhr) { notifyError(xhr.responseText || 'Could not save.'); });
}

function pfLoadComponents() {
    $.getJSON('/api/employee-payroll/' + window.profileId + '/components', function (rows) {
        if (!rows || !rows.length) {
            $('#pfComponentBody').html('<tr><td colspan="5" class="text-center py-4 text-muted">'
                + 'No allowances or deductions are defined. Add them under Payroll Setup.</td></tr>');
            return;
        }

        $('#pfComponentBody').html(rows.map(function (c) {
            // The input holds the override only. Left blank it shows the placeholder
            // "default", so an overridden value is visibly a deliberate difference.
            var overrideValue = c.HasOverride ? c.EffectiveValue : '';

            return '<tr>'
                 + '<td class="ps-3"><span class="fw-semibold">' + esc(c.Name) + '</span> '
                 + '<span class="text-muted small">' + esc(c.Code) + '</span>'
                 + (c.IsEpfLiable ? ' <span class="badge bg-info ms-1">EPF</span>' : '')
                 + (isOneOff(c) ? ' <span class="badge bg-light text-dark ms-1">one-off</span>' : '')
                 + '</td>'
                 + '<td><span class="badge bg-' + (isEarning(c) ? 'success' : 'danger') + '">'
                 + esc(c.ComponentTypeDisplay) + '</span></td>'
                 + '<td class="text-end text-muted">' + c.DefaultValue.toFixed(2) + '</td>'
                 + '<td class="text-end">'
                 + '<input type="number" step="0.01" class="form-control form-control-sm text-end pf-comp"'
                 + ' data-component="' + esc(c.SalaryComponentId) + '"'
                 + ' value="' + esc(overrideValue) + '" placeholder="default">'
                 + '</td>'
                 + '<td class="text-end pe-3 fw-semibold">' + c.EffectiveValue.toFixed(2) + '</td>'
                 + '</tr>';
        }).join(''));

        // Saved as each field is left, rather than behind one button: these are small
        // independent values, and a single Save for all of them would make one typo
        // ambiguous about what had been written.
        $('#pfComponentBody').off('change.pfcomp').on('change.pfcomp', '.pf-comp', function () {
            var raw = $(this).val().trim();
            $.ajax({
                url: '/api/employee-payroll/components', type: 'POST', contentType: 'application/json',
                data: JSON.stringify({
                    EmployeeId: window.profileId,
                    SalaryComponentId: parseInt($(this).attr('data-component'), 10),
                    // Blank clears the override and returns this employee to the default.
                    Value: raw === '' ? null : parseFloat(raw)
                })
            }).done(function () { pfLoadComponents(); })
              .fail(function (xhr) { notifyError(xhr.responseText || 'Could not save that value.'); });
        });
    });
}

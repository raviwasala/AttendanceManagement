/* ── Admin Overtime Rules ── */

var allRules = [];

/* Blank stays null so the branch figures apply. Zero would be a divide by zero in the
   hourly rate, so it is treated as "not set" rather than accepted. */
function ruleNum(sel, isInt) {
    var v = ($(sel).val() || '').trim();
    if (v === '') return null;
    var n = isInt ? parseInt(v, 10) : parseFloat(v);
    return (!n || n <= 0) ? null : n;
}

/* Shows the divisor the two boxes produce, so 30 × 8 = 240 is visible rather than implied.
   Blank boxes read as "branch default" because the figure is not known on this screen. */
function ruleUpdateDivisor() {
    var d = ruleNum('#ruleDays', true);
    var h = ruleNum('#ruleHours', false);

    $('#ruleDivisor').val(d && h ? (d * h) : 'branch default');
}

$(document).on('input', '#ruleDays,#ruleHours', ruleUpdateDivisor);

$(function () {
    $.when(
        $.getJSON('/api/departments', function (d) {
            (d || []).filter(function (x) { return x.IsActive; }).forEach(function (x) {
                $('#ruleDept').append('<option value="' + esc(x.Id) + '">' + esc(x.Name) + '</option>');
            });
        }),
        $.getJSON('/api/shifts', function (d) {
            (d || []).filter(function (x) { return x.IsActive; }).forEach(function (x) {
                $('#ruleShift').append('<option value="' + esc(x.Id) + '">' + esc(x.Name) + '</option>');
            });
        })
    ).always(loadRules);
});

function loadRules() {
    $.getJSON('/api/overtime/rules', function (d) { allRules = d || []; renderRules(); })
     .fail(function (xhr) {
         $('#ruleBody').html('<tr><td colspan="10" class="text-danger text-center py-3">'
             + esc(xhr.responseText || 'Failed to load overtime rules.') + '</td></tr>');
     });
}

function renderRules() {
    var empty = 'No overtime rules yet.'
              + (window.otPerms.edit
                    ? ' Add one — until a rule matches, no overtime can be claimed.' : '');

    amsPage('#ruleBody', allRules, function (r) {
        var actions = '';
        if (window.otPerms.edit) {
            actions += '<button class="btn btn-sm btn-outline-primary me-1" onclick="openRule(' + r.Id + ')" '
                     + 'title="Edit"><i class="fa fa-pencil"></i></button>';
        }
        if (window.otPerms.delete) {
            actions += '<button class="btn btn-sm btn-outline-danger" onclick="deleteRule(' + r.Id + ')" '
                     + 'title="Delete"><i class="fa fa-trash"></i></button>';
        }

        return '<tr' + (r.IsActive ? '' : ' class="text-muted"') + '>'
            + '<td class="ps-3 text-muted small">' + esc(r.Priority) + '</td>'
            + '<td><div class="fw-semibold">' + esc(r.Name) + '</div>'
              + (r.Description ? '<div class="text-muted" style="font-size:.72rem;">' + esc(r.Description) + '</div>' : '')
              + '</td>'
            + '<td class="small text-muted">' + esc(r.ScopeDisplay) + '</td>'
            + '<td class="text-center"><span class="badge bg-info">&times;' + esc(r.RateMultiplier) + '</span></td>'
            + '<td class="text-center small">' + esc(r.MinimumMinutes) + 'm</td>'
            + '<td class="text-center small">' + (r.MaxMinutesPerDay ? esc(r.MaxMinutesPerDay) + 'm' : '—') + '</td>'
            + '<td class="text-center small">' + (r.RoundToMinutes ? esc(r.RoundToMinutes) + 'm' : '—') + '</td>'
            + '<td class="text-center">' + (r.RequiresApproval
                ? '<span class="badge bg-warning text-dark">Required</span>'
                : '<span class="badge bg-success">Automatic</span>') + '</td>'
            + '<td>' + (r.IsActive
                ? '<span class="badge bg-success">Active</span>'
                : '<span class="badge bg-secondary">Inactive</span>') + '</td>'
            + '<td class="text-end pe-3">' + actions + '</td>'
            + '</tr>';
    }, { colspan: 10, empty: empty, label: 'rule' });
}

function openRule(id) {
    $('#ruleError').addClass('d-none').text('');

    if (!id) {
        $('#ruleId').val(0);
        $('#ruleName,#ruleDesc,#ruleMax').val('');
        $('#rulePriority').val(100);
        $('#ruleDept,#ruleShift').val('');
        $('#ruleDayType').val('0');
        $('#ruleRate').val('1.5');
        $('#ruleMin').val(30);
        $('#ruleRound').val('15');
        $('#ruleApproval,#ruleActive').prop('checked', true);
        $('#ruleCode').val('');
        $('#ruleDays').val('');
        $('#ruleHours').val('');
        // Matches the entity defaults. Whether overtime is EPF-liable varies by site — this
        // is only a starting point, not a rule.
        $('#ruleApit,#ruleGross').prop('checked', true);
        $('#ruleEpf').prop('checked', false);
        ruleUpdateDivisor();
        $('#ruleModalTitle').text('Add Overtime Rule');
        new bootstrap.Modal('#ruleModal').show();
        return;
    }

    $.getJSON('/api/overtime/rules/' + id, function (r) {
        $('#ruleId').val(r.Id);
        $('#ruleName').val(r.Name);
        $('#ruleDesc').val(r.Description || '');
        $('#rulePriority').val(r.Priority);
        $('#ruleDept').val(r.DepartmentId || '');
        $('#ruleShift').val(r.ShiftId || '');
        $('#ruleDayType').val(String(r.DayType));
        $('#ruleRate').val(r.RateMultiplier);
        $('#ruleMin').val(r.MinimumMinutes);
        $('#ruleMax').val(r.MaxMinutesPerDay || '');
        $('#ruleRound').val(String(r.RoundToMinutes));
        $('#ruleApproval').prop('checked', r.RequiresApproval);
        $('#ruleActive').prop('checked', r.IsActive);
        $('#ruleCode').val(r.Code || '');
        // Blank means "use the branch figures", so nulls stay blank rather than showing 0.
        $('#ruleDays').val(r.DaysPerMonth === null ? '' : r.DaysPerMonth);
        $('#ruleHours').val(r.HoursPerDay === null ? '' : r.HoursPerDay);
        $('#ruleApit').prop('checked', r.IsApitLiable);
        $('#ruleEpf').prop('checked', r.IsEpfLiable);
        $('#ruleGross').prop('checked', r.IncludeInGrossPay);
        ruleUpdateDivisor();
        $('#ruleModalTitle').text('Edit Overtime Rule');
        new bootstrap.Modal('#ruleModal').show();
    }).fail(function (xhr) { notifyError(xhr.responseText || 'Failed to load the rule.'); });
}

function saveRule() {
    var name = ($('#ruleName').val() || '').trim();
    if (!name) { showRuleError('Rule name is required.'); return; }

    var max = $('#ruleMax').val();
    var dto = {
        Id: parseInt($('#ruleId').val(), 10) || 0,
        Name: name,
        Description: ($('#ruleDesc').val() || '').trim() || null,
        IsActive: $('#ruleActive').is(':checked'),
        Priority: parseInt($('#rulePriority').val(), 10) || 100,
        DepartmentId: $('#ruleDept').val() ? parseInt($('#ruleDept').val(), 10) : null,
        ShiftId: $('#ruleShift').val() ? parseInt($('#ruleShift').val(), 10) : null,
        DayType: parseInt($('#ruleDayType').val(), 10) || 0,
        RateMultiplier: parseFloat($('#ruleRate').val()) || 1,
        MinimumMinutes: parseInt($('#ruleMin').val(), 10) || 0,
        // Blank means no cap, which is null rather than 0 — 0 would read as "never allowed".
        MaxMinutesPerDay: max === '' ? null : (parseInt(max, 10) || null),
        RoundToMinutes: parseInt($('#ruleRound').val(), 10) || 0,
        RequiresApproval: $('#ruleApproval').is(':checked'),
        Code: $('#ruleCode').val().trim() || null,
        // Null, not 0 — blank means "use the branch figures", and 0 would be a divide by zero.
        DaysPerMonth: ruleNum('#ruleDays', true),
        HoursPerDay: ruleNum('#ruleHours', false),
        IsApitLiable: $('#ruleApit').is(':checked'),
        IsEpfLiable: $('#ruleEpf').is(':checked'),
        IncludeInGrossPay: $('#ruleGross').is(':checked')
    };

    $.ajax({
        url: '/api/overtime/rules', type: 'POST',
        contentType: 'application/json', data: JSON.stringify(dto),
        success: function () {
            bootstrap.Modal.getInstance('#ruleModal').hide();
            notifySuccess('Overtime rule saved.');
            loadRules();
        },
        error: function (xhr) { showRuleError(xhr.responseText || 'Save failed.'); }
    });
}

function showRuleError(msg) {
    $('#ruleError').removeClass('d-none').text(msg);
}

function deleteRule(id) {
    notifyConfirm({
        title: 'Delete Overtime Rule',
        text: 'Claims already approved under this rule keep the rate they were approved at.',
        confirmText: 'Delete', icon: 'warning'
    }, function () {
        $.ajax({
            url: '/api/overtime/rules/' + id, type: 'DELETE',
            success: function () { notifySuccess('Overtime rule deleted.'); loadRules(); },
            error: function (xhr) { notifyError(xhr.responseText || 'Delete failed.'); }
        });
    });
}

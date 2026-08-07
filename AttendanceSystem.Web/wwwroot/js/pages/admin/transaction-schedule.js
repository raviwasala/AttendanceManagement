/* ── Transaction Schedule ── */

var tsComponents = [], tsRows = [];

$(function () {
    $.when(
        $.getJSON('/api/employee-payroll/list'),
        $.getJSON('/api/payroll-setup/components')
    ).done(function (empRes, compRes) {
        $('#tsEmployee').html('<option value="">— choose an employee —</option>'
            + (empRes[0] || []).map(function (e) {
                  return '<option value="' + esc(e.EmployeeId) + '">'
                       + esc(e.EmployeeCode) + ' — ' + esc(e.EmployeeName) + '</option>';
              }).join(''));

        tsComponents = (compRes[0] || []).filter(function (c) { return c.IsActive; });
    });
});

/* yyyymm is what the API speaks; <input type="month"> speaks yyyy-mm. Converted at the edge
   so neither side has to know about the other's format. */
function tsToYyyyMm(monthValue) {
    if (!monthValue) return null;
    return parseInt(monthValue.replace('-', ''), 10);
}

function tsToMonthInput(yyyymm) {
    if (!yyyymm) return '';
    var s = String(yyyymm);
    return s.substring(0, 4) + '-' + s.substring(4, 6);
}

function tsMonthLabel(yyyymm) {
    if (!yyyymm) return '—';
    var y = Math.floor(yyyymm / 100), m = yyyymm % 100;
    return new Date(y, m - 1, 1).toLocaleString(undefined, { month: 'short', year: 'numeric' });
}

function tsLoad() {
    var id = parseInt($('#tsEmployee').val(), 10);
    $('#tsAddBtn').prop('disabled', !id);

    if (!id) {
        $('#tsBody').html('<tr><td colspan="8" class="text-center py-4 text-muted">Choose an employee.</td></tr>');
        return;
    }

    $('#tsBody').html('<tr><td colspan="8" class="text-center py-4 text-muted">Loading…</td></tr>');

    $.getJSON('/api/employee-payroll/schedule/' + id, function (rows) {
        tsRows = rows || [];

        if (!tsRows.length) {
            $('#tsBody').html('<tr><td colspan="8" class="text-center py-4 text-muted">'
                + 'Nothing scheduled for this employee.</td></tr>');
            return;
        }

        $('#tsBody').html(tsRows.map(function (r) {
            var c = tsComponents.filter(function (x) { return x.Id === r.SalaryComponentId; })[0];

            var statusBadge = r.StatusDisplay === 'Running'
                ? '<span class="badge bg-success">Running</span>'
                : r.StatusDisplay === 'Ended'
                    ? '<span class="badge bg-secondary">Ended</span>'
                    : '<span class="badge bg-info">Not started</span>';

            // Ended rows are dimmed rather than hidden — the history is why an allowance
            // stopped, and hiding it invites somebody to add a duplicate.
            return '<tr' + (r.IsCurrent ? '' : ' class="opacity-50"') + '>'
                 + '<td class="ps-3 fw-semibold">' + esc(r.Code) + '</td>'
                 + '<td>' + esc(r.Description) + '</td>'
                 + '<td>' + (c
                        ? '<span class="badge bg-' + (isEarning(c) ? 'success' : 'danger') + '">'
                          + esc(c.ComponentTypeDisplay) + '</span>'
                        : '<span class="text-muted">—</span>') + '</td>'
                 + '<td class="text-end fw-semibold">' + r.Amount.toFixed(2) + '</td>'
                 + '<td class="text-center small">' + esc(tsMonthLabel(r.FromYearMonth)) + '</td>'
                 + '<td class="text-center small">' + (r.ToYearMonth
                        ? esc(tsMonthLabel(r.ToYearMonth))
                        : '<span class="text-muted">open</span>') + '</td>'
                 + '<td class="text-center">' + statusBadge + '</td>'
                 + '<td class="text-end pe-3">'
                 + '<button class="btn btn-sm btn-outline-primary me-1" onclick="tsRowModal(' + r.Id + ')">'
                 + '<i class="fa fa-pencil"></i></button>'
                 + '<button class="btn btn-sm btn-outline-danger" onclick="tsDelete(' + r.Id + ')">'
                 + '<i class="fa fa-trash"></i></button>'
                 + '</td></tr>';
        }).join(''));
    }).fail(function (xhr) {
        $('#tsBody').html('<tr><td colspan="8" class="text-danger text-center py-4">'
            + esc(xhr.responseText || 'Failed to load.') + '</td></tr>');
    });
}

function tsRowModal(id) {
    var r = tsRows.filter(function (x) { return x.Id === id; })[0];

    if (!tsComponents.length) {
        notifyError('No allowances or deductions are defined. Add them under Payroll Setup.');
        return;
    }

    var group = function (label, match) {
        var items = tsComponents.filter(match);
        if (!items.length) return '';
        return '<optgroup label="' + label + '">'
             + items.map(function (c) {
                   return '<option value="' + esc(c.Id) + '"'
                        + (r && r.SalaryComponentId === c.Id ? ' selected' : '') + '>'
                        + esc(c.Code) + ' — ' + esc(c.Name) + '</option>';
               }).join('') + '</optgroup>';
    };

    $('#tsModalTitle').text(id ? 'Edit Entry' : 'Add Entry');
    $('#tsRowId').val(id || 0);
    $('#tsComponent').html(group('Earnings', isEarning) + group('Deductions', isDeduction));
    $('#tsAmount').val(r ? r.Amount : '');

    // Defaults to this month, which is what somebody adding an entry today almost always means.
    var now = new Date();
    var thisMonth = now.getFullYear() + '-' + String(now.getMonth() + 1).padStart(2, '0');
    $('#tsFrom').val(r ? tsToMonthInput(r.FromYearMonth) : thisMonth);
    $('#tsTo').val(r && r.ToYearMonth ? tsToMonthInput(r.ToYearMonth) : '');

    tsComponentChanged();
    new bootstrap.Modal('#tsModal').show();
}

function tsComponentChanged() {
    var id = parseInt($('#tsComponent').val(), 10);
    var c = tsComponents.filter(function (x) { return x.Id === id; })[0];
    if (!c) { $('#tsComponentHint').text(''); return; }

    $('#tsComponentHint').html('<strong>' + esc(c.ComponentTypeDisplay) + '</strong>, default '
        + esc(c.ValueDisplay)
        + (isPctOfBasic(c) ? ' — the amount below is a percentage of basic.' : ''));

    tsPreview();
}

$(document).on('change', '#tsFrom,#tsTo', tsPreview);

/* Says how many months the entry will run for. "202608 to 202610" is three months, not two,
   and the off-by-one is the easiest mistake to make on this screen. */
function tsPreview() {
    var from = tsToYyyyMm($('#tsFrom').val());
    var to = tsToYyyyMm($('#tsTo').val());

    if (!from) { $('#tsPreview').text(''); return; }

    if (!to) {
        $('#tsPreview').html('Runs from <strong>' + esc(tsMonthLabel(from))
            + '</strong> indefinitely, until somebody ends it.');
        return;
    }

    if (to < from) {
        $('#tsPreview').html('<span class="text-danger">The end month is before the start month.</span>');
        return;
    }

    var months = (Math.floor(to / 100) - Math.floor(from / 100)) * 12 + ((to % 100) - (from % 100)) + 1;
    $('#tsPreview').html('Runs for <strong>' + months + ' month(s)</strong>, '
        + esc(tsMonthLabel(from)) + ' to ' + esc(tsMonthLabel(to))
        + ' inclusive — the last month is paid.');
}

function tsSave() {
    var dto = {
        Id: parseInt($('#tsRowId').val(), 10) || 0,
        EmployeeId: parseInt($('#tsEmployee').val(), 10),
        SalaryComponentId: parseInt($('#tsComponent').val(), 10),
        Amount: parseFloat($('#tsAmount').val()) || 0,
        FromYearMonth: tsToYyyyMm($('#tsFrom').val()),
        ToYearMonth: tsToYyyyMm($('#tsTo').val())
    };

    if (!dto.SalaryComponentId) { notifyError('Choose a code.'); return; }
    if (!dto.FromYearMonth) { notifyError('Choose a start month.'); return; }
    if (dto.ToYearMonth && dto.ToYearMonth < dto.FromYearMonth) {
        notifyError('The end month cannot be before the start month.'); return;
    }

    $.ajax({ url: '/api/employee-payroll/schedule', type: 'POST',
             contentType: 'application/json', data: JSON.stringify(dto) })
        .done(function () {
            bootstrap.Modal.getInstance('#tsModal').hide();
            $('#tsAlert').html('<div class="alert alert-success alert-dismissible fade show py-2">'
                + 'Schedule entry saved.'
                + '<button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>');
            tsLoad();
        })
        .fail(function (xhr) { notifyError(xhr.responseText || 'Could not save the entry.'); });
}

function tsDelete(id) {
    notifyConfirm({
        title: 'Delete this entry?',
        text: 'It will no longer be paid or deducted. Ending it with a To month instead keeps '
            + 'the history of what was paid.',
        confirmText: 'Delete', icon: 'warning'
    }, function () {
        $.ajax({ url: '/api/employee-payroll/schedule/' + id, type: 'DELETE' })
            .done(function () { tsLoad(); })
            .fail(function (xhr) { notifyError(xhr.responseText || 'Could not delete.'); });
    });
}

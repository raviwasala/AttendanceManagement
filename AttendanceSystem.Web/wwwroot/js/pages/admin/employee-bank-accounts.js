/* ── Employee Bank Accounts ── */

var baRows = [], baBranches = [];

$(function () {
    $.getJSON('/api/departments', function (d) {
        (d || []).filter(function (x) { return x.IsActive; }).forEach(function (x) {
            $('#baDept').append('<option value="' + esc(x.Id) + '">' + esc(x.Name) + '</option>');
        });
    });

    // Branches are needed to build every row's dropdown, so they are fetched before the rows.
    $.getJSON('/api/payroll-setup/bank-branches', function (d) {
        baBranches = (d || []).filter(function (b) { return b.IsActive; });
        baLoad();
    });
});

function baLoad() {
    $('#baBody').html('<tr><td colspan="8" class="text-center py-4 text-muted">Loading…</td></tr>');

    // Fetched unfiltered so the summary describes everyone, not the current view.
    $.getJSON('/api/employee-payroll/bank-rows', function (rows) {
        baRows = rows || [];
        baFilter();
    }).fail(function (xhr) {
        $('#baBody').html('<tr><td colspan="8" class="text-danger text-center py-4">'
            + esc(xhr.responseText || 'Failed to load.') + '</td></tr>');
    });
}

function baFilter() {
    var q = ($('#baSearch').val() || '').toLowerCase();
    var deptId = $('#baDept').val();
    var deptName = $('#baDept').find('option:selected').text();
    var incompleteOnly = $('#baIncomplete').val() === '1';

    var shown = baRows.filter(function (r) {
        return (!q || (r.EmployeeName || '').toLowerCase().indexOf(q) >= 0
                   || (r.EmployeeCode || '').toLowerCase().indexOf(q) >= 0
                   || (r.AccountNumber || '').toLowerCase().indexOf(q) >= 0)
            && (!deptId || r.Department === deptName)
            && (!incompleteOnly || r.IsIncomplete);
    });

    var missing = baRows.filter(function (r) { return r.IsIncomplete; }).length;
    $('#baSummary').html(missing
        ? '<i class="feather icon-alert-triangle text-warning me-1"></i><strong>' + missing
          + '</strong> employee(s) are paid by transfer but have no bank branch or account number — '
          + 'they cannot be included in the transfer file.'
        : '<i class="feather icon-check-circle text-success me-1"></i>'
          + 'Every employee paid by transfer has bank details.');

    baRender(shown);
}

function baBranchOptions(selected) {
    return '<option value="">— none —</option>'
         + baBranches.map(function (b) {
               return '<option value="' + esc(b.Id) + '"'
                    + (String(b.Id) === String(selected) ? ' selected' : '') + '>'
                    + esc(b.BankName) + ' — ' + esc(b.Name) + '</option>';
           }).join('');
}

function baRender(rows) {
    amsPage('#baBody', rows, function (r) {
        return '<tr data-employee="' + esc(r.EmployeeId) + '">'
             + '<td class="ps-3 fw-semibold">' + esc(r.EmployeeCode) + '</td>'
             + '<td>' + esc(r.EmployeeName) + '</td>'
             + '<td class="text-muted small">' + esc(r.Department) + '</td>'
             + '<td><select class="form-select form-select-sm ba-branch">'
             + baBranchOptions(r.BankBranchId) + '</select></td>'
             + '<td><input type="text" class="form-control form-control-sm ba-account" maxlength="30"'
             + ' value="' + esc(r.AccountNumber || '') + '"></td>'
             + '<td><input type="text" class="form-control form-control-sm ba-name" maxlength="150"'
             + ' placeholder="same as employee" value="' + esc(r.AccountName || '') + '"></td>'
             + '<td class="text-center"><div class="form-check form-switch d-inline-block">'
             + '<input class="form-check-input ba-transfer" type="checkbox"'
             + (r.IsBankTransfer ? ' checked' : '') + '></div></td>'
             + '<td class="text-center pe-3 ba-status">' + baStatusBadge(r) + '</td>'
             + '</tr>';
    }, { colspan: 8, empty: 'No employees match these filters.', label: 'employee' });
}

function baStatusBadge(r) {
    if (!r.IsBankTransfer) return '<span class="badge bg-secondary">Cash</span>';
    return r.IsIncomplete
        ? '<span class="badge bg-warning text-dark">Incomplete</span>'
        : '<span class="badge bg-success">OK</span>';
}

/* Saved as each field is left rather than behind one button. The grid is hundreds of
   independent rows, and a single Save would make one bad value ambiguous about what had
   been written. */
$(document).on('change', '.ba-branch, .ba-account, .ba-name, .ba-transfer', function () {
    var $tr = $(this).closest('tr');
    var employeeId = parseInt($tr.attr('data-employee'), 10);

    var dto = {
        EmployeeId: employeeId,
        BankBranchId: parseInt($tr.find('.ba-branch').val(), 10) || null,
        AccountNumber: $tr.find('.ba-account').val().trim() || null,
        AccountName: $tr.find('.ba-name').val().trim() || null,
        IsBankTransfer: $tr.find('.ba-transfer').is(':checked')
    };

    $.ajax({ url: '/api/employee-payroll/bank-rows', type: 'POST',
             contentType: 'application/json', data: JSON.stringify(dto) })
        .done(function () {
            // The cached row is updated in place so the status badge and the summary count
            // stay right without refetching the whole grid on every keystroke.
            var row = baRows.filter(function (x) { return x.EmployeeId === employeeId; })[0];
            if (row) {
                row.BankBranchId = dto.BankBranchId;
                row.AccountNumber = dto.AccountNumber;
                row.AccountName = dto.AccountName;
                row.IsBankTransfer = dto.IsBankTransfer;
                row.IsIncomplete = dto.IsBankTransfer
                    && (!dto.BankBranchId || !dto.AccountNumber);
                $tr.find('.ba-status').html(baStatusBadge(row));
            }
            $tr.addClass('table-success');
            setTimeout(function () { $tr.removeClass('table-success'); }, 600);
        })
        .fail(function (xhr) {
            $tr.addClass('table-danger');
            notifyError(xhr.responseText || 'Could not save that row.');
        });
});

/* ── Admin: Employee Profile ── */

var profile = null;
var lookups = { depts: [], desigs: [], branches: [] };

// Same silhouette the employee form uses: an inline data URI cannot 404 if the
// theme's image folders are ever reorganised.
var DEFAULT_AVATAR =
    'data:image/svg+xml;utf8,' + encodeURIComponent(
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 110 110">' +
        '<rect width="110" height="110" fill="%23eef1f4"/>' +
        '<circle cx="55" cy="42" r="20" fill="%23b6c0cc"/>' +
        '<path d="M17 104c0-20 17-31 38-31s38 11 38 31z" fill="%23b6c0cc"/></svg>');

var statusBadgeClass = {
    Active: 'bg-success', Resigned: 'bg-secondary', Terminated: 'bg-danger',
    Suspended: 'bg-warning text-dark', OnLongLeave: 'bg-info text-dark'
};

$(function () {
    loadProfile();

    // Loaded once for the transfer modal rather than per open.
    $.getJSON('/api/departments',  function (d) { lookups.depts    = d || []; });
    $.getJSON('/api/designations', function (d) { lookups.desigs   = d || []; });
    $.getJSON('/api/branches',     function (d) { lookups.branches = d || []; });
});

function loadProfile() {
    $.getJSON('/api/employees/' + window.profileId + '/profile', function (d) {
        profile = d;
        renderIdentity(d);
        renderStats(d);
        renderBalances(d.LeaveBalances);
        renderHistory(d.History);
        renderDocuments(d.Documents);
    }).fail(function (xhr) {
        $('#pfError').removeClass('d-none').text(xhr.responseText || 'Could not load this employee.');
    });
}

function renderIdentity(d) {
    var e = d.Employee;
    var name = (e.FirstName || '') + (e.LastName ? ' ' + e.LastName : '');

    $('#pfName').text(name || 'Employee Profile');
    $('#pfSubtitle').text(e.EmployeeCode + (e.DepartmentName ? ' · ' + e.DepartmentName : ''));
    $('#pfFullName').text(name);
    $('#pfDesignation').text(e.DesignationName || '—');
    $('#pfPhoto').attr('src', e.Photo ? 'data:image/jpeg;base64,' + e.Photo : DEFAULT_AVATAR);

    var badge = '<span class="badge ' + (statusBadgeClass[d.StatusDisplay] || 'bg-secondary') + '">'
              + esc(d.StatusDisplay) + '</span>';
    if (d.ResignationDate) {
        badge += '<div class="small text-muted mt-1">Last day ' + fmtDate(d.ResignationDate) + '</div>';
    }
    $('#pfStatusBadge').html(badge);

    // Resign and Rejoin are the same decision from opposite sides; showing both at once
    // invites the wrong one.
    var isActive = d.StatusDisplay === 'Active';
    $('#pfResignBtn').toggleClass('d-none', !isActive);
    $('#pfRejoinBtn').toggleClass('d-none', isActive);

    var service = d.ServiceYears + 'y ' + d.ServiceMonths + 'm';
    var rows = [
        ['Employee code', e.EmployeeCode],
        ['User ID', e.UserCode],
        ['NIC', e.Nic],
        ['Branch', e.BranchName],
        ['Joined', fmtDate(e.JoiningDate) + ' (' + service + ')'],
        ['Shift', d.CurrentShift ? d.CurrentShift + ' · ' + d.CurrentShiftTimes : null],
        ['Phone', e.Phone],
        ['Email', e.Email],
        // Its absence is the single most common cause of "the import did nothing", so it
        // is called out rather than shown as a blank line.
        ['Biometric enroll ID', e.BiometricEnrollId ||
            '<span class="badge bg-warning text-dark">not set</span>']
    ];

    $('#pfDetails').html(rows.map(function (r) {
        return '<li class="list-group-item d-flex justify-content-between py-2">'
             + '<span class="text-muted">' + esc(r[0]) + '</span>'
             // Only the enroll-id cell contains markup, and it is ours, not data.
             + '<span class="fw-medium text-end">' + (r[1] == null || r[1] === ''
                    ? '—'
                    : (r[0] === 'Biometric enroll ID' ? r[1] : esc(r[1]))) + '</span></li>';
    }).join(''));
}

function renderStats(d) {
    var tile = function (label, value, colour) {
        return '<div class="col-6 col-md-3"><div class="card stat-card ' + colour + '">'
             + '<div class="card-body stat-card-body py-2"><div class="stat-card-text">'
             + '<p class="stat-card-label mb-0">' + label + '</p>'
             + '<h3 class="stat-card-value" style="font-size:1.3rem;">' + value + '</h3>'
             + '</div></div></div></div>';
    };
    $('#pfStats').html(
        tile('Present', d.PresentDays, 'bg-c-green')
      + tile('Late', d.LateDays, 'bg-c-yellow')
      + tile('Absent', d.AbsentDays, 'bg-c-pink')
      + tile('On leave', d.LeaveDays, 'bg-c-blue'));
}

function renderBalances(balances) {
    if (!balances || !balances.length) {
        $('#pfBalances').html('<div class="text-muted small">No leave types configured.</div>');
        return;
    }
    $('#pfBalances').html(balances.map(function (b) {
        return '<div class="d-flex justify-content-between small py-1">'
             + '<span>' + esc(b.LeaveType) + '</span>'
             + '<span><strong>' + b.Remaining + '</strong> <span class="text-muted">of '
             + b.Entitled + '</span></span></div>';
    }).join(''));
}

function renderHistory(rows) {
    if (!rows || !rows.length) {
        $('#pfHistoryBody').html('<tr><td colspan="4" class="text-center py-4 text-muted">'
            + 'Nothing recorded yet. Transfers, status changes and resignations appear here.</td></tr>');
        return;
    }

    var typeBadge = {
        Transfer: 'bg-primary', Promotion: 'bg-success', StatusChange: 'bg-warning text-dark',
        Resignation: 'bg-danger', Rejoin: 'bg-info text-dark'
    };

    $('#pfHistoryBody').html(rows.map(function (h) {
        return '<tr>'
            + '<td class="ps-3 small">' + esc(h.EffectiveDateDisplay) + '</td>'
            + '<td><span class="badge ' + (typeBadge[h.ChangeTypeDisplay] || 'bg-secondary') + '">'
            + esc(h.ChangeTypeDisplay) + '</span></td>'
            + '<td class="small">'
            + (h.FromLabel ? esc(h.FromLabel) + ' <i class="feather icon-arrow-right text-muted"></i> ' : '')
            + '<strong>' + esc(h.ToLabel || '—') + '</strong></td>'
            + '<td class="pe-3 small text-muted">' + esc(h.Reason || '—')
            + (h.Notes ? '<div style="font-size:.72rem;">' + esc(h.Notes) + '</div>' : '')
            + '</td></tr>';
    }).join(''));
}

function renderDocuments(docs) {
    if (!docs || !docs.length) {
        $('#pfDocBody').html('<tr><td colspan="5" class="text-center py-4 text-muted">No documents yet.</td></tr>');
        return;
    }

    $('#pfDocBody').html(docs.map(function (d) {
        return '<tr' + (d.IsExpired ? ' class="table-warning"' : '') + '>'
            + '<td class="ps-3 small">' + esc(d.Title)
            + '<div class="text-muted" style="font-size:.72rem;">' + esc(d.FileName) + '</div></td>'
            + '<td class="small">' + esc(d.DocumentTypeDisplay) + '</td>'
            + '<td class="small text-muted">' + esc(d.SizeDisplay) + '</td>'
            + '<td class="small">' + (d.ExpiryDate
                ? fmtDate(d.ExpiryDate) + (d.IsExpired ? ' <span class="badge bg-danger">expired</span>' : '')
                : '<span class="text-muted">—</span>') + '</td>'
            + '<td class="pe-3 text-center">'
            + '<a class="btn btn-sm btn-outline-primary py-0 px-2 me-1" title="Download"'
            + ' href="/api/employees/documents/' + d.Id + '/download"><i class="fa fa-download"></i></a>'
            + (window.profilePerms.del
                ? '<button class="btn btn-sm btn-outline-danger py-0 px-2" title="Delete"'
                  + ' onclick="deleteDoc(' + d.Id + ')"><i class="fa fa-trash"></i></button>' : '')
            + '</td></tr>';
    }).join(''));
}

function fmtDate(iso) {
    return iso ? new Date(iso).toLocaleDateString(undefined,
        { day: '2-digit', month: 'short', year: 'numeric' }) : '—';
}

function today() { return new Date().toISOString().split('T')[0]; }

/* ── Transfer ─────────────────────────────────────────────────────────────── */

function openTransfer() {
    var e = profile.Employee;
    var opts = function (list, current) {
        return list.filter(function (x) { return x.IsActive !== false; })
                   .map(function (x) {
                       return '<option value="' + esc(x.Id) + '"'
                            + (x.Id === current ? ' selected' : '') + '>' + esc(x.Name) + '</option>';
                   }).join('');
    };

    $('#trDept').html(opts(lookups.depts, e.DepartmentId));
    $('#trDesig').html(opts(lookups.desigs, e.DesignationId));
    $('#trBranch').html(opts(lookups.branches, e.BranchId));
    $('#trDate').val(today());
    $('#trReason').val('');

    new bootstrap.Modal('#transferModal').show();
}

function submitTransfer() {
    if (!$('#trDate').val()) { notifyError('Choose an effective date.'); return; }

    $.ajax({
        url: '/api/employees/transfer', type: 'POST', contentType: 'application/json',
        data: JSON.stringify({
            EmployeeId: window.profileId,
            DepartmentId: parseInt($('#trDept').val()),
            DesignationId: parseInt($('#trDesig').val()),
            BranchId: parseInt($('#trBranch').val()),
            EffectiveDate: $('#trDate').val(),
            Reason: $('#trReason').val().trim() || null
        }),
        success: function () {
            bootstrap.Modal.getInstance('#transferModal').hide();
            notifySuccess('Transfer recorded.');
            loadProfile();
        },
        // The service refuses a transfer where nothing changed; its wording is more useful
        // than a generic failure.
        error: function (xhr) { notifyError(xhr.responseText || 'Transfer failed.'); }
    });
}

/* ── Status ───────────────────────────────────────────────────────────────── */

function openStatus() {
    $('#stDate').val(today());
    $('#stReason').val('');
    new bootstrap.Modal('#statusModal').show();
}

function submitStatus() {
    var reason = $('#stReason').val().trim();
    if (!reason) { notifyError('A reason is required.'); return; }
    if (!$('#stDate').val()) { notifyError('Choose an effective date.'); return; }

    $.ajax({
        url: '/api/employees/status', type: 'POST', contentType: 'application/json',
        data: JSON.stringify({
            EmployeeId: window.profileId,
            Status: parseInt($('#stStatus').val()),
            EffectiveDate: $('#stDate').val(),
            Reason: reason
        }),
        success: function () {
            bootstrap.Modal.getInstance('#statusModal').hide();
            notifySuccess('Status updated.');
            loadProfile();
        },
        error: function (xhr) { notifyError(xhr.responseText || 'Could not change the status.'); }
    });
}

/* ── Resign / rejoin ──────────────────────────────────────────────────────── */

function openResign() {
    $('#rsDate').val(today());
    $('#rsReason').val('');
    $('#rsTermination').prop('checked', false);
    new bootstrap.Modal('#resignModal').show();
}

function submitResign() {
    var reason = $('#rsReason').val().trim();
    if (!$('#rsDate').val()) { notifyError('Choose a last working day.'); return; }
    if (!reason) { notifyError('A reason is required.'); return; }

    notifyConfirm({
        title: 'Record resignation',
        text: 'This marks the employee as no longer working from ' + $('#rsDate').val() + '.',
        confirmText: 'Record', icon: 'warning'
    }, function () {
        $.ajax({
            url: '/api/employees/resign', type: 'POST', contentType: 'application/json',
            data: JSON.stringify({
                EmployeeId: window.profileId,
                ResignationDate: $('#rsDate').val(),
                Reason: reason,
                IsTermination: $('#rsTermination').is(':checked')
            }),
            success: function () {
                bootstrap.Modal.getInstance('#resignModal').hide();
                notifySuccess('Resignation recorded.');
                loadProfile();
            },
            error: function (xhr) { notifyError(xhr.responseText || 'Could not record the resignation.'); }
        });
    });
}

function openRejoin() {
    notifyConfirm({
        title: 'Rejoin',
        text: 'This returns the employee to Active from today and clears their last working day. '
            + 'The original resignation stays in their history.',
        confirmText: 'Rejoin', icon: 'question'
    }, function () {
        $.ajax({
            url: '/api/employees/' + window.profileId + '/rejoin', type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ EffectiveDate: today(), Reason: 'Rejoined' }),
            success: function () { notifySuccess('Employee reactivated.'); loadProfile(); },
            error: function (xhr) { notifyError(xhr.responseText || 'Could not reactivate.'); }
        });
    });
}

/* ── Documents ────────────────────────────────────────────────────────────── */

function openUpload() {
    $('#dcFile').val(''); $('#dcTitle').val(''); $('#dcExpiry').val(''); $('#dcType').val('99');
    new bootstrap.Modal('#uploadModal').show();
}

function submitUpload() {
    var input = $('#dcFile')[0];
    if (!input || !input.files || !input.files.length) { notifyError('Choose a file.'); return; }

    var fd = new FormData();
    fd.append('file', input.files[0]);
    fd.append('title', $('#dcTitle').val().trim());
    // The numeric enum value, not the label: Enum.TryParse is case-sensitive and the labels
    // do not match the members ("NIC" is not "Nic", "Medical" is not "MedicalRecord").
    fd.append('documentType', $('#dcType').val());
    if ($('#dcExpiry').val()) fd.append('expiryDate', $('#dcExpiry').val());

    $.ajax({
        url: '/api/employees/' + window.profileId + '/documents', type: 'POST',
        data: fd, processData: false, contentType: false,
        success: function () {
            bootstrap.Modal.getInstance('#uploadModal').hide();
            notifySuccess('Document uploaded.');
            loadProfile();
        },
        error: function (xhr) { notifyError(xhr.responseText || 'Upload failed.'); }
    });
}

function deleteDoc(id) {
    notifyConfirm({
        title: 'Delete document', text: 'Remove this document from the employee record?',
        confirmText: 'Delete', icon: 'warning'
    }, function () {
        $.ajax({
            url: '/api/employees/documents/' + id, type: 'DELETE',
            success: function () { notifySuccess('Document deleted.'); loadProfile(); },
            error: function (xhr) { notifyError(xhr.responseText || 'Delete failed.'); }
        });
    });
}

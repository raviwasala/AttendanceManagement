/* ── Admin Leave Management JavaScript ── */

var allReqs = [], allTypes = [];

/*
 * Applies a ?status= value from the URL to the request-status dropdown.
 *
 * The dashboard's "On Leave Today" tile deep-links here with status=Approved. Set before
 * loadRequests(), because loadRequests() calls filterRequests() once the data arrives —
 * so the filter is already in place by then. Unrecognised values are ignored.
 */
function applyStatusFromQuery() {
    var wanted = new URLSearchParams(window.location.search).get('status');
    if (!wanted) return;

    var match = $('#reqStatusFilter option').filter(function () {
        return this.value.toLowerCase() === wanted.toLowerCase();
    }).first();
    if (match.length) $('#reqStatusFilter').val(match.val());
}

$(function () { applyStatusFromQuery(); loadRequests(); loadTypes(); loadApplyDropdowns(); });

function loadApplyDropdowns() {
    $.getJSON('/api/employees', function (d) {
        var o = '<option value="">-- Employee --</option>';
        d.forEach(function (e) { o += '<option value="' + e.Id + '">' + e.EmployeeCode + ' - ' + e.FullName + '</option>'; });
        $('#leaveEmp').html(o);
    });
}

function loadRequests() {
    $.getJSON('/api/leave/requests', function (d) { allReqs = d; filterRequests(); })
     .fail(function () { $('#reqBody').html('<tr><td colspan="9" class="text-danger text-center py-3">Failed to load.</td></tr>'); });
}

function filterRequests() {
    var s = $('#reqStatusFilter').val(); var q = $('#reqSearch').val().toLowerCase();
    renderRequests(allReqs.filter(function (r) {
        return (s==='' || r.StatusDisplay===s) && (!q || r.EmployeeName.toLowerCase().includes(q));
    }));
}

function renderRequests(data) {
    amsPage('#reqBody', data, function (r) {
        var badge = r.StatusDisplay==='Approved'?'success':r.StatusDisplay==='Rejected'?'danger':r.StatusDisplay==='Pending'?'warning':'secondary';
        return '<tr>'
            + '<td>' + esc(r.EmployeeName) + '<br><small class="text-muted">' + esc(r.EmployeeCode) + '</small></td>'
            + '<td class="text-muted small">' + esc(r.Department) + '</td>'
            + '<td>' + esc(r.LeaveTypeName) + '</td>'
            + '<td>' + new Date(r.FromDate).toLocaleDateString() + '</td>'
            + '<td>' + new Date(r.ToDate).toLocaleDateString() + '</td>'
            + '<td class="text-center">' + esc(r.TotalDays) + '</td>'
            + '<td class="text-muted small" style="max-width:120px;overflow:hidden;white-space:nowrap;text-overflow:ellipsis;">' + esc(r.Reason) + '</td>'
            + '<td><span class="badge bg-' + badge + '">' + esc(r.StatusDisplay) + '</span></td>'
            + '<td>'
            + (r.StatusDisplay==='Pending' ? '<button class="btn btn-sm btn-success me-1" onclick="approveReject(' + r.Id + ',true)" title="Approve"><i class="fa fa-check"></i></button>'
               + '<button class="btn btn-sm btn-danger me-1" onclick="approveReject(' + r.Id + ',false)" title="Reject"><i class="fa fa-times"></i></button>' : '')
            + (r.StatusDisplay==='Pending'||r.StatusDisplay==='Approved' ? '<button class="btn btn-sm btn-outline-secondary" onclick="cancel(' + r.Id + ')" title="Cancel"><i class="fa fa-ban"></i></button>' : '')
            + '</td></tr>';
    }, { colspan: 9, empty: 'No leave requests found.', label: 'request' });
}

function loadTypes() {
    $.getJSON('/api/leave/types', function (d) {
        allTypes = d;
        renderTypes(d);
        var o = '<option value="">-- Leave Type --</option>';
        d.filter(function(x){return x.IsActive;}).forEach(function (t) { o += '<option value="' + t.Id + '">' + t.Name + '</option>'; });
        $('#leaveType').html(o);
    }).fail(function () { $('#typeBody').html('<tr><td colspan="6" class="text-danger text-center py-3">Failed to load.</td></tr>'); });
}

function renderTypes(data) {
    amsPage('#typeBody', data, function (t, i) {
        return '<tr><td class="text-muted">' + (i+1) + '</td><td class="fw-semibold">' + esc(t.Name) + '</td>'
            + '<td>' + esc(t.TotalDays) + '</td>'
            + '<td>' + (t.IsPaid ? '<span class="badge bg-success">Paid</span>' : '<span class="badge bg-secondary">Unpaid</span>') + '</td>'
            + '<td>' + (t.IsActive ? '<span class="badge bg-success">Active</span>' : '<span class="badge bg-danger">Inactive</span>') + '</td>'
            + '<td><button class="btn btn-sm btn-outline-primary me-1" onclick="editType(' + t.Id + ')" title="Edit"><i class="fa fa-pencil"></i></button>'
            + '<button class="btn btn-sm btn-outline-danger" onclick="deleteType(' + t.Id + ')" title="Delete"><i class="fa fa-trash"></i></button></td></tr>';
    }, { colspan: 6, empty: 'No leave types defined.', label: 'leave type' });
}

function openApplyModal() { $('#applyModal').find('select,input,textarea').val(''); new bootstrap.Modal('#applyModal').show(); }

function applyLeave() {
    var emp = parseInt($('#leaveEmp').val()), lt = parseInt($('#leaveType').val());
    var frm = $('#leaveFrom').val(), to = $('#leaveTo').val(), reason = $('#leaveReason').val().trim();
    if (!emp || !lt || !frm || !to || !reason) { notifyError('All fields are required.', 'Validation Error'); return; }
    $.ajax({ url: '/api/leave/requests', type: 'POST', contentType: 'application/json',
        data: JSON.stringify({ EmployeeId: emp, LeaveTypeId: lt, FromDate: frm, ToDate: to, Reason: reason }),
        success: function () { 
            bootstrap.Modal.getInstance('#applyModal').hide(); 
            notifySuccess('Leave request submitted successfully.');
            loadRequests(); 
        },
        error: function (xhr) { notifyError(xhr.responseText || 'Apply failed.'); }
    });
}

function approveReject(id, isApproved) {
    if (isApproved) {
        notifyConfirm({ title: 'Approve Leave', text: 'Approve this leave request?', confirmText: 'Approve', icon: 'question' }, function () {
            doApproveReject(id, true, '');
        });
    } else {
        if (typeof Swal !== 'undefined') {
            Swal.fire({
                title: 'Reject Leave Request',
                input: 'textarea',
                inputPlaceholder: 'Enter rejection reason...',
                showCancelButton: true,
                confirmButtonText: 'Reject Request',
                confirmButtonColor: '#d33',
                inputValidator: function (value) {
                    if (!value) return 'You need to write a rejection reason!';
                }
            }).then(function (result) {
                if (result.isConfirmed) {
                    doApproveReject(id, false, result.value);
                }
            });
        } else {
            var reason = prompt('Rejection reason:');
            if (reason !== null) doApproveReject(id, false, reason);
        }
    }
}

function doApproveReject(id, isApproved, reason) {
    $.ajax({ url: '/api/leave/requests/approve', type: 'POST', contentType: 'application/json',
        data: JSON.stringify({ LeaveRequestId: id, IsApproved: isApproved, RejectionReason: reason }),
        success: function () { 
            notifySuccess(isApproved ? 'Leave request approved.' : 'Leave request rejected.');
            loadRequests(); 
        },
        error: function (xhr) { notifyError(xhr.responseText || 'Action failed.'); }
    });
}

function cancel(id) {
    notifyConfirm({ title: 'Cancel Leave Request', text: 'Are you sure you want to cancel this leave request?', confirmText: 'Cancel Request', icon: 'warning' }, function () {
        $.ajax({ url: '/api/leave/requests/' + id + '/cancel', type: 'POST',
            success: function () { 
                notifySuccess('Leave request cancelled.');
                loadRequests(); 
            },
            error: function (xhr) { notifyError(xhr.responseText || 'Cancel failed.'); }
        });
    });
}

function openTypeModal() { $('#typeId').val(0); $('#typeName').val(''); $('#typeDays').val(10); $('#typePaid').prop('checked',true); $('#typeActive').prop('checked',true); $('#typeModalTitle').text('Add Leave Type'); new bootstrap.Modal('#typeModal').show(); }

function editType(id) {
    var t = allTypes.find(function(x){return x.Id===id;});
    $('#typeId').val(t.Id); $('#typeName').val(t.Name); $('#typeDays').val(t.TotalDays);
    $('#typePaid').prop('checked',t.IsPaid); $('#typeActive').prop('checked',t.IsActive);
    $('#typeModalTitle').text('Edit Leave Type'); new bootstrap.Modal('#typeModal').show();
}

function saveType() {
    var name = $('#typeName').val().trim(), days = parseInt($('#typeDays').val());
    if (!name || !days) { notifyError('Name and Total Days are required.', 'Validation Error'); return; }
    var dto = { Id: parseInt($('#typeId').val())||0, Name: name, TotalDays: days, IsPaid: $('#typePaid').is(':checked'), IsActive: $('#typeActive').is(':checked') };
    $.ajax({ url: '/api/leave/types', type: 'POST', contentType: 'application/json', data: JSON.stringify(dto),
        success: function () { 
            bootstrap.Modal.getInstance('#typeModal').hide(); 
            notifySuccess('Leave type saved successfully.');
            loadTypes(); 
        },
        error: function (xhr) { notifyError(xhr.responseText || 'Save failed.'); }
    });
}

function deleteType(id) {
    notifyConfirm({ title: 'Delete Leave Type', text: 'Are you sure you want to delete this leave type?', confirmText: 'Delete', icon: 'warning' }, function () {
        $.ajax({ url: '/api/leave/types/' + id, type: 'DELETE',
            success: function () { 
                notifySuccess('Leave type deleted.');
                loadTypes(); 
            },
            error: function (xhr) { notifyError(xhr.responseText || 'Delete failed.'); }
        });
    });
}

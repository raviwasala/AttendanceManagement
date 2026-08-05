/* ── Employee self-service: My Leave ── */

// Held so the apply modal can show the remaining balance for the type being picked,
// without a second request.
var myBalances = [];

$(function () { loadMyLeave(); });

function loadMyLeave() {
    $.getJSON('/api/me/leave', function (d) {
        $('#myLeaveYear').text(d.Year);
        myBalances = d.Balances || [];
        renderBalances(d.Balances);
        renderRequests(d.Requests);
        $('#myPending').text(d.PendingCount
            ? d.PendingCount + ' awaiting approval'
            : 'nothing pending');
    }).fail(function (xhr) {
        $('#myLeaveError').removeClass('d-none').text(xhr.responseText || 'Failed to load your leave.');
        $('#myBalances').html('<div class="text-muted small">Unavailable.</div>');
        $('#myLeaveBody').html('<tr><td colspan="6" class="text-center py-3 text-muted">Unavailable.</td></tr>');
    });
}

function renderBalances(balances) {
    if (!balances || !balances.length) {
        $('#myBalances').html('<div class="text-muted small">No leave types configured.</div>');
        return;
    }

    $('#myBalances').html(balances.map(function (b) {
        var pct = b.Entitled > 0 ? Math.min(100, Math.round(b.Used / b.Entitled * 100)) : 0;
        // Amber once most of the entitlement is gone — the number people actually care about
        // is what is left, not what is used.
        var bar = pct >= 80 ? 'bg-danger' : pct >= 50 ? 'bg-warning' : 'bg-success';

        return '<div class="mb-3">'
             + '<div class="d-flex justify-content-between small mb-1">'
             + '<span class="fw-medium">' + esc(b.LeaveType)
             + (b.IsPaid ? '' : ' <span class="badge bg-light text-muted">unpaid</span>') + '</span>'
             + '<span><strong>' + b.Remaining + '</strong> <span class="text-muted">of ' + b.Entitled + ' left</span></span>'
             + '</div>'
             + '<div class="progress" style="height:6px;">'
             + '<div class="progress-bar ' + bar + '" role="progressbar" style="width:' + pct + '%"'
             + ' aria-valuenow="' + pct + '" aria-valuemin="0" aria-valuemax="100"></div>'
             + '</div></div>';
    }).join(''));
}

function statusBadge(s) {
    var map = { Approved:'success', Pending:'warning', Rejected:'danger', Cancelled:'secondary' };
    return '<span class="badge bg-' + (map[s] || 'light text-dark') + '">' + esc(s) + '</span>';
}

function fmtDate(iso) {
    return iso ? new Date(iso).toLocaleDateString(undefined, { day:'2-digit', month:'short', year:'numeric' }) : '—';
}

function renderRequests(rows) {
    if (!rows || !rows.length) {
        $('#myLeaveBody').html('<tr><td colspan="6" class="text-center py-4 text-muted">You have no leave requests.</td></tr>');
        return;
    }

    $('#myLeaveBody').html(rows.map(function (r) {
        // Only a pending request can be withdrawn. Cancelling an approved one is a
        // conversation with an approver, not a button — the day may already be rostered
        // around, and the balance was released on approval.
        var canCancel = r.StatusDisplay === 'Pending';

        return '<tr>'
            + '<td class="ps-3 small">' + esc(r.LeaveType) + '</td>'
            + '<td class="small">' + fmtDate(r.FromDate) + ' – ' + fmtDate(r.ToDate) + '</td>'
            + '<td class="text-center small">' + r.TotalDays + '</td>'
            + '<td>' + statusBadge(r.StatusDisplay)
              // A rejection without the reason is the most frustrating possible outcome.
              + (r.RejectionReason
                    ? '<div class="text-danger" style="font-size:.7rem;">' + esc(r.RejectionReason) + '</div>' : '')
              + '</td>'
            + '<td class="small text-muted">' + esc(r.Reason || '') + '</td>'
            + '<td class="pe-3 text-center">'
            + (canCancel
                ? '<button class="btn btn-sm btn-outline-secondary py-0 px-2" title="Withdraw this request"'
                  + ' onclick="cancelMyLeave(' + r.Id + ')"><i class="fa fa-ban"></i></button>'
                : '<span class="text-muted">—</span>')
            + '</td></tr>';
    }).join(''));
}

/* ── Applying ─────────────────────────────────────────────────────────────── */

function openApplyLeave() {
    if (!myBalances.length) {
        notifyError('No leave types are configured yet. Ask an administrator to set them up.');
        return;
    }

    $('#alType').html(myBalances.map(function (b) {
        return '<option value="' + esc(b.LeaveTypeId) + '">' + esc(b.LeaveType) + '</option>';
    }).join(''));

    var today = new Date().toISOString().split('T')[0];
    $('#alFrom').val(today);
    $('#alTo').val(today);
    $('#alReason').val('');

    showBalanceHint();
    $('#alType').off('change.al').on('change.al', showBalanceHint);

    new bootstrap.Modal('#applyLeaveModal').show();
}

/* Shows what is left before the request is made, rather than rejecting it afterwards. */
function showBalanceHint() {
    var id = parseInt($('#alType').val());
    var b = myBalances.filter(function (x) { return x.LeaveTypeId === id; })[0];
    if (!b) { $('#alBalanceHint').text(''); return; }

    $('#alBalanceHint').html(b.Remaining > 0
        ? '<span class="text-success">' + b.Remaining + ' day(s) remaining</span> of ' + b.Entitled + '.'
        : '<span class="text-danger">No days remaining</span> on this type — the request will be refused.');
}

function submitApplyLeave() {
    var from = $('#alFrom').val(), to = $('#alTo').val();
    var reason = $('#alReason').val().trim();

    if (!$('#alType').val()) { notifyError('Choose a leave type.'); return; }
    if (!from || !to) { notifyError('Choose both a start and an end date.'); return; }
    if (to < from) { notifyError('The end date must be on or after the start date.'); return; }
    if (!reason) { notifyError('A reason is required.'); return; }

    $.ajax({
        url: '/api/me/leave', type: 'POST', contentType: 'application/json',
        data: JSON.stringify({
            LeaveTypeId: parseInt($('#alType').val()),
            FromDate: from, ToDate: to, Reason: reason
        }),
        success: function () {
            bootstrap.Modal.getInstance('#applyLeaveModal').hide();
            notifySuccess('Leave request submitted for approval.');
            loadMyLeave();
        },
        // The server carries the balance and overlap rules; surfacing its message verbatim
        // is more useful than a generic failure.
        error: function (xhr) { notifyError(xhr.responseText || 'Could not submit your request.'); }
    });
}

function cancelMyLeave(id) {
    notifyConfirm({
        title: 'Withdraw request',
        text: 'This will withdraw your pending leave request.',
        confirmText: 'Withdraw', icon: 'warning'
    }, function () {
        $.ajax({
            url: '/api/me/leave/' + id + '/cancel', type: 'POST',
            success: function () { notifySuccess('Request withdrawn.'); loadMyLeave(); },
            error: function (xhr) { notifyError(xhr.responseText || 'Could not withdraw that request.'); }
        });
    });
}

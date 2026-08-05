/* ── Employee self-service: My Leave ── */

$(function () { loadMyLeave(); });

function loadMyLeave() {
    $.getJSON('/api/me/leave', function (d) {
        $('#myLeaveYear').text(d.Year);
        renderBalances(d.Balances);
        renderRequests(d.Requests);
        $('#myPending').text(d.PendingCount
            ? d.PendingCount + ' awaiting approval'
            : 'nothing pending');
    }).fail(function (xhr) {
        $('#myLeaveError').removeClass('d-none').text(xhr.responseText || 'Failed to load your leave.');
        $('#myBalances').html('<div class="text-muted small">Unavailable.</div>');
        $('#myLeaveBody').html('<tr><td colspan="5" class="text-center py-3 text-muted">Unavailable.</td></tr>');
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
        $('#myLeaveBody').html('<tr><td colspan="5" class="text-center py-4 text-muted">You have no leave requests.</td></tr>');
        return;
    }

    $('#myLeaveBody').html(rows.map(function (r) {
        return '<tr>'
            + '<td class="ps-3 small">' + esc(r.LeaveType) + '</td>'
            + '<td class="small">' + fmtDate(r.FromDate) + ' – ' + fmtDate(r.ToDate) + '</td>'
            + '<td class="text-center small">' + r.TotalDays + '</td>'
            + '<td>' + statusBadge(r.StatusDisplay)
              // A rejection without the reason is the most frustrating possible outcome.
              + (r.RejectionReason
                    ? '<div class="text-danger" style="font-size:.7rem;">' + esc(r.RejectionReason) + '</div>' : '')
              + '</td>'
            + '<td class="pe-3 small text-muted">' + esc(r.Reason || '') + '</td>'
            + '</tr>';
    }).join(''));
}

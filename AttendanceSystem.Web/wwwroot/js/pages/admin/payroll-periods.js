/* ── Payroll Months ── */

var ppPeriods = [];

$(function () { ppLoad(); });

function ppLoad() {
    $.getJSON('/api/payroll-period', function (d) {
        ppPeriods = d || [];
        ppRenderCurrent();
        ppRenderHistory();
    }).fail(function (xhr) {
        $('#ppBody').html('<tr><td colspan="4" class="text-danger text-center py-4">'
            + esc(xhr.responseText || 'Failed to load.') + '</td></tr>');
    });
}

function ppOpenPeriod() {
    return ppPeriods.filter(function (p) { return p.StatusDisplay === 'Open'; })[0];
}

function ppRenderCurrent() {
    var open = ppOpenPeriod();

    if (!open) {
        // The state is called out rather than shown as an empty card, because "no month is
        // open" is why the entry screens are refusing to default — and that connection is
        // not obvious from the other end.
        $('#ppCurrent').html(
            '<div class="alert alert-warning py-2 small mb-3">'
            + '<i class="feather icon-alert-triangle me-1"></i>'
            + 'No payroll month is open. Entry screens will not default their month until one is.'
            + '</div>'
            + '<button class="btn btn-primary w-100" onclick="ppOpenModal()">Open a Month</button>');
        return;
    }

    $('#ppCurrent').html(
        '<div class="text-muted small">Working month</div>'
        + '<h4 class="mb-1">' + esc(open.MonthDisplay) + '</h4>'
        + '<span class="badge bg-success mb-3">' + esc(open.StatusDisplay) + '</span>'
        + (open.Notes ? '<div class="small text-muted mb-3">' + esc(open.Notes) + '</div>' : '')
        + '<button class="btn btn-outline-primary w-100" onclick="ppClose()">'
        + 'Close ' + esc(open.MonthDisplay) + ' &amp; open the next</button>');
}

function ppRenderHistory() {
    if (!ppPeriods.length) {
        $('#ppBody').html('<tr><td colspan="4" class="text-center py-4 text-muted">'
            + 'No payroll month has been opened yet.</td></tr>');
        return;
    }

    $('#ppBody').html(ppPeriods.map(function (p) {
        var badge = p.StatusDisplay === 'Open'
            ? '<span class="badge bg-success">Open</span>'
            : p.StatusDisplay === 'Paid'
                ? '<span class="badge bg-dark">Paid</span>'
                : '<span class="badge bg-secondary">Closed</span>';

        return '<tr>'
             + '<td class="ps-3 fw-semibold">' + esc(p.MonthDisplay) + '</td>'
             + '<td class="text-center">' + badge + '</td>'
             + '<td class="small text-muted">'
             + (p.ApprovedAt ? esc(new Date(p.ApprovedAt).toLocaleDateString()) : '—') + '</td>'
             + '<td class="text-end pe-3">'
             + (p.StatusDisplay === 'Closed'
                    ? '<button class="btn btn-sm btn-outline-secondary" onclick="ppReopen('
                      + p.Id + ')">Reopen</button>'
                    : '')
             + '</td></tr>';
    }).join(''));
}

function ppOpenModal() {
    // Suggests the month after the last one, which is the only value that will be accepted
    // anyway — the server refuses gaps.
    var last = ppPeriods[0];
    var next;

    if (last) {
        next = new Date(last.Year, last.Month, 1);   // Month is 1-based, so this is the next
        $('#ppOpenHint').text('The next payroll month after ' + last.MonthDisplay + '.');
    } else {
        var now = new Date();
        next = new Date(now.getFullYear(), now.getMonth(), 1);
        $('#ppOpenHint').text('The first payroll month. Choose the month you are about to pay, '
            + 'which is usually the one just finished.');
    }

    $('#ppOpenMonth').val(next.getFullYear() + '-' + String(next.getMonth() + 1).padStart(2, '0'));
    $('#ppOpenNotes').val('');
    new bootstrap.Modal('#ppOpenModal').show();
}

function ppOpen() {
    var v = $('#ppOpenMonth').val();
    if (!v) { notifyError('Choose a month.'); return; }

    var parts = v.split('-');
    var dto = {
        Year: parseInt(parts[0], 10),
        Month: parseInt(parts[1], 10),
        Notes: $('#ppOpenNotes').val() || null
    };

    $.ajax({ url: '/api/payroll-period/open', type: 'POST',
             contentType: 'application/json', data: JSON.stringify(dto) })
        .done(function (p) {
            bootstrap.Modal.getInstance('#ppOpenModal').hide();
            ppOk(p.MonthDisplay + ' is now the payroll month.');
            ppLoad();
        })
        .fail(function (xhr) { notifyError(xhr.responseText || 'Could not open the month.'); });
}

function ppClose() {
    var open = ppOpenPeriod();
    if (!open) return;

    var next = new Date(open.Year, open.Month, 1)
        .toLocaleString(undefined, { month: 'long', year: 'numeric' });

    // Spells out both halves. "Close" sounds like it only ends something, and somebody who
    // did not expect the next month to open would not know where their entries were going.
    notifyConfirm({
        title: 'Close ' + open.MonthDisplay + '?',
        text: 'It stops being the working month and ' + next + ' opens in its place. '
            + 'New entries will go to ' + next + ' from then on.',
        confirmText: 'Close and advance', icon: 'warning'
    }, function () {
        $.ajax({ url: '/api/payroll-period/close', type: 'POST' })
            .done(function (p) {
                ppOk('Closed. The payroll month is now ' + p.MonthDisplay + '.');
                ppLoad();
            })
            .fail(function (xhr) { notifyError(xhr.responseText || 'Could not close the month.'); });
    });
}

function ppReopen(id) {
    var p = ppPeriods.filter(function (x) { return x.Id === id; })[0];
    if (!p) return;

    // notifyPrompt rather than notifyConfirm: the server requires a reason, and the
    // confirm helper is suppressed by the "confirm before delete" setting.
    notifyPrompt({
        title: 'Reopen ' + p.MonthDisplay + '?',
        text: 'Reopening a closed month is recorded against your name.',
        placeholder: 'Why is this being reopened?',
        required: 'A reason is required.',
        confirmText: 'Reopen', icon: 'warning'
    }, function (reason) {
        $.ajax({ url: '/api/payroll-period/reopen', type: 'POST', contentType: 'application/json',
                 data: JSON.stringify({ Id: id, Reason: reason }) })
            .done(function () { ppOk(p.MonthDisplay + ' is open again.'); ppLoad(); })
            .fail(function (xhr) { notifyError(xhr.responseText || 'Could not reopen.'); });
    });
}

function ppOk(msg) {
    $('#ppAlert').html('<div class="alert alert-success alert-dismissible fade show py-2">'
        + esc(msg) + '<button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>');
}

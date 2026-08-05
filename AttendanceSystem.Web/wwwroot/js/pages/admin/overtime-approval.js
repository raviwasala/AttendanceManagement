/* ── Admin Overtime Approval ── */

var apData = null, apPage = 1;

$(function () {
    $.when(
        $.getJSON('/api/departments', function (d) {
            (d || []).filter(function (x) { return x.IsActive; }).forEach(function (x) {
                $('#apDept').append('<option value="' + esc(x.Id) + '">' + esc(x.Name) + '</option>');
            });
        }),
        $.getJSON('/api/employees', function (d) {
            (d || []).filter(function (x) { return x.IsActive; }).forEach(function (x) {
                $('#apEmployee').append('<option value="' + esc(x.Id) + '">'
                    + esc(x.EmployeeCode) + ' — ' + esc(x.FullName) + '</option>');
            });
        })
    ).always(loadApproval);

    $('#apDept, #apEmployee').on('change', function () { loadApproval(1); });
});

function loadApproval(page) {
    apPage = amsPageNo(page, apPage);

    var q = '?from=' + $('#apFrom').val() + '&to=' + $('#apTo').val() + '&status=1'
          + '&page=' + apPage + '&pageSize=' + (amsPageSize() || 25);
    if ($('#apDept').val())     q += '&departmentId=' + encodeURIComponent($('#apDept').val());
    if ($('#apEmployee').val()) q += '&employeeId=' + encodeURIComponent($('#apEmployee').val());

    $('#apBody').html('<tr><td colspan="9" class="text-center py-4 text-muted">Loading…</td></tr>');

    $.getJSON('/api/overtime/register' + q, function (d) { apData = d; renderApproval(); })
     .fail(function (xhr) {
         $('#apBody').html('<tr><td colspan="9" class="text-danger text-center py-3">'
             + esc(xhr.responseText || 'Failed to load overtime claims.') + '</td></tr>');
     });
}

function tile(label, value, colour) {
    return '<div class="col-6 col-md-3"><div class="card mb-0"><div class="card-body py-2 px-3">'
         + '<div class="text-muted" style="font-size:.72rem;">' + esc(label) + '</div>'
         + '<div class="h5 mb-0 text-' + colour + '">' + esc(value) + '</div>'
         + '</div></div></div>';
}

function renderApproval() {
    if (!apData) return;
    var canApprove = window.otPerms.approve;
    var cols = canApprove ? 9 : 8;

    // Range totals, not page totals — the server aggregates the whole filtered range.
    // "Weighted if approved" comes from claimed minutes rather than approved: nothing on this
    // screen is approved yet, so the approved-based figure would always read 0. Each row is
    // weighted by its own rate server-side, since holiday and ordinary overtime differ.
    $('#apStats').html(
        tile('Awaiting decision', apData.TotalCount, 'warning')
      + tile('Total claimed', apData.TotalClaimedDisplay, 'primary')
      + tile('On this page', apData.Rows.length, 'info')
      + tile('Weighted if approved',
             (apData.RangeClaimedWeightedHours || 0).toFixed(2) + ' h', 'success'));

    amsPage('#apBody', apData.Rows, function (r) {
        var dayBadge = r.DayTypeDisplay === 'Holiday'
            ? '<span class="badge bg-danger">Holiday</span>'
            : r.DayTypeDisplay === 'Weekly off'
                ? '<span class="badge bg-warning text-dark">Weekly off</span>'
                : '<span class="badge bg-light text-muted">Working</span>';

        var actions = canApprove
            ? '<button class="btn btn-sm btn-success me-1" onclick="openDecision(' + r.Id + ',true)" '
              + 'title="Approve"><i class="fa fa-check"></i></button>'
              + '<button class="btn btn-sm btn-outline-danger" onclick="openDecision(' + r.Id + ',false)" '
              + 'title="Reject"><i class="fa fa-times"></i></button>'
            : '<span class="text-muted small">—</span>';

        return '<tr>'
            + (canApprove
                ? '<td class="ps-3"><input type="checkbox" class="form-check-input ap-pick" value="' + r.Id + '"></td>'
                : '')
            + '<td class="small">' + esc(r.DateDisplay)
              + '<div class="text-muted" style="font-size:.68rem;">' + esc(r.DayName) + '</div></td>'
            // Department is a filter here and has its own column on the register; repeating it
            // in every row was what pushed the Decision buttons off the right edge.
            + '<td><div class="fw-semibold small">' + esc(r.EmployeeName) + '</div>'
              + '<div class="text-muted" style="font-size:.7rem;" title="' + esc(r.Department) + '">'
              + esc(r.EmployeeCode) + '</div></td>'
            + '<td class="small text-muted">' + (r.ShiftName ? esc(r.ShiftName) : '—') + '</td>'
            + '<td class="text-center small text-muted">'
              + (r.CheckInDisplay ? esc(r.CheckInDisplay) + ' – ' + esc(r.CheckOutDisplay || '?') : '—') + '</td>'
            + '<td class="text-center">' + dayBadge + '</td>'
            // Raw under claimed makes the rule's effect visible — otherwise a 97-minute stay
            // showing as 90 looks like a bug rather than rounding.
            + '<td class="text-center"><div class="fw-semibold">' + esc(r.ClaimedDisplay) + '</div>'
              + (r.RawMinutes !== r.ClaimedMinutes
                    ? '<div class="text-muted" style="font-size:.66rem;">from ' + esc(r.RawMinutes) + 'm</div>'
                    : '')
              + '</td>'
            + '<td class="small text-muted">' + (r.RuleName ? esc(r.RuleName) : '—')
              + '<div style="font-size:.68rem;">&times;' + esc(r.RateMultiplier) + '</div></td>'
            + '<td class="text-end pe-3">' + actions + '</td>'
            + '</tr>';
    }, {
        colspan: cols,
        empty: 'Nothing is waiting for approval in this range.'
             + (window.otPerms.generate ? ' Use <strong>Generate From Attendance</strong> if claims have not been created yet.' : ''),
        label: 'claim',
        server: {
            total: apData.TotalCount,
            page: apData.Page,
            pageSize: apData.PageSize,
            onPage: loadApproval
        }
    });

    bindPicks();
}

function bindPicks() {
    $('#apBody').off('change.ap').on('change.ap', '.ap-pick', updateBulkBar);
    updateBulkBar();
}

function selectedIds() {
    return $('.ap-pick:checked').map(function () { return parseInt(this.value, 10); }).get();
}

function updateBulkBar() {
    var n = selectedIds().length;
    $('#apSelCount').text(n + ' selected');
    $('#apBulkBar').toggleClass('d-none', n === 0);
    $('#apAll').prop('checked', n > 0 && n === $('.ap-pick').length);
}

function toggleAll(el) {
    $('.ap-pick').prop('checked', el.checked);
    updateBulkBar();
}

// ── Decisions ────────────────────────────────────────────────────────────────

function openDecision(id, approve) {
    var row = (apData.Rows || []).find(function (r) { return r.Id === id; });
    if (!row) return;

    $('#decideError').addClass('d-none').text('');
    $('#decideId').val(id);
    $('#decideApprove').val(approve ? '1' : '0');
    $('#decideTitle').text(approve ? 'Approve Overtime' : 'Reject Overtime');
    $('#decideSubject').html('<strong>' + esc(row.EmployeeName) + '</strong> — '
        + esc(row.DateDisplay) + ', ' + esc(row.ClaimedDisplay) + ' claimed');
    $('#decideMinutes').val(row.ClaimedMinutes).attr('max', row.ClaimedMinutes);
    $('#decideMinutesWrap').toggle(approve);
    $('#decideReasonLabel').text(approve ? 'Remarks (optional)' : 'Reason for rejection *');
    $('#decideReason').val('');
    $('#decideConfirm')
        .toggleClass('btn-primary', approve)
        .toggleClass('btn-danger', !approve)
        .text(approve ? 'Approve' : 'Reject');

    new bootstrap.Modal('#decideModal').show();
}

function submitDecision() {
    var approve = $('#decideApprove').val() === '1';
    var reason = ($('#decideReason').val() || '').trim();

    if (!approve && !reason) {
        $('#decideError').removeClass('d-none').text('A reason is required when rejecting overtime.');
        return;
    }

    post({
        Ids: [parseInt($('#decideId').val(), 10)],
        Approve: approve,
        ApprovedMinutes: approve ? (parseInt($('#decideMinutes').val(), 10) || 0) : null,
        Reason: reason || null
    }, function () { bootstrap.Modal.getInstance('#decideModal').hide(); });
}

function decideSelected(approve) {
    var ids = selectedIds();
    if (!ids.length) { notifyError('Select at least one claim.'); return; }

    if (approve) {
        notifyConfirm({
            title: 'Approve ' + ids.length + ' claim(s)?',
            text: 'Each claim is approved for the minutes it claimed.',
            confirmText: 'Approve', icon: 'question'
        }, function () { post({ Ids: ids, Approve: true }, null); });
        return;
    }

    // Rejection needs a reason, so it cannot be a plain confirm.
    $('#decideError').addClass('d-none').text('');
    $('#decideId').val('');
    $('#decideApprove').val('0');
    $('#decideTitle').text('Reject ' + ids.length + ' claim(s)');
    $('#decideSubject').text('All selected claims will be rejected with the reason below.');
    $('#decideMinutesWrap').hide();
    $('#decideReasonLabel').text('Reason for rejection *');
    $('#decideReason').val('');
    $('#decideConfirm').removeClass('btn-primary').addClass('btn-danger').text('Reject')
        .off('click').on('click', function () {
            var reason = ($('#decideReason').val() || '').trim();
            if (!reason) {
                $('#decideError').removeClass('d-none').text('A reason is required when rejecting overtime.');
                return;
            }
            post({ Ids: ids, Approve: false, Reason: reason },
                 function () { bootstrap.Modal.getInstance('#decideModal').hide(); });
        });

    new bootstrap.Modal('#decideModal').show();
}

function post(dto, onDone) {
    $.ajax({
        url: '/api/overtime/decide', type: 'POST',
        contentType: 'application/json', data: JSON.stringify(dto),
        success: function (res) {
            if (onDone) onDone();
            notifySuccess((res.Changed || dto.Ids.length) + ' claim(s) '
                + (dto.Approve ? 'approved' : 'rejected') + '.');
            // Restore the single-row handler the bulk path replaced.
            $('#decideConfirm').off('click').on('click', submitDecision);
            loadApproval();
        },
        error: function (xhr) {
            $('#decideError').removeClass('d-none').text(xhr.responseText || 'The decision could not be saved.');
            notifyError(xhr.responseText || 'The decision could not be saved.');
        }
    });
}

// ── Generation ───────────────────────────────────────────────────────────────

function generateOt() {
    $('#genFrom').val($('#apFrom').val());
    $('#genTo').val($('#apTo').val());
    $('#genResult').addClass('d-none').text('');
    new bootstrap.Modal('#genModal').show();
}

function runGenerate() {
    var dto = { From: $('#genFrom').val(), To: $('#genTo').val() };
    if (!dto.From || !dto.To) { notifyError('Choose a date range.'); return; }

    $('#genResult').removeClass('d-none')
        .html('<span class="spinner-border spinner-border-sm me-2"></span>Scanning attendance…');

    $.ajax({
        url: '/api/overtime/generate', type: 'POST',
        contentType: 'application/json', data: JSON.stringify(dto),
        success: function (res) {
            $('#genResult').html(esc(res.Summary));
            loadApproval();
        },
        error: function (xhr) {
            $('#genResult').html('<span class="text-danger">'
                + esc(xhr.responseText || 'Generation failed.') + '</span>');
        }
    });
}

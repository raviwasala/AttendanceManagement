/* ── Increment Confirmation ─────────────────────────────────────────────────────
   Proposals waiting for approval. Confirming here is what actually changes pay.
   ───────────────────────────────────────────────────────────────────────────── */

var icRows = [], icSelected = {};

$(function () {
    $.getJSON('/api/departments', function (d) {
        (d || []).filter(function (x) { return x.IsActive; }).forEach(function (x) {
            $('#icDept').append('<option value="' + esc(x.Name) + '">' + esc(x.Name) + '</option>');
        });
    });

    icLoad();
});

function icLoad() {
    $.getJSON('/api/salary-increment/pending', function (d) {
        icRows = d || [];
        icSelected = {};
        icRender();
    }).fail(function (xhr) {
        $('#icBody').html('<tr><td colspan="9" class="text-danger text-center py-4">'
            + esc(xhr.responseText || 'Failed to load.') + '</td></tr>');
    });
}

function icShown() {
    var term = ($('#icSearch').val() || '').trim().toLowerCase();
    var dept = $('#icDept').val();

    return icRows.filter(function (r) {
        if (term && (r.EmployeeCode + ' ' + r.EmployeeName).toLowerCase().indexOf(term) === -1) return false;
        if (dept && r.DepartmentName !== dept) return false;
        return true;
    });
}

function icRender() {
    var shown = icShown();

    if (!icRows.length) {
        $('#icBody').html('<tr><td colspan="9" class="text-center py-4 text-muted">'
            + 'Nothing is waiting for confirmation. Propose increments on '
            + '<a href="/Admin/SalaryIncrements">Salary Increments</a>.</td></tr>');
        $('#icCount').text('');
        icUpdateTotals();
        return;
    }

    if (!shown.length) {
        $('#icBody').html('<tr><td colspan="9" class="text-center py-4 text-muted">'
            + 'No pending increment matches these filters.</td></tr>');
        icUpdateTotals();
        return;
    }

    $('#icBody').html(shown.map(function (r) {
        var join = new Date(r.JoiningDate).toLocaleDateString();
        var inc = new Date(r.EffectiveDate).toLocaleDateString();

        return '<tr>'
             + '<td class="ps-3"><input type="checkbox" value="' + esc(r.Id) + '"'
             + (icSelected[r.Id] ? ' checked' : '') + ' onchange="icToggle(this)"></td>'
             + '<td class="fw-semibold">' + esc(r.EmployeeCode) + '</td>'
             + '<td>' + esc(r.EmployeeName)
             + '<div class="small text-muted">' + esc(r.DepartmentName) + '</div></td>'
             + '<td class="text-end">' + parseFloat(r.BasicSalary).toFixed(2) + '</td>'
             // Service alongside the joining date, because the joining date on its own needs
             // arithmetic before it means anything — and years of service is usually the
             // thing being judged.
             + '<td class="small">' + esc(join)
             + '<div class="text-muted">' + esc(r.YearsOfService) + ' yr</div></td>'
             + '<td class="small">' + esc(inc) + '</td>'
             + '<td class="small">' + esc(r.Condition)
             + (r.BatchId ? ' <span class="badge bg-light text-dark">batch</span>' : '') + '</td>'
             + '<td class="text-end text-success">+' + parseFloat(r.IncrementAmount).toFixed(2)
             + '<div class="small text-muted">' + esc(r.BasisDisplay) + '</div></td>'
             + '<td class="text-end fw-semibold pe-3">' + parseFloat(r.NewBasic).toFixed(2) + '</td>'
             + '</tr>';
    }).join(''));

    $('#icCount').text(shown.length + ' of ' + icRows.length + ' shown');
    icUpdateTotals();
}

function icToggle(el) {
    var id = parseInt(el.value, 10);
    if (el.checked) icSelected[id] = true; else delete icSelected[id];
    icUpdateTotals();
}

/* Select-all covers the rows currently SHOWN, not every pending row. Ticking a header box
   while a department filter is on and silently approving the other two hundred would be an
   expensive surprise. */
function icToggleAll(el) {
    icShown().forEach(function (r) {
        if (el.checked) icSelected[r.Id] = true; else delete icSelected[r.Id];
    });
    icRender();
}

function icSelectedRows() {
    return icRows.filter(function (r) { return icSelected[r.Id]; });
}

function icUpdateTotals() {
    var sel = icSelectedRows();
    var total = sel.reduce(function (s, r) { return s + parseFloat(r.IncrementAmount); }, 0);

    $('#icTotal').text(total.toFixed(2));
    $('#icConfirmBtn, #icRejectBtn').prop('disabled', sel.length === 0);
    $('#icConfirmBtn').html('<i class="feather icon-check me-1"></i>Confirm'
        + (sel.length ? ' (' + sel.length + ')' : ''));
}

function icConfirm() {
    var sel = icSelectedRows();
    if (!sel.length) return;

    var total = sel.reduce(function (s, r) { return s + parseFloat(r.IncrementAmount); }, 0);

    // Says plainly that this is the moment pay changes, and gives the annual figure — a
    // monthly increase reads small, twelve times it does not.
    notifyConfirm({
        title: 'Confirm ' + sel.length + ' increment(s)?',
        text: 'Their basic salary changes now, costing ' + total.toFixed(2) + ' more per month — '
            + (total * 12).toFixed(2) + ' a year. This is the step that actually raises pay.',
        confirmText: 'Confirm', icon: 'warning'
    }, function () {
        $.ajax({ url: '/api/salary-increment/confirm', type: 'POST', contentType: 'application/json',
                 data: JSON.stringify({ Ids: sel.map(function (r) { return r.Id; }) }) })
            .done(function (res) { icOk(res.Summary); icLoad(); })
            .fail(function (xhr) { notifyError(xhr.responseText || 'Could not confirm.'); });
    });
}

function icReject() {
    var sel = icSelectedRows();
    if (!sel.length) return;

    notifyPrompt({
        title: 'Reject ' + sel.length + ' increment(s)?',
        text: 'They are kept with your reason, so the same proposal is not simply made again.',
        placeholder: 'Why are these being turned down?',
        required: 'A reason is required.',
        confirmText: 'Reject', icon: 'warning'
    }, function (reason) {
        $.ajax({ url: '/api/salary-increment/reject', type: 'POST', contentType: 'application/json',
                 data: JSON.stringify({ Ids: sel.map(function (r) { return r.Id; }), Reason: reason }) })
            .done(function (res) { icOk(res.Summary); icLoad(); })
            .fail(function (xhr) { notifyError(xhr.responseText || 'Could not reject.'); });
    });
}

function icOk(msg) {
    $('#icAlert').html('<div class="alert alert-success alert-dismissible fade show py-2">'
        + esc(msg) + '<button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>');
}

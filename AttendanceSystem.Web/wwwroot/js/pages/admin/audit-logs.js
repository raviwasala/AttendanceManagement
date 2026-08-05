/* ── Admin Audit Log ── */

var auditRows = [];

$(function () {
    $('#alModule').on('change', loadAudit);
    $('#alCount').on('change', loadAudit);
    loadAudit();
});

function loadAudit() {
    var module = $('#alModule').val(), count = $('#alCount').val();
    $('#auditBody').html('<tr><td colspan="6" class="text-center py-4 text-muted">Loading…</td></tr>');

    $.getJSON('/api/audit-logs?count=' + count + (module ? '&module=' + encodeURIComponent(module) : ''),
        function (d) {
            auditRows = d || [];
            renderAudit();
        })
     .fail(function (xhr) {
         $('#auditBody').html('<tr><td colspan="6" class="text-danger text-center py-3">' +
             esc(xhr.responseText || 'Failed to load audit log.') + '</td></tr>');
     });
}

function actionBadge(a) {
    var map = {
        Create: 'success', Update: 'primary', Edit: 'primary', Delete: 'danger',
        Login: 'info', Logout: 'secondary', LoginViaRememberToken: 'info',
        ChangePassword: 'warning', ResetPasswordWithToken: 'warning',
        RequestPasswordReset: 'warning', TestConnection: 'secondary'
    };
    return '<span class="badge bg-' + (map[a] || 'light text-dark') + '">' + esc(a) + '</span>';
}

/** Values are stored as JSON strings; show them readably but never as markup. */
function changeCell(r) {
    if (!r.OldValues && !r.NewValues) return '<span class="text-muted">—</span>';
    var pretty = function (v) {
        if (!v) return '';
        try { return JSON.stringify(JSON.parse(v), null, 1); } catch (e) { return v; }
    };
    var body = (r.OldValues ? 'before: ' + pretty(r.OldValues) + '\n' : '')
             + (r.NewValues ? 'after: ' + pretty(r.NewValues) : '');
    return '<details><summary class="small text-primary" style="cursor:pointer;">view</summary>'
         + '<pre class="small mb-0 mt-1" style="white-space:pre-wrap;max-width:420px;">'
         + esc(body) + '</pre></details>';
}

function renderAudit() {
    var q = ($('#alSearch').val() || '').toLowerCase();
    var rows = auditRows.filter(function (r) {
        if (!q) return true;
        return (r.Username || '').toLowerCase().indexOf(q) >= 0
            || (r.Action || '').toLowerCase().indexOf(q) >= 0
            || (r.EntityName || '').toLowerCase().indexOf(q) >= 0;
    });

    // "No entries match" reads as a broken filter when the truth is that nothing has been
    // logged for the selected module at all — say which of the two it is.
    var empty = auditRows.length
        ? 'No entries match your search.'
        : ($('#alModule').val()
            ? 'Nothing has been logged for this module yet.'
            : 'No activity has been logged yet.');

    $('#auditCount').text(rows.length
        ? 'Showing ' + rows.length + ' of ' + auditRows.length + ' entries.'
        : '');

    amsPage('#auditBody', rows, function (r) {
        return '<tr>'
            + '<td class="ps-3 small text-muted" style="white-space:nowrap;">' + esc(r.CreatedAtDisplay) + '</td>'
            + '<td class="small">' + (r.Username
                ? esc(r.Username)
                : '<span class="text-muted" title="No signed-in user - system or background action">system</span>') + '</td>'
            + '<td class="small">' + esc(r.Module) + '</td>'
            + '<td>' + actionBadge(r.Action) + '</td>'
            + '<td class="small text-muted">' + (r.EntityName
                ? esc(r.EntityName) + (r.EntityId ? ' #' + r.EntityId : '') : '—') + '</td>'
            + '<td class="pe-3">' + changeCell(r) + '</td>'
            + '</tr>';
    }, { colspan: 6, empty: empty, label: 'entry', labelPlural: 'entries' });
}

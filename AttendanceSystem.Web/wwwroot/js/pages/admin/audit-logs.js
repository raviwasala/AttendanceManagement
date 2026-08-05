/* ── Admin Audit Log ── */

/*
 * Server-paged. The audit table is the only one that grows without bound, so the module
 * filter and the search both run in SQL and only one page ever reaches the browser.
 */
var auditPage = 1;
var auditSearchTimer = null;

$(function () {
    $('#alModule').on('change', function () { auditPage = 1; loadAudit(); });

    // Debounced: every keystroke is now a round trip, unlike the old in-memory filter.
    $('#alSearch').on('input', function () {
        clearTimeout(auditSearchTimer);
        auditSearchTimer = setTimeout(function () { auditPage = 1; loadAudit(); }, 300);
    });

    loadAudit();
});

function loadAudit(page) {
    auditPage = amsPageNo(page, auditPage);

    var module = $('#alModule').val();
    var search = ($('#alSearch').val() || '').trim();
    var size = amsPageSize() || 25;

    var url = '/api/audit-logs?page=' + auditPage + '&pageSize=' + size
            + (module ? '&module=' + encodeURIComponent(module) : '')
            + (search ? '&search=' + encodeURIComponent(search) : '');

    $('#auditBody').html('<tr><td colspan="6" class="text-center py-4 text-muted">Loading…</td></tr>');

    $.getJSON(url, renderAudit)
     .fail(function (xhr) {
         $('#auditBody').html('<tr><td colspan="6" class="text-danger text-center py-3">'
             + esc(xhr.responseText || 'Failed to load audit log.') + '</td></tr>');
     });
}

function actionBadge(a) {
    var map = {
        Create: 'success', Update: 'primary', Edit: 'primary', Delete: 'danger',
        Login: 'info', Logout: 'secondary', LoginViaRememberToken: 'info',
        ChangePassword: 'warning', ResetPasswordWithToken: 'warning',
        RequestPasswordReset: 'warning', TestConnection: 'secondary',
        Approve: 'success', Reject: 'danger', Generate: 'info'
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

function renderAudit(data) {
    data = data || { Items: [], TotalCount: 0, Page: 1, PageSize: 25 };
    auditPage = data.Page;

    // "No entries match" reads as a broken filter when the truth is that nothing has been
    // logged for the selected module at all — say which of the two it is.
    var searching = !!($('#alSearch').val() || '').trim();
    var empty = searching
        ? 'No entries match your search.'
        : ($('#alModule').val()
            ? 'Nothing has been logged for this module yet.'
            : 'No activity has been logged yet.');

    $('#auditCount').text(data.TotalCount
        ? data.TotalCount + ' matching ' + (data.TotalCount === 1 ? 'entry' : 'entries')
        : '');

    amsPage('#auditBody', data.Items, function (r) {
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
    }, {
        colspan: 6,
        empty: empty,
        label: 'entry',
        labelPlural: 'entries',
        server: {
            total: data.TotalCount,
            page: data.Page,
            pageSize: data.PageSize,
            onPage: loadAudit
        }
    });
}

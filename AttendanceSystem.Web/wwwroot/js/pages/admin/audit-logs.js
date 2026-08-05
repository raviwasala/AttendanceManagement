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

function parseJson(v) {
    if (!v) return null;
    try { return JSON.parse(v); } catch (e) { return null; }
}

function fmtValue(v) {
    if (v === null || v === undefined || v === '') return '<em class="text-muted">empty</em>';
    if (v === true) return 'yes';
    if (v === false) return 'no';
    if (Array.isArray(v)) return esc(v.join(', '));
    if (typeof v === 'object') return esc(JSON.stringify(v));
    return esc(v);
}

/*
 * Renders the before/after pair as a field-level diff.
 *
 * The service stores only the fields that actually changed, with the same keys on both sides,
 * so this can line them up as "Field: old → new" instead of asking the reader to spot the
 * difference between two thirty-line JSON dumps. Creates have no old side and deletes have no
 * new one; both fall back to a plain list.
 */
function changeCell(r) {
    var oldV = parseJson(r.OldValues);
    var newV = parseJson(r.NewValues);

    if (!oldV && !newV) {
        // Older entries, and the few places that record free text rather than JSON.
        var raw = r.NewValues || r.OldValues;
        return raw
            ? '<span class="small text-muted">' + esc(raw) + '</span>'
            : '<span class="text-muted">—</span>';
    }

    var keys = Object.keys(Object.assign({}, oldV || {}, newV || {}));
    if (!keys.length) return '<span class="text-muted">—</span>';

    var rows = keys.map(function (k) {
        var a = oldV ? oldV[k] : undefined;
        var b = newV ? newV[k] : undefined;

        // One-sided: a create or a delete. Show the value, not a "→ nothing" arrow that
        // implies the field was cleared.
        if (!oldV || !newV) {
            return '<tr><td class="pe-2 text-muted">' + esc(k) + '</td>'
                 + '<td colspan="2">' + fmtValue(oldV ? a : b) + '</td></tr>';
        }
        return '<tr><td class="pe-2 text-muted">' + esc(k) + '</td>'
             + '<td class="pe-2"><s class="text-danger">' + fmtValue(a) + '</s></td>'
             + '<td class="text-success fw-semibold">' + fmtValue(b) + '</td></tr>';
    }).join('');

    var label = !oldV ? keys.length + ' field' + (keys.length === 1 ? '' : 's') + ' set'
              : !newV ? keys.length + ' field' + (keys.length === 1 ? '' : 's') + ' removed'
              : keys.length + ' change' + (keys.length === 1 ? '' : 's');

    return '<details><summary class="small text-primary" style="cursor:pointer;">' + label + '</summary>'
         + '<table class="table table-sm table-borderless mb-0 mt-1" style="font-size:.72rem;">'
         + rows + '</table></details>';
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

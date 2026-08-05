/* ── Shared Site-wide JavaScript ── */

/*
 * Escapes text before it is placed into an HTML string.
 *
 * The table renderers build markup by concatenation, so any value that came from the
 * database is executed as markup unless it goes through here. An employee whose name is
 * `<img src=x onerror=alert(1)>` is enough to run script in every admin's browser.
 */
window.esc = function (value) {
    if (value === null || value === undefined) return '';
    return String(value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
};

/* ── Toastr Global Configuration & Notification Helpers ── */
if (typeof toastr !== 'undefined') {
    toastr.options = {
        "closeButton": true,
        "progressBar": true,
        "positionClass": "toast-top-right",
        "showDuration": "300",
        "hideDuration": "500",
        "timeOut": "4000"
    };
}

window.notifySuccess = function (msg, title) {
    if (typeof toastr !== 'undefined') toastr.success(msg, title || 'Success');
    else alert(msg);
};

window.notifyError = function (msg, title) {
    if (typeof toastr !== 'undefined') toastr.error(msg, title || 'Error');
    else alert(msg);
};

window.notifyConfirm = function (options, onConfirm) {
    // Settings → System Configuration can switch the prompts off for trusted sites; the
    // action then runs straight away. Default is to ask, including if config never loaded.
    if (window.amsConfig && window.amsConfig.confirmDelete === false) {
        if (typeof onConfirm === 'function') onConfirm();
        return;
    }

    var title = typeof options === 'string' ? options : (options.title || 'Are you sure?');
    var text = typeof options === 'object' ? (options.text || '') : '';
    var confirmText = typeof options === 'object' ? (options.confirmText || 'Yes, proceed!') : 'Yes, proceed!';
    var icon = typeof options === 'object' ? (options.icon || 'warning') : 'warning';

    if (typeof Swal !== 'undefined') {
        Swal.fire({
            title: title,
            text: text,
            icon: icon,
            showCancelButton: true,
            confirmButtonColor: '#00acac',
            cancelButtonColor: '#6c757d',
            confirmButtonText: confirmText,
            cancelButtonText: 'Cancel',
            customClass: {
                confirmButton: 'btn btn-primary me-2',
                cancelButton: 'btn btn-secondary'
            },
            buttonsStyling: false
        }).then(function (result) {
            if (result.isConfirmed && typeof onConfirm === 'function') {
                onConfirm();
            }
        });
    } else {
        if (confirm(title + (text ? '\n' + text : ''))) {
            if (typeof onConfirm === 'function') onConfirm();
        }
    }
};

/*
 * Table pagination.
 *
 * Every list screen already fetches its rows as one array and renders them by concatenation,
 * so paging is done here on the client: one helper, no change to any endpoint, and filters
 * keep working exactly as they did because they still filter the full array before it
 * arrives here.
 *
 *     amsPage('#empBody', filteredRows, function (r) { return '<tr>…</tr>'; },
 *             { colspan: 8, empty: 'No employees found.', label: 'employees' });
 *
 * Page size comes from Settings → System Configuration (window.amsConfig.pageSize); 0 there
 * means show everything, and the pager hides itself.
 */
(function () {
    var state = {};

    function configuredSize() {
        var n = window.amsConfig && window.amsConfig.pageSize;
        return (typeof n === 'number' && n > 0) ? n : 0;
    }

    /* The pager lives in a div created next to the table, so no view markup has to change. */
    function pagerFor($body) {
        var $table = $body.closest('table');
        var $host = $table.closest('.table-responsive');
        if (!$host.length) $host = $table.parent();

        var $pager = $host.next('.ams-pager');
        if (!$pager.length) {
            $pager = $('<div class="ams-pager"></div>');
            $host.after($pager);
        }
        return $pager;
    }

    function button(page, text, disabled, active) {
        return '<button type="button" class="ams-pager-btn'
             + (active ? ' active' : '') + '"'
             + (disabled ? ' disabled' : ' data-page="' + page + '"')
             + '>' + text + '</button>';
    }

    /* A window around the current page — a 40-page list must not render 40 buttons. */
    function windowed(current, pages) {
        var out = [], i;
        var from = Math.max(1, current - 2), to = Math.min(pages, current + 2);
        if (from > 1) { out.push(1); if (from > 2) out.push('…'); }
        for (i = from; i <= to; i++) out.push(i);
        if (to < pages) { if (to < pages - 1) out.push('…'); out.push(pages); }
        return out;
    }

    window.amsPage = function (bodySel, rows, rowHtml, opts) {
        opts = opts || {};
        var $body = $(bodySel);
        if (!$body.length) return;

        var st = state[bodySel] || (state[bodySel] = { page: 1 });

        // Any call that did not come from the pager itself is a fresh load or a changed
        // filter, and must land on page 1 — otherwise a narrow filter shows an empty page 7.
        if (!st.fromPager) st.page = 1;
        st.fromPager = false;
        st.redraw = function () { st.fromPager = true; window.amsPage(bodySel, rows, rowHtml, opts); };

        var colspan = opts.colspan || $body.closest('table').find('thead th').length || 1;
        var $pager = pagerFor($body);

        rows = rows || [];
        if (!rows.length) {
            $body.html('<tr><td colspan="' + colspan + '" class="text-center text-muted py-4">'
                     + (opts.empty || 'No records found.') + '</td></tr>');
            $pager.empty();
            return;
        }

        var size = configuredSize() || rows.length;
        var pages = Math.max(1, Math.ceil(rows.length / size));
        if (st.page > pages) st.page = pages;

        var start = (st.page - 1) * size;
        // Second argument is the row's position in the whole filtered set, not in the page,
        // so a "#" column keeps counting across pages instead of restarting at 1.
        var slice = rows.slice(start, start + size);
        $body.html(slice.map(function (r, i) { return rowHtml(r, start + i); }).join(''));

        // "branches", not "branchs" — irregular plurals are passed in explicitly.
        var one = opts.label || 'record';
        var many = opts.labelPlural || (one + 's');

        if (pages <= 1) {
            // Still say how many there are — "6 employees" is useful even with one page.
            $pager.html('<div class="ams-pager-info">' + rows.length + ' '
                      + (rows.length === 1 ? one : many) + '</div>');
            return;
        }

        var html = '<div class="ams-pager-info">Showing ' + (start + 1) + '–'
                 + Math.min(start + size, rows.length) + ' of ' + rows.length + ' '
                 + many + '</div><div class="ams-pager-btns">'
                 + button(st.page - 1, '‹', st.page === 1, false);

        windowed(st.page, pages).forEach(function (p) {
            html += (p === '…')
                ? '<span class="ams-pager-gap">…</span>'
                : button(p, p, false, p === st.page);
        });

        html += button(st.page + 1, '›', st.page === pages, false) + '</div>';
        $pager.html(html);

        $pager.off('click.amspage').on('click.amspage', '.ams-pager-btn[data-page]', function () {
            st.page = parseInt($(this).data('page'), 10);
            st.redraw();
        });
    };
})();

/*
 * Header notifications.
 *
 * Fetched once per page load from live data. Not polled: the counts change at human speed
 * (someone approves leave, someone fixes a device), and a background poll on every open tab
 * would cost more than it is worth. Navigating refreshes it.
 */
(function () {
    function severityClass(s) {
        return s === 2 ? 'sev-critical' : s === 1 ? 'sev-warning' : 'sev-info';
    }

    function render(data) {
        var badge = document.getElementById('notifBadge');
        var items = document.getElementById('notifItems');
        var count = document.getElementById('notifCount');
        if (!badge || !items) return;

        // The theme positions this badge absolutely and its rules beat `.d-none`,
        // so hide it with an inline style instead of a utility class.
        if (!data || !data.Items || data.Items.length === 0) {
            badge.style.display = 'none';
            if (count) count.textContent = '';
            items.innerHTML = '<div class="ams-notif-empty">'
                            + '<i class="feather icon-check-circle"></i>'
                            + 'All clear — nothing needs attention.</div>';
            return;
        }

        badge.textContent = data.TotalCount > 99 ? '99+' : data.TotalCount;
        badge.style.display = '';
        if (count) {
            count.textContent = data.Items.length + (data.Items.length === 1 ? ' item' : ' items');
        }

        items.innerHTML = data.Items.map(function (n) {
            var inner =
                '<span class="ams-notif-icon ' + severityClass(n.Severity) + '">'
              + '<i class="feather ' + esc(n.Icon) + '"></i></span>'
              + '<span class="ams-notif-text">'
              + '<span class="ams-notif-item-title">' + esc(n.Title)
              + (n.Count > 1 ? '<span class="ams-notif-pill">' + n.Count + '</span>' : '')
              + '</span>'
              + '<span class="ams-notif-msg">' + esc(n.Message) + '</span>'
              + '</span>'
              + (n.Url ? '<i class="feather icon-chevron-right ams-notif-chevron"></i>' : '');

            return n.Url
                ? '<a href="' + esc(n.Url) + '" class="ams-notif-item">' + inner + '</a>'
                : '<div class="ams-notif-item">' + inner + '</div>';
        }).join('');
    }

    function load() {
        if (!document.getElementById('notifBadge')) return;
        fetch('/api/notifications', { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(render)
            .catch(function () { /* the bell is not worth an error toast */ });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', load);
    } else {
        load();
    }
})();

document.addEventListener('DOMContentLoaded', function () {
    /* ── Logout confirmation ── */
    var trigger = document.getElementById('logoutTrigger');
    if (trigger) {
        trigger.addEventListener('click', function (e) {
            e.preventDefault();
            window.notifyConfirm({
                title: 'Log Out',
                text: 'Are you sure you want to end your current session?',
                confirmText: 'Log Out',
                icon: 'question'
            }, function () {
                document.getElementById('logoutForm').submit();
            });
        });
    }
});

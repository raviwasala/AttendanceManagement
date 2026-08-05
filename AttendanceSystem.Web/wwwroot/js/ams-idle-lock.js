/* ── Idle screen lock ─────────────────────────────────────────────────────────
   Locks the screen after a period without activity.

   The lock itself is server-side: this only decides *when* to ask for it. The
   session carries a Locked flag and SessionAuthorizeAttribute refuses every
   request while it is set, so a lock cannot be undone by refreshing, typing a
   URL, or calling the API directly. Without that, everything here would be an
   overlay somebody could dismiss with F5.

   Configured from Settings via window.amsConfig.screenLockMinutes; 0 disables it.
   ───────────────────────────────────────────────────────────────────────────── */
(function ($) {
    'use strict';

    var minutes = (window.amsConfig && window.amsConfig.screenLockMinutes) || 0;
    if (minutes <= 0) return;                 // disabled for this site

    var LOCK_MS = minutes * 60 * 1000;

    // Two minutes of warning, or a third of the period when that is shorter — a
    // 2-minute lock cannot give a 2-minute warning.
    var WARN_MS = Math.min(120000, Math.floor(LOCK_MS / 3));

    var lastActivity = Date.now();
    var $overlay = null;
    var ticker = null;

    /* Activity is anything a working person does. Scroll and touch are included
       because reading a long report is work, and a lock that fires mid-page is
       the kind people disable. */
    var events = 'mousemove mousedown keydown wheel touchstart scroll';
    $(document).on(events, function () {
        lastActivity = Date.now();
        if ($overlay) hideWarning();
    });

    // An AJAX call means the page is doing something on the user's behalf, even
    // if they have not touched the mouse — a long save, a report loading.
    $(document).ajaxSend(function () { lastActivity = Date.now(); });

    function idleFor() { return Date.now() - lastActivity; }

    function showWarning(secondsLeft) {
        if (!$overlay) {
            $overlay = $(
                '<div class="ams-idle-warn">' +
                '<div class="ams-idle-card">' +
                '<i class="feather icon-clock"></i>' +
                '<div class="ams-idle-text">' +
                '<strong>Locking soon</strong>' +
                '<div class="small">The screen will lock in <span class="ams-idle-count"></span>. ' +
                'Your work stays open.</div></div>' +
                '<button type="button" class="btn btn-sm btn-primary ams-idle-stay">Stay signed in</button>' +
                '</div></div>').appendTo('body');

            // Clicking the button counts as activity through the handler above; this
            // is only here so the overlay closes immediately rather than on the next tick.
            $overlay.on('click', '.ams-idle-stay', function () {
                lastActivity = Date.now();
                hideWarning();
            });
        }
        $overlay.find('.ams-idle-count').text(secondsLeft + ' second' + (secondsLeft === 1 ? '' : 's'));
    }

    function hideWarning() {
        if (!$overlay) return;
        $overlay.remove();
        $overlay = null;
    }

    function lockNow() {
        clearInterval(ticker);
        hideWarning();

        // Told to the server first, so the flag is set even if the navigation is
        // slow or the user closes the tab mid-redirect.
        $.post('/Auth/LockNow')
            .always(function () { window.location.href = '/Auth/Lock'; });
    }

    ticker = setInterval(function () {
        var idle = idleFor();

        if (idle >= LOCK_MS) { lockNow(); return; }

        if (idle >= LOCK_MS - WARN_MS) {
            showWarning(Math.max(1, Math.ceil((LOCK_MS - idle) / 1000)));
        } else if ($overlay) {
            hideWarning();
        }
    }, 1000);

    // Locking on demand, from the user menu.
    window.amsLockScreen = lockNow;

})(jQuery);

/* ── The current payroll month ───────────────────────────────────────────────────
   Every payroll entry screen defaults its month from here rather than from the clock.

   On the 3rd of August a clerk is still finishing July. A screen defaulting to "this
   calendar month" would put that morning's incentive into an August nobody has opened,
   and it would surface a month later on the wrong payslip with nothing having flagged it.

   Fetched once per page and shared, so five screens do not make five requests.
   ───────────────────────────────────────────────────────────────────────────── */
(function ($) {
    'use strict';

    var request = null;

    /**
     * Resolves with the open period, or null when none is open.
     * Never falls back to today's date — a screen inventing a month is the bug this exists
     * to prevent, so callers are told plainly that there is nothing open.
     */
    window.amsPayrollPeriod = function () {
        if (!request) request = $.getJSON('/api/payroll-period/current');
        return request;
    };

    /**
     * Points a <input type="month"> at the current payroll month and shows which month
     * that is beside it. Returns the same promise so a caller can chain its own load.
     *
     * `noticeSelector` is where the "no month is open" warning goes; without one the
     * screen would look like it simply failed to load.
     */
    window.amsBindPayrollMonth = function (monthSelector, noticeSelector, onReady) {
        return window.amsPayrollPeriod().done(function (p) {
            var $notice = $(noticeSelector);

            if (!p) {
                $(monthSelector).val('').prop('disabled', true);
                $notice.html('<div class="alert alert-warning py-2 small mb-0">'
                    + '<i class="feather icon-alert-triangle me-1"></i>'
                    + 'No payroll month is open. Open one under '
                    + '<a href="/Admin/PayrollPeriods">Payroll Months</a> before entering figures.'
                    + '</div>');
                if (onReady) onReady(null);
                return;
            }

            var ym = String(p.YearMonth);
            $(monthSelector).val(ym.substring(0, 4) + '-' + ym.substring(4, 6));

            // Says which month is current and that it can be overridden — a locked-down
            // month box would stop legitimate back-dated corrections.
            $notice.html('<div class="small text-muted">'
                + 'Payroll month: <strong>' + esc(p.MonthDisplay) + '</strong>'
                + ' <span class="badge bg-success">' + esc(p.StatusDisplay) + '</span>'
                + '<br>Change the month above to work on a different one.'
                + '</div>');

            if (onReady) onReady(p);
        }).fail(function () {
            if (onReady) onReady(null);
        });
    };

    /* A badge in the header, so the working month is visible from every screen rather than
       only the ones that ask for it. Somebody keying a whole afternoon of figures should not
       have to remember which month they are in. */
    $(function () {
        var $slot = $('#amsPeriodBadge');
        if (!$slot.length) return;

        window.amsPayrollPeriod().done(function (p) {
            $slot.html(p
                ? '<a href="/Admin/PayrollPeriods" class="ams-period-ok" title="Current payroll month">'
                  + '<i class="feather icon-calendar me-1"></i>' + esc(p.MonthDisplay) + '</a>'
                : '<a href="/Admin/PayrollPeriods" class="ams-period-none" '
                  + 'title="No payroll month is open">'
                  + '<i class="feather icon-alert-triangle me-1"></i>No payroll month</a>');
        });
    });

})(jQuery);

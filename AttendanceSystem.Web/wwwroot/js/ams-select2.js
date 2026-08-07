/* ── Searchable dropdowns via Select2 ─────────────────────────────────────────
   Turns every <select> into a type-to-filter dropdown.

   Select2 4.0.3 is the Adminty template's own component, vendored locally from
   the theme package into wwwroot/lib/select2. It is not loaded from a CDN: this
   product is deployed on-premise, sometimes without outbound internet, and the
   theme's stylesheet already carries Adminty's select2 overrides.

   Select2 keeps the native <select> as the source of truth, so every page script
   that does $('#empDept').val(), .val(id) or .html(options) keeps working. The
   two hooks at the bottom are what make that true in practice — see there.

   Opt out on one control with  data-no-search  (left entirely native).
   ───────────────────────────────────────────────────────────────────────────── */
(function ($) {
    'use strict';

    if (!$.fn.select2) return;          // library missing — leave every select native

    // Below this many options the search box is more friction than help: the list
    // fits on screen and the eye beats typing.
    var SEARCH_MIN_OPTIONS = 8;

    /*
     * Whether this select should carry a search box.
     *
     * Counted from the options it holds RIGHT NOW, which matters because almost every
     * dropdown in this app is filled by AJAX after the page loads. Deciding once at
     * init would decide while the select is still empty — 0 options is below the
     * threshold, so a picker holding two hundred employees would never get a search
     * box no matter how long the list grew. See refresh().
     *
     * data-search forces one on regardless: a list whose length depends on how much
     * master data a site has entered should be searchable on day one, not once it
     * happens to cross eight.
     */
    function needsSearch($el) {
        if ($el.is('[data-search]')) return true;
        return $el.find('option').length >= SEARCH_MIN_OPTIONS;
    }

    function initOne(el) {
        var $el = $(el);

        if ($el.data('select2')) return;                 // already initialised
        if (el.multiple || el.size > 1) return;          // not handled; left native
        if ($el.is('[data-no-search]')) return;          // explicit opt-out

        // Inside a Bootstrap modal the dropdown MUST be parented to the modal.
        // Bootstrap keeps focus inside the dialog, so a dropdown appended to
        // <body> renders but its search box cannot be typed into — the dropdown
        // looks broken rather than erroring.
        var $modal = $el.closest('.modal');

        // A select sized inline — style="width:auto" on an inline toolbar, or a fixed px
        // width — is laying itself out next to its neighbours on one line. The theme's
        // `.select2-container{width:100% !important}` overrides that once select2 takes over,
        // which pushes each control onto a line of its own and stacks the whole toolbar.
        // Carry the original width across so those rows stay on one line.
        var inlineWidth = el.style.width;

        var search = needsSearch($el);

        var opts = {
            width: inlineWidth || '100%',
            // Adminty styles .select2-container; keeping the default theme lets
            // those overrides apply.
            minimumResultsForSearch: search ? 0 : Infinity
        };
        if ($modal.length) opts.dropdownParent = $modal;

        $el.select2(opts);
        $el.data('amsSearch', search);   // what refresh() compares against

        if (inlineWidth) {
            // Passed as a custom property because the theme's rule is !important and would
            // otherwise beat an inline width; the CSS reads it back out. See ams-select2.css.
            $el.next('.select2-container')
               .addClass('ams-select-sized')
               .css('--ams-select-w', inlineWidth);
        }
    }

    /** Initialises every eligible <select> in `scope`. Safe to call repeatedly. */
    window.amsInitSelects = function (scope) {
        $(scope || document).find('select').each(function () { initOne(this); });
    };

    // Guards against re-entering refresh from inside select2's own DOM work.
    var refreshing = false;

    /** Redraws a select2 from its <select> after options or value changed. */
    function refresh($el) {
        if (!$el.data('select2') || refreshing) return;

        // minimumResultsForSearch is fixed when select2 initialises, so a dropdown that
        // was empty at page load keeps "no search box" forever once the AJAX lands.
        // Re-initialising is the only way to change it — done only when the answer
        // actually flips, so an ordinary repopulate stays cheap.
        if (needsSearch($el) !== $el.data('amsSearch')) {
            refreshing = true;
            try {
                var value = $el.val();
                $el.select2('destroy');
                initOne($el[0]);
                if (value !== null && value !== undefined) {
                    $el[0].value = value;    // native, so the .val() hook cannot recurse
                }
            } finally {
                refreshing = false;
            }
        }

        // The .select2 namespace updates the widget WITHOUT running the page's own
        // change handlers. A bare .trigger('change') here would re-enter caller
        // code — saving a form from inside a repopulate, for instance.
        $el.trigger('change.select2');
    }

    $(function () {
        window.amsInitSelects(document);

        // Modal selects are commonly populated immediately before the modal opens.
        $(document).on('shown.bs.modal', function (e) {
            window.amsInitSelects(e.target);
            $(e.target).find('select').each(function () { refresh($(this)); });
        });

        // A destroyed modal body would leave orphaned containers behind.
        $(document).on('hidden.bs.modal', function (e) {
            $(e.target).find('select').each(function () {
                var $s = $(this);
                if ($s.data('select2')) $s.select2('close');
            });
        });
    });

    // ── Keeping select2 in step with code that does not raise events ─────────
    // jQuery's .val() and .html() setters fire nothing. Page scripts use both —
    // .html(opts) to fill a dropdown and .val(id) to select a row's value — so
    // without these hooks the widget shows a stale label after an edit.
    // Hooking here means no page script has to know select2 exists.

    var nativeVal = $.fn.val;
    $.fn.val = function () {
        var result = nativeVal.apply(this, arguments);
        if (arguments.length) {
            this.each(function () {
                if (this.tagName === 'SELECT') refresh($(this));
            });
        }
        return result;
    };

    var nativeHtml = $.fn.html;
    $.fn.html = function () {
        var result = nativeHtml.apply(this, arguments);
        if (arguments.length) {
            this.each(function () {
                if (this.tagName === 'SELECT') refresh($(this));
            });
        }
        return result;
    };

})(jQuery);

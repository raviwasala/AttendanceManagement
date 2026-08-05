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

        var opts = {
            width: '100%',
            // Adminty styles .select2-container; keeping the default theme lets
            // those overrides apply.
            minimumResultsForSearch: $el.is('[data-search]') ? 0 : SEARCH_MIN_OPTIONS
        };
        if ($modal.length) opts.dropdownParent = $modal;

        $el.select2(opts);
    }

    /** Initialises every eligible <select> in `scope`. Safe to call repeatedly. */
    window.amsInitSelects = function (scope) {
        $(scope || document).find('select').each(function () { initOne(this); });
    };

    /** Redraws a select2 from its <select> after options or value changed. */
    function refresh($el) {
        // The .select2 namespace updates the widget WITHOUT running the page's own
        // change handlers. A bare .trigger('change') here would re-enter caller
        // code — saving a form from inside a repopulate, for instance.
        if ($el.data('select2')) $el.trigger('change.select2');
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

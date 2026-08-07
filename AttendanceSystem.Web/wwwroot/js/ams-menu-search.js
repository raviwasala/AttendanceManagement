/* ── Sidebar menu search ──────────────────────────────────────────────────────
   Filters the rendered sidebar as you type and opens the groups that match.

   Filters the DOM rather than a search index, which is what keeps it honest about
   permissions: the sidebar only ever contains screens this user may open, so the
   search cannot surface one they may not. A server-side index would have to repeat
   every permission check to say the same thing.

   Ctrl+K / Cmd+K focuses it; Escape clears and restores the menu.
   ───────────────────────────────────────────────────────────────────────────── */
(function ($) {
    'use strict';

    var $input, $clear, $noResult, $nav;

    // Groups the user had open before searching, so clearing puts the sidebar back
    // exactly as it was rather than collapsing everything.
    var openBeforeSearch = null;

    function groups() { return $nav.find('li.pcoded-hasmenu'); }

    /*
     * The menu scrolls natively — the theme's own mCustomScrollbar calls are commented out
     * in pcoded.min.js, so .main-menu is a plain overflow-y container (see site.css).
     * Native scrolling needs no re-measuring after the DOM changes, so there is nothing to
     * do here; the function stays as the single place to hook if that ever changes.
     */
    function rescroll() { /* native overflow — nothing to update */ }

    function snapshotOpenGroups() {
        openBeforeSearch = groups().map(function () {
            return $(this).hasClass('pcoded-trigger');
        }).get();
    }

    function restoreOpenGroups() {
        if (!openBeforeSearch) return;
        groups().each(function (i) {
            var $g = $(this);
            $g.toggleClass('pcoded-trigger', !!openBeforeSearch[i]);
            // pcoded animates the submenu with an inline height; clearing it lets the
            // theme's own CSS take the group back over.
            $g.children('.pcoded-submenu').css('display', openBeforeSearch[i] ? 'block' : '');
        });
        openBeforeSearch = null;
    }

    function reset() {
        $nav.removeClass('ams-menu-searching');
        $nav.find('li, .pcoded-navigatio-lavel').removeClass('ams-menu-hidden ams-menu-hit');
        $noResult.hide();
        restoreOpenGroups();
        rescroll();
    }

    function filter(term) {
        term = (term || '').trim().toLowerCase();

        if (!term) { reset(); return; }

        if (openBeforeSearch === null) snapshotOpenGroups();

        $nav.addClass('ams-menu-searching');

        var hits = 0;

        // Leaves first: anything with a real link is a page that can be landed on.
        // Captions and group headers are decided afterwards, by whether anything
        // underneath them survived.
        $nav.find('li').each(function () {
            var $li = $(this);
            if ($li.hasClass('pcoded-hasmenu')) return;          // handled below
            if ($li.hasClass('pcoded-submenu-caption')) return;  // handled below

            var text = $li.children('a').text().toLowerCase();
            var hit = text.indexOf(term) !== -1;

            $li.toggleClass('ams-menu-hidden', !hit).toggleClass('ams-menu-hit', hit);
            if (hit) hits++;
        });

        // A group matching by its own name — "Payroll" — keeps all its children, so
        // searching for a section shows what is in it rather than nothing.
        groups().each(function () {
            var $g = $(this);
            var ownText = $g.children('a').text().toLowerCase();
            var groupHit = ownText.indexOf(term) !== -1;

            var $children = $g.find('.pcoded-submenu > li');

            if (groupHit) {
                $children.removeClass('ams-menu-hidden');
                hits += $children.filter(':not(.pcoded-submenu-caption)').length;
            }

            var visible = $children.filter(':not(.ams-menu-hidden)')
                                   .filter(':not(.pcoded-submenu-caption)').length;

            $g.toggleClass('ams-menu-hidden', visible === 0);

            // Opened directly rather than through pcoded's click handler: a match two
            // groups down is useless if the user still has to expand it by hand.
            $g.toggleClass('pcoded-trigger', visible > 0);
            $g.children('.pcoded-submenu').css('display', visible > 0 ? 'block' : 'none');
        });

        // A caption with nothing left under it is noise — "Loans" above an empty gap.
        $nav.find('li.pcoded-submenu-caption').each(function () {
            var $cap = $(this);
            var any = false;

            $cap.nextAll('li').each(function () {
                if ($(this).hasClass('pcoded-submenu-caption')) return false;  // next section
                if (!$(this).hasClass('ams-menu-hidden')) { any = true; return false; }
            });

            $cap.toggleClass('ams-menu-hidden', !any);
        });

        // Section headings ("My Space", "Navigation") follow the list they introduce.
        $nav.find('.pcoded-navigatio-lavel').each(function () {
            var $lbl = $(this);
            var $list = $lbl.next('ul');
            var any = $list.children('li:not(.ams-menu-hidden)').length > 0;
            $lbl.toggleClass('ams-menu-hidden', !any);
        });

        $noResult.toggle(hits === 0);
        rescroll();
    }

    $(function () {
        $input = $('#amsMenuSearch');
        if (!$input.length) return;

        $clear = $('#amsMenuSearchClear');
        $noResult = $('#amsMenuNoResult');
        $nav = $('.pcoded-inner-navbar');

        $input.on('input', function () {
            $clear.toggle(!!$(this).val());
            filter($(this).val());
        });

        $input.on('keydown', function (e) {
            if (e.key === 'Escape') {
                $(this).val('');
                $clear.hide();
                reset();
                $(this).blur();
                return;
            }

            // Enter opens the first match, so a search can be finished from the keyboard.
            if (e.key === 'Enter') {
                var href = $nav.find('li.ams-menu-hit:not(.ams-menu-hidden)')
                               .first().children('a').attr('href');
                if (href && href !== 'javascript:void(0)') window.location.href = href;
            }
        });

        $clear.hide().on('click', function () {
            $input.val('').focus();
            $(this).hide();
            reset();
        });

        // Ctrl+K is the near-universal shortcut for this; suppressed inside an input so
        // it never steals a keystroke from a form the user is filling in.
        $(document).on('keydown', function (e) {
            if ((e.ctrlKey || e.metaKey) && (e.key === 'k' || e.key === 'K')) {
                e.preventDefault();
                $input.focus().select();
            }
        });

        initAccordion();
        scrollActiveIntoView();
    });

    /* ── One group open at a time ─────────────────────────────────────────────
       Nine groups holding forty screens is taller than the screen as soon as two
       are expanded, and Payroll alone runs to fourteen rows with its captions. The
       theme leaves every group a user has ever clicked open, so the menu only grows
       and the last groups become unreachable without a long scroll.

       Closing the siblings keeps the whole menu roughly one screen tall, which is
       the difference between scrolling to a group and simply seeing it. */
    function initAccordion() {
        $nav.on('click', 'li.pcoded-hasmenu > a', function () {
            var $li = $(this).parent();

            // Search opens several groups deliberately — collapsing them here would
            // undo the results the moment one was clicked.
            if ($nav.hasClass('ams-menu-searching')) return;

            // Runs after pcoded's own handler, so this reads the state it just set
            // rather than guessing what the click was about to do.
            setTimeout(function () {
                if (!$li.hasClass('pcoded-trigger')) { rescroll(); return; }

                $li.siblings('li.pcoded-hasmenu.pcoded-trigger').each(function () {
                    $(this).removeClass('pcoded-trigger active')
                           .children('.pcoded-submenu').slideUp(200);
                });

                // Opening the last group would otherwise expand it below the fold,
                // leaving the user looking at a header with its contents off-screen.
                setTimeout(function () {
                    rescroll();
                    scrollIntoView($li);
                }, 220);
            }, 10);
        });
    }

    /** Brings an element into view inside the menu's scroll container. */
    function scrollIntoView($el) {
        var $menu = $('.main-menu');
        if (!$el.length || !$menu.length) return;

        var box = $menu[0];
        var boxTop = $menu.offset().top;
        var boxBottom = boxTop + $menu.height();
        var elTop = $el.offset().top;
        var elBottom = elTop + $el.outerHeight();

        // Only scrolls when the element is genuinely outside the visible box. An
        // unconditional scroll would jog the menu on every click.
        if (elTop >= boxTop && elBottom <= boxBottom) return;

        // Positioned a little below the top edge rather than flush against it, so a group
        // header does not end up looking like the first row of the list above it.
        var delta = elTop < boxTop ? (elTop - boxTop - 8) : (elBottom - boxBottom + 8);

        if (box.scrollTo) {
            box.scrollTo({ top: box.scrollTop + delta, behavior: 'smooth' });
        } else {
            box.scrollTop = box.scrollTop + delta;
        }
    }

    /* The page you are on should be visible without hunting for it. On a long menu the
       active item is frequently below the fold at load, which reads as "my page is not
       in the menu". */
    function scrollActiveIntoView() {
        setTimeout(function () {
            var $active = $nav.find('li.active').last();
            if ($active.length) scrollIntoView($active);
        }, 400);
    }

})(jQuery);

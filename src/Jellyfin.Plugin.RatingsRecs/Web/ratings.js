/*
 * Ratings & Recommendations - web client.
 * Replaces the Favourite heart with a 4-level thumbs selector (docs/DESIGN.md, sections 7.1-7.2).
 * No dependencies, no build step. Served by the plugin with a `window.RatingsRecsBootstrap` prelude.
 *
 * Safety rules: the heart is hidden, never removed; nothing here may throw into jellyfin-web;
 * if no heart is found the page is left untouched and a note is written to the console.
 */
(function () {
    'use strict';

    if (window.__ratingsRecs) {
        return;
    }

    var BOOT = window.RatingsRecsBootstrap || {};
    var LOG_PREFIX = '[RatingsRecs]';
    var PREFS_TTL_MS = 60000;
    var LEVELS = [
        { value: -3, cls: 'hate', icon: 'down2' },
        { value: -1, cls: 'watchable', icon: 'down' },
        { value: 1, cls: 'good', icon: 'up' },
        { value: 3, cls: 'amazing', icon: 'up2' }
    ];
    // Monochrome outline thumb drawn with a thick stroke (no fill, no colours): readable down to badge size,
    // also when two are drawn side by side ("Hate it" / "Amazing").
    var THUMB_UP = 'M7.5 10.5l3.4-6.6c.2-.5.7-.8 1.2-.8 1.2 0 2.1 1.1 1.9 2.3L13.6 9h5.3c1.3 0 2.2 1.2 1.9 2.5l-1.5 6.3c-.3 1.3-1.4 2.2-2.7 2.2H7.5zM3.2 10.5h4.3V20H3.2z';

    /*
     * DOM adapters. Verified on 10.11.11 (Legacy) and 12.4 (Legacy and Modern): the item detail page and the
     * cards are still rendered by the legacy controllers in both layouts, so the first selectors match in all
     * three; the following ones are fallbacks for future markup changes. The layouts differ in the chrome
     * (header, drawer), which matters for the menu entry added in a later milestone.
     */
    var ADAPTERS = [
        {
            name: 'modern',
            detect: function () { return !!document.querySelector('.MuiAppBar-root'); },
            pageRoot: ['.mainAnimatedPages .page:not(.hide)', '#reactRoot .page:not(.hide)', '.page:not(.hide)'],
            detailHeart: [
                '.mainDetailButtons button[is="emby-ratingbutton"]',
                '.mainDetailButtons .btnUserRating',
                '.detailButtons button[data-isfavorite]',
                '.itemDetailPage button[data-isfavorite]'
            ],
            cardHeart: [
                '.cardOverlayButton-br button[is="emby-ratingbutton"]',
                '.card button[is="emby-ratingbutton"]',
                '.card button[data-isfavorite]'
            ]
        },
        {
            name: 'legacy',
            detect: function () { return !!document.querySelector('.skinHeader, .mainDrawer'); },
            pageRoot: ['.mainAnimatedPages .page:not(.hide)', '.page:not(.hide)'],
            detailHeart: [
                '.mainDetailButtons button[is="emby-ratingbutton"]',
                '.mainDetailButtons .btnUserRating',
                '.detailPagePrimaryContainer button[data-isfavorite]',
                '.itemDetailPage button[data-isfavorite]'
            ],
            cardHeart: [
                '.cardOverlayButton-br button[is="emby-ratingbutton"]',
                '.card button[is="emby-ratingbutton"]',
                '.card button[data-isfavorite]'
            ]
        }
    ];

    var state = {
        userId: null,
        ratings: {},
        types: {},
        pending: {},
        seq: {},
        missingNoted: {},
        widgetCounter: 0,
        openWidget: null
    };

    var hoverCapable = !!(window.matchMedia && window.matchMedia('(hover: hover) and (pointer: fine)').matches);

    function log() {
        try {
            var args = Array.prototype.slice.call(arguments);
            args.unshift(LOG_PREFIX);
            console.info.apply(console, args);
        } catch (e) {
            /* ignore */
        }
    }

    function normId(id) {
        return String(id || '').replace(/-/g, '').toLowerCase();
    }

    /* ---------- i18n ---------- */

    function language() {
        var raw = document.documentElement.getAttribute('lang') || navigator.language || 'en';
        var code = raw.toLowerCase().slice(0, 2);
        return BOOT.strings && BOOT.strings[code] ? code : 'en';
    }

    function t(key, args) {
        var strings = BOOT.strings || {};
        var text = (strings[language()] || {})[key] || (strings.en || {})[key] || key;
        if (args === undefined) {
            return text;
        }

        var list = Array.isArray(args) ? args : [args];
        return text.replace(/\{(\d)\}/g, function (match, index) {
            var value = list[Number(index)];
            return value === undefined || value === null ? '' : String(value);
        });
    }

    function levelNameForValue(value) {
        // Profile values may be customised: name the closest canonical level.
        if (value >= 2) {
            return levelName(3);
        }

        return value > 0 ? levelName(1) : (value <= -2 ? levelName(-3) : levelName(-1));
    }

    function levelName(value) {
        var custom = BOOT.levelNames && BOOT.levelNames[String(value)];
        return custom || t('level.' + value);
    }

    function levelOf(value) {
        for (var i = 0; i < LEVELS.length; i++) {
            if (LEVELS[i].value === value) {
                return LEVELS[i];
            }
        }

        return null;
    }

    /* ---------- API ---------- */

    function apiClient() {
        return window.ApiClient || null;
    }

    function ready() {
        var client = apiClient();
        try {
            return !!(client && client.accessToken && client.accessToken() && client.getCurrentUserId && client.getCurrentUserId());
        } catch (e) {
            return false;
        }
    }

    function request(method, path, body) {
        var client = apiClient();
        if (!client) {
            return Promise.reject(new Error('ApiClient not available'));
        }

        var headers = { 'Authorization': 'MediaBrowser Token="' + client.accessToken() + '"' };
        var init = { method: method, headers: headers };
        if (body !== undefined) {
            headers['Content-Type'] = 'application/json';
            init.body = JSON.stringify(body);
        }

        return fetch(client.getUrl('RatingsRecs/' + path), init).then(function (response) {
            if (!response.ok) {
                throw new Error('HTTP ' + response.status);
            }

            return response.status === 204 ? null : response.json();
        });
    }

    function checkUser() {
        var client = apiClient();
        var current = client ? normId(client.getCurrentUserId()) : null;
        if (current !== state.userId) {
            state.userId = current;
            state.ratings = {};
            state.pending = {};
            state.prefs = null;
            state.prefsLoadedAt = 0;
        }
    }

    /* ---------- series rating mode (docs/DESIGN.md section 4.1) ---------- */

    function seriesMode() {
        return (state.prefs && state.prefs.seriesMode) || 'Series';
    }

    function isRateable(type) {
        var mode = seriesMode();
        if (type === 'Movie') {
            return true;
        }

        if (type === 'Series') {
            return mode !== 'Season';
        }

        if (type === 'Season') {
            return mode !== 'Series';
        }

        return false;
    }

    function loadPrefs() {
        if (state.prefsLoading) {
            return state.prefsLoading;
        }

        state.prefsLoading = request('GET', 'Preferences').then(function (prefs) {
            var changed = state.prefs && state.prefs.seriesMode !== prefs.seriesMode;
            state.prefs = prefs;
            state.prefsLoadedAt = Date.now();
            state.prefsLoading = null;
            if (changed) {
                resetAll();
            }

            return prefs;
        }, function (err) {
            state.prefsLoading = null;
            // Fall back to the whole-series behaviour, retry later.
            state.prefs = state.prefs || { seriesMode: 'Series' };
            state.prefsLoadedAt = Date.now();
            log('could not read preferences:', err && err.message);
            return state.prefs;
        });
        return state.prefsLoading;
    }

    function resetAll() {
        // The mode changed: give every heart back and redraw with the new rules.
        var widgets = document.querySelectorAll('.rr-rating:not(.rr-card-rating)');
        for (var i = 0; i < widgets.length; i++) {
            widgets[i].parentNode.removeChild(widgets[i]);
        }

        var hearts = document.querySelectorAll('[data-rr-item]');
        for (var j = 0; j < hearts.length; j++) {
            if (!hearts[j].classList.contains('rr-rating')) {
                hearts[j].classList.remove('rr-hidden-heart');
                hearts[j].removeAttribute('data-rr-item');
                hearts[j].removeAttribute('data-rr-skip');
            }
        }

        var copies = document.querySelectorAll('.rr-copy-wrap');
        for (var k = 0; k < copies.length; k++) {
            copies[k].parentNode.removeChild(copies[k]);
        }

        // Cards: rateable types depend on the mode too.
        var hidden = document.querySelectorAll('.card .rr-hidden-heart');
        for (var m = 0; m < hidden.length; m++) {
            hidden[m].classList.remove('rr-hidden-heart');
        }

        var badges = document.querySelectorAll('.rr-badge');
        for (var n = 0; n < badges.length; n++) {
            badges[n].parentNode.removeChild(badges[n]);
        }

        var cards = document.querySelectorAll('[data-rr-card]');
        for (var p = 0; p < cards.length; p++) {
            cards[p].removeAttribute('data-rr-card');
        }

        schedule();
    }

    var queue = {};
    var queueTimer = null;

    function loadRatings(ids) {
        ids.forEach(function (id) {
            if (!Object.prototype.hasOwnProperty.call(state.ratings, id) && !state.pending[id]) {
                state.pending[id] = true;
                queue[id] = true;
            }
        });
        if (!queueTimer) {
            queueTimer = setTimeout(flushQueue, 40);
        }
    }

    function flushQueue() {
        queueTimer = null;
        var ids = Object.keys(queue);
        queue = {};
        while (ids.length) {
            fetchBatch(ids.splice(0, 100));
        }
    }

    function fetchBatch(batch) {
        request('GET', 'Ratings?itemIds=' + batch.join(',')).then(function (list) {
            batch.forEach(function (id) {
                delete state.pending[id];
                if (!Object.prototype.hasOwnProperty.call(state.ratings, id)) {
                    state.ratings[id] = null;
                }
            });
            (list || []).forEach(function (rating) {
                state.ratings[normId(rating.itemId)] = rating.value;
            });
            batch.forEach(refreshItem);
        }, function (err) {
            batch.forEach(function (id) { delete state.pending[id]; });
            log('could not load ratings:', err && err.message);
        });
    }

    function getItemType(id) {
        if (state.types[id]) {
            return Promise.resolve(state.types[id]);
        }

        var client = apiClient();
        return client.getItem(client.getCurrentUserId(), id).then(function (item) {
            state.types[id] = item && item.Type;
            return state.types[id];
        });
    }

    /* ---------- rendering ---------- */

    function svg(path, down) {
        return '<svg class="rr-icon' + (down ? ' rr-down' : '') + '" viewBox="0 0 24 24" aria-hidden="true" focusable="false">'
            + '<path fill="none" stroke="currentColor" stroke-width="2.3" stroke-linejoin="round" stroke-linecap="round" d="' + path + '"/></svg>';
    }

    function icon(kind) {
        switch (kind) {
            case 'up': return svg(THUMB_UP, false);
            case 'down': return svg(THUMB_UP, true);
            case 'up2': return '<span class="rr-double">' + svg(THUMB_UP, false) + svg(THUMB_UP, false) + '</span>';
            case 'down2': return '<span class="rr-double">' + svg(THUMB_UP, true) + svg(THUMB_UP, true) + '</span>';
            default: return '<span class="rr-empty">' + svg(THUMB_UP, false) + '</span>';
        }
    }

    function cleanClassName(className) {
        return String(className || '')
            .split(/\s+/)
            .filter(function (c) {
                // 'hide' is set by jellyfin-web on the heart while the item loads: never copy it.
                return c && c !== 'btnUserRating' && c !== 'itemAction' && c !== 'hide' && c.indexOf('rr-') !== 0 && c.indexOf('ratingbutton') !== 0;
            })
            .join(' ');
    }

    function createWidget(itemId, heart, compact) {
        var id = 'rr-bar-' + (++state.widgetCounter);
        var root = document.createElement('div');
        root.className = 'rr-rating' + (compact ? ' rr-compact' : '');
        root.setAttribute('data-rr-item', itemId);

        var toggle = document.createElement('button');
        toggle.type = 'button';
        toggle.className = cleanClassName(heart.className) + ' rr-toggle';
        toggle.setAttribute('aria-haspopup', 'true');
        toggle.setAttribute('aria-expanded', 'false');
        toggle.setAttribute('aria-controls', id);

        var bar = document.createElement('div');
        bar.id = id;
        bar.className = 'rr-bar' + (compact ? ' rr-compact' : '');
        bar.setAttribute('role', 'radiogroup');
        bar.setAttribute('aria-label', t('ratingGroup'));
        bar.hidden = true;
        LEVELS.forEach(function (level) {
            var button = document.createElement('button');
            button.type = 'button';
            button.className = 'rr-level rr-' + level.cls;
            button.setAttribute('role', 'radio');
            button.setAttribute('aria-checked', 'false');
            button.setAttribute('data-value', String(level.value));
            button.setAttribute('aria-label', levelName(level.value));
            button.title = levelName(level.value);
            button.tabIndex = -1;
            button.innerHTML = icon(level.icon) + '<span class="rr-label"></span>';
            button.querySelector('.rr-label').textContent = levelName(level.value);
            bar.appendChild(button);
        });

        root.appendChild(toggle);
        root.appendChild(bar);
        root.__rrBar = bar;
        bar.__rrRoot = root;
        wire(root, toggle, bar, itemId);
        render(root);
        return root;
    }

    function render(root) {
        var itemId = root.getAttribute('data-rr-item');
        var value = state.ratings[itemId];
        var level = levelOf(value);
        var toggle = root.querySelector('.rr-toggle');
        var label = level ? t('currentRating', levelName(value)) : (Object.prototype.hasOwnProperty.call(state.ratings, itemId) ? t('notRated') : t('rate'));
        toggle.innerHTML = icon(level ? level.icon : 'empty');
        toggle.title = label;
        toggle.setAttribute('aria-label', label);
        LEVELS.forEach(function (l) { toggle.classList.remove('rr-val-' + l.cls); });
        if (level) {
            toggle.classList.add('rr-val-' + level.cls);
        }

        var radios = root.__rrBar.querySelectorAll('.rr-level');
        for (var i = 0; i < radios.length; i++) {
            var checked = Number(radios[i].getAttribute('data-value')) === value;
            radios[i].setAttribute('aria-checked', checked ? 'true' : 'false');
        }
    }

    function refreshItem(itemId) {
        var widgets = document.querySelectorAll('.rr-rating[data-rr-item="' + itemId + '"]');
        for (var i = 0; i < widgets.length; i++) {
            render(widgets[i]);
        }

        var cards = document.querySelectorAll('[data-rr-card="' + itemId + '"]');
        for (var j = 0; j < cards.length; j++) {
            renderBadge(cards[j]);
        }
    }

    /*
     * The badge sits above jellyfin-web's transparent card link (so that its tooltip shows on hover): a click on it is
     * handed to that link, so it opens the item exactly like a click anywhere else on the card. Inside a link or a
     * button (our own cards, other layouts) the click already reaches it.
     */
    function openCardFromBadge(e) {
        var badge = e.currentTarget;
        if (badge.parentNode && badge.parentNode.closest && badge.parentNode.closest('a[href], button, .itemAction')) {
            return;
        }

        var card = badge.closest ? badge.closest('.card') : null;
        var link = card && (card.querySelector('a.cardImageContainer[href]') || card.querySelector('a[href*="details"]') || card.querySelector('.itemAction[data-action="link"]'));
        if (link) {
            e.preventDefault();
            e.stopPropagation();
            link.click();
        }
    }

    /*
     * Card badge: read-only, top right, only when the item is rated. No popup and no handlers: a click on it is a
     * click on the card, which opens the item as usual. Ratings are only given on the item's own page.
     */
    function renderBadge(card) {
        var itemId = card.getAttribute('data-rr-card');
        var value = state.ratings[itemId];
        var level = levelOf(value);
        // jellyfin-web cards: in .cardScalable, after the image and the link layer. The image container has
        // `contain: strict` (its own stacking context), so a badge inside it could never get the hover for its tooltip.
        var scalable = card.querySelector('.cardScalable');
        var host = scalable || card.querySelector('.rr-card-image');
        if (!host) {
            return;
        }

        var badge = host.querySelector('.rr-badge');
        if (!level) {
            if (badge) {
                badge.parentNode.removeChild(badge);
            }

            return;
        }

        if (!badge) {
            badge = document.createElement('span');
            badge.className = 'rr-badge';
            badge.setAttribute('role', 'img');
            badge.addEventListener('click', openCardFromBadge);
            host.appendChild(badge);
        }

        if (badge.getAttribute('data-value') !== String(value)) {
            var label = t('currentRating', levelName(value));
            badge.setAttribute('data-value', String(value));
            badge.innerHTML = icon(level.icon);
            badge.title = label;
            badge.setAttribute('aria-label', label);
        }

        if (scalable) {
            placeBesideIndicators(card, scalable, badge);
        }
    }

    function placeBesideIndicators(card, scalable, badge) {
        // Top right, to the left of jellyfin-web's own indicators (played check, unplayed count) when there are some.
        var indicators = card.querySelector('.cardIndicators');
        var box = indicators && indicators.children.length ? indicators.getBoundingClientRect() : null;
        if (!box || box.width === 0) {
            badge.style.right = '';
            badge.style.top = '';
            return;
        }

        var outer = scalable.getBoundingClientRect();
        var right = Math.round(outer.right - box.left + 4) + 'px';
        var top = Math.round(box.top - outer.top + ((box.height - badge.offsetHeight) / 2)) + 'px';
        if (badge.style.right !== right) {
            badge.style.right = right;
        }

        if (badge.style.top !== top) {
            badge.style.top = top;
        }
    }

    /* ---------- open / close ---------- */

    function positionBar(root) {
        var bar = root.__rrBar;
        var rect = root.querySelector('.rr-toggle').getBoundingClientRect();
        var width = bar.offsetWidth;
        var height = bar.offsetHeight;
        var left = Math.min(Math.max(8, rect.left + rect.width / 2 - width / 2), window.innerWidth - width - 8);
        var top = rect.top - height - 8;
        if (top < 8) {
            top = rect.bottom + 8;
        }

        bar.style.left = Math.round(left) + 'px';
        bar.style.top = Math.round(top) + 'px';
    }

    function open(root, focusRadio) {
        if (state.openWidget && state.openWidget !== root) {
            close(state.openWidget, false);
        }

        var bar = root.__rrBar;
        clearTimeout(root.__rrCloseTimer);
        if (!bar.hidden) {
            return;
        }

        // Rendered in <body> with fixed positioning so cards (overflow, transforms) cannot clip it.
        document.body.appendChild(bar);
        bar.hidden = false;
        positionBar(root);
        root.classList.add('rr-open');
        root.querySelector('.rr-toggle').setAttribute('aria-expanded', 'true');
        state.openWidget = root;

        var radios = bar.querySelectorAll('.rr-level');
        var target = bar.querySelector('.rr-level[aria-checked="true"]') || radios[0];
        for (var i = 0; i < radios.length; i++) {
            radios[i].tabIndex = radios[i] === target ? 0 : -1;
        }

        if (focusRadio) {
            target.focus();
        }
    }

    function close(root, returnFocus) {
        var bar = root.__rrBar;
        clearTimeout(root.__rrCloseTimer);
        if (bar.hidden) {
            return;
        }

        bar.hidden = true;
        root.appendChild(bar);
        root.classList.remove('rr-open');
        var toggle = root.querySelector('.rr-toggle');
        toggle.setAttribute('aria-expanded', 'false');
        if (state.openWidget === root) {
            state.openWidget = null;
        }

        if (returnFocus) {
            toggle.focus();
        }
    }

    function scheduleClose(root) {
        clearTimeout(root.__rrCloseTimer);
        root.__rrCloseTimer = setTimeout(function () { close(root, false); }, 300);
    }

    function stop(e) {
        e.stopPropagation();
    }

    function wire(root, toggle, bar, itemId) {
        // Keep clicks away from jellyfin-web card handlers (which would open the item or start playback).
        ['click', 'mousedown', 'touchstart', 'pointerdown', 'contextmenu'].forEach(function (type) {
            root.addEventListener(type, stop);
            bar.addEventListener(type, stop);
        });

        toggle.addEventListener('click', function (e) {
            e.preventDefault();
            if (bar.hidden) {
                open(root, e.detail === 0);
            } else if (!hoverCapable || e.detail === 0) {
                close(root, false);
            }
        });

        if (hoverCapable) {
            root.addEventListener('mouseenter', function () {
                if (!root.__rrSuppressHover) {
                    open(root, false);
                }
            });
            root.addEventListener('mouseleave', function () {
                root.__rrSuppressHover = false;
                scheduleClose(root);
            });
            bar.addEventListener('mouseenter', function () { clearTimeout(root.__rrCloseTimer); });
            bar.addEventListener('mouseleave', function () { scheduleClose(root); });
        }

        bar.addEventListener('click', function (e) {
            var button = e.target.closest ? e.target.closest('.rr-level') : null;
            if (!button) {
                return;
            }

            e.preventDefault();
            select(itemId, Number(button.getAttribute('data-value')));
            // Moving the bar back may count as entering the widget: do not reopen until the pointer leaves.
            root.__rrSuppressHover = true;
            setTimeout(function () { root.__rrSuppressHover = false; }, 1500);
            close(root, e.detail === 0);
        });

        bar.addEventListener('keydown', function (e) {
            var radios = Array.prototype.slice.call(bar.querySelectorAll('.rr-level'));
            var index = radios.indexOf(document.activeElement);
            var next = -1;
            switch (e.key) {
                case 'ArrowRight':
                case 'ArrowDown':
                    next = (index + 1) % radios.length;
                    break;
                case 'ArrowLeft':
                case 'ArrowUp':
                    next = (index - 1 + radios.length) % radios.length;
                    break;
                case 'Home':
                    next = 0;
                    break;
                case 'End':
                    next = radios.length - 1;
                    break;
                case 'Escape':
                    e.preventDefault();
                    e.stopPropagation();
                    close(root, true);
                    return;
                case 'Tab':
                    close(root, false);
                    return;
                default:
                    return;
            }

            e.preventDefault();
            e.stopPropagation();
            radios.forEach(function (r, i) { r.tabIndex = i === next ? 0 : -1; });
            radios[next].focus();
        });

        toggle.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                close(root, false);
            } else if ((e.key === 'ArrowUp' || e.key === 'ArrowDown') && !bar.hidden) {
                e.preventDefault();
                (bar.querySelector('.rr-level[tabindex="0"]') || bar.querySelector('.rr-level')).focus();
            }
        });

        bar.addEventListener('focusout', function (e) {
            if (e.relatedTarget && !bar.contains(e.relatedTarget) && !root.contains(e.relatedTarget)) {
                close(root, false);
            }
        });
    }

    document.addEventListener('click', function () {
        if (state.openWidget) {
            close(state.openWidget, false);
        }
    });

    window.addEventListener('scroll', function () {
        if (state.openWidget) {
            close(state.openWidget, false);
        }
    }, true);

    window.addEventListener('resize', function () {
        if (state.openWidget) {
            positionBar(state.openWidget);
        }
    });

    /* ---------- saving ---------- */

    function select(itemId, value) {
        var previous = Object.prototype.hasOwnProperty.call(state.ratings, itemId) ? state.ratings[itemId] : null;
        var next = previous === value ? null : value;
        var seq = (state.seq[itemId] || 0) + 1;
        state.seq[itemId] = seq;

        // Optimistic update, rolled back if the server refuses.
        state.ratings[itemId] = next;
        refreshItem(itemId);

        var call = next === null
            ? request('DELETE', 'Ratings/' + itemId)
            : request('PUT', 'Ratings/' + itemId, { value: next });
        call.then(function () {
            announce(next === null ? t('removed') : t('saved', levelName(next)));
        }, function (err) {
            log('could not save the rating:', err && err.message);
            if (state.seq[itemId] === seq) {
                state.ratings[itemId] = previous;
                refreshItem(itemId);
            }

            toast(t('saveError'));
        });
    }

    function liveRegion() {
        var region = document.getElementById('rr-live');
        if (!region) {
            region = document.createElement('div');
            region.id = 'rr-live';
            region.className = 'rr-sr-only';
            region.setAttribute('aria-live', 'polite');
            document.body.appendChild(region);
        }

        return region;
    }

    function announce(text) {
        liveRegion().textContent = text;
    }

    function toast(text, ok) {
        var el = document.getElementById('rr-toast');
        if (!el) {
            el = document.createElement('div');
            el.id = 'rr-toast';
            el.className = 'rr-toast';
            el.setAttribute('role', 'alert');
            document.body.appendChild(el);
        }

        el.textContent = text;
        el.classList.toggle('rr-toast-ok', !!ok);
        el.classList.add('rr-toast-show');
        clearTimeout(el.__rrTimer);
        el.__rrTimer = setTimeout(function () { el.classList.remove('rr-toast-show'); }, 5000);
    }

    /* ---------- scanning ---------- */

    function currentAdapter() {
        for (var i = 0; i < ADAPTERS.length; i++) {
            if (ADAPTERS[i].detect()) {
                return ADAPTERS[i];
            }
        }

        return ADAPTERS[ADAPTERS.length - 1];
    }

    function queryFirst(root, selectors) {
        for (var i = 0; i < selectors.length; i++) {
            var found = root.querySelector(selectors[i]);
            if (found) {
                return found;
            }
        }

        return null;
    }

    function queryAll(root, selectors) {
        for (var i = 0; i < selectors.length; i++) {
            var found = root.querySelectorAll(selectors[i]);
            if (found.length) {
                return found;
            }
        }

        return [];
    }

    function idFromLocation() {
        var match = /[?&]id=([0-9a-fA-F-]{32,36})/.exec(location.hash || location.search || '');
        return match ? normId(match[1]) : null;
    }

    function isDetailRoute() {
        return /details/i.test(location.hash || location.pathname || '');
    }

    function attach(heart, itemId, compact, knownType) {
        var next = heart.nextElementSibling;
        var hasWidget = next && next.classList && next.classList.contains('rr-rating') && next.getAttribute('data-rr-item') === itemId;
        if (heart.getAttribute('data-rr-item') === itemId && (hasWidget || heart.hasAttribute('data-rr-skip'))) {
            return;
        }

        if (heart.getAttribute('data-rr-pending') === itemId) {
            return;
        }

        heart.setAttribute('data-rr-pending', itemId);
        var typePromise = knownType ? Promise.resolve(knownType) : getItemType(itemId);
        typePromise.then(function (type) {
            heart.removeAttribute('data-rr-pending');
            heart.setAttribute('data-rr-item', itemId);
            var sibling = heart.nextElementSibling;
            if (sibling && sibling.classList && sibling.classList.contains('rr-rating')) {
                sibling.parentNode.removeChild(sibling);
            }

            if (type === 'Series' && seriesMode() === 'Season') {
                addCopyToSeasonsButton(heart, itemId);
            }

            if (!isRateable(type)) {
                // Episodes, seasons (until per-season mode), music...: keep the original heart.
                heart.setAttribute('data-rr-skip', '');
                heart.classList.remove('rr-hidden-heart');
                return;
            }

            heart.removeAttribute('data-rr-skip');
            heart.parentNode.insertBefore(createWidget(itemId, heart, compact), heart.nextSibling);
            heart.classList.add('rr-hidden-heart');
            loadRatings([itemId]);
        }).catch(function (err) {
            heart.removeAttribute('data-rr-pending');
            log('could not read item type, keeping the heart:', err && err.message);
        });
    }

    function addCopyToSeasonsButton(heart, seriesId) {
        // Per-season mode: offer to copy an existing series rating onto the seasons not rated yet.
        if (state.copiedSeries && state.copiedSeries[seriesId]) {
            return;
        }

        request('GET', 'Ratings/' + seriesId).then(function (rating) {
            if (!rating || rating.value === null || rating.value === undefined || rating.source !== 'explicit') {
                return;
            }

            // On its own line inside the ribbon (which is already offset next to the poster).
            var anchor = heart.parentNode;
            if (!anchor || !anchor.parentNode || anchor.parentNode.querySelector('.rr-copy-seasons[data-rr-item="' + seriesId + '"]')) {
                return;
            }

            anchor.parentNode.classList.add('rr-ribbon-wrap');
            var wrapper = document.createElement('div');
            wrapper.className = 'rr-copy-wrap';

            var button = document.createElement('button');
            button.type = 'button';
            button.className = 'rr-copy-seasons';
            button.setAttribute('data-rr-item', seriesId);
            button.textContent = t('copySeasons', levelName(rating.value));
            button.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                button.disabled = true;
                request('POST', 'Ratings/' + seriesId + '/CopyToSeasons').then(function (result) {
                    toast(t('copySeasons.done', String(result.copied)), true);
                    state.copiedSeries = state.copiedSeries || {};
                    state.copiedSeries[seriesId] = true;
                    state.ratings = {};
                    wrapper.parentNode.removeChild(wrapper);
                    resetAll();
                }, function (err) {
                    button.disabled = false;
                    log('copy to seasons failed:', err && err.message);
                    toast(t('saveError'));
                });
            });
            wrapper.appendChild(button);
            anchor.parentNode.insertBefore(wrapper, anchor.nextSibling);
        }, function () { /* no rating: nothing to offer */ });
    }

    function scanDetail(adapter, root) {
        if (!isDetailRoute()) {
            return;
        }

        var heart = queryFirst(root, adapter.detailHeart);
        var key = location.hash;
        if (!heart) {
            // The page may still be rendering: only note it once per page, never throw.
            if (!state.missingNoted[key] && root.querySelector && root.querySelector('.mainDetailButtons, .detailPagePrimaryContainer')) {
                state.missingNoted[key] = true;
                setTimeout(function () {
                    if (!queryFirst(activePage(adapter), adapter.detailHeart) && location.hash === key) {
                        log('favourite button not found on this page (' + adapter.name + ' layout): leaving it unchanged.');
                    }
                }, 3000);
            }

            return;
        }

        var itemId = normId(heart.getAttribute('data-id')) || idFromLocation();
        if (itemId) {
            attach(heart, itemId, false, null);
        }
    }

    function scanCards(adapter, root) {
        // Cards never rate: the heart of rateable items is hidden (it would count as a rating) and rated items
        // get a read-only badge. Other items (episodes, music...) keep their heart.
        var hearts = queryAll(root, adapter.cardHeart);
        for (var i = 0; i < hearts.length; i++) {
            var heart = hearts[i];
            if (heart.closest && heart.closest('.mainDetailButtons')) {
                continue;
            }

            var owner = heart.closest ? heart.closest('.card') : null;
            var heartType = heart.getAttribute('data-itemtype') || (owner && owner.getAttribute('data-type'));
            if (isRateable(heartType)) {
                heart.classList.add('rr-hidden-heart');
            }
        }

        var cards = root.querySelectorAll ? root.querySelectorAll('.card[data-id]') : [];
        var ids = [];
        for (var j = 0; j < cards.length; j++) {
            var card = cards[j];
            if (!isRateable(card.getAttribute('data-type'))) {
                continue;
            }

            var itemId = normId(card.getAttribute('data-id'));
            if (card.getAttribute('data-rr-card') !== itemId) {
                card.setAttribute('data-rr-card', itemId);
            }

            ids.push(itemId);
            renderBadge(card);
        }

        loadRatings(ids);
    }

    function activePage(adapter) {
        return queryFirst(document, adapter.pageRoot) || document.body;
    }

    /* ---------- "For you" page (#/ratingsrecs) ---------- */

    /*
     * jellyfin-web has no API to add routes. An unknown route such as #/ratingsrecs shows its own
     * "page not found" view (.fallbackPage) inside the normal header and menu, in both layouts:
     * the page is drawn inside that view, whose own content is hidden.
     */
    var FOR_YOU_ROUTE = /^#\/ratingsrecs(?:[/?]|$)/;

    function isForYouRoute() {
        return FOR_YOU_ROUTE.test(location.hash || '');
    }

    function forYouType() {
        var match = /[?&]type=(Movie|Series)/i.exec(location.hash || '');
        return match && match[1].toLowerCase() === 'series' ? 'Series' : 'Movie';
    }

    // Home row titles link to one section of the page: #/ratingsrecs?section=recommended | rate.
    var SECTION_LINK = { recommended: '#/ratingsrecs?section=recommended', rate: '#/ratingsrecs?section=rate' };

    function forYouSection() {
        var match = /[?&]section=(recommended|rate)\b/i.exec(location.hash || '');
        return match ? match[1].toLowerCase() : null;
    }

    function scrollToSection(host, name) {
        var target = name && host.querySelector('#rr-section-' + name);
        if (target && target.scrollIntoView) {
            // scroll-margin-top in ratings.css keeps the title below the fixed header.
            target.scrollIntoView({ block: 'start' });
        }
    }

    function el(tag, className, text) {
        var node = document.createElement(tag);
        if (className) {
            node.className = className;
        }

        if (text !== undefined && text !== null) {
            node.textContent = text;
        }

        return node;
    }

    function explanationText(explanation) {
        if (!explanation) {
            return '';
        }

        var parts = [];
        if (explanation.SimilarSeason) {
            var similar = explanation.SimilarSeason;
            var sentence = t('similarSeason', [explanation.BestSeasonNumber, similar.SeasonNumber, similar.SeriesName, levelNameForValue(similar.Value)]);
            if (similar.SharedCategory && similar.SharedValue) {
                sentence += ' — ' + t('sharedFeature', [t('feature.' + similar.SharedCategory).toLowerCase(), similar.SharedValue]);
            }

            parts.push(sentence);
        } else if (explanation.BecauseOf && explanation.BecauseOf.Name) {
            parts.push(t(explanation.BecauseOf.Value >= 3 ? 'becauseLoved' : 'becauseLiked', explanation.BecauseOf.Name));
        }

        var shared = explanation.SimilarSeason || {};
        (explanation.Features || []).forEach(function (feature) {
            var duplicate = feature.Category === shared.SharedCategory && feature.Value === shared.SharedValue;
            if (parts.length < 2 && !duplicate) {
                parts.push(t('feature.' + feature.Category) + ': ' + feature.Value);
            }
        });
        if (explanation.StartSeasonNumber && explanation.StartSeasonNumber !== 1) {
            parts.push(t('startFrom', explanation.StartSeasonNumber));
        }

        return parts.join(' · ');
    }

    function itemCard(item, explanation, serverId) {
        var id = normId(item.Id);
        var card = el('div', 'rr-card');
        var link = el('a', 'rr-card-link');
        link.href = '#/details?id=' + id + (serverId ? '&serverId=' + serverId : '');
        var poster = el('div', 'rr-card-image');
        var tag = item.ImageTags && item.ImageTags.Primary;
        if (tag) {
            poster.style.backgroundImage = 'url("' + apiClient().getScaledImageUrl(item.Id, { type: 'Primary', maxWidth: 300, tag: tag }) + '")';
        } else {
            poster.appendChild(el('span', 'rr-card-placeholder', item.Name));
        }

        link.appendChild(poster);

        // A season alone ("Season 1") says nothing: show the series name, with the season underneath.
        var isSeason = item.Type === 'Season' && item.SeriesName;
        link.appendChild(el('div', 'rr-card-title', isSeason ? item.SeriesName : item.Name));
        if (isSeason) {
            link.appendChild(el('div', 'rr-card-meta', item.Name + (item.ProductionYear ? ' · ' + item.ProductionYear : '')));
        } else if (item.ProductionYear) {
            link.appendChild(el('div', 'rr-card-meta', String(item.ProductionYear)));
        }

        card.appendChild(link);
        var why = explanationText(explanation);
        if (why) {
            card.appendChild(el('div', 'rr-card-why', why));
        }

        // Same as every card: a read-only badge when rated, nothing otherwise (ratings are given on the item's page).
        if (isRateable(item.Type)) {
            card.setAttribute('data-rr-card', id);
            renderBadge(card);
            loadRatings([id]);
        }

        return card;
    }

    /* ---------- rows: home ("Recommended for you", "Rate these") and detail page ("Similar, picked for you") ---------- */

    function rowSection(className, title, link, response, withExplanations) {
        var section = el('div', 'verticalSection rr-row-section ' + className);
        var header = el('div', 'sectionTitleContainer sectionTitleContainer-cards padded-left');
        var heading = el('h2', 'sectionTitle sectionTitle-cards', title);
        if (link) {
            var a = el('a', 'rr-section-link');
            a.href = link;
            a.appendChild(heading);
            a.appendChild(el('span', 'material-icons chevron_right'));
            header.appendChild(a);
        } else {
            header.appendChild(heading);
        }

        section.appendChild(header);
        var row = el('div', 'rr-row padded-left padded-right');
        var serverId = apiClient().serverId ? apiClient().serverId() : '';
        (response.Items || []).forEach(function (item, i) {
            row.appendChild(itemCard(item, withExplanations ? (response.Explanations || [])[i] : null, serverId));
        });
        section.appendChild(row);
        return section;
    }

    function isHomeRoute() {
        var hash = location.hash || '';
        return hash === '' || hash === '#/' || /^#\/home(\.html)?(\?(?!.*tab=[1-9]).*)?$/.test(hash);
    }

    function renderHomeRows(root) {
        if (BOOT.homeRows === 'off' || !isHomeRoute() || !root || !root.querySelector) {
            return;
        }

        var container = root.querySelector('.homeSectionsContainer');
        if (!container) {
            return;
        }

        // Home Screen Sections renders its own sections (data-page attribute): ours are among them if the user added them.
        if (container.querySelector('.verticalSection[data-page]')) {
            decorateHomeScreenSections(container);
            return;
        }

        // Wait until jellyfin-web has drawn its standard home, then add ours on top (no Home Screen Sections home).
        if (!container.querySelector('.section0') || state.homeLoading) {
            return;
        }

        var existing = container.querySelector('.rr-home-rows');
        if (existing && existing.getAttribute('data-rr-user') === state.userId && !state.homeStale) {
            return;
        }

        state.homeStale = false;
        state.homeLoading = true;
        Promise.all([
            request('GET', 'Recommendations?limit=' + (BOOT.homeRowSize || 20)),
            request('GET', 'RateThese?limit=' + (BOOT.homeRowSize || 20)).catch(function () { return { Items: [] }; })
        ]).then(function (results) {
            state.homeLoading = false;
            var block = el('div', 'rr-home-rows');
            block.setAttribute('data-rr-user', state.userId);
            if (results[0].Items && results[0].Items.length) {
                block.appendChild(rowSection('rr-home-recommended', t('forYou.recommended'), SECTION_LINK.recommended, results[0], true));
            }

            // Watched but not rated yet: no row when there is nothing to rate.
            if (results[1].Items && results[1].Items.length) {
                block.appendChild(rowSection('rr-home-ratethese', t('rateThese'), SECTION_LINK.rate, results[1], false));
            }

            var old = container.querySelector('.rr-home-rows');
            if (old) {
                old.parentNode.removeChild(old);
            }

            if (block.children.length) {
                container.insertBefore(block, container.firstChild);
            }
        }, function (err) {
            state.homeLoading = false;
            log('could not load the home rows:', err && err.message);
        });
    }

    function decorateHomeScreenSections(container) {
        // Our registered titles (server language) are relabelled in the user's language, linked to the "For you"
        // page, and the recommended cards get their explanation line.
        var titles = container.querySelectorAll('.verticalSection[data-page] .sectionTitle');
        for (var i = 0; i < titles.length; i++) {
            var title = titles[i];
            var text = (title.textContent || '').trim();
            var kind = /^(Recommended for you|Consigliati per te)$/.test(text) ? 'recommended' : (/^(Watched, to rate|Già visti, da valutare|Rate these|Valuta questi)$/.test(text) ? 'ratethese' : null);
            if (!kind || title.getAttribute('data-rr-done')) {
                continue;
            }

            title.setAttribute('data-rr-done', '1');
            title.textContent = kind === 'recommended' ? t('forYou.recommended') : t('rateThese');
            var section = title.closest('.verticalSection');
            section.classList.add(kind === 'recommended' ? 'rr-home-recommended' : 'rr-home-ratethese');
            if (!title.closest('a')) {
                var link = el('a', 'rr-section-link');
                link.href = kind === 'recommended' ? SECTION_LINK.recommended : SECTION_LINK.rate;
                title.parentNode.insertBefore(link, title);
                link.appendChild(title);
            }

            if (kind === 'recommended') {
                addExplanationsToCards(section);
            }
        }
    }

    function addExplanationsToCards(section) {
        request('GET', 'Recommendations?limit=' + (BOOT.homeRowSize || 20)).then(function (response) {
            var byId = {};
            (response.Items || []).forEach(function (item, i) { byId[normId(item.Id)] = (response.Explanations || [])[i]; });
            var cards = section.querySelectorAll('.card[data-id]');
            for (var i = 0; i < cards.length; i++) {
                var why = explanationText(byId[normId(cards[i].getAttribute('data-id'))]);
                var footer = cards[i].querySelector('.cardFooter') || cards[i].querySelector('.cardBox');
                if (why && footer && !footer.querySelector('.rr-card-why')) {
                    footer.appendChild(el('div', 'rr-card-why', why));
                }
            }
        }, function () { /* explanations are optional */ });
    }

    function renderSimilarRow(root) {
        if (!BOOT.similarRow || !isDetailRoute() || !root || !root.querySelector) {
            return;
        }

        var itemId = idFromLocation();
        if (!itemId || root.querySelector('.rr-similar-section[data-rr-item="' + itemId + '"]') || state.similarPending === itemId) {
            return;
        }

        // 10.11: .detailPageContent; 12 (both layouts): .detailPageSecondaryContainer.
        var content = root.querySelector('.detailPageContent, .detailPageSecondaryContainer, .detailPageWrapperContainer');
        if (!content) {
            return;
        }

        state.similarPending = itemId;
        getItemType(itemId).then(function (type) {
            if (type !== 'Movie' && type !== 'Series') {
                return null;
            }

            return request('GET', 'Similar/' + itemId + '?limit=' + (BOOT.similarRowSize || 12));
        }).then(function (response) {
            state.similarPending = null;
            var stale = root.querySelectorAll('.rr-similar-section');
            for (var i = 0; i < stale.length; i++) {
                stale[i].parentNode.removeChild(stale[i]);
            }

            if (!response || !response.Items || !response.Items.length || idFromLocation() !== itemId) {
                return;
            }

            var section = rowSection('rr-similar-section', t('similar.title'), null, response, true);
            section.setAttribute('data-rr-item', itemId);

            // Right below jellyfin-web's own "More like this", or at the end of the details.
            var native = root.querySelector('#similarCollapsible') || (root.querySelector('.similarContent') && root.querySelector('.similarContent').closest('.verticalSection'));
            if (native && native.parentNode) {
                native.parentNode.insertBefore(section, native.nextSibling);
            } else {
                content.appendChild(section);
            }
        }).catch(function (err) {
            state.similarPending = null;
            log('could not load similar items:', err && err.message);
        });
    }

    /* ---------- side menu entry and Plugin Pages ---------- */

    function addMenuEntry() {
        if (BOOT.menuEntry === false) {
            return;
        }

        // Legacy drawer.
        var home = document.querySelector('.mainDrawer a.navMenuOption[href="#/home"]');
        if (home && !home.parentNode.querySelector('.rr-menu-foryou')) {
            var a = el('a', 'navMenuOption lnkMediaFolder emby-button rr-menu-foryou');
            a.href = '#/ratingsrecs';
            a.appendChild(el('span', 'material-icons navMenuOptionIcon thumb_up'));
            a.appendChild(el('span', 'navMenuOptionText', t('forYou.title')));
            home.parentNode.insertBefore(a, home.nextSibling);
        }

        // Modern drawer (React/MUI): a copy of the "Home" entry, plain link without React handlers.
        var modernHome = document.querySelector('.MuiDrawer-root a[href="#/home"]');
        if (modernHome && !modernHome.parentNode.querySelector('.rr-menu-foryou')) {
            var clone = modernHome.cloneNode(true);
            clone.classList.add('rr-menu-foryou');
            clone.classList.remove('Mui-selected');
            clone.removeAttribute('aria-current');
            clone.setAttribute('href', '#/ratingsrecs');
            var label = clone.querySelector('.MuiListItemText-primary') || clone.querySelector('.MuiTypography-root');
            if (label) {
                label.textContent = t('forYou.title');
            }

            var icon = clone.querySelector('.MuiListItemIcon-root');
            if (icon) {
                icon.innerHTML = svg(THUMB_UP, false);
            }

            modernHome.parentNode.insertBefore(clone, modernHome.nextSibling);
        }

        hideDuplicateDrawerEntries();
        addModernTopBarEntry();
        addModernUserMenuEntry();
    }

    function setButtonContent(button, text) {
        // Keeps the MUI start-icon span, replaces its SVG with our thumb and the label text.
        var icon = button.querySelector('.MuiButton-startIcon, .MuiListItemIcon-root');
        if (icon) {
            icon.innerHTML = svg(THUMB_UP, false);
        }

        var label = button.querySelector('.MuiListItemText-primary, .MuiTypography-root');
        if (label) {
            label.textContent = text;
            return;
        }

        for (var i = button.childNodes.length - 1; i >= 0; i--) {
            if (button.childNodes[i].nodeType === 3) {
                button.removeChild(button.childNodes[i]);
            }
        }

        button.appendChild(document.createTextNode(text));
    }

    function addModernTopBarEntry() {
        // Modern layout on wide screens has no side menu: the libraries are buttons in the top bar.
        var bar = document.querySelector('.MuiAppBar-root .MuiToolbar-root .MuiStack-root');
        if (!bar || bar.querySelector('.rr-appbar-foryou')) {
            return;
        }

        var model = bar.querySelector('a.MuiButton-root[href="#/home?tab=1"]') || bar.querySelector('a.MuiButton-root[href^="#/"]:not([href="#/"])');
        if (!model) {
            return;
        }

        var button = model.cloneNode(true);
        button.classList.add('rr-appbar-foryou');
        button.setAttribute('href', '#/ratingsrecs');
        button.removeAttribute('aria-current');
        setButtonContent(button, t('forYou.title'));
        bar.insertBefore(button, model);
    }

    function hideDuplicateDrawerEntries() {
        // Our own drawer entry is already there: hide the Plugin Pages copy ("For you" in the server language).
        var drawers = document.querySelectorAll('.mainDrawer, .MuiDrawer-root');
        for (var i = 0; i < drawers.length; i++) {
            if (!drawers[i].querySelector('.rr-menu-foryou')) {
                continue;
            }

            var copies = drawers[i].querySelectorAll('a[href*="RatingsRecs/Web/foryou"]');
            for (var j = 0; j < copies.length; j++) {
                copies[j].classList.add('rr-hidden-duplicate');
            }
        }
    }

    function addModernUserMenuEntry() {
        var menu = document.getElementById('app-user-menu');
        if (!menu) {
            return;
        }

        // Plugin Pages adds "For you" in the server language: show it in the user's language.
        var pluginPagesItem = menu.querySelector('a[href*="RatingsRecs/Web/foryou"]');
        if (pluginPagesItem) {
            if (!pluginPagesItem.getAttribute('data-rr-label')) {
                pluginPagesItem.setAttribute('data-rr-label', '1');
                setButtonContent(pluginPagesItem, t('forYou.title'));
            }

            return;
        }

        var settings = menu.querySelector('a[href="#/mypreferencesmenu"]');
        if (!settings || menu.querySelector('.rr-usermenu-foryou')) {
            return;
        }

        var item = settings.cloneNode(true);
        item.classList.add('rr-usermenu-foryou');
        item.setAttribute('href', '#/ratingsrecs');
        setButtonContent(item, t('forYou.title'));
        settings.parentNode.insertBefore(item, settings.nextSibling);
    }

    function handlePluginPagesRoute() {
        // Plugin Pages opens #/userpluginsettings.html?pageUrl=/RatingsRecs/Web/foryou.html: show our page instead.
        var hash = location.hash || '';
        if (/^#\/userpluginsettings/i.test(hash) && /RatingsRecs(%2F|\/)Web(%2F|\/)foryou/i.test(hash)) {
            location.replace('#/ratingsrecs');
        }
    }

    function renderSection(container, title, response, withExplanations, name) {
        var section = el('section', 'rr-section');
        if (name) {
            section.id = 'rr-section-' + name;
        }

        if (title) {
            section.appendChild(el('h2', 'rr-section-title', title));
        }

        var grid = el('div', 'rr-grid');
        var serverId = apiClient().serverId ? apiClient().serverId() : '';
        (response.Items || []).forEach(function (item, i) {
            grid.appendChild(itemCard(item, withExplanations ? (response.Explanations || [])[i] : null, serverId));
        });
        section.appendChild(grid);
        container.appendChild(section);
    }

    function preferencesBlock() {
        var block = el('div', 'rr-prefs');
        var prefs = state.prefs;
        if (!prefs || !prefs.canChooseSeriesMode) {
            return block;
        }

        var label = el('label', 'rr-prefs-label', t('prefs.seriesMode'));
        var select = el('select', 'rr-prefs-select');
        select.id = 'rr-series-mode';
        label.htmlFor = select.id;
        var options = [['', t('mode.default', t('mode.' + prefs.defaultSeriesMode))], ['Series', t('mode.Series')], ['Season', t('mode.Season')], ['Both', t('mode.Both')]];
        options.forEach(function (o) {
            var option = el('option', null, o[1]);
            option.value = o[0];
            option.selected = (prefs.userSeriesMode || '') === o[0];
            select.appendChild(option);
        });
        select.addEventListener('change', function () {
            request('PUT', 'Preferences', { seriesMode: select.value || null }).then(function (updated) {
                state.prefs = updated;
                state.prefsLoadedAt = Date.now();
                toast(t('prefs.saved'), true);
                resetAll();
            }, function (err) {
                log('could not save preferences:', err && err.message);
                toast(t('saveError'));
            });
        });
        block.appendChild(label);
        block.appendChild(select);
        block.appendChild(el('p', 'rr-prefs-hint', t('prefs.hint')));
        return block;
    }

    function renderForYou(root) {
        if (!isForYouRoute()) {
            return;
        }

        // The "not found" view is <div id="fallbackPage" class="page ..."> in 10.11 and 12 (both layouts).
        var host = document.querySelector('#fallbackPage:not(.hide), .fallbackPage:not(.hide)');
        if (!host && root && root.classList && root.classList.contains('page')) {
            host = root;
        }

        if (!host) {
            return;
        }

        // Legacy header title of the "not found" view (jellyfin-web may write it again on viewshow).
        var headerTitle = document.querySelector('.skinHeader .pageTitle');
        if (headerTitle && headerTitle.textContent !== t('forYou.title')) {
            headerTitle.textContent = t('forYou.title');
        }

        var key = location.hash + '|' + state.userId;
        if (host.getAttribute('data-rr-foryou') === key) {
            return;
        }

        host.setAttribute('data-rr-foryou', key);
        host.classList.add('rr-foryou-host');
        var old = host.querySelector('.rr-foryou');
        if (old) {
            old.parentNode.removeChild(old);
        }

        var type = forYouType();
        var page = el('div', 'rr-foryou padded-left padded-right');
        page.appendChild(el('h1', 'rr-foryou-title', t('forYou.title')));
        var tabs = el('div', 'rr-tabs');
        tabs.setAttribute('role', 'tablist');
        [['Movie', 'tab.movies'], ['Series', 'tab.series']].forEach(function (tab) {
            var a = el('a', 'rr-tab' + (tab[0] === type ? ' rr-tab-active' : ''), t(tab[1]));
            a.href = '#/ratingsrecs?type=' + tab[0];
            a.setAttribute('role', 'tab');
            a.setAttribute('aria-selected', tab[0] === type ? 'true' : 'false');
            tabs.appendChild(a);
        });
        page.appendChild(tabs);
        page.appendChild(preferencesBlock());
        var body = el('div', 'rr-foryou-body');
        body.appendChild(el('p', 'rr-foryou-status', '…'));
        page.appendChild(body);
        host.appendChild(page);
        document.title = t('forYou.title');

        Promise.all([
            request('GET', 'Recommendations?limit=100&type=' + type),
            request('GET', 'RateThese?limit=40&type=' + type).catch(function () { return { Items: [] }; })
        ]).then(function (results) {
            if (host.getAttribute('data-rr-foryou') !== key) {
                return;
            }

            var recs = results[0];
            var onboarding = results[1];
            body.innerHTML = '';
            if (recs.Status === 'Computing') {
                body.appendChild(el('p', 'rr-foryou-status', t('forYou.computing')));
                setTimeout(function () {
                    if (host.getAttribute('data-rr-foryou') === key) {
                        host.removeAttribute('data-rr-foryou');
                        schedule();
                    }
                }, 5000);
                return;
            }

            if (recs.IsColdStart) {
                body.appendChild(el('p', 'rr-foryou-note', t('forYou.coldStart', String(recs.ExplicitRatingCount || 0))));
            }

            if (onboarding.Items && onboarding.Items.length) {
                renderSection(body, t('rateThese'), onboarding, false, 'rate');
            } else {
                // Nothing watched left to rate: say how to get there (also the target of #/ratingsrecs?section=rate).
                var empty = el('section', 'rr-section');
                empty.id = 'rr-section-rate';
                empty.appendChild(el('h2', 'rr-section-title', t('rateThese')));
                empty.appendChild(el('p', 'rr-foryou-status', t('forYou.rateEmpty')));
                body.appendChild(empty);
            }

            if (recs.Items && recs.Items.length) {
                renderSection(body, t('forYou.recommended'), recs, true, 'recommended');
            } else {
                body.appendChild(el('p', 'rr-foryou-status', t('forYou.empty')));
            }

            scrollToSection(host, forYouSection());
        }, function (err) {
            body.innerHTML = '';
            body.appendChild(el('p', 'rr-foryou-status', t('loadError')));
            log('could not load recommendations:', err && err.message);
        });
    }

    function scan() {
        try {
            if (!ready() || BOOT.enabled === false) {
                return;
            }

            checkUser();
            if (!state.prefs || Date.now() - state.prefsLoadedAt > PREFS_TTL_MS) {
                loadPrefs().then(function () { schedule(); });
                if (!state.prefs) {
                    return;
                }
            }

            var adapter = currentAdapter();
            var root = activePage(adapter);
            handlePluginPagesRoute();
            addMenuEntry();
            renderForYou(root);
            renderHomeRows(root);
            renderSimilarRow(root);
            scanDetail(adapter, root);
            scanCards(adapter, root);
        } catch (err) {
            log('unexpected error, page left unchanged:', err && err.message);
        }
    }

    var scheduled = false;

    function schedule() {
        if (scheduled) {
            return;
        }

        scheduled = true;
        setTimeout(function () {
            scheduled = false;
            scan();
        }, 80);
    }

    function ensureStylesheet() {
        if (document.querySelector('link[href*="RatingsRecs/Web/ratings.css"]')) {
            return;
        }

        var script = document.currentScript || document.querySelector('script[src*="RatingsRecs/Web/ratings.js"]');
        if (!script || !script.src) {
            return;
        }

        var link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = script.src.replace(/ratings\.js(\?.*)?$/, 'ratings.css$1');
        document.head.appendChild(link);
    }

    function start() {
        try {
            ensureStylesheet();
            new MutationObserver(schedule).observe(document.body, { childList: true, subtree: true });
            document.addEventListener('viewshow', function () {
                state.homeStale = true;
                schedule();
            }, true);
            window.addEventListener('hashchange', schedule);
            window.addEventListener('popstate', schedule);
            schedule();
            log('loaded, version ' + (BOOT.version || '?'));
        } catch (err) {
            log('could not start:', err && err.message);
        }
    }

    window.__ratingsRecs = { version: BOOT.version, scan: scan };
    ensureStylesheet();
    if (document.body) {
        start();
    } else {
        document.addEventListener('DOMContentLoaded', start);
    }
})();

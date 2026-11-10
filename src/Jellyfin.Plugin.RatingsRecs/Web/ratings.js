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
    var RATEABLE = { Movie: true, Series: true };
    var LEVELS = [
        { value: -3, cls: 'hate', icon: 'down2' },
        { value: -1, cls: 'watchable', icon: 'down' },
        { value: 1, cls: 'good', icon: 'up' },
        { value: 3, cls: 'amazing', icon: 'up2' }
    ];
    var THUMB_UP = 'M1 21h4V9H1v12zm22-11c0-1.1-.9-2-2-2h-6.31l.95-4.57.03-.32c0-.41-.17-.79-.44-1.06L14.17 1 7.59 7.59C7.22 7.95 7 8.45 7 9v10c0 1.1.9 2 2 2h9c.83 0 1.54-.5 1.84-1.22l3.02-7.05c.09-.23.14-.47.14-.73v-2z';
    var THUMB_UP_OUTLINE = 'M13.11 5.72l-.57 2.89c-.12.59.04 1.2.42 1.66.38.46.94.73 1.54.73H20v1.08L17.43 18H9.34c-.18 0-.34-.16-.34-.34V10.8l4.11-5.08M14 2L7.59 8.41C7.21 8.79 7 9.3 7 9.83v7.83C7 18.95 8.05 20 9.34 20h8.1c.71 0 1.36-.37 1.72-.97l2.67-6.15c.11-.25.17-.52.17-.8V11c0-1.1-.9-2-2-2h-5.5l.92-4.65c.05-.22.02-.46-.08-.66-.23-.45-.52-.86-.88-1.22L14 2zM4 9H2v11h2c.55 0 1-.45 1-1v-9c0-.55-.45-1-1-1z';

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

    function t(key, arg) {
        var strings = BOOT.strings || {};
        var text = (strings[language()] || {})[key] || (strings.en || {})[key] || key;
        return arg === undefined ? text : text.replace('{0}', arg);
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
        }
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
        return '<svg class="rr-icon' + (down ? ' rr-down' : '') + '" viewBox="0 0 24 24" aria-hidden="true" focusable="false"><path fill="currentColor" d="' + path + '"/></svg>';
    }

    function icon(kind) {
        switch (kind) {
            case 'up': return svg(THUMB_UP, false);
            case 'down': return svg(THUMB_UP, true);
            case 'up2': return '<span class="rr-double">' + svg(THUMB_UP, false) + svg(THUMB_UP, false) + '</span>';
            case 'down2': return '<span class="rr-double">' + svg(THUMB_UP, true) + svg(THUMB_UP, true) + '</span>';
            default: return svg(THUMB_UP_OUTLINE, false);
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

    function toast(text) {
        var el = document.getElementById('rr-toast');
        if (!el) {
            el = document.createElement('div');
            el.id = 'rr-toast';
            el.className = 'rr-toast';
            el.setAttribute('role', 'alert');
            document.body.appendChild(el);
        }

        el.textContent = text;
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

            if (!RATEABLE[type]) {
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
        var hearts = queryAll(root, adapter.cardHeart);
        for (var i = 0; i < hearts.length; i++) {
            var heart = hearts[i];
            if (heart.closest && heart.closest('.mainDetailButtons')) {
                continue;
            }

            var card = heart.closest ? heart.closest('.card') : null;
            var itemId = normId(heart.getAttribute('data-id') || (card && card.getAttribute('data-id')));
            var type = heart.getAttribute('data-itemtype') || (card && card.getAttribute('data-type'));
            if (itemId && type) {
                attach(heart, itemId, true, type);
            }
        }
    }

    function activePage(adapter) {
        return queryFirst(document, adapter.pageRoot) || document.body;
    }

    function scan() {
        try {
            if (!ready() || BOOT.enabled === false) {
                return;
            }

            checkUser();
            var adapter = currentAdapter();
            var root = activePage(adapter);
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
            document.addEventListener('viewshow', schedule, true);
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

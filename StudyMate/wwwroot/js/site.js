// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Swipe-to-decide deck (Phase 3). Progressive enhancement over the two
// existing <form> posts in Views/Matches/Index.cshtml (Connect / Pass) — no
// new endpoint, no fetch/AJAX. If there is no .match-card on the page (any
// other view), this does nothing and throws nothing.
(function () {
    'use strict';

    var card = document.querySelector('.match-card');
    if (!card || typeof window.PointerEvent === 'undefined') {
        return;
    }

    var connectButton = card.querySelector('[data-shortcut="c"]');
    var passButton = card.querySelector('[data-shortcut="n"]');
    if (!connectButton || !passButton) {
        // Markup doesn't match what this script expects — bail rather than
        // half-wire a deck that can't submit.
        return;
    }

    var reducedMotionQuery = window.matchMedia('(prefers-reduced-motion: reduce)');
    function prefersReducedMotion() {
        return reducedMotionQuery.matches;
    }

    // --- build the drag affordances at runtime (never in the .cshtml) ------

    var wrap = document.createElement('div');
    wrap.className = 'match-card-wrap';
    card.parentNode.insertBefore(wrap, card);
    wrap.appendChild(card);

    var shadowRaised = document.createElement('div');
    shadowRaised.className = 'match-card-shadow-raised';
    shadowRaised.setAttribute('aria-hidden', 'true');
    wrap.insertBefore(shadowRaised, card);

    function buildWash(kind, label) {
        var wash = document.createElement('div');
        wash.className = 'match-card-wash match-card-wash--' + kind;
        wash.setAttribute('aria-hidden', 'true');

        var stamp = document.createElement('span');
        stamp.className = 'match-card-stamp';
        stamp.textContent = label;

        wash.appendChild(stamp);
        card.appendChild(wash);
        return wash;
    }

    // Right = Connect (lime), left = Not now (pink), per the motion spec.
    var rightWash = buildWash('right', 'Connect');
    var leftWash = buildWash('left', 'Not now');

    // --- drag state ----------------------------------------------------

    var DIRECTION_LOCK_PX = 8;   // movement needed before deciding swipe vs. scroll
    var VELOCITY_THRESHOLD = 0.6; // px/ms
    var ROTATE_FACTOR = 0.04;    // deg per px of horizontal drag
    var ROTATE_CAP = 12;         // deg

    var activePointerId = null;
    var directionDecided = false;
    var isHorizontalDrag = false;
    var startX = 0;
    var startY = 0;
    var lastX = 0;
    var lastT = 0;
    var velocity = 0;
    var currentDx = 0;
    var currentRotation = 0;
    var cardWidth = 0;
    var threshold = 0;
    var rafHandle = null;

    function clamp(value, min, max) {
        return Math.min(max, Math.max(min, value));
    }

    function resetSettleClasses() {
        wrap.classList.remove('match-card-wrap--snap-back');
        wrap.classList.remove('match-card-wrap--exit');
    }

    function cancelScheduledVisuals() {
        if (rafHandle !== null) {
            window.cancelAnimationFrame(rafHandle);
            rafHandle = null;
        }
    }

    function clearDragState() {
        cancelScheduledVisuals();
        activePointerId = null;
        directionDecided = false;
        isHorizontalDrag = false;
        currentDx = 0;
        currentRotation = 0;
        velocity = 0;
        wrap.classList.remove('is-dragging');
        card.style.willChange = '';
    }

    function applyDragVisuals(dx) {
        if (prefersReducedMotion()) {
            // The card still follows the finger. prefers-reduced-motion exists to
            // suppress autonomous movement that can trigger vestibular symptoms —
            // spin, parallax, things flying across the viewport — not 1:1 tracking
            // of the user's own hand. A drag with no feedback whatsoever reads as
            // broken, which is worse for everyone. So: position only, no rotation,
            // no lift, and the stamp appears as a discrete state at the commit
            // threshold rather than fading in continuously.
            currentRotation = 0;
            card.style.transform = 'translateX(' + dx + 'px)';
            shadowRaised.style.opacity = '0';

            var past = threshold > 0 && Math.abs(dx) >= threshold;
            rightWash.style.opacity = (past && dx > 0) ? '1' : '0';
            leftWash.style.opacity = (past && dx < 0) ? '1' : '0';
            return;
        }

        currentRotation = clamp(dx * ROTATE_FACTOR, -ROTATE_CAP, ROTATE_CAP);
        card.style.transform = 'translateX(' + dx + 'px) rotate(' + currentRotation + 'deg)';

        var progress = threshold > 0 ? Math.min(1, Math.abs(dx) / threshold) : 0;
        shadowRaised.style.opacity = String(progress);

        if (dx > 0) {
            rightWash.style.opacity = String(progress);
            leftWash.style.opacity = '0';
        } else if (dx < 0) {
            leftWash.style.opacity = String(progress);
            rightWash.style.opacity = '0';
        } else {
            rightWash.style.opacity = '0';
            leftWash.style.opacity = '0';
        }
    }

    // Reads currentDx inside the frame rather than closing over the dx that
    // happened to schedule it. Pointers fire faster than frames, so several moves
    // can land between paints; capturing the first one made the card render a
    // stale position and visibly lag the finger.
    function scheduleDragVisuals() {
        if (rafHandle !== null) {
            return;
        }
        rafHandle = window.requestAnimationFrame(function () {
            rafHandle = null;
            applyDragVisuals(currentDx);
        });
    }

    function submitDecision(direction) {
        var behind = card.closest('.deck') ? card.closest('.deck').querySelector('.deck-shadow-1') : null;
        if (behind) {
            behind.classList.add('deck-shadow-1--promote');
        }
        if (direction === 'right') {
            connectButton.click();
        } else {
            passButton.click();
        }
    }

    function flyOut(direction, rotation) {
        wrap.classList.add('match-card-wrap--exit');
        var offscreenX = direction === 'right' ? '160vw' : '-160vw';
        // Continue the rotation the card already reached rather than
        // inventing a new fly-off angle — the fling reads as a continuation
        // of the drag, not a snap to a different value.
        card.style.transform = 'translateX(' + offscreenX + ') rotate(' + rotation + 'deg)';
        rightWash.style.opacity = direction === 'right' ? '1' : '0';
        leftWash.style.opacity = direction === 'left' ? '1' : '0';

        window.setTimeout(function () {
            submitDecision(direction);
        }, 260); // keep in sync with --duration-fly in site.css
    }

    function snapBack() {
        wrap.classList.add('match-card-wrap--snap-back');
        card.style.transform = '';
        shadowRaised.style.opacity = '0';
        rightWash.style.opacity = '0';
        leftWash.style.opacity = '0';

        window.setTimeout(function () {
            resetSettleClasses();
            card.style.willChange = '';
        }, 320); // keep in sync with --duration-deliberate in site.css
    }

    function onPointerMove(event) {
        if (event.pointerId !== activePointerId) {
            return;
        }

        var dx = event.clientX - startX;
        var dy = event.clientY - startY;

        if (!directionDecided) {
            if (Math.abs(dx) < DIRECTION_LOCK_PX && Math.abs(dy) < DIRECTION_LOCK_PX) {
                return;
            }

            directionDecided = true;

            if (Math.abs(dy) > Math.abs(dx)) {
                // Predominantly vertical: this is a scroll, not a swipe.
                // Leave it to the browser (touch-action: pan-y) and stop
                // tracking this pointer as a drag.
                isHorizontalDrag = false;
                return;
            }

            isHorizontalDrag = true;
            try {
                card.setPointerCapture(event.pointerId);
            } catch (err) {
                // Some browsers reject capture for non-primary pointers;
                // the drag still works without it, just less robustly.
            }
            wrap.classList.add('is-dragging');
            card.style.willChange = 'transform';
        }

        if (!isHorizontalDrag) {
            return;
        }

        var now = event.timeStamp;
        var dt = now - lastT;
        if (dt > 0) {
            velocity = (event.clientX - lastX) / dt;
        }
        lastX = event.clientX;
        lastT = now;
        currentDx = dx;
        scheduleDragVisuals();
    }

    function onPointerUp(event) {
        if (event.pointerId !== activePointerId) {
            return;
        }

        document.removeEventListener('pointermove', onPointerMove);
        document.removeEventListener('pointerup', onPointerUp);
        document.removeEventListener('pointercancel', onPointerUp);

        var wasHorizontalDrag = isHorizontalDrag;
        var wasCancelled = event.type === 'pointercancel';
        var dx = currentDx;
        var v = velocity;
        var rotation = currentRotation;

        clearDragState();

        if (!wasHorizontalDrag) {
            return;
        }

        var absDx = Math.abs(dx);
        var committed = !wasCancelled && (absDx >= threshold || Math.abs(v) > VELOCITY_THRESHOLD);
        if (!committed) {
            if (!prefersReducedMotion()) {
                snapBack();
            } else {
                // No animated snap-back, but the card did track the finger, so it
                // has to be put back — instantly, in one step.
                resetSettleClasses();
                card.style.transform = '';
                shadowRaised.style.opacity = '0';
                rightWash.style.opacity = '0';
                leftWash.style.opacity = '0';
            }
            return;
        }

        var direction;
        if (dx !== 0) {
            direction = dx > 0 ? 'right' : 'left';
        } else {
            // Committed by velocity alone (a flick with little travel yet).
            direction = v >= 0 ? 'right' : 'left';
        }

        if (prefersReducedMotion()) {
            // Commits immediately: no rotation, no wash, no fly-out.
            submitDecision(direction);
            return;
        }

        flyOut(direction, rotation);
    }

    function onPointerDown(event) {
        // Ignore non-primary mouse buttons; touch/pen report button 0.
        if (typeof event.button === 'number' && event.button !== 0) {
            return;
        }
        if (activePointerId !== null) {
            return;
        }
        // Don't start a new drag while a previous one is still settling.
        if (wrap.classList.contains('match-card-wrap--snap-back') || wrap.classList.contains('match-card-wrap--exit')) {
            return;
        }

        activePointerId = event.pointerId;
        directionDecided = false;
        isHorizontalDrag = false;
        startX = event.clientX;
        startY = event.clientY;
        lastX = startX;
        lastT = event.timeStamp;
        velocity = 0;
        currentDx = 0;
        cardWidth = card.getBoundingClientRect().width;
        threshold = cardWidth * 0.33;

        document.addEventListener('pointermove', onPointerMove);
        document.addEventListener('pointerup', onPointerUp);
        document.addEventListener('pointercancel', onPointerUp);
    }

    card.addEventListener('pointerdown', onPointerDown);
})();

// ============================================================================
// Motion layer, part 2. Everything below is a separate IIFE appended after
// the swipe deck above — none of it touches `card`, `wrap`, or any of the
// deck's own state, and none of it runs against .match-card (Requirement D).
// Each IIFE guards every DOM query so a page missing its target element does
// nothing and throws nothing (Requirement F).
// ============================================================================

// --- Animated nav indicator (item 4) ---------------------------------------
// Built entirely at runtime — the indicator element itself never appears in
// _Layout.cshtml, so a no-JS visitor's nav is unchanged. Active item is
// derived from the current URL; hover/focus previews other items and the
// indicator returns to the active one on mouseleave/blur. Position and size
// both travel through one `transform: translateX() scaleX()` — left/width
// are never touched (Requirement D in the motion brief).
(function () {
    'use strict';

    var nav = document.querySelector('.navbar .navbar-nav');
    if (!nav) {
        return;
    }

    var links = Array.prototype.slice.call(nav.querySelectorAll('a.nav-link'));
    if (!links.length) {
        return;
    }

    var indicator = document.createElement('span');
    indicator.className = 'nav-indicator';
    indicator.setAttribute('aria-hidden', 'true');
    nav.insertBefore(indicator, nav.firstChild);

    function pathOf(link) {
        try {
            return new URL(link.href, window.location.href).pathname.toLowerCase().replace(/\/+$/, '') || '/';
        } catch (err) {
            return (link.getAttribute('href') || '').toLowerCase();
        }
    }

    var currentPath = window.location.pathname.toLowerCase().replace(/\/+$/, '') || '/';

    function findActiveLink() {
        var best = null;
        var bestLength = -1;
        links.forEach(function (link) {
            var linkPath = pathOf(link);
            if (!linkPath || linkPath === '/') {
                return;
            }
            var matches = currentPath === linkPath || currentPath.indexOf(linkPath + '/') === 0;
            if (matches && linkPath.length > bestLength) {
                best = link;
                bestLength = linkPath.length;
            }
        });
        return best;
    }

    var activeLink = findActiveLink();

    function moveTo(link) {
        if (!link) {
            indicator.style.transform = 'translateX(0) scaleX(0)';
            return;
        }
        var navRect = nav.getBoundingClientRect();
        var linkRect = link.getBoundingClientRect();
        indicator.style.transform = 'translateX(' + (linkRect.left - navRect.left) + 'px) scaleX(' + linkRect.width + ')';
    }

    function restToActive() {
        moveTo(activeLink);
    }

    restToActive();
    window.addEventListener('resize', restToActive);

    // Web fonts loading after first paint can shift link widths slightly;
    // re-measure once they've settled rather than leaving the indicator
    // a few pixels off.
    if (document.fonts && document.fonts.ready && typeof document.fonts.ready.then === 'function') {
        document.fonts.ready.then(restToActive);
    }

    links.forEach(function (link) {
        link.addEventListener('mouseenter', function () { moveTo(link); });
        link.addEventListener('focus', function () { moveTo(link); });
        link.addEventListener('mouseleave', restToActive);
        link.addEventListener('blur', restToActive);
    });
})();

// --- Scroll-triggered reveals + colour-block entrance (items 5 and 7) ------
// One IntersectionObserver drives both .reveal (fade + rise, set by the
// views) and the existing .color-block / .demo-panel selectors (scale +
// fade). Unobserves each element once revealed, per the brief. Reduced
// motion is checked here directly, not only in CSS: rather than let items
// wait on a scroll event that may never arrive, everything is marked visible
// immediately.
(function () {
    'use strict';

    var targets = Array.prototype.slice.call(document.querySelectorAll('.reveal, .color-block, .demo-panel'));
    if (!targets.length) {
        return;
    }

    var reducedMotionQuery = window.matchMedia('(prefers-reduced-motion: reduce)');

    // Once the entrance has played, strip both classes so the element goes back
    // to its own styling. While `.reveal.is-visible` is present it pins
    // `transform`, and it outranks a plain `:hover` rule — so leaving it on
    // silently kills the hover lift on every element that revealed. Clearing it
    // is the fix; overriding it with !important only hides the conflict.
    function settle(el) {
        // Only safe for .reveal elements. Their hidden-initial state comes from
        // the .reveal class itself, so dropping both classes returns them to
        // ordinary styling. .demo-panel and .color-block are hidden by rules keyed
        // to their own permanent class names — take .is-visible away from those and
        // they snap straight back to opacity 0. They are not hover-lift targets, so
        // the pinned transform costs them nothing.
        if (!el.classList.contains('reveal')) {
            return;
        }
        var done = function () {
            el.classList.remove('reveal', 'is-visible');
            el.style.removeProperty('--reveal-delay');
        };
        // transitionend can fail to arrive (element already at its end state, tab
        // backgrounded mid-transition), so a timeout backstops it. Whichever wins,
        // done() is idempotent.
        el.addEventListener('transitionend', done, { once: true });
        window.setTimeout(done, 1200);
    }

    function revealAll() {
        targets.forEach(function (el) {
            el.classList.add('is-visible');
            settle(el);
        });
    }

    if (reducedMotionQuery.matches || typeof window.IntersectionObserver === 'undefined') {
        revealAll();
        return;
    }

    var observer = new window.IntersectionObserver(function (entries, obs) {
        entries.forEach(function (entry) {
            if (entry.isIntersecting) {
                entry.target.classList.add('is-visible');
                settle(entry.target);
                obs.unobserve(entry.target);
            }
        });
    }, { threshold: 0.15 });

    // Anything already on screen is shown straight away rather than waiting for
    // an observer callback. A scroll reveal should only ever apply to content you
    // have to scroll to; making the top of the page depend on a callback means
    // that if it never arrives — the document is never visible, the page is
    // restored from bfcache, an automated or embedded viewer never paints — the
    // content stays invisible for good. Below-fold elements still wait for the
    // scroll, which is the whole point of the effect.
    function onScreen(el) {
        var r = el.getBoundingClientRect();
        var h = window.innerHeight || document.documentElement.clientHeight;
        var w = window.innerWidth || document.documentElement.clientWidth;
        return r.top < h && r.bottom > 0 && r.left < w && r.right > 0;
    }

    targets.forEach(function (el) {
        if (onScreen(el)) {
            el.classList.add('is-visible');
            settle(el);
        } else {
            observer.observe(el);
        }
    });
})();

// Retire the staggered entrance once it has played. See the .motion-settled
// rule in site.css: a filled animation goes on holding `transform`, and that
// held value beats a :hover declaration in the cascade no matter how specific
// the selector, so the hover lift would never fire on any row that animated in.
(function () {
    'use strict';

    var items = Array.prototype.slice.call(
        document.querySelectorAll('.stagger-item, .deck-rail .rail-card, .up-next-item'));
    if (!items.length) {
        return;
    }

    items.forEach(function (el) {
        var settle = function () { el.classList.add('motion-settled'); };
        el.addEventListener('animationend', settle, { once: true });
        // animationend never arrives if the animation was suppressed (reduced
        // motion) or the tab was hidden throughout, so backstop it.
        window.setTimeout(settle, 1500);
    });
})();

// --- Marquee strip (item 6) --------------------------------------------------
// The scroll itself is pure CSS (the marquee-scroll keyframe + animation on
// .marquee-track). This only enforces that reduced motion stops it outright
// rather than merely slowing it (the CSS guard already does this too — this
// is the explicit JS-side check the brief calls for) and keeps it in sync if
// the user flips that OS setting while the page is open.
(function () {
    'use strict';

    var strip = document.querySelector('.marquee-strip');
    var track = strip ? strip.querySelector('.marquee-track') : null;
    if (!strip || !track) {
        return;
    }

    var reducedMotionQuery = window.matchMedia('(prefers-reduced-motion: reduce)');

    function applyMotionPreference() {
        if (reducedMotionQuery.matches) {
            track.style.animation = 'none';
            track.style.transform = 'none';
        } else {
            track.style.animation = '';
            track.style.transform = '';
        }
    }

    applyMotionPreference();

    if (typeof reducedMotionQuery.addEventListener === 'function') {
        reducedMotionQuery.addEventListener('change', applyMotionPreference);
    } else if (typeof reducedMotionQuery.addListener === 'function') {
        // Safari < 14
        reducedMotionQuery.addListener(applyMotionPreference);
    }
})();

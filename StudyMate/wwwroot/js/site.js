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

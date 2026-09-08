// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Swipe-to-decide card stack. Progressive enhancement over the real <form>
// posts rendered per card in Views/Matches/_MatchCard.cshtml (Connect /
// Pass) — those forms always stay real, submittable forms with a genuine
// antiforgery token; this IIFE only intercepts the TOP card's forms to
// replace a full-page POST with an instant local animation plus a
// background fetch to the same endpoints. If there is no .match-stack on
// the page (any other view, or the empty-deck state), this does nothing
// and throws nothing.
(function () {
    'use strict';

    var deck = document.querySelector('.deck');
    var stackEl = deck ? deck.querySelector('.match-stack') : null;
    if (!stackEl || !stackEl.querySelector('.match-card') || typeof window.PointerEvent === 'undefined') {
        return;
    }

    var reducedMotionQuery = window.matchMedia('(prefers-reduced-motion: reduce)');
    function prefersReducedMotion() {
        return reducedMotionQuery.matches;
    }

    // --- shared constants ------------------------------------------------

    var DIRECTION_LOCK_PX = 8;    // movement needed before deciding swipe vs. scroll
    var VELOCITY_THRESHOLD = 0.6; // px/ms
    var ROTATE_FACTOR = 0.04;     // deg per px of horizontal drag
    var ROTATE_CAP = 12;          // deg
    // The intent wash tints the card; it must not bury it. Ramping all the way to
    // opaque meant that at the commit threshold you could no longer read who you
    // were about to connect with — the stamp carries the signal, the wash only
    // colours it.
    var WASH_MAX_OPACITY = 0.42;
    var FLY_MS = 260;             // keep in sync with --duration-fly in site.css
    var SNAP_MS = 320;            // keep in sync with --duration-deliberate in site.css

    // --- deck-wide state ---------------------------------------------------
    // remainingCount/greatCount start from what the server rendered and are
    // decremented locally as decisions land — the rail is never re-fetched.

    var railRemaining = document.getElementById('rail-remaining');
    var railRemainingLabel = document.getElementById('rail-remaining-label');
    var railGreatWrap = document.getElementById('rail-great-wrap');
    var railGreat = document.getElementById('rail-great');
    var railGreatLabel = document.getElementById('rail-great-label');
    var deckSubtitle = document.getElementById('deck-subtitle');
    var flashEl = document.getElementById('deck-flash');

    var remainingCount = parseInt(deck.getAttribute('data-remaining'), 10) || 0;
    var greatCount = railGreat ? (parseInt(railGreat.textContent, 10) || 0) : 0;
    var poolExhausted = false;
    var isBusy = false;

    // The currently-interactive (top) card's parts. Reassigned by
    // setupTopCard() every time a new card is promoted — there is only ever
    // one wired up at a time.
    var top = { card: null };

    function clamp(value, min, max) {
        return Math.min(max, Math.max(min, value));
    }

    function articleOf(el) {
        if (!el) {
            return null;
        }
        return el.classList && el.classList.contains('match-card') ? el : el.querySelector('.match-card');
    }

    // Stamps every direct child of .match-stack with its position (0 = top)
    // so CSS (html.js only) can lay the two behind cards out, scaled down and
    // offset. Returns the live children in DOM order.
    function reindexStack() {
        var children = Array.prototype.slice.call(stackEl.children);
        children.forEach(function (child, i) {
            var article = articleOf(child);
            if (article) {
                article.setAttribute('data-stack-index', String(i));
            }
        });
        syncUpNext(children);
        return children;
    }

    // "Up next" is rendered once by the server, so without this it keeps naming
    // the candidate you are currently looking at once the stack advances. Rebuild
    // it from the cards actually queued behind the top one — those carry the
    // avatar, name and major already, so it stays true to what is really next.
    function syncUpNext(children) {
        var rail = document.getElementById('rail-up-next');
        if (!rail) {
            return;
        }

        var behind = children.map(articleOf).filter(Boolean).slice(1);
        Array.prototype.slice.call(rail.querySelectorAll('.up-next-item')).forEach(function (item) {
            item.remove();
        });

        if (!behind.length) {
            rail.hidden = true;
            return;
        }
        rail.hidden = false;

        behind.forEach(function (card) {
            var nameEl = card.querySelector('.candidate-name');
            var metaEl = card.querySelector('.candidate-meta');
            var avatar = card.querySelector('.avatar');
            if (!nameEl) {
                return;
            }

            var item = document.createElement('div');
            item.className = 'up-next-item';

            if (avatar) {
                var clone = avatar.cloneNode(true);
                clone.className = 'avatar avatar-sm';
                item.appendChild(clone);
            }

            var text = document.createElement('div');
            text.className = 'min-width-0';

            var name = document.createElement('div');
            name.className = 'up-next-name';
            name.textContent = nameEl.textContent.trim();
            text.appendChild(name);

            var meta = document.createElement('div');
            meta.className = 'up-next-meta';
            // The card shows "Major · Year N"; the rail only ever showed the major.
            meta.textContent = metaEl ? metaEl.textContent.trim().split('·')[0].trim() : '';
            text.appendChild(meta);

            item.appendChild(text);
            rail.appendChild(item);
        });
    }

    function behindCard() {
        return stackEl.querySelector(':scope > .match-card[data-stack-index="1"]');
    }

    // --- rail + flash + empty state -----------------------------------------

    function updateRailAfterDecision(tier) {
        remainingCount = Math.max(0, remainingCount - 1);

        if (railRemaining) {
            railRemaining.textContent = String(remainingCount);
        }
        if (railRemainingLabel) {
            railRemainingLabel.textContent = (remainingCount === 1 ? 'student' : 'students') + ' to review';
        }
        if (deckSubtitle) {
            if (remainingCount > 0) {
                deckSubtitle.textContent = remainingCount + ' to review';
                deckSubtitle.hidden = false;
            } else {
                deckSubtitle.hidden = true;
            }
        }

        if (tier === 'Great' && greatCount > 0) {
            greatCount -= 1;
            if (greatCount > 0) {
                if (railGreat) { railGreat.textContent = String(greatCount); }
                if (railGreatLabel) { railGreatLabel.textContent = 'great ' + (greatCount === 1 ? 'match' : 'matches'); }
            } else if (railGreatWrap) {
                railGreatWrap.remove();
            }
        }
    }

    function showFlash(message) {
        if (!flashEl) { return; }
        flashEl.innerHTML = '';
        if (!message) { return; }
        var div = document.createElement('div');
        div.className = 'alert alert-success py-2 small';
        div.textContent = message;
        flashEl.appendChild(div);
    }

    function showErrorFlash(message) {
        if (!flashEl) { return; }
        flashEl.innerHTML = '';
        var div = document.createElement('div');
        div.className = 'alert alert-danger py-2 small';
        div.textContent = message;
        flashEl.appendChild(div);
    }

    function showEmptyState() {
        var layout = document.getElementById('deck-layout');
        var template = document.getElementById('empty-deck-template');
        if (layout && template && template.content) {
            layout.parentNode.replaceChild(template.content.cloneNode(true), layout);
        } else if (layout) {
            layout.remove();
        }
        if (deckSubtitle) {
            deckSubtitle.hidden = true;
        }
        top = { card: null };
    }

    // --- server calls --------------------------------------------------

    // Reads the id and antiforgery token off the outgoing card's own <form> —
    // the same values a real submit of that form would have sent. `outgoingCard`
    // is detached from the document by the time this runs, but a detached node
    // is still fully readable, so that is not a problem.
    function postDecision(outgoingCard, direction) {
        var decision = direction === 'right' ? 'connect' : 'pass';
        var form = outgoingCard.querySelector('form[data-decision="' + decision + '"]');
        if (!form) {
            return Promise.reject(new Error('missing decision form'));
        }

        var idField = form.querySelector('input[name="id"]');
        var tokenField = form.querySelector('input[name="__RequestVerificationToken"]');
        var body = new URLSearchParams();
        if (idField) { body.set('id', idField.value); }
        if (tokenField) { body.set('__RequestVerificationToken', tokenField.value); }

        return fetch(form.action, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            body: body
        }).then(function (response) {
            if (!response.ok) {
                throw new Error('decision request failed: ' + response.status);
            }
            return response.json();
        });
    }

    function currentStackIds() {
        return Array.prototype.map.call(stackEl.querySelectorAll('.match-card[data-student-id]'), function (el) {
            return el.getAttribute('data-student-id');
        });
    }

    function appendCard(html) {
        var holder = document.createElement('template');
        holder.innerHTML = html.trim();
        var node = holder.content.firstElementChild;
        if (!node) { return; }
        stackEl.appendChild(node);
        reindexStack();
    }

    // Builds a placeholder the same shape as a real .match-card, for
    // refillIfNeeded to show if the fetch below is slow. Carries the
    // .match-card class so it inherits the existing stack positioning and
    // z-index rules for whichever slot it lands in, but nothing that would
    // let it be mistaken for a real, decidable card: no data-student-id
    // (excluded from currentStackIds()), no <form>s, no [data-shortcut]
    // buttons — setupTopCard() already no-ops on a card missing those
    // (same guard that protects against a malformed real card), so even a
    // skeleton that is transiently promoted to the top slot cannot become
    // interactive.
    function buildSkeletonCard() {
        var el = document.createElement('article');
        el.className = 'card match-card match-card-skeleton';
        el.setAttribute('aria-hidden', 'true');
        el.innerHTML =
            '<div class="card-body">' +
                '<div class="d-flex align-items-start mb-3">' +
                    '<span class="skeleton-bar skeleton-avatar"></span>' +
                    '<div class="ml-3 flex-grow-1">' +
                        '<span class="skeleton-bar skeleton-line skeleton-line-name"></span>' +
                        '<span class="skeleton-bar skeleton-line skeleton-line-meta"></span>' +
                    '</div>' +
                '</div>' +
                '<span class="skeleton-bar skeleton-block"></span>' +
                '<span class="skeleton-bar skeleton-line"></span>' +
                '<span class="skeleton-bar skeleton-line"></span>' +
                '<span class="skeleton-bar skeleton-line skeleton-line-short"></span>' +
            '</div>' +
            '<div class="card-footer d-flex">' +
                '<span class="skeleton-bar skeleton-btn mr-2"></span>' +
                '<span class="skeleton-bar skeleton-btn"></span>' +
            '</div>';
        return el;
    }

    function refillIfNeeded() {
        if (poolExhausted || stackEl.children.length >= 3) {
            return Promise.resolve();
        }

        var ids = currentStackIds();
        var query = ids.map(function (id) { return 'exclude=' + encodeURIComponent(id); }).join('&');

        // Only shown if the fetch is still in flight after 300ms — a fast
        // response clears the timer below before it ever fires, so nothing
        // flashes on the common case.
        var skeleton = null;
        var skeletonTimer = window.setTimeout(function () {
            if (stackEl.children.length >= 3) { return; }
            skeleton = buildSkeletonCard();
            stackEl.appendChild(skeleton);
            reindexStack();
        }, 300);

        function clearSkeleton() {
            window.clearTimeout(skeletonTimer);
            if (skeleton && skeleton.parentNode) {
                skeleton.parentNode.removeChild(skeleton);
                reindexStack();
            }
            skeleton = null;
        }

        return fetch('/Matches/Card' + (query ? ('?' + query) : ''), {
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            credentials: 'same-origin'
        }).then(function (response) {
            clearSkeleton();
            if (response.status === 204) {
                poolExhausted = true;
                return;
            }
            if (!response.ok) {
                throw new Error('card fetch failed: ' + response.status);
            }
            return response.text().then(appendCard);
        }).catch(function () {
            // A failed top-up is not a lost decision — it just leaves the stack
            // shallower than 3 until the next successful decision tries again.
            // Only a lost decision (below) is worth reloading over.
            clearSkeleton();
        });
    }

    // --- per-card drag wiring --------------------------------------------
    // Everything in this section operates on `top` (module state,
    // reassigned by setupTopCard for whichever card is currently on top) or
    // on the drag-local variables below, which reset on every pointerdown.

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
    var threshold = 0;
    var rafHandle = null;
    var draggingBehindEl = null;

    function resetSettleClasses() {
        if (top.wrap) {
            top.wrap.classList.remove('match-card-wrap--snap-back');
            top.wrap.classList.remove('match-card-wrap--exit');
        }
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
        draggingBehindEl = null;
        if (top.wrap) { top.wrap.classList.remove('is-dragging'); }
        if (top.card) { top.card.style.willChange = ''; }
    }

    function applyDragVisuals(dx) {
        if (prefersReducedMotion()) {
            // The card still follows the finger. prefers-reduced-motion exists to
            // suppress autonomous movement that can trigger vestibular symptoms —
            // spin, parallax, things flying across the viewport — not 1:1 tracking
            // of the user's own hand. A drag with no feedback whatsoever reads as
            // broken, which is worse for everyone. So: position only, no rotation,
            // no lift, no stack promotion — and the stamp appears as a discrete
            // state at the commit threshold rather than fading in continuously.
            currentRotation = 0;
            top.card.style.transform = 'translateX(' + dx + 'px)';
            top.shadowRaised.style.opacity = '0';

            var past = threshold > 0 && Math.abs(dx) >= threshold;
            top.rightWash.style.opacity = (past && dx > 0) ? String(WASH_MAX_OPACITY) : '0';
            top.leftWash.style.opacity = (past && dx < 0) ? String(WASH_MAX_OPACITY) : '0';
            top.rightWash.stampLayer.style.opacity = (past && dx > 0) ? '1' : '0';
            top.leftWash.stampLayer.style.opacity = (past && dx < 0) ? '1' : '0';
            return;
        }

        currentRotation = clamp(dx * ROTATE_FACTOR, -ROTATE_CAP, ROTATE_CAP);
        top.card.style.transform = 'translateX(' + dx + 'px) rotate(' + currentRotation + 'deg)';

        var progress = threshold > 0 ? Math.min(1, Math.abs(dx) / threshold) : 0;
        top.shadowRaised.style.opacity = String(progress);

        if (dx > 0) {
            top.rightWash.style.opacity = String(progress * WASH_MAX_OPACITY);
            top.rightWash.stampLayer.style.opacity = String(progress);
            top.leftWash.style.opacity = '0';
            top.leftWash.stampLayer.style.opacity = '0';
        } else if (dx < 0) {
            top.leftWash.style.opacity = String(progress * WASH_MAX_OPACITY);
            top.leftWash.stampLayer.style.opacity = String(progress);
            top.rightWash.style.opacity = '0';
            top.rightWash.stampLayer.style.opacity = '0';
        } else {
            top.rightWash.style.opacity = '0';
            top.leftWash.style.opacity = '0';
            top.rightWash.stampLayer.style.opacity = '0';
            top.leftWash.stampLayer.style.opacity = '0';
        }

        // Tinder-like stack feel: the card behind rises toward full size in
        // proportion to the same drag progress, so the two read as physically
        // connected. Transform/custom-property only — site.css turns
        // --stack-progress into translateY()/scale().
        if (draggingBehindEl) {
            draggingBehindEl.style.setProperty('--stack-progress', String(progress));
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

    // Detaches the decided card, promotes whatever is behind it to top, fires
    // the decision in the background, and — once that resolves — tops the
    // stack back up to three. None of this waits on the network: the next
    // card is interactive the instant this function returns.
    function finishCommit(outgoingWrap, outgoingCard, direction) {
        if (outgoingWrap && outgoingWrap.parentNode) {
            outgoingWrap.parentNode.removeChild(outgoingWrap);
        }

        updateRailAfterDecision(outgoingCard.getAttribute('data-tier'));

        var childrenAfter = reindexStack();
        var newTopArticle = articleOf(childrenAfter[0]);
        if (newTopArticle) {
            setupTopCard(newTopArticle);
        } else {
            top = { card: null };
        }

        isBusy = false;

        if (remainingCount <= 0) {
            showEmptyState();
        }

        postDecision(outgoingCard, direction)
            .then(function (data) {
                // Always resolve the flash slot, one way or the other — a Pass
                // (no message) after a Connect (message) must clear the old one,
                // the same way a fresh page load would show nothing rather than
                // repeat the last request's TempData.
                showFlash(data && data.message);
                if (remainingCount > 0) {
                    return refillIfNeeded();
                }
            })
            .catch(function () {
                // The decision may or may not have reached the server — never
                // guess. Reload so the deck reflects whatever actually happened.
                showErrorFlash('That didn’t save — reloading to check.');
                window.setTimeout(function () {
                    window.location.reload();
                }, 900);
            });
    }

    function commitTop(direction, rotation) {
        if (isBusy || !top.card) {
            return;
        }
        isBusy = true;

        var outgoingWrap = top.wrap;
        var outgoingCard = top.card;
        var behind = behindCard();

        if (prefersReducedMotion()) {
            // Commits immediately: no rotation, no wash, no fly-out, no
            // animated stack promotion.
            finishCommit(outgoingWrap, outgoingCard, direction);
            return;
        }

        outgoingWrap.classList.add('match-card-wrap--exit');
        var offscreenX = direction === 'right' ? '115%' : '-115%';
        // Continue the rotation the card already reached rather than
        // inventing a new fly-off angle — the fling reads as a continuation
        // of the drag, not a snap to a different value.
        outgoingCard.style.transform = 'translateX(' + offscreenX + ') rotate(' + rotation + 'deg)';
        top.rightWash.style.opacity = direction === 'right' ? String(WASH_MAX_OPACITY) : '0';
        top.leftWash.style.opacity = direction === 'left' ? String(WASH_MAX_OPACITY) : '0';
        top.rightWash.stampLayer.style.opacity = direction === 'right' ? '1' : '0';
        top.leftWash.stampLayer.style.opacity = direction === 'left' ? '1' : '0';

        if (behind) {
            stackEl.classList.add('is-committing');
            behind.style.setProperty('--stack-progress', '1');
        }

        window.setTimeout(function () {
            if (behind) {
                stackEl.classList.remove('is-committing');
                behind.style.removeProperty('--stack-progress');
            }
            finishCommit(outgoingWrap, outgoingCard, direction);
        }, FLY_MS);
    }

    function snapBack(behind) {
        top.wrap.classList.add('match-card-wrap--snap-back');
        top.card.style.transform = '';
        top.shadowRaised.style.opacity = '0';
        top.rightWash.style.opacity = '0';
        top.leftWash.style.opacity = '0';
        top.rightWash.stampLayer.style.opacity = '0';
        top.leftWash.stampLayer.style.opacity = '0';

        if (behind) {
            stackEl.classList.add('is-settling');
            behind.style.setProperty('--stack-progress', '0');
        }

        window.setTimeout(function () {
            resetSettleClasses();
            if (top.card) { top.card.style.willChange = ''; }
            if (behind) {
                stackEl.classList.remove('is-settling');
                behind.style.removeProperty('--stack-progress');
            }
            isBusy = false;
        }, SNAP_MS);
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
                top.card.setPointerCapture(event.pointerId);
            } catch (err) {
                // Some browsers reject capture for non-primary pointers;
                // the drag still works without it, just less robustly.
            }
            top.wrap.classList.add('is-dragging');
            top.card.style.willChange = 'transform';
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
        var behind = draggingBehindEl;

        clearDragState();

        if (!wasHorizontalDrag) {
            return;
        }

        var absDx = Math.abs(dx);
        var committed = !wasCancelled && (absDx >= threshold || Math.abs(v) > VELOCITY_THRESHOLD);
        if (!committed) {
            if (!prefersReducedMotion()) {
                isBusy = true;
                snapBack(behind);
            } else {
                // No animated snap-back, but the card did track the finger, so it
                // has to be put back — instantly, in one step.
                resetSettleClasses();
                top.card.style.transform = '';
                top.shadowRaised.style.opacity = '0';
                top.rightWash.style.opacity = '0';
                top.leftWash.style.opacity = '0';
                top.rightWash.stampLayer.style.opacity = '0';
                top.leftWash.stampLayer.style.opacity = '0';
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

        commitTop(direction, rotation);
    }

    function onPointerDown(event) {
        // Ignore non-primary mouse buttons; touch/pen report button 0.
        if (typeof event.button === 'number' && event.button !== 0) {
            return;
        }
        if (activePointerId !== null || isBusy || !top.card) {
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
        draggingBehindEl = behindCard();
        threshold = top.card.getBoundingClientRect().width * 0.33;

        document.addEventListener('pointermove', onPointerMove);
        document.addEventListener('pointerup', onPointerUp);
        document.addEventListener('pointercancel', onPointerUp);
    }

    // Trackpads expose a two-finger horizontal swipe as a sequence of wheel
    // events. Reuse the exact drag state and visual path above; vertical wheels
    // are deliberately left alone so ordinary page scrolling keeps working.
    var wheelDx = 0;
    var wheelLastDx = 0;
    var wheelLastT = 0;
    var wheelEndTimer = null;
    function finishWheelGesture() {
        wheelEndTimer = null;
        if (!wheelDx || isBusy || !top.card) { wheelDx = 0; return; }
        var dx = wheelDx;
        var v = velocity;
        var behind = draggingBehindEl;
        wheelDx = 0;
        if (Math.abs(dx) >= threshold || Math.abs(v) > VELOCITY_THRESHOLD) {
            commitTop(dx > 0 || (dx === 0 && v >= 0) ? 'right' : 'left', currentRotation);
        } else if (prefersReducedMotion()) {
            top.card.style.transform = '';
            top.shadowRaised.style.opacity = '0'; top.rightWash.style.opacity = '0'; top.leftWash.style.opacity = '0'; top.rightWash.stampLayer.style.opacity = '0'; top.leftWash.stampLayer.style.opacity = '0';
        } else {
            isBusy = true; snapBack(behind);
        }
    }
    function onWheel(event) {
        if (isBusy || !top.card || Math.abs(event.deltaX) <= Math.abs(event.deltaY)) { return; }
        event.preventDefault();
        var now = event.timeStamp;
        if (wheelLastT) { var dt = now - wheelLastT; if (dt > 0) { velocity = (event.deltaX - wheelLastDx) / dt; } }
        wheelLastT = now; wheelLastDx = event.deltaX;
        wheelDx += event.deltaX;
        threshold = top.card.getBoundingClientRect().width * 0.33;
        currentDx = wheelDx; draggingBehindEl = behindCard();
        top.wrap.classList.add('is-dragging'); top.card.style.willChange = 'transform';
        scheduleDragVisuals();
        if (wheelEndTimer) { window.clearTimeout(wheelEndTimer); }
        wheelEndTimer = window.setTimeout(finishWheelGesture, 120);
    }

    // Wraps whichever card is now on top with the drag affordances (built at
    // runtime, never in the .cshtml) and wires up its pointer/button
    // handlers. Called once at init and again every time a card is promoted.
    function setupTopCard(cardEl) {
        var connectButton = cardEl.querySelector('[data-shortcut="c"]');
        var passButton = cardEl.querySelector('[data-shortcut="n"]');
        if (!connectButton || !passButton) {
            // Markup doesn't match what this script expects — leave this card
            // as a plain, fully-functional (if un-animated) form-based card.
            top = { card: null };
            return;
        }

        cardEl.style.transform = '';
        cardEl.style.removeProperty('--stack-progress');

        var wrap = document.createElement('div');
        wrap.className = 'match-card-wrap';
        cardEl.parentNode.insertBefore(wrap, cardEl);
        wrap.appendChild(cardEl);

        var shadowRaised = document.createElement('div');
        shadowRaised.className = 'match-card-shadow-raised';
        shadowRaised.setAttribute('aria-hidden', 'true');
        wrap.insertBefore(shadowRaised, cardEl);

        // The tint and the stamp are siblings, not parent and child. Nesting the
        // stamp inside the wash made it inherit the tint's opacity, so the verdict
        // came out ghosted and the card's own text showed through it. They ramp
        // together but to different ceilings: the tint stays translucent so the
        // candidate is still readable, the stamp goes fully opaque.
        function buildWash(kind, label) {
            var wash = document.createElement('div');
            wash.className = 'match-card-wash match-card-wash--' + kind;
            wash.setAttribute('aria-hidden', 'true');
            cardEl.appendChild(wash);

            var stampLayer = document.createElement('div');
            stampLayer.className = 'match-card-stamp-layer match-card-stamp-layer--' + kind;
            stampLayer.setAttribute('aria-hidden', 'true');

            var stamp = document.createElement('span');
            stamp.className = 'match-card-stamp';
            stamp.textContent = label;

            stampLayer.appendChild(stamp);
            cardEl.appendChild(stampLayer);

            wash.stampLayer = stampLayer;
            return wash;
        }

        // Right = Connect (lime), left = Not now (pink), per the motion spec.
        var rightWash = buildWash('right', 'Connect');
        var leftWash = buildWash('left', 'Not now');

        top = {
            card: cardEl,
            wrap: wrap,
            shadowRaised: shadowRaised,
            rightWash: rightWash,
            leftWash: leftWash
        };

        cardEl.addEventListener('pointerdown', onPointerDown);
        cardEl.addEventListener('wheel', onWheel, { passive: false });

        function onControlClick(direction) {
            return function (event) {
                event.preventDefault();
                if (isBusy) { return; }
                commitTop(direction, 0);
            };
        }

        connectButton.addEventListener('click', onControlClick('right'));
        passButton.addEventListener('click', onControlClick('left'));
    }

    // --- keyboard shortcuts (moved out of Views/Matches/Index.cshtml) ------
    // Scoped to the top card only: with three cards rendered there are three
    // sets of [data-shortcut] buttons, and a plain document-wide selector
    // would not reliably hit the interactive one. Ignored while typing.
    document.addEventListener('keydown', function (event) {
        if (event.metaKey || event.ctrlKey || event.altKey) { return; }

        var tag = (event.target.tagName || '').toLowerCase();
        if (tag === 'input' || tag === 'textarea' || tag === 'select') { return; }

        var key = event.key.toLowerCase();
        if (key !== 'c' && key !== 'n') { return; }
        if (isBusy || !top.card) { return; }

        event.preventDefault();
        commitTop(key === 'c' ? 'right' : 'left', 0);
    });

    // --- init ------------------------------------------------------------

    var initialChildren = reindexStack();
    var initialTop = articleOf(initialChildren[0]);
    if (initialTop) {
        setupTopCard(initialTop);
    }
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

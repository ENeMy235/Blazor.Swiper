// Interop module for the Swiper Blazor component. The Swiper Element (<swiper-container>) is rendered
// with init="false" so we can assign parameters before calling initialize().
//
// Beyond forwarding calls, this module closes gaps that every host would otherwise have to work around
// itself: telling a user's swipe apart from a code-driven move, keeping autoHeight true after init,
// making programmatic navigation work in cssMode, and keeping Swiper's own re-anchors from undoing a
// move that is already in flight. Per-instance state lives on `element.__blazorSwiper`, which is on the
// element rather than on the Swiper instance so it survives a destroy.
//
// The decisions behind those behaviours live in swiper-policy.js, which touches neither Swiper nor
// the DOM and is covered by tests/interop/swiper-policy.test.mjs. This file is the wiring.

import {
    applicableOptions,
    changedOptions,
    optionalSpeed,
    navigationMode,
    isIntentArmed,
    shouldDisarmIntent,
    shouldReanchor,
    scrollPlan,
    scrollPositionAt,
    isSwiperCorrection,
    isInitPhaseEvent,
    isInitOnlyParam,
    isHighFrequencyEvent,
    shouldSendThrottledEvent,
    extractPaginationTemplates,
    renderTemplate,
    formatFractionNumber,
    virtualExternalOptions
} from "./swiper-policy.js";

// Swiper Element dispatches every Swiper event as a DOM event named `swiper` + the lowercased event
// name, and hands the emit arguments through `detail` with the Swiper instance itself in front.
const EVENT_PREFIX = "swiper";

function hostState(element) {
    element.__blazorSwiper ??= {
        isUserDriven: false,
        intendedIndex: null,
        anchorIndex: null,
        anchorSwiper: null,
        anchorObserver: null,
        slideSetObserver: null,
        slideResizeObserver: null,
        endScrollMove: null,
        dotNetRef: null,
        appliedOptions: {},
        eventThrottleMs: 0,
        lastSentAt: new Map(),
        listeners: []
    };
    return element.__blazorSwiper;
}

// The element's Swiper, or null when there is none to call. A destroyed instance counts as none:
// <swiper-container> destroys its Swiper when it leaves the DOM, and Swiper's destroy strips every own
// property (params included) but clears `.swiper` only on the shadow `.swiper` div it was built on - so
// the host keeps pointing at the gutted instance, and anything that reads it throws.
function liveSwiper(element) {
    const swiper = element?.swiper;
    return isLiveSwiper(element, swiper) ? swiper : null;
}

// Whether `swiper` is still the slider its host is showing. Deferred work - an observer, an animation frame,
// a listener, an awaited companion - holds on to the instance it was set up for, and by the time it runs that
// instance may have been destroyed (the container left the DOM) or replaced by a new one on the same host.
// Acting on either is wrong: a destroyed instance throws on its missing params, and a replaced one moves a
// slider nobody can see while the one on screen goes uncorrected.
function isLiveSwiper(element, swiper) {
    return !!swiper && !swiper.destroyed && element?.swiper === swiper;
}

// The instance that raised a Swiper event, which rides in detail[0]. It is the one to read rather than
// element.swiper: while a replacement is being constructed it announces its own events, and the host still
// names its destroyed predecessor until the constructor returns.
function emitterOf(event) {
    const swiper = Array.isArray(event.detail) ? event.detail[0] : null;
    return swiper && !swiper.destroyed ? swiper : null;
}

export async function initialize(element, options, dotNetRef, subscribedEvents, eventThrottleMs, isVirtualExternal) {
    // The host can tear the slider down before this runs - a conditionally rendered pager collapsing on the
    // very tap that scheduled it - and the reference then marshals to null. Awaiting the element definition
    // widens that window further, so re-check after it too. Without this, initialising a slider that no
    // longer exists throws out of OnAfterRenderAsync and into the host's error boundary.
    if (!element) {
        return;
    }

    await customElements.whenDefined("swiper-container");

    if (!element.isConnected) {
        return;
    }

    const state = hostState(element);
    state.dotNetRef = dotNetRef;
    state.eventThrottleMs = eventThrottleMs ?? 0;

    const subscriptions = subscribedEvents ?? [];

    // beforeInit/init/afterInit ARE the initialization, so they are the one set of listeners that has to
    // be attached before it rather than after. Everything else keeps the ordering that stops Swiper's
    // own opening announcement being forwarded as news - see the comment on initialize() below.
    for (const name of subscriptions.filter(isInitPhaseEvent)) {
        subscribe(element, name);
    }

    applyOptions(element, withVirtualExternal(element, options, isVirtualExternal));

    // Listeners are attached AFTER initialize() on purpose. Swiper announces its starting position from
    // inside init() (runCallbacksOnInit defaults on), and that announcement is not news to the host - it
    // is the position the host just asked for. Forwarding it makes every consumer write an "ignore the
    // first one" guard, and in a two-way binding it reports slide 0 back before the host has settled.
    element.initialize();

    const swiper = liveSwiper(element);

    for (const name of subscriptions.filter(name => !isInitPhaseEvent(name))) {
        subscribe(element, name);
    }

    attachInternalListeners(element);

    if (swiper) {
        attachIntendedIndexGuard(element, swiper);

        if (swiper.params.autoHeight) {
            attachLiveAutoHeight(element, swiper);
        }

        // Deliberately not awaited: the companion may not exist yet, and holding initialize() open
        // until it does would keep the slider hidden behind the reveal handshake indefinitely.
        wireCompanionsFromSelectors(element, swiper);
    }
}

/**
 * The options with virtual slides pointed at the host, when the host asked to render them.
 *
 * The callback is built here rather than in the policy because it is the one part that needs the
 * live Swiper: the offset belongs on a CSS property that direction and text direction pick between,
 * and only the instance knows which. Everything the host renders is a slide element it owns, so
 * Swiper never touches the DOM on this path at all.
 */
function withVirtualExternal(element, options, isVirtualExternal) {
    if (!isVirtualExternal || !options?.virtual) {
        return options;
    }

    const renderExternal = function (window) {
        const offsetProperty = this.rtlTranslate
            ? "right"
            : this.isHorizontal()
                ? "left"
                : "top";

        invoke(element, "OnVirtualRenderInternal", window.from, window.to, window.offset, offsetProperty);
    };

    return { ...options, virtual: virtualExternalOptions(options.virtual, renderExternal) };
}

// --- companions named by selector ----------------------------------------------------------------

// Swiper's own thumbs and controller modules take a live Swiper instance and nothing else, so a
// selector has to be resolved on this side. Doing it here rather than from a component reference is
// what makes the two sliders order-independent: an @ref is still null while the sibling's markup is
// being evaluated, but a selector can simply wait for whichever slider initializes last.
async function wireCompanionsFromSelectors(element, swiper) {
    // Both selectors are read before anything is awaited: the owner can be destroyed while it waits for a
    // companion, and a destroyed instance has no params left to read the second selector from.
    const thumbsSelector = selectorOf(swiper.params.thumbs?.swiper);
    const controlSelector = selectorOf(swiper.params.controller?.control);

    if (thumbsSelector) {
        const target = await resolveSwiperElement(thumbsSelector);
        if (target && isLiveSwiper(element, swiper)) {
            setThumbs(element, target);
        }
    }

    if (controlSelector) {
        const target = await resolveSwiperElement(controlSelector);
        if (target && isLiveSwiper(element, swiper)) {
            setController(element, target);
        }
    }
}

function selectorOf(value) {
    return typeof value === "string" && value.length > 0 ? value : null;
}

// Resolves once the target element has a Swiper on it. Either slider can be the one that finishes
// first, so an element that exists but is still initializing is waited on rather than given up on.
function resolveSwiperElement(selector) {
    const target = document.querySelector(selector);

    if (!target) {
        console.warn(`[Blazor.Swiper] No element matches '${selector}', so the companion slider was not wired.`);
        return Promise.resolve(null);
    }

    if (liveSwiper(target)) {
        return Promise.resolve(target);
    }

    return new Promise(resolve => {
        const onInitialized = () => {
            target.removeEventListener("swiperafterinit", onInitialized);
            resolve(target);
        };
        target.addEventListener("swiperafterinit", onInitialized);
    });
}

// --- options -----------------------------------------------------------------------------------------

// Applied as element properties, which is where Swiper reads them from on initialize(), and which is
// also the only route that survives a value HTML attributes cannot carry - a nested options object, or
// the render functions the pagination templates become.
function applyOptions(element, options) {
    const state = hostState(element);
    const entries = applicableOptions(options);

    for (const [key, value] of entries) {
        element[key] = prepareOption(key, value);
    }

    state.appliedOptions = Object.fromEntries(entries);
}

/**
 * Pushes the members that changed since the last applied set onto a live slider.
 *
 * Swiper Element defines a setter for every parameter that forwards to its own update path, so an
 * assignment here is all it takes for the ones Swiper can re-apply. The ones it cannot are reported
 * back to .NET rather than silently dropped.
 */
export function updateOptions(element, options) {
    if (!liveSwiper(element)) {
        return [];
    }

    const state = hostState(element);
    const changes = changedOptions(state.appliedOptions, options);

    for (const [key, value] of changes) {
        element[key] = prepareOption(key, value);

        // Assigning one of these reaches Swiper and does nothing, because Swiper only read it while it
        // was initializing. Saying so beats leaving a caller to wonder why their change had no effect.
        if (isInitOnlyParam(key)) {
            console.warn(`[Blazor.Swiper] '${key}' is only read while Swiper initializes, so changing it on a running slider has no effect.`);
        }
    }

    state.appliedOptions = Object.fromEntries(applicableOptions(options));

    return changes.map(([key]) => key);
}

// The pagination templates are the wrapper's own members rather than Swiper parameters, so they are
// turned into the functions Swiper actually calls before the object reaches the element.
function prepareOption(key, value) {
    if (key !== "pagination" || !value || typeof value !== "object") {
        return value;
    }

    const { templates, parameters } = extractPaginationTemplates(value);
    const prepared = { ...parameters };

    if (typeof templates.renderBulletTemplate === "string") {
        prepared.renderBullet = (index, className) => renderTemplate(templates.renderBulletTemplate, { index: index + 1, className });
    }

    if (typeof templates.renderFractionTemplate === "string") {
        prepared.renderFraction = (currentClass, totalClass) => renderTemplate(templates.renderFractionTemplate, { currentClass, totalClass });
    }

    if (typeof templates.renderProgressbarTemplate === "string") {
        prepared.renderProgressbar = (fillClass) => renderTemplate(templates.renderProgressbarTemplate, { fillClass });
    }

    if (typeof templates.renderCustomTemplate === "string") {
        prepared.renderCustom = (swiper, current, total) => renderTemplate(templates.renderCustomTemplate, { current, total });
    }

    if (templates.fractionMinimumDigits) {
        prepared.formatFractionCurrent = (value) => formatFractionNumber(value, templates.fractionMinimumDigits);
        prepared.formatFractionTotal = (value) => formatFractionNumber(value, templates.fractionMinimumDigits);
    }

    return prepared;
}

// --- events ------------------------------------------------------------------------------------------

// The bookkeeping the wrapper needs whether or not the host subscribed to anything: which of the two
// kinds of slide change this is, and whether a move the host asked for is still in flight.
function attachInternalListeners(element) {
    // The drag has moved the slider for the first time. This is the only signal that a slide change came
    // from the user rather than from code: loop mode re-announces the slide it LEFT after a programmatic
    // move, and that echo is indistinguishable from a swipe by index alone. Deliberately NOT touchstart -
    // that also fires for a plain tap on a button inside a slide, which is usually what triggered the
    // programmatic move in the first place. Cleared on transitionend, since the snap outlives the pointer.
    listen(element, "sliderFirstMove", () => {
        hostState(element).isUserDriven = true;
        abandonScrollMove(element);
    });

    listen(element, "transitionEnd", (event) => {
        const swiper = emitterOf(event);
        if (!swiper) {
            return;
        }

        const state = hostState(element);
        // Not every transitionend belongs to the host's move - see shouldDisarmIntent.
        if (shouldDisarmIntent(swiper.realIndex, state.intendedIndex, state.isUserDriven)) {
            state.intendedIndex = null;
        }
        state.isUserDriven = false;
    });

    // Always forwarded rather than subscribed to, because the component's ActiveIndex - and so the
    // two-way binding built on it - has to stay in step whether or not the host wired a callback.
    listen(element, "slideChange", (event) => {
        const swiper = emitterOf(event);
        if (!swiper) {
            return;
        }

        // realIndex is the logical slide index; in loop mode it differs from activeIndex (which counts
        // the shifted/duplicated slides). It equals activeIndex when loop is off, so it is always correct.
        invoke(element, "OnSlideChangeInternal", swiper.realIndex, hostState(element).isUserDriven);
    });
}

function subscribe(element, name) {
    listen(element, name, (event) => {
        const swiper = emitterOf(event);

        // beforeInit, init and afterInit are raised from inside the Swiper constructor, which is the
        // expression whose result becomes element.swiper - so for those three there is no instance on
        // the element yet, and requiring one would drop exactly the events this subscription path
        // attaches early to catch. They carry no payload, so there is nothing to read off it either.
        if (!swiper && !isInitPhaseEvent(name)) {
            return;
        }

        if (isHighFrequencyEvent(name) && !passesThrottle(element, name)) {
            return;
        }

        // detail[0] is the Swiper instance itself, which must never cross the interop boundary.
        const args = Array.isArray(event.detail) ? event.detail.slice(1) : [];
        invoke(element, "OnSwiperEventInternal", name, JSON.stringify(eventPayload(name, swiper, args)));
    }, "subscription");
}

// Every listener is recorded so destroy() can take them all off again. The element outlives the Swiper
// instance - Blazor owns it - so a listener left behind would keep firing against a destroyed slider.
// `kind` separates the wrapper's own bookkeeping from the host's subscriptions, because the second set
// comes and goes with the callbacks the host has wired and the first must never be removed.
function listen(element, name, handler, kind = "internal") {
    const domEvent = `${EVENT_PREFIX}${name.toLowerCase()}`;
    element.addEventListener(domEvent, handler);
    hostState(element).listeners.push({ name, domEvent, handler, kind });
}

/**
 * Brings the listened-for events in line with the callbacks the host currently has wired.
 *
 * A host can subscribe conditionally - a diagnostics panel that watches `progress` only while it is
 * open - so the set is not fixed at init. Only the host's own subscriptions are touched; the
 * wrapper's bookkeeping listeners stay whatever the host does.
 */
export function setSubscriptions(element, names) {
    if (!element) {
        return;
    }

    const state = hostState(element);
    const wanted = new Set(names ?? []);
    const kept = [];

    for (const listener of state.listeners) {
        if (listener.kind === "subscription" && !wanted.has(listener.name)) {
            element.removeEventListener(listener.domEvent, listener.handler);
            continue;
        }

        if (listener.kind === "subscription") {
            wanted.delete(listener.name);
        }

        kept.push(listener);
    }

    state.listeners = kept;

    for (const name of wanted) {
        subscribe(element, name);
    }
}

function passesThrottle(element, name) {
    const state = hostState(element);
    const now = performance.now();

    if (!shouldSendThrottledEvent(state.lastSentAt.get(name) ?? null, now, state.eventThrottleMs)) {
        return false;
    }

    state.lastSentAt.set(name, now);
    return true;
}

function invoke(element, method, ...args) {
    hostState(element).dotNetRef?.invokeMethodAsync(method, ...args);
}

// What each event is actually about, projected onto something that can be serialized. Swiper hands
// several of them a DOM element or a DOM event, neither of which can cross the interop boundary, so
// those become the index of the slide involved and the coordinates of the pointer.
function eventPayload(name, swiper, args) {
    if (!swiper) {
        return null;
    }

    switch (name) {
        case "progress":
            return args[0] ?? swiper.progress;
        case "setTranslate":
            return args[0] ?? swiper.translate;
        case "setTransition":
        case "beforeTransitionStart":
            return args[0] ?? 0;
        case "activeIndexChange":
            return swiper.activeIndex;
        case "realIndexChange":
            return swiper.realIndex;
        case "snapIndexChange":
            return swiper.snapIndex;
        case "slidesLengthChange":
        case "slidesGridLengthChange":
        case "snapGridLengthChange":
        case "slidesUpdated":
            return swiper.slides.length;
        case "breakpoint":
            return swiper.currentBreakpoint;
        case "keyPress":
            return String(args[0] ?? "");
        case "scroll":
            return { deltaX: args[0]?.deltaX ?? 0, deltaY: args[0]?.deltaY ?? 0 };
        case "autoplayTimeLeft":
            return { timeLeft: args[0] ?? 0, percentage: args[1] ?? 0 };
        case "zoomChange":
            return { scale: args[0] ?? 1, slideIndex: slideIndexOf(swiper, args[2]) };
        case "hashChange":
        case "hashSet":
            return typeof location === "undefined" ? "" : location.hash;
        case "touchStart":
        case "touchMove":
        case "touchMoveOpposite":
        case "touchEnd":
        case "sliderMove":
        case "sliderFirstMove":
        case "click":
        case "tap":
        case "doubleTap":
        case "doubleClick":
        case "scrollbarDragStart":
        case "scrollbarDragMove":
        case "scrollbarDragEnd":
            return pointerPayload(swiper, args[0]);
        default:
            return null;
    }
}

function pointerPayload(swiper, event) {
    return {
        clientX: event?.clientX ?? 0,
        clientY: event?.clientY ?? 0,
        // clickedIndex is only set for the events that follow a click or tap; elsewhere the pointer is
        // not over a particular slide as far as Swiper is concerned.
        slideIndex: typeof swiper.clickedIndex === "number" ? swiper.clickedIndex : -1
    };
}

function slideIndexOf(swiper, slideEl) {
    if (!slideEl || !swiper.slides) {
        return -1;
    }

    return Array.prototype.indexOf.call(swiper.slides, slideEl);
}

// --- programmatic navigation -------------------------------------------------------------------------

export function slideTo(element, index, speed) {
    const swiper = liveSwiper(element);
    if (!swiper) {
        return;
    }

    hostState(element).intendedIndex = index;

    switch (navigationMode(swiper.params)) {
        case "scroll":
            scrollToSlide(element, swiper, index, speed);
            break;
        case "loop":
            swiper.slideToLoop(index, optionalSpeed(speed));
            break;
        default:
            swiper.slideTo(index, optionalSpeed(speed));
            break;
    }
}

// cssMode moves the slider by scrolling the wrapper, and Swiper drives that with a native smooth scroll.
// That is unusable under scroll-snap: `scroll-snap-type: mandatory` yanks every intermediate position back
// to a snap point, and any re-render during the scroll cancels it outright, leaving the slider where it
// started. Writing scrollLeft per frame is not cancellable, so the animation always completes.
//
// There is one move per slider, and a new request ends the one in flight. Left running, the older move
// keeps writing its own target every frame and can be the one that lands last - a request that finds the
// slider already on its slide starts no animation at all, so nothing would even compete with it. Each
// move also hands scroll-snap back to the value it found, which for a move started during another is that
// one's "none": snapping would then stay off for good, and the slider would rest wherever a swipe is
// released, between two slides.
//
// Writing the offset every frame also overwrites whatever else moves the slider meanwhile, so the move
// steps aside for all of it (abandonScrollMove): Swiper navigating by any route, an anchor and a drag.
function scrollToSlide(element, swiper, index, speed) {
    const state = hostState(element);
    const wrapper = swiper.wrapperEl;

    // Both offsets are read before the move in flight is ended. Ending it hands scroll-snap back, and a
    // layout read after that re-snaps the wrapper on the spot - so this move would start from the nearest
    // slide rather than from where the slider is, a jump that can point away from the new target.
    const start = wrapper.scrollLeft;
    const plan = scrollPlan(start, slideOffset(swiper, index), speed, swiper.params.speed);
    state.endScrollMove?.();

    if (plan.kind === "none") {
        return;
    }

    if (plan.kind === "instant") {
        wrapper.scrollLeft = plan.target;
        return;
    }

    const previousSnapType = wrapper.style.scrollSnapType;
    wrapper.style.scrollSnapType = "none";

    let frameRequest = 0;
    let isClockArmed = false;
    let startTime = null;

    const end = () => {
        cancelAnimationFrame(frameRequest);
        delete swiper.slideTo;
        if (state.endScrollMove === end) {
            state.endScrollMove = null;
        }
        wrapper.style.scrollSnapType = previousSnapType;
    };

    // Swiper's arrows, pagination and keyboard never pass through this module, and none of them announces
    // a move before making it. What they do share - with slideNext, slidePrev and every other way Swiper
    // navigates - is that they end in the instance's own slideTo, so that is where a move in flight hears
    // that the slider has been sent somewhere else. The method is shadowed on the instance for as long as
    // the move runs and no longer.
    //
    // The wheel comes the same way and needs no listener here: in cssMode Swiper puts its own on the wrapper,
    // cancels the native scroll and steps a slide along whichever axis the wheel turned.
    //
    // Swiper moves first and the move is abandoned after, so Swiper reads the slider's position while
    // scroll-snap is still suspended - the other way round, the restored snapping pulls the slider onto the
    // nearest slide before Swiper has looked at it. A call Swiper turns down leaves the move running.
    const swiperSlideTo = swiper.slideTo;
    swiper.slideTo = function (slideIndex, slideSpeed, runCallbacks, isInternal, ...rest) {
        const hasMoved = swiperSlideTo.call(this, slideIndex, slideSpeed, runCallbacks, isInternal, ...rest);
        if (hasMoved && !isSwiperCorrection(slideSpeed, isInternal)) {
            abandonScrollMove(element);
        }
        return hasMoved;
    };

    const step = (now) => {
        if (!isLiveSwiper(element, swiper)) {
            end();
            return;
        }

        // The clock starts on the second frame rather than at the request. A host usually asks for the move
        // from the same event that re-renders its slides, so the main thread is gone for a while right after
        // the call and the first frame then has that render to lay out and paint. A clock already running
        // across either would find most of the duration spent and jump instead of animating.
        if (!isClockArmed) {
            isClockArmed = true;
            frameRequest = requestAnimationFrame(step);
            return;
        }

        // The target is measured every frame rather than once at the request: a resize part-way moves the
        // slides, and the offset taken before it then belongs to a different slide, or to none.
        startTime ??= now;
        const elapsed = now - startTime;
        const distance = slideOffset(swiper, index) - start;
        wrapper.scrollLeft = scrollPositionAt(start, distance, elapsed, plan.duration);
        if (elapsed < plan.duration) {
            frameRequest = requestAnimationFrame(step);
            return;
        }

        end();
    };

    state.endScrollMove = end;
    frameRequest = requestAnimationFrame(step);
}

function slideOffset(swiper, index) {
    return swiper.slides[index]?.offsetLeft ?? 0;
}

// Ends the cssMode move in flight because something other than a newer slideTo is taking the slider over,
// and stands its intent down with it: the slider is no longer heading for that slide, so a later resize
// must not put it there.
function abandonScrollMove(element) {
    const state = element?.__blazorSwiper;
    if (!state?.endScrollMove) {
        return;
    }

    state.endScrollMove();
    state.intendedIndex = null;
}

export function slideNext(element, speed) {
    liveSwiper(element)?.slideNext(optionalSpeed(speed));
}

export function slidePrev(element, speed) {
    liveSwiper(element)?.slidePrev(optionalSpeed(speed));
}

export function slideReset(element, speed) {
    liveSwiper(element)?.slideReset(optionalSpeed(speed));
}

export function slideToClosest(element, speed) {
    liveSwiper(element)?.slideToClosest(optionalSpeed(speed));
}

export function slideToClickedSlide(element) {
    liveSwiper(element)?.slideToClickedSlide();
}

// Swiper's resize handling ends by re-anchoring onto the index it holds, and it defers that by a frame.
// When the host moves the slider during the same interaction that changed the size, the deferred re-anchor
// carries the PRE-move index, lands last, and silently undoes the move. Re-asserting the index the host
// actually asked for - and only while its move is still in flight - keeps the host authoritative without
// disturbing a slider at rest or a move the user is making.
function attachIntendedIndexGuard(element, swiper) {
    listen(element, "resize", () => {
        const state = hostState(element);
        // Deliberately not shouldReanchor: the drift this defends against happens in Swiper's own
        // deferred frame, so at this point the index still matches and the check would skip the
        // very case the guard exists for. Only armed-ness can be judged this early.
        if (!isIntentArmed(state.intendedIndex) || state.isUserDriven) {
            return;
        }

        requestAnimationFrame(() => {
            if (!isLiveSwiper(element, swiper)) {
                return;
            }
            // A cssMode move still in flight follows its slide through the resize by itself, and writes its
            // own offset on the next frame anyway - anchoring here would only flash the target for one.
            if (state.endScrollMove) {
                return;
            }
            if (shouldReanchor(swiper.realIndex, state.intendedIndex, state.isUserDriven)) {
                applyAnchor(swiper, state.intendedIndex);
            }
        });
    });
}

// --- live autoHeight ---------------------------------------------------------------------------------

// Swiper measures autoHeight once, against whatever the slides contained at init. Any content that arrives
// later - an async load resolving, an image decoding, a lazily mounted section - leaves the track pinned to
// the height it saw first, which clips the real content. Observing the slides keeps the measurement true.
function attachLiveAutoHeight(element, swiper) {
    const state = hostState(element);

    // In cssMode the wrapper IS the scroll container, with overflow:auto on BOTH axes - so it stays
    // vertically scrollable down to the TALLEST slide no matter which one is active, and a short slide
    // scrolls on into empty space owned by a taller sibling. autoHeight already pins the wrapper to the
    // active slide, so clip the rest. Only in cssMode: elsewhere the wrapper is a plain transformed track,
    // and overflow-y:hidden would force computed overflow-x from visible to auto, turning it into a
    // one-slide-wide clip box that travels with the transform - every slide but the first would vanish.
    if (swiper.params.cssMode) {
        swiper.wrapperEl.style.overflowY = "hidden";
    }

    // Removing the container destroys this instance without the interop's destroy() ever running, so nothing
    // else disconnects these observers. Each callback stops them once the instance is no longer the live one -
    // only these two, since the state may already hold a later instance's by then.
    let slideSetObserver = null;
    let slideResizeObserver = null;
    const stopObserving = () => {
        slideSetObserver?.disconnect();
        slideResizeObserver?.disconnect();
    };

    const updateHeight = () => {
        if (!isLiveSwiper(element, swiper)) {
            stopObserving();
            return;
        }
        swiper.updateAutoHeight(0);
    };

    const observeSlides = () => {
        if (!isLiveSwiper(element, swiper)) {
            stopObserving();
            return;
        }
        slideResizeObserver?.disconnect();
        slideResizeObserver = new ResizeObserver(updateHeight);
        state.slideResizeObserver = slideResizeObserver;
        for (const slide of swiper.slides) {
            slideResizeObserver.observe(slide);
        }
        swiper.updateAutoHeight(0);
    };

    // A slide added or removed by the host is a new set to observe, and a new height to measure.
    slideSetObserver = new MutationObserver(observeSlides);
    state.slideSetObserver = slideSetObserver;
    slideSetObserver.observe(swiper.slidesEl, { childList: true });

    // The ResizeObserver reacts to a slide's size changing, not to a different slide becoming active - and
    // in cssMode Swiper's own transition-driven autoHeight never runs, since it has no transition events.
    listen(element, "slideChange", updateHeight);

    observeSlides();
}

// --- anchoring ---------------------------------------------------------------------------------------

// Recalculate and settle instantly on `index`. update() on its own is not enough: it finishes with
// slideTo(swiper.activeIndex) using the index from BEFORE the mutation, which addresses a different slide
// once the collection changed. Both steps run in one turn so the intermediate position is never laid out.
function applyAnchor(swiper, index) {
    // slideTo obeys allowSlideNext/allowSlidePrev, but this is a correction rather than navigation, so the
    // caller's swipe locks must not be able to veto it.
    const wasSlideNextAllowed = swiper.allowSlideNext;
    const wasSlidePrevAllowed = swiper.allowSlidePrev;
    swiper.allowSlideNext = true;
    swiper.allowSlidePrev = true;

    swiper.update();
    swiper.slideTo(index, 0, false, true);

    swiper.allowSlideNext = wasSlideNextAllowed;
    swiper.allowSlidePrev = wasSlidePrevAllowed;
}

// Re-anchor onto `index` the moment the host framework next adds or removes slide elements itself.
//
// Call this BEFORE the mutation. A framework that owns the slide DOM (Blazor, React, ...) shifts every
// later slide sideways when it removes one that sits before the active slide, while Swiper's transform
// still points at the old offset - so the wrong slide is on screen until Swiper is told. Correcting from
// an interop call made after the mutation cannot win that race: the call is a separate task, so the
// browser is free to paint in between (measured at 9-66ms on real hardware, i.e. one to four wrong frames).
//
// A MutationObserver callback is delivered at the microtask checkpoint - after the framework's DOM patch
// but before the next paint - so the correction is guaranteed to land in the same frame as the mutation
// that caused it. This is a platform ordering guarantee rather than a timing race.
export function armAnchor(element, index) {
    const swiper = liveSwiper(element);
    if (!swiper) {
        return;
    }

    const state = hostState(element);
    if (!state.anchorObserver) {
        // Created once and reused for every arm, so the instance to move is read from the state at delivery
        // rather than captured here - otherwise a slider replaced since the first arm would never be
        // corrected, and the correction would go to its destroyed predecessor instead.
        state.anchorObserver = new MutationObserver(() => {
            const anchorIndex = state.anchorIndex;
            const anchorSwiper = state.anchorSwiper;
            if (anchorIndex === null || anchorIndex === undefined) {
                return;
            }
            // One-shot: only the mutation this was armed for should move the slider.
            state.anchorIndex = null;
            state.anchorSwiper = null;
            state.anchorObserver.disconnect();
            if (!isLiveSwiper(element, anchorSwiper)) {
                return;
            }
            abandonScrollMove(element);
            applyAnchor(anchorSwiper, anchorIndex);
        });
    }

    state.anchorIndex = index;
    state.anchorSwiper = swiper;
    state.anchorObserver.observe(swiper.slidesEl, { childList: true });
}

export function updateAndAnchor(element, index) {
    const swiper = liveSwiper(element);
    if (!swiper) {
        return;
    }
    abandonScrollMove(element);
    applyAnchor(swiper, index);
}

// --- updating ----------------------------------------------------------------------------------------

export function update(element) {
    liveSwiper(element)?.update();
}

export function updateSize(element) {
    liveSwiper(element)?.updateSize();
}

export function updateSlides(element) {
    liveSwiper(element)?.updateSlides();
}

export function updateProgress(element) {
    liveSwiper(element)?.updateProgress();
}

export function updateSlidesClasses(element) {
    liveSwiper(element)?.updateSlidesClasses();
}

export function updateAutoHeight(element, speed) {
    liveSwiper(element)?.updateAutoHeight(speed ?? 0);
}

// --- state -------------------------------------------------------------------------------------------

// One read of everything a host might otherwise keep its own copy of. Gathered in a single call because
// each separate read would be its own interop round trip, and on Blazor Server that is a network hop.
export function getState(element) {
    const swiper = liveSwiper(element);
    if (!swiper) {
        return null;
    }

    return {
        activeIndex: swiper.realIndex,
        rawActiveIndex: swiper.activeIndex,
        previousIndex: swiper.previousIndex,
        snapIndex: swiper.snapIndex,
        slidesCount: swiper.slides?.length ?? 0,
        isBeginning: swiper.isBeginning,
        isEnd: swiper.isEnd,
        isLocked: swiper.isLocked,
        isAnimating: swiper.animating,
        isEnabled: swiper.enabled,
        progress: swiper.progress,
        translate: swiper.translate,
        width: swiper.width,
        height: swiper.height,
        swipeDirection: swiper.swipeDirection ?? "",
        currentBreakpoint: swiper.currentBreakpoint ?? "",
        visibleSlideIndexes: swiper.visibleSlidesIndexes ?? [],
        isAutoplayRunning: swiper.autoplay?.running === true,
        isAutoplayPaused: swiper.autoplay?.paused === true,
        zoomScale: swiper.zoom?.scale ?? 1
    };
}

// --- locks and enablement ----------------------------------------------------------------------------

export function setAllowSlideNext(element, value) {
    const swiper = liveSwiper(element);
    if (swiper) {
        swiper.allowSlideNext = value;
    }
}

export function setAllowSlidePrev(element, value) {
    const swiper = liveSwiper(element);
    if (swiper) {
        swiper.allowSlidePrev = value;
    }
}

export function setAllowTouchMove(element, value) {
    const swiper = liveSwiper(element);
    if (swiper) {
        swiper.allowTouchMove = value;
    }
}

export function enable(element) {
    liveSwiper(element)?.enable();
}

export function disable(element) {
    liveSwiper(element)?.disable();
}

export function setProgress(element, progress, speed) {
    liveSwiper(element)?.setProgress(progress, optionalSpeed(speed));
}

export function changeDirection(element, direction) {
    liveSwiper(element)?.changeDirection(direction, true);
}

export function changeLanguageDirection(element, direction) {
    liveSwiper(element)?.changeLanguageDirection(direction);
}

export function translateTo(element, translate, speed) {
    liveSwiper(element)?.translateTo(translate, speed ?? 0);
}

export function getTranslate(element) {
    return liveSwiper(element)?.getTranslate() ?? 0;
}

export function detachEvents(element) {
    liveSwiper(element)?.detachEvents();
}

export function attachEvents(element) {
    liveSwiper(element)?.attachEvents();
}

// --- module controllers -------------------------------------------------------------------------------

export function startAutoplay(element) {
    liveSwiper(element)?.autoplay?.start();
}

export function stopAutoplay(element) {
    liveSwiper(element)?.autoplay?.stop();
}

export function pauseAutoplay(element, speed) {
    liveSwiper(element)?.autoplay?.pause(optionalSpeed(speed));
}

export function resumeAutoplay(element) {
    liveSwiper(element)?.autoplay?.resume();
}

export function zoomIn(element, ratio) {
    liveSwiper(element)?.zoom?.in(optionalSpeed(ratio));
}

export function zoomOut(element) {
    liveSwiper(element)?.zoom?.out();
}

export function zoomToggle(element) {
    liveSwiper(element)?.zoom?.toggle();
}

export function enableZoom(element) {
    liveSwiper(element)?.zoom?.enable();
}

export function disableZoom(element) {
    liveSwiper(element)?.zoom?.disable();
}

export function enableKeyboard(element) {
    liveSwiper(element)?.keyboard?.enable();
}

export function disableKeyboard(element) {
    liveSwiper(element)?.keyboard?.disable();
}

export function enableMousewheel(element) {
    liveSwiper(element)?.mousewheel?.enable();
}

export function disableMousewheel(element) {
    liveSwiper(element)?.mousewheel?.disable();
}

// Manipulation writes slide elements Blazor did not render and does not know about, so the next render
// that touches the slide collection will fight it. It is here as an escape hatch for hosts that own the
// slider outright; ArmAnchor and UpdateAndAnchor are the route for slides Blazor renders.
export function appendSlide(element, markup) {
    liveSwiper(element)?.appendSlide(markup);
}

export function prependSlide(element, markup) {
    liveSwiper(element)?.prependSlide(markup);
}

export function addSlide(element, index, markup) {
    liveSwiper(element)?.addSlide(index, markup);
}

export function removeSlide(element, index) {
    liveSwiper(element)?.removeSlide(index);
}

export function removeAllSlides(element) {
    liveSwiper(element)?.removeAllSlides();
}

export function updateVirtual(element, force) {
    liveSwiper(element)?.virtual?.update(force === true);
}

// --- cross-instance wiring ----------------------------------------------------------------------------

// The thumbnail strip and the controlled slider are other components, and a component reference cannot be
// serialized into an options object - so both are wired here from the two elements once each side has a
// Swiper on it. Ordering is the caller's problem to the extent that both must be initialized; the
// component only calls this once both have reported ready.
export function setThumbs(element, thumbsElement) {
    const swiper = liveSwiper(element);
    const thumbs = liveSwiper(thumbsElement);
    if (!swiper || !thumbs || !swiper.thumbs) {
        return;
    }

    swiper.params.thumbs = { ...(swiper.params.thumbs ?? {}), swiper: thumbs };
    swiper.thumbs.init();
    swiper.thumbs.update(true);
}

export function setController(element, controlledElement) {
    const swiper = liveSwiper(element);
    if (!swiper || !swiper.controller) {
        return;
    }

    swiper.controller.control = liveSwiper(controlledElement) ?? undefined;
}

// --- teardown -----------------------------------------------------------------------------------------

export function destroy(element) {
    const swiper = liveSwiper(element);
    const state = element?.__blazorSwiper;

    if (state) {
        state.anchorObserver?.disconnect();
        state.slideSetObserver?.disconnect();
        state.slideResizeObserver?.disconnect();
        state.endScrollMove?.();

        for (const { domEvent, handler } of state.listeners) {
            element.removeEventListener(domEvent, handler);
        }
        state.listeners.length = 0;
        state.dotNetRef = null;
    }

    if (swiper) {
        swiper.destroy(true, true);
    }

    if (element) {
        delete element.__blazorSwiper;
    }
}

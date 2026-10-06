// Decision logic for the interop module: what an option value means, which route a programmatic
// move takes, when a pending move is still worth defending, and where a cssMode scroll should be
// on a given frame. Kept apart from swiper-interop.js because none of it touches Swiper, the DOM
// or the interop channel - it works on plain values - so it can be reasoned about, and tested,
// without a browser.

/**
 * The option entries that should actually be written onto the element.
 *
 * SwiperOptions serializes a member the caller never set as null, and writing that would override
 * Swiper's own default with nothing. Absence is the signal, so only null and undefined are
 * dropped - `false`, `0` and `""` are all legitimate Swiper settings and must survive.
 *
 * The drop has to reach all the way down. A module's options are their own object, so an unset
 * member of `pagination` arrives as `pagination.type = null` and would blank a default just as
 * effectively as a top-level one. An object left empty by that cleaning still survives: Swiper
 * Element reads the mere presence of a module's options as "module wanted", which is exactly what
 * `Pagination = new()` means.
 */
export function applicableOptions(options) {
    const cleaned = withWrapperDefaults(cleanOptionValue(options ?? {}));

    return Object.entries(cleaned)
        .filter(([, value]) => value !== null && value !== undefined);
}

/**
 * One option value with its unset members dropped, recursively.
 *
 * A module options object whose `enabled` is explicitly false collapses to the literal `false`.
 * Passing the object instead would enable the module: Swiper Element treats any object as "module
 * wanted" and builds its elements - the pagination container, the two navigation buttons - before
 * the module itself reads `enabled` and declines to run, leaving the markup behind.
 */
function cleanOptionValue(value) {
    if (Array.isArray(value)) {
        return value.map(cleanOptionValue);
    }

    if (value === null || typeof value !== "object") {
        return value;
    }

    if (value.enabled === false) {
        return false;
    }

    const cleaned = {};
    for (const [key, member] of Object.entries(value)) {
        if (member === null || member === undefined) {
            continue;
        }
        cleaned[key] = cleanOptionValue(member);
    }

    return cleaned;
}

/**
 * The wrapper's own default, applied only where it deliberately differs from Swiper's.
 *
 * Swiper Element spreads `{observer: true}` in ahead of the caller's parameters, so leaving the
 * member unset does not mean "off" the way it does everywhere else - it means Swiper's own
 * MutationObserver runs. With a framework re-rendering slide content, and especially with
 * autoHeight, whose height writes are themselves mutations, that drives an update/height feedback
 * loop. Only an explicit `Observer = true` turns it on.
 */
function withWrapperDefaults(options) {
    if (options.observer === undefined) {
        return { ...options, observer: false };
    }

    return options;
}

/**
 * The option entries that changed between two applied sets, for pushing to a live slider.
 *
 * Compared by serialization rather than by identity, because a nested module options object is a
 * fresh instance on every render even when nothing about it moved. A member that disappears is not
 * reported: Swiper has no way to unset a parameter back to its default, so the last applied value
 * stands, and reporting it would push `undefined` and blank it instead.
 */
export function changedOptions(previous, next) {
    const before = previous ?? {};

    return applicableOptions(next).filter(([key, value]) => JSON.stringify(before[key]) !== JSON.stringify(value));
}

/**
 * Parameters Swiper only reads while it initializes.
 *
 * Assigning any parameter after init reaches Swiper - the element defines a setter for every one of
 * them - but only some of those have an effect. These are the ones that do not: the module was
 * already installed, the listeners were already attached, the class was already written, or the
 * position they describe has already been taken. Changing one is not an error, it is simply
 * ignored, which is worth saying out loud rather than letting a caller wonder.
 */
const INIT_ONLY_PARAMS = new Set([
    "injectStyles",
    "injectStylesUrls",
    "initialSlide",
    "runCallbacksOnInit",
    "effect",
    "cssMode",
    "observer",
    "observeParents",
    "observeSlideChildren",
    "resizeObserver",
    "updateOnWindowResize",
    "passiveListeners",
    "touchEventsTarget",
    "simulateTouch",
    "nested",
    "userAgent",
    "url",
    "virtual",
    "hashNavigation",
    "history",
    "a11y",
    "keyboard",
    "mousewheel",
    "parallax",
    "zoom",
    "containerModifierClass",
    "slideClass",
    "slideActiveClass",
    "slideVisibleClass",
    "slideFullyVisibleClass",
    "slideBlankClass",
    "slideNextClass",
    "slidePrevClass",
    "wrapperClass",
    "lazyPreloaderClass"
]);

/**
 * Whether changing this parameter after init is silently ignored by Swiper.
 */
export function isInitOnlyParam(name) {
    return INIT_ONLY_PARAMS.has(name);
}

/**
 * The virtual module's options rewritten for a host that renders the window itself.
 *
 * Three things have to be true for that arrangement, and none of them can be expressed from .NET:
 * `renderExternal` is a function, so it cannot cross the interop boundary as JSON and is installed
 * here; `renderExternalUpdate` has to be false, because Swiper's post-render pass would otherwise
 * measure a window Blazor has not rendered yet - the call announcing it only *starts* a render; and
 * `slides` has to be an array, because Swiper reads its length to know where the collection ends,
 * so the count the caller gave is expanded into one. Its contents are never read - Swiper hands the
 * host indices, and the host owns what they mean.
 *
 * Returns the options unchanged when the module is off or the host is not rendering, so the plain
 * `renderSlide` path stays exactly as Swiper ships it.
 */
export function virtualExternalOptions(virtual, renderExternal) {
    if (!virtual || typeof virtual !== "object" || !renderExternal) {
        return virtual;
    }

    const { slideCount, ...rest } = virtual;

    return {
        ...rest,
        slides: Array.from({ length: slideCount ?? 0 }, (_, index) => index),
        renderExternal,
        renderExternalUpdate: false
    };
}

/**
 * A speed argument as Swiper wants it.
 *
 * Swiper reads a missing argument as "use params.speed", but .NET sends an unset `int?` across
 * interop as null, which Swiper would coerce to 0 and jump. Only null becomes undefined: 0 is an
 * intentional instant move and has to survive, which `speed || undefined` would not manage.
 */
export function optionalSpeed(speed) {
    return speed ?? undefined;
}

/**
 * Which of the three programmatic-navigation routes applies.
 *
 * cssMode is checked first because it has no transform track to slide at all - the wrapper is a
 * scroll container - so it wins even when loop is also on. Loop then takes the logical-index
 * route, since slideTo would expect the shifted index while realIndex, the one reported to .NET,
 * is the logical one.
 */
export function navigationMode(params) {
    if (params.cssMode) {
        return "scroll";
    }

    return params.loop ? "loop" : "direct";
}

/**
 * Whether a settled transition should clear the host's pending move.
 *
 * Not every transitionend belongs to that move. Swiper's own re-anchors slide at speed 0 and raise
 * one too, so clearing unconditionally would drop the guard in the very frame it exists to correct
 * and let the re-anchor stand. Clear once the move has arrived, or as soon as the user drags -
 * a swipe supersedes the host's move outright.
 */
export function shouldDisarmIntent(realIndex, intendedIndex, isUserDriven) {
    if (!isIntentArmed(intendedIndex)) {
        return false;
    }

    return isUserDriven || realIndex === intendedIndex;
}

/**
 * Whether a resize should re-assert the index the host asked for.
 *
 * Swiper's resize handling ends by re-anchoring onto the index it holds, deferred by a frame. When
 * the host moves the slider during the same interaction that changed the size, that deferred
 * re-anchor carries the pre-move index, lands last, and silently undoes the move.
 */
export function shouldReanchor(realIndex, intendedIndex, isUserDriven) {
    if (!isIntentArmed(intendedIndex) || isUserDriven) {
        return false;
    }

    return realIndex !== intendedIndex;
}

/**
 * Whether the host has a move pending that the guards should defend.
 *
 * Slide 0 is a perfectly ordinary target and is falsy, so this is an explicit null check. A
 * truthiness test would refuse to defend the first slide - the one a reset lands on most.
 */
export function isIntentArmed(intendedIndex) {
    return intendedIndex !== null && intendedIndex !== undefined;
}

/**
 * How a cssMode move from `start` to `target` should be carried out.
 *
 * Sub-pixel distances are dropped: they are invisible, and running the animation for one would
 * suspend and restore scroll-snap for no reason.
 */
export function scrollPlan(start, target, speed, defaultSpeed) {
    const distance = target - start;
    if (Math.abs(distance) < 1) {
        return { kind: "none", target, distance: 0, duration: 0 };
    }

    const duration = speed ?? defaultSpeed ?? 300;
    if (duration <= 0) {
        return { kind: "instant", target, distance, duration: 0 };
    }

    return { kind: "animate", target, distance, duration };
}

/**
 * The scroll offset an animated cssMode move should hold at `elapsed` milliseconds in.
 *
 * Progress is clamped, so a frame delivered after the deadline lands exactly on the target rather
 * than carrying the ease past 1 and scrolling beyond the slide.
 */
export function scrollPositionAt(start, distance, elapsed, duration) {
    const progress = Math.min(elapsed / duration, 1);
    const eased = 1 - Math.pow(1 - progress, 3);

    return start + distance * eased;
}

/**
 * Whether a call to Swiper's `slideTo` is Swiper settling itself rather than the slider being sent
 * somewhere.
 *
 * After a resize or an update Swiper re-seats itself on the slide it already holds, and it makes
 * that call the same way every time: instantly, and flagged internal. Those must not end a cssMode
 * move in flight - the slider has not been given a new destination, and the move follows its slide
 * through the resize by itself. Everything else is navigation, whoever asked: an arrow, a bullet
 * and a key leave the flag unset, and autoplay sets it but travels at a speed.
 */
export function isSwiperCorrection(speed, isInternal) {
    return speed === 0 && isInternal === true;
}

// --- events -------------------------------------------------------------------------------------

/**
 * Events Swiper raises from inside `initialize()` itself.
 *
 * Every other listener is attached AFTER initialize on purpose - Swiper announces its starting
 * position from inside init and that is not news to the host. These three ARE the initialization,
 * so a listener attached afterwards would never hear them at all, and they get the one exception.
 */
const INIT_PHASE_EVENTS = new Set(["beforeInit", "init", "afterInit"]);

/**
 * Whether this event has to be subscribed before `initialize()` rather than after it.
 */
export function isInitPhaseEvent(name) {
    return INIT_PHASE_EVENTS.has(name);
}

/**
 * Events that fire continuously rather than at a moment.
 *
 * These run per animation frame while a drag, a transition or a pinch is in progress, so each one
 * subscribed is a interop call per frame - on Blazor Server, a network round trip per frame. They
 * are the ones the throttle applies to; everything else is delivered as it happens, because an
 * event that fires once should not be delayed.
 */
const HIGH_FREQUENCY_EVENTS = new Set([
    "progress",
    "setTranslate",
    "setTransition",
    "sliderMove",
    "touchMove",
    "touchMoveOpposite",
    "autoplayTimeLeft",
    "zoomChange",
    "scroll"
]);

/**
 * Whether this event fires per frame and so is subject to the throttle.
 */
export function isHighFrequencyEvent(name) {
    return HIGH_FREQUENCY_EVENTS.has(name);
}

/**
 * Whether a throttled event due now should be sent.
 *
 * Leading edge: the first event of a burst goes immediately, so a host watching `progress` sees the
 * drag start rather than waiting out an interval first. `lastSentAt` of null means no event has
 * been sent yet, which is not the same as one sent at time 0 - a burst starting in the page's first
 * millisecond would otherwise be held back.
 */
export function shouldSendThrottledEvent(lastSentAt, now, intervalMs) {
    if (!intervalMs || intervalMs <= 0) {
        return true;
    }

    if (lastSentAt === null || lastSentAt === undefined) {
        return true;
    }

    return now - lastSentAt >= intervalMs;
}

// --- pagination templates -----------------------------------------------------------------------

/**
 * The wrapper's own pagination members, which are templates rather than Swiper parameters.
 *
 * Swiper's renderBullet, renderFraction, renderProgressbar and renderCustom are JavaScript
 * functions it calls synchronously while rendering, and a C# delegate cannot answer synchronously -
 * interop is asynchronous by construction, and on Blazor Server it is a network round trip. These
 * templates are turned into those functions on this side of the boundary instead.
 */
const PAGINATION_TEMPLATE_KEYS = [
    "renderBulletTemplate",
    "renderFractionTemplate",
    "renderProgressbarTemplate",
    "renderCustomTemplate",
    "fractionMinimumDigits"
];

/**
 * Splits a pagination options object into the templates and the parameters Swiper itself knows.
 *
 * Leaving the templates in would set parameters Swiper has never heard of. Harmless, but it would
 * also mean the pagination options object no longer round-trips, and the reactive diff would report
 * a change every time one of them was set.
 */
export function extractPaginationTemplates(pagination) {
    if (!pagination || typeof pagination !== "object") {
        return { templates: {}, parameters: pagination };
    }

    const templates = {};
    const parameters = {};

    for (const [key, value] of Object.entries(pagination)) {
        if (PAGINATION_TEMPLATE_KEYS.includes(key)) {
            templates[key] = value;
            continue;
        }
        parameters[key] = value;
    }

    return { templates, parameters };
}

/**
 * Fills `{{placeholder}}` holes in a template.
 *
 * Unknown placeholders are left alone rather than blanked, so a typo shows up in the rendered
 * output as itself instead of silently disappearing.
 */
export function renderTemplate(template, values) {
    return String(template).replace(/\{\{(\w+)\}\}/g, (match, key) => (key in values ? String(values[key]) : match));
}

/**
 * Zero-pads a fraction number to `minimumDigits`, standing in for Swiper's formatFraction hooks.
 *
 * Null or a width of one digit means no padding at all, rather than a formatter that pads to
 * nothing - Swiper keeps its own default formatting in that case.
 */
export function formatFractionNumber(value, minimumDigits) {
    if (!minimumDigits || minimumDigits <= 1) {
        return value;
    }

    return String(value).padStart(minimumDigits, "0");
}

import assert from "node:assert/strict";
import test from "node:test";

import {
    applicableOptions,
    changedOptions,
    isInitOnlyParam,
    optionalSpeed,
    navigationMode,
    isIntentArmed,
    shouldDisarmIntent,
    shouldReanchor,
    scrollPlan,
    scrollPositionAt,
    isSwiperCorrection,
    isInitPhaseEvent,
    isHighFrequencyEvent,
    shouldSendThrottledEvent,
    extractPaginationTemplates,
    renderTemplate,
    formatFractionNumber,
    virtualExternalOptions
} from "../../src/Blazor.Swiper/wwwroot/swiper-policy.js";

/** The applied options as an object, which reads better than an entry array in an assertion. */
function applied(options) {
    return Object.fromEntries(applicableOptions(options));
}

test("ApplicableOptions_UnsetMember_IsSkippedSoSwiperKeepsItsOwnDefault", () => {
    // Arrange - SwiperOptions serializes an unset member as null; applying it would override
    // Swiper's default with nothing.
    const options = { slidesPerView: 2, spaceBetween: null, initialSlide: undefined };

    // Act & Assert
    assert.deepEqual(applied(options), { slidesPerView: 2, observer: false });
});

test("ApplicableOptions_FalseOrZero_IsAppliedRatherThanSkipped", () => {
    // Arrange - the difference between "unset" and "explicitly off" is the whole point of the
    // filter, and every falsy value here is a legitimate Swiper setting.
    const options = { loop: false, spaceBetween: 0, wrapperClass: "" };

    // Act & Assert
    assert.deepEqual(applied(options), { loop: false, spaceBetween: 0, wrapperClass: "", observer: false });
});

test("ApplicableOptions_NoOptionsAtAll_StillTurnsSwiperElementsObserverOff", () => {
    // Swiper Element spreads {observer: true} in ahead of the caller's parameters, so leaving the
    // member unset is not "off" the way it is everywhere else - it has to be said explicitly.
    assert.deepEqual(applied(null), { observer: false });
    assert.deepEqual(applied(undefined), { observer: false });
});

test("ApplicableOptions_ObserverAskedForExplicitly_IsLeftAlone", () => {
    assert.equal(applied({ observer: true }).observer, true);
});

test("ApplicableOptions_UnsetMemberOfAModulesOptions_IsDroppedToo", () => {
    // Arrange - a module's options are their own object, so an unset member arrives as
    // pagination.type = null and would blank Swiper's default just as effectively as a top-level one.
    const options = { pagination: { clickable: true, type: null, dynamicBullets: undefined } };

    // Act & Assert
    assert.deepEqual(applied(options).pagination, { clickable: true });
});

test("ApplicableOptions_ModuleOptionsLeftEmptyByCleaning_StillEnableTheModule", () => {
    // Swiper Element reads the mere presence of a module's options as "module wanted", which is
    // exactly what `Pagination = new()` means - so an empty object must survive the cleaning.
    assert.deepEqual(applied({ pagination: { type: null } }).pagination, {});
});

test("ApplicableOptions_ModuleDisabledExplicitly_CollapsesToFalseRatherThanAnObject", () => {
    // Handing over {enabled:false} would enable the module: the element treats any object as
    // "module wanted" and builds its elements before the module declines to run, leaving the
    // pagination container and both navigation buttons behind.
    assert.equal(applied({ pagination: { enabled: false } }).pagination, false);
    assert.equal(applied({ navigation: { enabled: false, hideOnClick: true } }).navigation, false);
});

test("ApplicableOptions_ModuleEnabledExplicitly_StaysAnObject", () => {
    assert.deepEqual(applied({ pagination: { enabled: true } }).pagination, { enabled: true });
});

test("ApplicableOptions_ArrayValues_SurviveIntact", () => {
    // creativeEffect's transforms are arrays of numbers, and injectStyles is an array of strings.
    const options = { injectStyles: ["a", "b"], creativeEffect: { prev: { translate: [0, 0, -400] } } };

    // Act
    const result = applied(options);

    // Assert
    assert.deepEqual(result.injectStyles, ["a", "b"]);
    assert.deepEqual(result.creativeEffect.prev.translate, [0, 0, -400]);
});

test("OptionalSpeed_NullFromDotNet_BecomesUndefinedSoSwiperUsesItsConfiguredSpeed", () => {
    // Swiper reads a missing argument as "use params.speed"; passing null instead would be
    // coerced to 0 and make every programmatic move jump.
    assert.equal(optionalSpeed(null), undefined);
    assert.equal(optionalSpeed(undefined), undefined);
});

test("OptionalSpeed_Zero_StaysZeroBecauseAnInstantMoveIsARealSpeed", () => {
    // Guards against "speed || undefined", which reads an intentional 0 as "unset".
    assert.equal(optionalSpeed(0), 0);
    assert.equal(optionalSpeed(400), 400);
});

test("NavigationMode_CssMode_ScrollsTheWrapperEvenWhenLooping", () => {
    // cssMode has no transform track to slide, so it wins over loop.
    assert.equal(navigationMode({ cssMode: true, loop: true }), "scroll");
    assert.equal(navigationMode({ cssMode: true, loop: false }), "scroll");
});

test("NavigationMode_Loop_TakesTheLogicalIndexRoute", () => {
    // slideTo would take the shifted index, which does not match the realIndex reported to .NET.
    assert.equal(navigationMode({ cssMode: false, loop: true }), "loop");
});

test("NavigationMode_PlainSlider_TakesTheDirectRoute", () => {
    assert.equal(navigationMode({ cssMode: false, loop: false }), "direct");
    assert.equal(navigationMode({}), "direct");
});

test("IsIntentArmed_SlideZero_CountsAsArmed", () => {
    // The resize guard schedules its deferred check on this alone, so treating falsy 0 as "nothing
    // pending" would leave a move to the first slide undefended.
    assert.equal(isIntentArmed(0), true);
});

test("IsIntentArmed_NothingPending_IsNotArmed", () => {
    assert.equal(isIntentArmed(null), false);
    assert.equal(isIntentArmed(undefined), false);
});

test("ShouldDisarmIntent_MoveArrived_ClearsTheIntent", () => {
    assert.equal(shouldDisarmIntent(3, 3, false), true);
});

test("ShouldDisarmIntent_UserDragged_ClearsTheIntentBecauseTheSwipeSupersedesIt", () => {
    assert.equal(shouldDisarmIntent(1, 3, true), true);
});

test("ShouldDisarmIntent_ReanchorMidFlight_KeepsTheIntentArmed", () => {
    // Swiper's own re-anchors slide at speed 0 and raise their own transitionend. Disarming on
    // any transitionend would drop the guard in the very frame it exists to correct, and the
    // re-anchor would stand.
    assert.equal(shouldDisarmIntent(1, 3, false), false);
});

test("ShouldDisarmIntent_NothingArmed_HasNothingToClear", () => {
    assert.equal(shouldDisarmIntent(0, null, false), false);
});

test("ShouldReanchor_ResizeDriftedOffTheIntendedSlide_Reanchors", () => {
    assert.equal(shouldReanchor(0, 3, false), true);
});

test("ShouldReanchor_IntendedSlideIsZero_StillReanchors", () => {
    // Slide 0 is falsy. A "!intendedIndex" guard would silently refuse to correct the first
    // slide - the one a reset lands on most often.
    assert.equal(shouldReanchor(2, 0, false), true);
});

test("ShouldReanchor_AlreadyOnTheIntendedSlide_LeavesTheSliderAlone", () => {
    assert.equal(shouldReanchor(3, 3, false), false);
});

test("ShouldReanchor_UserIsDragging_LeavesTheSliderAlone", () => {
    // The host's move is stale the moment the user takes over.
    assert.equal(shouldReanchor(0, 3, true), false);
});

test("ShouldReanchor_NoIntentArmed_LeavesASliderAtRestAlone", () => {
    assert.equal(shouldReanchor(2, null, false), false);
    assert.equal(shouldReanchor(2, undefined, false), false);
});

test("ScrollPlan_SubPixelDistance_DoesNothing", () => {
    // Sub-pixel corrections are invisible and would restart the snap-type dance for nothing.
    assert.equal(scrollPlan(100, 100.4, 300, 300).kind, "none");
});

test("ScrollPlan_ZeroSpeed_JumpsStraightToTheTarget", () => {
    // Act
    const plan = scrollPlan(0, 480, 0, 300);

    // Assert
    assert.equal(plan.kind, "instant");
    assert.equal(plan.target, 480);
});

test("ScrollPlan_NoSpeedGiven_FallsBackToTheSlidersConfiguredSpeed", () => {
    // Act
    const plan = scrollPlan(0, 480, null, 700);

    // Assert
    assert.equal(plan.kind, "animate");
    assert.equal(plan.duration, 700);
});

test("ScrollPlan_NoSpeedAnywhere_FallsBackToSwipersOwnDefault", () => {
    assert.equal(scrollPlan(0, 480, null, undefined).duration, 300);
});

test("ScrollPlan_BackwardsMove_AnimatesOverANegativeDistance", () => {
    // Act
    const plan = scrollPlan(480, 0, 300, 300);

    // Assert
    assert.equal(plan.kind, "animate");
    assert.equal(plan.distance, -480);
});

test("ScrollPositionAt_TheFirstFrame_IsStillTheStartingOffset", () => {
    assert.equal(scrollPositionAt(20, 100, 0, 300), 20);
});

test("ScrollPositionAt_PastTheDuration_LandsExactlyOnTargetWithoutOvershooting", () => {
    // A late frame must not carry the easing past 1, which would scroll beyond the slide.
    assert.equal(scrollPositionAt(20, 100, 300, 300), 120);
    assert.equal(scrollPositionAt(20, 100, 5000, 300), 120);
});

test("ScrollPositionAt_Midway_IsAlreadyMostOfTheWayBecauseTheEaseIsFrontLoaded", () => {
    // easeOutCubic(0.5) = 1 - 0.5^3 = 0.875
    assert.equal(scrollPositionAt(0, 100, 150, 300), 87.5);
});

test("IsSwiperCorrection_InstantAndInternal_IsSwiperReseatingItself", () => {
    // The call Swiper makes after a resize or an update: slideTo(activeIndex, 0, false, true).
    assert.equal(isSwiperCorrection(0, true), true);
});

test("IsSwiperCorrection_InternalFlagLeftUnset_IsNavigation", () => {
    // Arrows, bullets and keys never set the flag - not even for an instant move.
    assert.equal(isSwiperCorrection(0, undefined), false);
    assert.equal(isSwiperCorrection(0, false), false);
    assert.equal(isSwiperCorrection(300, undefined), false);
    assert.equal(isSwiperCorrection(undefined, undefined), false);
});

test("IsSwiperCorrection_InternalButAnimated_IsNavigation", () => {
    // Autoplay flags its moves internal too, and those do send the slider somewhere.
    assert.equal(isSwiperCorrection(300, true), false);
    assert.equal(isSwiperCorrection(undefined, true), false);
});

test("ChangedOptions_MemberThatMoved_IsTheOnlyOneReported", () => {
    // Arrange
    const previous = { slidesPerView: 1, spaceBetween: 16, observer: false };
    const next = { slidesPerView: 3, spaceBetween: 16 };

    // Act
    const changes = changedOptions(previous, next);

    // Assert
    assert.deepEqual(changes, [["slidesPerView", 3]]);
});

test("ChangedOptions_NestedModuleOptionsRebuiltButUnchanged_AreNotReported", () => {
    // A record is a fresh instance on every render, so identity says "changed" when nothing moved.
    const previous = { pagination: { clickable: true }, observer: false };
    const next = { pagination: { clickable: true } };

    assert.deepEqual(changedOptions(previous, next), []);
});

test("ChangedOptions_NestedMemberThatMoved_ReportsTheWholeModuleObject", () => {
    // Swiper takes a module's parameters as one object, so the whole thing is what gets assigned.
    const previous = { pagination: { clickable: true }, observer: false };
    const next = { pagination: { clickable: true, dynamicBullets: true } };

    assert.deepEqual(changedOptions(previous, next), [["pagination", { clickable: true, dynamicBullets: true }]]);
});

test("ChangedOptions_MemberThatDisappeared_IsNotReported", () => {
    // Swiper cannot unset a parameter back to its default, so reporting it would push undefined and
    // blank the value instead of restoring anything.
    const previous = { slidesPerView: 3, observer: false };
    const next = {};

    assert.deepEqual(changedOptions(previous, next), []);
});

test("ChangedOptions_NoPreviousSetAtAll_ReportsEverything", () => {
    assert.deepEqual(changedOptions(null, { loop: true }), [["loop", true], ["observer", false]]);
});

test("IsInitOnlyParam_ParametersSwiperOnlyReadsWhileInitializing_AreRecognised", () => {
    // Assigning one of these reaches Swiper and does nothing, so the wrapper says so out loud.
    assert.equal(isInitOnlyParam("effect"), true);
    assert.equal(isInitOnlyParam("cssMode"), true);
    assert.equal(isInitOnlyParam("initialSlide"), true);
    assert.equal(isInitOnlyParam("keyboard"), true);
});

test("IsInitOnlyParam_ParametersSwiperReAppliesOnUpdate_AreNot", () => {
    assert.equal(isInitOnlyParam("slidesPerView"), false);
    assert.equal(isInitOnlyParam("spaceBetween"), false);
    assert.equal(isInitOnlyParam("loop"), false);
    assert.equal(isInitOnlyParam("pagination"), false);
});

test("IsInitPhaseEvent_TheThreeEventsRaisedFromInsideInitialize_NeedSubscribingFirst", () => {
    // Every other listener is attached after initialize() so that Swiper's opening announcement is
    // not forwarded as news. These three ARE the initialization, so they get the exception.
    assert.equal(isInitPhaseEvent("beforeInit"), true);
    assert.equal(isInitPhaseEvent("init"), true);
    assert.equal(isInitPhaseEvent("afterInit"), true);
});

test("IsInitPhaseEvent_EverythingElse_KeepsTheAfterInitializeOrdering", () => {
    assert.equal(isInitPhaseEvent("slideChange"), false);
    assert.equal(isInitPhaseEvent("reachEnd"), false);
    assert.equal(isInitPhaseEvent("transitionEnd"), false);
});

test("IsHighFrequencyEvent_TheOnesRaisedPerAnimationFrame_AreThrottled", () => {
    // On Blazor Server each delivery is a network round trip, so a per-frame event subscribed
    // without a throttle is a round trip per frame.
    for (const name of ["progress", "setTranslate", "setTransition", "sliderMove", "touchMove", "autoplayTimeLeft", "zoomChange"]) {
        assert.equal(isHighFrequencyEvent(name), true, `${name} should be throttled`);
    }
});

test("IsHighFrequencyEvent_TheOnesRaisedAtAMoment_AreDeliveredAsTheyHappen", () => {
    for (const name of ["slideChange", "reachEnd", "transitionEnd", "autoplayStart", "tap"]) {
        assert.equal(isHighFrequencyEvent(name), false, `${name} should not be throttled`);
    }
});

test("ShouldSendThrottledEvent_TheFirstEventOfABurst_GoesImmediately", () => {
    // Leading edge, so a host watching progress sees the drag start rather than waiting an interval.
    assert.equal(shouldSendThrottledEvent(null, 1000, 16), true);
    assert.equal(shouldSendThrottledEvent(undefined, 1000, 16), true);
});

test("ShouldSendThrottledEvent_WithinTheInterval_IsHeldBack", () => {
    assert.equal(shouldSendThrottledEvent(1000, 1008, 16), false);
});

test("ShouldSendThrottledEvent_OnceTheIntervalHasPassed_GoesAgain", () => {
    assert.equal(shouldSendThrottledEvent(1000, 1016, 16), true);
    assert.equal(shouldSendThrottledEvent(1000, 1200, 16), true);
});

test("ShouldSendThrottledEvent_NoThrottleConfigured_SendsEverything", () => {
    assert.equal(shouldSendThrottledEvent(1000, 1001, 0), true);
    assert.equal(shouldSendThrottledEvent(1000, 1001, null), true);
});

test("ShouldSendThrottledEvent_ABurstStartingAtTimeZero_IsNotMistakenForOneAlreadySent", () => {
    // The distinction between "never sent" and "sent at time 0" is why this is a null check rather
    // than a truthiness one - the same reason isIntentArmed is.
    assert.equal(shouldSendThrottledEvent(0, 1, 16), false);
    assert.equal(shouldSendThrottledEvent(null, 1, 16), true);
});

test("ExtractPaginationTemplates_TheWrappersOwnMembers_AreSeparatedFromSwipersParameters", () => {
    // Arrange - leaving the templates in would set parameters Swiper has never heard of, and make
    // the reactive diff report a change every time one of them was set.
    const pagination = { clickable: true, type: "bullets", renderBulletTemplate: "<b>{{index}}</b>", fractionMinimumDigits: 2 };

    // Act
    const { templates, parameters } = extractPaginationTemplates(pagination);

    // Assert
    assert.deepEqual(parameters, { clickable: true, type: "bullets" });
    assert.deepEqual(templates, { renderBulletTemplate: "<b>{{index}}</b>", fractionMinimumDigits: 2 });
});

test("ExtractPaginationTemplates_NoPaginationAtAll_IsLeftAlone", () => {
    assert.deepEqual(extractPaginationTemplates(null), { templates: {}, parameters: null });
    assert.deepEqual(extractPaginationTemplates(true), { templates: {}, parameters: true });
});

test("RenderTemplate_Placeholders_AreFilledIn", () => {
    const rendered = renderTemplate("<span class='{{className}}'>{{index}}</span>", { className: "bullet", index: 3 });

    assert.equal(rendered, "<span class='bullet'>3</span>");
});

test("RenderTemplate_AnUnknownPlaceholder_IsLeftVisibleRatherThanBlanked", () => {
    // A typo that silently disappears is a typo nobody finds.
    assert.equal(renderTemplate("{{index}} of {{ttoal}}", { index: 1 }), "1 of {{ttoal}}");
});

test("FormatFractionNumber_APaddingWidth_ZeroPadsTheNumber", () => {
    assert.equal(formatFractionNumber(3, 2), "03");
    assert.equal(formatFractionNumber(12, 2), "12");
});

test("FormatFractionNumber_NoPaddingAsked_LeavesTheNumberAsItWas", () => {
    // Swiper keeps its own formatting rather than being handed a formatter that pads to nothing.
    assert.equal(formatFractionNumber(3, null), 3);
    assert.equal(formatFractionNumber(3, 1), 3);
});

test("VirtualExternalOptions_NoRenderer_LeavesSwiperToItsOwnRendering", () => {
    // Arrange - the module is on, but nobody handles OnVirtualRender
    const virtual = { enabled: true, slideCount: 40 };

    // Act
    const result = virtualExternalOptions(virtual, null);

    // Assert - unchanged, so the plain renderSlide path stays exactly as Swiper ships it
    assert.equal(result, virtual);
});

test("VirtualExternalOptions_AHostRenderer_IsInstalledAsRenderExternal", () => {
    // Arrange
    const renderExternal = () => { };

    // Act
    const result = virtualExternalOptions({ enabled: true, slideCount: 3 }, renderExternal);

    // Assert
    assert.equal(result.renderExternal, renderExternal);
});

test("VirtualExternalOptions_AHostRenderer_TurnsOffSwipersOwnPostRenderPass", () => {
    // Act
    const result = virtualExternalOptions({ enabled: true, slideCount: 3 }, () => { });

    // Assert - Swiper's pass would measure a window Blazor has not rendered yet, because the call
    // announcing it only starts a render
    assert.equal(result.renderExternalUpdate, false);
});

test("VirtualExternalOptions_ASlideCount_BecomesTheArraySwiperMeasuresAgainst", () => {
    // Act
    const result = virtualExternalOptions({ enabled: true, slideCount: 4 }, () => { });

    // Assert - Swiper reads the length to know where the collection ends
    assert.deepEqual(result.slides, [0, 1, 2, 3]);
});

test("VirtualExternalOptions_ASlideCount_IsNotForwardedToSwiper", () => {
    // Act
    const result = virtualExternalOptions({ enabled: true, slideCount: 4 }, () => { });

    // Assert - slideCount is the wrapper's own spelling; Swiper has no such parameter
    assert.equal("slideCount" in result, false);
});

test("VirtualExternalOptions_NoSlideCount_IsAnEmptyCollectionRatherThanACrash", () => {
    // Act
    const result = virtualExternalOptions({ enabled: true }, () => { });

    // Assert
    assert.deepEqual(result.slides, []);
});

test("VirtualExternalOptions_TheCallersOwnMembers_Survive", () => {
    // Act
    const result = virtualExternalOptions({ enabled: true, cache: false, addSlidesBefore: 2, slideCount: 1 }, () => { });

    // Assert
    assert.equal(result.enabled, true);
    assert.equal(result.cache, false);
    assert.equal(result.addSlidesBefore, 2);
});

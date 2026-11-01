using Xunit;

namespace Blazor.Swiper.E2E;

/// <summary>
/// That the events actually fire, in a browser, for the interactions they are supposed to.
/// </summary>
/// <remarks>
/// bUnit proves the dispatcher fans a payload out to the right callback, but it never runs the
/// interop module - so nothing there shows that a listener was attached to the right DOM event, at
/// the right moment, and that Swiper raised it at all.
/// </remarks>
[Collection(DemoCollectionDefinition.Name)]
public sealed class SwiperEventTests(DemoFixture fixture)
{
    private const string LabStory = "components-swiper-events--event-lab";

    /// <summary>
    /// The one ordering rule that has no second chance: these three are raised from inside
    /// <c>element.initialize()</c>, so a listener attached after it - which every other listener
    /// deliberately is - would never hear them.
    /// </summary>
    [Fact]
    public async Task InitPhaseEvents_RaisedFromInsideInitialize_AreStillDelivered()
    {
        // Arrange & Act
        var canvas = await fixture.NavigateToStoryAsync(LabStory, "lab-state");

        // Assert
        var state = await StoryState.WaitForAsync(
            canvas,
            "lab-state",
            candidate => StoryState.EventCount(candidate, "afterInit") > 0,
            "the initialization events to be delivered");

        Assert.True(StoryState.EventCount(state, "beforeInit") > 0, $"beforeInit never arrived. State: {state}");
        Assert.True(StoryState.EventCount(state, "init") > 0, $"init never arrived. State: {state}");
        Assert.True(StoryState.EventCount(state, "afterInit") > 0, $"afterInit never arrived. State: {state}");
        fixture.AssertNoJsErrors();
    }

    /// <summary>
    /// The complement: Swiper announces its opening position from inside init too, and that is not
    /// news to the host - it is the position the host asked for. Forwarding it would make every
    /// consumer write an "ignore the first one" guard.
    /// </summary>
    [Fact]
    public async Task SlideChange_SwipersOpeningAnnouncement_IsNotForwarded()
    {
        // Arrange & Act
        var canvas = await fixture.NavigateToStoryAsync(LabStory, "lab-state");

        // Assert
        var state = await StoryState.WaitForAsync(
            canvas,
            "lab-state",
            candidate => StoryState.EventCount(candidate, "afterInit") > 0,
            "the slider to finish initializing");

        Assert.Equal(0, StoryState.Int(state, "slideChanges"));
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task ProgrammaticMove_NextButton_RaisesTheTransitionAndIndexEvents()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync(LabStory, "lab-state");

        // Act
        await canvas.GetByTestId("lab-next").ClickAsync();

        // Assert
        var state = await StoryState.WaitForAsync(
            canvas,
            "lab-state",
            candidate => StoryState.EventCount(candidate, "slideChangeTransitionEnd") > 0,
            "the move to complete");

        Assert.True(StoryState.EventCount(state, "transitionStart") > 0, $"transitionStart missing: {state}");
        Assert.True(StoryState.EventCount(state, "transitionEnd") > 0, $"transitionEnd missing: {state}");
        Assert.True(StoryState.EventCount(state, "slideNextTransitionStart") > 0, $"slideNextTransitionStart missing: {state}");
        Assert.True(StoryState.EventCount(state, "activeIndexChange") > 0, $"activeIndexChange missing: {state}");
        Assert.True(StoryState.EventCount(state, "realIndexChange") > 0, $"realIndexChange missing: {state}");
        Assert.True(StoryState.EventCount(state, "fromEdge") > 0, $"fromEdge missing: {state}");

        // ...and none of the touch events, because nobody touched anything.
        Assert.Equal(0, StoryState.EventCount(state, "touchStart"));
        Assert.Equal(0, StoryState.EventCount(state, "sliderFirstMove"));
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task RealDrag_PointerGesture_RaisesTheTouchEvents()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync(LabStory, "lab-state");

        // Act
        await SwipeHelper.SwipeLeftAsync(fixture.Page, canvas.GetByTestId("lab-swiper"));

        // Assert
        var state = await StoryState.WaitForAsync(
            canvas,
            "lab-state",
            candidate => StoryState.EventCount(candidate, "touchEnd") > 0,
            "the drag to be reported");

        Assert.True(StoryState.EventCount(state, "touchStart") > 0, $"touchStart missing: {state}");
        Assert.True(StoryState.EventCount(state, "sliderFirstMove") > 0, $"sliderFirstMove missing: {state}");
        Assert.True(StoryState.EventCount(state, "sliderMove") > 0, $"sliderMove missing: {state}");
        Assert.True(StoryState.EventCount(state, "progress") > 0, $"progress missing: {state}");
        Assert.True(StoryState.EventCount(state, "setTranslate") > 0, $"setTranslate missing: {state}");
        Assert.True(StoryState.Int(state, "userSlideChanges") > 0, $"the drag was not reported as user-driven: {state}");
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task ReachingTheLastSlide_ProgrammaticMove_RaisesReachEndAndToEdge()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync(LabStory, "lab-state");

        // Act
        await canvas.GetByTestId("lab-last").ClickAsync();

        // Assert
        var state = await StoryState.WaitForAsync(
            canvas,
            "lab-state",
            candidate => StoryState.EventCount(candidate, "reachEnd") > 0,
            "the slider to report reaching the end");

        Assert.True(StoryState.EventCount(state, "toEdge") > 0, $"toEdge missing: {state}");
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task Update_CalledExplicitly_RaisesTheUpdateEvents()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync(LabStory, "lab-state");

        // Act
        await canvas.GetByTestId("lab-update").ClickAsync();

        // Assert
        await StoryState.WaitForAsync(
            canvas,
            "lab-state",
            candidate => StoryState.EventCount(candidate, "update") > 0,
            "the update to be reported");

        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task PaginationClick_BulletInTheShadowDom_RaisesTheNavigationAndPaginationEvents()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync(LabStory, "lab-state");

        // Act - the arrows and bullets are inside the element's shadow DOM, which Playwright pierces.
        await SwipeHelper.TapAsync(fixture.Page, canvas.GetByTestId("lab-swiper").Locator("css=.swiper-button-next"));

        // Assert
        var state = await StoryState.WaitForAsync(
            canvas,
            "lab-state",
            candidate => StoryState.EventCount(candidate, "navigationNext") > 0,
            "the navigation click to be reported");

        Assert.True(StoryState.EventCount(state, "paginationUpdate") > 0, $"paginationUpdate missing: {state}");
        fixture.AssertNoJsErrors();
    }

    /// <summary>
    /// The throttle exists because these events fire per animation frame, and on Blazor Server each
    /// delivery is a network round trip. Two sliders, the same drag, different delivery counts.
    /// </summary>
    [Fact]
    public async Task HighFrequencyEvents_Throttled_AreDeliveredFarLessOften()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper-events--high-frequency-events", "unthrottled-state");

        // Act
        await SwipeHelper.SwipeLeftAsync(fixture.Page, canvas.GetByTestId("unthrottled-swiper"));
        await SwipeHelper.SwipeLeftAsync(fixture.Page, canvas.GetByTestId("throttled-swiper"));

        var unthrottled = await StoryState.WaitForAsync(
            canvas,
            "unthrottled-state",
            state => StoryState.EventCount(state, "progress") > 0,
            "the unthrottled slider to report progress");

        var throttled = await StoryState.ReadAsync(canvas, "throttled-state");

        // Assert - the leading edge means the throttled one still reports the start of the drag.
        var unthrottledCount = StoryState.EventCount(unthrottled, "progress");
        var throttledCount = StoryState.EventCount(throttled, "progress");

        Assert.True(throttledCount > 0, "The throttle swallowed the first event of the burst.");
        Assert.True(
            throttledCount < unthrottledCount,
            $"The throttle changed nothing: {throttledCount} throttled against {unthrottledCount} unthrottled.");
        fixture.AssertNoJsErrors();
    }

    /// <summary>
    /// Subscriptions are not fixed at init: a callback assigned later starts being listened for, and
    /// one removed stops costing anything.
    /// </summary>
    [Fact]
    public async Task Subscriptions_CallbackAssignedAfterInit_StartsBeingDelivered()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper-events--subscribing-on-demand", "on-demand-state");

        // Act - nothing is watching yet, so a drag should report nothing at all.
        await SwipeHelper.SwipeLeftAsync(fixture.Page, canvas.GetByTestId("on-demand-swiper"));
        var before = await StoryState.ReadAsync(canvas, "on-demand-state");

        await canvas.GetByTestId("on-demand-toggle").ClickAsync();
        await SwipeHelper.SwipeLeftAsync(fixture.Page, canvas.GetByTestId("on-demand-swiper"));

        // Assert
        Assert.Equal(0, before.GetProperty("progressEvents").GetInt32());

        await StoryState.WaitForAsync(
            canvas,
            "on-demand-state",
            state => state.GetProperty("progressEvents").GetInt32() > 0,
            "progress to start being delivered once the callback was wired");

        fixture.AssertNoJsErrors();
    }
}

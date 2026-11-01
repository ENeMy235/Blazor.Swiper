using Microsoft.Playwright;
using Xunit;

namespace Blazor.Swiper.E2E;

/// <summary>
/// Two-way binding, the module controllers and the two cross-instance wirings, driven for real.
/// </summary>
[Collection(DemoCollectionDefinition.Name)]
public sealed class SwiperBindingTests(DemoFixture fixture)
{
    [Fact]
    public async Task ActiveIndex_HostChangedTheBoundValue_MovesTheSlider()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper--two-way-binding", "binding-state");

        // Act
        await canvas.GetByTestId("binding-next").ClickAsync();

        // Assert - both that the host's own value moved and that the slider actually followed it.
        await StoryState.WaitForAsync(
            canvas,
            "binding-state",
            state => StoryState.Int(state, "activeIndex") == 1,
            "the bound index to advance");

        Assert.Equal(1, await fixture.ReadSwiperAsync<int>("binding-swiper", "s.realIndex"));
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task ActiveIndex_RealDrag_ReportsBackToTheBoundValue()
    {
        // Arrange - the other direction, which is the one an @ref and a callback would otherwise be
        // needed for.
        var canvas = await fixture.NavigateToStoryAsync("components-swiper--two-way-binding", "binding-state");

        // Act
        await SwipeHelper.SwipeLeftAsync(fixture.Page, canvas.GetByTestId("binding-swiper"));

        // Assert
        await StoryState.WaitForAsync(
            canvas,
            "binding-state",
            state => StoryState.Int(state, "activeIndex") == 1,
            "the swipe to reach the bound value");

        fixture.AssertNoJsErrors();
    }

    /// <summary>
    /// The loop the echo guard exists for: the slider reports its move, the host re-renders with the
    /// index it was just told, and the component must read that as agreement rather than as an
    /// instruction to move again. Chasing it would leave the slider stuck or oscillating.
    /// </summary>
    [Fact]
    public async Task ActiveIndex_SeveralMovesInARow_SettleWhereTheyWereAsked()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper--two-way-binding", "binding-state");

        // Act
        await canvas.GetByTestId("binding-next").ClickAsync();
        await StoryState.WaitForAsync(canvas, "binding-state", s => StoryState.Int(s, "activeIndex") == 1, "slide 1");
        await canvas.GetByTestId("binding-next").ClickAsync();
        await StoryState.WaitForAsync(canvas, "binding-state", s => StoryState.Int(s, "activeIndex") == 2, "slide 2");
        await canvas.GetByTestId("binding-last").ClickAsync();

        // Assert
        await StoryState.WaitForAsync(canvas, "binding-state", s => StoryState.Int(s, "activeIndex") == 4, "the last slide");

        // Give any feedback loop a chance to show itself before reading the slider.
        await Task.Delay(500);
        Assert.Equal(4, await fixture.ReadSwiperAsync<int>("binding-swiper", "s.realIndex"));
        Assert.Equal(4, StoryState.Int(await StoryState.ReadAsync(canvas, "binding-state"), "activeIndex"));
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task SlideState_SliderMoved_ReachesTheSlidesOwnContent()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper--slide-state");

        // Act & Assert
        Assert.Equal("active", (await canvas.GetByTestId("slide-state-0").InnerTextAsync()).Trim());

        await canvas.GetByTestId("slide-state-next").ClickAsync();

        await Assertions.Expect(canvas.GetByTestId("slide-state-1")).ToHaveTextAsync("active");
        await Assertions.Expect(canvas.GetByTestId("slide-state-0")).ToHaveTextAsync("previous");
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task IsAutoplayRunning_AutoplayLeftToRun_AdvancesTheSliderByItself()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper-modules--autoplay", "autoplay-state");

        // Act & Assert - the story's delay is 2s, so this is the module actually running rather than
        // anything the test did.
        await StoryState.WaitForAsync(
            canvas,
            "autoplay-state",
            state => StoryState.Int(state, "slideChanges") > 0,
            "autoplay to advance the slider on its own");

        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task IsAutoplayRunning_PauseButton_StopsAutoplayAndKeepsTheBoundValueTrue()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper-modules--autoplay", "autoplay-state");

        // Act
        await canvas.GetByTestId("autoplay-toggle").ClickAsync();

        // Assert - the host's flag and Swiper's own state have to agree, in both directions.
        await StoryState.WaitForAsync(
            canvas,
            "autoplay-state",
            state => !StoryState.Bool(state, "isPlaying"),
            "the binding to report autoplay stopped");

        Assert.False(await fixture.ReadSwiperAsync<bool>("autoplay-swiper", "s.autoplay.running"));

        await canvas.GetByTestId("autoplay-toggle").ClickAsync();
        await StoryState.WaitForAsync(
            canvas,
            "autoplay-state",
            state => StoryState.Bool(state, "isPlaying"),
            "the binding to report autoplay started again");

        Assert.True(await fixture.ReadSwiperAsync<bool>("autoplay-swiper", "s.autoplay.running"));
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task Thumbs_ClickingAThumbnail_MovesTheMainSlider()
    {
        // Arrange - the two sliders are separate components, wired by handing both elements to the
        // interop once each side has a Swiper on it.
        var canvas = await fixture.NavigateToStoryAsync("components-swiper-recipes--thumbnail-gallery", "gallery-state");

        // Act
        await SwipeHelper.TapAsync(fixture.Page, canvas.GetByTestId("gallery-thumbs").Locator("css=swiper-slide").Nth(2));

        // Assert
        await StoryState.WaitForAsync(
            canvas,
            "gallery-state",
            state => StoryState.Int(state, "activeIndex") == 2,
            "the main slider to follow the thumbnail");

        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task Controller_MainSliderMoved_MovesTheControlledOne()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper-recipes--synced-sliders", "controller-state");

        // Act
        await SwipeHelper.TapAsync(fixture.Page, canvas.GetByTestId("controller-swiper").Locator("css=.swiper-button-next"));

        // Assert
        await StoryState.WaitForAsync(
            canvas,
            "controller-state",
            state => StoryState.Int(state, "activeIndex") == 1,
            "the driving slider to advance");

        // Inverse, so the captions move the other way rather than tracking the same index.
        var captionIndex = await fixture.ReadSwiperAsync<int>("controller-captions", "s.activeIndex");
        Assert.NotEqual(0, captionIndex);
        fixture.AssertNoJsErrors();
    }

    /// <summary>
    /// A changed option has to reach the running slider without tearing it down and building a new
    /// one - which would lose the position, the listeners and every bit of state on it.
    /// </summary>
    [Fact]
    public async Task Options_ChangedAfterInit_ReachTheSameSwiperInstance()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper--reactive-options", "reactive-state");

        // A marker on the live instance, so a silent re-create would be visible as its absence.
        await fixture.ReadSwiperAsync<bool>("reactive-swiper", "(s.__e2eMarker = true)");
        Assert.Equal(1d, await fixture.ReadParameterAsync<double>("reactive-swiper", "slidesPerView"));

        // Act
        await canvas.GetByTestId("reactive-toggle").ClickAsync();

        // Assert
        await StoryState.WaitForAsync(
            canvas,
            "reactive-state",
            state => state.GetProperty("slidesPerView").GetInt32() == 3,
            "the host to report the new slidesPerView");

        Assert.Equal(3d, await fixture.ReadParameterAsync<double>("reactive-swiper", "slidesPerView"));
        Assert.True(
            await fixture.ReadSwiperAsync<bool>("reactive-swiper", "s.__e2eMarker === true"),
            "The slider was rebuilt rather than updated in place.");
        fixture.AssertNoJsErrors();
    }

    /// <summary>
    /// Removing a slide that sits before the active one shifts every later slide sideways while
    /// Swiper's transform still points at the old offset. ArmAnchor corrects that from a
    /// MutationObserver callback, which the platform delivers before the next paint - so the wrong
    /// position is never painted at all.
    /// </summary>
    [Fact]
    public async Task ArmAnchor_SlideRemovedBeforeTheActiveOne_KeepsTheSliderWhereItWas()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper-modules--keeping-position-across-slide-changes", "anchor-state");

        await canvas.GetByTestId("anchor-next").ClickAsync();
        await StoryState.WaitForAsync(canvas, "anchor-state", s => StoryState.Int(s, "activeIndex") == 1, "slide 1");
        await canvas.GetByTestId("anchor-next").ClickAsync();
        await StoryState.WaitForAsync(canvas, "anchor-state", s => StoryState.Int(s, "activeIndex") == 2, "slide 2");

        var labelBefore = await ActiveSlideLabel();

        // Act
        await canvas.GetByTestId("anchor-remove-first").ClickAsync();

        // Assert - one slide fewer, so the index moves down by one, but the slide on screen is the
        // same one the reader was looking at.
        await StoryState.WaitForAsync(
            canvas,
            "anchor-state",
            state => state.GetProperty("slides").GetInt32() == 4,
            "the slide to be removed");

        Assert.Equal(labelBefore, await ActiveSlideLabel());
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task Wizard_NextButton_AdvancesTheStepWithoutTheUserBeingAbleToSwipe()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper-recipes--wizard");

        // Act
        await canvas.GetByTestId("wizard-next").ClickAsync();
        await Assertions.Expect(canvas.GetByTestId("wizard-progress")).ToHaveTextAsync("2 / 4");

        // A drag must do nothing at all, because the host owns the position.
        await SwipeHelper.SwipeLeftAsync(fixture.Page, canvas.GetByTestId("wizard-swiper"));
        await Task.Delay(400);

        // Assert
        await Assertions.Expect(canvas.GetByTestId("wizard-progress")).ToHaveTextAsync("2 / 4");
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task Zoom_ZoomInMethod_ScalesTheActiveSlide()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper-modules--zoom", "zoom-state");

        // Act
        await canvas.GetByTestId("zoom-in").ClickAsync();

        // Assert
        await StoryState.WaitForAsync(
            canvas,
            "zoom-state",
            state => StoryState.EventCount(state, "zoomChange") > 0,
            "the zoom change to be reported");

        Assert.True(await fixture.ReadSwiperAsync<double>("zoom-swiper", "s.zoom.scale") > 1);

        await canvas.GetByTestId("zoom-out").ClickAsync();
        await Task.Delay(400);
        Assert.Equal(1d, await fixture.ReadSwiperAsync<double>("zoom-swiper", "s.zoom.scale"));
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task Keyboard_ArrowKey_MovesTheSliderUntilTheModuleIsDisabled()
    {
        Assert.SkipWhen(DeviceInput.IsIPhone, "An iPhone has no arrow keys to press.");

        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper-modules--keyboard", "keyboard-state");
        await canvas.GetByTestId("keyboard-swiper").ClickAsync();

        // Act
        await fixture.Page.Keyboard.PressAsync("ArrowRight");

        // Assert
        await StoryState.WaitForAsync(
            canvas,
            "keyboard-state",
            state => StoryState.Int(state, "activeIndex") == 1,
            "the arrow key to move the slider");

        // Act - and the runtime toggle has to actually take the keys away again.
        await canvas.GetByTestId("keyboard-toggle").ClickAsync();
        await fixture.Page.Keyboard.PressAsync("ArrowRight");
        await Task.Delay(400);

        // Assert
        Assert.Equal(1, await fixture.ReadSwiperAsync<int>("keyboard-swiper", "s.realIndex"));
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task GetState_AfterAMove_ReportsWhatSwiperActuallyHolds()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper--reading-the-state", "read-state-panel");

        // Act
        await canvas.GetByTestId("read-state").ClickAsync();

        // Assert - one call, every value, and the visible-slide indexes that only exist because the
        // story turned WatchSlidesProgress on.
        var state = await StoryState.WaitForAsync(
            canvas,
            "read-state-panel",
            candidate => candidate.TryGetProperty("slidesCount", out _),
            "the state snapshot to be read");

        Assert.Equal(5, state.GetProperty("slidesCount").GetInt32());
        Assert.True(state.GetProperty("isBeginning").GetBoolean());
        Assert.False(state.GetProperty("isEnd").GetBoolean());
        Assert.True(state.GetProperty("visibleSlideIndexes").GetArrayLength() > 0);
        fixture.AssertNoJsErrors();
    }

    private async Task<string> ActiveSlideLabel()
    {
        return await fixture.ReadSwiperAsync<string>("anchor-swiper", "s.slides[s.activeIndex].textContent.trim()");
    }
}

using System.Text.Json;
using Xunit;

namespace Blazor.Swiper.E2E;

/// <summary>
/// Programmatic moves of a cssMode slider that overlap, or that are requested just before the host
/// blocks the main thread with a render. The wrapper animates these itself by writing the scroll
/// offset per frame with scroll-snap suspended, so every one of them is a question of what a second
/// request does to an animation already in flight - which only real frames and a real scroll
/// container can answer.
/// </summary>
/// <remarks>
/// The scenarios drive the interop module directly against the story's own slider rather than
/// through its button. Two requests have to land a known number of milliseconds apart, and a click
/// goes through Blazor's event dispatch and a render first, which decides that gap for us.
/// </remarks>
[Collection(DemoCollectionDefinition.Name)]
public sealed class SwiperCssModeMoveTests(DemoFixture fixture)
{
    private const string CssModeStory = "components-swiper--css-mode";
    private const string CssModeState = "css-mode-state";

    /// <summary>
    /// How far the scroll offset may differ between two reads of a slider that should not have moved.
    /// Each read is rounded, so one pixel either way is the rounding and not a jump.
    /// </summary>
    private const int HandoverTolerancePixels = 2;

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    // Shared page-side setup: the interop module, the story's slider, and waits counted in time and
    // in frames. `settle` outlasts the 300ms move with room to spare.
    private const string Harness = @"
        const interop = await import(new URL('./_content/Kebechet.Blazor.Swiper/swiper-interop.js', document.baseURI).href);

        const host = document.querySelector('[data-testid=""css-mode-swiper""]');
        const wrapper = host.swiper.wrapperEl;
        const offsetOf = (index) => host.swiper.slides[index].offsetLeft;

        const wait = (milliseconds) => new Promise(resolve => setTimeout(resolve, milliseconds));
        const frame = () => new Promise(resolve => requestAnimationFrame(resolve));
        const settle = () => wait(900);

        const read = (extra) => JSON.stringify({
            scrollLeft: Math.round(wrapper.scrollLeft),
            inlineSnapType: wrapper.style.scrollSnapType,
            computedSnapType: getComputedStyle(wrapper).scrollSnapType,
            realIndex: host.swiper.realIndex,
            ...extra
        });
    ";

    // A slider of its own on a 420px stage, for scenarios the story's slider cannot set up. `finishSlider`
    // reads the result and removes the slider again.
    private const string StandaloneSlider = @"
        const standaloneSlider = async (options, { slideWidth = null, count = 10, stageWidth = 420 } = {}) => {
            const stage = document.createElement('div');
            stage.style.width = `${stageWidth}px`;
            document.body.appendChild(stage);

            const el = document.createElement('swiper-container');
            el.setAttribute('init', 'false');
            for (let i = 0; i < count; i++) {
                const slide = document.createElement('swiper-slide');
                slide.textContent = `Slide ${i}`;
                if (slideWidth) {
                    slide.style.width = slideWidth;
                }
                slide.style.height = '120px';
                el.appendChild(slide);
            }
            stage.appendChild(el);

            await interop.initialize(el, { cssMode: true, spaceBetween: 16, ...options }, null, [], 0, false);
            return { stage, el, track: el.swiper.wrapperEl };
        };

        // Slides sized by CSS - 80% of the stage - rather than by Swiper. Swiper pins the width of every slide
        // it sizes itself, so those keep their old offsets until its own resize handling runs; these move with
        // the stage at once, while Swiper still holds the slide grid from before the resize.
        const cssSizedSlider = async (options) => {
            const slider = await standaloneSlider({ slidesPerView: 'auto', ...options }, { slideWidth: '80%' });
            await settle();
            return slider;
        };

        const finishSlider = ({ stage, el, track }, index, extra) => {
            const json = JSON.stringify({
                scrollLeft: Math.round(track.scrollLeft),
                realIndex: el.swiper.realIndex,
                expectedIndex: index,
                expectedScrollLeft: el.swiper.slides[index].offsetLeft,
                ...extra
            });
            stage.remove();
            return json;
        };
    ";

    [Fact]
    public async Task SlideTo_SecondMoveStartedDuringTheFirst_LeavesScrollSnapOn()
    {
        // Arrange
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act - the second move starts while the first still has scroll-snap suspended
        var result = await EvaluateAsync(@"
            const snapTypeAtRest = getComputedStyle(wrapper).scrollSnapType;
            interop.slideTo(host, 2, 300);
            await wait(120);
            const snapTypeMidMove = getComputedStyle(wrapper).scrollSnapType;
            interop.slideTo(host, 0, 300);
            await settle();
            return read({ snapTypeAtRest, snapTypeMidMove, expectedScrollLeft: offsetOf(0) });");

        // Assert - the slider snaps at rest and not mid-move, so the final value is a real restore
        // rather than a slider that never suspended snapping in the first place.
        Assert.NotEqual("none", result.SnapTypeAtRest);
        Assert.Equal("none", result.SnapTypeMidMove);
        Assert.Equal(result.SnapTypeAtRest, result.ComputedSnapType);
        Assert.Equal(string.Empty, result.InlineSnapType);
        Assert.Equal(result.ExpectedScrollLeft, result.ScrollLeft);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task SlideTo_ReleasedBetweenSlidesAfterOverlappingMoves_SnapsToASlide()
    {
        // Arrange
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act - the offset a swipe released part-way would leave the slider at
        var result = await EvaluateAsync(@"
            interop.slideTo(host, 2, 300);
            await wait(120);
            interop.slideTo(host, 0, 300);
            await settle();
            wrapper.scrollLeft = Math.round(offsetOf(1) * 0.4);
            await settle();
            return read({ expectedScrollLeft: offsetOf(0), otherSlideScrollLeft: offsetOf(1) });");

        // Assert - on a slide, whichever of the two neighbours the browser picked
        Assert.Contains(result.ScrollLeft, new[] { result.ExpectedScrollLeft, result.OtherSlideScrollLeft });
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task SlideTo_BackToTheStartBeforeTheFirstMoveBegan_EndsOnTheLastRequestedSlide()
    {
        // Arrange
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act - the slider has not moved yet, so the second request finds it already on its target
        var result = await EvaluateAsync(@"
            interop.slideTo(host, 2, 300);
            interop.slideTo(host, 0, 300);
            await settle();
            return read({ expectedScrollLeft: offsetOf(0), otherSlideScrollLeft: offsetOf(2) });");

        // Assert
        Assert.NotEqual(result.ExpectedScrollLeft, result.OtherSlideScrollLeft);
        Assert.Equal(result.ExpectedScrollLeft, result.ScrollLeft);
        Assert.Equal(0, result.RealIndex);
        Assert.Equal(string.Empty, result.InlineSnapType);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task SlideTo_ThreeOverlappingMoves_EndsOnTheLastRequestedSlide()
    {
        // Arrange
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act
        var result = await EvaluateAsync(@"
            interop.slideTo(host, 3, 300);
            await wait(80);
            interop.slideTo(host, 0, 300);
            await wait(80);
            interop.slideTo(host, 1, 300);
            await settle();
            return read({ expectedScrollLeft: offsetOf(1) });");

        // Assert
        Assert.Equal(result.ExpectedScrollLeft, result.ScrollLeft);
        Assert.Equal(1, result.RealIndex);
        Assert.Equal(string.Empty, result.InlineSnapType);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task SlideTo_MainThreadBlockedRightAfterTheRequest_StillAnimates()
    {
        // Arrange
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act - what a host does when the move and a heavy render come from the same click: the
        // request is made, then the thread is gone for longer than the whole move lasts
        var result = await EvaluateAsync(@"
            interop.slideTo(host, 2, 300);
            const blockedUntil = performance.now() + 450;
            while (performance.now() < blockedUntil) { }
            await frame();
            await frame();
            const scrollLeftAfterTwoFrames = Math.round(wrapper.scrollLeft);
            await settle();
            return read({ scrollLeftAfterTwoFrames, expectedScrollLeft: offsetOf(2) });");

        // Assert - the move is still under way two frames in, and arrives afterwards. A clock
        // started at the request has already run out by the first frame, which jumps instead.
        Assert.True(
            result.ScrollLeftAfterTwoFrames < result.ExpectedScrollLeft / 2,
            $"The slider was at {result.ScrollLeftAfterTwoFrames}px of {result.ExpectedScrollLeft}px two frames after the thread came back.");
        Assert.Equal(result.ExpectedScrollLeft, result.ScrollLeft);
        Assert.Equal(string.Empty, result.InlineSnapType);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task SlideTo_ReplacingAMoveInFlight_StartsFromWhereTheSliderIs()
    {
        // Arrange
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act - the first move is caught between two slides, which is where scroll-snap would pull
        // the slider onto the nearer one the moment it is handed back
        var result = await EvaluateAsync(@"
            interop.slideTo(host, 2, 1000);
            await wait(450);
            const scrollLeftBeforeHandover = Math.round(wrapper.scrollLeft);
            interop.slideTo(host, 0, 1000);
            const scrollLeftAfterHandover = Math.round(wrapper.scrollLeft);
            await frame();
            const scrollLeftOnNextFrame = Math.round(wrapper.scrollLeft);
            await wait(1400);
            return read({
                scrollLeftBeforeHandover,
                scrollLeftAfterHandover,
                scrollLeftOnNextFrame,
                expectedScrollLeft: offsetOf(0),
                otherSlideScrollLeft: offsetOf(2)
            });");

        // Assert - caught mid-way, so staying put is not the same as resting on a slide
        Assert.InRange(result.ScrollLeftBeforeHandover ?? -1, 1, result.OtherSlideScrollLeft - 1);
        AssertWithinPixels(result.ScrollLeftBeforeHandover, result.ScrollLeftAfterHandover);
        AssertWithinPixels(result.ScrollLeftBeforeHandover, result.ScrollLeftOnNextFrame);
        Assert.Equal(result.ExpectedScrollLeft, result.ScrollLeft);
        Assert.Equal(string.Empty, result.InlineSnapType);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task SlideNext_DuringAMove_MovesTheSlider()
    {
        // Arrange
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act - Swiper steps on from the slide it holds at that moment, part-way through the move
        var result = await EvaluateAsync(@"
            interop.slideTo(host, 2, 0);
            await settle();
            interop.slideTo(host, 0, 1000);
            await wait(150);
            const expectedIndex = host.swiper.activeIndex + 1;
            interop.slideNext(host, 300);
            await wait(1600);
            return read({ expectedIndex, expectedScrollLeft: offsetOf(expectedIndex) });");

        // Assert - left running, the move writes its own position every frame and ends on slide 0
        Assert.Equal(result.ExpectedIndex, result.RealIndex);
        Assert.Equal(result.ExpectedScrollLeft, result.ScrollLeft);
        Assert.Equal(string.Empty, result.InlineSnapType);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task SlideReset_RightAfterAMoveWasRequested_KeepsTheSliderWhereItIs()
    {
        // Arrange
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act - the reset finds the slider still on the slide it holds, so Swiper has nothing to move and
        // answers false, just as it does for a call it turns down
        var result = await EvaluateAsync(@"
            interop.slideTo(host, 2, 400);
            interop.slideReset(host, 0);
            await settle();
            return read({ expectedIndex: 0, expectedScrollLeft: offsetOf(0), otherSlideScrollLeft: offsetOf(2) });");

        // Assert - left running, the move carries the slider on to the third slide
        Assert.Equal(result.ExpectedIndex, result.RealIndex);
        Assert.Equal(result.ExpectedScrollLeft, result.ScrollLeft);
        Assert.Equal(string.Empty, result.InlineSnapType);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task SlidePrev_TurnedDownDuringAMove_LeavesTheMoveRunning()
    {
        // Arrange - stepping back is locked, so Swiper turns a step back down
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act
        var result = await EvaluateAsync(@"
            interop.slideTo(host, 1, 0);
            await settle();
            host.swiper.allowSlidePrev = false;
            interop.slideTo(host, 3, 1000);
            await wait(150);
            interop.slidePrev(host, 0);
            await wait(1600);
            return read({ expectedIndex: 3, expectedScrollLeft: offsetOf(3) });");

        // Assert - the slider was sent nowhere, so the move still lands on its own slide
        Assert.Equal(result.ExpectedIndex, result.RealIndex);
        Assert.Equal(result.ExpectedScrollLeft, result.ScrollLeft);
        Assert.Equal(string.Empty, result.InlineSnapType);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task Resize_DuringAMove_EndsOnTheRequestedSlide()
    {
        // Arrange
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act - the slides move under the animation: the offset measured at the request is no
        // longer where the third slide is
        var result = await EvaluateAsync(@"
            const offsetBeforeResize = offsetOf(2);
            interop.slideTo(host, 2, 1000);
            await wait(300);
            host.style.width = '70%';
            await wait(1600);
            return read({ expectedScrollLeft: offsetOf(2), otherSlideScrollLeft: offsetBeforeResize });");

        // Assert
        Assert.NotEqual(result.OtherSlideScrollLeft, result.ExpectedScrollLeft);
        Assert.Equal(result.ExpectedScrollLeft, result.ScrollLeft);
        Assert.Equal(2, result.RealIndex);
        Assert.Equal(string.Empty, result.InlineSnapType);
        fixture.AssertNoJsErrors();
    }

    [Theory]
    [InlineData(2, 300)]
    [InlineData(1, 0)]
    [InlineData(0, 300)]
    public async Task Resize_AfterAMoveThatFinished_StaysOnTheSlideTheSliderIsOn(int index, int speed)
    {
        // Arrange - an animated move, an instant one, and one the slider was already on
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act - the slider has since been moved on by something else
        var result = await EvaluateAsync($@"
            interop.slideTo(host, {index}, {speed});
            await settle();
            interop.slideNext(host, 0);
            await settle();
            host.style.width = '70%';
            await settle();
            return read({{ expectedIndex: {index} + 1, expectedScrollLeft: offsetOf({index} + 1) }});");

        // Assert - cssMode raises no transition end, so nothing else ever stands the finished move down
        Assert.Equal(result.ExpectedIndex, result.RealIndex);
        Assert.Equal(result.ExpectedScrollLeft, result.ScrollLeft);
        fixture.AssertNoJsErrors();
    }

    [Theory]
    [InlineData("200px")]
    [InlineData("900px")]
    public async Task SlideTo_InstantMoveWhileTheSliderResizes_EndsOnTheRequestedSlide(string resizedWidth)
    {
        // Arrange
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act - the host resizes the slider and asks for an instant move in the same task
        var result = await EvaluateAsync(StandaloneSlider + $@"
            const slider = await cssSizedSlider({{}});
            const offsetBeforeResize = slider.el.swiper.slides[5].offsetLeft;
            slider.stage.style.width = '{resizedWidth}';
            interop.slideTo(slider.el, 5, 0);
            await settle();
            return finishSlider(slider, 5, {{ otherSlideScrollLeft: offsetBeforeResize }});");

        // Assert - Swiper reads its index off the old slide grid on the first scroll, and re-seats onto
        // that slide when it handles the resize
        Assert.NotEqual(result.OtherSlideScrollLeft, result.ExpectedScrollLeft);
        Assert.Equal(result.ExpectedIndex, result.RealIndex);
        Assert.Equal(result.ExpectedScrollLeft, result.ScrollLeft);
        fixture.AssertNoJsErrors();
    }

    [Theory]
    [InlineData("200px")]
    [InlineData("900px")]
    public async Task Resize_CssSizedSlidesAtRest_StaysOnTheSlide(string resizedWidth)
    {
        // Arrange - on the sixth slide from the start, so no move and no intent of the wrapper's is involved
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act
        var result = await EvaluateAsync(StandaloneSlider + $@"
            const slider = await cssSizedSlider({{ initialSlide: 5 }});
            const realIndexBeforeResize = slider.el.swiper.realIndex;
            const offsetBeforeResize = slider.el.swiper.slides[5].offsetLeft;
            slider.stage.style.width = '{resizedWidth}';
            await settle();
            return finishSlider(slider, 5, {{ realIndexBeforeResize, otherSlideScrollLeft: offsetBeforeResize }});");

        // Assert - the browser re-snaps onto the same slide at its new offset, and Swiper reads that re-snap
        // against the slide grid from before the resize: https://github.com/nolimits4web/swiper/issues/8246
        Assert.Equal(result.ExpectedIndex, result.RealIndexBeforeResize);
        Assert.NotEqual(result.OtherSlideScrollLeft, result.ExpectedScrollLeft);
        Assert.Equal(result.ExpectedIndex, result.RealIndex);
        Assert.Equal(result.ExpectedScrollLeft, result.ScrollLeft);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task Resize_CssSizedSlidesInFreeMode_StaysOnTheSlide()
    {
        // Arrange - free mode does not snap, so nothing re-snaps the slider; growing keeps its scroll position
        // in range, so the browser leaves that position alone
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act
        var result = await EvaluateAsync(StandaloneSlider + @"
            const slider = await cssSizedSlider({ initialSlide: 5, freeMode: true });
            const realIndexBeforeResize = slider.el.swiper.realIndex;
            const offsetBeforeResize = slider.el.swiper.slides[5].offsetLeft;
            slider.stage.style.width = '900px';
            await settle();
            return finishSlider(slider, 5, { realIndexBeforeResize, otherSlideScrollLeft: offsetBeforeResize });");

        // Assert - read against the new slide grid, that untouched position would name the third slide
        Assert.Equal(result.ExpectedIndex, result.RealIndexBeforeResize);
        Assert.Equal(result.ExpectedIndex, result.RealIndex);
        Assert.Equal(result.ExpectedScrollLeft, result.ScrollLeft);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task Resize_WithWindowResizeUpdatesOff_LeavesTheSliderUnmeasured()
    {
        // Arrange - a host that turned Swiper's own resize handling off measures the slider itself, when it
        // chooses to
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act
        var result = await EvaluateAsync(StandaloneSlider + @"
            const slider = await cssSizedSlider({ initialSlide: 5, updateOnWindowResize: false });
            const sizeBeforeResize = Math.round(slider.el.swiper.size);
            slider.stage.style.width = '900px';
            await settle();
            const result = JSON.stringify({ sizeBeforeResize, sizeAfterResize: Math.round(slider.el.swiper.size) });
            slider.stage.remove();
            return result;");

        // Assert - the drift correction runs ahead of Swiper's resize handling and must not stand in for it
        Assert.Equal(420, result.SizeBeforeResize);
        Assert.Equal(result.SizeBeforeResize, result.SizeAfterResize);
        fixture.AssertNoJsErrors();
    }

    [Theory]
    [InlineData(2, 390, 844, "SlideTo")]
    [InlineData(3, 390, 844, "SlideTo")]
    [InlineData(10, 420, 900, "SlideTo")]
    [InlineData(10, 420, 900, "Swiper")]
    public async Task Resize_GrowingWhileOnTheLastSlide_StaysOnIt(int slideCount, int fromWidth, int toWidth, string route)
    {
        // Arrange - slides Swiper sizes itself, on the last one: a pager on its last tab, say, as the phone turns
        // from portrait to landscape. Swiper's own navigation takes the slider there just as well as SlideTo.
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);
        var moveToLast = route == "SlideTo" ? "interop.slideTo(slider.el, last, 0);" : "slider.el.swiper.slideTo(last, 0);";

        // Act
        var result = await EvaluateAsync(StandaloneSlider + $@"
            const slider = await standaloneSlider({{}}, {{ count: {slideCount}, stageWidth: {fromWidth} }});
            await settle();
            const last = {slideCount} - 1;
            {moveToLast}
            await settle();
            const realIndexBeforeResize = slider.el.swiper.realIndex;
            slider.stage.style.width = '{toWidth}px';
            await settle();
            return finishSlider(slider, last, {{ realIndexBeforeResize }});");

        // Assert - the wider slider leaves the old scroll position past the end of the track, so the browser
        // clamps it, and Swiper reads the clamped position against the slide grid from before the resize
        Assert.Equal(result.ExpectedIndex, result.RealIndexBeforeResize);
        Assert.Equal(result.ExpectedIndex, result.RealIndex);
        Assert.Equal(result.ExpectedScrollLeft, result.ScrollLeft);
        fixture.AssertNoJsErrors();
    }

    [Theory]
    [InlineData(0, 5, "SlideTo")]
    [InlineData(0, 5, "Swiper")]
    [InlineData(9, 2, "SlideTo")]
    public async Task SlideTo_InstantMoveInTheFrameAfterAResize_EndsOnTheRequestedSlide(int fromSlide, int toSlide, string route)
    {
        // Arrange - slides Swiper sizes itself. A width transition lays the slider out again every frame, so
        // an instant move - SlideTo, or a pagination bullet at Speed = 0 - lands in this window all the time
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);
        var move = route == "SlideTo" ? $"interop.slideTo(slider.el, {toSlide}, 0);" : $"slider.el.swiper.slideTo({toSlide}, 0);";

        // Act - Swiper handles a layout change a frame after its ResizeObserver hears of it, and the move
        // comes in between
        var result = await EvaluateAsync(StandaloneSlider + $@"
            const slider = await standaloneSlider({{}});
            await settle();
            slider.el.swiper.slideTo({fromSlide}, 0);
            await settle();
            slider.stage.style.width = '600px';
            await frame();
            await wait(0);
            {move}
            await settle();
            return finishSlider(slider, {toSlide});");

        // Assert - the move is the slider's state now, not the index it held when the layout changed
        Assert.Equal(result.ExpectedIndex, result.RealIndex);
        Assert.Equal(result.ExpectedScrollLeft, result.ScrollLeft);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task UpdateOptions_CssModeSwitchedOnAfterInit_ResizesWithoutErrors()
    {
        // Arrange - cssMode is init-only: switching it on later reaches the params, but Swiper never wires the
        // scroll handling that goes with it
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act
        var result = await EvaluateAsync(StandaloneSlider + @"
            const slider = await cssSizedSlider({ cssMode: false });
            interop.updateOptions(slider.el, { cssMode: true, spaceBetween: 16, slidesPerView: 'auto' });
            const isCssModeAfterUpdate = slider.el.swiper.params.cssMode;
            slider.stage.style.width = '900px';
            await settle();
            slider.stage.style.width = '300px';
            await settle();
            slider.stage.remove();
            return JSON.stringify({ isCssModeAfterUpdate });");

        // Assert
        Assert.True(result.IsCssModeAfterUpdate);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task SlideTo_StartedWhileSwiperHoldsScrollSnapOff_HandsScrollSnapBack()
    {
        // Arrange - a virtual slider. Swiper switches scroll-snap off itself for one frame around every instant
        // move it makes there, and an anchor is one
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act - the host anchors and moves in the same task, so the move starts inside that frame. Where the
        // move heads is not the point here, only what it leaves behind
        var result = await EvaluateAsync(StandaloneSlider + @"
            const slider = await standaloneSlider({ virtual: true });
            await settle();
            interop.updateAndAnchor(slider.el, 2);
            const snapTypeAtRequest = slider.track.style.scrollSnapType;
            const scrollLeftAtRequest = Math.round(slider.track.scrollLeft);
            interop.slideTo(slider.el, 6, 600);
            await settle();
            const result = JSON.stringify({
                snapTypeAtRequest,
                scrollLeftAtRequest,
                scrollLeftAfterMove: Math.round(slider.track.scrollLeft),
                inlineSnapType: slider.track.style.scrollSnapType,
                computedSnapType: getComputedStyle(slider.track).scrollSnapType
            });
            slider.stage.remove();
            return result;");

        // Assert - the move found Swiper's own 'none' and must not hand that back: snapping would stay off for
        // good, and the slider would rest wherever a swipe is released
        Assert.Equal("none", result.SnapTypeAtRequest);
        Assert.NotEqual(result.ScrollLeftAtRequest, result.ScrollLeftAfterMove);
        Assert.Equal(string.Empty, result.InlineSnapType);
        Assert.Contains("mandatory", result.ComputedSnapType);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task SwiperSlideTo_InstantOnAVirtualSliderDuringAMove_LandsOnItsSlide()
    {
        // Arrange - a virtual slider. Swiper switches scroll-snap off itself around an instant move there, and
        // puts it back a frame later, once the move's slides are rendered
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act - Swiper's own instant navigation, part-way through a move of the host's
        var result = await EvaluateAsync(StandaloneSlider + @"
            const slider = await standaloneSlider({ virtual: true });
            await settle();
            interop.slideTo(slider.el, 1, 1500);
            await wait(300);
            slider.el.swiper.slideTo(7, 0);
            await wait(1600);
            const result = JSON.stringify({
                realIndex: slider.el.swiper.realIndex,
                inlineSnapType: slider.track.style.scrollSnapType,
                computedSnapType: getComputedStyle(slider.track).scrollSnapType
            });
            slider.stage.remove();
            return result;");

        // Assert - handing scroll-snap back inside Swiper's window lets the browser snap the wrapper onto a slide
        // that is rendered already, before the one Swiper is moving to
        Assert.Equal(7, result.RealIndex);
        Assert.Equal(string.Empty, result.InlineSnapType);
        Assert.Contains("mandatory", result.ComputedSnapType);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task SlideTo_OnAWrapperWithItsOwnInlineSnapType_HandsThatSnapTypeBack()
    {
        // Arrange - a host that set the wrapper's snapping inline itself, which is not a 'none' of Swiper's
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act
        var result = await EvaluateAsync(@"
            wrapper.style.scrollSnapType = 'x proximity';
            const snapTypeAtRest = wrapper.style.scrollSnapType;
            const computedSnapTypeAtRest = getComputedStyle(wrapper).scrollSnapType;
            interop.slideTo(host, 2, 300);
            await settle();
            return read({ snapTypeAtRest, computedSnapTypeAtRest });");

        // Assert
        Assert.NotEqual(string.Empty, result.SnapTypeAtRest);
        Assert.Equal(result.SnapTypeAtRest, result.InlineSnapType);
        Assert.Equal(result.ComputedSnapTypeAtRest, result.ComputedSnapType);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task Autoplay_StepAtSpeedZeroDuringAMove_MovesTheSlider()
    {
        // Arrange - at speed 0 every autoplay step is instant and flagged internal, just like Swiper re-seating
        // itself after a resize; only its callbacks set it apart
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act - the first autoplay step lands part-way through a long move, and autoplay stops after it so the
        // slider has nowhere else to go
        var result = await EvaluateAsync(StandaloneSlider + @"
            const slider = await standaloneSlider({ speed: 0, autoplay: { delay: 800 } });
            let scrollLeftAtAutoplay = null;
            let millisecondsIntoMove = null;
            slider.el.addEventListener('swiperautoplay', () => {
                if (scrollLeftAtAutoplay !== null) {
                    return;
                }
                scrollLeftAtAutoplay = Math.round(slider.track.scrollLeft);
                millisecondsIntoMove = Math.round(performance.now() - requestedAt);
                slider.el.swiper.autoplay.stop();
            });
            const requestedAt = performance.now();
            interop.slideTo(slider.el, 6, 2500);
            await wait(3200);
            const result = JSON.stringify({
                scrollLeft: Math.round(slider.track.scrollLeft),
                expectedScrollLeft: scrollLeftAtAutoplay ?? -1,
                otherSlideScrollLeft: slider.el.swiper.slides[6].offsetLeft,
                inlineSnapType: slider.track.style.scrollSnapType,
                millisecondsIntoMove
            });
            slider.stage.remove();
            return result;");

        // Assert - left running, the move writes its own offset over the step every frame and ends on its slide
        Assert.NotEqual(-1, result.ExpectedScrollLeft);
        Assert.InRange(result.MillisecondsIntoMove ?? -1, 0, 2000);
        Assert.NotEqual(result.OtherSlideScrollLeft, result.ExpectedScrollLeft);
        AssertWithinPixels(result.ExpectedScrollLeft, result.ScrollLeft);
        Assert.Equal(string.Empty, result.InlineSnapType);
        fixture.AssertNoJsErrors();
    }

    [Theory]
    [InlineData("wrapper.dispatchEvent(new WheelEvent('wheel', { deltaX: 40, bubbles: true, cancelable: true }));")]
    [InlineData("wrapper.dispatchEvent(new WheelEvent('wheel', { deltaY: 40, bubbles: true, cancelable: true }));")]
    public async Task UserScroll_DuringAMove_HandsTheSliderBackToTheUser(string userScroll)
    {
        // Arrange - the wheel on either axis, which Swiper turns into a step of its own in cssMode
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act
        var result = await EvaluateAsync($@"
            const slideOffsets = Array.from(host.swiper.slides, slide => slide.offsetLeft);
            interop.slideTo(host, 4, 1500);
            await wait(200);
            {userScroll}
            const snapTypeMidMove = wrapper.style.scrollSnapType;
            await wait(2000);
            return read({{ snapTypeMidMove, slideOffsets, otherSlideScrollLeft: offsetOf(4) }});");

        // Assert - snapping is back at once rather than when the move would have finished, and
        // the slider rests on a slide that is not the one the move was heading for
        Assert.Equal(string.Empty, result.SnapTypeMidMove);
        Assert.Contains(result.ScrollLeft, result.SlideOffsets ?? []);
        Assert.NotEqual(result.OtherSlideScrollLeft, result.ScrollLeft);
        Assert.Equal(string.Empty, result.InlineSnapType);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task Drag_DuringAMove_HandsTheSliderBackToTheUser()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);
        var slider = canvas.GetByTestId("css-mode-swiper");

        // Act - a real pointer drag, so it is Swiper that raises sliderFirstMove and not the scenario.
        // The drag is over well before the move would have finished by itself.
        var beforeDrag = await EvaluateAsync(@"
            interop.slideTo(host, 4, 1500);
            await wait(250);
            return read();");
        await SwipeHelper.SwipeLeftAsync(fixture.Page, slider);
        var afterDrag = await EvaluateAsync("return read();");
        var result = await EvaluateAsync(@"
            await wait(2000);
            return read({
                slideOffsets: Array.from(host.swiper.slides, slide => slide.offsetLeft),
                otherSlideScrollLeft: offsetOf(4)
            });");

        // Assert - snapping is back at once rather than when the move would have finished, and
        // the slider rests on a slide that is not the one the move was heading for
        Assert.Equal("none", beforeDrag.InlineSnapType);
        Assert.Equal(string.Empty, afterDrag.InlineSnapType);
        Assert.Contains(result.ScrollLeft, result.SlideOffsets ?? []);
        Assert.NotEqual(result.OtherSlideScrollLeft, result.ScrollLeft);
        Assert.Equal(string.Empty, result.InlineSnapType);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task Resize_DuringAMove_NeverShowsTheTargetEarly()
    {
        // Arrange
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act - every frame for a while after the resize, read both as the frame starts and once it
        // has been painted. The move is slow enough to be well short of its slide throughout.
        var result = await EvaluateAsync(@"
            interop.slideTo(host, 2, 2000);
            await wait(300);
            host.style.width = '70%';
            let highestScrollLeft = 0;
            const sampledUntil = performance.now() + 300;
            while (performance.now() < sampledUntil) {
                await frame();
                highestScrollLeft = Math.max(highestScrollLeft, wrapper.scrollLeft);
                await wait(0);
                highestScrollLeft = Math.max(highestScrollLeft, wrapper.scrollLeft);
            }
            await wait(2400);
            return read({ highestScrollLeftAfterResize: Math.round(highestScrollLeft), expectedScrollLeft: offsetOf(2) });");

        // Assert - the resize guard anchoring onto the intended slide would put the slider on it for
        // a frame, before the move writes its own offset again
        Assert.NotNull(result.HighestScrollLeftAfterResize);
        Assert.True(
            result.HighestScrollLeftAfterResize < result.ExpectedScrollLeft * 0.9,
            $"The slider reached {result.HighestScrollLeftAfterResize}px of {result.ExpectedScrollLeft}px right after the resize.");
        Assert.Equal(result.ExpectedScrollLeft, result.ScrollLeft);
        Assert.Equal(2, result.RealIndex);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task ArmAnchor_SlidesChangedDuringAMove_EndsOnTheAnchoredSlide()
    {
        // Arrange
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act - the appended slide is the mutation the anchor waits for
        var result = await EvaluateAsync(@"
            interop.slideTo(host, 3, 1000);
            await wait(200);
            interop.armAnchor(host, 1);
            host.appendChild(document.createElement('swiper-slide'));
            await frame();
            const snapTypeMidMove = wrapper.style.scrollSnapType;
            await wait(1400);
            return read({ snapTypeMidMove, expectedScrollLeft: offsetOf(1), otherSlideScrollLeft: offsetOf(3) });");

        // Assert - the anchor's own slideTo is instant and internal, exactly what a move in flight
        // lets through as Swiper re-seating itself, so the move has to be ended for it
        Assert.Equal(string.Empty, result.SnapTypeMidMove);
        Assert.NotEqual(result.OtherSlideScrollLeft, result.ExpectedScrollLeft);
        Assert.Equal(result.ExpectedScrollLeft, result.ScrollLeft);
        Assert.Equal(1, result.RealIndex);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task UpdateAndAnchor_DuringAMove_EndsOnTheAnchoredSlide()
    {
        // Arrange
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act
        var result = await EvaluateAsync(@"
            interop.slideTo(host, 3, 1000);
            await wait(200);
            interop.updateAndAnchor(host, 1);
            const snapTypeMidMove = wrapper.style.scrollSnapType;
            await wait(1400);
            return read({ snapTypeMidMove, expectedScrollLeft: offsetOf(1) });");

        // Assert - an anchor is the host saying where the slider is, so the move does not carry on
        // to its own slide afterwards
        Assert.Equal(string.Empty, result.SnapTypeMidMove);
        Assert.Equal(result.ExpectedScrollLeft, result.ScrollLeft);
        Assert.Equal(1, result.RealIndex);
        fixture.AssertNoJsErrors();
    }

    [Theory]
    [InlineData("controlled.shadowRoot.querySelector('.swiper-button-next').click(); expectedIndex = activeIndexAtInput + 1;")]
    [InlineData("pressKey(39); expectedIndex = activeIndexAtInput + 1;")]
    [InlineData("controlled.shadowRoot.querySelector('.swiper-pagination-bullet').click(); expectedIndex = 0;")]
    public async Task SwiperControl_UsedDuringAMove_MovesTheSlider(string useControl)
    {
        // Arrange - the next arrow, the right arrow key and the first pagination bullet. The story's
        // slider has none of the three, so the scenario builds one that does.
        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);

        // Act - Swiper handles each of these itself, without a call into the interop module
        var result = await EvaluateAsync($@"
            const stage = document.createElement('div');
            stage.style.width = '420px';
            document.body.appendChild(stage);

            const controlled = document.createElement('swiper-container');
            controlled.setAttribute('init', 'false');
            for (let i = 0; i < 5; i++) {{
                const slide = document.createElement('swiper-slide');
                slide.textContent = `Slide ${{i}}`;
                slide.style.height = '120px';
                controlled.appendChild(slide);
            }}
            stage.appendChild(controlled);

            const options = {{
                cssMode: true,
                spaceBetween: 16,
                navigation: true,
                pagination: {{ clickable: true }},
                keyboard: {{ enabled: true }}
            }};
            await interop.initialize(controlled, options, null, [], 0, false);
            const track = controlled.swiper.wrapperEl;

            // Swiper reads the legacy keyCode, which a constructed KeyboardEvent leaves at 0.
            const pressKey = (keyCode) => {{
                const keyDown = new KeyboardEvent('keydown', {{ bubbles: true }});
                Object.defineProperty(keyDown, 'keyCode', {{ value: keyCode }});
                document.dispatchEvent(keyDown);
            }};

            interop.slideTo(controlled, 4, 1500);
            await wait(250);
            const activeIndexAtInput = controlled.swiper.activeIndex;
            let expectedIndex = -1;
            {useControl}
            await wait(2200);

            const result = JSON.stringify({{
                scrollLeft: Math.round(track.scrollLeft),
                inlineSnapType: track.style.scrollSnapType,
                computedSnapType: getComputedStyle(track).scrollSnapType,
                realIndex: controlled.swiper.realIndex,
                expectedIndex,
                expectedScrollLeft: controlled.swiper.slides[expectedIndex].offsetLeft,
                otherSlideScrollLeft: controlled.swiper.slides[4].offsetLeft
            }});
            stage.remove();
            return result;");

        // Assert - left running, the move writes its own offset every frame and ends on the last slide
        Assert.NotEqual(result.OtherSlideScrollLeft, result.ExpectedScrollLeft);
        Assert.Equal(result.ExpectedIndex, result.RealIndex);
        Assert.Equal(result.ExpectedScrollLeft, result.ScrollLeft);
        Assert.Equal(string.Empty, result.InlineSnapType);
        fixture.AssertNoJsErrors();
    }

    private static void AssertWithinPixels(int? expected, int? actual)
    {
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.InRange(actual.Value, expected.Value - HandoverTolerancePixels, expected.Value + HandoverTolerancePixels);
    }

    private async Task<CssModeMoveResult> EvaluateAsync(string scenario)
    {
        var json = await fixture.Page.EvaluateAsync<string>($"async () => {{ {Harness} {scenario} }}");
        return JsonSerializer.Deserialize<CssModeMoveResult>(json, _jsonOptions)!;
    }

    private sealed record CssModeMoveResult(
        int ScrollLeft,
        string InlineSnapType,
        string ComputedSnapType,
        int RealIndex,
        int ExpectedScrollLeft,
        int OtherSlideScrollLeft,
        int ScrollLeftAfterTwoFrames,
        string? SnapTypeAtRest,
        string? SnapTypeMidMove,
        int? ScrollLeftBeforeHandover,
        int? ScrollLeftAfterHandover,
        int? ScrollLeftOnNextFrame,
        int? ExpectedIndex,
        int[]? SlideOffsets,
        int? HighestScrollLeftAfterResize,
        int? RealIndexBeforeResize,
        string? SnapTypeAtRequest,
        int? ScrollLeftAtRequest,
        int? ScrollLeftAfterMove,
        int? SizeBeforeResize,
        int? SizeAfterResize,
        string? ComputedSnapTypeAtRest,
        int? MillisecondsIntoMove,
        bool? IsCssModeAfterUpdate);
}

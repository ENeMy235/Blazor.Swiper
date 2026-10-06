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
        int? ScrollLeftOnNextFrame);
}

using System.Text.Json;
using Xunit;

namespace Blazor.Swiper.E2E;

/// <summary>
/// What only a real phone can do: taps through the phone's own input stack, and a real rotation.
/// </summary>
/// <remarks>
/// Skipped unless the suite runs against a phone. The <c>phone-e2e</c> skill in <c>.claude/skills</c> sets one up.
/// </remarks>
[Collection(DemoCollectionDefinition.Name)]
public sealed class DeviceScenarioTests(DemoFixture fixture, ITestOutputHelper output)
{
    private const string NeedsAPhone = "Needs a real phone: see the phone-e2e skill in .claude/skills.";

    private const string CssModeStory = "components-swiper--css-mode";
    private const string CssModeState = "css-mode-state";
    private const int TabMoveMilliseconds = 2000;

    private const string Harness = @"
        const interop = await import(new URL('./_content/Kebechet.Blazor.Swiper/swiper-interop.js', document.baseURI).href);
        window.e2eInterop = interop;
        const host = document.querySelector('[data-testid=""css-mode-swiper""]');
        const wrapper = host.swiper.wrapperEl;
        const wait = (milliseconds) => new Promise(resolve => setTimeout(resolve, milliseconds));
        const read = (extra) => JSON.stringify({
            scrollLeft: Math.round(wrapper.scrollLeft),
            slideOffsets: Array.from(host.swiper.slides, slide => slide.offsetLeft),
            inlineSnapType: wrapper.style.scrollSnapType,
            computedSnapType: getComputedStyle(wrapper).scrollSnapType,
            realIndex: host.swiper.realIndex,
            clicks: window.e2eClicks ?? [],
            ...extra
        });
    ";

    private async Task<JsonElement> EvaluateAsync(string scenario)
    {
        var json = await fixture.Page.EvaluateAsync<string>($"async () => {{ {Harness} {scenario} }}");
        output.WriteLine(json);
        return JsonDocument.Parse(json).RootElement;
    }

    private static void AssertRestsOnASlide(JsonElement state)
    {
        var scrollLeft = state.GetProperty("scrollLeft").GetInt32();
        var offsets = state.GetProperty("slideOffsets").EnumerateArray().Select(o => o.GetInt32()).ToArray();
        Assert.True(offsets.Any(o => Math.Abs(o - scrollLeft) <= 2), $"Resting between slides at {scrollLeft}: {state}");
        Assert.Equal(string.Empty, state.GetProperty("inlineSnapType").GetString());
        Assert.Contains("mandatory", state.GetProperty("computedSnapType").GetString());
    }

    /// <summary>Fails a run whose taps came too far apart to overlap the moves they started, and so tested nothing.</summary>
    private static void AssertEachTapCameDuringThePreviousMove(JsonElement state, int taps)
    {
        var clickTimes = state.GetProperty("clicks")
            .EnumerateArray()
            .Select(click => click[1].GetInt32())
            .ToArray();
        Assert.True(clickTimes.Length == taps, $"{taps} taps, {clickTimes.Length} clicks: {state}");
        var gaps = clickTimes
            .Zip(clickTimes.Skip(1), (previous, next) => next - previous)
            .ToArray();
        Assert.True(gaps.All(gap => gap < TabMoveMilliseconds), $"A tap came after the previous move had ended: {state}");
    }

    private async Task TapAsync(int gapMilliseconds, params (double X, double Y)[] clientPoints)
    {
        var screen = await DeviceInput.CalibrateAsync(fixture.Page);
        await DeviceInput.TapAsync(gapMilliseconds, clientPoints.Select(p => (screen.X(p.X), screen.Y(p.Y))).ToArray());
    }

    [Fact]
    public async Task StoryButton_TappedTwiceQuickly_LandsOnTheThirdSlideAndASwipeStillSnaps()
    {
        Assert.SkipUnless(DeviceInput.IsActive, NeedsAPhone);
        Assert.SkipWhen(DeviceInput.IsIPhone, "WebDriverAgent's taps land about 600 ms apart, too late to catch the button's 300 ms move.");

        var canvas = await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);
        var button = canvas.GetByTestId("css-mode-to-third");
        var slider = canvas.GetByTestId("css-mode-swiper");
        await slider.ScrollIntoViewIfNeededAsync();
        await fixture.Page.EvaluateAsync(@"() => {
            window.e2eClicks = [];
            document.querySelector('[data-testid=""css-mode-to-third""]')
                .addEventListener('click', () => window.e2eClicks.push(Math.round(performance.now())), true);
        }");
        var box = (await button.BoundingBoxAsync())!;
        var center = (box.X + (box.Width / 2), box.Y + (box.Height / 2));

        await TapAsync(30, center, center);
        var afterTaps = await EvaluateAsync("await wait(1500); return read({ expectedScrollLeft: host.swiper.slides[2].offsetLeft });");

        await SwipeHelper.SwipeLeftAsync(fixture.Page, slider);
        var afterSwipe = await EvaluateAsync("await wait(1500); return read();");

        Assert.Equal(2, afterTaps.GetProperty("clicks").GetArrayLength());
        Assert.Equal(2, afterTaps.GetProperty("realIndex").GetInt32());
        Assert.True(Math.Abs(afterTaps.GetProperty("expectedScrollLeft").GetInt32() - afterTaps.GetProperty("scrollLeft").GetInt32()) <= 2);
        AssertRestsOnASlide(afterTaps);
        Assert.Equal(3, afterSwipe.GetProperty("realIndex").GetInt32());
        AssertRestsOnASlide(afterSwipe);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task TabToggle_TappedQuicklyBackAndForth_RestsOnTheLastTappedPageAndAShortDragSnapsBack()
    {
        Assert.SkipUnless(DeviceInput.IsActive, NeedsAPhone);

        var canvas = await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);
        var slider = canvas.GetByTestId("css-mode-swiper");
        await slider.ScrollIntoViewIfNeededAsync();
        // The host's two-tab pager: each tab asks for its page with a move long enough for the taps to overlap
        // it, even on an iPhone, whose taps land about 600 ms apart.
        await EvaluateAsync($"const moveMilliseconds = {TabMoveMilliseconds};" + @"
            window.e2eClicks = [];
            const bar = document.createElement('div');
            bar.id = 'e2e-tabs';
            // At the top: Safari's floating toolbar covers the bottom of the page.
            bar.style.cssText = 'position:fixed;left:0;right:0;top:0;z-index:99999;display:flex;gap:8px;padding:8px;background:#fff';
            [0, 1].forEach(page => {
                const tab = document.createElement('button');
                tab.id = `e2e-tab-${page}`;
                tab.textContent = `Page ${page}`;
                tab.style.cssText = 'flex:1;height:72px;font-size:20px';
                tab.addEventListener('click', () => {
                    window.e2eClicks.push([page, Math.round(performance.now())]);
                    window.e2eInterop.slideTo(host, page, moveMilliseconds);
                });
                bar.appendChild(tab);
            });
            document.body.appendChild(bar);
            return read();");
        async Task<(double, double)> CenterOf(string id)
        {
            var box = (await fixture.Page.Locator(id).BoundingBoxAsync())!;
            return (box.X + (box.Width / 2), box.Y + (box.Height / 2));
        }
        var tab0 = await CenterOf("#e2e-tab-0");
        var tab1 = await CenterOf("#e2e-tab-1");

        await TapAsync(150, tab1, tab0);
        var afterTwoTaps = await EvaluateAsync($"await wait({TabMoveMilliseconds + 500}); const r = read(); window.e2eClicks = []; return r;");

        await TapAsync(150, tab1, tab0, tab1);
        var afterThreeTaps = await EvaluateAsync($"await wait({TabMoveMilliseconds + 500}); return read();");

        var box = (await slider.BoundingBoxAsync())!;
        var screen = await DeviceInput.CalibrateAsync(fixture.Page);
        var y = screen.Y(box.Y + (box.Height / 2));
        await EvaluateAsync(@"
            window.e2eLowestScrollLeft = wrapper.scrollLeft;
            wrapper.addEventListener('scroll', () => {
                window.e2eLowestScrollLeft = Math.min(window.e2eLowestScrollLeft, wrapper.scrollLeft);
            });
            return read();");
        // Slow enough that no fling carries it on: the finger lets go between two slides.
        await DeviceInput.SwipeAsync(screen.X(box.X + (box.Width * 0.6)), y, screen.X(box.X + (box.Width * 0.75)), y, 1500);
        var afterShortDrag = await EvaluateAsync(
            "await wait(1500); document.getElementById('e2e-tabs').remove(); return read({ lowestScrollLeft: Math.round(window.e2eLowestScrollLeft) });");
        var secondSlideOffset = afterShortDrag.GetProperty("slideOffsets")[1].GetInt32();
        Assert.True(afterShortDrag.GetProperty("lowestScrollLeft").GetInt32() < secondSlideOffset - 30, "The drag never moved the slider.");

        AssertEachTapCameDuringThePreviousMove(afterTwoTaps, 2);
        AssertEachTapCameDuringThePreviousMove(afterThreeTaps, 3);
        Assert.Equal(0, afterTwoTaps.GetProperty("realIndex").GetInt32());
        AssertRestsOnASlide(afterTwoTaps);
        Assert.Equal(1, afterThreeTaps.GetProperty("realIndex").GetInt32());
        AssertRestsOnASlide(afterThreeTaps);
        Assert.Equal(1, afterShortDrag.GetProperty("realIndex").GetInt32());
        AssertRestsOnASlide(afterShortDrag);
        fixture.AssertNoJsErrors();
    }

    private async Task BuildSlidersAsync(bool plainSwiper, string stageWidth)
    {
        await EvaluateAsync($"const plainSwiper = {(plainSwiper ? "true" : "false")}; const stageWidth = '{stageWidth}';" + @"
            const cases = [
                { name: 'two-tab pager, last tab', options: { slidesPerView: 1 }, count: 2, index: 1 },
                { name: 'two-tab pager, last tab (Swiper)', options: { slidesPerView: 1 }, count: 2, index: 1, bySwiper: true },
                { name: 'pager, last of 5 (Swiper)', options: { slidesPerView: 1 }, count: 5, index: 4, bySwiper: true },
                { name: 'peek 1.5, at the end (Swiper)', options: { slidesPerView: 1.5 }, count: 6, index: 5, end: true, bySwiper: true },
                { name: 'pager, last of 5', options: { slidesPerView: 1 }, count: 5, index: 4 },
                { name: 'pager, middle of 5', options: { slidesPerView: 1 }, count: 5, index: 2 },
                { name: 'peek 1.5, at the end', options: { slidesPerView: 1.5 }, count: 6, index: 5, end: true },
                { name: 'CSS-sized 80%, slide 3', options: { slidesPerView: 'auto' }, count: 8, index: 3, slideWidth: '80%' },
                { name: 'centered 1.5, slide 3', options: { slidesPerView: 1.5, centeredSlides: true }, count: 8, index: 3 },
                { name: 'loop pager, slide 3', options: { slidesPerView: 1, loop: true }, count: 5, index: 3 },
                { name: 'no cssMode, last of 5', options: { slidesPerView: 1, cssMode: false }, count: 5, index: 4 }
            ];
            window.e2eSliders = [];
            for (const c of cases.reverse()) {
                const stage = document.createElement('div');
                stage.style.cssText = `width:${stageWidth};margin:0 0 8px`;
                const el = document.createElement('swiper-container');
                el.setAttribute('init', 'false');
                for (let i = 0; i < c.count; i++) {
                    const slide = document.createElement('swiper-slide');
                    slide.textContent = `${c.name}: ${i}`;
                    slide.style.cssText = `height:60px;background:#dde;${c.slideWidth ? `width:${c.slideWidth}` : ''}`;
                    el.appendChild(slide);
                }
                stage.appendChild(el);
                document.body.prepend(stage);
                if (plainSwiper) {
                    // Upstream Swiper alone: no wrapper code initializes, moves or corrects this slider.
                    Object.assign(el, { cssMode: true, spaceBetween: 16, ...c.options });
                    el.initialize();
                    if (c.options.loop) el.swiper.slideToLoop(c.index, 0); else el.swiper.slideTo(c.index, 0);
                } else {
                    await interop.initialize(el, { cssMode: true, spaceBetween: 16, ...c.options }, null, [], 0, false);
                    // Swiper's own navigation - a swipe, a bullet, an arrow - goes through its slideTo, not the host's.
                    if (c.bySwiper) el.swiper.slideTo(c.index, 0); else interop.slideTo(el, c.index, 0);
                }
                window.e2eSliders.push({ ...c, stage, el });
            }
            window.scrollTo(0, 0);
            window.e2eReadSliders = () => window.e2eSliders.map(({ name, el, index, end }) => {
                const s = el.swiper;
                const position = s.params.cssMode ? s.wrapperEl.scrollLeft : -s.translate;
                const max = s.params.cssMode ? s.wrapperEl.scrollWidth - s.wrapperEl.clientWidth : -s.maxTranslate();
                const active = s.slides[s.activeIndex];
                const expected = end
                    ? max
                    : s.params.cssMode && s.params.centeredSlides
                        ? active.offsetLeft + (active.offsetWidth / 2) - (s.wrapperEl.clientWidth / 2)
                        : s.snapGrid[s.snapIndex];
                const isOnSlide = end
                    ? s.isEnd && Math.abs(position - max) <= 2
                    : s.realIndex === index && Math.abs(position - expected) <= 2;
                return { name, isOnSlide, realIndex: s.realIndex, expectedIndex: index, position: Math.round(position), expected: Math.round(expected), isEnd: s.isEnd, width: el.offsetWidth };
            });
            await wait(1000);
            return JSON.stringify({ sliders: window.e2eReadSliders() });");
    }

    private async Task<JsonElement> ReadSlidersAsync(string phase)
    {
        var json = await fixture.Page.EvaluateAsync<string>("async () => { await new Promise(r => setTimeout(r, 1500)); return JSON.stringify(window.e2eReadSliders()); }");
        output.WriteLine($"{phase}: {json}");
        return JsonDocument.Parse(json).RootElement;
    }

    /// <remarks>
    /// <paramref name="plainSwiper"/> runs the same sliders on Swiper alone, with no wrapper code at all, so a
    /// failure says straight away whether it is the wrapper's or Swiper's.
    /// </remarks>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rotation_ToLandscapeAndBack_KeepsEverySliderOnItsSlide(bool plainSwiper)
    {
        Assert.SkipUnless(DeviceInput.IsActive, NeedsAPhone);

        await fixture.NavigateToStoryAsync(CssModeStory, CssModeState);
        await BuildSlidersAsync(plainSwiper, "100%");

        var before = await ReadSlidersAsync("portrait, before");
        JsonElement landscape;
        JsonElement portrait;
        try
        {
            await DeviceInput.RotateAsync(landscape: true);
            await fixture.Page.WaitForFunctionAsync("() => innerWidth > innerHeight", null, new() { Timeout = 15_000 });
            landscape = await ReadSlidersAsync("landscape");
            await DeviceInput.RotateAsync(landscape: false);
            await fixture.Page.WaitForFunctionAsync("() => innerWidth < innerHeight", null, new() { Timeout = 15_000 });
            portrait = await ReadSlidersAsync("portrait, after");
        }
        finally
        {
            await DeviceInput.RestoreRotationAsync();
        }

        var failures = new[] { ("before", before), ("landscape", landscape), ("portrait", portrait) }
            .SelectMany(phase => phase.Item2.EnumerateArray()
                .Where(s => !s.GetProperty("isOnSlide").GetBoolean())
                .Select(s => $"{phase.Item1}: {s}"))
            .ToArray();
        Assert.True(failures.Length == 0, string.Join(Environment.NewLine, failures));
        fixture.AssertNoJsErrors();
    }
}

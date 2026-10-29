using Microsoft.Playwright;
using Xunit;

namespace Blazor.Swiper.E2E;

/// <summary>
/// That the typed options actually arrive at Swiper.
/// </summary>
/// <remarks>
/// A serialized option passes through three hands before it means anything: the interop cleans it,
/// Swiper Element parses it onto the instance, and Swiper merges it with its own defaults. bUnit
/// sees only the first of those - it never runs the interop at all - so asking the live Swiper what
/// it ended up with is the only assertion that covers the whole path.
/// </remarks>
[Collection(DemoCollectionDefinition.Name)]
public sealed class SwiperOptionsTests(DemoFixture fixture)
{
    [Fact]
    public async Task SlidesPerViewAuto_TheStringUnion_SurvivesAsAString()
    {
        // Arrange - `number | 'auto'` is the union a plain double could not express, so this is the
        // one that proves SwiperSlidesPerView serializes both ways.
        await fixture.NavigateToStoryAsync("components-swiper--auto-slides-per-view");

        // Act
        var slidesPerView = await fixture.ReadParameterAsync<string>("auto-per-view-swiper", "slidesPerView");

        // Assert
        Assert.Equal("auto", slidesPerView);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task SlidesPerGroup_NextButton_AdvancesByTheWholeGroup()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper--slides-per-group", "group-state");

        // Act - the navigation arrows live in the element's shadow DOM, which Playwright pierces.
        await canvas.GetByTestId("group-swiper").Locator("css=.swiper-button-next").ClickAsync();

        // Assert - a group of three means one press travels three slides, not one.
        var state = await StoryState.WaitForAsync(
            canvas,
            "group-state",
            candidate => StoryState.Int(candidate, "activeIndex") > 0,
            "the slider to advance");

        Assert.Equal(3, StoryState.Int(state, "activeIndex"));
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task CenteredSlides_Configured_ReachesSwiper()
    {
        // Arrange
        await fixture.NavigateToStoryAsync("components-swiper--centered-slides");

        // Act & Assert
        Assert.True(await fixture.ReadParameterAsync<bool>("centered-swiper", "centeredSlides"));
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task FreeMode_NestedModuleOptions_ArriveAsAnObjectRatherThanAFlag()
    {
        // Arrange - the nested option objects are the shape the deep null-cleaning exists for: an
        // unset member of freeMode would otherwise blank Swiper's default for it.
        await fixture.NavigateToStoryAsync("components-swiper--free-mode");

        // Act
        var isEnabled = await fixture.ReadParameterAsync<bool>("free-mode-swiper", "freeMode.enabled");
        var momentumRatio = await fixture.ReadParameterAsync<double>("free-mode-swiper", "freeMode.momentumRatio");
        var momentumBounce = await fixture.ReadParameterAsync<bool>("free-mode-swiper", "freeMode.momentumBounce");

        // Assert - the first two were set, the third was left unset and must still be Swiper's own default.
        Assert.True(isEnabled);
        Assert.Equal(1.5, momentumRatio);
        Assert.True(momentumBounce);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task Scrollbar_Draggable_ReachesSwiperAndRendersTheBar()
    {
        // Arrange
        await fixture.NavigateToStoryAsync("components-swiper--scrollbar");

        // Act
        var isDraggable = await fixture.ReadParameterAsync<bool>("scrollbar-swiper", "scrollbar.draggable");
        var hasBar = await fixture.ReadSwiperAsync<bool>("scrollbar-swiper", "!!s.scrollbar.el");

        // Assert
        Assert.True(isDraggable);
        Assert.True(hasBar, "Swiper Element should have created the scrollbar for a truthy scrollbar option.");
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task Rewind_NextPastTheLastSlide_ReturnsToTheFirst()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper--rewind", "rewind-state");
        var next = canvas.GetByTestId("rewind-next");

        // Act - three slides, so the third press is the one that wraps.
        await next.ClickAsync();
        await StoryState.WaitForAsync(canvas, "rewind-state", s => StoryState.Int(s, "activeIndex") == 1, "slide 1");
        await next.ClickAsync();
        await StoryState.WaitForAsync(canvas, "rewind-state", s => StoryState.Int(s, "activeIndex") == 2, "slide 2");
        await next.ClickAsync();

        // Assert
        await StoryState.WaitForAsync(canvas, "rewind-state", s => StoryState.Int(s, "activeIndex") == 0, "the rewind back to slide 0");
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task Breakpoints_ViewportResized_SwapsTheParametersInForce()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper--breakpoints", "breakpoint-state");

        // Act - the breakpoint keys are window widths, so the viewport is what moves them. The suite's
        // default viewport already sits above the widest key, so the first move has to be downwards or
        // nothing crosses a boundary at all.
        await fixture.Page.SetViewportSizeAsync(420, 900);
        var narrow = await WaitForSlidesPerViewAsync(1);

        await fixture.Page.SetViewportSizeAsync(1200, 900);
        var wide = await WaitForSlidesPerViewAsync(3);

        // Assert
        Assert.Equal(1, narrow);
        Assert.Equal(3, wide);

        var state = await StoryState.ReadAsync(canvas, "breakpoint-state");
        Assert.Equal("800", StoryState.EventValue(state, "breakpoint"));
        fixture.AssertNoJsErrors();

        await fixture.Page.SetViewportSizeAsync(1280, 1000);
    }

    private async Task<double> WaitForSlidesPerViewAsync(double expected)
    {
        await fixture.Page.WaitForFunctionAsync(
            $"() => document.querySelector('[data-testid=\"breakpoint-swiper\"]').swiper.params.slidesPerView === {expected}",
            null,
            new PageWaitForFunctionOptions { Timeout = 10_000 });

        return await fixture.ReadParameterAsync<double>("breakpoint-swiper", "slidesPerView");
    }

    [Fact]
    public async Task Grid_Rows_ReachSwiperAsANestedObject()
    {
        // Arrange
        await fixture.NavigateToStoryAsync("components-swiper-modules--grid");

        // Act
        var rows = await fixture.ReadParameterAsync<int>("grid-swiper", "grid.rows");
        var fill = await fixture.ReadParameterAsync<string>("grid-swiper", "grid.fill");

        // Assert - fill is an enum on the C# side and a lowercase string to Swiper.
        Assert.Equal(3, rows);
        Assert.Equal("row", fill);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task RightToLeft_DirAttribute_PutsSwiperInRtlMode()
    {
        // Arrange
        await fixture.NavigateToStoryAsync("components-swiper--right-to-left");

        // Act & Assert - Swiper reads the direction off the document rather than from a parameter.
        Assert.True(await fixture.ReadSwiperAsync<bool>("rtl-swiper", "s.rtl"));
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task WatchOverflow_SlidesThatAllFit_LocksTheSlider()
    {
        // Arrange - two slides in a two-slide view, so there is nothing to slide.
        var canvas = await fixture.NavigateToStoryAsync("components-swiper--watch-overflow", "lock-state");

        // Act & Assert
        Assert.True(await fixture.ReadSwiperAsync<bool>("lock-swiper", "s.isLocked"));

        await canvas.GetByTestId("lock-toggle").ClickAsync();

        await StoryState.WaitForAsync(
            canvas,
            "lock-state",
            state => StoryState.EventCount(state, "unlock") > 0,
            "the slider to unlock once there are more slides than fit");

        Assert.False(await fixture.ReadSwiperAsync<bool>("lock-swiper", "s.isLocked"));
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task PaginationFraction_WithAMinimumWidth_IsZeroPadded()
    {
        // Arrange - this is the whole point of the declarative templates: Swiper's formatFraction
        // hooks are JS functions called synchronously during render, which a C# delegate cannot be.
        var canvas = await fixture.NavigateToStoryAsync("components-swiper-modules--pagination-types");

        // Act
        var fraction = await canvas.GetByTestId("fraction-swiper").Locator("css=.swiper-pagination").InnerTextAsync();

        // Assert
        Assert.Equal("01 / 05", fraction.Trim());
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task PaginationBullets_WithARenderTemplate_AreNumbered()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper-modules--pagination-templates");

        // Act - the template is turned into Swiper's renderBullet function on the JavaScript side.
        var firstBullet = canvas.GetByTestId("numbered-bullets-swiper").Locator("css=.swiper-pagination-bullet").First;

        // Assert
        Assert.Equal("1", (await firstBullet.InnerTextAsync()).Trim());
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task PaginationProgressbar_Configured_RendersAFillRatherThanBullets()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper-modules--pagination-types");

        // Act
        var fills = await canvas.GetByTestId("progressbar-swiper").Locator("css=.swiper-pagination-progressbar-fill").CountAsync();

        // Assert
        Assert.Equal(1, fills);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task Accessibility_ConfiguredMessages_ReachTheRenderedSlider()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper-modules--accessibility");

        // Act - the a11y module writes onto swiper.el, which for Swiper Element is the div inside the
        // shadow root rather than the host element the test can see.
        var label = await fixture.ReadSwiperAsync<string>("a11y-swiper", "s.el.getAttribute('aria-label') ?? ''");
        var slideLabel = await canvas.GetByTestId("a11y-swiper").Locator("css=swiper-slide").First.GetAttributeAsync("aria-label");

        // Assert - the template placeholders are Swiper's own, filled in as it renders.
        Assert.Equal("Product gallery", label);
        Assert.Equal("Product 1 of 4", slideLabel);
        Assert.False(await fixture.ReadParameterAsync<bool>("a11y-swiper", "a11y.scrollOnFocus"));
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task Autoplay_NestedOptions_ReachSwiper()
    {
        // Arrange
        await fixture.NavigateToStoryAsync("components-swiper-modules--autoplay", "autoplay-state");

        // Act
        var delay = await fixture.ReadParameterAsync<int>("autoplay-swiper", "autoplay.delay");
        var disableOnInteraction = await fixture.ReadParameterAsync<bool>("autoplay-swiper", "autoplay.disableOnInteraction");

        // Assert
        Assert.Equal(2000, delay);
        Assert.False(disableOnInteraction);
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task InjectStyles_SuppliedInOptions_ReachTheElementsShadowDom()
    {
        // Arrange - the shadow DOM is unreachable from a page stylesheet, so this is the only route
        // to the pagination bullets, and the numbered-bullet story depends on it.
        await fixture.NavigateToStoryAsync("components-swiper-modules--pagination-templates");

        // Act
        // Chrome takes the constructable-stylesheet path, so the CSS is in adoptedStyleSheets rather
        // than in a <style> element - both are checked, since which one is used is the browser's call.
        var hasInjectedStyle = await fixture.Page.EvaluateAsync<bool>(
            """
            () => {
                const root = document.querySelector('[data-testid="numbered-bullets-swiper"]').shadowRoot;
                const inStyleElements = Array.from(root.querySelectorAll('style'))
                    .some(s => s.textContent.includes('demo-numbered-bullet'));
                const inAdoptedSheets = Array.from(root.adoptedStyleSheets ?? [])
                    .some(sheet => Array.from(sheet.cssRules).some(rule => rule.cssText.includes('demo-numbered-bullet')));
                return inStyleElements || inAdoptedSheets;
            }
            """);

        // Assert
        Assert.True(hasInjectedStyle, "InjectStyles did not reach the element's shadow DOM.");
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task AutoHeight_SlidesOfDifferentHeights_ResizeTheTrack()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper--auto-height", "auto-height-state");

        // Act
        var firstHeight = await fixture.ReadSwiperAsync<double>("auto-height-swiper", "s.wrapperEl.offsetHeight");
        await SwipeHelper.SwipeLeftAsync(fixture.Page, canvas.GetByTestId("auto-height-swiper"));
        await StoryState.WaitForAsync(canvas, "auto-height-state", s => StoryState.Int(s, "activeIndex") == 1, "the slider to advance");

        // Assert - the second slide is taller, so the track has to have grown.
        var secondHeight = await fixture.ReadSwiperAsync<double>("auto-height-swiper", "s.wrapperEl.offsetHeight");
        Assert.True(secondHeight > firstHeight, $"The track did not follow the slide height: {firstHeight} then {secondHeight}.");
        fixture.AssertNoJsErrors();
    }

    /// <summary>
    /// The distinction between the two settings that both look like "cannot go back": this one really
    /// cannot, by any route at all.
    /// </summary>
    [Fact]
    public async Task ForwardOnly_SlidePrev_DoesNothingBecauseBackwardsIsGoneEntirely()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper--forward-only", "forward-only-state");

        await canvas.GetByTestId("forward-only-next").ClickAsync();
        await StoryState.WaitForAsync(canvas, "forward-only-state", s => StoryState.Int(s, "activeIndex") == 1, "slide 1");

        // Act
        await canvas.GetByTestId("forward-only-prev").ClickAsync();
        await fixture.Page.WaitForTimeoutAsync(600);

        // Assert - AllowSlidePrev false is honoured by the API, not only by the gesture.
        Assert.Equal(1, await fixture.ReadSwiperAsync<int>("forward-only-swiper", "s.realIndex"));
        fixture.AssertNoJsErrors();
    }

    /// <summary>
    /// ...and the other one only reinterprets a drag, leaving backwards perfectly available.
    /// </summary>
    [Fact]
    public async Task OneWayMovement_SlidePrev_StillGoesBack()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper--one-way-movement", "one-way-state");

        await canvas.GetByTestId("one-way-next").ClickAsync();
        await StoryState.WaitForAsync(canvas, "one-way-state", s => StoryState.Int(s, "activeIndex") == 1, "slide 1");

        // Act
        await canvas.GetByTestId("one-way-prev").ClickAsync();

        // Assert
        await StoryState.WaitForAsync(
            canvas,
            "one-way-state",
            state => StoryState.Int(state, "activeIndex") == 0,
            "the slider to go back, which is what tells this apart from Forward only");

        Assert.True(await fixture.ReadParameterAsync<bool>("one-way-swiper", "allowSlidePrev"));
        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task Mousewheel_OrdinaryVerticalWheel_MovesTheSlider()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper-modules--mousewheel", "mousewheel-state");
        var slider = canvas.GetByTestId("mousewheel-swiper");
        var box = await slider.BoundingBoxAsync() ?? throw new InvalidOperationException("The slider has no layout box.");

        // Act - a real wheel over the slider, which is the interaction the story is about.
        await fixture.Page.Mouse.MoveAsync(box.X + (box.Width / 2), box.Y + (box.Height / 2));
        await fixture.Page.Mouse.WheelAsync(0, 200);

        // Assert
        await StoryState.WaitForAsync(
            canvas,
            "mousewheel-state",
            state => StoryState.Int(state, "activeIndex") >= 1,
            "the wheel to advance the slider");

        fixture.AssertNoJsErrors();
    }

    /// <summary>
    /// ForceToAxis is the reason a mousewheel story can look broken: on a horizontal slider it makes an
    /// ordinary vertical wheel pass straight through to the page, on purpose.
    /// </summary>
    [Fact]
    public async Task Mousewheel_ForceToAxis_IgnoresAVerticalWheelOnAHorizontalSlider()
    {
        // Arrange
        var canvas = await fixture.NavigateToStoryAsync("components-swiper-modules--mousewheel", "mousewheel-axis-state");
        var slider = canvas.GetByTestId("mousewheel-axis-swiper");
        var box = await slider.BoundingBoxAsync() ?? throw new InvalidOperationException("The slider has no layout box.");

        // Act
        await fixture.Page.Mouse.MoveAsync(box.X + (box.Width / 2), box.Y + (box.Height / 2));
        await fixture.Page.Mouse.WheelAsync(0, 200);
        await fixture.Page.WaitForTimeoutAsync(600);

        // Assert - unmoved by the vertical wheel...
        Assert.Equal(0, await fixture.ReadSwiperAsync<int>("mousewheel-axis-swiper", "s.realIndex"));

        // ...but a wheel along its own axis does move it, so the module is wired and merely selective.
        await fixture.Page.Mouse.WheelAsync(200, 0);
        await StoryState.WaitForAsync(
            canvas,
            "mousewheel-axis-state",
            state => StoryState.Int(state, "activeIndex") >= 1,
            "a horizontal wheel to advance the axis-restricted slider");

        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task CssMode_ProgrammaticMove_ArrivesDespiteScrollSnap()
    {
        // Arrange - Swiper drives a cssMode move with a native smooth scroll, which scroll-snap
        // cancels outright. The wrapper animates the scroll itself for exactly this reason.
        var canvas = await fixture.NavigateToStoryAsync("components-swiper--css-mode", "css-mode-state");

        // Act
        await canvas.GetByTestId("css-mode-to-third").ClickAsync();

        // Assert
        await StoryState.WaitForAsync(
            canvas,
            "css-mode-state",
            state => StoryState.Int(state, "activeIndex") == 2,
            "the cssMode slider to reach slide 2");

        fixture.AssertNoJsErrors();
    }

    [Fact]
    public async Task AttachEvents_WhileTheyAreAttached_KeepsOneSetOfListeners()
    {
        // Arrange - Swiper binds a new set of listeners on every attach, and a slider sized by its slides relays
        // out on every image that loads
        await fixture.NavigateToStoryAsync("components-swiper--auto-slides-per-view");

        // Act
        var updates = await fixture.Page.EvaluateAsync<int[]>(
            """
            async () => {
                const interop = await import(new URL('./_content/Kebechet.Blazor.Swiper/swiper-interop.js', document.baseURI).href);
                const stage = document.createElement('div');
                stage.style.width = '420px';
                document.body.appendChild(stage);
                const el = document.createElement('swiper-container');
                el.setAttribute('init', 'false');
                const slide = document.createElement('swiper-slide');
                slide.style.width = '150px';
                const image = document.createElement('img');
                image.loading = 'lazy';
                slide.appendChild(image);
                el.appendChild(slide);
                stage.appendChild(el);
                await interop.initialize(el, { slidesPerView: 'auto', lazyPreload: true }, null, [], 0, false);
                let count = 0;
                const swiperUpdate = el.swiper.update;
                el.swiper.update = function (...args) {
                    count++;
                    return swiperUpdate.apply(this, args);
                };
                const updates = [];
                const load = () => {
                    count = 0;
                    image.dispatchEvent(new Event('load'));
                    updates.push(count);
                };
                interop.attachEvents(el);
                load();
                interop.detachEvents(el);
                load();
                interop.attachEvents(el);
                load();
                stage.remove();
                return updates;
            }
            """);

        // Assert - one relayout per load while attached, and none once detached
        Assert.Equal([1, 0, 1], updates);
        fixture.AssertNoJsErrors();
    }
}

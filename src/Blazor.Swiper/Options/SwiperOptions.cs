namespace Kebechet.Blazor.Swiper;

/// <summary>
/// Swiper's parameters, mirrored one for one.
/// </summary>
/// <remarks>
/// <para>
/// Every member is nullable and unset by default, and an unset member is dropped before the options
/// reach Swiper - so Swiper's own defaults are the only defaults, and this type never has to restate
/// them. <c>false</c>, <c>0</c> and <c>""</c> are legitimate Swiper settings and do survive; only
/// null is absence. The one deliberate deviation is <see cref="Observer"/>, documented on itself.
/// </para>
/// <para>
/// Property names are the PascalCase of Swiper's own parameter names and are camel-cased back by the
/// interop serializer, so <see href="https://swiperjs.com/swiper-api#parameters">Swiper's parameter
/// reference</see> reads directly onto this type.
/// </para>
/// <para>
/// This is a record, so a change is a <c>with</c> expression - and the component pushes changed
/// members to a live slider for the parameters Swiper can update after init.
/// </para>
/// </remarks>
public sealed record SwiperOptions
{
    /// <summary>
    /// Whether the slider responds to anything at all. Disabled hides the navigation elements and
    /// ignores every interaction. Null = Swiper's default (true).
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>CSS injected into the slider's shadow DOM, which is the only way to reach its internals.</summary>
    public string[]? InjectStyles { get; set; }

    /// <summary>URLs of stylesheets linked into the slider's shadow DOM.</summary>
    public string[]? InjectStylesUrls { get; set; }

    /// <summary>Slider axis. Null = Swiper's default (horizontal).</summary>
    public SwiperDirection? Direction { get; set; }

    /// <summary>Slide shown first. Null = Swiper's default (0).</summary>
    public int? InitialSlide { get; set; }

    /// <summary>Transition duration in ms. Null = Swiper's default (300).</summary>
    public int? Speed { get; set; }

    /// <summary>Forces the slider's width in px. Useful when initializing while hidden, or when prerendering.</summary>
    public double? Width { get; set; }

    /// <summary>Forces the slider's height in px. Useful when initializing while hidden, or when prerendering.</summary>
    public double? Height { get; set; }

    /// <summary>Track height follows the active slide's content height. Null = Swiper's default (false).</summary>
    /// <remarks>
    /// The wrapper keeps this measurement live: Swiper measures once at init, so content arriving
    /// later - an image decoding, an async load resolving - would otherwise leave the track pinned to
    /// the height it saw first.
    /// </remarks>
    public bool? AutoHeight { get; set; }

    /// <summary>Round slide sizes to whole pixels, which stops text blurring on low-DPI screens. Null = Swiper's default (false).</summary>
    public bool? RoundLengths { get; set; }

    /// <summary>Set an explicit size on the slides wrapper, as a fallback for poor flexbox support. Null = Swiper's default (false).</summary>
    public bool? SetWrapperSize { get; set; }

    /// <summary>Run everything but the actual movement, so a custom transition can own the transform. Null = Swiper's default (false).</summary>
    public bool? VirtualTranslate { get; set; }

    /// <summary>Set on a slider inside another slider of the same direction, so touches are routed correctly. Null = Swiper's default (false).</summary>
    public bool? Nested { get; set; }

    /// <summary>Transition effect. Each one reads its own options record on this type. Null = Swiper's default (slide).</summary>
    public SwiperEffect? Effect { get; set; }

    /// <summary>Gap between slides, in px or as a percentage string. Null = Swiper's default (0).</summary>
    public SwiperLength? SpaceBetween { get; set; }

    /// <summary>
    /// Slides visible at once - a count, fractional allowed, or
    /// <see cref="SwiperSlidesPerView.Auto"/> to size each slide from its own content.
    /// Null = Swiper's default (1).
    /// </summary>
    public SwiperSlidesPerView? SlidesPerView { get; set; }

    /// <summary>How many slides a single move advances. Null = Swiper's default (1).</summary>
    public int? SlidesPerGroup { get; set; }

    /// <summary>Leading slides excluded from grouping, so the first few advance one at a time. Null = Swiper's default (0).</summary>
    public int? SlidesPerGroupSkip { get; set; }

    /// <summary>With <see cref="SwiperSlidesPerView.Auto"/>, advance past every slide currently in view. Null = Swiper's default (false).</summary>
    public bool? SlidesPerGroupAuto { get; set; }

    /// <summary>Center the active slide instead of aligning it to the start. Null = Swiper's default (false).</summary>
    public bool? CenteredSlides { get; set; }

    /// <summary>Center the active slide without leaving gaps at either end. Needs <see cref="CenteredSlides"/>. Null = Swiper's default (false).</summary>
    public bool? CenteredSlidesBounds { get; set; }

    /// <summary>Extra space before the first slide, in px. Null = Swiper's default (0).</summary>
    public double? SlidesOffsetBefore { get; set; }

    /// <summary>Extra space after the last slide, in px. Null = Swiper's default (0).</summary>
    public double? SlidesOffsetAfter { get; set; }

    /// <summary>Normalize the reported slide index when slides are grouped. Null = Swiper's default (true).</summary>
    public bool? NormalizeSlideIndex { get; set; }

    /// <summary>Center the slides when there are fewer than <see cref="SlidesPerView"/> of them. Null = Swiper's default (false).</summary>
    public bool? CenterInsufficientSlides { get; set; }

    /// <summary>Always settle on a slide edge rather than an arbitrary offset. Only bites with a fractional or auto <see cref="SlidesPerView"/>. Null = Swiper's default (false).</summary>
    public bool? SnapToSlideEdge { get; set; }

    /// <summary>Slide count below which Swiper hides slide backfaces, which reduces flicker in Safari. Null = Swiper's default (10).</summary>
    public int? MaxBackfaceHiddenSlides { get; set; }

    /// <summary>Disable the slider and hide its navigation when the slides all fit. Null = Swiper's default (true).</summary>
    public bool? WatchOverflow { get; set; }

    /// <summary>Track each slide's progress and visibility, which the visibility classes and several effects need. Null = Swiper's default (false).</summary>
    public bool? WatchSlidesProgress { get; set; }

    /// <summary>
    /// Reinterpret a backwards drag as a forward one, so the slider advances whichever way the user
    /// pulls. Null = Swiper's default (false).
    /// </summary>
    /// <remarks>
    /// Not the same as <see cref="AllowSlidePrev"/> <c>= false</c>, though both read as "cannot go
    /// back" from a swipe alone. This one changes only what a <em>drag</em> means: backwards is
    /// otherwise untouched, so <see cref="Swiper.SlidePrev"/>, the navigation arrows and the
    /// pagination all still go back. Setting <see cref="AllowSlidePrev"/> to false removes backwards
    /// outright, from the gesture and from the API alike.
    /// </remarks>
    public bool? OneWayMovement { get; set; }

    /// <summary>
    /// Use the browser's native CSS Scroll Snap API instead of JS transforms, which moves the slider
    /// on the compositor thread and is dramatically smoother for heavy or tall slides.
    /// Null = Swiper's default (false).
    /// </summary>
    /// <remarks>
    /// Swiper's own trade-offs apply: mouse-drag does not work (wheel, trackpad and touch do, exactly
    /// like a native scroller), <see cref="Speed"/> is ignored, transition start/end events do not
    /// fire, and it is not compatible with <see cref="Loop"/>. The wrapper animates programmatic
    /// moves itself here, because Swiper drives them with a native smooth scroll that scroll-snap
    /// cancels.
    /// </remarks>
    public bool? CssMode { get; set; }

    /// <summary>Re-measure on window resize and orientation change. Null = Swiper's default (true).</summary>
    public bool? UpdateOnWindowResize { get; set; }

    /// <summary>
    /// Whether Swiper watches its own element for size changes and re-measures.
    /// Null = Swiper's default (true).
    /// </summary>
    /// <remarks>
    /// Swiper's resize handling finishes by re-anchoring onto the index it currently holds, deferred
    /// by a frame - which is destructive when a programmatic move is already in flight, because the
    /// re-anchor carries the pre-move index and lands after it. Set false when the slides resize as a
    /// matter of course and your code drives the position. Window resizes still re-measure; only the
    /// per-element observer is dropped.
    /// </remarks>
    public bool? ResizeObserver { get; set; }

    /// <summary>Show the "grab" cursor over the slider. Null = Swiper's default (false).</summary>
    public bool? GrabCursor { get; set; }

    /// <summary>Which element touch events are listened for on. Null = Swiper's default (wrapper).</summary>
    public SwiperTouchEventsTarget? TouchEventsTarget { get; set; }

    /// <summary>Multiplier between finger movement and slider movement. Null = Swiper's default (1).</summary>
    public double? TouchRatio { get; set; }

    /// <summary>Angle from the slider's axis, in degrees, within which a drag counts as a swipe. Null = Swiper's default (45).</summary>
    public double? TouchAngle { get; set; }

    /// <summary>Treat mouse drags as swipes. Null = Swiper's default (true).</summary>
    public bool? SimulateTouch { get; set; }

    /// <summary>Allow a short flick to change slide. Null = Swiper's default (true).</summary>
    public bool? ShortSwipes { get; set; }

    /// <summary>Allow a slow drag past the threshold to change slide. Null = Swiper's default (true).</summary>
    public bool? LongSwipes { get; set; }

    /// <summary>Fraction of a slide a long swipe must cover to commit. Null = Swiper's default (0.5).</summary>
    public double? LongSwipesRatio { get; set; }

    /// <summary>Minimum duration of a long swipe, in ms. Null = Swiper's default (300).</summary>
    public int? LongSwipesMs { get; set; }

    /// <summary>Move the slider while the finger is down rather than only on release. Null = Swiper's default (true).</summary>
    public bool? FollowFinger { get; set; }

    /// <summary>Whether the slider can be swiped at all. Null = Swiper's default (true).</summary>
    public bool? AllowTouchMove { get; set; }

    /// <summary>Movement in px below which a drag is ignored. Null = Swiper's default (5).</summary>
    public double? Threshold { get; set; }

    /// <summary>Call <c>preventDefault</c> on pointerdown. Null = Swiper's default (true).</summary>
    public bool? TouchStartPreventDefault { get; set; }

    /// <summary>Call <c>preventDefault</c> on touchstart even when Swiper would not. Null = Swiper's default (false).</summary>
    public bool? TouchStartForcePreventDefault { get; set; }

    /// <summary>Stop touchmove from propagating. Null = Swiper's default (false).</summary>
    public bool? TouchMoveStopPropagation { get; set; }

    /// <summary>How swipes starting at the screen edge are treated, so system swipe-back keeps working. Null = Swiper's default (disabled).</summary>
    public SwiperEdgeSwipeDetection? EdgeSwipeDetection { get; set; }

    /// <summary>Width of that edge area, in px. Null = Swiper's default (20).</summary>
    public double? EdgeSwipeThreshold { get; set; }

    /// <summary>Release touch events at the slider's ends so the page can scroll on. Null = Swiper's default (false).</summary>
    public bool? TouchReleaseOnEdges { get; set; }

    /// <summary>Use passive listeners where possible. Turn off when you need <c>preventDefault</c>. Null = Swiper's default (true).</summary>
    public bool? PassiveListeners { get; set; }

    /// <summary>Resist dragging past the first and last slide. Null = Swiper's default (true).</summary>
    public bool? Resistance { get; set; }

    /// <summary>How much of that overdrag gets through. Null = Swiper's default (0.85).</summary>
    public double? ResistanceRatio { get; set; }

    /// <summary>Ignore swipes and control clicks while a transition runs. Null = Swiper's default (false).</summary>
    public bool? PreventInteractionOnTransition { get; set; }

    /// <summary>
    /// Allow moving to the previous slide. Toggle at runtime via
    /// <see cref="Swiper.SetAllowSlidePrev"/>. Null = Swiper's default (true).
    /// </summary>
    /// <remarks>
    /// False removes backwards <em>entirely</em>: the swipe, the navigation arrows and the API all
    /// stop going back, so even <see cref="Swiper.SlidePrev"/> does nothing. That is the difference
    /// from <see cref="OneWayMovement"/>, which leaves backwards available everywhere except the
    /// drag. Corrections made by <see cref="Swiper.ArmAnchor"/> and
    /// <see cref="Swiper.UpdateAndAnchor"/> ignore this, since a correction is not navigation.
    /// </remarks>
    public bool? AllowSlidePrev { get; set; }

    /// <summary>Allow moving to the next slide. Toggle at runtime via <see cref="Swiper.SetAllowSlideNext"/>. Null = Swiper's default (true).</summary>
    public bool? AllowSlideNext { get; set; }

    /// <summary>Honour <see cref="NoSwipingClass"/> and <see cref="NoSwipingSelector"/>. Null = Swiper's default (true).</summary>
    public bool? NoSwiping { get; set; }

    /// <summary>Elements with this class cannot start a swipe. Null = Swiper's default (<c>swiper-no-swiping</c>).</summary>
    public string? NoSwipingClass { get; set; }

    /// <summary>CSS selector for elements that cannot start a swipe, used instead of <see cref="NoSwipingClass"/>.</summary>
    public string? NoSwipingSelector { get; set; }

    /// <summary>CSS selector for the only element a swipe may start on.</summary>
    public string? SwipeHandler { get; set; }

    /// <summary>Suppress clicks that land at the end of a swipe. Null = Swiper's default (true).</summary>
    public bool? PreventClicks { get; set; }

    /// <summary>Stop those clicks propagating. Null = Swiper's default (true).</summary>
    public bool? PreventClicksPropagation { get; set; }

    /// <summary>Clicking a slide moves to it. Null = Swiper's default (false).</summary>
    public bool? SlideToClickedSlide { get; set; }

    /// <summary>CSS selector for elements that keep their own interaction rather than starting a swipe. Null = Swiper's default (form controls).</summary>
    public string? FocusableElements { get; set; }

    /// <summary>Continuous loop mode. Null = Swiper's default (false).</summary>
    /// <remarks>
    /// Swiper duplicates slides to loop, so its internal index counts positions your collection does
    /// not have. Everything this wrapper reports and accepts is the logical index instead.
    /// </remarks>
    public bool? Loop { get; set; }

    /// <summary>Pad the loop with blank slides when the count does not divide evenly. Null = Swiper's default (true).</summary>
    public bool? LoopAddBlankSlides { get; set; }

    /// <summary>Extra duplicated slides kept on each side. Null = Swiper's default (0).</summary>
    public int? LoopAdditionalSlides { get; set; }

    /// <summary>Ignore next/prev while a loop transition runs. Null = Swiper's default (true).</summary>
    public bool? LoopPreventsSliding { get; set; }

    /// <summary>Jump from the last slide back to the first without the loop machinery. Null = Swiper's default (false).</summary>
    public bool? Rewind { get; set; }

    /// <summary>
    /// Parameter overrides per breakpoint, keyed by width in px (<c>"640"</c>) or by ratio
    /// (<c>"@1.5"</c>).
    /// </summary>
    /// <remarks>
    /// The value is a full <see cref="SwiperOptions"/> because Swiper accepts the same shape at a
    /// breakpoint. Not every parameter can change responsively - Swiper ignores the ones it cannot
    /// re-apply, such as <see cref="Loop"/> and <see cref="Effect"/>.
    /// </remarks>
    public Dictionary<string, SwiperOptions>? Breakpoints { get; set; }

    /// <summary>
    /// What the breakpoint keys measure: <see cref="SwiperBreakpointsBase.Window"/>,
    /// <see cref="SwiperBreakpointsBase.Container"/>, or a CSS selector. Null = Swiper's default (window).
    /// </summary>
    public string? BreakpointsBase { get; set; }

    /// <summary>
    /// Swiper's own <c>MutationObserver</c>, which calls <c>update()</c> on any DOM change inside the
    /// container.
    /// </summary>
    /// <remarks>
    /// This is the wrapper's one deliberate deviation from Swiper's defaults: Swiper Element turns it
    /// on, and leaving it unset here turns it off. With a framework re-rendering slide content - and
    /// especially with <see cref="AutoHeight"/>, whose height writes are themselves DOM mutations - it
    /// drives a costly update/height feedback loop. Call <see cref="Swiper.Update"/> explicitly
    /// instead, or set this to true to get Swiper's behaviour back.
    /// </remarks>
    public bool? Observer { get; set; }

    /// <summary>Also watch the slider's ancestors for mutations. Null = Swiper's default (false).</summary>
    public bool? ObserveParents { get; set; }

    /// <summary>Also watch inside the slides for mutations. Null = Swiper's default (false).</summary>
    public bool? ObserveSlideChildren { get; set; }

    /// <summary>User-agent string, so device detection works when prerendering on the server.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Page URL, so history navigation resolves the active slide when prerendering on the server.</summary>
    public string? Url { get; set; }

    /// <summary>Look for navigation elements inside the slider before searching the document. Null = Swiper's default (true).</summary>
    public bool? UniqueNavElements { get; set; }

    /// <summary>
    /// Whether Swiper raises transition and slide-change events during init.
    /// Null = Swiper's default (true).
    /// </summary>
    /// <remarks>
    /// The wrapper attaches its listeners after init either way, so this changes nothing for the
    /// component's own callbacks. It is here because Swiper's internals read it.
    /// </remarks>
    public bool? RunCallbacksOnInit { get; set; }

    /// <summary>Prefix of the modifier classes Swiper sets on the container.</summary>
    public string? ContainerModifierClass { get; set; }

    /// <summary>Class identifying a slide.</summary>
    public string? SlideClass { get; set; }

    /// <summary>Class set on the active slide.</summary>
    public string? SlideActiveClass { get; set; }

    /// <summary>Class set on a partially visible slide. Needs <see cref="WatchSlidesProgress"/>.</summary>
    public string? SlideVisibleClass { get; set; }

    /// <summary>Class set on a fully visible slide. Needs <see cref="WatchSlidesProgress"/>.</summary>
    public string? SlideFullyVisibleClass { get; set; }

    /// <summary>Class set on the blank slides loop mode adds.</summary>
    public string? SlideBlankClass { get; set; }

    /// <summary>Class set on the slide after the active one.</summary>
    public string? SlideNextClass { get; set; }

    /// <summary>Class set on the slide before the active one.</summary>
    public string? SlidePrevClass { get; set; }

    /// <summary>Class of the slides wrapper.</summary>
    public string? WrapperClass { get; set; }

    /// <summary>Class of the lazy-loading placeholder.</summary>
    public string? LazyPreloaderClass { get; set; }

    /// <summary>
    /// Let Swiper handle <c>loading="lazy"</c> images: remove their preloader on load, relayout
    /// for them, and preload the neighbours <see cref="LazyPreloadPrevNext"/> asks for. Turn it off
    /// when the images' loading is handled elsewhere. Swiper reads it only while it initializes, so changing
    /// it on a running slider has no effect. Null = Swiper's default (true).
    /// </summary>
    public bool? LazyPreload { get; set; }

    /// <summary>How many slides either side of the active one have their lazy images preloaded. Null = Swiper's default (0).</summary>
    public int? LazyPreloadPrevNext { get; set; }

    /// <summary>Accessibility wiring. Null = Swiper's defaults, which have a11y on.</summary>
    public SwiperA11yOptions? A11y { get; set; }

    /// <summary>Advance by itself. Null = off.</summary>
    public SwiperAutoplayOptions? Autoplay { get; set; }

    /// <summary>How a controlled slider follows this one. Set the slider itself on <see cref="Swiper.Controller"/>.</summary>
    public SwiperControllerOptions? Controller { get; set; }

    /// <summary>Prev/next arrows. Null = off.</summary>
    public SwiperNavigationOptions? Navigation { get; set; }

    /// <summary>Bullets, fraction or progress bar. Null = off.</summary>
    public SwiperPaginationOptions? Pagination { get; set; }

    /// <summary>Scrollbar. Null = off.</summary>
    public SwiperScrollbarOptions? Scrollbar { get; set; }

    /// <summary>Keyboard control. Null = off.</summary>
    public SwiperKeyboardOptions? Keyboard { get; set; }

    /// <summary>Mousewheel control. Null = off.</summary>
    public SwiperMousewheelOptions? Mousewheel { get; set; }

    /// <summary>Glide freely instead of snapping to slides. Null = off.</summary>
    public SwiperFreeModeOptions? FreeMode { get; set; }

    /// <summary>Several rows of slides. Null = a single row.</summary>
    public SwiperGridOptions? Grid { get; set; }

    /// <summary>Pinch and double-tap zoom. Null = off.</summary>
    public SwiperZoomOptions? Zoom { get; set; }

    /// <summary>Move elements inside the slides at their own rate. Null = off.</summary>
    public SwiperParallaxOptions? Parallax { get; set; }

    /// <summary>Mirror the active slide into the URL fragment. Null = off.</summary>
    public SwiperHashNavigationOptions? HashNavigation { get; set; }

    /// <summary>Mirror the active slide into the URL path. Null = off.</summary>
    public SwiperHistoryOptions? History { get; set; }

    /// <summary>Thumbnail strip behaviour. Set the strip itself on <see cref="Swiper.Thumbs"/>.</summary>
    public SwiperThumbsOptions? Thumbs { get; set; }

    /// <summary>Keep only nearby slides measured. Null = off. Read its remarks before reaching for it.</summary>
    public SwiperVirtualOptions? Virtual { get; set; }

    /// <summary>Options for <see cref="SwiperEffect.Fade"/>.</summary>
    public SwiperFadeEffectOptions? FadeEffect { get; set; }

    /// <summary>Options for <see cref="SwiperEffect.Cube"/>.</summary>
    public SwiperCubeEffectOptions? CubeEffect { get; set; }

    /// <summary>Options for <see cref="SwiperEffect.Flip"/>.</summary>
    public SwiperFlipEffectOptions? FlipEffect { get; set; }

    /// <summary>Options for <see cref="SwiperEffect.Coverflow"/>.</summary>
    public SwiperCoverflowEffectOptions? CoverflowEffect { get; set; }

    /// <summary>Options for <see cref="SwiperEffect.Creative"/>.</summary>
    public SwiperCreativeEffectOptions? CreativeEffect { get; set; }

    /// <summary>Options for <see cref="SwiperEffect.Cards"/>.</summary>
    public SwiperCardsEffectOptions? CardsEffect { get; set; }
}

[!["Buy Me A Coffee"](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/kebechet)

# Blazor.Swiper
[![NuGet Version](https://img.shields.io/nuget/v/Kebechet.Blazor.Swiper)](https://www.nuget.org/packages/Kebechet.Blazor.Swiper/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Kebechet.Blazor.Swiper)](https://www.nuget.org/packages/Kebechet.Blazor.Swiper/)
[![Build](https://github.com/Kebechet/Blazor.Swiper/actions/workflows/build.yml/badge.svg)](https://github.com/Kebechet/Blazor.Swiper/actions/workflows/build.yml)
[![codecov](https://codecov.io/gh/Kebechet/Blazor.Swiper/graph/badge.svg)](https://codecov.io/gh/Kebechet/Blazor.Swiper)
[![Storybook](https://img.shields.io/badge/storybook-live%20demo-ff4785)](https://kebechet.github.io/Blazor.Swiper/)
![Last updated](https://img.shields.io/github/last-commit/Kebechet/Blazor.Swiper/main?label=last%20updated)
[![Twitter](https://img.shields.io/twitter/url/https/twitter.com/samuel_sidor.svg?style=social&label=Follow%20samuel_sidor)](https://x.com/samuel_sidor)

A Blazor wrapper for [Swiper](https://swiperjs.com), built on the framework-agnostic [Swiper Element](https://swiperjs.com/element) web component. The Swiper bundle ships inside the package and is auto-registered on startup - no npm build step and no manual `<script>` tags.

**Every** Swiper parameter is typed, **every** Swiper event has a callback, and the callbacks are opt-in, so an event you don't wire up costs nothing.

**[Live storybook](https://kebechet.github.io/Blazor.Swiper/)** - an interactive story for every feature.

## Installation

```bash
dotnet add package Kebechet.Blazor.Swiper
```

## Usage

```razor
@using Kebechet.Blazor.Swiper

<Swiper Options="new() { Pagination = true, SpaceBetween = 16 }">
    <SwiperSlide>Slide 1</SwiperSlide>
    <SwiperSlide>Slide 2</SwiperSlide>
    <SwiperSlide>Slide 3</SwiperSlide>
</Swiper>
```

Any attribute you put on `<Swiper>` or `<SwiperSlide>` that isn't a parameter is forwarded to the underlying `<swiper-container>` / `<swiper-slide>` element, so `class`, `style` and `id` work as usual. Styling the shadow-DOM controls is done through Swiper's CSS parts (`::part(button-next)`) or [`InjectStyles`](#reaching-the-shadow-dom).

## Two-way binding

`@bind-ActiveIndex` is the whole API for the common case. Setting it moves the slider; the slider moving sets it.

```razor
<Swiper @bind-ActiveIndex="_step">
    @foreach (var page in _pages) { <SwiperSlide>@page</SwiperSlide> }
</Swiper>

<button @onclick="() => _step++">Next</button>
<p>Step @(_step + 1) of @_pages.Count</p>

@code {
    private int _step;
}
```

- The value is always the index into **your** collection. In `Loop` mode Swiper's own index counts duplicated slides that your collection doesn't have; a bound move routes through Swiper's loop-aware navigation so both directions agree.
- A bound change animates at `Options.Speed`. Call `SlideTo(i, 0)` for an instant jump.
- A value supplied *before* the slider initializes becomes the slide it opens on, so binding an index of 2 starts on the third slide rather than starting at 0 and jumping.

`@bind-IsAutoplayRunning` works the same way, and is what a play/pause button wants - autoplay also stops *itself*, on interaction and at the last slide, and the binding follows that.

```razor
<Swiper @bind-IsAutoplayRunning="_playing" Options="new() { Autoplay = new() { Delay = 3000 } }">...</Swiper>
<button @onclick="() => _playing = !_playing">@(_playing ? "Pause" : "Play")</button>
```

## Options

`SwiperOptions` mirrors [Swiper's parameters](https://swiperjs.com/swiper-api#parameters) one for one: every member is the PascalCase of Swiper's own name, camel-cased back by the interop serializer, so Swiper's reference reads directly onto this type.

**Every member is nullable and unset by default.** An unset member is dropped before the options reach Swiper, so Swiper's defaults are the only defaults and this type never restates them. `false`, `0` and `""` are legitimate Swiper settings and do survive - only null is absence.

It's a `record`, so a change is a `with` expression, and changing it pushes the members that moved to the live slider (see [Reactivity](#options-are-reactive)).

### Layout and sizing

| Option | Type | Swiper's default |
| --- | --- | --- |
| `Direction` | `SwiperDirection?` | `Horizontal` |
| `InitialSlide` | `int?` | `0` |
| `Speed` | `int?` | `300` |
| `Width` / `Height` | `double?` | `null` - forces a size, for initializing while hidden or prerendering |
| `AutoHeight` | `bool?` | `false` |
| `RoundLengths` | `bool?` | `false` |
| `SetWrapperSize` | `bool?` | `false` |
| `VirtualTranslate` | `bool?` | `false` |
| `Nested` | `bool?` | `false` |
| `Effect` | `SwiperEffect?` | `Slide` |
| `SpaceBetween` | `SwiperLength?` | `0` |
| `SlidesPerView` | `SwiperSlidesPerView?` | `1` |
| `SlidesPerGroup` | `int?` | `1` |
| `SlidesPerGroupSkip` | `int?` | `0` |
| `SlidesPerGroupAuto` | `bool?` | `false` |
| `CenteredSlides` | `bool?` | `false` |
| `CenteredSlidesBounds` | `bool?` | `false` |
| `SlidesOffsetBefore` / `SlidesOffsetAfter` | `double?` | `0` |
| `NormalizeSlideIndex` | `bool?` | `true` |
| `CenterInsufficientSlides` | `bool?` | `false` |
| `SnapToSlideEdge` | `bool?` | `false` |
| `MaxBackfaceHiddenSlides` | `int?` | `10` |
| `WatchOverflow` | `bool?` | `true` |
| `WatchSlidesProgress` | `bool?` | `false` |
| `OneWayMovement` | `bool?` | `false` - not the same as `AllowSlidePrev = false`, see [below](#onewaymovement-is-not-allowslideprev--false) |
| `CssMode` | `bool?` | `false` - see [below](#cssmode-trades-features-for-smoothness) |
| `UpdateOnWindowResize` | `bool?` | `true` |
| `ResizeObserver` | `bool?` | `true` - see [below](#resizeobserver-and-a11yscrollonfocus-can-undo-a-move-you-just-made) |
| `Enabled` | `bool?` | `true` |

### Touch and interaction

| Option | Type | Swiper's default |
| --- | --- | --- |
| `AllowTouchMove` | `bool?` | `true` |
| `AllowSlideNext` / `AllowSlidePrev` | `bool?` | `true` - see [below](#onewaymovement-is-not-allowslideprev--false) |
| `GrabCursor` | `bool?` | `false` |
| `TouchEventsTarget` | `SwiperTouchEventsTarget?` | `Wrapper` |
| `TouchRatio` | `double?` | `1` |
| `TouchAngle` | `double?` | `45` |
| `SimulateTouch` | `bool?` | `true` |
| `ShortSwipes` / `LongSwipes` | `bool?` | `true` |
| `LongSwipesRatio` | `double?` | `0.5` |
| `LongSwipesMs` | `int?` | `300` |
| `FollowFinger` | `bool?` | `true` |
| `Threshold` | `double?` | `5` |
| `TouchStartPreventDefault` | `bool?` | `true` |
| `TouchStartForcePreventDefault` | `bool?` | `false` |
| `TouchMoveStopPropagation` | `bool?` | `false` |
| `EdgeSwipeDetection` | `SwiperEdgeSwipeDetection?` | `Disabled` |
| `EdgeSwipeThreshold` | `double?` | `20` |
| `TouchReleaseOnEdges` | `bool?` | `false` |
| `PassiveListeners` | `bool?` | `true` |
| `Resistance` | `bool?` | `true` |
| `ResistanceRatio` | `double?` | `0.85` |
| `PreventInteractionOnTransition` | `bool?` | `false` |
| `NoSwiping` | `bool?` | `true` |
| `NoSwipingClass` | `string?` | `swiper-no-swiping` |
| `NoSwipingSelector` | `string?` | `null` |
| `SwipeHandler` | `string?` | `null` |
| `PreventClicks` / `PreventClicksPropagation` | `bool?` | `true` |
| `SlideToClickedSlide` | `bool?` | `false` |
| `FocusableElements` | `string?` | form controls |

### Loop and rewind

| Option | Type | Swiper's default |
| --- | --- | --- |
| `Loop` | `bool?` | `false` |
| `LoopAddBlankSlides` | `bool?` | `true` |
| `LoopAdditionalSlides` | `int?` | `0` |
| `LoopPreventsSliding` | `bool?` | `true` |
| `Rewind` | `bool?` | `false` |

### Breakpoints, observers and the rest

| Option | Type | Swiper's default |
| --- | --- | --- |
| `Breakpoints` | `Dictionary<string, SwiperOptions>?` | `null` - see [below](#breakpoints) |
| `BreakpointsBase` | `string?` | `window` - use the `SwiperBreakpointsBase` constants |
| `Observer` | `bool?` | **off**, deviating from Swiper - see [below](#observer-is-off-by-default) |
| `ObserveParents` / `ObserveSlideChildren` | `bool?` | `false` |
| `UserAgent` / `Url` | `string?` | `null` - for prerendering on the server |
| `UniqueNavElements` | `bool?` | `true` |
| `RunCallbacksOnInit` | `bool?` | `true` |
| `InjectStyles` | `string[]?` | `null` - see [below](#reaching-the-shadow-dom) |
| `InjectStylesUrls` | `string[]?` | `null` |
| `ContainerModifierClass`, `SlideClass`, `SlideActiveClass`, `SlideVisibleClass`, `SlideFullyVisibleClass`, `SlideBlankClass`, `SlideNextClass`, `SlidePrevClass`, `WrapperClass`, `LazyPreloaderClass` | `string?` | Swiper's own class names |
| `LazyPreload` | `bool?` | `true` - turn off when something else handles `loading="lazy"` images |
| `LazyPreloadPrevNext` | `int?` | `0` |

### Modules

Each module is its own record, and each one that can be switched off outright has an implicit conversion from `bool` - so `Pagination = true` still reads well.

| Option | Type | Notes |
| --- | --- | --- |
| `Navigation` | `SwiperNavigationOptions?` | `NextEl`, `PrevEl`, `AddIcons`, `HideOnClick`, and the four class names |
| `Pagination` | `SwiperPaginationOptions?` | `Type`, `Clickable`, `DynamicBullets`, `DynamicMainBullets`, `BulletElement`, `ProgressbarOpposite`, `HideOnClick`, the render templates, and thirteen class names |
| `Scrollbar` | `SwiperScrollbarOptions?` | `Draggable`, `Hide`, `SnapOnRelease`, `DragSize`, and five class names |
| `Autoplay` | `SwiperAutoplayOptions?` | `Delay`, `StopOnLastSlide`, `DisableOnInteraction`, `ReverseDirection`, `WaitForTransition`, `PauseOnMouseEnter` |
| `Keyboard` | `SwiperKeyboardOptions?` | `OnlyInViewport`, `PageUpDown`, `Speed` |
| `Mousewheel` | `SwiperMousewheelOptions?` | `ForceToAxis`, `ReleaseOnEdges`, `Invert`, `Sensitivity`, `EventsTarget`, `ThresholdDelta`, `ThresholdTime`, `NoMousewheelClass` |
| `FreeMode` | `SwiperFreeModeOptions?` | `Momentum`, `MomentumRatio`, `MomentumVelocityRatio`, `MomentumBounce`, `MomentumBounceRatio`, `MinimumVelocity`, `Sticky` |
| `Grid` | `SwiperGridOptions?` | `Rows`, `Fill` |
| `Zoom` | `SwiperZoomOptions?` | `MaxRatio`, `MinRatio`, `LimitToOriginalSize`, `PanOnMouseMove`, `Toggle`, `ContainerClass`, `ZoomedSlideClass` |
| `Parallax` | `SwiperParallaxOptions?` | Drive it from `data-swiper-parallax` attributes inside the slides |
| `A11y` | `SwiperA11yOptions?` | On by default. Every message, role and `ScrollOnFocus` |
| `HashNavigation` | `SwiperHashNavigationOptions?` | `WatchState`, `ReplaceState`. Needs `SwiperSlide.Hash` |
| `History` | `SwiperHistoryOptions?` | `Root`, `Key`, `ReplaceState`, `KeepQuery`. Needs `SwiperSlide.Hash` |
| `Thumbs` | `SwiperThumbsOptions?` | `MultipleActiveThumbs`, `AutoScrollOffset`, two class names. The strip itself goes on [`Thumbs`](#thumbnails-and-synced-sliders) |
| `Controller` | `SwiperControllerOptions?` | `Inverse`, `By`. The controlled slider goes on [`Controller`](#thumbnails-and-synced-sliders) |
| `Virtual` | `SwiperVirtualOptions?` | `Cache`, `SlideCount`, `AddSlidesBefore`, `AddSlidesAfter`, `SlidesPerViewAutoSlideSize`. Pair `SlideCount` with [`OnVirtualRender`](#virtual-slides-rendered-by-blazor) to keep only a window in the DOM |
| `FadeEffect`, `CubeEffect`, `FlipEffect`, `CoverflowEffect`, `CreativeEffect`, `CardsEffect` | records | Read by the matching `Effect` value |

A module options object with **no** `Enabled` set is taken by Swiper as enabled, so `Autoplay = new() { Delay = 2000 }` starts playing. `Enabled = false` is sent as a plain `false` rather than as an object, because Swiper Element builds a module's elements for any object it's given - before the module itself declines to run.

### The two union types

Swiper has two parameters that take either a number or a string, and both are their own type here so the union survives:

```csharp
new SwiperOptions
{
    SlidesPerView = 2.5,                        // a count, fractions allowed
    SlidesPerView = SwiperSlidesPerView.Auto,   // ...or sized from the slides' own content
    SpaceBetween = 16,                          // px
    SpaceBetween = "10%",                       // ...or a percentage
}
```

### Options are reactive

Changing `Options` pushes the members that moved to the live slider - the slider is updated in place, not rebuilt.

```razor
<Swiper Options="_options" />

@code {
    private SwiperOptions _options = new() { SlidesPerView = 1 };

    private void Widen() => _options = _options with { SlidesPerView = 3 };
}
```

Swiper re-applies most parameters on update. The ones it only reads while initializing - `Effect`, `CssMode`, the module option objects, the class names, `InitialSlide` - are ignored, and say so in the browser console rather than failing silently. Setting a member back to null leaves the last applied value, because Swiper has no way to unset a parameter back to its default.

### Breakpoints

The value is a full `SwiperOptions`, because Swiper accepts the same shape at a breakpoint. Keys are widths in px, or ratios like `@1.5`.

```csharp
new SwiperOptions
{
    SlidesPerView = 1,
    Breakpoints = new()
    {
        ["480"] = new() { SlidesPerView = 2 },
        ["800"] = new() { SlidesPerView = 3 },
    }
}
```

Not every parameter can change responsively; Swiper ignores the ones it can't re-apply, such as `Loop` and `Effect`.

### `OneWayMovement` is not `AllowSlidePrev = false`

Both read as "the slider won't go back" if the only thing you try is swiping backwards, but they do different things and only one of them is a lock:

| | Backwards drag | `SlidePrev()`, arrows, pagination |
| --- | --- | --- |
| `OneWayMovement = true` | advances **forward** | still go back |
| `AllowSlidePrev = false` | does nothing | do nothing either |

`OneWayMovement` reinterprets the *gesture*: pull either way and the slider advances. `AllowSlidePrev` is a lock, and it applies to the API as much as to the gesture — with it off, `SlidePrev()` is a no-op. Reach for the first when the slides are a stack being flicked through, and the second when going back is genuinely not allowed.

`ArmAnchor` and `UpdateAndAnchor` ignore the lock, since a correction isn't navigation.

### Observer is off by default

Swiper Element turns its `MutationObserver` on; this wrapper turns it off. With a framework re-rendering slide content - and especially with `AutoHeight`, whose height writes are themselves DOM mutations - it drives a costly update/height feedback loop. Call `Update()` explicitly when the slide collection changes, or set `Observer = true` for Swiper's behaviour.

### CssMode trades features for smoothness

`CssMode = true` moves the slider onto the browser's native CSS Scroll Snap API, so scrolling runs on the compositor thread - dramatically smoother for heavy or tall slides. In exchange (these are Swiper's own limitations):

- mouse-drag does not work - wheel, trackpad and touch still do, exactly like a native scroller
- `Speed` is ignored
- transition start/end events do not fire, so use `OnSlideChange` rather than `OnTransitionEnd`
- it is **not** compatible with `Loop`

Programmatic moves are animated by the wrapper rather than by Swiper, because Swiper drives them with a native smooth scroll that `scroll-snap-type: mandatory` cancels outright.

### `ResizeObserver` and `A11y.ScrollOnFocus` can undo a move you just made

Both of these Swiper behaviours end by re-anchoring onto the index Swiper is currently holding, and both are deferred by a frame. When your code moves the slider during the same interaction that triggered one of them, the correction is scheduled with the *pre-move* index, lands after your move, and silently undoes it.

- `ResizeObserver = false` drops the per-element size observer. Window resizes still re-measure.
- `A11y = new() { ScrollOnFocus = false }` stops Swiper sliding a slide into view when something inside it takes focus - which otherwise pulls the slider back to the slide holding the button that just advanced it.

The wrapper already re-asserts a programmatic move if a *resize* tries to undo it while the move is still in flight, so reach for these when you'd rather Swiper didn't schedule the correction at all.

### Reaching the shadow DOM

Swiper Element renders its controls into a shadow root. The navigation arrows are exposed as CSS parts:

```css
swiper-container::part(button-next) { color: #0ea5a4; }
```

Anything deeper - the pagination bullets, for instance - needs `InjectStyles`, which is the same mechanism Swiper Element uses for its own CSS:

```csharp
new SwiperOptions
{
    Pagination = new() { Clickable = true },
    InjectStyles = [".swiper-pagination-bullet { background: #0ea5a4; }"],
}
```

### Pagination render templates

Swiper's `renderBullet`, `renderFraction`, `renderProgressbar`, `renderCustom` and the `formatFraction*` hooks are JavaScript functions it calls **synchronously** while rendering. A C# delegate can't answer synchronously - interop is asynchronous, and on Blazor Server it's a network round trip - so the wrapper takes templates and builds the functions on the JavaScript side.

```csharp
Pagination = new()
{
    RenderBulletTemplate = "<span class='{{className}}'>{{index}}</span>",   // {{index}} is 1-based
    FractionMinimumDigits = 2,                                              // "03 / 12"
}
```

`RenderFractionTemplate` takes `{{currentClass}}` and `{{totalClass}}`, `RenderProgressbarTemplate` takes `{{fillClass}}`, and `RenderCustomTemplate` takes `{{current}}` and `{{total}}`.

## Events

All 77 of Swiper's events have a callback. **Nothing is listened for until you assign one** - the component works out which callbacks have a delegate and tells the interop module only those names, so an unwired event costs no DOM listener and no interop call.

```razor
<Swiper OnReachEnd="LoadMore" OnUserSlideChange="LoadPageFor" OnZoomChange="z => _scale = z.Scale" />
```

| Group | Callbacks |
| --- | --- |
| Lifecycle | `OnBeforeInit`, `OnInit`, `OnAfterInit`, `OnReady`, `OnBeforeDestroy`, `OnDestroy` |
| Slide changes | `OnSlideChange`, `OnUserSlideChange`, `OnBeforeSlideChangeStart`, `OnActiveIndexChange`, `OnRealIndexChange`, `OnSnapIndexChange` |
| Transitions | `OnTransitionStart`, `OnTransitionEnd`, `OnBeforeTransitionStart`, `OnSlideChangeTransitionStart/End`, `OnSlideNextTransitionStart/End`, `OnSlidePrevTransitionStart/End`, `OnSlideResetTransitionStart/End` |
| Pointer | `OnTouchStart`, `OnTouchMove`, `OnTouchMoveOpposite`, `OnTouchEnd`, `OnSliderMove`, `OnSliderFirstMove`, `OnClick`, `OnTap`, `OnDoubleTap`, `OnDoubleClick` |
| Position | `OnProgress`, `OnSetTranslate`, `OnSetTransition`, `OnReachBeginning`, `OnReachEnd`, `OnToEdge`, `OnFromEdge`, `OnMomentumBounce` |
| Layout | `OnResize`, `OnBeforeResize`, `OnUpdate`, `OnSlidesUpdated`, `OnSlidesLengthChange`, `OnSlidesGridLengthChange`, `OnSnapGridLengthChange`, `OnBreakpoint`, `OnChangeDirection`, `OnOrientationChange`, `OnObserverUpdate`, `OnLock`, `OnUnlock` |
| Loop | `OnBeforeLoopFix`, `OnLoopFix` |
| Autoplay | `OnAutoplay`, `OnAutoplayStart`, `OnAutoplayStop`, `OnAutoplayPause`, `OnAutoplayResume`, `OnAutoplayTimeLeft` |
| Navigation & pagination | `OnNavigationNext`, `OnNavigationPrev`, `OnNavigationShow`, `OnNavigationHide`, `OnPaginationRender`, `OnPaginationUpdate`, `OnPaginationShow`, `OnPaginationHide` |
| Scrollbar | `OnScrollbarDragStart`, `OnScrollbarDragMove`, `OnScrollbarDragEnd` |
| Other modules | `OnKeyPress`, `OnScroll` (mousewheel), `OnZoomChange`, `OnHashChange`, `OnHashSet`, `OnVirtualUpdate`, [`OnVirtualRender`](#virtual-slides-rendered-by-blazor) |

Swiper hands several events a DOM element or DOM event, neither of which can cross the interop boundary, so what arrives is the part you can act on: `SwiperPointerEventArgs` (client coordinates and the clicked slide index), `SwiperAutoplayTimeLeft`, `SwiperZoomChange`, `SwiperMousewheelScroll`.

### Events that fire every frame

`OnProgress`, `OnSetTranslate`, `OnSetTransition`, `OnSliderMove`, `OnTouchMove`, `OnTouchMoveOpposite`, `OnAutoplayTimeLeft`, `OnZoomChange` and `OnScroll` fire per animation frame. On Blazor Server that's a network round trip per frame, which is what `EventThrottle` is for:

```razor
<Swiper EventThrottle="TimeSpan.FromMilliseconds(50)" OnProgress="p => _progress = p" />
```

The first event of a burst is always delivered, so a throttled `OnProgress` still sees a drag start immediately.

### Prefer `OnUserSlideChange` when you react by changing state

`OnSlideChange` reports every change - including the one your own `SlideTo` call just caused. A host that reacts to a change by updating its own state will feed its own move straight back into itself, and by index alone the two are indistinguishable.

`OnUserSlideChange` reports only the changes the user dragged. A plain tap raises neither, so a button inside a slide is never mistaken for a swipe. The slider keeps moving after the pointer is released, so treat the interaction as finished on `OnTransitionEnd` rather than on release.

## Programmatic control

Capture the component with `@ref`. Every method is a no-op until the component has rendered and its JS module has loaded, so calling one before `OnReady` does nothing rather than throwing.

| Member | Description |
| --- | --- |
| `ActiveIndex` | The active slide's logical index. Two-way bindable. |
| `SlideTo(index, speed?)` | Transition to a slide. |
| `SlideNext(speed?)` / `SlidePrev(speed?)` | Step. |
| `SlideReset(speed?)` | Settle back onto the current slide. |
| `SlideToClosest(speed?)` | Snap to whichever slide is nearest. |
| `SlideToClickedSlide()` | Move to the slide the user last clicked. |
| `Update()` | Recalculate after the slide collection changed. |
| `UpdateSize()`, `UpdateSlides()`, `UpdateProgress()`, `UpdateSlidesClasses()`, `UpdateAutoHeight(speed)` | The narrower recalculations. |
| `UpdateAndAnchor(index)` / `ArmAnchor(index)` | Keep the position across a slide-collection change - see [below](#keeping-the-position-when-slides-are-added-or-removed). |
| `SetAllowSlideNext(bool)` / `SetAllowSlidePrev(bool)` / `SetAllowTouchMove(bool)` | Runtime locks. |
| `Enable()` / `Disable()` | Whether the slider responds to anything at all. |
| `SetProgress(progress, speed?)` | Move to a position rather than to a slide. |
| `ChangeDirection(direction)` | Switch axis at runtime. |
| `ChangeLanguageDirection(isRightToLeft)` | Switch between LTR and RTL. |
| `TranslateTo(translate, speed)` / `GetTranslateAsync()` | The track's raw offset. |
| `DetachEvents()` / `AttachEvents()` | Stop and restart Swiper's own listeners. |
| `GetStateAsync()` | Everything Swiper knows about itself, in one call. |

Modules with several methods of their own are grouped:

```csharp
await _swiper.Autoplay.Start();        // Stop, Pause, Resume
await _swiper.Zoom.Toggle();           // In, Out, Enable, Disable
await _swiper.Keyboard.Disable();      // Enable
await _swiper.Mousewheel.Enable();     // Disable
await _swiper.Virtual.Update(force: true);
await _swiper.Slides.Append("<swiper-slide>...</swiper-slide>");   // Prepend, Insert, Remove, RemoveAll
```

`Slides` is Swiper's own manipulation API. It writes slide elements Blazor didn't render and doesn't know about, so the next render that touches the collection will fight it - it's an escape hatch for a host that owns the slider outright. When Blazor renders the slides, keep the `@foreach` and use the anchoring methods below.

### Virtual slides rendered by Blazor

Swiper's virtual module normally creates and removes slide elements itself, which is a fight with
Blazor over the same DOM. `OnVirtualRender` is Swiper's `renderExternal` hook - the one its React and
Vue bindings use - so Swiper works out *which* slides are needed and *where* the window sits, hands
both over, and touches no element at all.

```csharp
private SwiperVirtualWindow _window = new() { From = 0, To = 2 };

private readonly SwiperOptions _options = new()
{
    Virtual = new SwiperVirtualOptions { Enabled = true, SlideCount = 200 }
};
```

```razor
<Swiper Options="_options" OnVirtualRender="w => { _window = w; StateHasChanged(); }">
    @for (var index = _window.From; index <= _window.To; index++)
    {
        var slide = index;
        <SwiperSlide @key="slide" VirtualIndex="slide">Slide @(slide + 1)</SwiperSlide>
    }
</Swiper>
```

Three slide elements exist for a collection of two hundred, and the window moves with the slider.
Two things to know:

- **Leave `Offset` alone.** The wrapper applies it to the slides, which is where Swiper's own
  renderer puts it - it is what keeps a three-slide window sitting where slides 98 to 100 belong
  rather than at the track's origin.
- **An empty first render is fine.** The module builds its grids from `SlideCount`, not from the
  elements present, so a slider that initializes before the first window arrives still knows the
  collection is two hundred slides long and still navigates. Seeding a window - as the story does -
  only saves the empty frame between init and the first render. The exception is
  `SlidesPerView = "auto"`, which sizes from real elements and falls back to
  `SlidesPerViewAutoSlideSize` until they exist.

Re-measuring is handled too: the window arrives *before* Blazor has rendered it, so Swiper is told to
skip its own post-render pass and the wrapper runs it on the render that follows.

Without a handler the module behaves as it always did - it renders slides itself, which is why the
plain `AddSlidesBefore`/`AddSlidesAfter` route is worth using only for its measuring side.

### Reading the state

```csharp
var state = await _swiper.GetStateAsync();
// ActiveIndex, RawActiveIndex, PreviousIndex, SnapIndex, SlidesCount,
// IsBeginning, IsEnd, IsLocked, IsAnimating, IsEnabled, Progress, Translate,
// Width, Height, SwipeDirection, CurrentBreakpoint, VisibleSlideIndexes,
// IsAutoplayRunning, IsAutoplayPaused, ZoomScale
```

One call rather than a round trip per value, which on Blazor Server is a network hop each. `VisibleSlideIndexes` is empty unless `WatchSlidesProgress` is on, since that's what measures them.

### Keeping the position when slides are added or removed

When Blazor itself adds or removes `<SwiperSlide>` elements, Swiper's transform still points at the old offset. Removing a slide that sits *before* the active one shifts every later slide sideways, so the wrong slide is on screen until Swiper is told. `Update()` isn't enough here: it finishes by sliding to the index it already held, and that index addresses a different slide once the collection changed.

- **`UpdateAndAnchor(index)`** recalculates and settles on `index` in one operation. Use it when your code changes the collection and knows where it wants to end up.
- **`ArmAnchor(index)`** is for when Blazor owns the mutation. Call it *before* the change: it arms a one-shot `MutationObserver` whose callback runs after Blazor's DOM patch but before the next paint, so the correction lands in the same frame. Correcting from a call made *after* the change is a race the browser can win by painting first.

```csharp
// About to remove a slide that sits before the active one.
await _swiper!.ArmAnchor(_activeIndex - 1);
_slides.RemoveAt(0);
```

Both ignore `AllowSlideNext` / `AllowSlidePrev` - a correction isn't navigation, so your swipe locks can't veto it.

## Slides

| Parameter | Type | Description |
| --- | --- | --- |
| `Index` | `int?` | The slide's position, for `SlideContent`. See below. |
| `Zoom` | `bool` | Wraps the content in Swiper's zoom container, which is what `Options.Zoom` scales. |
| `ZoomMaxRatio` | `double?` | A zoom factor for this slide alone. |
| `Lazy` | `bool` | Defers this slide's images. Mark the images `loading="lazy"` too. |
| `Hash` | `string?` | This slide's key for `HashNavigation` and `History`. |
| `AutoplayDelay` | `int?` | How long autoplay holds this slide, overriding the slider's delay. |
| `VirtualIndex` | `int?` | This slide's index for the `Virtual` module. |

Swiper reads all of these off the slide element itself, which is why they're parameters here rather than members of `SwiperOptions`.

### Per-slide state

`SlideContent` hands the content the slide's own state, so it can render differently while it's the active one - pausing a video on the slides that aren't, for instance:

```razor
@foreach (var (item, index) in _items.Select((x, i) => (x, i)))
{
    <SwiperSlide Index="index">
        <SlideContent Context="slide">
            <video autoplay="@slide.IsActive" src="@item.Url" />
        </SlideContent>
    </SwiperSlide>
}
```

`SwiperSlideContext` carries `Index`, `IsActive`, `IsNext` and `IsPrevious`, derived from the slider's active index rather than read back out of Swiper - so it costs no interop and is available during render. Plain `ChildContent` is untouched and doesn't pay for any of it.

`Index` is only needed for this. Left null, a slide takes its position from the order the slides registered in, which is right for a fixed list and for one that only grows at the end, but not for one that inserts in the middle.

## Thumbnails and synced sliders

Name the other slider by **CSS selector**. It's the order-independent route and the one to prefer:

```razor
<Swiper Options="new() { Thumbs = new() { Swiper = "#thumbs", MultipleActiveThumbs = false } }">
    ...
</Swiper>

<Swiper id="thumbs" Options="new() { SlidesPerView = 5, WatchSlidesProgress = true, SlideToClickedSlide = true }">
    ...
</Swiper>
```

`Controller = new() { Control = "#captions", Inverse = true }` works the same way.

Why a selector rather than a `@ref`? Because `@ref` is assigned *during* the render that creates the component — after the sibling's `Thumbs="_thumbs"` attribute has already been evaluated. On first paint it's still null, and it only arrives if the host happens to re-render. A selector has no such problem: the wiring is resolved on the JavaScript side and waits for whichever slider initializes last.

The `Thumbs` and `Controller` component parameters still exist for hosts that do re-render (they're wired as soon as the companion reports itself initialized), but the selector is what the stories use.

Swiper's own thumbs and controller modules take a live instance and nothing else, so both routes end in the wrapper handing one over — the selector is resolved by us, not by Swiper.

## Reaching anything else

`SwiperOptions` covers every parameter Swiper Element accepts, so this is rarely needed - but any attribute you put on `<Swiper>` that isn't a component parameter is forwarded to the underlying `<swiper-container>`, which reads it as a parameter when it initializes. The element also parses nested parameters from attributes, so `pagination-clickable="true"` reaches `pagination.clickable`.

```razor
<Swiper Options="new() { SlidesPerView = 2 }" pagination-clickable="true" />
```

## Vertical direction needs a height

A vertical Swiper sizes its slides from the `<Swiper>` element's own height, which defaults to its content height. Give it an explicit height, or it collapses and the slides stack:

```razor
<Swiper Options="new() { Direction = SwiperDirection.Vertical }" style="height: 300px;">
```

## Upgrading from 14.0.6.1

The API grew from a curated subset to Swiper's whole surface, which meant some breaks. All of them are compile-time.

| Was | Now |
| --- | --- |
| `Direction = "horizontal"` | `SwiperDirection` is an enum. `SwiperDirection.Horizontal` still compiles; a string literal doesn't. |
| `Pagination = true` | Still compiles - the module options records convert implicitly from `bool`. |
| `AllowSlideNext = true` and friends | Now `bool?`. Assignments still compile; reads need `== true` or `?? true`. |
| `A11y = new SwiperA11yOptions { ScrollOnFocus = false }` | Unchanged, and the record now carries every other a11y parameter too. |
| `ActiveIndex` (read-only property) | A two-way bindable parameter. Reads are unchanged. |
| `OnSlideChangeInternal`, `OnReachEndInternal`, ... | Collapsed into one `OnSwiperEventInternal` dispatcher. These were never meant to be called from your code. |
| `<SwiperSlide>` content | Unchanged. Per-slide state is the new `SlideContent`, which is opt-in. |
| `OnSlideChange` etc. | Unchanged - but they're now opt-in, so an unassigned callback is no longer listened for at all. |

## License

[MIT](https://github.com/Kebechet/Blazor.Swiper/blob/main/LICENSE)

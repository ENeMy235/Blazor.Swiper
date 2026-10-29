using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Kebechet.Blazor.Swiper;

// Swiper's events, one callback each.
//
// Nothing is listened for until a callback is assigned: the component works out which events have a
// delegate and tells the interop module only those names, so an unsubscribed event costs no DOM
// listener and no interop call - which matters, because a handful of these fire on every animation
// frame and on Blazor Server each one is a network round trip. EventThrottle covers those.
//
// Swiper hands several events a DOM element or DOM event. Neither can cross the interop boundary, so
// what arrives is the part a host can act on - a slide index, a pointer position.
//
// The <summary> for the type itself lives on the primary part, in Swiper.razor.cs. A second one here
// would be a coin toss over which the compiler writes into the XML docs, and the storybook reads that.
public partial class Swiper
{
    /// <summary>Raised once Swiper has begun initializing, before it has measured anything.</summary>
    [Parameter] public EventCallback OnBeforeInit { get; set; }

    /// <summary>Raised when Swiper has initialized.</summary>
    /// <remarks>
    /// Use <see cref="OnReady"/> instead unless you specifically need Swiper's own moment: this one
    /// fires from inside initialization, before the component has revealed the slider.
    /// </remarks>
    [Parameter] public EventCallback OnInit { get; set; }

    /// <summary>Raised after Swiper has finished initializing.</summary>
    [Parameter] public EventCallback OnAfterInit { get; set; }

    /// <summary>Raised before the underlying Swiper is torn down.</summary>
    [Parameter] public EventCallback OnBeforeDestroy { get; set; }

    /// <summary>Raised when the underlying Swiper has been torn down.</summary>
    [Parameter] public EventCallback OnDestroy { get; set; }

    /// <summary>Raised when Swiper has re-measured its slides; the argument is the slide count.</summary>
    [Parameter] public EventCallback<int> OnSlidesUpdated { get; set; }

    /// <summary>Raised when a slide-change transition starts.</summary>
    [Parameter] public EventCallback OnSlideChangeTransitionStart { get; set; }

    /// <summary>Raised when a slide-change transition ends.</summary>
    [Parameter] public EventCallback OnSlideChangeTransitionEnd { get; set; }

    /// <summary>Raised when a forward transition starts.</summary>
    [Parameter] public EventCallback OnSlideNextTransitionStart { get; set; }

    /// <summary>Raised when a forward transition ends.</summary>
    [Parameter] public EventCallback OnSlideNextTransitionEnd { get; set; }

    /// <summary>Raised when a backward transition starts.</summary>
    [Parameter] public EventCallback OnSlidePrevTransitionStart { get; set; }

    /// <summary>Raised when a backward transition ends.</summary>
    [Parameter] public EventCallback OnSlidePrevTransitionEnd { get; set; }

    /// <summary>Raised when a transition back to the same slide starts, i.e. a swipe that did not commit.</summary>
    [Parameter] public EventCallback OnSlideResetTransitionStart { get; set; }

    /// <summary>Raised when a transition back to the same slide ends.</summary>
    [Parameter] public EventCallback OnSlideResetTransitionEnd { get; set; }

    /// <summary>Raised when any transition starts.</summary>
    [Parameter] public EventCallback OnTransitionStart { get; set; }

    /// <summary>Raised when a slide transition finishes, i.e. the slider has settled at rest.</summary>
    [Parameter] public EventCallback OnTransitionEnd { get; set; }

    /// <summary>Raised just before a transition starts; the argument is its duration in ms.</summary>
    [Parameter] public EventCallback<double> OnBeforeTransitionStart { get; set; }

    /// <summary>Raised just before the active slide changes.</summary>
    [Parameter] public EventCallback OnBeforeSlideChangeStart { get; set; }

    /// <summary>Raised when a pointer goes down on the slider.</summary>
    [Parameter] public EventCallback<SwiperPointerEventArgs> OnTouchStart { get; set; }

    /// <summary>Raised as the pointer moves with the button down. Fires per frame - see <see cref="EventThrottle"/>.</summary>
    [Parameter] public EventCallback<SwiperPointerEventArgs> OnTouchMove { get; set; }

    /// <summary>Raised when the pointer moves across the slider's axis rather than along it. Fires per frame.</summary>
    [Parameter] public EventCallback<SwiperPointerEventArgs> OnTouchMoveOpposite { get; set; }

    /// <summary>Raised when the pointer is released.</summary>
    [Parameter] public EventCallback<SwiperPointerEventArgs> OnTouchEnd { get; set; }

    /// <summary>Raised as the drag moves the slider. Fires per frame - see <see cref="EventThrottle"/>.</summary>
    [Parameter] public EventCallback<SwiperPointerEventArgs> OnSliderMove { get; set; }

    /// <summary>
    /// Raised the first time a drag actually moves the slider. Use it to tell a user-driven slide change
    /// apart from a programmatic one - by index alone they are identical. A plain tap does not raise this,
    /// so a button inside a slide cannot be mistaken for a swipe. The slider keeps moving
    /// afterwards, so treat the interaction as over on <see cref="OnTransitionEnd"/>, not on pointer release.
    /// Not raised in <see cref="SwiperOptions.CssMode"/>, where the browser owns the scroll.
    /// </summary>
    [Parameter] public EventCallback OnSliderFirstMove { get; set; }

    /// <summary>
    /// Raised when the slider is clicked. This is Swiper's own <c>click</c> event, not a DOM handler -
    /// it carries the index of the slide that was clicked.
    /// </summary>
    [Parameter] public EventCallback<SwiperPointerEventArgs> OnClick { get; set; }

    /// <summary>Raised on a tap that did not turn into a swipe; carries the tapped slide's index.</summary>
    [Parameter] public EventCallback<SwiperPointerEventArgs> OnTap { get; set; }

    /// <summary>Raised on a double tap; carries the tapped slide's index.</summary>
    [Parameter] public EventCallback<SwiperPointerEventArgs> OnDoubleTap { get; set; }

    /// <summary>Raised on a double click; carries the clicked slide's index.</summary>
    [Parameter] public EventCallback<SwiperPointerEventArgs> OnDoubleClick { get; set; }

    /// <summary>
    /// Raised as the slider moves, with its position from 0 at the first slide to 1 at the last.
    /// Fires per frame - see <see cref="EventThrottle"/>.
    /// </summary>
    [Parameter] public EventCallback<double> OnProgress { get; set; }

    /// <summary>Raised when the first slide is reached.</summary>
    [Parameter] public EventCallback OnReachBeginning { get; set; }

    /// <summary>Raised when the last slide is reached.</summary>
    [Parameter] public EventCallback OnReachEnd { get; set; }

    /// <summary>Raised when the slider arrives at either end.</summary>
    [Parameter] public EventCallback OnToEdge { get; set; }

    /// <summary>Raised when the slider leaves either end.</summary>
    [Parameter] public EventCallback OnFromEdge { get; set; }

    /// <summary>Raised with the track's translate in px. Fires per frame - see <see cref="EventThrottle"/>.</summary>
    [Parameter] public EventCallback<double> OnSetTranslate { get; set; }

    /// <summary>Raised with a transition duration in ms as it is applied. Fires per frame.</summary>
    [Parameter] public EventCallback<double> OnSetTransition { get; set; }

    /// <summary>Raised after the slider re-measures itself.</summary>
    [Parameter] public EventCallback OnResize { get; set; }

    /// <summary>Raised before the slider re-measures itself.</summary>
    [Parameter] public EventCallback OnBeforeResize { get; set; }

    /// <summary>
    /// Raised when Swiper's own <c>MutationObserver</c> sees a change. Only ever fires with
    /// <see cref="SwiperOptions.Observer"/> on, which this wrapper leaves off by default.
    /// </summary>
    [Parameter] public EventCallback OnObserverUpdate { get; set; }

    /// <summary>Raised before loop mode rearranges its duplicated slides.</summary>
    [Parameter] public EventCallback OnBeforeLoopFix { get; set; }

    /// <summary>Raised after loop mode has rearranged its duplicated slides.</summary>
    [Parameter] public EventCallback OnLoopFix { get; set; }

    /// <summary>Raised when a breakpoint takes effect; the argument is the breakpoint key, or empty for none.</summary>
    [Parameter] public EventCallback<string> OnBreakpoint { get; set; }

    /// <summary>
    /// Raised when Swiper's own active index changes. In loop mode that index counts the duplicated
    /// slides - <see cref="OnSlideChange"/> reports the logical one.
    /// </summary>
    [Parameter] public EventCallback<int> OnActiveIndexChange { get; set; }

    /// <summary>Raised when the snap-grid index changes, which differs from the slide index when slides are grouped.</summary>
    [Parameter] public EventCallback<int> OnSnapIndexChange { get; set; }

    /// <summary>Raised when the logical slide index changes.</summary>
    [Parameter] public EventCallback<int> OnRealIndexChange { get; set; }

    /// <summary>Raised when the slider's direction changes.</summary>
    [Parameter] public EventCallback OnChangeDirection { get; set; }

    /// <summary>Raised when a free-mode glide bounces back off an edge.</summary>
    [Parameter] public EventCallback OnMomentumBounce { get; set; }

    /// <summary>Raised on device orientation change.</summary>
    [Parameter] public EventCallback OnOrientationChange { get; set; }

    /// <summary>Raised when the number of slides changes; the argument is the new count.</summary>
    [Parameter] public EventCallback<int> OnSlidesLengthChange { get; set; }

    /// <summary>Raised when the slides grid is rebuilt; the argument is the slide count.</summary>
    [Parameter] public EventCallback<int> OnSlidesGridLengthChange { get; set; }

    /// <summary>Raised when the snap grid is rebuilt; the argument is the slide count.</summary>
    [Parameter] public EventCallback<int> OnSnapGridLengthChange { get; set; }

    /// <summary>Raised when Swiper recalculates, whether from <see cref="Update"/> or from its own observers.</summary>
    [Parameter] public EventCallback OnUpdate { get; set; }

    /// <summary>Raised when the slides all fit, so there is nothing left to slide.</summary>
    [Parameter] public EventCallback OnLock { get; set; }

    /// <summary>Raised when the slides no longer all fit.</summary>
    [Parameter] public EventCallback OnUnlock { get; set; }

    /// <summary>Raised when autoplay starts.</summary>
    [Parameter] public EventCallback OnAutoplayStart { get; set; }

    /// <summary>Raised when autoplay stops.</summary>
    [Parameter] public EventCallback OnAutoplayStop { get; set; }

    /// <summary>Raised when autoplay pauses, e.g. because the pointer came to rest on the slider.</summary>
    [Parameter] public EventCallback OnAutoplayPause { get; set; }

    /// <summary>Raised when a paused autoplay resumes.</summary>
    [Parameter] public EventCallback OnAutoplayResume { get; set; }

    /// <summary>
    /// Raised with the time left before autoplay advances. Fires per frame - see
    /// <see cref="EventThrottle"/> - and exists to drive a countdown.
    /// </summary>
    [Parameter] public EventCallback<SwiperAutoplayTimeLeft> OnAutoplayTimeLeft { get; set; }

    /// <summary>Raised each time autoplay moves the slider.</summary>
    [Parameter] public EventCallback OnAutoplay { get; set; }

    /// <summary>Raised when the URL fragment changed the active slide; the argument is the fragment.</summary>
    [Parameter] public EventCallback<string> OnHashChange { get; set; }

    /// <summary>Raised when Swiper wrote the active slide into the URL fragment; the argument is the fragment.</summary>
    [Parameter] public EventCallback<string> OnHashSet { get; set; }

    /// <summary>Raised when a key moved the slider; the argument is the key code.</summary>
    [Parameter] public EventCallback<string> OnKeyPress { get; set; }

    /// <summary>
    /// Raised when the mousewheel module sees a wheel movement. Fires per frame - see
    /// <see cref="EventThrottle"/>. This is Swiper's <c>scroll</c> event, and has nothing to do with
    /// the page scrolling.
    /// </summary>
    [Parameter] public EventCallback<SwiperMousewheelScroll> OnScroll { get; set; }

    /// <summary>Raised when the navigation arrows are hidden.</summary>
    [Parameter] public EventCallback OnNavigationHide { get; set; }

    /// <summary>Raised when the navigation arrows are shown.</summary>
    [Parameter] public EventCallback OnNavigationShow { get; set; }

    /// <summary>Raised when the previous arrow is clicked.</summary>
    [Parameter] public EventCallback OnNavigationPrev { get; set; }

    /// <summary>Raised when the next arrow is clicked.</summary>
    [Parameter] public EventCallback OnNavigationNext { get; set; }

    /// <summary>Raised when the pagination is first rendered.</summary>
    [Parameter] public EventCallback OnPaginationRender { get; set; }

    /// <summary>Raised when the pagination is updated.</summary>
    [Parameter] public EventCallback OnPaginationUpdate { get; set; }

    /// <summary>Raised when the pagination is hidden.</summary>
    [Parameter] public EventCallback OnPaginationHide { get; set; }

    /// <summary>Raised when the pagination is shown.</summary>
    [Parameter] public EventCallback OnPaginationShow { get; set; }

    /// <summary>Raised when a scrollbar drag starts.</summary>
    [Parameter] public EventCallback<SwiperPointerEventArgs> OnScrollbarDragStart { get; set; }

    /// <summary>Raised as the scrollbar is dragged. Fires per frame.</summary>
    [Parameter] public EventCallback<SwiperPointerEventArgs> OnScrollbarDragMove { get; set; }

    /// <summary>Raised when a scrollbar drag ends.</summary>
    [Parameter] public EventCallback<SwiperPointerEventArgs> OnScrollbarDragEnd { get; set; }

    /// <summary>Raised when the virtual module re-renders its window of slides.</summary>
    [Parameter] public EventCallback OnVirtualUpdate { get; set; }

    /// <summary>
    /// Raised as a slide is zoomed. Fires per frame during a pinch - see <see cref="EventThrottle"/>.
    /// </summary>
    [Parameter] public EventCallback<SwiperZoomChange> OnZoomChange { get; set; }

    private static readonly JsonSerializerOptions PayloadOptions = new() { PropertyNameCaseInsensitive = true };

    private Dictionary<string, Func<string?, Task>>? _eventDispatch;
    private IReadOnlyList<SwiperEventBinding>? _eventBindings;

    /// <summary>
    /// One event, in both directions: whether the host wants it, and how to raise it.
    /// </summary>
    /// <remarks>
    /// A single table rather than one list for subscribing and a separate switch for dispatching,
    /// because two lists of seventy-odd names drift, and a name in one but not the other fails
    /// silently - the event is either listened for and dropped, or handled and never delivered.
    /// </remarks>
    private readonly record struct SwiperEventBinding(string Name, Func<bool> IsSubscribed, Func<string?, Task> Raise);

    private IReadOnlyList<SwiperEventBinding> EventBindings => _eventBindings ??= BuildEventBindings();

    private Dictionary<string, Func<string?, Task>> EventDispatch =>
        _eventDispatch ??= EventBindings.ToDictionary(binding => binding.Name, binding => binding.Raise);

    private List<SwiperEventBinding> BuildEventBindings()
    {
        return new List<SwiperEventBinding>
        {
            new("beforeInit", () => OnBeforeInit.HasDelegate, _ => OnBeforeInit.InvokeAsync()),
            new("init", () => OnInit.HasDelegate, _ => OnInit.InvokeAsync()),
            new("afterInit", () => OnAfterInit.HasDelegate, _ => OnAfterInit.InvokeAsync()),
            new("beforeDestroy", () => OnBeforeDestroy.HasDelegate, _ => OnBeforeDestroy.InvokeAsync()),
            new("destroy", () => OnDestroy.HasDelegate, _ => OnDestroy.InvokeAsync()),
            new("slidesUpdated", () => OnSlidesUpdated.HasDelegate, json => OnSlidesUpdated.InvokeAsync(AsInt(json))),
            new("slideChangeTransitionStart", () => OnSlideChangeTransitionStart.HasDelegate, _ => OnSlideChangeTransitionStart.InvokeAsync()),
            new("slideChangeTransitionEnd", () => OnSlideChangeTransitionEnd.HasDelegate, _ => OnSlideChangeTransitionEnd.InvokeAsync()),
            new("slideNextTransitionStart", () => OnSlideNextTransitionStart.HasDelegate, _ => OnSlideNextTransitionStart.InvokeAsync()),
            new("slideNextTransitionEnd", () => OnSlideNextTransitionEnd.HasDelegate, _ => OnSlideNextTransitionEnd.InvokeAsync()),
            new("slidePrevTransitionStart", () => OnSlidePrevTransitionStart.HasDelegate, _ => OnSlidePrevTransitionStart.InvokeAsync()),
            new("slidePrevTransitionEnd", () => OnSlidePrevTransitionEnd.HasDelegate, _ => OnSlidePrevTransitionEnd.InvokeAsync()),
            new("slideResetTransitionStart", () => OnSlideResetTransitionStart.HasDelegate, _ => OnSlideResetTransitionStart.InvokeAsync()),
            new("slideResetTransitionEnd", () => OnSlideResetTransitionEnd.HasDelegate, _ => OnSlideResetTransitionEnd.InvokeAsync()),
            new("transitionStart", () => OnTransitionStart.HasDelegate, _ => OnTransitionStart.InvokeAsync()),
            new("transitionEnd", () => OnTransitionEnd.HasDelegate, _ => OnTransitionEnd.InvokeAsync()),
            new("beforeTransitionStart", () => OnBeforeTransitionStart.HasDelegate, json => OnBeforeTransitionStart.InvokeAsync(AsDouble(json))),
            new("beforeSlideChangeStart", () => OnBeforeSlideChangeStart.HasDelegate, _ => OnBeforeSlideChangeStart.InvokeAsync()),
            new("touchStart", () => OnTouchStart.HasDelegate, json => OnTouchStart.InvokeAsync(AsPointer(json))),
            new("touchMove", () => OnTouchMove.HasDelegate, json => OnTouchMove.InvokeAsync(AsPointer(json))),
            new("touchMoveOpposite", () => OnTouchMoveOpposite.HasDelegate, json => OnTouchMoveOpposite.InvokeAsync(AsPointer(json))),
            new("touchEnd", () => OnTouchEnd.HasDelegate, json => OnTouchEnd.InvokeAsync(AsPointer(json))),
            new("sliderMove", () => OnSliderMove.HasDelegate, json => OnSliderMove.InvokeAsync(AsPointer(json))),
            new("sliderFirstMove", () => OnSliderFirstMove.HasDelegate, _ => OnSliderFirstMove.InvokeAsync()),
            new("click", () => OnClick.HasDelegate, json => OnClick.InvokeAsync(AsPointer(json))),
            new("tap", () => OnTap.HasDelegate, json => OnTap.InvokeAsync(AsPointer(json))),
            new("doubleTap", () => OnDoubleTap.HasDelegate, json => OnDoubleTap.InvokeAsync(AsPointer(json))),
            new("doubleClick", () => OnDoubleClick.HasDelegate, json => OnDoubleClick.InvokeAsync(AsPointer(json))),
            new("progress", () => OnProgress.HasDelegate, json => OnProgress.InvokeAsync(AsDouble(json))),
            new("reachBeginning", () => OnReachBeginning.HasDelegate, _ => OnReachBeginning.InvokeAsync()),
            new("reachEnd", () => OnReachEnd.HasDelegate, _ => OnReachEnd.InvokeAsync()),
            new("toEdge", () => OnToEdge.HasDelegate, _ => OnToEdge.InvokeAsync()),
            new("fromEdge", () => OnFromEdge.HasDelegate, _ => OnFromEdge.InvokeAsync()),
            new("setTranslate", () => OnSetTranslate.HasDelegate, json => OnSetTranslate.InvokeAsync(AsDouble(json))),
            new("setTransition", () => OnSetTransition.HasDelegate, json => OnSetTransition.InvokeAsync(AsDouble(json))),
            new("resize", () => OnResize.HasDelegate, _ => OnResize.InvokeAsync()),
            new("beforeResize", () => OnBeforeResize.HasDelegate, _ => OnBeforeResize.InvokeAsync()),
            new("observerUpdate", () => OnObserverUpdate.HasDelegate, _ => OnObserverUpdate.InvokeAsync()),
            new("beforeLoopFix", () => OnBeforeLoopFix.HasDelegate, _ => OnBeforeLoopFix.InvokeAsync()),
            new("loopFix", () => OnLoopFix.HasDelegate, _ => OnLoopFix.InvokeAsync()),
            new("breakpoint", () => OnBreakpoint.HasDelegate, json => OnBreakpoint.InvokeAsync(AsString(json))),
            new("activeIndexChange", () => OnActiveIndexChange.HasDelegate, json => OnActiveIndexChange.InvokeAsync(AsInt(json))),
            new("snapIndexChange", () => OnSnapIndexChange.HasDelegate, json => OnSnapIndexChange.InvokeAsync(AsInt(json))),
            new("realIndexChange", () => OnRealIndexChange.HasDelegate, json => OnRealIndexChange.InvokeAsync(AsInt(json))),
            new("changeDirection", () => OnChangeDirection.HasDelegate, _ => OnChangeDirection.InvokeAsync()),
            new("momentumBounce", () => OnMomentumBounce.HasDelegate, _ => OnMomentumBounce.InvokeAsync()),
            new("orientationchange", () => OnOrientationChange.HasDelegate, _ => OnOrientationChange.InvokeAsync()),
            new("slidesLengthChange", () => OnSlidesLengthChange.HasDelegate, json => OnSlidesLengthChange.InvokeAsync(AsInt(json))),
            new("slidesGridLengthChange", () => OnSlidesGridLengthChange.HasDelegate, json => OnSlidesGridLengthChange.InvokeAsync(AsInt(json))),
            new("snapGridLengthChange", () => OnSnapGridLengthChange.HasDelegate, json => OnSnapGridLengthChange.InvokeAsync(AsInt(json))),
            new("update", () => OnUpdate.HasDelegate, _ => OnUpdate.InvokeAsync()),
            new("lock", () => OnLock.HasDelegate, _ => OnLock.InvokeAsync()),
            new("unlock", () => OnUnlock.HasDelegate, _ => OnUnlock.InvokeAsync()),
            new("autoplayStart", () => OnAutoplayStart.HasDelegate || IsAutoplayRunningChanged.HasDelegate, _ => RaiseAutoplayStart()),
            new("autoplayStop", () => OnAutoplayStop.HasDelegate || IsAutoplayRunningChanged.HasDelegate, _ => RaiseAutoplayStop()),
            new("autoplayPause", () => OnAutoplayPause.HasDelegate, _ => OnAutoplayPause.InvokeAsync()),
            new("autoplayResume", () => OnAutoplayResume.HasDelegate, _ => OnAutoplayResume.InvokeAsync()),
            new("autoplayTimeLeft", () => OnAutoplayTimeLeft.HasDelegate, json => OnAutoplayTimeLeft.InvokeAsync(AsPayload<SwiperAutoplayTimeLeft>(json))),
            new("autoplay", () => OnAutoplay.HasDelegate, _ => OnAutoplay.InvokeAsync()),
            new("hashChange", () => OnHashChange.HasDelegate, json => OnHashChange.InvokeAsync(AsString(json))),
            new("hashSet", () => OnHashSet.HasDelegate, json => OnHashSet.InvokeAsync(AsString(json))),
            new("keyPress", () => OnKeyPress.HasDelegate, json => OnKeyPress.InvokeAsync(AsString(json))),
            new("scroll", () => OnScroll.HasDelegate, json => OnScroll.InvokeAsync(AsPayload<SwiperMousewheelScroll>(json))),
            new("navigationHide", () => OnNavigationHide.HasDelegate, _ => OnNavigationHide.InvokeAsync()),
            new("navigationShow", () => OnNavigationShow.HasDelegate, _ => OnNavigationShow.InvokeAsync()),
            new("navigationPrev", () => OnNavigationPrev.HasDelegate, _ => OnNavigationPrev.InvokeAsync()),
            new("navigationNext", () => OnNavigationNext.HasDelegate, _ => OnNavigationNext.InvokeAsync()),
            new("paginationRender", () => OnPaginationRender.HasDelegate, _ => OnPaginationRender.InvokeAsync()),
            new("paginationUpdate", () => OnPaginationUpdate.HasDelegate, _ => OnPaginationUpdate.InvokeAsync()),
            new("paginationHide", () => OnPaginationHide.HasDelegate, _ => OnPaginationHide.InvokeAsync()),
            new("paginationShow", () => OnPaginationShow.HasDelegate, _ => OnPaginationShow.InvokeAsync()),
            new("scrollbarDragStart", () => OnScrollbarDragStart.HasDelegate, json => OnScrollbarDragStart.InvokeAsync(AsPointer(json))),
            new("scrollbarDragMove", () => OnScrollbarDragMove.HasDelegate, json => OnScrollbarDragMove.InvokeAsync(AsPointer(json))),
            new("scrollbarDragEnd", () => OnScrollbarDragEnd.HasDelegate, json => OnScrollbarDragEnd.InvokeAsync(AsPointer(json))),
            new("virtualUpdate", () => OnVirtualUpdate.HasDelegate, _ => OnVirtualUpdate.InvokeAsync()),
            new("zoomChange", () => OnZoomChange.HasDelegate, json => OnZoomChange.InvokeAsync(AsPayload<SwiperZoomChange>(json)))
        };
    }

    /// <summary>Every Swiper event this component can deliver, for the surface-coverage test.</summary>
    internal IReadOnlyList<string> KnownEventNames => EventBindings
        .Select(binding => binding.Name)
        .ToArray();

    /// <summary>The event names the host has actually wired up, which are the only ones listened for.</summary>
    private string[] SubscribedEventNames()
    {
        return EventBindings
            .Where(binding => binding.IsSubscribed())
            .Select(binding => binding.Name)
            .ToArray();
    }

    private async Task RaiseAutoplayStart()
    {
        await SetAutoplayRunning(true);
        await OnAutoplayStart.InvokeAsync();
    }

    private async Task RaiseAutoplayStop()
    {
        await SetAutoplayRunning(false);
        await OnAutoplayStop.InvokeAsync();
    }

    /// <summary>
    /// Interop callback for every subscribed Swiper event. Not intended to be called from your code.
    /// </summary>
    /// <param name="name">Swiper's own event name.</param>
    /// <param name="payloadJson">The event's data, or <c>null</c> for the events that carry none.</param>
    [JSInvokable]
    public Task OnSwiperEventInternal(string name, string? payloadJson)
    {
        // An event arriving for a name nothing handles is not an error worth throwing over: a
        // subscription can outlive the render that removed the callback by exactly one interop hop.
        return EventDispatch.TryGetValue(name, out var raise)
            ? raise(payloadJson)
            : Task.CompletedTask;
    }

    private static SwiperPointerEventArgs AsPointer(string? json) => AsPayload<SwiperPointerEventArgs>(json);

    private static double AsDouble(string? json) => TryRead<double>(json);

    private static int AsInt(string? json) => TryRead<int>(json);

    private static string AsString(string? json) => TryRead<string>(json) ?? string.Empty;

    private static T AsPayload<T>(string? json)
        where T : class, new()
    {
        return TryRead<T>(json) ?? new T();
    }

    // The events that carry nothing send the JSON literal "null" rather than no payload at all, so
    // that the interop signature stays the same shape for all of them.
    private static T? TryRead<T>(string? json)
    {
        if (string.IsNullOrEmpty(json) || json == "null")
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json!, PayloadOptions);
    }
}

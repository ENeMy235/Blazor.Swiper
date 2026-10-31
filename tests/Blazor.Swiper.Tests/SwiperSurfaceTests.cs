using System.Text.Json;
using System.Text.RegularExpressions;
using Bunit;
using Shouldly;
using Xunit;
using TestContext = Bunit.TestContext;

namespace Kebechet.Blazor.Swiper.Tests;

/// <summary>
/// Proves the wrapper actually covers Swiper's surface, rather than most of it.
/// </summary>
/// <remarks>
/// The parameter half reads the vendored bundle itself, so it needs no maintenance and fails the
/// moment a re-vendored Swiper adds a parameter. The event half is checked against the list Swiper
/// publishes in its type definitions, which has to be refreshed by hand when the bundle is - the
/// same moment <see cref="PackagingTests"/> already forces attention to.
/// </remarks>
public sealed class SwiperSurfaceTests : IDisposable
{
    private readonly TestContext _context;

    public SwiperSurfaceTests()
    {
        _context = new TestContext();
        _context.JSInterop.SetupModule("./_content/Kebechet.Blazor.Swiper/swiper-interop.js").Mode = JSRuntimeMode.Loose;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    /// <summary>
    /// Parameters the wrapper deliberately does not expose, and why. Anything not here has to have a
    /// <see cref="SwiperOptions"/> member.
    /// </summary>
    private static readonly Dictionary<string, string> IntentionallyUnexposedParameters = new()
    {
        ["modules"] = "The vendored bundle registers every module itself; there is nothing to choose.",
        ["init"] = "The component owns initialization - the element is rendered with init=\"false\" so options can be assigned first.",
        ["swiperElementNodeName"] = "Internal to Swiper Element's own rendering.",
        ["eventsPrefix"] = "Every listener in swiper-interop.js is built from the default prefix; changing it would silence them all.",
        ["createElements"] = "Swiper Element already creates the wrapper and the navigation elements.",
        ["control"] = "Swiper Element's shorthand for controller.control, which SwiperControllerOptions.Control covers."
    };

    [Fact]
    public void SwiperOptions_EveryParameterTheVendoredBundleAccepts_IsExposedOrDeliberatelyNot()
    {
        // Arrange - read the parameter names out of the bundle rather than restating them, so this
        // fails on its own the next time Swiper is re-vendored with something new.
        var parameters = ParameterNamesInBundle();
        var exposed = typeof(SwiperOptions)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var missing = parameters
            .Where(name => !exposed.Contains(name))
            .Where(name => !IntentionallyUnexposedParameters.ContainsKey(name))
            .OrderBy(name => name)
            .ToList();

        // Assert
        parameters.Count.ShouldBeGreaterThan(100, "the parameter list should have been found in the bundle");
        missing.Count.ShouldBe(
            0,
            $"SwiperOptions is missing {missing.Count} of Swiper's parameters: {string.Join(", ", missing)}. " +
            "Add a member, or add it to IntentionallyUnexposedParameters with the reason.");
    }

    [Fact]
    public void SwiperOptions_EveryMemberItExposes_IsAParameterSwiperActuallyHas()
    {
        // Arrange - the other direction, which catches a typo in a member name. A misspelled member
        // serializes to a key Swiper ignores, and the option silently never applies.
        var parameters = ParameterNamesInBundle().ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var unknown = typeof(SwiperOptions)
            .GetProperties()
            .Select(property => property.Name)
            .Where(name => !parameters.Contains(name))
            .OrderBy(name => name)
            .ToList();

        // Assert
        unknown.ShouldBeEmpty($"SwiperOptions exposes members Swiper has no parameter for: {string.Join(", ", unknown)}");
    }

    [Fact]
    public void Swiper_EveryEventSwiperRaises_HasACallback()
    {
        // Arrange
        var cut = _context.RenderComponent<Swiper>();
        var covered = cut.Instance.KnownEventNames.ToHashSet(StringComparer.Ordinal);

        // Act - slideChange is delivered through its own interop callback rather than the generic
        // dispatcher, because ActiveIndex and the two-way binding depend on it arriving whether or
        // not the host wired anything up.
        var missing = SwiperEventNames
            .Where(name => name != "slideChange")
            .Where(name => !covered.Contains(name))
            .OrderBy(name => name)
            .ToList();

        // Assert
        missing.ShouldBeEmpty($"These Swiper events have no callback: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Swiper_EveryCallbackItExposes_IsAnEventSwiperActuallyRaises()
    {
        // Arrange
        var cut = _context.RenderComponent<Swiper>();
        var known = SwiperEventNames.ToHashSet(StringComparer.Ordinal);

        // Act
        var unknown = cut.Instance.KnownEventNames
            .Where(name => !known.Contains(name))
            .OrderBy(name => name)
            .ToList();

        // Assert - a name that is not one of Swiper's listens for a DOM event that never fires.
        unknown.ShouldBeEmpty($"These callbacks are bound to events Swiper does not raise: {string.Join(", ", unknown)}");
    }

    [Fact]
    public void Swiper_EventNames_AreEachBoundOnlyOnce()
    {
        // Arrange
        var cut = _context.RenderComponent<Swiper>();

        // Act
        var duplicated = cut.Instance.KnownEventNames
            .GroupBy(name => name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        // Assert - a name bound twice means one of the two callbacks silently never fires, because
        // the dispatch table keeps whichever entry landed last.
        duplicated.ShouldBeEmpty($"These event names are bound more than once: {string.Join(", ", duplicated)}");
    }

    /// <summary>
    /// Every parameter name the vendored Swiper Element accepts, read from its own parameter list.
    /// </summary>
    /// <remarks>
    /// The element carries the list as a string array it uses both to define its properties and to
    /// derive its observed attributes. Names it can also take as an attribute are prefixed with an
    /// underscore there, which is stripped here.
    /// </remarks>
    private static IReadOnlyCollection<string> ParameterNamesInBundle()
    {
        var bundle = File.ReadAllText(PackagingTests.BundlePath);

        // Found by a member rather than by the variable it is assigned to, since minification renames
        // the variable on every release. "_slidesPerView" appears nowhere else in the bundle.
        var match = Regex.Match(bundle, @"(\[[^\[\]]*""_slidesPerView""[^\[\]]*\])");

        match.Success.ShouldBeTrue("the Swiper Element parameter list was not found in the vendored bundle");

        return JsonSerializer.Deserialize<string[]>(match.Groups[1].Value)!
            .Select(name => name.Replace("_", string.Empty))
            .ToList();
    }

    /// <summary>
    /// Swiper's events, from the type definitions published with Swiper 14.4.0 - the 54 in
    /// <c>types/events.d.ts</c> plus the 23 the modules add.
    /// </summary>
    /// <remarks>
    /// Unlike the parameter list, this cannot be read out of the minified bundle: the events are
    /// raised through per-module aliases of <c>emit</c>, and several are emitted as space-separated
    /// groups (<c>"reachEnd toEdge"</c>), so there is no literal to find. Refresh this list when the
    /// bundle is re-vendored.
    /// </remarks>
    private static readonly string[] SwiperEventNames =
    [
        "activeIndexChange",
        "afterInit",
        "autoplay",
        "autoplayPause",
        "autoplayResume",
        "autoplayStart",
        "autoplayStop",
        "autoplayTimeLeft",
        "beforeDestroy",
        "beforeInit",
        "beforeLoopFix",
        "beforeResize",
        "beforeSlideChangeStart",
        "beforeTransitionStart",
        "breakpoint",
        "changeDirection",
        "click",
        "destroy",
        "doubleClick",
        "doubleTap",
        "fromEdge",
        "hashChange",
        "hashSet",
        "init",
        "keyPress",
        "lock",
        "loopFix",
        "momentumBounce",
        "navigationHide",
        "navigationNext",
        "navigationPrev",
        "navigationShow",
        "observerUpdate",
        "orientationchange",
        "paginationHide",
        "paginationRender",
        "paginationShow",
        "paginationUpdate",
        "progress",
        "reachBeginning",
        "reachEnd",
        "realIndexChange",
        "resize",
        "scroll",
        "scrollbarDragEnd",
        "scrollbarDragMove",
        "scrollbarDragStart",
        "setTransition",
        "setTranslate",
        "slideChange",
        "slideChangeTransitionEnd",
        "slideChangeTransitionStart",
        "slideNextTransitionEnd",
        "slideNextTransitionStart",
        "slidePrevTransitionEnd",
        "slidePrevTransitionStart",
        "slideResetTransitionEnd",
        "slideResetTransitionStart",
        "sliderFirstMove",
        "sliderMove",
        "slidesGridLengthChange",
        "slidesLengthChange",
        "slidesUpdated",
        "snapGridLengthChange",
        "snapIndexChange",
        "tap",
        "toEdge",
        "touchEnd",
        "touchMove",
        "touchMoveOpposite",
        "touchStart",
        "transitionEnd",
        "transitionStart",
        "unlock",
        "update",
        "virtualUpdate",
        "zoomChange"
    ];
}

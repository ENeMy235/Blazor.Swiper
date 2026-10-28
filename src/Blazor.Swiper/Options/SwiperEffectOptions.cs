namespace Kebechet.Blazor.Swiper;

/// <summary>
/// Swiper's <c>fadeEffect</c>, used when <see cref="SwiperOptions.Effect"/> is
/// <see cref="SwiperEffect.Fade"/>.
/// </summary>
public sealed record SwiperFadeEffectOptions
{
    /// <summary>How the outgoing and incoming slides fade. Null = Swiper's default (<see cref="SwiperFadeEffectMode.Default"/>).</summary>
    public SwiperFadeEffectMode? Mode { get; set; }

    /// <summary>
    /// Fade the outgoing slide out as the incoming one fades in, rather than only fading the
    /// incoming one over an opaque stack. Null = Swiper's default (false). Swiper deprecates it
    /// in favour of <see cref="Mode"/> = <see cref="SwiperFadeEffectMode.CrossFade"/>.
    /// </summary>
    public bool? CrossFade { get; set; }
}

/// <summary>
/// Swiper's <c>cubeEffect</c>, used when <see cref="SwiperOptions.Effect"/> is
/// <see cref="SwiperEffect.Cube"/>.
/// </summary>
public sealed record SwiperCubeEffectOptions
{
    /// <summary>Shade the cube's side faces. Null = Swiper's default (true).</summary>
    public bool? SlideShadows { get; set; }

    /// <summary>Draw a shadow underneath the cube. Null = Swiper's default (true).</summary>
    public bool? Shadow { get; set; }

    /// <summary>Distance of that shadow from the cube, in px. Null = Swiper's default (20).</summary>
    public double? ShadowOffset { get; set; }

    /// <summary>Scale of that shadow. Null = Swiper's default (0.94).</summary>
    public double? ShadowScale { get; set; }
}

/// <summary>
/// Swiper's <c>flipEffect</c>, used when <see cref="SwiperOptions.Effect"/> is
/// <see cref="SwiperEffect.Flip"/>.
/// </summary>
public sealed record SwiperFlipEffectOptions
{
    /// <summary>Shade the backs of the flipping slides. Null = Swiper's default (true).</summary>
    public bool? SlideShadows { get; set; }

    /// <summary>Keep the rotation within a half turn. Null = Swiper's default (true).</summary>
    public bool? LimitRotation { get; set; }
}

/// <summary>
/// Swiper's <c>coverflowEffect</c>, used when <see cref="SwiperOptions.Effect"/> is
/// <see cref="SwiperEffect.Coverflow"/>.
/// </summary>
/// <remarks>
/// Coverflow wants several slides on screen and the active one centred, so it is normally paired
/// with <c>SlidesPerView</c> above 1 and <c>CenteredSlides = true</c>.
/// </remarks>
public sealed record SwiperCoverflowEffectOptions
{
    /// <summary>Shade the slides to either side. Null = Swiper's default (true).</summary>
    public bool? SlideShadows { get; set; }

    /// <summary>Rotation of the side slides, in degrees. Null = Swiper's default (50).</summary>
    public double? Rotate { get; set; }

    /// <summary>Space between slides, in px or as a percentage string. Null = Swiper's default (0).</summary>
    public SwiperLength? Stretch { get; set; }

    /// <summary>How far back the side slides sit, in px. Null = Swiper's default (100).</summary>
    public double? Depth { get; set; }

    /// <summary>Scale applied to the side slides. Null = Swiper's default (1).</summary>
    public double? Scale { get; set; }

    /// <summary>Multiplier applied to all of the above. Null = Swiper's default (1).</summary>
    public double? Modifier { get; set; }
}

/// <summary>
/// Swiper's <c>cardsEffect</c>, used when <see cref="SwiperOptions.Effect"/> is
/// <see cref="SwiperEffect.Cards"/>.
/// </summary>
public sealed record SwiperCardsEffectOptions
{
    /// <summary>Shade the cards behind the active one. Null = Swiper's default (true).</summary>
    public bool? SlideShadows { get; set; }

    /// <summary>Whether the stacked cards are rotated at all. Null = Swiper's default (true).</summary>
    public bool? Rotate { get; set; }

    /// <summary>Rotation added per card down the stack, in degrees. Null = Swiper's default (2).</summary>
    public double? PerSlideRotate { get; set; }

    /// <summary>Offset added per card down the stack, in px. Null = Swiper's default (8).</summary>
    public double? PerSlideOffset { get; set; }
}

/// <summary>One side's transform for <see cref="SwiperCreativeEffectOptions"/>.</summary>
public sealed record SwiperCreativeEffectTransform
{
    /// <summary>Translate X, Y and Z. Each entry is px or a percentage string.</summary>
    public SwiperLength[]? Translate { get; set; }

    /// <summary>Rotate X, Y and Z, in degrees.</summary>
    public double[]? Rotate { get; set; }

    /// <summary>Slide opacity.</summary>
    public double? Opacity { get; set; }

    /// <summary>Slide scale.</summary>
    public double? Scale { get; set; }

    /// <summary>Whether the slide is shaded.</summary>
    public bool? Shadow { get; set; }

    /// <summary>CSS transform origin, e.g. <c>"left bottom"</c>.</summary>
    public string? Origin { get; set; }
}

/// <summary>
/// Swiper's <c>creativeEffect</c>, used when <see cref="SwiperOptions.Effect"/> is
/// <see cref="SwiperEffect.Creative"/>. Every other effect is a preset of this one.
/// </summary>
public sealed record SwiperCreativeEffectOptions
{
    /// <summary>Transform applied to the slides before the active one.</summary>
    public SwiperCreativeEffectTransform? Prev { get; set; }

    /// <summary>Transform applied to the slides after the active one.</summary>
    public SwiperCreativeEffectTransform? Next { get; set; }

    /// <summary>
    /// How many slides out from the active one keep progressing; beyond that they share a state.
    /// Null = Swiper's default (1).
    /// </summary>
    public double? LimitProgress { get; set; }

    /// <summary>Split shadow opacity per slide. Null = Swiper's default (false).</summary>
    public bool? ShadowPerProgress { get; set; }

    /// <summary>Multiplier applied to the transforms and opacity. Null = Swiper's default (1).</summary>
    public double? ProgressMultiplier { get; set; }

    /// <summary>Needed when the transforms use Z translation or X/Y rotation. Null = Swiper's default (true).</summary>
    public bool? Perspective { get; set; }
}

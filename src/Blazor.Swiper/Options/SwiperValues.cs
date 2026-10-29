using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kebechet.Blazor.Swiper;

/// <summary>Values for <see cref="SwiperOptions.Direction"/>.</summary>
[JsonConverter(typeof(SwiperEnumConverter))]
public enum SwiperDirection
{
    /// <summary>Slides move left/right.</summary>
    Horizontal,

    /// <summary>Slides move up/down. Needs an explicit height on the <c>Swiper</c> element.</summary>
    Vertical
}

/// <summary>Values for <see cref="SwiperOptions.Effect"/>. Each one reads its own options record.</summary>
[JsonConverter(typeof(SwiperEnumConverter))]
public enum SwiperEffect
{
    /// <summary>The default sliding track.</summary>
    Slide,

    /// <summary>Cross-dissolve. Configured by <see cref="SwiperOptions.FadeEffect"/>.</summary>
    Fade,

    /// <summary>Slides wrap a rotating cube. Configured by <see cref="SwiperOptions.CubeEffect"/>.</summary>
    Cube,

    /// <summary>Slides fan out in perspective. Configured by <see cref="SwiperOptions.CoverflowEffect"/>.</summary>
    Coverflow,

    /// <summary>Slides flip over. Configured by <see cref="SwiperOptions.FlipEffect"/>.</summary>
    Flip,

    /// <summary>Fully custom per-slide transforms. Configured by <see cref="SwiperOptions.CreativeEffect"/>.</summary>
    Creative,

    /// <summary>A stack of cards. Configured by <see cref="SwiperOptions.CardsEffect"/>.</summary>
    Cards
}

/// <summary>Values for <see cref="SwiperOptions.TouchEventsTarget"/>.</summary>
[JsonConverter(typeof(SwiperEnumConverter))]
public enum SwiperTouchEventsTarget
{
    /// <summary>Listen on the slides wrapper.</summary>
    Wrapper,

    /// <summary>Listen on the swiper container itself.</summary>
    Container
}

/// <summary>Values for <see cref="SwiperPaginationOptions.Type"/>.</summary>
[JsonConverter(typeof(SwiperEnumConverter))]
public enum SwiperPaginationType
{
    /// <summary>One bullet per slide.</summary>
    Bullets,

    /// <summary>"3 / 8" style counter.</summary>
    Fraction,

    /// <summary>A progress bar.</summary>
    Progressbar,

    /// <summary>Rendered by <see cref="SwiperPaginationOptions.RenderCustomTemplate"/>.</summary>
    Custom
}

/// <summary>Values for <see cref="SwiperGridOptions.Fill"/>.</summary>
[JsonConverter(typeof(SwiperEnumConverter))]
public enum SwiperGridFill
{
    /// <summary>Fill column by column.</summary>
    Column,

    /// <summary>Fill row by row.</summary>
    Row
}

/// <summary>Values for <see cref="SwiperControllerOptions.By"/>.</summary>
[JsonConverter(typeof(SwiperEnumConverter))]
public enum SwiperControlBy
{
    /// <summary>The controlled slider moves slide for slide.</summary>
    Slide,

    /// <summary>The controlled slider mirrors the container translate, proportionally.</summary>
    Container
}

/// <summary>Values for <see cref="SwiperFadeEffectOptions.Mode"/>.</summary>
[JsonConverter(typeof(SwiperFadeEffectModeConverter))]
public enum SwiperFadeEffectMode
{
    /// <summary>
    /// Only the slide stacked on top fades - the incoming one in when moving forward, the active one out when
    /// moving back - while the other stays fully opaque beneath it.
    /// </summary>
    Default,

    /// <summary>Both slides fade at once.</summary>
    CrossFade,

    /// <summary>The active slide fades out completely before the incoming one starts to fade in.</summary>
    OutIn
}

/// <summary>Values for <see cref="SwiperOptions.EdgeSwipeDetection"/>.</summary>
[JsonConverter(typeof(SwiperEdgeSwipeDetectionConverter))]
public enum SwiperEdgeSwipeDetection
{
    /// <summary>Swiper handles swipes that start at the screen edge like any other.</summary>
    Disabled,

    /// <summary>Swipes starting within <see cref="SwiperOptions.EdgeSwipeThreshold"/> of the edge are released to the system.</summary>
    Enabled,

    /// <summary>The system's swipe-back navigation is prevented instead.</summary>
    Prevent
}

/// <summary>
/// A <c>slidesPerView</c> value: either a slide count - fractional values are allowed, so
/// <c>2.5</c> peeks at the next slide - or <see cref="Auto"/>, which sizes each slide from its
/// own content and fits as many as the container holds.
/// </summary>
[JsonConverter(typeof(SwiperSlidesPerViewConverter))]
public readonly struct SwiperSlidesPerView : IEquatable<SwiperSlidesPerView>
{
    private readonly double _count;
    private readonly bool _isAuto;

    private SwiperSlidesPerView(double count, bool isAuto)
    {
        _count = count;
        _isAuto = isAuto;
    }

    /// <summary>Slide widths come from the slides themselves rather than from a count.</summary>
    public static SwiperSlidesPerView Auto => new(0, true);

    /// <summary>Whether this is <see cref="Auto"/> rather than a count.</summary>
    public bool IsAuto => _isAuto;

    /// <summary>The slide count, meaningless when <see cref="IsAuto"/> is true.</summary>
    public double Count => _count;

    /// <summary>Builds a value from a slide count.</summary>
    /// <param name="count">The number of slides visible at once.</param>
    public static implicit operator SwiperSlidesPerView(double count) => new(count, false);

    /// <inheritdoc />
    public bool Equals(SwiperSlidesPerView other) => _isAuto == other._isAuto && _count.Equals(other._count);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SwiperSlidesPerView other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _isAuto ? -1 : _count.GetHashCode();

    /// <summary>Compares two values.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    public static bool operator ==(SwiperSlidesPerView left, SwiperSlidesPerView right) => left.Equals(right);

    /// <summary>Compares two values.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    public static bool operator !=(SwiperSlidesPerView left, SwiperSlidesPerView right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => _isAuto ? "auto" : _count.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// A length Swiper accepts either as a number of pixels or as a CSS-ish string - <c>16</c> and
/// <c>"10%"</c> are both valid for <see cref="SwiperOptions.SpaceBetween"/>, and
/// <see cref="SwiperScrollbarOptions.DragSize"/> additionally takes <see cref="Auto"/>.
/// </summary>
[JsonConverter(typeof(SwiperLengthConverter))]
public readonly struct SwiperLength : IEquatable<SwiperLength>
{
    private readonly double _pixels;
    private readonly string? _text;

    private SwiperLength(double pixels, string? text)
    {
        _pixels = pixels;
        _text = text;
    }

    /// <summary>The length is decided by the browser.</summary>
    public static SwiperLength Auto => new(0, "auto");

    /// <summary>The literal value when the length was given as a string, otherwise null.</summary>
    public string? Text => _text;

    /// <summary>The pixel value, meaningless when <see cref="Text"/> is set.</summary>
    public double Pixels => _pixels;

    /// <summary>Builds a length from a pixel count.</summary>
    /// <param name="pixels">The length in px.</param>
    public static implicit operator SwiperLength(double pixels) => new(pixels, null);

    /// <summary>Builds a length from a CSS value such as <c>"10%"</c>.</summary>
    /// <param name="value">The CSS length.</param>
    public static implicit operator SwiperLength(string value) => new(0, value);

    /// <inheritdoc />
    public bool Equals(SwiperLength other) => _text == other._text && _pixels.Equals(other._pixels);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SwiperLength other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _text?.GetHashCode() ?? _pixels.GetHashCode();

    /// <summary>Compares two lengths.</summary>
    /// <param name="left">The first length.</param>
    /// <param name="right">The second length.</param>
    public static bool operator ==(SwiperLength left, SwiperLength right) => left.Equals(right);

    /// <summary>Compares two lengths.</summary>
    /// <param name="left">The first length.</param>
    /// <param name="right">The second length.</param>
    public static bool operator !=(SwiperLength left, SwiperLength right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => _text ?? _pixels.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>Values for <see cref="SwiperOptions.BreakpointsBase"/>.</summary>
public static class SwiperBreakpointsBase
{
    /// <summary>Breakpoint keys are window widths.</summary>
    public const string Window = "window";

    /// <summary>Breakpoint keys are the swiper container's own width.</summary>
    public const string Container = "container";
}

/// <summary>
/// Base for the option records of Swiper modules that can be switched off outright.
/// </summary>
/// <remarks>
/// Every one of these has an implicit conversion from <see cref="bool"/>, so
/// <c>Pagination = true</c> reads as well as it did when these were plain flags. A record whose
/// <see cref="Enabled"/> is <c>false</c> is sent to Swiper as the literal <c>false</c> rather than
/// as an object, because Swiper Element treats <em>any</em> object as "module wanted" and would
/// build the module's elements before the module itself declined to run.
/// </remarks>
public abstract record SwiperToggleableOptions
{
    /// <summary>Whether the module runs. Null leaves Swiper's own default.</summary>
    public bool? Enabled { get; set; }
}

/// <summary>Serializes enums the way Swiper spells them, i.e. camel-cased.</summary>
internal sealed class SwiperEnumConverter : JsonStringEnumConverter
{
    public SwiperEnumConverter()
        : base(JsonNamingPolicy.CamelCase)
    {
    }
}

/// <summary>Writes <see cref="SwiperSlidesPerView"/> as Swiper's <c>number | 'auto'</c>.</summary>
internal sealed class SwiperSlidesPerViewConverter : JsonConverter<SwiperSlidesPerView>
{
    public override SwiperSlidesPerView Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType == JsonTokenType.String
            ? SwiperSlidesPerView.Auto
            : reader.GetDouble();
    }

    public override void Write(Utf8JsonWriter writer, SwiperSlidesPerView value, JsonSerializerOptions options)
    {
        if (value.IsAuto)
        {
            writer.WriteStringValue("auto");
            return;
        }

        writer.WriteNumberValue(value.Count);
    }
}

/// <summary>Writes <see cref="SwiperLength"/> as Swiper's <c>number | string</c>.</summary>
internal sealed class SwiperLengthConverter : JsonConverter<SwiperLength>
{
    public override SwiperLength Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? string.Empty
            : reader.GetDouble();
    }

    public override void Write(Utf8JsonWriter writer, SwiperLength value, JsonSerializerOptions options)
    {
        if (value.Text is not null)
        {
            writer.WriteStringValue(value.Text);
            return;
        }

        writer.WriteNumberValue(value.Pixels);
    }
}

/// <summary>Writes <see cref="SwiperEdgeSwipeDetection"/> as Swiper's <c>boolean | 'prevent'</c>.</summary>
internal sealed class SwiperEdgeSwipeDetectionConverter : JsonConverter<SwiperEdgeSwipeDetection>
{
    public override SwiperEdgeSwipeDetection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return SwiperEdgeSwipeDetection.Prevent;
        }

        return reader.GetBoolean() ? SwiperEdgeSwipeDetection.Enabled : SwiperEdgeSwipeDetection.Disabled;
    }

    public override void Write(Utf8JsonWriter writer, SwiperEdgeSwipeDetection value, JsonSerializerOptions options)
    {
        if (value == SwiperEdgeSwipeDetection.Prevent)
        {
            writer.WriteStringValue("prevent");
            return;
        }

        writer.WriteBooleanValue(value == SwiperEdgeSwipeDetection.Enabled);
    }
}

/// <summary>Writes <see cref="SwiperFadeEffectMode"/> as Swiper's kebab-case strings, such as <c>'cross-fade'</c>.</summary>
internal sealed class SwiperFadeEffectModeConverter : JsonConverter<SwiperFadeEffectMode>
{
    public override SwiperFadeEffectMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetString() switch
        {
            "cross-fade" => SwiperFadeEffectMode.CrossFade,
            "out-in" => SwiperFadeEffectMode.OutIn,
            _ => SwiperFadeEffectMode.Default
        };
    }

    public override void Write(Utf8JsonWriter writer, SwiperFadeEffectMode value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            SwiperFadeEffectMode.CrossFade => "cross-fade",
            SwiperFadeEffectMode.OutIn => "out-in",
            _ => "default"
        });
    }
}

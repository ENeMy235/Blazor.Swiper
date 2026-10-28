using System.Text.Json;
using Shouldly;
using Xunit;

namespace Kebechet.Blazor.Swiper.Tests;

/// <summary>
/// What the options actually become on the wire.
/// </summary>
/// <remarks>
/// The interop serializer camel-cases property names, so a member's name IS its Swiper parameter
/// name and nothing in between checks that. These assert the shapes Swiper cannot read from a plain
/// C# value - its string unions, its <c>number | 'auto'</c> parameters and its module objects.
/// </remarks>
public sealed class SwiperOptionsTests
{
    [Fact]
    public void Serialize_Enum_BecomesTheLowerCaseStringSwiperExpects()
    {
        // Arrange
        var options = new SwiperOptions { Direction = SwiperDirection.Vertical, Effect = SwiperEffect.Coverflow };

        // Act
        var json = Swiper.SerializeOptions(options);

        // Assert
        json.Contains("\"direction\":\"vertical\"").ShouldBeTrue($"direction was not camel-cased: {json}");
        json.Contains("\"effect\":\"coverflow\"").ShouldBeTrue($"effect was not camel-cased: {json}");
    }

    [Fact]
    public void Serialize_SlidesPerViewAsACount_StaysANumber()
    {
        // Arrange
        var options = new SwiperOptions { SlidesPerView = 2.5 };

        // Act
        var json = Swiper.SerializeOptions(options);

        // Assert - a fractional count is how a slider peeks at the next slide, so it must not round.
        json.Contains("\"slidesPerView\":2.5").ShouldBeTrue(json);
    }

    [Fact]
    public void Serialize_SlidesPerViewAuto_BecomesTheStringSwiperMatchesOn()
    {
        // Arrange - Swiper's parameter is `number | 'auto'`, and 'auto' is not a number in disguise:
        // it means the slides size themselves from their own content.
        var options = new SwiperOptions { SlidesPerView = SwiperSlidesPerView.Auto };

        // Act
        var json = Swiper.SerializeOptions(options);

        // Assert
        json.Contains("\"slidesPerView\":\"auto\"").ShouldBeTrue(json);
    }

    [Fact]
    public void Serialize_SpaceBetweenInPixels_StaysANumber()
    {
        Swiper.SerializeOptions(new SwiperOptions { SpaceBetween = 16 })
            .Contains("\"spaceBetween\":16")
            .ShouldBeTrue();
    }

    [Fact]
    public void Serialize_SpaceBetweenAsAPercentage_StaysAString()
    {
        // Swiper accepts either, and a percentage written as a number would be sixteen pixels.
        Swiper.SerializeOptions(new SwiperOptions { SpaceBetween = "10%" })
            .Contains("\"spaceBetween\":\"10%\"")
            .ShouldBeTrue();
    }

    [Fact]
    public void Serialize_ModuleTurnedOnWithABool_BecomesTheModulesOptionsObject()
    {
        // Arrange - the implicit conversion is what keeps `Pagination = true` reading well now that
        // the member is a record rather than a flag.
        var options = new SwiperOptions { Pagination = true, Navigation = true, Scrollbar = true, Keyboard = true };

        // Act
        using var json = JsonDocument.Parse(Swiper.SerializeOptions(options));

        // Assert
        foreach (var module in new[] { "pagination", "navigation", "scrollbar", "keyboard" })
        {
            json.RootElement.GetProperty(module).GetProperty("enabled").GetBoolean()
                .ShouldBeTrue($"{module} should have been turned on by the implicit conversion from bool");
        }
    }

    [Fact]
    public void Serialize_ModuleTurnedOffWithABool_SaysSoExplicitly()
    {
        // Arrange - false has to survive as false rather than becoming absence, because the interop
        // collapses an explicitly disabled module to the literal `false` Swiper Element understands.
        var options = new SwiperOptions { Pagination = false };

        // Act
        using var json = JsonDocument.Parse(Swiper.SerializeOptions(options));

        // Assert
        json.RootElement.GetProperty("pagination").GetProperty("enabled").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public void Serialize_ModuleConfiguredInFull_KeepsEveryMemberItSet()
    {
        // Arrange
        var options = new SwiperOptions
        {
            Pagination = new SwiperPaginationOptions
            {
                Type = SwiperPaginationType.Fraction,
                Clickable = true,
                DynamicMainBullets = 3
            }
        };

        // Act
        var json = Swiper.SerializeOptions(options);

        // Assert
        json.Contains("\"type\":\"fraction\"").ShouldBeTrue(json);
        json.Contains("\"clickable\":true").ShouldBeTrue(json);
        json.Contains("\"dynamicMainBullets\":3").ShouldBeTrue(json);
    }

    [Fact]
    public void Serialize_EdgeSwipeDetection_BecomesSwipersBoolOrPreventUnion()
    {
        Swiper.SerializeOptions(new SwiperOptions { EdgeSwipeDetection = SwiperEdgeSwipeDetection.Prevent })
            .Contains("\"edgeSwipeDetection\":\"prevent\"")
            .ShouldBeTrue();

        Swiper.SerializeOptions(new SwiperOptions { EdgeSwipeDetection = SwiperEdgeSwipeDetection.Enabled })
            .Contains("\"edgeSwipeDetection\":true")
            .ShouldBeTrue();

        Swiper.SerializeOptions(new SwiperOptions { EdgeSwipeDetection = SwiperEdgeSwipeDetection.Disabled })
            .Contains("\"edgeSwipeDetection\":false")
            .ShouldBeTrue();
    }

    [Theory]
    [InlineData(SwiperFadeEffectMode.Default, "default")]
    [InlineData(SwiperFadeEffectMode.CrossFade, "cross-fade")]
    [InlineData(SwiperFadeEffectMode.OutIn, "out-in")]
    public void Serialize_FadeEffectMode_BecomesSwipersKebabCaseString(SwiperFadeEffectMode mode, string expected)
    {
        Swiper.SerializeOptions(new SwiperOptions { FadeEffect = new() { Mode = mode } })
            .Contains($"\"fadeEffect\":{{\"mode\":\"{expected}\"")
            .ShouldBeTrue();
    }

    [Fact]
    public void Serialize_LazyPreloadOff_IsWrittenAsFalse()
    {
        Swiper.SerializeOptions(new SwiperOptions { LazyPreload = false })
            .Contains("\"lazyPreload\":false")
            .ShouldBeTrue();
    }

    [Fact]
    public void Serialize_Breakpoints_KeepTheirKeysExactly()
    {
        // Arrange - the keys are widths in px or ratios like "@1.5", and Swiper matches on them
        // literally, so any reshaping of the key would silently never match.
        var options = new SwiperOptions
        {
            Breakpoints = new Dictionary<string, SwiperOptions>
            {
                ["640"] = new() { SlidesPerView = 2 },
                ["@1.5"] = new() { SlidesPerView = 3 }
            }
        };

        // Act
        var json = Swiper.SerializeOptions(options);

        // Assert
        json.Contains("\"640\":").ShouldBeTrue(json);
        json.Contains("\"@1.5\":").ShouldBeTrue(json);
    }

    [Fact]
    public void Serialize_UnsetMember_IsWrittenAsNullForTheInteropToDrop()
    {
        // The whole "unset keeps Swiper's default" contract rests on this: C# writes null, and
        // swiper-policy.js drops it before it can blank a default. If nulls stopped being written the
        // drop would have nothing to do, but if they stopped being dropped every default would go.
        var json = Swiper.SerializeOptions(new SwiperOptions());

        json.Contains("\"slidesPerView\":null").ShouldBeTrue(json);
        json.Contains("\"loop\":null").ShouldBeTrue(json);
    }

    [Fact]
    public void Serialize_CreativeEffectTransforms_KeepTheirArrayShape()
    {
        // Arrange - Swiper reads translate as [x, y, z], where each entry is px or a percentage.
        var options = new SwiperOptions
        {
            Effect = SwiperEffect.Creative,
            CreativeEffect = new SwiperCreativeEffectOptions
            {
                Prev = new SwiperCreativeEffectTransform { Translate = ["-20%", 0, -1], Rotate = [0, 0, -8] }
            }
        };

        // Act
        var json = Swiper.SerializeOptions(options);

        // Assert
        json.Contains("\"translate\":[\"-20%\",0,-1]").ShouldBeTrue(json);
        json.Contains("\"rotate\":[0,0,-8]").ShouldBeTrue(json);
    }

    [Fact]
    public void With_ChangingOneMember_ProducesADifferentSnapshot()
    {
        // Arrange - reactivity is built on comparing these snapshots, so a `with` that did not change
        // the serialization would mean a changed parameter never reached the slider.
        var options = new SwiperOptions { SlidesPerView = 1 };

        // Act
        var changed = options with { SlidesPerView = 3 };

        // Assert
        Swiper.SerializeOptions(changed).ShouldNotBe(Swiper.SerializeOptions(options));
    }

    [Fact]
    public void With_ChangingNothing_ProducesTheSameSnapshot()
    {
        // Arrange - and the other way: a re-render that rebuilds an identical options record must not
        // look like a change, or every render would push an update to the slider.
        var options = new SwiperOptions { SlidesPerView = 2, Pagination = new SwiperPaginationOptions { Clickable = true } };

        // Act
        var rebuilt = new SwiperOptions { SlidesPerView = 2, Pagination = new SwiperPaginationOptions { Clickable = true } };

        // Assert
        Swiper.SerializeOptions(rebuilt).ShouldBe(Swiper.SerializeOptions(options));
    }
}

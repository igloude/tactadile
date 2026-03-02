using Tactadile.Config;
using Tactadile.Core;
using Tactadile.Native;

namespace Tactadile.Tests;

public sealed class ZoneCalculatorTests
{
    // Standard 1920x1080 work area (with taskbar at bottom: 0,0 to 1920,1040)
    private static readonly RECT StandardWorkArea = new()
    {
        Left = 0, Top = 0, Right = 1920, Bottom = 1040
    };

    [Fact]
    public void LeftHalf_CoversLeftHalfOfWorkArea()
    {
        var zone = ZoneCalculator.Calculate(ZoneType.LeftHalf, StandardWorkArea);
        Assert.Equal(0, zone.X);
        Assert.Equal(0, zone.Y);
        Assert.Equal(960, zone.Width);
        Assert.Equal(1040, zone.Height);
    }

    [Fact]
    public void RightHalf_CoversRightHalfOfWorkArea()
    {
        var zone = ZoneCalculator.Calculate(ZoneType.RightHalf, StandardWorkArea);
        Assert.Equal(960, zone.X);
        Assert.Equal(0, zone.Y);
        Assert.Equal(960, zone.Width);
        Assert.Equal(1040, zone.Height);
    }

    [Fact]
    public void TopHalf_CoversTopHalfOfWorkArea()
    {
        var zone = ZoneCalculator.Calculate(ZoneType.TopHalf, StandardWorkArea);
        Assert.Equal(0, zone.X);
        Assert.Equal(0, zone.Y);
        Assert.Equal(1920, zone.Width);
        Assert.Equal(520, zone.Height);
    }

    [Fact]
    public void BottomHalf_CoversBottomHalfOfWorkArea()
    {
        var zone = ZoneCalculator.Calculate(ZoneType.BottomHalf, StandardWorkArea);
        Assert.Equal(0, zone.X);
        Assert.Equal(520, zone.Y);
        Assert.Equal(1920, zone.Width);
        Assert.Equal(520, zone.Height);
    }

    [Fact]
    public void TopLeft_CoversQuarter()
    {
        var zone = ZoneCalculator.Calculate(ZoneType.TopLeft, StandardWorkArea);
        Assert.Equal(0, zone.X);
        Assert.Equal(0, zone.Y);
        Assert.Equal(960, zone.Width);
        Assert.Equal(520, zone.Height);
    }

    [Fact]
    public void TopRight_CoversQuarter()
    {
        var zone = ZoneCalculator.Calculate(ZoneType.TopRight, StandardWorkArea);
        Assert.Equal(960, zone.X);
        Assert.Equal(0, zone.Y);
        Assert.Equal(960, zone.Width);
        Assert.Equal(520, zone.Height);
    }

    [Fact]
    public void BottomLeft_CoversQuarter()
    {
        var zone = ZoneCalculator.Calculate(ZoneType.BottomLeft, StandardWorkArea);
        Assert.Equal(0, zone.X);
        Assert.Equal(520, zone.Y);
        Assert.Equal(960, zone.Width);
        Assert.Equal(520, zone.Height);
    }

    [Fact]
    public void BottomRight_CoversQuarter()
    {
        var zone = ZoneCalculator.Calculate(ZoneType.BottomRight, StandardWorkArea);
        Assert.Equal(960, zone.X);
        Assert.Equal(520, zone.Y);
        Assert.Equal(960, zone.Width);
        Assert.Equal(520, zone.Height);
    }

    [Fact]
    public void LeftThird_CoversOneThird()
    {
        var zone = ZoneCalculator.Calculate(ZoneType.LeftThird, StandardWorkArea);
        Assert.Equal(0, zone.X);
        Assert.Equal(0, zone.Y);
        Assert.Equal(640, zone.Width);
        Assert.Equal(1040, zone.Height);
    }

    [Fact]
    public void LeftTwoThirds_CoversTwoThirds()
    {
        var zone = ZoneCalculator.Calculate(ZoneType.LeftTwoThirds, StandardWorkArea);
        Assert.Equal(0, zone.X);
        Assert.Equal(0, zone.Y);
        Assert.Equal(1280, zone.Width);
        Assert.Equal(1040, zone.Height);
    }

    [Fact]
    public void RightThird_CoversOneThird()
    {
        var zone = ZoneCalculator.Calculate(ZoneType.RightThird, StandardWorkArea);
        Assert.Equal(1280, zone.X);
        Assert.Equal(0, zone.Y);
        Assert.Equal(640, zone.Width);
        Assert.Equal(1040, zone.Height);
    }

    [Fact]
    public void RightTwoThirds_CoversTwoThirds()
    {
        var zone = ZoneCalculator.Calculate(ZoneType.RightTwoThirds, StandardWorkArea);
        Assert.Equal(640, zone.X);
        Assert.Equal(0, zone.Y);
        Assert.Equal(1280, zone.Width);
        Assert.Equal(1040, zone.Height);
    }

    [Fact]
    public void Centered_IsTwoThirdsSizeCentered()
    {
        var zone = ZoneCalculator.Calculate(ZoneType.Centered, StandardWorkArea);

        // 2/3 of 1920 = 1280, 2/3 of 1040 = 693 (int truncation)
        Assert.Equal(1280, zone.Width);
        Assert.Equal(693, zone.Height);

        // Centered: offset = (total - size) / 2
        Assert.Equal((1920 - 1280) / 2, zone.X);
        Assert.Equal((1040 - 693) / 2, zone.Y);
    }

    [Fact]
    public void AllZones_CoverFullHeight_ExceptHalvesAndQuarters()
    {
        var fullHeightZones = new[]
        {
            ZoneType.LeftHalf, ZoneType.RightHalf,
            ZoneType.LeftThird, ZoneType.LeftTwoThirds,
            ZoneType.RightThird, ZoneType.RightTwoThirds
        };

        foreach (var zoneType in fullHeightZones)
        {
            var zone = ZoneCalculator.Calculate(zoneType, StandardWorkArea);
            Assert.Equal(1040, zone.Height);
        }
    }

    [Fact]
    public void LeftAndRightHalves_CoverEntireWidth()
    {
        var left = ZoneCalculator.Calculate(ZoneType.LeftHalf, StandardWorkArea);
        var right = ZoneCalculator.Calculate(ZoneType.RightHalf, StandardWorkArea);

        Assert.Equal(0, left.X);
        Assert.Equal(left.Width, right.X);
        Assert.Equal(1920, left.Width + right.Width);
    }

    [Fact]
    public void TopAndBottomHalves_CoverEntireHeight()
    {
        var top = ZoneCalculator.Calculate(ZoneType.TopHalf, StandardWorkArea);
        var bottom = ZoneCalculator.Calculate(ZoneType.BottomHalf, StandardWorkArea);

        Assert.Equal(0, top.Y);
        Assert.Equal(top.Height, bottom.Y);
        Assert.Equal(1040, top.Height + bottom.Height);
    }

    [Fact]
    public void OffsetWorkArea_CalculatesRelativeToOrigin()
    {
        // Second monitor at (1920,0)
        var workArea = new RECT { Left = 1920, Top = 0, Right = 3840, Bottom = 1080 };
        var zone = ZoneCalculator.Calculate(ZoneType.LeftHalf, workArea);

        Assert.Equal(1920, zone.X);
        Assert.Equal(0, zone.Y);
        Assert.Equal(960, zone.Width);
        Assert.Equal(1080, zone.Height);
    }

    // FindClosestZone tests

    [Fact]
    public void FindClosestZone_ExactMatch_ReturnsCorrectZone()
    {
        // Create a window rect that exactly matches LeftHalf
        var windowRect = new RECT { Left = 0, Top = 0, Right = 960, Bottom = 1040 };
        var result = ZoneCalculator.FindClosestZone(windowRect, StandardWorkArea);
        Assert.Equal(ZoneType.LeftHalf, result);
    }

    [Fact]
    public void FindClosestZone_CloseToRightHalf_ReturnsRightHalf()
    {
        // Window approximately at right half (off by a few pixels)
        var windowRect = new RECT { Left = 962, Top = 2, Right = 1922, Bottom = 1042 };
        var result = ZoneCalculator.FindClosestZone(windowRect, StandardWorkArea);
        Assert.Equal(ZoneType.RightHalf, result);
    }

    [Fact]
    public void FindClosestZone_FullScreen_ReturnsClosestToFull()
    {
        // Fullscreen window — no exact "fullscreen" zone, should match one of them
        var windowRect = new RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };
        var result = ZoneCalculator.FindClosestZone(windowRect, StandardWorkArea);
        // Any zone is acceptable as long as no crash
        Assert.True(Enum.IsDefined(result));
    }

    // GetFriendlyName tests

    [Theory]
    [InlineData(ZoneType.Centered, "Centered")]
    [InlineData(ZoneType.TopHalf, "Top Half")]
    [InlineData(ZoneType.BottomHalf, "Bottom Half")]
    [InlineData(ZoneType.TopLeft, "Top Left")]
    [InlineData(ZoneType.TopRight, "Top Right")]
    [InlineData(ZoneType.BottomLeft, "Bottom Left")]
    [InlineData(ZoneType.BottomRight, "Bottom Right")]
    [InlineData(ZoneType.LeftThird, "Left 1/3")]
    [InlineData(ZoneType.LeftHalf, "Left Half")]
    [InlineData(ZoneType.LeftTwoThirds, "Left 2/3")]
    [InlineData(ZoneType.RightThird, "Right 1/3")]
    [InlineData(ZoneType.RightHalf, "Right Half")]
    [InlineData(ZoneType.RightTwoThirds, "Right 2/3")]
    public void GetFriendlyName_ReturnsExpectedName(ZoneType zone, string expected)
    {
        Assert.Equal(expected, ZoneCalculator.GetFriendlyName(zone));
    }

    [Fact]
    public void AllZoneTypes_HaveCalculations()
    {
        foreach (var zone in Enum.GetValues<ZoneType>())
        {
            var rect = ZoneCalculator.Calculate(zone, StandardWorkArea);
            Assert.True(rect.Width > 0, $"Zone {zone} has zero width");
            Assert.True(rect.Height > 0, $"Zone {zone} has zero height");
        }
    }
}

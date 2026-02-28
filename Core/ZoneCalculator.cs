using Tactadile.Config;
using Tactadile.Native;

namespace Tactadile.Core;

public static class ZoneCalculator
{
    public readonly record struct ZoneRect(int X, int Y, int Width, int Height);

    /// <summary>
    /// Computes the pixel rect for a zone within the given monitor work area.
    /// Work area values are already DPI-aware (provided by the OS).
    /// </summary>
    public static ZoneRect Calculate(ZoneType zone, RECT workArea)
    {
        int x = workArea.Left;
        int y = workArea.Top;
        int w = workArea.Width;
        int h = workArea.Height;

        return zone switch
        {
            ZoneType.Centered       => Centered(x, y, w, h),
            ZoneType.TopHalf        => new(x, y, w, h / 2),
            ZoneType.BottomHalf     => new(x, y + h / 2, w, h - h / 2),
            ZoneType.TopLeft        => new(x, y, w / 2, h / 2),
            ZoneType.TopRight       => new(x + w / 2, y, w - w / 2, h / 2),
            ZoneType.BottomLeft     => new(x, y + h / 2, w / 2, h - h / 2),
            ZoneType.BottomRight    => new(x + w / 2, y + h / 2, w - w / 2, h - h / 2),
            ZoneType.LeftThird      => new(x, y, w / 3, h),
            ZoneType.LeftHalf       => new(x, y, w / 2, h),
            ZoneType.LeftTwoThirds  => new(x, y, w * 2 / 3, h),
            ZoneType.RightThird     => new(x + w - w / 3, y, w / 3, h),
            ZoneType.RightHalf      => new(x + w / 2, y, w - w / 2, h),
            ZoneType.RightTwoThirds => new(x + w - w * 2 / 3, y, w * 2 / 3, h),
            _                       => new(x, y, w, h)
        };
    }

    private static ZoneRect Centered(int x, int y, int w, int h)
    {
        int cw = w * 2 / 3;
        int ch = h * 2 / 3;
        return new(x + (w - cw) / 2, y + (h - ch) / 2, cw, ch);
    }

    public static string GetFriendlyName(ZoneType zone) => zone switch
    {
        ZoneType.Centered       => "Centered",
        ZoneType.TopHalf        => "Top Half",
        ZoneType.BottomHalf     => "Bottom Half",
        ZoneType.TopLeft        => "Top Left",
        ZoneType.TopRight       => "Top Right",
        ZoneType.BottomLeft     => "Bottom Left",
        ZoneType.BottomRight    => "Bottom Right",
        ZoneType.LeftThird      => "Left 1/3",
        ZoneType.LeftHalf       => "Left Half",
        ZoneType.LeftTwoThirds  => "Left 2/3",
        ZoneType.RightThird     => "Right 1/3",
        ZoneType.RightHalf      => "Right Half",
        ZoneType.RightTwoThirds => "Right 2/3",
        _                       => zone.ToString()
    };

    /// <summary>
    /// Finds the closest matching zone for a window's current position within a work area.
    /// Used by the "Capture from current window" flow.
    /// </summary>
    public static ZoneType FindClosestZone(RECT windowRect, RECT workArea)
    {
        var zones = Enum.GetValues<ZoneType>();
        ZoneType best = ZoneType.Centered;
        double bestScore = double.MaxValue;

        foreach (var zone in zones)
        {
            var rect = Calculate(zone, workArea);
            double score =
                Math.Abs(windowRect.Left - rect.X) +
                Math.Abs(windowRect.Top - rect.Y) +
                Math.Abs(windowRect.Width - rect.Width) +
                Math.Abs(windowRect.Height - rect.Height);

            if (score < bestScore)
            {
                bestScore = score;
                best = zone;
            }
        }

        return best;
    }
}

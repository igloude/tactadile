using System.Runtime.InteropServices;
using Tactadile.Config;
using Tactadile.Native;

namespace Tactadile.Core.Recognizers;

/// <summary>
/// Detects high-velocity cursor movement that terminates at a screen edge.
/// Combines velocity detection with screen-edge proximity.
/// </summary>
public static class EdgeFlickRecognizer
{
    /// <summary>
    /// Evaluate the ring buffer for an edge flick gesture.
    /// Returns the detected GestureType, or null if no edge flick detected.
    /// </summary>
    public static GestureType? Evaluate(
        CursorRingBuffer buffer,
        double minVelocityPxPerSec,
        int edgeThresholdPx)
    {
        if (buffer.Count < 3) return null;

        var newest = buffer.GetByAge(0);

        // Use a short window (~100ms) for velocity calculation
        long cutoffMs = newest.TimestampMs - 100;
        int oldestIdx = -1;
        for (int i = 0; i < buffer.Count; i++)
        {
            var s = buffer.GetByIndex(i);
            if (s.TimestampMs >= cutoffMs)
            {
                oldestIdx = i;
                break;
            }
        }

        if (oldestIdx < 0) return null;

        var oldest = buffer.GetByIndex(oldestIdx);
        double elapsedMs = newest.TimestampMs - oldest.TimestampMs;
        if (elapsedMs < 16) return null;

        double deltaX = newest.X - oldest.X;
        double deltaY = newest.Y - oldest.Y;
        double elapsedSec = elapsedMs / 1000.0;

        // Get monitor bounds for the current cursor position
        var cursorPt = new POINT { X = newest.X, Y = newest.Y };
        var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        IntPtr hMonitor = NativeMethods.MonitorFromPoint(cursorPt, NativeConstants.MONITOR_DEFAULTTONEAREST);
        NativeMethods.GetMonitorInfo(hMonitor, ref mi);
        var bounds = mi.rcMonitor;

        // Check left edge
        if (newest.X <= bounds.Left + edgeThresholdPx && deltaX < 0)
        {
            double velocity = Math.Abs(deltaX) / elapsedSec;
            if (velocity >= minVelocityPxPerSec)
                return GestureType.EdgeFlickLeft;
        }

        // Check right edge
        if (newest.X >= bounds.Right - edgeThresholdPx && deltaX > 0)
        {
            double velocity = Math.Abs(deltaX) / elapsedSec;
            if (velocity >= minVelocityPxPerSec)
                return GestureType.EdgeFlickRight;
        }

        // Check top edge
        if (newest.Y <= bounds.Top + edgeThresholdPx && deltaY < 0)
        {
            double velocity = Math.Abs(deltaY) / elapsedSec;
            if (velocity >= minVelocityPxPerSec)
                return GestureType.EdgeFlickUp;
        }

        return null;
    }
}

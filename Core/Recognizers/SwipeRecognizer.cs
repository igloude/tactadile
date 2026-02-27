using Tactadile.Config;

namespace Tactadile.Core.Recognizers;

/// <summary>
/// Detects fast, deliberate single-direction cursor movements (swipes).
/// Stateless per-call — evaluates the ring buffer snapshot each tick.
/// </summary>
public static class SwipeRecognizer
{
    /// <summary>
    /// Evaluate the ring buffer for a swipe gesture.
    /// Returns the detected GestureType, or null if no swipe detected.
    /// </summary>
    public static GestureType? Evaluate(
        CursorRingBuffer buffer,
        double minVelocityPxPerSec,
        double minDisplacementPx,
        double maxCrossAxisPx,
        double timeWindowMs)
    {
        if (buffer.Count < 3) return null;

        var newest = buffer.GetByAge(0);
        long cutoffMs = newest.TimestampMs - (long)timeWindowMs;

        // Find the oldest sample within the time window
        int oldestIdx = -1;
        for (int i = buffer.Count - 1; i >= 0; i--)
        {
            var s = buffer.GetByIndex(i);
            if (s.TimestampMs >= cutoffMs)
            {
                oldestIdx = i;
                break;
            }
        }

        // We want the oldest sample in the window — search from the front
        oldestIdx = -1;
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
        if (elapsedMs < 16) return null; // Need at least one frame of data

        double deltaX = newest.X - oldest.X;
        double deltaY = newest.Y - oldest.Y;
        double elapsedSec = elapsedMs / 1000.0;

        double absX = Math.Abs(deltaX);
        double absY = Math.Abs(deltaY);

        // Check horizontal swipe
        if (absX > absY && absX >= minDisplacementPx && absY <= maxCrossAxisPx)
        {
            double velocity = absX / elapsedSec;
            if (velocity >= minVelocityPxPerSec)
            {
                return deltaX < 0 ? GestureType.SwipeLeft : GestureType.SwipeRight;
            }
        }

        // Check vertical swipe
        if (absY > absX && absY >= minDisplacementPx && absX <= maxCrossAxisPx)
        {
            double velocity = absY / elapsedSec;
            if (velocity >= minVelocityPxPerSec)
            {
                return deltaY < 0 ? GestureType.SwipeUp : GestureType.SwipeDown;
            }
        }

        return null;
    }
}

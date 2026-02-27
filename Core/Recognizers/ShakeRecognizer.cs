using Tactadile.Config;

namespace Tactadile.Core.Recognizers;

/// <summary>
/// Detects rapid oscillation (direction reversals) in cursor movement along one axis.
/// A shake requires multiple direction changes within a time window.
/// </summary>
public sealed class ShakeRecognizer
{
    private const int NoiseThresholdPx = 5;

    private int _lastDirX; // +1 or -1, 0 = unset
    private int _lastDirY;
    private int _lastX;
    private int _lastY;

    private readonly List<long> _reversalsX = new();
    private readonly List<long> _reversalsY = new();

    public void Reset()
    {
        _lastDirX = 0;
        _lastDirY = 0;
        _lastX = 0;
        _lastY = 0;
        _reversalsX.Clear();
        _reversalsY.Clear();
    }

    /// <summary>
    /// Feed a new cursor sample and check for shake gestures.
    /// Returns the detected GestureType, or null if no shake detected.
    /// </summary>
    public GestureType? Evaluate(CursorSample sample, double timeWindowMs, int minReversals, double minDisplacementPx)
    {
        if (_lastDirX == 0 && _lastDirY == 0)
        {
            // First sample — just record position
            _lastX = sample.X;
            _lastY = sample.Y;
            return null;
        }

        int deltaX = sample.X - _lastX;
        int deltaY = sample.Y - _lastY;

        // X-axis direction tracking
        if (Math.Abs(deltaX) > NoiseThresholdPx)
        {
            int dirX = deltaX > 0 ? 1 : -1;
            if (_lastDirX != 0 && dirX != _lastDirX)
            {
                _reversalsX.Add(sample.TimestampMs);
            }
            _lastDirX = dirX;
            _lastX = sample.X;
        }

        // Y-axis direction tracking
        if (Math.Abs(deltaY) > NoiseThresholdPx)
        {
            int dirY = deltaY > 0 ? 1 : -1;
            if (_lastDirY != 0 && dirY != _lastDirY)
            {
                _reversalsY.Add(sample.TimestampMs);
            }
            _lastDirY = dirY;
            _lastY = sample.Y;
        }

        // Prune old reversals
        long cutoff = sample.TimestampMs - (long)timeWindowMs;
        PruneOlderThan(_reversalsX, cutoff);
        PruneOlderThan(_reversalsY, cutoff);

        // Check horizontal shake
        if (_reversalsX.Count >= minReversals)
        {
            Reset();
            return GestureType.ShakeHorizontal;
        }

        // Check vertical shake
        if (_reversalsY.Count >= minReversals)
        {
            Reset();
            return GestureType.ShakeVertical;
        }

        return null;
    }

    private static void PruneOlderThan(List<long> timestamps, long cutoff)
    {
        int removeCount = 0;
        for (int i = 0; i < timestamps.Count; i++)
        {
            if (timestamps[i] >= cutoff) break;
            removeCount++;
        }
        if (removeCount > 0)
            timestamps.RemoveRange(0, removeCount);
    }
}

namespace Tactadile.Core.Recognizers;

/// <summary>
/// A single cursor position sample with timestamp, stored in the gesture ring buffer.
/// </summary>
public readonly struct CursorSample
{
    public readonly int X;
    public readonly int Y;
    public readonly long TimestampMs;

    public CursorSample(int x, int y, long timestampMs)
    {
        X = x;
        Y = y;
        TimestampMs = timestampMs;
    }
}

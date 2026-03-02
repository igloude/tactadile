using Tactadile.Config;
using Tactadile.Core.Recognizers;

namespace Tactadile.Tests;

public sealed class SwipeRecognizerTests
{
    private const double MinVelocity = 800;    // px/sec
    private const double MinDisplacement = 80;  // px
    private const double MaxCrossAxis = 40;     // px
    private const double TimeWindow = 300;      // ms

    private static CursorRingBuffer CreateBuffer(params (int x, int y, long t)[] samples)
    {
        var buffer = new CursorRingBuffer(64);
        foreach (var (x, y, t) in samples)
            buffer.Add(x, y, t);
        return buffer;
    }

    [Fact]
    public void TooFewSamples_ReturnsNull()
    {
        var buffer = CreateBuffer((0, 0, 0), (100, 0, 50));
        Assert.Null(SwipeRecognizer.Evaluate(buffer, MinVelocity, MinDisplacement, MaxCrossAxis, TimeWindow));
    }

    [Fact]
    public void EmptyBuffer_ReturnsNull()
    {
        var buffer = new CursorRingBuffer(64);
        Assert.Null(SwipeRecognizer.Evaluate(buffer, MinVelocity, MinDisplacement, MaxCrossAxis, TimeWindow));
    }

    [Fact]
    public void FastSwipeRight_Detected()
    {
        // 200px in 100ms = 2000 px/sec (above 800 threshold)
        var buffer = CreateBuffer(
            (0, 100, 0),
            (100, 100, 50),
            (200, 100, 100));

        var result = SwipeRecognizer.Evaluate(buffer, MinVelocity, MinDisplacement, MaxCrossAxis, TimeWindow);
        Assert.Equal(GestureType.SwipeRight, result);
    }

    [Fact]
    public void FastSwipeLeft_Detected()
    {
        // -200px in 100ms = 2000 px/sec
        var buffer = CreateBuffer(
            (200, 100, 0),
            (100, 100, 50),
            (0, 100, 100));

        var result = SwipeRecognizer.Evaluate(buffer, MinVelocity, MinDisplacement, MaxCrossAxis, TimeWindow);
        Assert.Equal(GestureType.SwipeLeft, result);
    }

    [Fact]
    public void FastSwipeUp_Detected()
    {
        // -200px vertical in 100ms
        var buffer = CreateBuffer(
            (100, 200, 0),
            (100, 100, 50),
            (100, 0, 100));

        var result = SwipeRecognizer.Evaluate(buffer, MinVelocity, MinDisplacement, MaxCrossAxis, TimeWindow);
        Assert.Equal(GestureType.SwipeUp, result);
    }

    [Fact]
    public void FastSwipeDown_Detected()
    {
        // +200px vertical in 100ms
        var buffer = CreateBuffer(
            (100, 0, 0),
            (100, 100, 50),
            (100, 200, 100));

        var result = SwipeRecognizer.Evaluate(buffer, MinVelocity, MinDisplacement, MaxCrossAxis, TimeWindow);
        Assert.Equal(GestureType.SwipeDown, result);
    }

    [Fact]
    public void SlowMovement_NotDetected()
    {
        // 100px in 1000ms = 100 px/sec (below 800 threshold)
        var buffer = CreateBuffer(
            (0, 100, 0),
            (50, 100, 500),
            (100, 100, 1000));

        var result = SwipeRecognizer.Evaluate(buffer, MinVelocity, MinDisplacement, MaxCrossAxis, TimeWindow);
        Assert.Null(result);
    }

    [Fact]
    public void InsufficientDisplacement_NotDetected()
    {
        // 50px in 20ms = 2500 px/sec (fast enough) but only 50px displacement (below 80 threshold)
        var buffer = CreateBuffer(
            (0, 100, 0),
            (25, 100, 10),
            (50, 100, 20));

        var result = SwipeRecognizer.Evaluate(buffer, MinVelocity, MinDisplacement, MaxCrossAxis, TimeWindow);
        Assert.Null(result);
    }

    [Fact]
    public void ExcessiveCrossAxis_NotDetected()
    {
        // 200px horizontal, 50px vertical (cross-axis > 40)
        var buffer = CreateBuffer(
            (0, 0, 0),
            (100, 25, 50),
            (200, 50, 100));

        var result = SwipeRecognizer.Evaluate(buffer, MinVelocity, MinDisplacement, MaxCrossAxis, TimeWindow);
        Assert.Null(result);
    }

    [Fact]
    public void DiagonalMovement_NotDetected()
    {
        // Equal X and Y displacement — absX is not > absY
        var buffer = CreateBuffer(
            (0, 0, 0),
            (100, 100, 50),
            (200, 200, 100));

        var result = SwipeRecognizer.Evaluate(buffer, MinVelocity, MinDisplacement, MaxCrossAxis, TimeWindow);
        Assert.Null(result);
    }

    [Fact]
    public void SamplesOutsideTimeWindow_Ignored()
    {
        // Old samples at t=0,50 are outside the 300ms window when newest is at t=1000
        // Only t=900,950,1000 are in window — that gives 200px in 100ms
        var buffer = CreateBuffer(
            (0, 100, 0),      // outside window
            (10, 100, 50),     // outside window
            (600, 100, 900),
            (700, 100, 950),
            (800, 100, 1000));

        var result = SwipeRecognizer.Evaluate(buffer, MinVelocity, MinDisplacement, MaxCrossAxis, TimeWindow);
        Assert.Equal(GestureType.SwipeRight, result);
    }

    [Fact]
    public void SingleFrame_TooShort_ReturnsNull()
    {
        // All at same timestamp (elapsed < 16ms)
        var buffer = CreateBuffer(
            (0, 0, 100),
            (100, 0, 100),
            (200, 0, 100));

        var result = SwipeRecognizer.Evaluate(buffer, MinVelocity, MinDisplacement, MaxCrossAxis, TimeWindow);
        Assert.Null(result);
    }

    [Fact]
    public void MinimalCrossAxis_StillDetected()
    {
        // 200px horizontal, 39px vertical (just under 40 max cross-axis)
        var buffer = CreateBuffer(
            (0, 0, 0),
            (100, 20, 50),
            (200, 39, 100));

        var result = SwipeRecognizer.Evaluate(buffer, MinVelocity, MinDisplacement, MaxCrossAxis, TimeWindow);
        Assert.Equal(GestureType.SwipeRight, result);
    }

    [Fact]
    public void ExactlyAtThresholds_Detected()
    {
        // Exactly 80px displacement, 800 px/sec velocity (80px in 100ms)
        var buffer = CreateBuffer(
            (0, 0, 0),
            (40, 0, 50),
            (80, 0, 100));

        var result = SwipeRecognizer.Evaluate(buffer, MinVelocity, MinDisplacement, MaxCrossAxis, TimeWindow);
        Assert.Equal(GestureType.SwipeRight, result);
    }
}

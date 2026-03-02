using Tactadile.Config;
using Tactadile.Core.Recognizers;

namespace Tactadile.Tests;

public sealed class ShakeRecognizerTests
{
    private const double TimeWindowMs = 600;
    private const int MinReversals = 3;
    private const double MinDisplacementPx = 40;
    private const int NoiseThreshold = 5; // From ShakeRecognizer source

    [Fact]
    public void Reset_ClearsState()
    {
        var recognizer = new ShakeRecognizer();

        // Feed some samples to build state
        recognizer.Evaluate(new CursorSample(0, 0, 0), TimeWindowMs, MinReversals, MinDisplacementPx);
        recognizer.Evaluate(new CursorSample(20, 0, 50), TimeWindowMs, MinReversals, MinDisplacementPx);

        recognizer.Reset();

        // After reset, first sample should just record position (return null)
        var result = recognizer.Evaluate(new CursorSample(100, 100, 1000), TimeWindowMs, MinReversals, MinDisplacementPx);
        Assert.Null(result);
    }

    [Fact]
    public void FirstSample_ReturnsNull()
    {
        var recognizer = new ShakeRecognizer();
        var result = recognizer.Evaluate(new CursorSample(100, 100, 0), TimeWindowMs, MinReversals, MinDisplacementPx);
        Assert.Null(result);
    }

    [Fact]
    public void HorizontalShake_DetectedAfterEnoughReversals()
    {
        var recognizer = new ShakeRecognizer();
        long t = 0;

        // First sample sets position
        recognizer.Evaluate(new CursorSample(100, 100, t), TimeWindowMs, MinReversals, MinDisplacementPx);

        // Move right (establish direction)
        t += 30;
        recognizer.Evaluate(new CursorSample(120, 100, t), TimeWindowMs, MinReversals, MinDisplacementPx);

        // Reversal 1: move left
        t += 30;
        var r = recognizer.Evaluate(new CursorSample(100, 100, t), TimeWindowMs, MinReversals, MinDisplacementPx);
        Assert.Null(r); // Not enough reversals yet

        // Reversal 2: move right
        t += 30;
        r = recognizer.Evaluate(new CursorSample(120, 100, t), TimeWindowMs, MinReversals, MinDisplacementPx);
        Assert.Null(r);

        // Reversal 3: move left — should trigger
        t += 30;
        r = recognizer.Evaluate(new CursorSample(100, 100, t), TimeWindowMs, MinReversals, MinDisplacementPx);
        Assert.Equal(GestureType.ShakeHorizontal, r);
    }

    [Fact]
    public void VerticalShake_DetectedAfterEnoughReversals()
    {
        var recognizer = new ShakeRecognizer();
        long t = 0;

        recognizer.Evaluate(new CursorSample(100, 100, t), TimeWindowMs, MinReversals, MinDisplacementPx);

        // Move down
        t += 30;
        recognizer.Evaluate(new CursorSample(100, 120, t), TimeWindowMs, MinReversals, MinDisplacementPx);

        // Reversal 1: move up
        t += 30;
        recognizer.Evaluate(new CursorSample(100, 100, t), TimeWindowMs, MinReversals, MinDisplacementPx);

        // Reversal 2: move down
        t += 30;
        recognizer.Evaluate(new CursorSample(100, 120, t), TimeWindowMs, MinReversals, MinDisplacementPx);

        // Reversal 3: move up — should trigger
        t += 30;
        var r = recognizer.Evaluate(new CursorSample(100, 100, t), TimeWindowMs, MinReversals, MinDisplacementPx);
        Assert.Equal(GestureType.ShakeVertical, r);
    }

    [Fact]
    public void SmallMovements_BelowNoiseThreshold_Ignored()
    {
        var recognizer = new ShakeRecognizer();
        long t = 0;

        recognizer.Evaluate(new CursorSample(100, 100, t), TimeWindowMs, MinReversals, MinDisplacementPx);

        // Movements <= 5px (noise threshold) should not register as direction changes
        for (int i = 0; i < 20; i++)
        {
            t += 30;
            int offset = (i % 2 == 0) ? 3 : -3; // oscillate within noise threshold
            var r = recognizer.Evaluate(new CursorSample(100 + offset, 100, t), TimeWindowMs, MinReversals, MinDisplacementPx);
            Assert.Null(r);
        }
    }

    [Fact]
    public void ReversalsExpire_OutsideTimeWindow()
    {
        var recognizer = new ShakeRecognizer();
        long t = 0;

        recognizer.Evaluate(new CursorSample(100, 100, t), TimeWindowMs, MinReversals, MinDisplacementPx);

        // Two reversals early
        t += 30;
        recognizer.Evaluate(new CursorSample(120, 100, t), TimeWindowMs, MinReversals, MinDisplacementPx);
        t += 30;
        recognizer.Evaluate(new CursorSample(100, 100, t), TimeWindowMs, MinReversals, MinDisplacementPx);
        t += 30;
        recognizer.Evaluate(new CursorSample(120, 100, t), TimeWindowMs, MinReversals, MinDisplacementPx);

        // Wait beyond time window (600ms)
        t += 700;

        // Third reversal happens after early reversals expired
        recognizer.Evaluate(new CursorSample(100, 100, t), TimeWindowMs, MinReversals, MinDisplacementPx);

        // Only 1 reversal in window, need 3
        t += 30;
        var r = recognizer.Evaluate(new CursorSample(120, 100, t), TimeWindowMs, MinReversals, MinDisplacementPx);
        Assert.Null(r);
    }

    [Fact]
    public void AfterDetection_StateResets()
    {
        var recognizer = new ShakeRecognizer();
        long t = 0;

        // Trigger a horizontal shake
        recognizer.Evaluate(new CursorSample(100, 100, t), TimeWindowMs, MinReversals, MinDisplacementPx);
        t += 30;
        recognizer.Evaluate(new CursorSample(120, 100, t), TimeWindowMs, MinReversals, MinDisplacementPx);
        t += 30;
        recognizer.Evaluate(new CursorSample(100, 100, t), TimeWindowMs, MinReversals, MinDisplacementPx);
        t += 30;
        recognizer.Evaluate(new CursorSample(120, 100, t), TimeWindowMs, MinReversals, MinDisplacementPx);
        t += 30;
        var r = recognizer.Evaluate(new CursorSample(100, 100, t), TimeWindowMs, MinReversals, MinDisplacementPx);
        Assert.Equal(GestureType.ShakeHorizontal, r);

        // After detection, state is reset. Next sample should just record position.
        t += 30;
        r = recognizer.Evaluate(new CursorSample(200, 200, t), TimeWindowMs, MinReversals, MinDisplacementPx);
        Assert.Null(r);
    }

    [Fact]
    public void HorizontalTakesPriority_WhenBothAxesReady()
    {
        var recognizer = new ShakeRecognizer();
        long t = 0;

        recognizer.Evaluate(new CursorSample(100, 100, t), TimeWindowMs, MinReversals, MinDisplacementPx);

        // Shake both axes simultaneously
        for (int i = 0; i < 4; i++)
        {
            t += 30;
            int xSign = (i % 2 == 0) ? 20 : -20;
            int ySign = (i % 2 == 0) ? 20 : -20;
            var r = recognizer.Evaluate(
                new CursorSample(100 + xSign, 100 + ySign, t),
                TimeWindowMs, MinReversals, MinDisplacementPx);

            if (r != null)
            {
                // Horizontal should take priority (checked first)
                Assert.Equal(GestureType.ShakeHorizontal, r);
                return;
            }
        }

        // If we get here, add more samples
        for (int i = 0; i < 4; i++)
        {
            t += 30;
            int sign = (i % 2 == 0) ? 20 : -20;
            var r = recognizer.Evaluate(
                new CursorSample(100 + sign, 100 + sign, t),
                TimeWindowMs, MinReversals, MinDisplacementPx);
            if (r != null)
            {
                Assert.Equal(GestureType.ShakeHorizontal, r);
                return;
            }
        }
    }
}

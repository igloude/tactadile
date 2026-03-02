using Tactadile.Core.Recognizers;

namespace Tactadile.Tests;

public sealed class CursorRingBufferTests
{
    [Fact]
    public void NewBuffer_HasZeroCount()
    {
        var buffer = new CursorRingBuffer(8);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Add_IncrementsCount()
    {
        var buffer = new CursorRingBuffer(8);
        buffer.Add(10, 20, 100);
        Assert.Equal(1, buffer.Count);
        buffer.Add(30, 40, 200);
        Assert.Equal(2, buffer.Count);
    }

    [Fact]
    public void Add_BeyondCapacity_CountClampsAtCapacity()
    {
        var buffer = new CursorRingBuffer(4);
        for (int i = 0; i < 10; i++)
            buffer.Add(i, i, i * 100);

        Assert.Equal(4, buffer.Count);
    }

    [Fact]
    public void GetByAge_Zero_ReturnsNewest()
    {
        var buffer = new CursorRingBuffer(8);
        buffer.Add(1, 2, 100);
        buffer.Add(3, 4, 200);
        buffer.Add(5, 6, 300);

        var newest = buffer.GetByAge(0);
        Assert.Equal(5, newest.X);
        Assert.Equal(6, newest.Y);
        Assert.Equal(300, newest.TimestampMs);
    }

    [Fact]
    public void GetByAge_CountMinusOne_ReturnsOldest()
    {
        var buffer = new CursorRingBuffer(8);
        buffer.Add(1, 2, 100);
        buffer.Add(3, 4, 200);
        buffer.Add(5, 6, 300);

        var oldest = buffer.GetByAge(buffer.Count - 1);
        Assert.Equal(1, oldest.X);
        Assert.Equal(2, oldest.Y);
        Assert.Equal(100, oldest.TimestampMs);
    }

    [Fact]
    public void GetByIndex_Zero_ReturnsOldest()
    {
        var buffer = new CursorRingBuffer(8);
        buffer.Add(1, 2, 100);
        buffer.Add(3, 4, 200);
        buffer.Add(5, 6, 300);

        var oldest = buffer.GetByIndex(0);
        Assert.Equal(1, oldest.X);
        Assert.Equal(2, oldest.Y);
        Assert.Equal(100, oldest.TimestampMs);
    }

    [Fact]
    public void GetByIndex_CountMinusOne_ReturnsNewest()
    {
        var buffer = new CursorRingBuffer(8);
        buffer.Add(1, 2, 100);
        buffer.Add(3, 4, 200);
        buffer.Add(5, 6, 300);

        var newest = buffer.GetByIndex(buffer.Count - 1);
        Assert.Equal(5, newest.X);
        Assert.Equal(6, newest.Y);
        Assert.Equal(300, newest.TimestampMs);
    }

    [Fact]
    public void WrapAround_OldestSamplesOverwritten()
    {
        var buffer = new CursorRingBuffer(3);
        buffer.Add(1, 1, 100);
        buffer.Add(2, 2, 200);
        buffer.Add(3, 3, 300);
        buffer.Add(4, 4, 400); // overwrites (1,1,100)
        buffer.Add(5, 5, 500); // overwrites (2,2,200)

        Assert.Equal(3, buffer.Count);

        // Oldest should now be (3,3,300)
        var oldest = buffer.GetByIndex(0);
        Assert.Equal(3, oldest.X);
        Assert.Equal(300, oldest.TimestampMs);

        // Newest should be (5,5,500)
        var newest = buffer.GetByAge(0);
        Assert.Equal(5, newest.X);
        Assert.Equal(500, newest.TimestampMs);
    }

    [Fact]
    public void Clear_ResetsCount()
    {
        var buffer = new CursorRingBuffer(8);
        buffer.Add(1, 2, 100);
        buffer.Add(3, 4, 200);

        buffer.Clear();

        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void DefaultCapacity_Is64()
    {
        var buffer = new CursorRingBuffer();
        for (int i = 0; i < 100; i++)
            buffer.Add(i, i, i);

        Assert.Equal(64, buffer.Count);
    }

    [Fact]
    public void GetByAge_And_GetByIndex_AreInverses()
    {
        var buffer = new CursorRingBuffer(8);
        for (int i = 0; i < 5; i++)
            buffer.Add(i * 10, i * 20, i * 100);

        for (int i = 0; i < buffer.Count; i++)
        {
            var byAge = buffer.GetByAge(i);
            var byIndex = buffer.GetByIndex(buffer.Count - 1 - i);
            Assert.Equal(byAge.X, byIndex.X);
            Assert.Equal(byAge.Y, byIndex.Y);
            Assert.Equal(byAge.TimestampMs, byIndex.TimestampMs);
        }
    }

    [Fact]
    public void WrapAround_ManyTimes_StillCorrect()
    {
        var buffer = new CursorRingBuffer(4);

        // Add 20 items (wrapping 5 times)
        for (int i = 0; i < 20; i++)
            buffer.Add(i, i * 2, i * 100);

        Assert.Equal(4, buffer.Count);

        // Should contain the last 4 items: 16,17,18,19
        var oldest = buffer.GetByIndex(0);
        Assert.Equal(16, oldest.X);

        var newest = buffer.GetByAge(0);
        Assert.Equal(19, newest.X);
    }
}

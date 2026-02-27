namespace Tactadile.Core.Recognizers;

/// <summary>
/// Fixed-size ring buffer of cursor samples. At 60fps with 64 slots,
/// stores approximately 1 second of cursor history.
/// </summary>
public sealed class CursorRingBuffer
{
    private readonly CursorSample[] _buffer;
    private int _head;
    private int _count;

    public int Count => _count;

    public CursorRingBuffer(int capacity = 64)
    {
        _buffer = new CursorSample[capacity];
    }

    public void Add(int x, int y, long timestampMs)
    {
        int writeIndex = (_head + _count) % _buffer.Length;
        _buffer[writeIndex] = new CursorSample(x, y, timestampMs);

        if (_count == _buffer.Length)
            _head = (_head + 1) % _buffer.Length; // overwrite oldest
        else
            _count++;
    }

    /// <summary>
    /// Get sample by age. 0 = newest, Count-1 = oldest.
    /// </summary>
    public CursorSample GetByAge(int ageIndex)
    {
        return _buffer[(_head + _count - 1 - ageIndex) % _buffer.Length];
    }

    /// <summary>
    /// Get sample by index from oldest. 0 = oldest, Count-1 = newest.
    /// </summary>
    public CursorSample GetByIndex(int index)
    {
        return _buffer[(_head + index) % _buffer.Length];
    }

    public void Clear()
    {
        _head = 0;
        _count = 0;
    }
}

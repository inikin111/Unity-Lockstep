using System;

public class RingBuffer<T>
{
    readonly T[] buffer;
    uint capacity => (uint)buffer.Length;
    uint latestTick = 0;
    public uint EarliestTick => latestTick - capacity + 1;
    public RingBuffer(int capacity)
    {
        buffer = new T[capacity];
    }

    public void Insert(uint index, T value)
    {
        latestTick = Math.Max(latestTick, index);
        uint modIndex = index % capacity;
        buffer[modIndex] = value;
    }

    public T Get(uint index)
    {
        uint modIndex = index % capacity;
        return buffer[modIndex];
    }

    public void Clear()
    {
        Array.Clear(buffer, 0, buffer.Length);
    }

    public T this[uint index]
    {
        get
        {
            return Get(index);
        }
        set
        {
            Insert(index, value);
        }
    }
}
using System;

public class RingBuffer<T>
{
    readonly T[] buffer;
    uint capacity => (uint)buffer.Length;
    uint latestTick = 0;
    public uint EarliestTick => latestTick - capacity;
    public RingBuffer(int capacity)
    {
        buffer = new T[capacity];
    }

    public void Insert(uint index, T value)
    {
        // TODO: 这里有问题，网络层重构时的Bug可能是，刚开始游戏index == 22，直接报错
        if (index > capacity && index < EarliestTick)
        {
            throw new InvalidOperationException($"Index {index} is too old. Earliest tick is {EarliestTick}.");
        }
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
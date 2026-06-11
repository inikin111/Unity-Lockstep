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

    // Fixme : Something is wrong here. The bug might be from the network layer refractoring
    //        When beginning the game, index == 22, which causes an error
    // Fixed: The bug is from the server logic. The server did't verify the tick and wait for empty tick comes.
    //        So the server ran ahead for dozens of frames, and the client only tries to get the latest frame packet every tick, which causes the error.
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
            try {
                return Get(index);
            }
            catch (InvalidOperationException)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
        set
        {
            Insert(index, value);
        }
    }
}
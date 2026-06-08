using System;
using System.Collections.Generic;

public class RingBuffer<T>
{
    readonly T[] buffer;
    uint capacity => (uint)buffer.Length;
    public RingBuffer(int capacity)
    {
        buffer = new T[capacity];
    }

    public void Insert(uint index, T value)
    {
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
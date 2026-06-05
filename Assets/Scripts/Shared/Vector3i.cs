using System;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public partial struct Vector3i
{
    const int Scale = 1000;
    public int x;
    public int y;
    public int z;

    public Vector3i(int x, int y, int z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public static Vector3i Zero => new Vector3i(0, 0, 0);
    public static Vector3i One => new Vector3i(Scale, Scale, Scale);

    public static int Distance(Vector3i a, Vector3i b)
    {
        int target = DistanceSquared(a, b);
        if (target <= 0) return 0;

        int res = 0;
        // 找到最高位的初始掩码。对于32位正整数，最高有效位的平方根掩码从 1 << 15 开始
        int bit = 1 << 30; 

        // 先把 bit 缩放到不大于 target 的最大值
        while (bit > target)
        {
            bit >>= 2;
        }

        // 逐位逼近
        while (bit != 0)
        {
            if (target >= res + bit)
            {
                target -= res + bit;
                res = (res >> 1) + bit;
            }
            else
            {
                res >>= 1;
            }
            bit >>= 2;
        }
        return res;
    }

    public static int DistanceSquared(Vector3i a, Vector3i b)
    {
        int dx = a.x - b.x;
        int dy = a.y - b.y;
        int dz = a.z - b.z;
        return dx * dx + dy * dy + dz * dz;
    }

    public static Vector3i operator +(Vector3i a, Vector3i b)
    {
        return new Vector3i(a.x + b.x, a.y + b.y, a.z + b.z);
    }

    public static Vector3i operator -(Vector3i a, Vector3i b)
    {
        return new Vector3i(a.x - b.x, a.y - b.y, a.z - b.z);
    }

    public static Vector3i operator *(Vector3i a, int k)
    {
        return new Vector3i(a.x * k, a.y * k, a.z * k);
    }

    public static Vector3i operator /(Vector3i a, int k)
    {
        return new Vector3i(a.x / k, a.y / k, a.z / k);
    }

    public static int Dot(Vector3i a, Vector3i b)
    {
        return a.x * b.x + a.y * b.y + a.z * b.z;
    }

    public override string ToString()
    {
        return $"({x}, {y}, {z})";
    }

    static int RoundToInt(double value)
    {
        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
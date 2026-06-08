using System;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public partial struct Vector3i
{
    public const int Scale = 1000;
    public int x;
    public int y;
    public int z;

    public Vector3i(int x, int y, int z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public readonly int Magnitude()
    {
        long sqredMagnitude = SquaredMagnitude();
        if (sqredMagnitude <= 0) return 0;

        int res = 0;
        int bit = 1 << 30;

        while (bit > sqredMagnitude)
        {
            bit >>= 2;
        }

        while (bit != 0)
        {
            if (sqredMagnitude >= res + bit)
            {
                sqredMagnitude -= res + bit;
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

    public readonly long SquaredMagnitude()
    {
        return (long) x * x + (long) y * y + (long) z * z;
    }

    public static Vector3i Zero => new Vector3i(0, 0, 0);
    public static Vector3i One => new Vector3i(Scale, Scale, Scale);

    public static int Distance(Vector3i a, Vector3i b)
    {
        long target = DistanceSquared(a, b);
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

    public readonly Vector3i Normalize()
    {
        int length = Magnitude();
        if (length == 0)
        {
            return Zero;
        }
        return this * Scale / length;
    }

    public readonly Vector3i ClampMagnitude(int maxMagnitude)
    {
        if (maxMagnitude <= 0)
        {
            return Zero;
        }

        int magnitude = Magnitude();
        if (magnitude == 0 || magnitude <= maxMagnitude)
        {
            return this;
        }

        return this * maxMagnitude / magnitude;
    }

    public readonly Vector3i ScaleTo(int magnitude)
    {
        int currentMagnitude = Magnitude();
        if (currentMagnitude == 0 || magnitude <= 0)
        {
            return Zero;
        }

        return this * magnitude / currentMagnitude;
    }

    public readonly Vector3i MultiplyByScalar(int scalar)
    {
        return new Vector3i(
            (int)((long)x * scalar),
            (int)((long)y * scalar),
            (int)((long)z * scalar));
    }

    public readonly Vector3i DivideByScalar(int scalar)
    {
        return new Vector3i(x / scalar, y / scalar, z / scalar);
    }

    public static int Dot(Vector3i a, Vector3i b)
    {
        long sum = a.x * b.x + a.y * b. y + a.z * b.z;
        return (int) sum / Scale;
    }

    public static long DistanceSquared(Vector3i a, Vector3i b)
    {
        long dx = (a.x - b.x) * (a.x - b.x);
        long dy = (a.y - b.y) * (a.y - b.y);
        long dz = (a.z - b.z) * (a.z - b.z);
        return dx + dy + dz;
    }

    public static Vector3i operator +(Vector3i a, Vector3i b)
    {
        return new Vector3i(a.x + b.x, a.y + b.y, a.z + b.z);
    }

    public static Vector3i operator -(Vector3i a, Vector3i b)
    {
        return new Vector3i(a.x - b.x, a.y - b.y, a.z - b.z);
    }

    public static Vector3i operator -(Vector3i a)
    {
        return new Vector3i(-a.x, -a.y, -a.z);
    }

    public static Vector3i operator *(Vector3i a, int k)
    {
        return new Vector3i(
            (int)((long)a.x * k / Scale),
            (int)((long)a.y * k / Scale),
            (int)((long)a.z * k / Scale));
    }

    public static Vector3i operator /(Vector3i a, int k)
    {
        return new Vector3i(
            (int)((long)a.x * Scale / k),
            (int)((long)a.y * Scale / k),
            (int)((long)a.z * Scale / k));
    }

    public override string ToString()
    {
        return $"({x}, {y}, {z})";
    }
}

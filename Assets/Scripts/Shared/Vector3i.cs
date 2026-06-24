using System;
using System.Runtime.InteropServices;

[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public partial struct Vector3i
{
    public const int Scale = 1000;
    public int rawX;
    public int rawY;
    public int rawZ;

    public float x
    {
        readonly get => RawToFloat(rawX);
        set => rawX = FloatToRaw(value);
    }

    public float y
    {
        readonly get => RawToFloat(rawY);
        set => rawY = FloatToRaw(value);
    }

    public float z
    {
        readonly get => RawToFloat(rawZ);
        set => rawZ = FloatToRaw(value);
    }

    public readonly int RawX => rawX;
    public readonly int RawY => rawY;
    public readonly int RawZ => rawZ;

    public Vector3i(float x, float y, float z)
    {
        rawX = FloatToRaw(x);
        rawY = FloatToRaw(y);
        rawZ = FloatToRaw(z);
    }

    public static Vector3i FromRaw(int x, int y, int z)
    {
        return new Vector3i
        {
            rawX = x,
            rawY = y,
            rawZ = z
        };
    }

    static int FloatToRaw(float value)
    {
        return (int)Math.Round(value * Scale, MidpointRounding.AwayFromZero);
    }

    static float RawToFloat(int value)
    {
        return value / (float)Scale;
    }

    static int ScalarToRaw(int scalar)
    {
        return (int)((long)scalar * Scale);
    }

    static int ScalarToRaw(float scalar)
    {
        return FloatToRaw(scalar);
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
        return (long)rawX * rawX + (long)rawY * rawY + (long)rawZ * rawZ;
    }

    public static Vector3i Zero => FromRaw(0, 0, 0);
    public static Vector3i One => FromRaw(Scale, Scale, Scale);

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
        return ScaleTo(Scale);
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

        return ScaleTo(maxMagnitude);
    }

    public readonly Vector3i ScaleTo(int magnitude)
    {
        int currentMagnitude = Magnitude();
        if (currentMagnitude == 0 || magnitude <= 0)
        {
            return Zero;
        }

        int rawScalar = (int)((long)magnitude * Scale / currentMagnitude);
        return ScaleByRawScalar(rawScalar);
    }

    public readonly Vector3i ScaleByRawScalar(int rawScalar)
    {
        return FromRaw(
            (int)((long)rawX * rawScalar / Scale),
            (int)((long)rawY * rawScalar / Scale),
            (int)((long)rawZ * rawScalar / Scale));
    }

    public static int Dot(Vector3i a, Vector3i b)
    {
        long sum = (long)a.rawX * b.rawX + (long)a.rawY * b.rawY + (long)a.rawZ * b.rawZ;
        return (int) sum / Scale;
    }

    public static long DistanceSquared(Vector3i a, Vector3i b)
    {
        long dx = a.rawX - b.rawX;
        long dy = a.rawY - b.rawY;
        long dz = a.rawZ - b.rawZ;
        return dx * dx + dy * dy + dz * dz;
    }

    public static Vector3i operator +(Vector3i a, Vector3i b)
    {
        return FromRaw(a.rawX + b.rawX, a.rawY + b.rawY, a.rawZ + b.rawZ);
    }

    public static Vector3i operator -(Vector3i a, Vector3i b)
    {
        return FromRaw(a.rawX - b.rawX, a.rawY - b.rawY, a.rawZ - b.rawZ);
    }

    public static Vector3i operator -(Vector3i a)
    {
        return FromRaw(-a.rawX, -a.rawY, -a.rawZ);
    }

    public static Vector3i operator *(Vector3i a, int scalar)
    {
        return a.ScaleByRawScalar(ScalarToRaw(scalar));
    }

    public static Vector3i operator *(int scalar, Vector3i a)
    {
        return a * scalar;
    }

    public static Vector3i operator *(Vector3i a, float scalar)
    {
        return a.ScaleByRawScalar(ScalarToRaw(scalar));
    }

    public static Vector3i operator *(float scalar, Vector3i a)
    {
        return a * scalar;
    }

    public static Vector3i operator /(Vector3i a, int scalar)
    {
        return DivideByRawScalar(a, ScalarToRaw(scalar));
    }

    public static Vector3i operator /(Vector3i a, float scalar)
    {
        return DivideByRawScalar(a, ScalarToRaw(scalar));
    }

    static Vector3i DivideByRawScalar(Vector3i a, int rawScalar)
    {
        return FromRaw(
            (int)((long)a.rawX * Scale / rawScalar),
            (int)((long)a.rawY * Scale / rawScalar),
            (int)((long)a.rawZ * Scale / rawScalar));
    }

    public override string ToString()
    {
        return $"({x}, {y}, {z})";
    }
}

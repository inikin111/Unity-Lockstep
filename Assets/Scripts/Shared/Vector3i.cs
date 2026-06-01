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
        int dx = a.x - b.x;
        int dy = a.y - b.y;
        int dz = a.z - b.z;
        return RoundToInt(Math.Sqrt((double)dx * dx + (double)dy * dy + (double)dz * dz));
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

    public static Vector3i Normalize(Vector3i a)
    {
        int length = RoundToInt(Math.Sqrt((double)a.x * a.x + (double)a.y * a.y + (double)a.z * a.z));
        if (length > 0)
        {
            return a / length;
        }
        return Zero;
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
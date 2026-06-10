public static class FloatExtensions
{
    public static int ToFixedInt(this float value, int precision = 1000)
    {
        return (int)(value * precision);
    }
}
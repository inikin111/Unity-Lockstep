using UnityEngine;

public partial struct Vector3i
{
    public static Vector3i FromVector3(Vector3 vec)
    {
        const int scale = 1000;
        return new Vector3i(
            Mathf.RoundToInt(vec.x * scale),
            Mathf.RoundToInt(vec.y * scale),
            Mathf.RoundToInt(vec.z * scale));
    }

    public Vector3 ToVector3()
    {
        const int scale = 1000;
        return new Vector3(x / (float)scale, y / (float)scale, z / (float)scale);
    }
}
using UnityEngine;

public static class Vector3iExtensions
{
    public static Vector3 ToVector3(this Vector3i vec)
    {
        const int scale = 1000;
        return new Vector3(vec.x / (float)scale, vec.y / (float)scale, vec.z / (float)scale);
    }
}
using UnityEngine;

public static class Vector3iExtensions
{
    public static Vector3 ToVector3(this Vector3i vec)
    {
        return new Vector3(vec.x, vec.y, vec.z);
    }
}

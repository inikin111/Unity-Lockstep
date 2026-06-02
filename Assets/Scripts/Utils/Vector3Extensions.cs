using UnityEngine;

public static class Vector3Extensions
{
    public static Vector3i ToVector3i(this Vector3 vec)
    {
        const int scale = 1000;
        return new Vector3i(
            Mathf.RoundToInt(vec.x * scale),
            Mathf.RoundToInt(vec.y * scale),
            Mathf.RoundToInt(vec.z * scale));
    }

    public static Vector3 MoveTowards(this Vector3 current, Vector3 target, float maxDistanceDelta)
    {
        Vector3 dir = target - current;
        float dist = dir.magnitude;
        if (dist <= maxDistanceDelta || dist == 0f)
        {
            return target;
        }
        return current + (dir / dist) * maxDistanceDelta;
    }

    public static Vector3 WithY(this Vector3 vec, float newY)
    {
        return new Vector3(vec.x, newY, vec.z);
    }
}
using UnityEngine;

public static class GameObjectExtensions
{
    public static T GetOrAdd<T>(this GameObject gameObject) where T : MonoBehaviour
    {
        if (gameObject.TryGetComponent<T>(out T component))
        {
            return component;
        }
        return gameObject.AddComponent<T>();
    }
}
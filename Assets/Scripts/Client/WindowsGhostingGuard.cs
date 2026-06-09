using System.Runtime.InteropServices;
using UnityEngine;

public static class WindowsGhostingGuard
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [DllImport("user32.dll")]
    static extern void DisableProcessWindowsGhosting();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void DisableGhosting()
    {
        DisableProcessWindowsGhosting();
    }
#endif
}

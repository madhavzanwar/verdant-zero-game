using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// WebGL build tweaks at startup: disables the browser context menu, because right click is the
/// Seed button on PC (see Plugins/WebGL/BrowserInput.jslib). Does nothing on other platforms.
/// </summary>
public static class WebGLBrowserSetup
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void VerdantZero_DisableContextMenu();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Setup() => VerdantZero_DisableContextMenu();
#endif
}

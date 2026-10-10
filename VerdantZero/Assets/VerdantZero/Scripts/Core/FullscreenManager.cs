using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// Keeps Verdant Zero in full screen.
/// - Desktop: starts in borderless full screen at the monitor's native resolution (even if an older
///   windowed resolution was saved), F11 toggles it, and the choice is remembered (on by default).
/// - Web: the page template (Assets/WebGLTemplates/VerdantZero) fills the whole browser window and
///   enters browser full screen on the first click / tap / key press; the Options toggle switches it.
/// - Mobile: the app is always full screen (immersive mode is set in Player Settings).
/// </summary>
public static class FullscreenManager
{
    public const string PrefFullscreen = "options.fullscreen";

    /// <summary>Default and fallback window size when the player leaves full screen on desktop.</summary>
    private static readonly Vector2Int WindowedSize = new Vector2Int(1280, 720);

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern int VerdantZero_IsFullscreen();
    [DllImport("__Internal")] private static extern void VerdantZero_SetFullscreen(int on);
#endif

    /// <summary>False on phones and tablets, where the app is always full screen.</summary>
    public static bool CanToggle
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return true;
#else
            return !Application.isMobilePlatform;
#endif
        }
    }

    /// <summary>The player's saved preference (full screen by default).</summary>
    public static bool Preference
    {
        get => PlayerPrefs.GetInt(PrefFullscreen, 1) == 1;
        private set => PlayerPrefs.SetInt(PrefFullscreen, value ? 1 : 0);
    }

    public static bool IsFullscreen
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return VerdantZero_IsFullscreen() == 1;
#else
            return Screen.fullScreen;
#endif
        }
    }

    public static void SetFullscreen(bool on)
    {
        Preference = on;
        PlayerPrefs.Save();
        Apply(on);
    }

    public static void Toggle() => SetFullscreen(!IsFullscreen);

    private static void Apply(bool on)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // The browser only allows full screen from a user gesture; the page queues it for the next one if needed
        VerdantZero_SetFullscreen(on ? 1 : 0);
#elif UNITY_EDITOR
        // The Game view cannot go full screen; nothing to do
#else
        if (Application.isMobilePlatform)
        {
            Screen.fullScreen = true;
            return;
        }

        if (on)
        {
            Resolution native = Screen.currentResolution;
            int width = Display.main.systemWidth > 0 ? Display.main.systemWidth : native.width;
            int height = Display.main.systemHeight > 0 ? Display.main.systemHeight : native.height;
            Screen.SetResolution(width, height, FullScreenMode.FullScreenWindow);
        }
        else
        {
            int width = Mathf.Min(WindowedSize.x, Display.main.systemWidth);
            int height = Mathf.Min(WindowedSize.y, Display.main.systemHeight);
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
        }
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
#if !UNITY_EDITOR && !UNITY_WEBGL
        Apply(Application.isMobilePlatform || Preference);
#endif
#if !UNITY_WEBGL
        // Hotkey listener (F11). In the browser F11 belongs to the browser itself.
        if (CanToggle)
        {
            var go = new GameObject("FullscreenHotkey") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<FullscreenHotkey>();
        }
#endif
    }

    private sealed class FullscreenHotkey : MonoBehaviour
    {
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f11Key.wasPressedThisFrame) Toggle();
        }
    }
}

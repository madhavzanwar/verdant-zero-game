using UnityEngine;

/// <summary>
/// Player options saved in PlayerPrefs (volumes, vibration, difficulty...).
/// Single read/write point for the menu, the AudioManager and gameplay.
/// Full screen lives in <see cref="FullscreenManager"/>.
/// </summary>
public static class GameSettings
{
    public const string PrefMusicVolume = "options.musicVolume";
    public const string PrefSfxVolume = "options.sfxVolume";
    public const string PrefVibration = "options.vibration";
    public const string PrefFixedJoystick = "options.fixedJoystick";
    public const string PrefDifficulty = "options.difficulty";
    public const string PrefFirstPersonView = "options.firstPersonView";

    /// <summary>Adjustable difficulty (GDD 10.2 and 10.5): smog speed and extra time.</summary>
    public enum DifficultyLevel
    {
        Easy,
        Normal,
        Hard,
    }

    private const float DefaultVolume = 0.8f;

    /// <summary>Music volume, 0-1.</summary>
    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(PrefMusicVolume, DefaultVolume);
        set => PlayerPrefs.SetFloat(PrefMusicVolume, Mathf.Clamp01(value));
    }

    /// <summary>Effects volume (SFX, UI and ambience), 0-1.</summary>
    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(PrefSfxVolume, DefaultVolume);
        set => PlayerPrefs.SetFloat(PrefSfxVolume, Mathf.Clamp01(value));
    }

    public static bool Vibration
    {
        get => PlayerPrefs.GetInt(PrefVibration, 1) == 1;
        set => PlayerPrefs.SetInt(PrefVibration, value ? 1 : 0);
    }

    /// <summary>Joystick fixed in place (true) or dynamic, appearing under the thumb (false, default). GDD 10.4.</summary>
    public static bool FixedJoystick
    {
        get => PlayerPrefs.GetInt(PrefFixedJoystick, 0) == 1;
        set => PlayerPrefs.SetInt(PrefFixedJoystick, value ? 1 : 0);
    }

    /// <summary>Last view chosen with the HUD camera button (false = third person).</summary>
    public static bool FirstPersonView
    {
        get => PlayerPrefs.GetInt(PrefFirstPersonView, 0) == 1;
        set => PlayerPrefs.SetInt(PrefFirstPersonView, value ? 1 : 0);
    }

    /// <summary>True if the player has ever chosen a view (otherwise the Inspector's setting wins).</summary>
    public static bool HasViewChoice => PlayerPrefs.HasKey(PrefFirstPersonView);

    public static DifficultyLevel Difficulty
    {
        get => (DifficultyLevel)Mathf.Clamp(PlayerPrefs.GetInt(PrefDifficulty, (int)DifficultyLevel.Normal), 0, 2);
        set => PlayerPrefs.SetInt(PrefDifficulty, (int)value);
    }

    /// <summary>Display name for Options.</summary>
    public static string DifficultyName(DifficultyLevel level)
    {
        switch (level)
        {
            case DifficultyLevel.Easy: return "EASY";
            case DifficultyLevel.Hard: return "HARD";
            default: return "NORMAL";
        }
    }

    /// <summary>Multiplier for the seconds between smog increases (higher = rises more slowly).</summary>
    public static float SmogIntervalMultiplier(DifficultyLevel level)
    {
        switch (level)
        {
            case DifficultyLevel.Easy: return 1.8f;
            case DifficultyLevel.Hard: return 0.9f;
            default: return 1f;
        }
    }

    /// <summary>Multiplier for micro-cloud speed (higher = they travel faster).</summary>
    public static float MicroCloudSpeedMultiplier(DifficultyLevel level)
    {
        return level == DifficultyLevel.Hard ? 4f : 1f;
    }

    /// <summary>Multiplier for the seconds a micro-cloud takes to escape (lower = escapes sooner).</summary>
    public static float MicroCloudSettleMultiplier(DifficultyLevel level)
    {
        return level == DifficultyLevel.Hard ? 0.8f : 1f;
    }

    /// <summary>Meters per second at which big clouds wander (0 = still, as in Easy and Normal).</summary>
    public static float BigCloudWanderSpeed(DifficultyLevel level)
    {
        return level == DifficultyLevel.Hard ? 2.5f : 0f;
    }

    /// <summary>Multiplier for the Beam's auto-aim angles (lower = you must aim better).</summary>
    public static float BeamAssistMultiplier(DifficultyLevel level)
    {
        return level == DifficultyLevel.Hard ? 0.5f : 1f;
    }

    /// <summary>Seconds added to (or removed from) the level time.</summary>
    public static float ExtraTime(DifficultyLevel level)
    {
        switch (level)
        {
            case DifficultyLevel.Easy: return 30f;
            case DifficultyLevel.Hard: return -30f;
            default: return 0f;
        }
    }

    public static void Save() => PlayerPrefs.Save();
}

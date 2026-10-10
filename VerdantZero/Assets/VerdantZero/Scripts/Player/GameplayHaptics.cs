using UnityEngine;

/// <summary>
/// Vibration on key actions (GDD 10.3), through <see cref="Haptics"/>:
/// - Light: cloud cleared, seed launched.
/// - Medium: plant sprouting, smog alert at 80%, victory.
/// - Strong: crash and defeat.
/// Lives on the drone.
/// </summary>
public class GameplayHaptics : MonoBehaviour
{
    [Range(0f, 1f)]
    [SerializeField] private float _alertThreshold = 0.8f;

    private SeedLauncher _seeds;
    private QuadcopterCrash _crash;
    private GameSession _session;
    private bool _aboveThreshold;

    private void Awake()
    {
        _seeds = GetComponent<SeedLauncher>();
        _crash = GetComponent<QuadcopterCrash>();
    }

    private void OnEnable()
    {
        SmogCloud.Purified += OnCloudPurified;
        PlantingSpot.Planted += OnPlanted;
        if (_seeds != null) _seeds.Launched += OnSeedLaunched;
        if (_crash != null) _crash.Crashed += OnCrashed;
        _session = FindAnyObjectByType<GameSession>();
        if (_session != null) _session.Ended += OnSessionEnded;
    }

    private void OnDisable()
    {
        SmogCloud.Purified -= OnCloudPurified;
        PlantingSpot.Planted -= OnPlanted;
        if (_seeds != null) _seeds.Launched -= OnSeedLaunched;
        if (_crash != null) _crash.Crashed -= OnCrashed;
        if (_session != null) _session.Ended -= OnSessionEnded;
    }

    private void Update()
    {
        if (_session == null || _session.IsOver) return;

        // A single vibration when crossing the threshold (the beep does repeat)
        bool above = _session.Smog >= _alertThreshold;
        if (above && !_aboveThreshold) Haptics.Pulse(Haptics.Strength.Medium);
        _aboveThreshold = above;
    }

    private void OnCloudPurified(SmogCloud cloud) => Haptics.Pulse(Haptics.Strength.Light);

    private void OnSeedLaunched() => Haptics.Pulse(Haptics.Strength.Light);

    private void OnPlanted(PlantingSpot spot) => Haptics.Pulse(Haptics.Strength.Medium);

    private void OnCrashed() => Haptics.Pulse(Haptics.Strength.Heavy);

    private void OnSessionEnded(GameSession.Result result)
    {
        // The crash already vibrated in OnCrashed
        if (result == GameSession.Result.Crash) return;
        Haptics.Pulse(result == GameSession.Result.Victory ? Haptics.Strength.Medium : Haptics.Strength.Heavy);
    }
}

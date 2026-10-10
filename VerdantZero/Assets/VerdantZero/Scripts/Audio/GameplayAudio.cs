using UnityEngine;

/// <summary>
/// Gameplay sounds (GDD 9.2) that are not from the engine:
/// - Purifier Beam: looping pulse while firing, with fade in and fade out.
/// - Cloud cleared, seed shot and blooming: short effects.
/// - Alert: a beep when the smog passes 80%, repeating while it stays above.
/// - Voice lines with on-screen captions: one when play starts (after the tutorial, if shown) and another
///   each time the smog drops to 20% ("the air is almost clean"), another when time runs out and another on victory.
/// Lives on the drone, next to <see cref="PurifierBeam"/> and <see cref="SeedLauncher"/>.
/// </summary>
public class GameplayAudio : MonoBehaviour
{
    [Header("Purifier Beam")]
    [SerializeField] private AudioClip _beamLoop;
    [SerializeField] private float _beamVolume = 0.6f;
    [Tooltip("Fade seconds when starting and stopping firing.")]
    [SerializeField] private float _beamFade = 0.08f;
    [SerializeField] private AudioClip _cloudPurified;
    [SerializeField] private float _cloudPurifiedVolume = 0.8f;

    [Header("Seeds")]
    [SerializeField] private AudioClip _seedLaunch;
    [SerializeField] private float _seedLaunchVolume = 0.8f;
    [SerializeField] private AudioClip _bloom;
    [SerializeField] private float _bloomVolume = 0.9f;

    [Header("Smog alert")]
    [SerializeField] private AudioClip _alert;
    [SerializeField] private float _alertVolume = 0.7f;
    [Range(0f, 1f)]
    [SerializeField] private float _alertThreshold = 0.8f;
    [Tooltip("Seconds between beeps while the smog stays above the threshold.")]
    [SerializeField] private float _alertRepeat = 3f;

    [Header("Start voice line")]
    [SerializeField] private AudioClip _startVoice;
    [Tooltip("Caption shown in the center of the screen while the voice plays.")]
    [SerializeField] private string _startVoiceText = "Let's clean the air of this city!";
    [SerializeField] private float _startVoiceVolume = 1f;
    [Tooltip("Seconds of gameplay before the line, so the engine sound starts first. The tutorial does not count (it pauses the game).")]
    [SerializeField] private float _startVoiceDelay = 0.8f;

    [Header("Almost-clean-air voice line")]
    [SerializeField] private AudioClip _almostCleanVoice;
    [SerializeField] private string _almostCleanText = "The air is almost cleaned up!";
    [SerializeField] private float _almostCleanVolume = 1f;
    [Range(0f, 1f)]
    [Tooltip("Plays when the smog drops to this level.")]
    [SerializeField] private float _almostCleanThreshold = 0.2f;
    [Range(0f, 1f)]
    [Tooltip("To play again, the smog must first rise above this level (otherwise it would play non-stop around 20%).")]
    [SerializeField] private float _almostCleanRearm = 0.3f;

    [Header("Time warning")]
    [SerializeField] private AudioClip _timeRunningOut;
    [SerializeField] private string _timeRunningOutText = "Time is running out!";
    [SerializeField] private float _timeRunningOutVolume = 1f;
    [Tooltip("Plays once when this many seconds remain (the HUD clock turns to warning at 30).")]
    [SerializeField] private float _timeRunningOutAt = 30f;

    [Header("Victory voice line")]
    [SerializeField] private AudioClip _victoryVoice;
    [SerializeField] private string _victoryText = "Look at the blue sky!";
    [SerializeField] private float _victoryVolume = 1f;
    [Tooltip("Seconds after winning before the line, so the victory sound starts first.")]
    [SerializeField] private float _victoryVoiceDelay = 0.4f;

    private PurifierBeam _beam;
    private SeedLauncher _seeds;
    private AudioSource _beamSource;
    private float _nextAlertTime;
    private bool _aboveThreshold;
    private float _playTime;

    /// <summary>When a voice line plays: the caption and its duration in seconds (the HUD shows it at the same time).</summary>
    public static event System.Action<string, float> VoiceLinePlayed;
    private bool _startVoicePlayed;
    private bool _almostCleanArmed = true;
    private bool _timeRunningOutPlayed;
    private float _victoryTime;
    private bool _victoryVoicePlayed;

    private void Awake()
    {
        _beam = GetComponent<PurifierBeam>();
        _seeds = GetComponent<SeedLauncher>();

        _beamSource = gameObject.AddComponent<AudioSource>();
        _beamSource.clip = _beamLoop;
        _beamSource.loop = true;
        _beamSource.playOnAwake = false;
        _beamSource.spatialBlend = 0f;
        _beamSource.volume = 0f;
    }

    private void Start()
    {
        if (AudioManager.Instance != null) _beamSource.outputAudioMixerGroup = AudioManager.Instance.SfxGroup;
    }

    private void OnEnable()
    {
        SmogCloud.Purified += OnCloudPurified;
        PlantingSpot.Planted += OnPlanted;
        if (_seeds != null) _seeds.Launched += OnSeedLaunched;
    }

    private void OnDisable()
    {
        SmogCloud.Purified -= OnCloudPurified;
        PlantingSpot.Planted -= OnPlanted;
        if (_seeds != null) _seeds.Launched -= OnSeedLaunched;
    }

    private void Update()
    {
        UpdateBeam();
        UpdateAlert();
        UpdateStartVoice();
        UpdateAlmostCleanVoice();
        UpdateTimeRunningOut();
        UpdateVictoryVoice();
    }

    // Counts scaled time: with the tutorial open (timeScale 0) it does not advance, so the line plays when flying starts
    private void UpdateStartVoice()
    {
        if (_startVoicePlayed || _startVoice == null) return;
        GameSession session = GameSession.Instance;
        if (session != null && session.IsOver) return;

        _playTime += Time.deltaTime;
        if (_playTime < _startVoiceDelay) return;
        _startVoicePlayed = true;
        PlayVoice(_startVoice, _startVoiceText, _startVoiceVolume);
    }

    private void UpdateAlmostCleanVoice()
    {
        GameSession session = GameSession.Instance;
        if (_almostCleanVoice == null || session == null || session.IsOver) return;

        if (session.Smog > _almostCleanRearm) _almostCleanArmed = true;
        else if (_almostCleanArmed && session.Smog <= _almostCleanThreshold)
        {
            _almostCleanArmed = false;
            PlayVoice(_almostCleanVoice, _almostCleanText, _almostCleanVolume);
        }
    }

    private void UpdateTimeRunningOut()
    {
        if (_timeRunningOutPlayed || _timeRunningOut == null) return;
        GameSession session = GameSession.Instance;
        if (session == null || session.IsOver || session.TimeRemaining > _timeRunningOutAt) return;

        _timeRunningOutPlayed = true;
        PlayVoice(_timeRunningOut, _timeRunningOutText, _timeRunningOutVolume);
    }

    private void UpdateVictoryVoice()
    {
        GameSession session = GameSession.Instance;
        if (_victoryVoicePlayed || _victoryVoice == null || session == null || session.Outcome != GameSession.Result.Victory) return;

        _victoryTime += Time.deltaTime;
        if (_victoryTime < _victoryVoiceDelay) return;
        _victoryVoicePlayed = true;
        PlayVoice(_victoryVoice, _victoryText, _victoryVolume);
    }

    private static void PlayVoice(AudioClip clip, string text, float volume)
    {
        Play(clip, volume);
        VoiceLinePlayed?.Invoke(text, clip.length);
    }

    private void UpdateBeam()
    {
        if (_beamLoop == null) return;

        bool firing = _beam != null && _beam.IsFiring && Time.timeScale > 0f;
        float target = firing ? _beamVolume : 0f;
        float step = _beamVolume / Mathf.Max(0.01f, _beamFade) * Time.unscaledDeltaTime;
        _beamSource.volume = Mathf.MoveTowards(_beamSource.volume, target, step);

        if (firing && !_beamSource.isPlaying) _beamSource.Play();
        else if (!firing && _beamSource.isPlaying && _beamSource.volume <= 0f) _beamSource.Stop();
    }

    private void UpdateAlert()
    {
        GameSession session = GameSession.Instance;
        if (_alert == null || session == null || session.IsOver || Time.timeScale <= 0f)
        {
            _aboveThreshold = false;
            return;
        }

        bool above = session.Smog >= _alertThreshold;
        // On crossing the threshold it plays right away; after that, every _alertRepeat seconds
        if (above && !_aboveThreshold) _nextAlertTime = Time.time;
        _aboveThreshold = above;

        if (above && Time.time >= _nextAlertTime)
        {
            Play(_alert, _alertVolume);
            _nextAlertTime = Time.time + _alertRepeat;
        }
    }

    // Slight pitch variation so repeats do not sound identical
    private void OnCloudPurified(SmogCloud cloud) => Play(_cloudPurified, _cloudPurifiedVolume, Random.Range(0.94f, 1.06f));

    private void OnSeedLaunched() => Play(_seedLaunch, _seedLaunchVolume, Random.Range(0.95f, 1.05f));

    private void OnPlanted(PlantingSpot spot) => Play(_bloom, _bloomVolume);

    private static void Play(AudioClip clip, float volume, float pitch = 1f)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(clip, volume, pitch);
    }
}

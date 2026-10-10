using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Run rules (GDD 5.2 and 5.3): global smog level, timer and end of the run.
/// - Smog starts at 60% and rises 1% every 3 s (a 2:00 run on Normal).
/// - Victory: smog at 0% before time runs out.
/// - Defeat: smog at 100%, time at 00:00 or crashing into a building.
/// Uses scaled time, so it stops while paused (timeScale = 0).
/// It also tracks the score and impact stats (GDD 9.3): clouds cleared, trees planted
/// and smog removed, with a bonus for the time left over on victory.
/// </summary>
public class GameSession : MonoBehaviour
{
    public enum Result
    {
        None,
        Victory,
        Saturation,
        TimeUp,
        Crash,
    }

    [Header("Smog (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float _startSmog = 0.6f;
    [Tooltip("How much the smog rises each step.")]
    [SerializeField] private float _riseAmount = 0.01f;
    [Tooltip("Seconds between each rise.")]
    [SerializeField] private float _riseInterval = 3f;

    [Header("Time")]
    [Tooltip("Level duration in seconds.")]
    [SerializeField] private float _timeLimit = 120f;

    [Header("Score")]
    [SerializeField] private int _cloudPoints = 100;
    [SerializeField] private int _microCloudPoints = 50;
    [SerializeField] private int _treePoints = 250;
    [Tooltip("Points for each second left over on victory.")]
    [SerializeField] private int _timeBonusPerSecond = 10;

    [Header("References (searched for in the scene if empty)")]
    [SerializeField] private QuadcopterCrash _player;
    [Tooltip("Ambient clouds: their density follows the smog level.")]
    [SerializeField] private SmogClouds _clouds;

    [Header("Debug")]
    [Tooltip("In the Editor and development builds, K lowers the smog and L raises it.")]
    [SerializeField] private float _debugStep = 0.1f;

    private QuadInput _input;
    private float _riseTimer;

    public static GameSession Instance { get; private set; }

    /// <summary>Global smog level (0-1).</summary>
    public float Smog { get; private set; }

    /// <summary>Seconds remaining.</summary>
    public float TimeRemaining { get; private set; }

    public float TimeLimit => _timeLimit;
    public Result Outcome { get; private set; } = Result.None;
    public bool IsOver => Outcome != Result.None;

    public int Score { get; private set; }
    public int CloudsPurified { get; private set; }
    public int TreesPlanted { get; private set; }
    /// <summary>The level's planting spots (planted or not).</summary>
    public int TotalPlantingSpots { get; private set; }
    /// <summary>Total smog the player has removed (0-1 per 100%), even if it later rose again.</summary>
    public float SmogRemoved { get; private set; }
    /// <summary>Time bonus points (only on victory).</summary>
    public int TimeBonus { get; private set; }

    private const string BestScorePrefix = "best2.";

    /// <summary>Best saved score for a level (scene name), 0 if it has not been won.</summary>
    public static int BestScore(string sceneName) => PlayerPrefs.GetInt(BestScorePrefix + sceneName, 0);

    /// <summary>True if this run beat the level's best score.</summary>
    public bool IsNewBest { get; private set; }

    /// <summary>Raised when the score or stats change.</summary>
    public event Action StatsChanged;

    /// <summary>Raised only once, when the run ends.</summary>
    public event Action<Result> Ended;

    private void Awake()
    {
        Instance = this;

        // Difficulty from Options: the smog rises faster or slower and there is more or less time
        GameSettings.DifficultyLevel difficulty = GameSettings.Difficulty;
        _riseInterval *= GameSettings.SmogIntervalMultiplier(difficulty);
        _timeLimit = Mathf.Max(30f, _timeLimit + GameSettings.ExtraTime(difficulty));

        Smog = Mathf.Clamp01(_startSmog);
        TimeRemaining = _timeLimit;
    }

    private void OnEnable()
    {
        if (_player == null) _player = FindAnyObjectByType<QuadcopterCrash>();
        if (_clouds == null) _clouds = FindAnyObjectByType<SmogClouds>();
        if (_player != null)
        {
            _input = _player.GetComponent<QuadInput>();
            _player.Crashed += OnPlayerCrashed;
        }

        if (_clouds != null) _clouds.Density = Smog;

        SmogCloud.Purified += OnCloudPurified;
        PlantingSpot.Planted += OnTreePlanted;
    }

    private void Start()
    {
        // Planting spots register themselves in their OnEnable, before Start
        TotalPlantingSpots = PlantingSpot.Available.Count + TreesPlanted;
        StatsChanged?.Invoke();
    }

    private void OnDisable()
    {
        if (_player != null) _player.Crashed -= OnPlayerCrashed;
        SmogCloud.Purified -= OnCloudPurified;
        PlantingSpot.Planted -= OnTreePlanted;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (IsOver) return;

        HandleDebugKeys();

        _riseTimer += Time.deltaTime;
        while (_riseTimer >= _riseInterval)
        {
            _riseTimer -= _riseInterval;
            SetSmog(Smog + _riseAmount);
        }

        TimeRemaining = Mathf.Max(0f, TimeRemaining - Time.deltaTime);

        if (Smog <= 0f) End(Result.Victory);
        else if (Smog >= 1f) End(Result.Saturation);
        else if (TimeRemaining <= 0f) End(Result.TimeUp);
    }

    /// <summary>Lowers the smog (e.g. 0.05 = 5% when a cloud is cleared).</summary>
    public void ReduceSmog(float amount)
    {
        if (IsOver || amount <= 0f) return;
        float before = Smog;
        SetSmog(Smog - amount);
        SmogRemoved += before - Smog;
        // Victory is immediate: waiting for Update could let the periodic rise come first and take the win away
        if (Smog <= 0f) End(Result.Victory);
    }

    /// <summary>Raises the smog (e.g. a micro-cloud that was not neutralized).</summary>
    public void AddSmog(float amount)
    {
        if (IsOver || amount <= 0f) return;
        SetSmog(Smog + amount);
    }

    private void SetSmog(float value)
    {
        Smog = Mathf.Clamp01(value);
        if (_clouds != null) _clouds.Density = Smog;
    }

    private void OnPlayerCrashed() => End(Result.Crash);

    private void OnCloudPurified(SmogCloud cloud)
    {
        if (IsOver) return;
        CloudsPurified++;
        Score += cloud.IsMicro ? _microCloudPoints : _cloudPoints;
        StatsChanged?.Invoke();
    }

    private void OnTreePlanted(PlantingSpot spot)
    {
        if (IsOver) return;
        TreesPlanted++;
        Score += _treePoints;
        StatsChanged?.Invoke();
    }

    private void End(Result result)
    {
        if (IsOver) return;

        Outcome = result;
        if (result == Result.Victory)
        {
            TimeBonus = Mathf.CeilToInt(TimeRemaining) * _timeBonusPerSecond;
            Score += TimeBonus;
            SaveBestScore();
            StatsChanged?.Invoke();
        }
        // The drone stays hovering in place (except after a crash, which already makes it fall)
        if (_input != null) _input.Locked = true;
        Ended?.Invoke(result);
    }

    private void SaveBestScore()
    {
        string key = BestScorePrefix + gameObject.scene.name;
        if (Score <= PlayerPrefs.GetInt(key, 0)) return;
        PlayerPrefs.SetInt(key, Score);
        PlayerPrefs.Save();
        IsNewBest = true;
    }

    private void HandleDebugKeys()
    {
        if (!Debug.isDebugBuild || Keyboard.current == null) return;
        if (Keyboard.current.kKey.wasPressedThisFrame) ReduceSmog(_debugStep);
        if (Keyboard.current.lKey.wasPressedThisFrame) AddSmog(_debugStep);
    }
}

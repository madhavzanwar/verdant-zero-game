using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Results screen shown when the run ends (<see cref="GameSession.Ended"/>):
/// victory, or defeat by saturation, time-out or crash, with its cause and a tip (GDD 4.4 and 10.5),
/// plus an impact summary: trees planted, clouds cleared, smog removed and score (GDD 9.3).
/// Lets the player retry the level or return to the main menu.
/// Without a GameSession in the scene, it only reacts to the drone crashing.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GameOverController : MonoBehaviour
{
    [SerializeField] private GameSession _session;
    [SerializeField] private QuadcopterCrash _player;
    [Tooltip("Optional: the camera stops turning with the drone while it falls.")]
    [SerializeField] private FollowCamera _followCamera;
    [Tooltip("Seconds between the crash and the screen (to watch the drone fall).")]
    [SerializeField] private float _crashDelay = 1.2f;
    [Tooltip("Seconds between a smog or time-out ending and the screen.")]
    [SerializeField] private float _showDelay = 0.6f;
    [Tooltip("Seconds between winning and the screen: lets the drone's celebration and the victory line play.")]
    [SerializeField] private float _victoryDelay = 2.8f;
    [SerializeField] private string _menuSceneName = "MainMenu";
    [SerializeField] private float _fadeDuration = 0.4f;
    [Tooltip("Sound effect on defeat (smog at 100%, time up or crash).")]
    [SerializeField] private AudioClip _defeatSound;
    [Tooltip("Sound effect on victory (smog at 0%).")]
    [SerializeField] private AudioClip _victorySound;

    private VisualElement _overlay;
    private Label _title;
    private Label _subtitle;
    private Label _hint;
    private VisualElement _stats;
    private VisualElement _fade;
    private Button _btnRetry;
    private Button _btnMenu;
    private bool _isLoading;

    private void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        _overlay = root.Q<VisualElement>("gameover-overlay");
        _fade = root.Q<VisualElement>("fade");
        _title = root.Q<Label>("gameover-title");
        _subtitle = root.Q<Label>("gameover-subtitle");
        _hint = root.Q<Label>("gameover-hint");
        _stats = root.Q<VisualElement>("gameover-stats");
        _btnRetry = root.Q<Button>("btn-retry");
        _btnMenu = root.Q<Button>("btn-menu");

        if (_btnRetry != null) _btnRetry.clicked += OnRetryClicked;
        if (_btnMenu != null) _btnMenu.clicked += OnMenuClicked;

        if (_session == null) _session = FindAnyObjectByType<GameSession>();
        if (_player == null) _player = FindAnyObjectByType<QuadcopterCrash>();
        if (_followCamera == null) _followCamera = FindAnyObjectByType<FollowCamera>();

        if (_session != null) _session.Ended += OnSessionEnded;
        else if (_player != null) _player.Crashed += OnPlayerCrashed;
        else Debug.LogWarning("GameOverController: there is no GameSession or QuadcopterCrash in the scene.", this);
    }

    private void OnDisable()
    {
        if (_btnRetry != null) _btnRetry.clicked -= OnRetryClicked;
        if (_btnMenu != null) _btnMenu.clicked -= OnMenuClicked;
        if (_session != null) _session.Ended -= OnSessionEnded;
        if (_player != null) _player.Crashed -= OnPlayerCrashed;
    }

    private void OnPlayerCrashed() => OnSessionEnded(GameSession.Result.Crash);

    private void OnSessionEnded(GameSession.Result result)
    {
        bool victory = result == GameSession.Result.Victory;
        AudioClip resultSound = victory ? _victorySound : _defeatSound;
        if (resultSound != null) AudioManager.Instance?.PlayUi(resultSound);
        _overlay?.EnableInClassList("gameover-overlay--victory", victory);
        if (_btnRetry != null) _btnRetry.text = victory ? "PLAY AGAIN" : "RETRY";

        switch (result)
        {
            case GameSession.Result.Victory:
                int seconds = _session != null ? Mathf.CeilToInt(_session.TimeRemaining) : 0;
                SetTexts("CLEAN AIR!",
                    $"You cleaned the air with {seconds / 60:00}:{seconds % 60:00} to spare",
                    "The trees and gardens keep cleaning the air every day.");
                break;
            case GameSession.Result.Saturation:
                SetTexts("ENVIRONMENTAL ALERT!",
                    "Pollution reached 100%!",
                    "Tip: the smog never stops rising. Act fast on the densest clouds.");
                break;
            case GameSession.Result.TimeUp:
                SetTexts("TIME'S UP!",
                    "You ran out of time!",
                    "Tip: start with the biggest clouds to bring the smog down sooner.");
                break;
            default:
                if (_followCamera != null) _followCamera.FollowYaw = false;
                SetTexts("YOU CRASHED!",
                    _player != null && _player.LastCrashReason == QuadcopterCrash.CrashReason.HardLanding
                        ? "Landing was too hard"
                        : "The drone crashed into a building!",
                    "Tip: fly above the buildings to cross the city.");
                break;
        }

        FillStats(victory);
        float delay = result == GameSession.Result.Crash ? _crashDelay : victory ? _victoryDelay : _showDelay;
        StartCoroutine(ShowAfterDelay(delay));
    }

    private void SetTexts(string title, string subtitle, string hint)
    {
        if (_title != null) _title.text = title;
        if (_subtitle != null) _subtitle.text = subtitle;
        if (_hint != null) _hint.text = hint;
    }

    private void FillStats(bool victory)
    {
        if (_stats == null) return;
        _stats.Clear();
        if (_session == null)
        {
            _stats.style.display = DisplayStyle.None;
            return;
        }

        AddStat("Trees planted", $"{_session.TreesPlanted}/{_session.TotalPlantingSpots}");
        AddStat("Smog clouds cleared", _session.CloudsPurified.ToString());
        AddStat("Smog removed", $"{Mathf.RoundToInt(_session.SmogRemoved * 100f)}%");
        if (victory && _session.TimeBonus > 0) AddStat("Time bonus", $"+{_session.TimeBonus:N0}");
        AddStat(_session.IsNewBest ? "NEW HIGH SCORE!" : "SCORE", _session.Score.ToString("N0"), "gameover-stat--total");
    }

    private void AddStat(string label, string value, string extraClass = null)
    {
        var row = new VisualElement();
        row.AddToClassList("gameover-stat");
        if (extraClass != null) row.AddToClassList(extraClass);
        var name = new Label(label);
        name.AddToClassList("gameover-stat__label");
        var amount = new Label(value);
        amount.AddToClassList("gameover-stat__value");
        row.Add(name);
        row.Add(amount);
        _stats.Add(row);
    }

    private IEnumerator ShowAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        _overlay.style.display = DisplayStyle.Flex;
        // Wait one frame so the USS transition applies
        _overlay.schedule.Execute(() =>
        {
            _overlay.AddToClassList("gameover-overlay--visible");
            _btnRetry?.Focus();
        });
    }

    private void OnRetryClicked() => LoadScene(SceneManager.GetActiveScene().name);

    private void OnMenuClicked() => LoadScene(_menuSceneName);

    private void LoadScene(string sceneName)
    {
        if (_isLoading) return;
        StartCoroutine(FadeAndLoad(sceneName));
    }

    private IEnumerator FadeAndLoad(string sceneName)
    {
        _isLoading = true;
        _btnRetry?.SetEnabled(false);
        _btnMenu?.SetEnabled(false);

        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
        load.allowSceneActivation = false;

        _fade?.AddToClassList("fade--visible");
        yield return new WaitForSecondsRealtime(_fadeDuration);

        while (load.progress < 0.9f)
        {
            yield return null;
        }

        load.allowSceneActivation = true;
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Level pause button and pause menu: resume, retry, main menu, volumes and full screen.
/// Pausing stops time (timeScale = 0) and sound effects (music keeps playing).
/// It also pauses when the app is minimized and with the Android "back" button / Escape.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private string _menuSceneName = "MainMenu";
    [SerializeField] private float _fadeDuration = 0.4f;

    private VisualElement _overlay;
    private VisualElement _fade;
    private Button _btnPause;
    private Button _btnResume;
    private Button _btnRetry;
    private Button _btnMenu;
    private Slider _sliderMusic;
    private Slider _sliderSfx;
    private Toggle _toggleVibration;
    private Toggle _toggleFixedJoystick;
    private Toggle _toggleFullscreen;

    private GameHudController _hud;
    private QuadcopterCrash _crash;
    private GameSession _session;
    private bool _isLoading;

    public bool IsPaused { get; private set; }

    private void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        _overlay = root.Q<VisualElement>("pause-overlay");
        _fade = root.Q<VisualElement>("fade");
        _btnPause = root.Q<Button>("btn-pause");
        _btnResume = root.Q<Button>("btn-resume");
        _btnRetry = root.Q<Button>("btn-pause-retry");
        _btnMenu = root.Q<Button>("btn-pause-menu");
        _sliderMusic = root.Q<Slider>("pause-slider-music");
        _sliderSfx = root.Q<Slider>("pause-slider-sfx");
        _toggleVibration = root.Q<Toggle>("pause-toggle-vibration");
        _toggleFixedJoystick = root.Q<Toggle>("pause-toggle-fixed-joystick");
        _toggleFullscreen = root.Q<Toggle>("pause-toggle-fullscreen");
        if (_toggleFullscreen != null && !FullscreenManager.CanToggle)
            _toggleFullscreen.style.display = DisplayStyle.None;

        if (_btnPause != null) _btnPause.clicked += Pause;
        if (_btnResume != null) _btnResume.clicked += Resume;
        if (_btnRetry != null) _btnRetry.clicked += OnRetryClicked;
        if (_btnMenu != null) _btnMenu.clicked += OnMenuClicked;

        _sliderMusic?.RegisterValueChangedCallback(OnMusicChanged);
        _sliderSfx?.RegisterValueChangedCallback(OnSfxChanged);
        _toggleVibration?.RegisterValueChangedCallback(OnVibrationChanged);
        _toggleFixedJoystick?.RegisterValueChangedCallback(OnFixedJoystickChanged);
        _toggleFullscreen?.RegisterValueChangedCallback(OnFullscreenChanged);

        _hud = GetComponent<GameHudController>();
        _crash = FindAnyObjectByType<QuadcopterCrash>();
        if (_crash != null) _crash.Crashed += OnCrashed;
        _session = FindAnyObjectByType<GameSession>();
        if (_session != null) _session.Ended += OnSessionEnded;
    }

    private void OnDisable()
    {
        if (_btnPause != null) _btnPause.clicked -= Pause;
        if (_btnResume != null) _btnResume.clicked -= Resume;
        if (_btnRetry != null) _btnRetry.clicked -= OnRetryClicked;
        if (_btnMenu != null) _btnMenu.clicked -= OnMenuClicked;

        _sliderMusic?.UnregisterValueChangedCallback(OnMusicChanged);
        _sliderSfx?.UnregisterValueChangedCallback(OnSfxChanged);
        _toggleVibration?.UnregisterValueChangedCallback(OnVibrationChanged);
        _toggleFixedJoystick?.UnregisterValueChangedCallback(OnFixedJoystickChanged);
        _toggleFullscreen?.UnregisterValueChangedCallback(OnFullscreenChanged);

        if (_crash != null) _crash.Crashed -= OnCrashed;
        if (_session != null) _session.Ended -= OnSessionEnded;

        // Never leave the game frozen when leaving the scene
        if (IsPaused) SetPausedState(false);
    }

    private void Update()
    {
        // Android "back" button (arrives as Escape), Escape on the keyboard or Start on the gamepad
        bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool start = Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
        if (escape || start)
        {
            if (IsPaused) Resume();
            else Pause();
        }
    }

    private void OnApplicationPause(bool paused)
    {
        // When the app is minimized on mobile, leave the game paused.
        // Not in the Editor: there it fires when switching windows and the game started paused and muted.
#if !UNITY_EDITOR
        if (paused) Pause();
#endif
    }

    public void Pause()
    {
        if (IsPaused || _isLoading) return;
        if (_crash != null && _crash.HasCrashed) return;
        if (_session != null && _session.IsOver) return;

        _hud?.ReleaseSticks();
        LoadSettings();
        SetPausedState(true);

        _overlay.style.display = DisplayStyle.Flex;
        // USS transitions use real time, so they work with timeScale = 0
        _overlay.schedule.Execute(() =>
        {
            _overlay.AddToClassList("overlay--visible");
            _btnResume?.Focus();
        });
    }

    public void Resume()
    {
        if (!IsPaused || _isLoading) return;

        GameSettings.Save();
        SetPausedState(false);
        _overlay.RemoveFromClassList("overlay--visible");
        _overlay.schedule.Execute(() => _overlay.style.display = DisplayStyle.None).StartingIn(200);
    }

    private void SetPausedState(bool paused)
    {
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused; // music and UI ignore the pause (see AudioManager)
    }

    private void OnCrashed() => HidePauseButton();

    private void OnSessionEnded(GameSession.Result result) => HidePauseButton();

    private void HidePauseButton()
    {
        // The results screen replaces the pause button
        if (_btnPause != null) _btnPause.style.display = DisplayStyle.None;
    }

    #region Options

    private void LoadSettings()
    {
        _sliderMusic?.SetValueWithoutNotify(GameSettings.MusicVolume);
        _sliderSfx?.SetValueWithoutNotify(GameSettings.SfxVolume);
        _toggleVibration?.SetValueWithoutNotify(GameSettings.Vibration);
        _toggleFixedJoystick?.SetValueWithoutNotify(GameSettings.FixedJoystick);
        _toggleFullscreen?.SetValueWithoutNotify(FullscreenManager.IsFullscreen);
    }

    private void OnMusicChanged(ChangeEvent<float> evt)
    {
        GameSettings.MusicVolume = evt.newValue;
        AudioManager.Instance?.ApplyVolumes();
    }

    private void OnSfxChanged(ChangeEvent<float> evt)
    {
        GameSettings.SfxVolume = evt.newValue;
        AudioManager.Instance?.ApplyVolumes();
    }

    private void OnVibrationChanged(ChangeEvent<bool> evt) => GameSettings.Vibration = evt.newValue;

    private void OnFixedJoystickChanged(ChangeEvent<bool> evt) => GameSettings.FixedJoystick = evt.newValue;

    private void OnFullscreenChanged(ChangeEvent<bool> evt) => FullscreenManager.SetFullscreen(evt.newValue);

    #endregion

    #region Scene change

    private void OnRetryClicked() => LoadScene(SceneManager.GetActiveScene().name);

    private void OnMenuClicked() => LoadScene(_menuSceneName);

    private void LoadScene(string sceneName)
    {
        if (_isLoading) return;
        GameSettings.Save();
        StartCoroutine(FadeAndLoad(sceneName));
    }

    private IEnumerator FadeAndLoad(string sceneName)
    {
        _isLoading = true;
        _btnResume?.SetEnabled(false);
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

        // Restore time and audio before activating the new scene
        SetPausedState(false);
        load.allowSceneActivation = true;
    }

    #endregion
}

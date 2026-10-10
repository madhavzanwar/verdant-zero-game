using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Gameplay HUD (GDD 6.2 and 9.3): a touch joystick to move and buttons held down
/// to climb, descend and fire the Beam. Passes its values to <see cref="QuadInput"/>.
/// Shows the remaining time from <see cref="GameSession"/> (MM:SS), the score and the trees planted.
/// Adjusts the safe area (notch) and hides the touch controls on devices without a touchscreen.
/// The camera button (also V or Select) switches between third and first person and remembers the choice.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GameHudController : MonoBehaviour
{
    [SerializeField] private QuadInput _input;
    [Tooltip("Show the touch controls on desktop too (useful for testing with the mouse).")]
    [SerializeField] private bool _showSticksOnDesktop;
    [SerializeField] private float _stickRadius = 110f;
    [Range(0f, 0.5f)]
    [SerializeField] private float _deadZone = 0.12f;
    [Tooltip("Remaining seconds below which the timer turns red.")]
    [SerializeField] private float _timerWarning = 30f;
    [Header("Map edge (GDD 5.4)")]
    [Tooltip("Half the side of the flight area, centered on the origin (invisible walls of City/Bounds).")]
    [SerializeField] private float _playAreaHalfSize = 77.5f;
    [Tooltip("At this distance from the wall \"Out of range\" appears.")]
    [SerializeField] private float _boundaryMargin = 6f;

    private VisualElement _root;
    private VisualElement _safeArea;
    private VisualElement _touchControls;
    private VirtualJoystick _moveStick;
    private HoldButton _upButton;
    private HoldButton _downButton;
    private HoldButton _beamButton;
    private HoldButton _seedButton;
    private QuadcopterCrash _crash;
    private GameSession _session;
    private Label _timer;
    private Label _score;
    private Label _trees;
    private int _shownScore = -1;
    private int _shownTrees = -1;
    private Label _boundary;
    private Label _voiceLine;
    private IVisualElementScheduledItem _voiceLineHide;
    private VisualElement _crosshair;
    private PurifierBeam _beam;
    private SeedLauncher _seeds;
    private VisualElement _seedElement;
    private IVisualElementScheduledItem _seedPulse;
    private VisualElement _beamElement;
    private IVisualElementScheduledItem _beamPulse;
    private int _shownSeconds = -1;
    private Rect _lastSafeArea;
    private Button _cameraButton;
    private CameraSwitchIcon _cameraIcon;
    private FollowCamera _followCamera;

    private void OnEnable()
    {
        _root = GetComponent<UIDocument>().rootVisualElement;
        _safeArea = _root.Q<VisualElement>("game-hud-safe-area");
        _touchControls = _root.Q<VisualElement>("touch-controls");
        _timer = _root.Q<Label>("hud-timer");
        _score = _root.Q<Label>("hud-score");
        _trees = _root.Q<Label>("hud-trees");
        _boundary = _root.Q<Label>("hud-boundary");
        _voiceLine = _root.Q<Label>("hud-voice-line");
        GameplayAudio.VoiceLinePlayed += ShowVoiceLine;
        _crosshair = _root.Q<VisualElement>("crosshair");

        _moveStick = CreateStick("touch-zone-left", "joystick--move");
        // On desktop web the drone moves with the keyboard: the joystick is not needed (the buttons stay,
        // they can be clicked with the mouse)
        if (IsDesktopWeb()) _root.Q<VisualElement>("touch-zone-left")?.AddToClassList("touch-zone--hidden");
        _upButton = new HoldButton(_root.Q<VisualElement>("btn-up"));
        _downButton = new HoldButton(_root.Q<VisualElement>("btn-down"));
        _beamElement = _root.Q<VisualElement>("btn-beam");
        _beamButton = new HoldButton(_beamElement);
        _seedElement = _root.Q<VisualElement>("btn-seed");
        _seedButton = new HoldButton(_seedElement);
        _seedPulse = _seedElement?.schedule.Execute(() => _seedElement.ToggleInClassList("action-button--pulse")).Every(350);
        // With a cloud in the sights the Beam button pulses, inviting the player to fire
        _beamPulse = _beamElement?.schedule.Execute(() => _beamElement.ToggleInClassList("action-button--pulse")).Every(350);

        if (_input == null) _input = FindAnyObjectByType<QuadInput>();
        _crash = _input != null ? _input.GetComponent<QuadcopterCrash>() : null;
        _beam = _input != null ? _input.GetComponent<PurifierBeam>() : null;
        _seeds = _input != null ? _input.GetComponent<SeedLauncher>() : null;
        _session = FindAnyObjectByType<GameSession>();
        if (_input != null) _input.MouseBlocker = IsOverControl;
        if (_timer != null) _timer.style.display = _session != null ? DisplayStyle.Flex : DisplayStyle.None;
        VisualElement stats = _root.Q<VisualElement>("hud-stats");
        if (stats != null) stats.style.display = _session != null ? DisplayStyle.Flex : DisplayStyle.None;

        // Camera button: the chosen view is remembered between runs. It is applied before the brain's first
        // frame, so the game starts directly in that view, with no blend.
        _cameraButton = _root.Q<Button>("btn-camera");
        if (Camera.main != null) _followCamera = Camera.main.GetComponent<FollowCamera>();
        if (_cameraButton != null)
        {
            if (_cameraIcon == null)
            {
                _cameraIcon = new CameraSwitchIcon();
                _cameraIcon.AddToClassList("camera-button__icon");
            }
            _cameraButton.Add(_cameraIcon);
            // No focus: otherwise Space (climb) would press it again
            _cameraButton.focusable = false;
            _cameraButton.clicked += ToggleCamera;
            _cameraButton.style.display = _followCamera != null ? DisplayStyle.Flex : DisplayStyle.None;
        }
        if (_followCamera != null && GameSettings.HasViewChoice)
        {
            _followCamera.SetMode(GameSettings.FirstPersonView ? FollowCamera.ViewMode.FirstPerson : FollowCamera.ViewMode.ThirdPerson);
        }

        _root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }

    private void OnDisable()
    {
        if (_input != null && _input.MouseBlocker == (System.Func<Vector2, bool>)IsOverControl) _input.MouseBlocker = null;
        _root?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        GameplayAudio.VoiceLinePlayed -= ShowVoiceLine;
        _voiceLineHide?.Pause();
        if (_cameraButton != null) _cameraButton.clicked -= ToggleCamera;
        ReleaseSticks();
        _beamPulse?.Pause();
        _seedPulse?.Pause();
        _seedButton?.Dispose();
        _upButton?.Dispose();
        _downButton?.Dispose();
        _beamButton?.Dispose();
    }

    private void Update()
    {
        if (Screen.safeArea != _lastSafeArea) ApplySafeArea();
        UpdateTimer();
        UpdateStats();
        UpdateBeamReady();
        UpdateBoundary();
        UpdateCrosshair();
        UpdateCameraButton();
        if (_input == null) return;

        // When the run ends (or after the crash) the controls do nothing and are hidden
        bool over = (_crash != null && _crash.HasCrashed) || (_session != null && _session.IsOver);
        SetSticksVisible(!over && HasTouchInput());
        if (over) return;

        // Fixed or dynamic depending on Options (can be changed from the pause menu)
        if (_moveStick != null) _moveStick.Floating = !GameSettings.FixedJoystick;
        _input.SetMove(_moveStick?.Value ?? Vector2.zero);
        float climb = (_upButton.IsPressed ? 1f : 0f) - (_downButton.IsPressed ? 1f : 0f);
        _input.SetClimb(climb);
        _input.SetBeam(_beamButton.IsPressed);
        _input.SetSeed(_seedButton.IsPressed);
    }

    // The crosshair marks where the Beam is going; it turns cyan when a cloud is locked
    private void UpdateCrosshair()
    {
        if (_crosshair == null) return;
        Camera cam = Camera.main;
        bool over = (_crash != null && _crash.HasCrashed) || (_session != null && _session.IsOver);
        if (_beam == null || cam == null || over || _crosshair.panel == null)
        {
            _crosshair.style.display = DisplayStyle.None;
            return;
        }

        Vector3 aimPoint = _beam.AimOrigin + _beam.AimDirection * (_beam.Range * 0.6f);
        Vector3 screen = cam.WorldToScreenPoint(aimPoint);
        if (screen.z <= 0f)
        {
            _crosshair.style.display = DisplayStyle.None;
            return;
        }

        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(_crosshair.panel, new Vector2(screen.x, Screen.height - screen.y));
        _crosshair.style.display = DisplayStyle.Flex;
        _crosshair.style.left = panelPos.x;
        _crosshair.style.top = panelPos.y;
        _crosshair.EnableInClassList("crosshair--locked", _beam.CurrentTarget != null);
    }

    // Highlights the button in first person and handles V on the keyboard and Select (View) on the gamepad.
    // Hidden when the run ends or on crash, like the other controls.
    private void UpdateCameraButton()
    {
        if (_cameraButton == null || _followCamera == null) return;

        bool over = (_crash != null && _crash.HasCrashed) || (_session != null && _session.IsOver);
        _cameraButton.style.display = over ? DisplayStyle.None : DisplayStyle.Flex;
        if (over) return;

        bool firstPerson = _followCamera.Mode == FollowCamera.ViewMode.FirstPerson;
        _cameraButton.EnableInClassList("camera-button--first-person", firstPerson);
        if (_cameraIcon != null) _cameraIcon.Highlighted = firstPerson;

        // While paused (timeScale 0) the view does not change
        if (Time.timeScale <= 0f) return;
        bool key = Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame;
        bool select = Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame;
        if (key || select) ToggleCamera();
    }

    // Each voice line appears in the center while it plays and fades out when it ends
    private void ShowVoiceLine(string text, float duration)
    {
        if (_voiceLine == null) return;
        _voiceLine.text = text;
        _voiceLine.AddToClassList("hud-voice-line--visible");
        _voiceLineHide?.Pause();
        _voiceLineHide = _voiceLine.schedule.Execute(() => _voiceLine.RemoveFromClassList("hud-voice-line--visible"))
            .StartingIn((long)(Mathf.Max(duration, 1.5f) * 1000f) + 300);
    }

    private void ToggleCamera()
    {
        if (_followCamera == null || !_followCamera.FollowYaw || Time.timeScale <= 0f) return;
        _followCamera.ToggleView();
        GameSettings.FirstPersonView = _followCamera.Mode == FollowCamera.ViewMode.FirstPerson;
        GameSettings.Save();
    }

    private void UpdateBoundary()
    {
        if (_boundary == null) return;
        bool near = false;
        if (_input != null && (_session == null || !_session.IsOver))
        {
            Vector3 p = _input.transform.position;
            float limit = _playAreaHalfSize - _boundaryMargin;
            near = Mathf.Abs(p.x) > limit || Mathf.Abs(p.z) > limit;
        }
        _boundary.EnableInClassList("hud-boundary--visible", near);
    }

    private void UpdateBeamReady()
    {
        SetReady(_beamElement, _beam != null && _beam.CurrentTarget != null);
        SetReady(_seedElement, _seeds != null && _seeds.CurrentTarget != null);
    }

    // With a target in range the button pulses, inviting the player to press it
    private static void SetReady(VisualElement button, bool ready)
    {
        if (button == null) return;
        button.EnableInClassList("action-button--ready", ready);
        if (!ready) button.RemoveFromClassList("action-button--pulse");
    }

    private void UpdateTimer()
    {
        if (_timer == null || _session == null) return;

        // Rounded up: shows 00:00 only once time has run out
        int seconds = Mathf.CeilToInt(_session.TimeRemaining);
        if (seconds == _shownSeconds) return;

        _shownSeconds = seconds;
        _timer.text = $"{seconds / 60:00}:{seconds % 60:00}";
        _timer.EnableInClassList("hud-timer--warning", seconds <= _timerWarning);
    }

    private void UpdateStats()
    {
        if (_session == null) return;

        if (_score != null && _session.Score != _shownScore)
        {
            bool bump = _shownScore >= 0;
            _shownScore = _session.Score;
            _score.text = _shownScore.ToString("N0");
            if (bump) Bump(_score);
        }

        if (_trees != null && _session.TreesPlanted != _shownTrees)
        {
            bool bump = _shownTrees >= 0;
            _shownTrees = _session.TreesPlanted;
            _trees.text = $"TREES {_shownTrees}/{_session.TotalPlantingSpots}";
            if (bump) Bump(_trees);
        }
    }

    private static void Bump(VisualElement element)
    {
        element.AddToClassList("hud-stat--bump");
        element.schedule.Execute(() => element.RemoveFromClassList("hud-stat--bump")).StartingIn(150);
    }

    // True if the screen position (origin bottom left) falls on a touch control, the pause
    // button or an open panel: there a mouse click must not fire the Beam or launch seeds
    private bool IsOverControl(Vector2 screenPosition)
    {
        if (_root?.panel == null) return false;
        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
        VisualElement picked = _root.panel.Pick(panelPos);
        // Only this HUD counts: the panel also holds the smog bar and the results screen
        return picked != null && picked != _root && _root.Contains(picked);
    }

    // Real phone, Device Simulator (creates a Touchscreen) or touch PC
    private bool HasTouchInput() => Application.isMobilePlatform || Touchscreen.current != null || _showSticksOnDesktop;

    // WebGL in a desktop browser (on mobile Application.isMobilePlatform is true)
    private bool IsDesktopWeb() => Application.platform == RuntimePlatform.WebGLPlayer && !Application.isMobilePlatform && !_showSticksOnDesktop;

    private void SetSticksVisible(bool visible)
    {
        if (_touchControls == null) return;
        DisplayStyle wanted = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (_touchControls.style.display == wanted) return;
        if (!visible) ReleaseSticks();
        _touchControls.style.display = wanted;
    }

    /// <summary>Releases the joystick and buttons and sets external input to 0 (e.g. when pausing).</summary>
    public void ReleaseSticks()
    {
        _moveStick?.Release();
        _upButton?.Release();
        _downButton?.Release();
        _beamButton?.Release();
        _seedButton?.Release();
        if (_input != null)
        {
            _input.SetMove(Vector2.zero);
            _input.SetClimb(0f);
            _input.SetBeam(false);
            _input.SetSeed(false);
        }
    }

    private VirtualJoystick CreateStick(string zoneName, string modifierClass)
    {
        VisualElement zone = _root.Q<VisualElement>(zoneName);
        if (zone == null) return null;

        zone.Clear();
        var stick = new VirtualJoystick { Radius = _stickRadius, DeadZone = _deadZone };
        stick.AddToClassList(modifierClass);
        zone.Add(stick);
        return stick;
    }

    private void OnGeometryChanged(GeometryChangedEvent evt) => ApplySafeArea();

    private void ApplySafeArea()
    {
        _lastSafeArea = Screen.safeArea;
        if (_safeArea == null || Screen.width <= 0 || Screen.height <= 0) return;

        // Screen pixels -> panel units (PanelSettings scales the UI)
        float scaleX = _root.layout.width / Screen.width;
        float scaleY = _root.layout.height / Screen.height;
        if (float.IsNaN(scaleX) || float.IsNaN(scaleY)) return;

        Rect area = Screen.safeArea;
        _safeArea.style.left = area.xMin * scaleX;
        _safeArea.style.right = (Screen.width - area.xMax) * scaleX;
        _safeArea.style.top = (Screen.height - area.yMax) * scaleY;
        _safeArea.style.bottom = area.yMin * scaleY;
    }

    /// <summary>
    /// Touch button that counts while held down. Captures its pointer,
    /// so it works at the same time as the joystick (multitouch).
    /// </summary>
    private sealed class HoldButton
    {
        private const string PressedClass = "hud-button--pressed";
        private readonly VisualElement _element;
        private int _pointerId = -1;

        public HoldButton(VisualElement element)
        {
            _element = element;
            if (_element == null) return;
            _element.RegisterCallback<PointerDownEvent>(OnDown);
            _element.RegisterCallback<PointerUpEvent>(OnUp);
            _element.RegisterCallback<PointerCancelEvent>(OnCancel);
        }

        public bool IsPressed => _pointerId >= 0;

        public void Release()
        {
            if (_element != null && _pointerId >= 0 && _element.HasPointerCapture(_pointerId))
            {
                _element.ReleasePointer(_pointerId);
            }
            _pointerId = -1;
            _element?.RemoveFromClassList(PressedClass);
        }

        public void Dispose()
        {
            if (_element == null) return;
            _element.UnregisterCallback<PointerDownEvent>(OnDown);
            _element.UnregisterCallback<PointerUpEvent>(OnUp);
            _element.UnregisterCallback<PointerCancelEvent>(OnCancel);
        }

        private void OnDown(PointerDownEvent evt)
        {
            if (_pointerId >= 0) return;
            _pointerId = evt.pointerId;
            _element.CapturePointer(evt.pointerId);
            _element.AddToClassList(PressedClass);
            evt.StopPropagation();
        }

        private void OnUp(PointerUpEvent evt)
        {
            if (evt.pointerId == _pointerId) Release();
        }

        private void OnCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId == _pointerId) Release();
        }
    }
}

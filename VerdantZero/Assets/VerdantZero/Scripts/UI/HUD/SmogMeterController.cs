using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// HUD smog level bar. Reads <see cref="GameSession.Smog"/> (0-1) and shows it
/// with an animated fill, percentage and color (green = clean, reddish brown = heavily polluted).
/// Above the critical threshold the border flashes. An icon (<see cref="SmogStateIcon"/>) shows the state
/// with a distinct shape (lungs, cloud with a mask, alert), so it does not rely on color alone.
/// Without a GameSession it uses <see cref="SmogClouds.Density"/>; it can also be fed with <see cref="SetLevel"/>.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class SmogMeterController : MonoBehaviour
{
    [Tooltip("Level source. If left empty, it is searched for in the scene.")]
    [SerializeField] private GameSession _session;
    [Tooltip("Fallback source if the scene has no GameSession.")]
    [SerializeField] private SmogClouds _source;
    [Tooltip("Speed at which the bar reaches the real value.")]
    [SerializeField] private float _smoothing = 4f;
    [Range(0f, 1f)]
    [SerializeField] private float _criticalThreshold = 0.75f;
    [SerializeField] private float _pulseInterval = 0.5f;
    [Tooltip("Below this level the icon shows lungs (clean air); above it, the cloud with a mask.")]
    [Range(0f, 1f)]
    [SerializeField] private float _cleanThreshold = 0.4f;
    [SerializeField] private Gradient _colors = DefaultGradient();

    private VisualElement _root;
    private VisualElement _safeArea;
    private VisualElement _meter;
    private VisualElement _fill;
    private Label _value;
    private SmogStateIcon _icon;
    private IVisualElementScheduledItem _pulse;
    private Rect _lastSafeArea;

    private float _target;
    private float _displayed = -1f;
    private int _shownPercent = -1;

    /// <summary>Displayed target level (0-1).</summary>
    public float Level => _target;

    /// <summary>Sets the level manually (ignores the source).</summary>
    public void SetLevel(float level)
    {
        _session = null;
        _source = null;
        _target = Mathf.Clamp01(level);
    }

    private void OnEnable()
    {
        _root = GetComponent<UIDocument>().rootVisualElement;
        _safeArea = _root.Q<VisualElement>("hud-safe-area");
        _meter = _root.Q<VisualElement>("smog-meter");
        _fill = _root.Q<VisualElement>("smog-meter-fill");
        _value = _root.Q<Label>("smog-meter-value");
        VisualElement header = _root.Q<VisualElement>(className: "smog-meter__header");
        if (header != null && _icon == null)
        {
            _icon = new SmogStateIcon();
            _icon.AddToClassList("smog-meter__icon");
            header.Insert(0, _icon);
        }

        if (_session == null) _session = FindAnyObjectByType<GameSession>();
        if (_session == null && _source == null) _source = FindAnyObjectByType<SmogClouds>();
        ReadSource();

        _root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        _pulse = _meter?.schedule.Execute(() => _meter.ToggleInClassList("smog-meter--pulse"))
            .Every((long)(_pulseInterval * 1000));
    }

    private void OnDisable()
    {
        _root?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        _pulse?.Pause();
    }

    private void Update()
    {
        ReadSource();
        if (Screen.safeArea != _lastSafeArea) ApplySafeArea();
        if (_fill == null) return;

        // First frame: no animation
        _displayed = _displayed < 0f
            ? _target
            : Mathf.Lerp(_displayed, _target, 1f - Mathf.Exp(-_smoothing * Time.unscaledDeltaTime));

        _fill.style.width = Length.Percent(_displayed * 100f);
        _fill.style.backgroundColor = _colors.Evaluate(_displayed);

        int percent = Mathf.RoundToInt(_displayed * 100f);
        if (percent != _shownPercent && _value != null)
        {
            _shownPercent = percent;
            _value.text = percent + "%";
        }

        bool critical = _displayed >= _criticalThreshold;
        if (_icon != null)
        {
            _icon.Current = critical ? SmogStateIcon.State.Alert
                : _displayed < _cleanThreshold ? SmogStateIcon.State.Clean
                : SmogStateIcon.State.Polluted;
        }
        _meter.EnableInClassList("smog-meter--critical", critical);
        if (!critical) _meter.RemoveFromClassList("smog-meter--pulse");
    }

    private void ReadSource()
    {
        if (_session != null) _target = _session.Smog;
        else if (_source != null) _target = _source.Density;
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

    private static Gradient DefaultGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.47f, 0.90f, 0.67f), 0f),   // clean air
                new GradientColorKey(new Color(0.90f, 0.80f, 0.35f), 0.5f), // polluted
                new GradientColorKey(new Color(0.80f, 0.40f, 0.25f), 1f),   // heavily polluted
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return gradient;
    }
}

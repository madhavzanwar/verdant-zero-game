using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// HUD markers over every smog cloud and every free planting spot (GDD 10.3: icons in addition
/// to colors). They are drawn in the UI, so they stay visible at a distance and even behind buildings.
/// Whatever is in the sights changes look: "SHOOT!" on clouds, "PLANT!" on spots.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class CloudMarkersController : MonoBehaviour
{
    [Tooltip("Meters above the top edge of the cloud.")]
    [SerializeField] private float _heightOffset = 1.5f;
    [Tooltip("Distance at which the marker is shown at full size; farther away, it shrinks.")]
    [SerializeField] private float _fullSizeDistance = 20f;
    [SerializeField] private float _minScale = 0.7f;
    [Tooltip("Minimum margin from the screen edge, in panel units.")]
    [SerializeField] private float _edgeMargin = 100f;
    [Tooltip("Top margin: below the smog bar and the timer.")]
    [SerializeField] private float _topMargin = 260f;
    [Tooltip("Beyond this distance the marker is not shown.")]
    [SerializeField] private float _maxDistance = 140f;

    private const string MarkerClass = "cloud-marker";
    private const string MicroClass = "cloud-marker--micro";
    private const string TargetClass = "cloud-marker--target";
    private const string SpotClass = "spot-marker";

    private readonly Dictionary<Object, VisualElement> _markers = new Dictionary<Object, VisualElement>();
    private readonly HashSet<Object> _seen = new HashSet<Object>();
    private readonly List<Object> _stale = new List<Object>();
    private readonly Stack<VisualElement> _pool = new Stack<VisualElement>();

    private VisualElement _layer;
    private Camera _camera;
    private GameSession _session;

    private void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        _layer = root.Q<VisualElement>("cloud-markers");
        _session = FindAnyObjectByType<GameSession>();
    }

    private void OnDisable()
    {
        foreach (VisualElement marker in _markers.Values) Recycle(marker);
        _markers.Clear();
    }

    private void LateUpdate()
    {
        if (_layer == null || _layer.panel == null) return;
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;

        bool hidden = _session != null && _session.IsOver;
        _layer.style.display = hidden ? DisplayStyle.None : DisplayStyle.Flex;
        if (hidden) return;

        _seen.Clear();
        foreach (SmogCloud cloud in SmogCloud.Active)
        {
            VisualElement marker = Get(cloud, cloud.IsMicro, false);
            Place(cloud.transform.position + Vector3.up * (cloud.Radius + _heightOffset), cloud.IsTargeted, marker);
        }
        foreach (PlantingSpot spot in PlantingSpot.Available)
        {
            VisualElement marker = Get(spot, false, true);
            Place(spot.AimPoint + Vector3.up * _heightOffset, spot.IsTargeted, marker);
        }

        // Remove markers for things that are gone (cleared cloud, planted spot)
        _stale.Clear();
        foreach (Object key in _markers.Keys)
        {
            if (!_seen.Contains(key)) _stale.Add(key);
        }
        foreach (Object key in _stale)
        {
            Recycle(_markers[key]);
            _markers.Remove(key);
        }
    }

    private VisualElement Get(Object key, bool micro, bool spot)
    {
        _seen.Add(key);
        if (_markers.TryGetValue(key, out VisualElement marker)) return marker;

        marker = Rent();
        marker.EnableInClassList(MicroClass, micro);
        marker.EnableInClassList(SpotClass, spot);
        marker.Q<Label>().text = spot ? "PLANT!" : "SHOOT!";
        _markers.Add(key, marker);
        return marker;
    }

    private void Place(Vector3 world, bool targeted, VisualElement marker)
    {
        Vector3 screen = _camera.WorldToScreenPoint(world);
        float distance = screen.z;

        // Behind the camera or too far away: hidden
        if (distance <= 0.5f || distance > _maxDistance)
        {
            marker.style.display = DisplayStyle.None;
            return;
        }

        // Screen pixels (origin at bottom) -> panel coordinates (origin at top)
        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(_layer.panel, new Vector2(screen.x, Screen.height - screen.y));
        float scale = Mathf.Clamp(_fullSizeDistance / distance, _minScale, 1f);

        // Keep the marker on screen (if the cloud is very high, the target sits at the edge)
        Rect area = _layer.layout;
        if (area.width > 0f)
        {
            panelPos.x = Mathf.Clamp(panelPos.x, _edgeMargin, area.width - _edgeMargin);
            panelPos.y = Mathf.Clamp(panelPos.y, _topMargin, area.height - _edgeMargin);
        }

        marker.style.display = DisplayStyle.Flex;
        marker.style.left = panelPos.x;
        marker.style.top = panelPos.y;
        marker.style.scale = new Scale(new Vector3(scale, scale, 1f));
        marker.EnableInClassList(TargetClass, targeted);
    }

    private VisualElement Rent()
    {
        VisualElement marker = _pool.Count > 0 ? _pool.Pop() : CreateMarker();
        marker.RemoveFromClassList(TargetClass);
        marker.style.display = DisplayStyle.Flex;
        _layer.Add(marker);
        return marker;
    }

    private void Recycle(VisualElement marker)
    {
        marker.RemoveFromHierarchy();
        _pool.Push(marker);
    }

    // Target: ring + center dot + label, all in USS (no reliance on font glyphs)
    private static VisualElement CreateMarker()
    {
        var marker = new VisualElement { pickingMode = PickingMode.Ignore };
        marker.AddToClassList(MarkerClass);

        var ring = new VisualElement { pickingMode = PickingMode.Ignore };
        ring.AddToClassList("cloud-marker__ring");
        var dot = new VisualElement { pickingMode = PickingMode.Ignore };
        dot.AddToClassList("cloud-marker__dot");
        ring.Add(dot);
        marker.Add(ring);

        var label = new Label("SHOOT!") { pickingMode = PickingMode.Ignore };
        label.AddToClassList("cloud-marker__label");
        marker.Add(label);
        return marker;
    }
}

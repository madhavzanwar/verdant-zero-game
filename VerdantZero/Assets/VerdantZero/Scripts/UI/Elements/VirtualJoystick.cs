using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit touch joystick. The element itself is the touch zone: when touched, the base
/// appears under the finger (floating mode) and the knob follows the finger up to <see cref="Radius"/>.
/// Each joystick captures its own pointer, so several work at once (multitouch).
/// </summary>
public class VirtualJoystick : VisualElement
{
    public const string UssClass = "joystick";
    public const string BaseUssClass = "joystick__base";
    public const string KnobUssClass = "joystick__knob";
    public const string ActiveUssClass = "joystick--active";

    private readonly VisualElement _base;
    private readonly VisualElement _knob;
    private int _pointerId = -1;
    private Vector2 _center;

    /// <summary>Maximum knob travel in panel units.</summary>
    public float Radius { get; set; } = 110f;

    /// <summary>Below this magnitude the output is 0.</summary>
    public float DeadZone { get; set; } = 0.12f;

    /// <summary>If true, the base is placed where the touch starts.</summary>
    public bool Floating { get; set; } = true;

    /// <summary>Resting position of the base, relative to the zone size (0-1).</summary>
    public Vector2 RestAnchor { get; set; } = new Vector2(0.5f, 0.62f);

    /// <summary>Output in [-1, 1]; +Y is up.</summary>
    public Vector2 Value { get; private set; }

    public bool IsActive => _pointerId >= 0;

    public VirtualJoystick()
    {
        AddToClassList(UssClass);

        _base = new VisualElement { pickingMode = PickingMode.Ignore };
        _base.AddToClassList(BaseUssClass);
        _knob = new VisualElement { pickingMode = PickingMode.Ignore };
        _knob.AddToClassList(KnobUssClass);
        _base.Add(_knob);
        Add(_base);

        RegisterCallback<PointerDownEvent>(OnPointerDown);
        RegisterCallback<PointerMoveEvent>(OnPointerMove);
        RegisterCallback<PointerUpEvent>(OnPointerUp);
        RegisterCallback<PointerCancelEvent>(_ => Release());
        RegisterCallback<PointerCaptureOutEvent>(_ => Release());
        RegisterCallback<GeometryChangedEvent>(_ => { if (!IsActive) MoveBaseTo(RestPosition()); });
    }

    /// <summary>Releases the joystick and sets the output to 0 (e.g. when pausing).</summary>
    public void Release()
    {
        int id = _pointerId;
        _pointerId = -1; // before releasing: ReleasePointer fires PointerCaptureOut, which calls back in here
        if (id >= 0 && this.HasPointerCapture(id)) this.ReleasePointer(id);
        Value = Vector2.zero;
        RemoveFromClassList(ActiveUssClass);
        _knob.style.translate = new Translate(0, 0);
        MoveBaseTo(RestPosition());
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (IsActive) return;

        _pointerId = evt.pointerId;
        this.CapturePointer(_pointerId);
        AddToClassList(ActiveUssClass);

        Vector2 local = evt.localPosition;
        MoveBaseTo(Floating ? ClampInside(local) : RestPosition());
        UpdateKnob(local);
        evt.StopPropagation();
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (evt.pointerId != _pointerId) return;
        UpdateKnob(evt.localPosition);
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (evt.pointerId != _pointerId) return;
        Release();
    }

    private void UpdateKnob(Vector2 local)
    {
        Vector2 offset = Vector2.ClampMagnitude(local - _center, Radius);
        _knob.style.translate = new Translate(offset.x, offset.y);

        Vector2 raw = offset / Radius;
        raw.y = -raw.y; // UI: y points down; output: +y points up
        float magnitude = raw.magnitude;
        Value = magnitude < DeadZone
            ? Vector2.zero
            : raw.normalized * Mathf.InverseLerp(DeadZone, 1f, magnitude);
    }

    private void MoveBaseTo(Vector2 center)
    {
        _center = center;
        // The base is centered on the point with translate -50% (see USS)
        _base.style.left = center.x;
        _base.style.top = center.y;
    }

    private Vector2 RestPosition()
    {
        Rect r = contentRect;
        return new Vector2(r.width * RestAnchor.x, r.height * RestAnchor.y);
    }

    private Vector2 ClampInside(Vector2 p)
    {
        Rect r = contentRect;
        float margin = Radius * 0.9f;
        return new Vector2(
            Mathf.Clamp(p.x, margin, Mathf.Max(margin, r.width - margin)),
            Mathf.Clamp(p.y, margin, Mathf.Max(margin, r.height - margin)));
    }
}

using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// HUD camera button icon: a photo camera surrounded by two circular arrows
/// (switch view). In first person it is painted in the accent green to mark the active view.
/// Drawn with vectors (Painter2D), no textures, so it stays sharp at any resolution.
/// </summary>
public class CameraSwitchIcon : VisualElement
{
    // Drawing coordinates use a 44 x 44 grid
    private const float Grid = 44f;

    private static readonly Color IconColor = new Color(0.88f, 0.92f, 0.90f);
    private static readonly Color AccentColor = new Color(0.47f, 0.90f, 0.67f);
    private static readonly Color InkColor = new Color(0.06f, 0.11f, 0.15f);

    private bool _highlighted;

    public CameraSwitchIcon()
    {
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
    }

    /// <summary>True to paint it in the accent color (first-person view).</summary>
    public bool Highlighted
    {
        get => _highlighted;
        set
        {
            if (value == _highlighted) return;
            _highlighted = value;
            MarkDirtyRepaint();
        }
    }

    private void Draw(MeshGenerationContext context)
    {
        Rect rect = contentRect;
        float size = Mathf.Min(rect.width, rect.height);
        if (size <= 0f) return;

        float scale = size / Grid;
        Vector2 origin = rect.center - new Vector2(size, size) * 0.5f;
        Vector2 P(float x, float y) => origin + new Vector2(x, y) * scale;

        Painter2D p = context.painter2D;
        p.lineJoin = LineJoin.Round;
        p.lineCap = LineCap.Round;
        Color color = _highlighted ? AccentColor : IconColor;

        // Camera body: rounded rectangle with the viewfinder on top
        p.fillColor = color;
        p.BeginPath();
        p.MoveTo(P(17f, 15f));
        p.LineTo(P(19f, 12f));
        p.LineTo(P(25f, 12f));
        p.LineTo(P(27f, 15f));
        p.ArcTo(P(32f, 15f), P(32f, 18f), 3f * scale);
        p.ArcTo(P(32f, 30f), P(29f, 30f), 3f * scale);
        p.ArcTo(P(12f, 30f), P(12f, 27f), 3f * scale);
        p.ArcTo(P(12f, 15f), P(15f, 15f), 3f * scale);
        p.ClosePath();
        p.Fill();

        // Lens: dark hole with a ring inside
        p.fillColor = InkColor;
        p.BeginPath();
        p.Arc(P(22f, 22.5f), 5f * scale, Angle.Degrees(0f), Angle.Degrees(360f));
        p.Fill();
        p.fillColor = color;
        p.BeginPath();
        p.Arc(P(22f, 22.5f), 2.6f * scale, Angle.Degrees(0f), Angle.Degrees(360f));
        p.Fill();

        // Two circular arrows around it, clockwise (top and bottom)
        DrawArrowArc(p, P(22f, 22f), 18f, 200f, 320f, scale, color);
        DrawArrowArc(p, P(22f, 22f), 18f, 20f, 140f, scale, color);
    }

    // Arc with an arrowhead at the end. Angles in degrees with y pointing down (0 = right, 90 = down).
    private static void DrawArrowArc(Painter2D p, Vector2 center, float radius, float from, float to, float scale, Color color)
    {
        float r = radius * scale;
        p.strokeColor = color;
        p.lineWidth = 3f * scale;
        p.BeginPath();
        p.Arc(center, r, Angle.Degrees(from), Angle.Degrees(to - 6f));
        p.Stroke();

        float a = to * Mathf.Deg2Rad;
        Vector2 radial = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        Vector2 tangent = new Vector2(-radial.y, radial.x);
        Vector2 end = center + radial * r;
        float head = 4.5f * scale;

        p.fillColor = color;
        p.BeginPath();
        p.MoveTo(end + tangent * head);
        p.LineTo(end - tangent * head * 0.6f + radial * head);
        p.LineTo(end - tangent * head * 0.6f - radial * head);
        p.ClosePath();
        p.Fill();
    }
}

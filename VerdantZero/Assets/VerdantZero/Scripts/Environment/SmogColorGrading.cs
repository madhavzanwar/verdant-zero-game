using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Color grading that follows the smog (GDD 8.6 and 9.1): with smog the image looks dull and yellowish;
/// as the air clears it regains saturation and contrast. Drives the Color Adjustments of a global Volume
/// (on a copy of the profile, so the asset is not touched). The thresholds match <see cref="SkyPurification"/>.
/// </summary>
[RequireComponent(typeof(Volume))]
public class SmogColorGrading : MonoBehaviour
{
    [SerializeField] private GameSession _session;

    [Header("Smog thresholds (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float _dirtySmog = 0.5f;
    [Range(0f, 1f)]
    [SerializeField] private float _cleanSmog = 0.05f;
    [SerializeField] private float _smoothing = 1.5f;

    [Header("With smog")]
    [Range(-100f, 100f)]
    [SerializeField] private float _dirtySaturation = -40f;
    [Range(-100f, 100f)]
    [SerializeField] private float _dirtyContrast = -12f;
    [SerializeField] private Color _dirtyFilter = new Color(1f, 0.93f, 0.8f);

    [Header("Clean air")]
    [Range(-100f, 100f)]
    [SerializeField] private float _cleanSaturation = 15f;
    [Range(-100f, 100f)]
    [SerializeField] private float _cleanContrast = 8f;
    [SerializeField] private Color _cleanFilter = Color.white;

    private ColorAdjustments _adjustments;
    private float _clean = -1f;

    private void Start()
    {
        if (_session == null) _session = FindAnyObjectByType<GameSession>();

        Volume volume = GetComponent<Volume>();
        // volume.profile creates a copy of the shared profile: changes are not saved to the asset
        VolumeProfile profile = volume.profile;
        if (!profile.TryGet(out _adjustments)) _adjustments = profile.Add<ColorAdjustments>();
        _adjustments.active = true;
        _adjustments.saturation.overrideState = true;
        _adjustments.contrast.overrideState = true;
        _adjustments.colorFilter.overrideState = true;

        Apply(Target());
    }

    private void Update()
    {
        if (_adjustments == null) return;
        float k = 1f - Mathf.Exp(-_smoothing * Time.deltaTime);
        Apply(Mathf.Lerp(_clean, Target(), k));
    }

    private float Target() => _session != null ? Mathf.InverseLerp(_dirtySmog, _cleanSmog, _session.Smog) : 0f;

    private void Apply(float clean)
    {
        if (Mathf.Abs(clean - _clean) < 0.0005f) return;
        _clean = clean;
        _adjustments.saturation.value = Mathf.Lerp(_dirtySaturation, _cleanSaturation, clean);
        _adjustments.contrast.value = Mathf.Lerp(_dirtyContrast, _cleanContrast, clean);
        _adjustments.colorFilter.value = Color.Lerp(_dirtyFilter, _cleanFilter, clean);
    }
}

using UnityEngine;

/// <summary>
/// The sky clears as the smog drops (GDD 8.6 and 9.1).
/// Blends the smoggy skybox with the clean sky (shader VerdantZero/Skybox/Panoramic Blend)
/// and moves the fog, ambient light and sun toward their clean-air values.
/// The smoggy values are the ones the scene has at the start.
/// </summary>
public class SkyPurification : MonoBehaviour
{
    private static readonly int BlendId = Shader.PropertyToID("_Blend");

    [SerializeField] private GameSession _session;

    [Header("Smog thresholds (0-1)")]
    [Tooltip("Above this level the sky looks fully polluted.")]
    [Range(0f, 1f)]
    [SerializeField] private float _dirtySmog = 0.5f;
    [Tooltip("Below this level the sky looks fully clean.")]
    [Range(0f, 1f)]
    [SerializeField] private float _cleanSmog = 0.05f;
    [Tooltip("Speed at which the sky follows the smog level.")]
    [SerializeField] private float _smoothing = 1.5f;

    [Header("Clean air")]
    [SerializeField] private Color _cleanFogColor = new Color(0.74f, 0.80f, 0.86f);
    [SerializeField] private float _cleanFogDensity = 0.004f;
    [SerializeField] private Color _cleanAmbientSky = new Color(0.62f, 0.74f, 0.92f);
    [SerializeField] private Color _cleanAmbientEquator = new Color(0.72f, 0.70f, 0.64f);
    [SerializeField] private Color _cleanAmbientGround = new Color(0.30f, 0.29f, 0.25f);

    [Header("Sun (optional)")]
    [SerializeField] private Light _sun;
    [SerializeField] private Color _cleanSunColor = new Color(1f, 0.90f, 0.76f);
    [SerializeField] private float _cleanSunIntensity = 1.5f;

    private Material _sky;
    private bool _ownsSky;
    private Material _originalSky;

    private Color _dirtyFogColor;
    private float _dirtyFogDensity;
    private Color _dirtyAmbientSky;
    private Color _dirtyAmbientEquator;
    private Color _dirtyAmbientGround;
    private Color _dirtySunColor;
    private float _dirtySunIntensity;

    private float _blend = -1f;

    /// <summary>How much the sky has cleared (0 = smoggy, 1 = clean).</summary>
    public float Blend => Mathf.Max(0f, _blend);

    private void Start()
    {
        if (_session == null) _session = FindAnyObjectByType<GameSession>();
        if (_sun == null) _sun = RenderSettings.sun;

        // Use the SkyboxRotator's skybox copy, or create one so the asset is not touched
        SkyboxRotator rotator = FindAnyObjectByType<SkyboxRotator>();
        _sky = rotator != null ? rotator.Instance : null;
        if (_sky == null && RenderSettings.skybox != null)
        {
            _originalSky = RenderSettings.skybox;
            _sky = new Material(_originalSky);
            _ownsSky = true;
            RenderSettings.skybox = _sky;
        }

        if (_sky != null && !_sky.HasProperty(BlendId))
        {
            Debug.LogWarning("SkyPurification: the skybox does not use the Panoramic Blend shader; only the fog and light will change.", this);
            _sky = null;
        }

        _dirtyFogColor = RenderSettings.fogColor;
        _dirtyFogDensity = RenderSettings.fogDensity;
        _dirtyAmbientSky = RenderSettings.ambientSkyColor;
        _dirtyAmbientEquator = RenderSettings.ambientEquatorColor;
        _dirtyAmbientGround = RenderSettings.ambientGroundColor;
        if (_sun != null)
        {
            _dirtySunColor = _sun.color;
            _dirtySunIntensity = _sun.intensity;
        }
    }

    private void Update()
    {
        if (_session == null) return;

        float target = Mathf.InverseLerp(_dirtySmog, _cleanSmog, _session.Smog);
        // First frame: no transition
        _blend = _blend < 0f
            ? target
            : Mathf.Lerp(_blend, target, 1f - Mathf.Exp(-_smoothing * Time.deltaTime));

        Apply(_blend);
    }

    private void Apply(float t)
    {
        if (_sky != null) _sky.SetFloat(BlendId, t);

        RenderSettings.fogColor = Color.Lerp(_dirtyFogColor, _cleanFogColor, t);
        RenderSettings.fogDensity = Mathf.Lerp(_dirtyFogDensity, _cleanFogDensity, t);
        RenderSettings.ambientSkyColor = Color.Lerp(_dirtyAmbientSky, _cleanAmbientSky, t);
        RenderSettings.ambientEquatorColor = Color.Lerp(_dirtyAmbientEquator, _cleanAmbientEquator, t);
        RenderSettings.ambientGroundColor = Color.Lerp(_dirtyAmbientGround, _cleanAmbientGround, t);

        if (_sun != null)
        {
            _sun.color = Color.Lerp(_dirtySunColor, _cleanSunColor, t);
            _sun.intensity = Mathf.Lerp(_dirtySunIntensity, _cleanSunIntensity, t);
        }
    }

    private void OnDestroy()
    {
        // Leave the scene as it was (matters when exiting Play Mode in the Editor)
        if (_blend >= 0f) Apply(0f);
        if (!_ownsSky || _sky == null) return;
        if (RenderSettings.skybox == _sky) RenderSettings.skybox = _originalSky;
        Destroy(_sky);
    }
}

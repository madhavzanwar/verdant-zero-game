using UnityEngine;

/// <summary>
/// Controls the street-level smog clouds (a ParticleSystem of large, soft blobs).
/// <see cref="Density"/> (0-1) scales how many clouds there are and how opaque they are; lowering it
/// gradually "cleans" the air (e.g. when the player removes smog).
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class SmogClouds : MonoBehaviour
{
    [Range(0f, 1f)]
    [SerializeField] private float _density = 0.8f;

    [Tooltip("Number of clouds at density 1.")]
    [SerializeField] private int _maxClouds = 75;

    [Tooltip("Opacity of each cloud at density 1.")]
    [Range(0f, 1f)]
    [SerializeField] private float _maxAlpha = 0.65f;

    [SerializeField] private Color _colorA = new Color(0.66f, 0.61f, 0.48f);
    [SerializeField] private Color _colorB = new Color(0.55f, 0.52f, 0.42f);

    private ParticleSystem _particles;

    public float Density
    {
        get => _density;
        set
        {
            _density = Mathf.Clamp01(value);
            Apply();
        }
    }

    private void Awake()
    {
        _particles = GetComponent<ParticleSystem>();
        Apply();
    }

    private void OnValidate()
    {
        _particles = GetComponent<ParticleSystem>();
        Apply();
    }

    private void Apply()
    {
        if (_particles == null) return;

        ParticleSystem.MainModule main = _particles.main;
        int count = Mathf.RoundToInt(_maxClouds * _density);
        main.maxParticles = Mathf.Max(1, count);

        // More density = more opaque clouds (square root so they are still visible at medium density)
        float alpha = _maxAlpha * Mathf.Sqrt(_density);
        Color a = _colorA; a.a = alpha;
        Color b = _colorB; b.a = alpha * 0.8f;
        main.startColor = new ParticleSystem.MinMaxGradient(a, b);

        // Steady emission: replace the clouds that expire
        float lifetime = Mathf.Max(1f, (main.startLifetime.constantMin + main.startLifetime.constantMax) * 0.5f);
        ParticleSystem.EmissionModule emission = _particles.emission;
        emission.rateOverTime = count / lifetime;
        emission.enabled = count > 0;
    }
}

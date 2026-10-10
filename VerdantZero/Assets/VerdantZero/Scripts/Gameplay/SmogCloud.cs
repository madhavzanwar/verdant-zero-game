using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Individual smog cloud (GDD 8.1 and 8.4). The Purifier Beam clears it instantly and lowers the global smog.
/// - Big cloud: sits over avenues and factories; clearing it gives −5%. On Hard it wanders slowly around
///   its starting spot.
/// - Micro-cloud (released by chimneys): travels slowly and, if nobody neutralizes it in time,
///   dissolves into the air and raises the smog.
/// Needs a trigger collider so the beam can detect it.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SmogCloud : MonoBehaviour
{
    private static readonly List<SmogCloud> ActiveList = new List<SmogCloud>();

    [Tooltip("How much the global smog drops when it is cleared (0.05 = 5%).")]
    [SerializeField] private float _purifyAmount = 0.05f;

    [Header("Micro-cloud")]
    [SerializeField] private bool _isMicro;
    [Tooltip("Meters per second.")]
    [SerializeField] private float _driftSpeed = 0.8f;
    [Tooltip("Seconds until it dissolves into the air if not neutralized.")]
    [SerializeField] private float _settleTime = 15f;
    [Tooltip("How much the global smog rises when it dissolves without being neutralized.")]
    [SerializeField] private float _settleAmount = 0.02f;
    [Tooltip("Final scale relative to the initial one (it grows while traveling).")]
    [SerializeField] private float _growth = 2f;

    [Header("Big cloud on Hard")]
    [Tooltip("Meters it can drift from its starting spot while wandering.")]
    [SerializeField] private float _wanderRadius = 8f;

    [Header("Aiming")]
    [Tooltip("Halo that lights up when the cloud is in the Beam's sights.")]
    [SerializeField] private GameObject _highlight;

    [Header("Disappearing")]
    [SerializeField] private float _fadeDuration = 0.5f;

    private Collider _collider;
    private ParticleSystem[] _particles;
    private Vector3 _driftDirection;
    private Vector3 _startScale;
    private float _age;
    private bool _gone;
    private bool _targeted;
    private Vector3 _home;
    private float _wanderSpeed;
    private Vector2 _wanderSign;

    /// <summary>Clouds still in the air.</summary>
    public static IReadOnlyList<SmogCloud> Active => ActiveList;

    /// <summary>Raised when the Beam clears a cloud (for sound and effects).</summary>
    public static event System.Action<SmogCloud> Purified;

    public bool IsMicro => _isMicro;

    /// <summary>Approximate radius in meters (to place its marker on top).</summary>
    public float Radius => _collider is SphereCollider sphere
        ? sphere.radius * transform.lossyScale.y
        : _collider.bounds.extents.y;

    /// <summary>True while the Beam is aiming at it.</summary>
    public bool IsTargeted => _targeted;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;
        _particles = GetComponentsInChildren<ParticleSystem>();
        _startScale = transform.localScale;
        if (_highlight != null) _highlight.SetActive(false);

        Vector2 flat = Random.insideUnitCircle.normalized;
        _driftDirection = new Vector3(flat.x, 0.15f, flat.y).normalized;

        // Difficulty from Options: on Hard micro-clouds travel faster and escape sooner
        if (_isMicro)
        {
            GameSettings.DifficultyLevel difficulty = GameSettings.Difficulty;
            _driftSpeed *= GameSettings.MicroCloudSpeedMultiplier(difficulty);
            _settleTime *= GameSettings.MicroCloudSettleMultiplier(difficulty);
        }
        else
        {
            _wanderSpeed = GameSettings.BigCloudWanderSpeed(GameSettings.Difficulty);
            _home = transform.position;
            // Random direction on each axis, so they do not all wander the same way
            _wanderSign = new Vector2(Random.value < 0.5f ? -1f : 1f, Random.value < 0.5f ? -1f : 1f);
        }
    }

    private void OnEnable() => ActiveList.Add(this);

    private void OnDisable() => ActiveList.Remove(this);

    private void Update()
    {
        if (_gone) return;
        if (GameSession.Instance != null && GameSession.Instance.IsOver) return;

        _age += Time.deltaTime;

        if (!_isMicro)
        {
            Wander();
            return;
        }

        transform.position += _driftDirection * (_driftSpeed * Time.deltaTime);
        transform.localScale = _startScale * Mathf.Lerp(1f, _growth, _age / _settleTime);

        if (_age >= _settleTime)
        {
            GameSession.Instance?.AddSmog(_settleAmount);
            Disappear();
        }
    }

    /// <summary>
    /// Big cloud on Hard: follows a smooth curve (Lissajous) around its spot, never drifting more
    /// than <see cref="_wanderRadius"/> on each axis and at the same height. Starts at its spot, with no jumps.
    /// </summary>
    private void Wander()
    {
        if (_wanderSpeed <= 0f || _wanderRadius <= 0f) return;
        float rate = _wanderSpeed / _wanderRadius;
        float x = Mathf.Sin(_age * rate) * _wanderSign.x;
        float z = Mathf.Sin(_age * rate * 0.73f) * _wanderSign.y;
        transform.position = _home + new Vector3(x, 0f, z) * _wanderRadius;
    }

    /// <summary>Turns the "in the sights" halo on or off.</summary>
    public void SetTargeted(bool targeted)
    {
        if (_gone || targeted == _targeted) return;
        _targeted = targeted;
        if (_highlight != null) _highlight.SetActive(targeted);
    }

    /// <summary>Cleared by the Purifier Beam: lowers the smog and the cloud fades out.</summary>
    public void Purify()
    {
        if (_gone) return;
        GameSession.Instance?.ReduceSmog(_purifyAmount);
        Disappear();
        Purified?.Invoke(this);
    }

    private void Disappear()
    {
        _gone = true;
        _targeted = false;
        if (_highlight != null) _highlight.SetActive(false);
        _collider.enabled = false;
        ActiveList.Remove(this);
        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        foreach (ParticleSystem ps in _particles)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // Live particles shrink and fly off: the cloud "comes apart" instead of cutting off abruptly
        Vector3 from = transform.localScale;
        for (float t = 0f; t < _fadeDuration; t += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(from, from * 0.2f, t / _fadeDuration);
            yield return null;
        }

        Destroy(gameObject);
    }
}

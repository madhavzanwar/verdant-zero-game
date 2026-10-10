using System.Collections;
using UnityEngine;

/// <summary>
/// Drone animations (GDD 7.3), done in code on the model (<see cref="QuadVisualTilt"/>) and the camera:
/// - Recoil: while firing the Beam, the drone leans back and vibrates; the camera shakes slightly.
/// - Seed ejection: a backward kick, the green cannon puffs up and the camera jolts.
/// - Sparks: with smog above 80%, the motors throw sparks and the view jerks.
/// - Celebration: on victory, the drone jumps and does a full spin.
/// </summary>
public class DroneAnimations : MonoBehaviour
{
    [Header("Beam recoil")]
    [SerializeField] private float _beamPitch = 5f;
    [SerializeField] private float _beamPushBack = 0.08f;
    [SerializeField] private float _beamJitter = 1.2f;
    [SerializeField] private float _beamCameraShake = 0.25f;
    [Tooltip("Camera jolt when a cloud is cleared.")]
    [SerializeField] private float _cloudCameraKick = 1.2f;

    [Header("Seed ejection")]
    [SerializeField] private float _seedPitch = 10f;
    [SerializeField] private float _seedPushBack = 0.15f;
    [SerializeField] private float _seedCannonPunch = 1.5f;
    [SerializeField] private float _seedCameraKick = 2f;

    [Header("Sparks (high smog)")]
    [Range(0f, 1f)]
    [SerializeField] private float _sparkThreshold = 0.8f;
    [SerializeField] private Material _sparkMaterial;
    [Tooltip("Seconds between spark bursts (min and max).")]
    [SerializeField] private Vector2 _sparkInterval = new Vector2(0.25f, 0.9f);
    [SerializeField] private float _sparkCameraShake = 0.8f;

    [Header("Celebration")]
    [SerializeField] private float _celebrateHop = 0.8f;
    [SerializeField] private float _celebrateDuration = 1.3f;

    private QuadVisualTilt _tilt;
    private PurifierBeam _beam;
    private SeedLauncher _seeds;
    private GameSession _session;
    private FollowCamera _camera;
    private Transform _seedCannon;
    private Vector3 _seedCannonScale = Vector3.one;
    private Transform[] _motors;
    private ParticleSystem _sparks;

    // Seed kick spring (0-1) and continuous Beam recoil (0-1)
    private float _kick;
    private float _kickVelocity;
    private float _beamAmount;
    private float _nextSpark;
    private bool _celebrating;
    private Quaternion _celebrateRotation = Quaternion.identity;
    private Vector3 _celebrateOffset;

    private void Awake()
    {
        _tilt = GetComponent<QuadVisualTilt>();
        _beam = GetComponent<PurifierBeam>();
        _seeds = GetComponent<SeedLauncher>();

        var motors = new System.Collections.Generic.List<Transform>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Cannon_Seed") _seedCannon = t;
            if (t.name.StartsWith("Motor_")) motors.Add(t);
        }
        _motors = motors.ToArray();
        if (_seedCannon != null) _seedCannonScale = _seedCannon.localScale;
        if (_sparkMaterial != null) _sparks = CreateSparks();
    }

    private void OnEnable()
    {
        _session = FindAnyObjectByType<GameSession>();
        if (Camera.main != null) _camera = Camera.main.GetComponent<FollowCamera>();
        if (_seeds != null) _seeds.Launched += OnSeedLaunched;
        if (_session != null) _session.Ended += OnSessionEnded;
        SmogCloud.Purified += OnCloudPurified;
    }

    private void OnDisable()
    {
        if (_seeds != null) _seeds.Launched -= OnSeedLaunched;
        if (_session != null) _session.Ended -= OnSessionEnded;
        SmogCloud.Purified -= OnCloudPurified;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Beam: eases in and out, with vibration while firing
        bool firing = _beam != null && _beam.IsFiring;
        _beamAmount = Mathf.MoveTowards(_beamAmount, firing ? 1f : 0f, dt * 10f);
        if (firing && _camera != null) _camera.Shake(_beamCameraShake);

        // Damped spring that brings the seed kick back to 0
        _kickVelocity += (-180f * _kick - 18f * _kickVelocity) * dt;
        _kick += _kickVelocity * dt;

        UpdateSparks();
        ApplyToModel();
    }

    private void ApplyToModel()
    {
        if (_tilt == null) return;

        float jitter = _beamAmount * _beamJitter;
        float pitch = -_beamAmount * _beamPitch - _kick * _seedPitch + Random.Range(-jitter, jitter);
        float roll = Random.Range(-jitter, jitter);
        float back = _beamAmount * _beamPushBack + _kick * _seedPushBack;

        _tilt.ExtraRotation = _celebrateRotation * Quaternion.Euler(pitch, 0f, roll);
        _tilt.ExtraOffset = _celebrateOffset + new Vector3(0f, 0f, -back);

        if (_seedCannon != null)
        {
            float punch = 1f + Mathf.Max(0f, _kick) * (_seedCannonPunch - 1f);
            _seedCannon.localScale = _seedCannonScale * punch;
        }
    }

    private void UpdateSparks()
    {
        bool high = _session != null && !_session.IsOver && _session.Smog >= _sparkThreshold;
        if (!high || Time.time < _nextSpark) return;

        _nextSpark = Time.time + Random.Range(_sparkInterval.x, _sparkInterval.y);
        if (_camera != null) _camera.Kick(_sparkCameraShake);
        if (_sparks == null || _motors.Length == 0) return;

        Transform motor = _motors[Random.Range(0, _motors.Length)];
        _sparks.transform.position = motor.position;
        _sparks.Emit(Random.Range(14, 24));
    }

    private void OnSeedLaunched()
    {
        _kickVelocity += 20f;
        if (_camera != null) _camera.Kick(_seedCameraKick);
    }

    private void OnCloudPurified(SmogCloud cloud)
    {
        if (_camera != null) _camera.Kick(_cloudCameraKick);
    }

    private void OnSessionEnded(GameSession.Result result)
    {
        if (result == GameSession.Result.Victory && !_celebrating) StartCoroutine(Celebrate());
    }

    // Jump with a full spin in place
    private IEnumerator Celebrate()
    {
        _celebrating = true;
        for (float t = 0f; t < _celebrateDuration; t += Time.deltaTime)
        {
            float k = t / _celebrateDuration;
            float eased = k * k * (3f - 2f * k);
            _celebrateRotation = Quaternion.Euler(0f, eased * 360f, 0f);
            _celebrateOffset = new Vector3(0f, Mathf.Sin(k * Mathf.PI) * _celebrateHop, 0f);
            yield return null;
        }
        _celebrateRotation = Quaternion.identity;
        _celebrateOffset = Vector3.zero;
        _celebrating = false;
    }

    // Orange sparks that jump and fall; emitted by hand in bursts from a random motor
    private ParticleSystem CreateSparks()
    {
        var go = new GameObject("Sparks");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.35f), new Color(1f, 0.45f, 0.1f));
        main.gravityModifier = 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 64;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = _sparkMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.04f;
        renderer.lengthScale = 2f;
        return ps;
    }
}

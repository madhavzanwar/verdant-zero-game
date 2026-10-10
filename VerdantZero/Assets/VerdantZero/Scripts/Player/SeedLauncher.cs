using System;
using UnityEngine;

/// <summary>
/// Instant Seed Launcher (GDD 6.1): pressing Seed fires a capsule from the green cannon.
/// It finds the nearest free planting spot in front of the drone (<see cref="CurrentTarget"/>) and launches the
/// seed in an arc toward it; if there is none, the seed falls ahead and marks the miss.
/// It cannot be used at the same time as the Beam (GDD 7.5): while the Beam fires it does not launch, and right
/// after launching the Beam is briefly blocked (<see cref="IsBusy"/>).
/// </summary>
[RequireComponent(typeof(QuadInput))]
public class SeedLauncher : MonoBehaviour
{
    [Tooltip("Tip of the green cannon. If left empty, a child named Cannon_Seed is searched for.")]
    [SerializeField] private Transform _muzzle;
    [SerializeField] private SeedProjectile _seedPrefab;
    [Tooltip("Maximum distance to a planting spot.")]
    [SerializeField] private float _range = 35f;
    [Tooltip("Degrees to each side of the drone's front within which a planting spot is searched for.")]
    [SerializeField] private float _aimAngle = 35f;
    [SerializeField] private float _cooldown = 0.6f;
    [Tooltip("Time during which the Beam stays blocked after launching (they are not used together).")]
    [SerializeField] private float _busyTime = 0.35f;
    [SerializeField] private float _flightTime = 0.9f;
    [Tooltip("How far a seed launched without a planting spot falls.")]
    [SerializeField] private float _missDistance = 14f;

    private QuadInput _input;
    private QuadcopterCrash _crash;
    private PurifierBeam _beam;
    private bool _wasPressed;
    private float _nextLaunchTime;
    private float _busyUntil;

    /// <summary>Planting spot the seed would go to if launched now.</summary>
    public PlantingSpot CurrentTarget { get; private set; }

    /// <summary>Raised when a seed is fired (for sound and effects).</summary>
    public event Action Launched;

    /// <summary>True right after launching: the Beam cannot fire.</summary>
    public bool IsBusy => Time.time < _busyUntil;

    private void Awake()
    {
        _input = GetComponent<QuadInput>();
        _crash = GetComponent<QuadcopterCrash>();
        _beam = GetComponent<PurifierBeam>();
        if (_muzzle == null)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Cannon_Seed") _muzzle = t;
            }
        }
    }

    private void OnDisable() => SetTarget(null);

    private void Update()
    {
        bool crashed = _crash != null && _crash.HasCrashed;
        bool over = GameSession.Instance != null && GameSession.Instance.IsOver;
        bool active = !crashed && !over && Time.timeScale > 0f;

        SetTarget(active ? FindTarget() : null);

        // Launched on press (not on hold)
        bool pressed = _input.Seed;
        bool justPressed = pressed && !_wasPressed;
        _wasPressed = pressed;

        bool beamFiring = _beam != null && _beam.IsFiring;
        if (!active || !justPressed || beamFiring || Time.time < _nextLaunchTime) return;
        Launch();
    }

    private void Launch()
    {
        _nextLaunchTime = Time.time + _cooldown;
        _busyUntil = Time.time + _busyTime;
        if (_seedPrefab == null) return;

        Vector3 start = _muzzle != null ? _muzzle.position : transform.position;
        PlantingSpot target = CurrentTarget;
        Vector3 end = target != null ? target.AimPoint : MissPoint(start);

        SeedProjectile seed = Instantiate(_seedPrefab, start, Quaternion.identity);
        seed.Launch(start, end, _flightTime, target);
        SetTarget(null);
        Launched?.Invoke();
    }

    private PlantingSpot FindTarget()
    {
        Vector3 origin = transform.position;
        Vector3 forward = Flat(transform.forward);
        PlantingSpot best = null;
        float bestScore = float.MaxValue;

        foreach (PlantingSpot spot in PlantingSpot.Available)
        {
            Vector3 toSpot = spot.AimPoint - origin;
            float distance = toSpot.magnitude;
            if (distance > _range) continue;

            float angle = Vector3.Angle(forward, Flat(toSpot));
            // Very close (right below the drone) counts even if the horizontal angle is large
            if (angle > _aimAngle && Flat(toSpot).magnitude > 4f) continue;

            // Prefer what is ahead and close
            float score = distance * (1f + angle / _aimAngle);
            if (score < bestScore)
            {
                bestScore = score;
                best = spot;
            }
        }
        return best;
    }

    private Vector3 MissPoint(Vector3 start)
    {
        Vector3 forward = Flat(transform.forward);
        Vector3 probe = start + forward * _missDistance;
        if (Physics.Raycast(probe + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }
        return new Vector3(probe.x, 0f, probe.z);
    }

    private void SetTarget(PlantingSpot target)
    {
        if (target == CurrentTarget) return;
        if (CurrentTarget != null) CurrentTarget.SetTargeted(false);
        CurrentTarget = target;
        if (target != null) target.SetTargeted(true);
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }
}

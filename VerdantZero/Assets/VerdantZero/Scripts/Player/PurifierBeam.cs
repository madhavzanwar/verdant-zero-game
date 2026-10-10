using UnityEngine;

/// <summary>
/// Purifier Beam (GDD 6.1): while held down, fires a ray from the blue cannon.
/// The first smog cloud it touches is cleared instantly; a building blocks the ray.
/// Aiming:
/// - The HUD crosshair marks where the beam is going: in first person, the center of the camera.
/// - Auto-aim: if no cloud is right in the sights, the beam locks onto the visible cloud
///   closest to the crosshair within a cone (20° to the sides, 40° up/down), even if it is higher or lower.
/// Even when not firing, it marks the locked cloud (<see cref="CurrentTarget"/>) so the player knows
/// that firing now will hit.
/// </summary>
[RequireComponent(typeof(QuadInput))]
public class PurifierBeam : MonoBehaviour
{
    [Tooltip("Tip of the blue cannon. If left empty, a child named Cannon_Beam is searched for.")]
    [SerializeField] private Transform _muzzle;
    [SerializeField] private LineRenderer _line;
    [SerializeField] private float _range = 35f;
    [Tooltip("Radius of the sweep along the crosshair: aiming tolerance.")]
    [SerializeField] private float _aimRadius = 1.5f;
    [Tooltip("Auto-aim: degrees left/right of the crosshair within which a cloud is locked.")]
    [SerializeField] private float _assistAngle = 20f;
    [Tooltip("Auto-aim: degrees up/down. Wider than horizontal because height is the hard part to match.")]
    [SerializeField] private float _assistVerticalAngle = 40f;
    [Tooltip("In third person: degrees downward relative to the drone's front.")]
    [SerializeField] private float _downAngle = 4f;
    [Tooltip("In first person the ray is drawn from here (m ahead of the cannon) so it does not block the view.")]
    [SerializeField] private float _firstPersonLineStart = 2.5f;

    private readonly RaycastHit[] _hits = new RaycastHit[16];
    private QuadInput _input;
    private QuadcopterCrash _crash;
    private SeedLauncher _seeds;
    private FollowCamera _camera;

    /// <summary>True while the ray is active (for sound and effects).</summary>
    public bool IsFiring { get; private set; }

    /// <summary>Cloud the Beam would hit if fired now (null if none).</summary>
    public SmogCloud CurrentTarget { get; private set; }

    /// <summary>Origin and direction of the crosshair (to draw it on the HUD).</summary>
    public Vector3 AimOrigin { get; private set; }
    public Vector3 AimDirection { get; private set; } = Vector3.forward;
    public float Range => _range;

    private void Awake()
    {
        _input = GetComponent<QuadInput>();
        _crash = GetComponent<QuadcopterCrash>();
        _seeds = GetComponent<SeedLauncher>();
        if (Camera.main != null) _camera = Camera.main.GetComponent<FollowCamera>();
        if (_muzzle == null) _muzzle = FindChild(transform, "Cannon_Beam");
        if (_line != null) _line.enabled = false;

        // Difficulty from Options: on Hard the auto-aim is narrower
        float assist = GameSettings.BeamAssistMultiplier(GameSettings.Difficulty);
        _assistAngle *= assist;
        _assistVerticalAngle *= assist;
    }

    private void Update()
    {
        bool crashed = _crash != null && _crash.HasCrashed;
        bool over = GameSession.Instance != null && GameSession.Instance.IsOver;
        bool active = !crashed && !over && Time.timeScale > 0f;
        // Not used at the same time as the seed (GDD 7.5)
        bool seedBusy = _seeds != null && _seeds.IsBusy;
        IsFiring = _input.Beam && active && !seedBusy;
        if (_line != null) _line.enabled = IsFiring;

        Vector3 muzzle = _muzzle != null ? _muzzle.position : transform.position;
        bool firstPerson = _camera != null && _camera.IsFirstPerson;

        // The crosshair: in first person, the center of the camera; otherwise the drone's front (without its tilt)
        if (firstPerson)
        {
            AimOrigin = _camera.transform.position;
            AimDirection = _camera.transform.forward;
        }
        else
        {
            Vector3 forward = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Vector3.forward;
            AimOrigin = muzzle;
            AimDirection = Quaternion.AngleAxis(_downAngle, Vector3.Cross(Vector3.up, forward)) * forward;
        }

        float end = Cast(AimOrigin, AimDirection, out SmogCloud target);
        if (target == null) target = FindAssistTarget(AimOrigin, AimDirection);
        SetTarget(active ? target : null);
        if (!IsFiring) return;

        // The ray goes to the locked cloud; if there is none, it follows the crosshair until it hits something
        Vector3 beamEnd = target != null ? target.transform.position : AimOrigin + AimDirection * end;
        if (target != null)
        {
            SetTarget(null);
            target.Purify();
        }

        if (_line != null)
        {
            Vector3 toEnd = beamEnd - muzzle;
            // In first person the camera is next to the cannon: start the ray a bit further ahead
            float lineStart = firstPerson ? Mathf.Min(_firstPersonLineStart, toEnd.magnitude * 0.5f) : 0f;
            _line.SetPosition(0, muzzle + toEnd.normalized * lineStart);
            _line.SetPosition(1, beamEnd);
        }
    }

    /// <summary>Visible cloud closest to the crosshair within the lock cone (null if none).</summary>
    private SmogCloud FindAssistTarget(Vector3 origin, Vector3 direction)
    {
        SmogCloud best = null;
        float bestScore = float.MaxValue;
        Vector3 flatAim = Flat(direction);
        float aimPitch = Pitch(direction);
        foreach (SmogCloud cloud in SmogCloud.Active)
        {
            Vector3 toCloud = cloud.transform.position - origin;
            float distance = toCloud.magnitude;
            if (distance > _range + cloud.Radius) continue;

            // Cone split into horizontal and vertical
            float yaw = Vector3.Angle(flatAim, Flat(toCloud));
            float pitch = Mathf.Abs(Pitch(toCloud) - aimPitch);
            if (yaw > _assistAngle || pitch > _assistVerticalAngle) continue;
            float score = yaw / _assistAngle + pitch / _assistVerticalAngle;
            if (score >= bestScore) continue;

            // No buildings in between
            float clear = Mathf.Max(0f, distance - cloud.Radius);
            if (clear > 0.1f && Physics.Raycast(origin, toCloud / distance, out RaycastHit hit, clear,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            bestScore = score;
            best = cloud;
        }
        return best;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }

    // Degrees above (+) or below (−) the horizon
    private static float Pitch(Vector3 v) => Mathf.Atan2(v.y, new Vector2(v.x, v.z).magnitude) * Mathf.Rad2Deg;

    private void SetTarget(SmogCloud target)
    {
        if (target == CurrentTarget) return;
        if (CurrentTarget != null) CurrentTarget.SetTargeted(false);
        CurrentTarget = target;
        if (target != null) target.SetTargeted(true);
    }

    private void OnDisable() => SetTarget(null);

    /// <summary>Finds the first thing the ray touches: the cloud in the sights (or null) and the ray length.</summary>
    private float Cast(Vector3 origin, Vector3 direction, out SmogCloud target)
    {
        int count = Physics.SphereCastNonAlloc(origin, _aimRadius, direction, _hits, _range,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

        SmogCloud closestCloud = null;
        float closest = _range;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _hits[i];
            if (hit.collider.transform.IsChildOf(transform)) continue;

            var cloud = hit.collider.GetComponentInParent<SmogCloud>();
            if (cloud != null)
            {
                if (hit.distance < closest)
                {
                    closest = hit.distance;
                    closestCloud = cloud;
                }
            }
            else if (!hit.collider.isTrigger && hit.distance < closest)
            {
                // Wall or other solid: the ray is cut there
                closest = hit.distance;
                closestCloud = null;
            }
        }

        target = closestCloud;
        // Sweeps that start inside a collider give distance 0: show at least a short segment
        return Mathf.Max(closest, 1f);
    }

    private static Transform FindChild(Transform root, string childName)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName) return t;
        }
        return null;
    }
}

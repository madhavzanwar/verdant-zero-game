using UnityEngine;

/// <summary>
/// Arcade movement controller for the quadcopter.
/// - Horizontal speed: velocity PID -> acceleration.
/// - Altitude: height PID with a setpoint that rises/falls with input.
/// - Heading: with the camera following the drone (first or third person), the lateral stick turns the drone and
///   the vertical one moves it forward/back along its heading; the camera stays behind and looks where it goes.
/// - Without that camera (or while falling), the drone turns on its own toward where it moves.
/// Uses ForceMode.Acceleration, so the Rigidbody's mass does not affect the feel.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(QuadInput))]
public class QuadcopterController : MonoBehaviour
{
    [Header("Horizontal movement")]
    [SerializeField] private float _maxSpeed = 12f;
    [SerializeField] private float _maxAcceleration = 25f;
    [Tooltip("How fast the target velocity changes (m/s²). Smooths the touch stick.")]
    [SerializeField] private float _targetVelocityRate = 40f;
    [SerializeField] private PIDController _velocityPid = new PIDController(4f, 0f, 0.2f, 5f);

    [Header("Altitude")]
    [SerializeField] private float _climbSpeed = 5f;
    [SerializeField] private float _minAltitude = 0.5f;
    [SerializeField] private float _maxAltitude = 60f;
    [SerializeField] private float _maxVerticalAcceleration = 20f;
    [SerializeField] private PIDController _altitudePid = new PIDController(6f, 0.5f, 4f, 5f);
    [Tooltip("Maximum the target altitude can sit below the drone (m). Keeps the target from sinking while resting on a rooftop, which delayed takeoff.")]
    [SerializeField] private float _maxAltitudeBelow = 1f;

    [Header("Automatic heading")]
    [Tooltip("Degrees per second at which the drone turns toward the direction of movement.")]
    [SerializeField] private float _maxYawRate = 240f;
    [Tooltip("Below this speed (m/s) the drone keeps its heading.")]
    [SerializeField] private float _minTurnSpeed = 0.8f;
    [Tooltip("Degrees per second of turning with the lateral stick (with the camera following the drone).")]
    [SerializeField] private float _firstPersonTurnRate = 110f;

    [Header("Movement reference")]
    [Tooltip("The stick is relative to its orientation. If left empty, the main camera is used.")]
    [SerializeField] private Transform _moveReference;

    private Rigidbody _rb;
    private QuadInput _input;

    private Vector3 _targetVelocity;
    private float _targetAltitude;
    private float _heading;
    private FollowCamera _followCamera;
    private bool _referenceResolved;

    private PIDController _velocityPidX;
    private PIDController _velocityPidZ;

    /// <summary>Current horizontal velocity (world).</summary>
    public Vector3 HorizontalVelocity => new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);

    public float MaxSpeed => _maxSpeed;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _input = GetComponent<QuadInput>();

        // Gravity is disabled and rotation frozen in the prefab: control is by velocity.
        _rb.useGravity = false;

        // One PID per horizontal axis with the same gains (each keeps its own state).
        _velocityPidX = new PIDController(_velocityPid.Kp, _velocityPid.Ki, _velocityPid.Kd, _velocityPid.IntegralLimit);
        _velocityPidZ = new PIDController(_velocityPid.Kp, _velocityPid.Ki, _velocityPid.Kd, _velocityPid.IntegralLimit);
    }

    private void OnEnable()
    {
        _targetAltitude = _rb.position.y;
        _heading = _rb.rotation.eulerAngles.y;
        _targetVelocity = Vector3.zero;
        _velocityPidX.Reset();
        _velocityPidZ.Reset();
        _altitudePid.Reset();
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        UpdateYaw(dt);
        UpdateHorizontal(dt);
        UpdateAltitude(dt);
    }

    private bool SteersWithHeading => _followCamera != null && _followCamera.SteersWithHeading;

    private void UpdateYaw(float dt)
    {
        // With the camera following the drone, the lateral stick turns the drone (and the view with it)
        if (SteersWithHeading)
        {
            _heading += _input.Move.x * _firstPersonTurnRate * dt;
        }
        // No camera following it: face where it wants to go, so the Beam fires in the direction of movement
        else if (_targetVelocity.magnitude > _minTurnSpeed)
        {
            float targetHeading = Mathf.Atan2(_targetVelocity.x, _targetVelocity.z) * Mathf.Rad2Deg;
            _heading = Mathf.MoveTowardsAngle(_heading, targetHeading, _maxYawRate * dt);
        }
        _rb.MoveRotation(Quaternion.Euler(0f, _heading, 0f));
    }

    private void UpdateHorizontal(float dt)
    {
        if (_moveReference == null && Camera.main != null) _moveReference = Camera.main.transform;
        if (_moveReference != null && !_referenceResolved)
        {
            _followCamera = _moveReference.GetComponent<FollowCamera>();
            _referenceResolved = true;
        }

        // Orientation reference projected onto the horizontal plane. With the chase camera its heading is used,
        // not where it is looking: when moving sideways the camera turns to follow the drone, and if the stick
        // depended on that the drone would end up spinning in circles.
        float referenceYaw = _followCamera != null ? _followCamera.Heading
            : _moveReference != null ? _moveReference.eulerAngles.y : _heading;
        Quaternion yawRotation = Quaternion.Euler(0f, referenceYaw, 0f);

        Vector2 stick = _input.Move;
        // The lateral stick turns instead of strafing: forward/back along the heading
        Vector3 desired = SteersWithHeading
            ? Quaternion.Euler(0f, _heading, 0f) * new Vector3(0f, 0f, stick.y) * _maxSpeed
            : yawRotation * new Vector3(stick.x, 0f, stick.y) * _maxSpeed;

        // The target velocity approaches the desired one with a rate limit (smooths touch input).
        _targetVelocity = Vector3.MoveTowards(_targetVelocity, desired, _targetVelocityRate * dt);

        Vector3 velocity = HorizontalVelocity;
        Vector3 acceleration = new Vector3(
            _velocityPidX.Update(_targetVelocity.x - velocity.x, dt),
            0f,
            _velocityPidZ.Update(_targetVelocity.z - velocity.z, dt));

        acceleration = Vector3.ClampMagnitude(acceleration, _maxAcceleration);
        _rb.AddForce(acceleration, ForceMode.Acceleration);
    }

    private void UpdateAltitude(float dt)
    {
        _targetAltitude = Mathf.Clamp(
            _targetAltitude + _input.Climb * _climbSpeed * dt,
            _minAltitude,
            _maxAltitude);
        _targetAltitude = Mathf.Max(_targetAltitude, _rb.position.y - _maxAltitudeBelow);

        float error = _targetAltitude - _rb.position.y;
        float acceleration = Mathf.Clamp(_altitudePid.Update(error, dt), -_maxVerticalAcceleration, _maxVerticalAcceleration);
        _rb.AddForce(Vector3.up * acceleration, ForceMode.Acceleration);
    }
}

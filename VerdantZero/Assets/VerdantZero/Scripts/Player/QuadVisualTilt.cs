using UnityEngine;

/// <summary>
/// Tilts the visual model according to local velocity (the root Rigidbody has frozen rotation)
/// and spins the propellers (children whose name contains prop/rotor/blade/helice) around the drone's vertical axis,
/// faster when accelerating or climbing and with diagonals spinning the same way, like a real quadcopter.
/// Other scripts (e.g. <see cref="DroneAnimations"/>) can add an extra rotation and offset to the model.
/// </summary>
public class QuadVisualTilt : MonoBehaviour
{
    [SerializeField] private QuadcopterController _controller;
    [Tooltip("Child holding the drone model. If left empty, this same transform is used.")]
    [SerializeField] private Transform _model;
    [SerializeField] private float _maxTiltAngle = 25f;
    [SerializeField] private float _tiltSmoothing = 6f;

    [Header("Propellers")]
    [SerializeField] private Transform[] _propellers;
    [Tooltip("Degrees per second with the drone hovering in the air.")]
    [SerializeField] private float _propellerSpeed = 2700f;
    [Tooltip("Degrees per second at full speed or while climbing.")]
    [SerializeField] private float _propellerMaxSpeed = 3600f;
    [SerializeField] private float _propellerSmoothing = 4f;
    [Tooltip("Maximum blade rotation per frame. Above about 45° the eye sees the blades still or spinning backwards " +
             "(stroboscopic effect); the rest of the speed is conveyed by the spin disc.")]
    [SerializeField] private float _maxStepPerFrame = 40f;
    [Tooltip("Semi-transparent material for the disc seen with the propellers at full speed (optional).")]
    [SerializeField] private Material _blurDiscMaterial;
    [Tooltip("Disc opacity when hovering and at full speed.")]
    [SerializeField] private Vector2 _blurDiscAlpha = new Vector2(0.25f, 0.4f);

    private Quaternion _baseRotation;
    private Vector3 _basePosition;
    private Quaternion _tilt;
    private float _spinRate;
    private Rigidbody _body;
    private float[] _spinSigns;
    private Renderer[] _blurDiscs;
    private MaterialPropertyBlock _discBlock;

    /// <summary>Rotation added to the tilt (recoil, celebration...). Applied in model space.</summary>
    public Quaternion ExtraRotation { get; set; } = Quaternion.identity;

    /// <summary>Local offset added to the model's position.</summary>
    public Vector3 ExtraOffset { get; set; }

    private void Awake()
    {
        if (_controller == null)
        {
            _controller = GetComponentInParent<QuadcopterController>();
        }

        if (_model == null)
        {
            _model = transform;
        }

        _baseRotation = _model.localRotation;
        _basePosition = _model.localPosition;
        _tilt = _baseRotation;

        if (_propellers == null || _propellers.Length == 0)
        {
            _propellers = FindPropellers(_model);
        }

        _body = _controller != null ? _controller.GetComponent<Rigidbody>() : null;
        // As on a real quadcopter, diagonal propellers spin the same way
        _spinSigns = new float[_propellers.Length];
        for (int i = 0; i < _propellers.Length; i++)
        {
            Vector3 p = _model.InverseTransformPoint(_propellers[i].position);
            _spinSigns[i] = p.x * p.z >= 0f ? 1f : -1f;
        }

        if (_blurDiscMaterial != null) CreateBlurDiscs();
    }

    private void Update()
    {
        if (_controller == null)
        {
            return;
        }

        Vector3 localVelocity = _controller.transform.InverseTransformDirection(_controller.HorizontalVelocity);
        float speedFactor = _controller.MaxSpeed > 0f ? 1f / _controller.MaxSpeed : 0f;

        // Forward -> pitch down (+X); right -> roll right (-Z).
        float pitch = Mathf.Clamp(localVelocity.z * speedFactor, -1f, 1f) * _maxTiltAngle;
        float roll = -Mathf.Clamp(localVelocity.x * speedFactor, -1f, 1f) * _maxTiltAngle;

        Quaternion target = _baseRotation * Quaternion.Euler(pitch, 0f, roll);
        _tilt = Quaternion.Slerp(_tilt, target, 1f - Mathf.Exp(-_tiltSmoothing * Time.deltaTime));
        _model.localRotation = _tilt * ExtraRotation;
        _model.localPosition = _basePosition + ExtraOffset;

        // More RPM when accelerating or climbing, like a real drone
        float effort = Mathf.Clamp01(Mathf.Max(Mathf.Abs(localVelocity.z), Mathf.Abs(localVelocity.x)) * speedFactor
                                     + (_body != null ? Mathf.Max(0f, _body.linearVelocity.y) * 0.15f : 0f));
        float targetRate = Mathf.Lerp(_propellerSpeed, _propellerMaxSpeed, effort);
        // Unscaled time: the motors do not stop hovering even if the game is paused or in the tutorial
        float dt = Time.unscaledDeltaTime;
        _spinRate = Mathf.Lerp(_spinRate <= 0f ? targetRate : _spinRate, targetRate, 1f - Mathf.Exp(-_propellerSmoothing * dt));
        UpdateBlurDiscs(Mathf.InverseLerp(_propellerSpeed, _propellerMaxSpeed, _spinRate));

        // The axis is the drone's "up": the local axis of the propellers imported from Blender points sideways,
        // and spinning around it flipped them vertically. The step per frame is capped so they do not look still.
        float spin = Mathf.Min(_spinRate * dt, _maxStepPerFrame);
        Vector3 axis = _model.up;
        for (int i = 0; i < _propellers.Length; i++)
        {
            _propellers[i].Rotate(axis, spin * _spinSigns[i], Space.World);
        }
    }

    // A thin semi-transparent disc under each propeller: that is how a real propeller looks at high RPM.
    // It hangs from the model (not the propeller) so it does not spin with it or inherit its Blender scale.
    private void CreateBlurDiscs()
    {
        _discBlock = new MaterialPropertyBlock();
        _blurDiscs = new Renderer[_propellers.Length];
        for (int i = 0; i < _propellers.Length; i++)
        {
            Renderer prop = _propellers[i].GetComponent<Renderer>();
            float diameter = prop != null ? Mathf.Max(prop.bounds.size.x, prop.bounds.size.z) : 1f;

            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = _propellers[i].name + "_Blur";
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(_model, false);
            disc.transform.position = _propellers[i].position;
            disc.transform.localRotation = Quaternion.identity;
            disc.transform.localScale = new Vector3(diameter, 0.004f, diameter);

            Renderer r = disc.GetComponent<Renderer>();
            r.sharedMaterial = _blurDiscMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            _blurDiscs[i] = r;
        }
    }

    private void UpdateBlurDiscs(float speed01)
    {
        if (_blurDiscs == null) return;
        Color color = _blurDiscMaterial.color;
        color.a = Mathf.Lerp(_blurDiscAlpha.x, _blurDiscAlpha.y, speed01);
        foreach (Renderer disc in _blurDiscs)
        {
            disc.GetPropertyBlock(_discBlock);
            _discBlock.SetColor("_BaseColor", color);
            disc.SetPropertyBlock(_discBlock);
        }
    }

    private static Transform[] FindPropellers(Transform root)
    {
        var found = new System.Collections.Generic.List<Transform>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>())
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("prop") || n.Contains("rotor") || n.Contains("blade") || n.Contains("helice"))
            {
                found.Add(t);
            }
        }

        return found.ToArray();
    }
}

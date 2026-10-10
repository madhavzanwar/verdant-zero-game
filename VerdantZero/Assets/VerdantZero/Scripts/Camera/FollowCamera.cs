using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Drone camera, lives on the Main Camera next to the CinemachineBrain. It does not move the camera: it picks
/// which virtual camera is in charge by raising its priority, and translates game effects to Cinemachine.
/// - Close third person (default, CM_TerceraPersona): behind and above the drone, looking over
///   and ahead of it, so the drone is seen low in the center and the crosshair stays near the center.
/// - First person (CM_PrimeraPersona): fixed at FPV_Mount, in the center of the drone slightly behind
///   the front propellers. The body is hidden; the front propellers and motors stay in view.
///   Chosen in game with the HUD camera button (<see cref="ToggleView"/>).
/// - Crash (CM_Caida): when crashing, the camera stays where it was and watches the drone fall.
/// In both flight modes the lateral stick turns the drone (<see cref="SteersWithHeading"/>): if the stick
/// strafed relative to a camera that follows the drone, the drone would end up spinning in circles.
/// </summary>
public class FollowCamera : MonoBehaviour
{
    public enum ViewMode
    {
        FirstPerson,
        ThirdPerson,
    }

    [SerializeField] private Transform _target;
    [SerializeField] private ViewMode _mode = ViewMode.ThirdPerson;

    [Header("Virtual cameras")]
    [SerializeField] private CinemachineCamera _thirdPersonCamera;
    [SerializeField] private CinemachineCamera _firstPersonCamera;
    [SerializeField] private CinemachineCamera _crashCamera;
    [Tooltip("Priority the chosen camera (first person or crash) takes to override the third-person one.")]
    [SerializeField] private int _activePriority = 30;

    [Header("First person")]
    [Tooltip("Camera mount point, child of the drone. Only follows the heading: the drone does not tilt at the root.")]
    [SerializeField] private Transform _fpvMount;
    [Tooltip("Mount position relative to the drone (x lateral, y height, z forward).")]
    [SerializeField] private Vector3 _firstPersonOffset = new Vector3(0f, 0.3f, -0.8f);
    [Tooltip("Degrees downward: lets you see the ground and what is ahead.")]
    [SerializeField] private float _firstPersonPitch = 8f;
    [Tooltip("Model hidden in first person (the mesh renderers under it). If left empty, the Quadcopter_Drone child is used.")]
    [SerializeField] private Transform _hiddenModel;
    [Tooltip("Keep the propellers and motors in front of the camera visible.")]
    [SerializeField] private bool _showFrontPropellers = true;
    [Tooltip("When switching to first person, the body is hidden once the camera is this far from the mount (meters), before entering the model.")]
    [SerializeField] private float _hideModelDistance = 1.6f;

    [Header("Effects")]
    [Tooltip("Impulse source on the drone (camera kicks and crash).")]
    [SerializeField] private CinemachineImpulseSource _impulse;
    [Tooltip("Meters of kick per degree of Kick (the impulse moves the camera, it does not rotate it).")]
    [SerializeField] private float _kickPerDegree = 0.12f;
    [SerializeField] private float _crashImpulse = 0.8f;
    [Tooltip("How fast the shake dies out if Shake stops being called (degrees per second).")]
    [SerializeField] private float _shakeDecay = 20f;

    private CinemachineBasicMultiChannelPerlin[] _noises;
    private MeshRenderer[] _modelRenderers;
    private bool _modelHidden;
    private bool _crashed;
    private float _shake;

    /// <summary>Upward camera kick (degrees) that returns on its own, e.g. when launching a seed.</summary>
    public void Kick(float degrees)
    {
        if (_impulse != null && degrees > 0f) _impulse.GenerateImpulseWithVelocity(Vector3.up * degrees * _kickPerDegree);
    }

    /// <summary>Continuous shake (degrees) while called every frame; it dies out on its own once calls stop.</summary>
    public void Shake(float degrees) => _shake = Mathf.Max(_shake, degrees);

    /// <summary>Actual camera heading (degrees), as given by the CinemachineBrain.</summary>
    public float Heading => transform.eulerAngles.y;

    /// <summary>If false, the camera stops following the drone (e.g. while it falls spinning):
    /// it switches to the crash camera, which stays still and watches it.</summary>
    public bool FollowYaw { get; set; } = true;

    /// <summary>True while the view is first person (the camera has reached the drone, not at the start of the blend).</summary>
    public bool IsFirstPerson { get; private set; }

    /// <summary>Chosen view (first person is not used while the drone falls).</summary>
    public ViewMode Mode => _mode;

    /// <summary>Switches view in game, with the brain's blend. Does nothing while the drone falls.</summary>
    public void SetMode(ViewMode mode)
    {
        if (!FollowYaw) return;
        _mode = mode;
        UpdatePriorities();
    }

    /// <summary>Switches from third to first person or back.</summary>
    public void ToggleView() => SetMode(_mode == ViewMode.FirstPerson ? ViewMode.ThirdPerson : ViewMode.FirstPerson);

    private bool WantsFirstPerson => _mode == ViewMode.FirstPerson && FollowYaw;

    /// <summary>True while the camera follows the drone's heading (both modes, except when falling):
    /// the lateral stick turns the drone and the vertical one moves it forward and back.</summary>
    public bool SteersWithHeading => FollowYaw;

    private void Start()
    {
        if (_target == null)
        {
            Debug.LogWarning("FollowCamera: no hay objetivo asignado.", this);
            enabled = false;
            return;
        }

        if (_fpvMount != null) _fpvMount.SetLocalPositionAndRotation(_firstPersonOffset, Quaternion.Euler(_firstPersonPitch, 0f, 0f));
        if (_hiddenModel == null) _hiddenModel = _target.Find("Quadcopter_Drone");
        if (_hiddenModel != null) _modelRenderers = HideableRenderers(_hiddenModel);
        _noises = new[]
        {
            _thirdPersonCamera != null ? _thirdPersonCamera.GetComponent<CinemachineBasicMultiChannelPerlin>() : null,
            _firstPersonCamera != null ? _firstPersonCamera.GetComponent<CinemachineBasicMultiChannelPerlin>() : null,
        };
        UpdatePriorities();
    }

    private void LateUpdate()
    {
        if (_target == null)
        {
            return;
        }

        if (!FollowYaw && !_crashed) StartCrash();
        else if (FollowYaw) _crashed = false;

        UpdatePriorities();
        // The body is hidden (and the crosshair leaves the camera) in the last stretch of the blend, right before
        // the camera enters the model: it neither vanishes halfway nor shows the inside of the drone
        IsFirstPerson = WantsFirstPerson
                        && (_fpvMount == null || (transform.position - _fpvMount.position).sqrMagnitude < _hideModelDistance * _hideModelDistance);
        SetModelHidden(IsFirstPerson);

        // The noise profile rotates the camera 1 degree per unit of gain
        foreach (CinemachineBasicMultiChannelPerlin noise in _noises)
        {
            if (noise != null) noise.AmplitudeGain = _shake;
        }
        _shake = Mathf.MoveTowards(_shake, 0f, _shakeDecay * Time.deltaTime);
    }

    private void UpdatePriorities()
    {
        if (_firstPersonCamera != null) _firstPersonCamera.Priority = WantsFirstPerson ? _activePriority : 10;
        if (_crashCamera != null) _crashCamera.Priority = _crashed ? _activePriority + 10 : 0;
    }

    // The crash camera starts exactly where the camera is (the brain cuts, it does not blend)
    private void StartCrash()
    {
        _crashed = true;
        if (_crashCamera != null) _crashCamera.ForceCameraPosition(transform.position, transform.rotation);
        if (_impulse != null) _impulse.GenerateImpulseWithVelocity(Random.onUnitSphere * _crashImpulse);
    }

    // The whole model except, if requested, the propellers and motors in front of the camera
    // (the arms come out of the center, right where the camera is, and would block the view).
    // Decided by position, not by name: in the model the "B" parts are the ones at the nose.
    private MeshRenderer[] HideableRenderers(Transform model)
    {
        var hideable = new System.Collections.Generic.List<MeshRenderer>();
        foreach (MeshRenderer r in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            string n = r.name;
            bool frontPart = _showFrontPropellers
                             && (n.StartsWith("Prop") || n.StartsWith("Motor"))
                             && _target.InverseTransformPoint(r.bounds.center).z > _firstPersonOffset.z + 0.2f;
            if (!frontPart) hideable.Add(r);
        }
        return hideable.ToArray();
    }

    private void SetModelHidden(bool hidden)
    {
        if (_modelRenderers == null || hidden == _modelHidden) return;
        _modelHidden = hidden;
        foreach (MeshRenderer r in _modelRenderers)
        {
            if (r != null) r.enabled = !hidden;
        }
    }
}

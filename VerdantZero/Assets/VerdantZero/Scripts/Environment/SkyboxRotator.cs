using UnityEngine;

/// <summary>
/// Slowly rotates the skybox (_Rotation property of Skybox/Panoramic or Skybox/Cubemap)
/// so the clouds appear to drift. Works on a copy of the material
/// so the original asset is not modified.
/// </summary>
public class SkyboxRotator : MonoBehaviour
{
    private static readonly int RotationId = Shader.PropertyToID("_Rotation");

    [Tooltip("Degrees per second.")]
    [SerializeField] private float _degreesPerSecond = 0.4f;

    private Material _original;
    private Material _instance;
    private float _rotation;

    /// <summary>Copy of the skybox the scene uses (null if it could not be created).</summary>
    public Material Instance => _instance;

    // In Awake so other components (e.g. SkyPurification) use the same copy in Start
    private void Awake()
    {
        _original = RenderSettings.skybox;
        if (_original == null || !_original.HasProperty(RotationId))
        {
            enabled = false;
            return;
        }

        _instance = new Material(_original);
        _rotation = _original.GetFloat(RotationId);
        RenderSettings.skybox = _instance;
    }

    private void Update()
    {
        _rotation = Mathf.Repeat(_rotation + _degreesPerSecond * Time.deltaTime, 360f);
        _instance.SetFloat(RotationId, _rotation);
    }

    private void OnDestroy()
    {
        if (_instance == null) return;
        if (RenderSettings.skybox == _instance) RenderSettings.skybox = _original;
        Destroy(_instance);
    }
}

using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Cinemachine extension: keeps the camera from going below a minimum height (e.g. when flying at ground level).
/// Runs after the Deoccluder, so it is respected even after moving away from buildings.
/// </summary>
[AddComponentMenu("Cinemachine/Procedural/Extensions/Camera Min Height")]
public class CameraMinHeight : CinemachineExtension
{
    [SerializeField] private float _minHeight = 0.5f;

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Body) return;
        float y = state.GetCorrectedPosition().y;
        if (y < _minHeight) state.PositionCorrection += Vector3.up * (_minHeight - y);
    }
}

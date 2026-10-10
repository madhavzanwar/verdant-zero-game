using UnityEngine;

/// <summary>
/// Instant Seed in flight: follows a parabola to its destination in a fixed time.
/// If it reaches a free planting spot, it plants it; otherwise it leaves an X that fades out
/// (GDD 10.5: the miss is signaled without punishment).
/// </summary>
public class SeedProjectile : MonoBehaviour
{
    [Tooltip("Miss marker (an X) that appears where the seed lands if there is no planting spot.")]
    [SerializeField] private GameObject _missMarkerPrefab;
    [SerializeField] private float _gravity = 18f;
    [SerializeField] private float _spinSpeed = 540f;

    private Vector3 _velocity;
    private float _timeLeft;
    private PlantingSpot _target;

    /// <summary>Launches the seed so it reaches <paramref name="end"/> in <paramref name="flightTime"/> seconds.</summary>
    public void Launch(Vector3 start, Vector3 end, float flightTime, PlantingSpot target)
    {
        transform.position = start;
        _target = target;
        _timeLeft = Mathf.Max(0.1f, flightTime);
        // Initial velocity for a parabola passing through end: d = v·t − ½·g·t²
        _velocity = (end - start) / _timeLeft + 0.5f * _gravity * _timeLeft * Vector3.up;
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, _timeLeft);
        _velocity += Vector3.down * (_gravity * dt);
        transform.position += _velocity * dt;
        transform.Rotate(Vector3.right, _spinSpeed * dt, Space.Self);

        _timeLeft -= dt;
        if (_timeLeft > 0f) return;

        if (_target != null && !_target.IsPlanted)
        {
            _target.Plant();
        }
        else if (_missMarkerPrefab != null)
        {
            Instantiate(_missMarkerPrefab, transform.position + Vector3.up * 0.3f, Quaternion.identity);
        }
        Destroy(gameObject);
    }
}

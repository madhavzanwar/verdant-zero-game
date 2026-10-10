using UnityEngine;

/// <summary>
/// Pollution source (chimney or exhaust pipe, GDD 8.1 and 8.4): releases a micro-cloud
/// every 10 to 15 s for the whole run, without exceeding the active cloud limit.
/// </summary>
public class SmogEmitter : MonoBehaviour
{
    [SerializeField] private SmogCloud _microCloudPrefab;
    [SerializeField] private Vector2 _interval = new Vector2(10f, 15f);
    [Tooltip("Delay before the first emission (so they do not all come out at once).")]
    [SerializeField] private Vector2 _firstDelay = new Vector2(4f, 12f);
    [Tooltip("Limit of active clouds in the whole scene (mobile performance, GDD 11.3).")]
    [SerializeField] private int _maxActiveClouds = 16;
    [Tooltip("Exit point relative to the emitter.")]
    [SerializeField] private Vector3 _spawnOffset = new Vector3(0f, 1.5f, 0f);

    private float _timer;

    private void Start()
    {
        _timer = Random.Range(_firstDelay.x, _firstDelay.y);
    }

    private void Update()
    {
        if (_microCloudPrefab == null) return;
        if (GameSession.Instance != null && GameSession.Instance.IsOver) return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        _timer = Random.Range(_interval.x, _interval.y);
        if (SmogCloud.Active.Count >= _maxActiveClouds) return;

        Instantiate(_microCloudPrefab, transform.position + _spawnOffset, Quaternion.identity);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.6f, 0.5f, 0.3f, 0.8f);
        Gizmos.DrawWireSphere(transform.position + _spawnOffset, 0.6f);
    }
}

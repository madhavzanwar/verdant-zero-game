using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Planting spot (GDD 5.2, 8.1): planter, flower bed or green roof marked by a green beam of light.
/// When it receives an Instant Seed, the plant grows instantly and passively purifies the air
/// (0.2% smog per second; see _purifyPerSecond) for the rest of the run. Each spot can only be planted once.
/// </summary>
public class PlantingSpot : MonoBehaviour
{
    private static readonly List<PlantingSpot> AvailableList = new List<PlantingSpot>();

    [Tooltip("Light beam and indicators that turn off when planted.")]
    [SerializeField] private GameObject _indicator;
    [Tooltip("Plant that grows when planted (starts hidden).")]
    [SerializeField] private Transform _plant;
    [Tooltip("Halo that lights up when the spot is in the seed's sights.")]
    [SerializeField] private GameObject _highlight;
    [SerializeField] private float _growDuration = 1.2f;
    [Tooltip("How much the smog drops per second once the plant has grown (0.002 = 0.2%). The GDD asks for 1%, tuned so winning requires combining seeds and the Beam.")]
    [SerializeField] private float _purifyPerSecond = 0.002f;
    [Tooltip("Height of the point the seed aims at, above the origin.")]
    [SerializeField] private float _aimHeight = 0.4f;

    private Vector3 _plantScale = Vector3.one;
    private bool _grown;
    private bool _targeted;

    /// <summary>Spots that have not been planted yet.</summary>
    public static IReadOnlyList<PlantingSpot> Available => AvailableList;

    /// <summary>Raised when a seed arrives and the plant sprouts (for sound and effects).</summary>
    public static event Action<PlantingSpot> Planted;

    public bool IsPlanted { get; private set; }
    public bool IsTargeted => _targeted;
    public Vector3 AimPoint => transform.position + Vector3.up * _aimHeight;

    private void Awake()
    {
        if (_plant != null)
        {
            _plantScale = _plant.localScale;
            _plant.gameObject.SetActive(false);
        }
        if (_highlight != null) _highlight.SetActive(false);
    }

    private void OnEnable()
    {
        if (!IsPlanted) AvailableList.Add(this);
    }

    private void OnDisable() => AvailableList.Remove(this);

    private void Update()
    {
        if (!_grown) return;
        GameSession session = GameSession.Instance;
        if (session == null || session.IsOver) return;
        session.ReduceSmog(_purifyPerSecond * Time.deltaTime);
    }

    public void SetTargeted(bool targeted)
    {
        if (IsPlanted || targeted == _targeted) return;
        _targeted = targeted;
        if (_highlight != null) _highlight.SetActive(targeted);
    }

    /// <summary>The seed arrives: the plant sprouts and starts purifying.</summary>
    public void Plant()
    {
        if (IsPlanted) return;
        IsPlanted = true;
        _targeted = false;
        AvailableList.Remove(this);
        if (_highlight != null) _highlight.SetActive(false);
        if (_indicator != null) _indicator.SetActive(false);
        StartCoroutine(Grow());
        Planted?.Invoke(this);
    }

    private IEnumerator Grow()
    {
        if (_plant != null)
        {
            _plant.gameObject.SetActive(true);
            for (float t = 0f; t < _growDuration; t += Time.deltaTime)
            {
                // Growth with a small bounce at the end: it feels like "it sprouted!"
                float k = t / _growDuration;
                float overshoot = 1f + 0.15f * Mathf.Sin(k * Mathf.PI);
                _plant.localScale = _plantScale * (k * overshoot);
                yield return null;
            }
            _plant.localScale = _plantScale;
        }
        _grown = true;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 0.9f, 0.3f, 0.9f);
        Gizmos.DrawWireSphere(AimPoint, 0.6f);
    }
}

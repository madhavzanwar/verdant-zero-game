using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Varied vegetation that sprouts around a planting spot when planted (in addition to the main tree):
/// trees, bushes, grass and mushrooms from the LowpolyVegetationPackFree pack, scaled and spread over the
/// spot's surface (rooftop or park). Each piece grows with a small bounce and a different delay.
/// Lives on the same object as <see cref="PlantingSpot"/> and is triggered by its <see cref="PlantingSpot.Planted"/> event.
/// </summary>
[RequireComponent(typeof(PlantingSpot))]
public class PlantingVegetation : MonoBehaviour
{
    [Serializable]
    private class Piece
    {
        public GameObject Prefab;
        [Tooltip("Final height in meters (min and max); the model is scaled to it.")]
        public Vector2 Height = new Vector2(0.5f, 1f);
        [Tooltip("Relative probability of appearing.")]
        public float Weight = 1f;
    }

    [SerializeField] private Piece[] _pieces = Array.Empty<Piece>();
    [Tooltip("How many pieces sprout (min and max).")]
    [SerializeField] private Vector2Int _count = new Vector2Int(8, 11);
    [Tooltip("Radius in meters over which they are spread. Smaller on rooftops.")]
    [SerializeField] private float _radius = 3f;
    [Tooltip("Clear gap around the main tree.")]
    [SerializeField] private float _innerRadius = 0.7f;
    [Tooltip("URP material with the pack's palette (its original materials are for the legacy pipeline).")]
    [SerializeField] private Material _material;
    [SerializeField] private float _growDuration = 0.9f;
    [Tooltip("Maximum delay between the first piece sprouting and the last.")]
    [SerializeField] private float _maxDelay = 0.9f;

    private PlantingSpot _spot;

    private void Awake() => _spot = GetComponent<PlantingSpot>();

    private void OnEnable() => PlantingSpot.Planted += OnPlanted;

    private void OnDisable() => PlantingSpot.Planted -= OnPlanted;

    private void OnPlanted(PlantingSpot spot)
    {
        if (spot != _spot || _pieces.Length == 0) return;

        var random = new System.Random(transform.position.GetHashCode() ^ Environment.TickCount);
        int count = random.Next(_count.x, _count.y + 1);
        for (int i = 0; i < count; i++)
        {
            Piece piece = PickPiece(random);
            if (piece == null || piece.Prefab == null) continue;
            if (!FindGround(random, out Vector3 position)) continue;

            float height = Mathf.Lerp(piece.Height.x, piece.Height.y, (float)random.NextDouble());
            float yaw = (float)random.NextDouble() * 360f;
            float delay = (float)random.NextDouble() * _maxDelay;
            StartCoroutine(Sprout(piece.Prefab, position, yaw, height, delay));
        }
    }

    private Piece PickPiece(System.Random random)
    {
        float total = 0f;
        foreach (Piece p in _pieces) total += Mathf.Max(0f, p.Weight);
        float roll = (float)random.NextDouble() * total;
        foreach (Piece p in _pieces)
        {
            roll -= Mathf.Max(0f, p.Weight);
            if (roll <= 0f) return p;
        }
        return _pieces[_pieces.Length - 1];
    }

    // A random point on the ring that lies on the same surface as the planting spot
    // (so on rooftops nothing is left floating past the edge)
    private bool FindGround(System.Random random, out Vector3 position)
    {
        Vector3 center = transform.position;
        for (int attempt = 0; attempt < 6; attempt++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Lerp(_innerRadius, _radius, Mathf.Sqrt((float)random.NextDouble()));
            Vector3 probe = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

            RaycastHit[] hits = Physics.RaycastAll(probe + Vector3.up * 2f, Vector3.down, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (Mathf.Abs(hit.point.y - center.y) > 0.6f || hit.normal.y < 0.7f) break;
                // The height is the spot's own (lawn or rooftop): building colliders are boxes
                // slightly taller than the visible rooftop, and the plants would float
                position = new Vector3(hit.point.x, center.y, hit.point.z);
                return true;
            }
        }
        position = default;
        return false;
    }

    private IEnumerator Sprout(GameObject prefab, Vector3 position, float yaw, float height, float delay)
    {
        yield return new WaitForSeconds(delay);

        GameObject plant = Instantiate(prefab, position, Quaternion.Euler(0f, yaw, 0f), transform);
        Renderer[] renderers = plant.GetComponentsInChildren<Renderer>();
        if (_material != null)
        {
            foreach (Renderer r in renderers)
            {
                var materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = _material;
                r.sharedMaterials = materials;
            }
        }

        // Scale to reach the requested height, whatever the model's size
        float modelHeight = 0f;
        foreach (Renderer r in renderers) modelHeight = Mathf.Max(modelHeight, r.bounds.max.y - position.y);
        Vector3 finalScale = plant.transform.localScale * (modelHeight > 0.01f ? height / modelHeight : 1f);

        for (float t = 0f; t < _growDuration; t += Time.deltaTime)
        {
            float k = t / _growDuration;
            float overshoot = 1f + 0.12f * Mathf.Sin(k * Mathf.PI);
            plant.transform.localScale = finalScale * (k * overshoot);
            yield return null;
        }
        plant.transform.localScale = finalScale;
    }
}

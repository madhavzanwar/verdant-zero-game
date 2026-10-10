using System;
using UnityEngine;

/// <summary>
/// Detects crashes against objects with <see cref="CrashHazard"/> (buildings).
/// Settling on top (contact from below, nearly horizontal surface and gentle descent) is a landing, not a crash.
/// On crash: cuts control, enables gravity, makes the drone fall spinning and raises <see cref="Crashed"/>.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class QuadcopterCrash : MonoBehaviour
{
    public enum CrashReason
    {
        None,
        HitObstacle,
        HardLanding,
    }

    [Header("Side impact")]
    [Tooltip("Minimum impact speed against a wall (m/s) to crash. 0 = any contact.")]
    [SerializeField] private float _minImpactSpeed = 0f;

    [Header("Landing")]
    [Tooltip("Minimum Y component of the normal to treat the contact as ground (0.7 ≈ 45° slope).")]
    [SerializeField] private float _landingNormalMinY = 0.7f;
    [Tooltip("Maximum descent speed (m/s) to land without breaking.")]
    [SerializeField] private float _maxLandingSpeed = 7f;

    [Header("Falling")]
    [Tooltip("Outward push from the building on crash (m/s).")]
    [SerializeField] private float _bounceSpeed = 2f;
    [Tooltip("Random spin when falling (rad/s).")]
    [SerializeField] private float _tumbleSpeed = 6f;

    private Rigidbody _rb;
    private QuadcopterController _controller;
    private QuadInput _input;
    private QuadVisualTilt _visualTilt;

    /// <summary>Raised only once, at the moment of the crash.</summary>
    public event Action Crashed;

    public bool HasCrashed { get; private set; }

    public CrashReason LastCrashReason { get; private set; }

    /// <summary>True while the drone is resting on a building.</summary>
    public bool IsLanded { get; private set; }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _controller = GetComponent<QuadcopterController>();
        _input = GetComponent<QuadInput>();
        _visualTilt = GetComponentInChildren<QuadVisualTilt>();
    }

    private void FixedUpdate()
    {
        // Recalculated every physics step from OnCollisionStay
        IsLanded = false;
    }

    private void OnCollisionEnter(Collision collision) => Evaluate(collision, isNewContact: true);

    // Covers side contacts that appear while the drone is already touching the same building
    private void OnCollisionStay(Collision collision) => Evaluate(collision, isNewContact: false);

    private void Evaluate(Collision collision, bool isNewContact)
    {
        if (HasCrashed) return;
        if (collision.collider.GetComponentInParent<CrashHazard>() == null) return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            // Speed at which the drone approaches the surface, along its normal
            float approachSpeed = Vector3.Dot(collision.relativeVelocity, contact.normal);

            if (contact.normal.y >= _landingNormalMinY)
            {
                // Surface below: landing, unless it arrives too fast
                if (isNewContact && approachSpeed > _maxLandingSpeed)
                {
                    Crash(contact.normal, CrashReason.HardLanding);
                    return;
                }
                IsLanded = true;
                continue;
            }

            // Wall or ceiling: crash
            bool hitHardEnough = approachSpeed >= _minImpactSpeed;
            bool anyTouchCounts = isNewContact && _minImpactSpeed <= 0f;
            if (hitHardEnough || anyTouchCounts)
            {
                Crash(contact.normal, CrashReason.HitObstacle);
                return;
            }
        }
    }

    private void Crash(Vector3 hitNormal, CrashReason reason)
    {
        HasCrashed = true;
        IsLanded = false;
        LastCrashReason = reason;

        // Without control: the drone becomes a physics object that falls
        if (_controller != null) _controller.enabled = false;
        if (_input != null) _input.enabled = false;
        if (_visualTilt != null) _visualTilt.enabled = false;

        _rb.useGravity = true;
        _rb.constraints = RigidbodyConstraints.None;
        _rb.AddForce(hitNormal * _bounceSpeed, ForceMode.VelocityChange);
        _rb.AddTorque(UnityEngine.Random.onUnitSphere * _tumbleSpeed, ForceMode.VelocityChange);

        Crashed?.Invoke();
    }
}

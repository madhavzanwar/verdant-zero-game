using System.Collections;
using UnityEngine;

/// <summary>
/// Quadcopter motor hum: a 3D loop whose pitch and volume follow
/// horizontal speed and climb. It starts with a "spin-up" and, when crashing or losing,
/// the motor powers down with falling pitch. On victory it keeps hovering while the drone
/// celebrates and then slowly fades out.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class QuadcopterAudio : MonoBehaviour
{
    [SerializeField] private AudioClip _motorLoop;

    [Header("Pitch")]
    [SerializeField] private float _idlePitch = 0.9f;
    [SerializeField] private float _maxSpeedPitch = 1.35f;
    [Tooltip("Extra pitch when climbing (+) and descending (-), per vertical m/s.")]
    [SerializeField] private float _climbPitchPerMeter = 0.03f;

    [Header("Volume")]
    [SerializeField] private float _idleVolume = 0.55f;
    [SerializeField] private float _maxSpeedVolume = 0.9f;
    [SerializeField] private float _smoothing = 6f;

    [Header("Spin-up and shutdown")]
    [SerializeField] private float _spinUpDuration = 0.8f;
    [SerializeField] private float _powerDownDuration = 0.7f;
    [Tooltip("On victory, seconds the motor keeps hovering (the length of the drone's celebration).")]
    [SerializeField] private float _victoryHoverTime = 1.3f;
    [Tooltip("On victory, seconds the motor takes to fade out after the celebration.")]
    [SerializeField] private float _victoryFadeDuration = 2f;

    private Rigidbody _rb;
    private QuadcopterController _controller;
    private QuadcopterCrash _crash;
    private GameSession _session;
    private AudioSource _source;
    private float _spinUp;
    private bool _poweringDown;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _controller = GetComponent<QuadcopterController>();
        _crash = GetComponent<QuadcopterCrash>();

        _source = gameObject.AddComponent<AudioSource>();
        _source.clip = _motorLoop;
        _source.loop = true;
        _source.playOnAwake = false;
        _source.spatialBlend = 0.8f; // almost 3D: positioned in space but never fully disappears
        _source.rolloffMode = AudioRolloffMode.Logarithmic;
        _source.minDistance = 6f;
        _source.maxDistance = 80f;
        _source.dopplerLevel = 0.3f;
        _source.volume = 0f;
        _source.pitch = _idlePitch * 0.5f;
    }

    private void Start()
    {
        // The AudioManager is created before the first scene; the SFX group respects the effects slider
        if (AudioManager.Instance != null) _source.outputAudioMixerGroup = AudioManager.Instance.SfxGroup;
        if (_motorLoop != null) _source.Play();
    }

    private void OnEnable()
    {
        if (_crash != null) _crash.Crashed += OnCrashed;
        _session = FindAnyObjectByType<GameSession>();
        if (_session != null) _session.Ended += OnSessionEnded;
    }

    private void OnDisable()
    {
        if (_crash != null) _crash.Crashed -= OnCrashed;
        if (_session != null) _session.Ended -= OnSessionEnded;
    }

    // On defeat the motors power down just like on a crash; on victory the drone keeps hovering and celebrates,
    // so the motor fades out slowly instead of sounding like a failure
    private void OnSessionEnded(GameSession.Result result)
    {
        if (result != GameSession.Result.Victory)
        {
            OnCrashed();
            return;
        }
        if (_poweringDown) return;
        StartCoroutine(VictoryWindDown());
    }

    private void Update()
    {
        if (_poweringDown || _motorLoop == null) return;

        _spinUp = Mathf.MoveTowards(_spinUp, 1f, Time.deltaTime / Mathf.Max(0.01f, _spinUpDuration));

        float maxSpeed = _controller != null ? _controller.MaxSpeed : 12f;
        float horizontal = new Vector2(_rb.linearVelocity.x, _rb.linearVelocity.z).magnitude;
        float speed01 = Mathf.Clamp01(horizontal / Mathf.Max(0.01f, maxSpeed));
        float vertical = _rb.linearVelocity.y;

        float targetPitch = Mathf.Lerp(_idlePitch, _maxSpeedPitch, speed01)
                            + Mathf.Clamp(vertical * _climbPitchPerMeter, -0.1f, 0.15f);
        float targetVolume = Mathf.Lerp(_idleVolume, _maxSpeedVolume, speed01);

        // During spin-up the pitch rises from half
        targetPitch *= Mathf.Lerp(0.5f, 1f, _spinUp);
        targetVolume *= _spinUp;

        float k = 1f - Mathf.Exp(-_smoothing * Time.deltaTime);
        _source.pitch = Mathf.Lerp(_source.pitch, targetPitch, k);
        _source.volume = Mathf.Lerp(_source.volume, targetVolume, k);
    }

    private void OnCrashed()
    {
        if (_poweringDown) return;
        _poweringDown = true;
        StartCoroutine(PowerDown());
    }

    private IEnumerator PowerDown()
    {
        float startPitch = _source.pitch;
        float startVolume = _source.volume;
        for (float t = 0f; t < _powerDownDuration; t += Time.deltaTime)
        {
            float k = t / _powerDownDuration;
            _source.pitch = Mathf.Lerp(startPitch, startPitch * 0.35f, k);
            _source.volume = Mathf.Lerp(startVolume, 0f, k * k);
            yield return null;
        }
        _source.Stop();
    }

    private IEnumerator VictoryWindDown()
    {
        // While the drone celebrates, Update keeps moving the pitch and volume as in normal flight
        yield return new WaitForSeconds(_victoryHoverTime);
        if (_poweringDown) yield break;
        _poweringDown = true;

        float startPitch = _source.pitch;
        float startVolume = _source.volume;
        for (float t = 0f; t < _victoryFadeDuration; t += Time.deltaTime)
        {
            float k = t / _victoryFadeDuration;
            _source.pitch = Mathf.Lerp(startPitch, startPitch * 0.85f, k);
            _source.volume = Mathf.Lerp(startVolume, 0f, k);
            yield return null;
        }
        _source.Stop();
    }
}

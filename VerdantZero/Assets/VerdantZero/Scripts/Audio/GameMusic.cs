using System.Collections;
using UnityEngine;

/// <summary>
/// Layered gameplay music (GDD 9.2): a base that always plays and a tension layer
/// (percussion and arpeggio) that comes in as the smog nears 80%. Both tracks have the same length
/// and start at the same DSP instant, so they stay in time while looping.
/// When the run ends they fade out to let the victory or defeat sound play.
/// Routed through the mixer's Music group, so it respects the music volume in Options.
/// </summary>
public class GameMusic : MonoBehaviour
{
    [SerializeField] private AudioClip _baseLayer;
    [SerializeField] private AudioClip _tensionLayer;
    [Range(0f, 1f)]
    [SerializeField] private float _baseVolume = 0.7f;
    [Range(0f, 1f)]
    [SerializeField] private float _tensionVolume = 0.8f;

    [Header("Tension layer")]
    [Tooltip("Smog level at which the layer starts playing.")]
    [Range(0f, 1f)]
    [SerializeField] private float _tensionFrom = 0.7f;
    [Tooltip("Smog level at which the layer plays at full volume.")]
    [Range(0f, 1f)]
    [SerializeField] private float _tensionFull = 0.8f;
    [Tooltip("Speed at which the layer follows the smog.")]
    [SerializeField] private float _smoothing = 2f;

    [Header("Fades")]
    [SerializeField] private float _fadeIn = 1.5f;
    [SerializeField] private float _fadeOutOnEnd = 1.2f;

    private GameSession _session;
    private AudioSource _base;
    private AudioSource _tension;
    private float _master;
    private float _tensionAmount;
    private bool _ending;

    private void Awake()
    {
        _base = CreateSource("Music Base", _baseLayer);
        _tension = CreateSource("Music Tension", _tensionLayer);
    }

    private void Start()
    {
        _session = FindAnyObjectByType<GameSession>();
        if (_session != null) _session.Ended += OnSessionEnded;

        if (AudioManager.Instance != null)
        {
            _base.outputAudioMixerGroup = AudioManager.Instance.MusicGroup;
            _tension.outputAudioMixerGroup = AudioManager.Instance.MusicGroup;
        }

        // Same start instant: the layers stay in sync forever
        double start = AudioSettings.dspTime + 0.1;
        if (_baseLayer != null) _base.PlayScheduled(start);
        if (_tensionLayer != null) _tension.PlayScheduled(start);
    }

    private void OnDestroy()
    {
        if (_session != null) _session.Ended -= OnSessionEnded;
    }

    private void Update()
    {
        if (_ending) return;

        // Unscaled time: the music keeps playing while paused (as in the AudioManager)
        float dt = Time.unscaledDeltaTime;
        _master = Mathf.MoveTowards(_master, 1f, dt / Mathf.Max(0.01f, _fadeIn));

        float target = _session != null ? Mathf.InverseLerp(_tensionFrom, _tensionFull, _session.Smog) : 0f;
        _tensionAmount = Mathf.Lerp(_tensionAmount, target, 1f - Mathf.Exp(-_smoothing * dt));

        _base.volume = _baseVolume * _master;
        _tension.volume = _tensionVolume * _master * _tensionAmount;
    }

    private void OnSessionEnded(GameSession.Result result)
    {
        if (_ending) return;
        _ending = true;
        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        float baseStart = _base.volume;
        float tensionStart = _tension.volume;
        for (float t = 0f; t < _fadeOutOnEnd; t += Time.unscaledDeltaTime)
        {
            float k = 1f - t / _fadeOutOnEnd;
            _base.volume = baseStart * k;
            _tension.volume = tensionStart * k;
            yield return null;
        }
        _base.Stop();
        _tension.Stop();
    }

    private AudioSource CreateSource(string sourceName, AudioClip clip)
    {
        var child = new GameObject(sourceName);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.ignoreListenerPause = true;
        return source;
    }
}

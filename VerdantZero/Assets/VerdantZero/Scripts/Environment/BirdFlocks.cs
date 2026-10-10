using UnityEngine;

/// <summary>
/// Flocks of birds that fly over the city when the air is almost clean.
/// When the smog drops to <see cref="_appearSmog"/> they spiral in from outside the city and circle
/// over it; if the smog rises back above <see cref="_leaveSmog"/> they fly away and disappear.
/// On victory they stay flying. A bird sound loop plays while they are in the sky.
/// The flock prefab only flaps in place: this script does the flying by moving the root.
/// </summary>
public class BirdFlocks : MonoBehaviour
{
    [SerializeField] private GameSession _session;

    [Header("Flocks")]
    [Tooltip("Flock prefab (e.g. 10BirdsPrefab from Low Poly Birds, URP version). If missing, nothing happens.")]
    [SerializeField] private GameObject _flockPrefab;
    [SerializeField] private int _flockCount = 2;
    [Tooltip("The pack's birds have a wingspan of about 0.16 m.")]
    [SerializeField] private float _flockScale = 8f;
    [Tooltip("Extra rotation so the beak faces the flight direction.")]
    [SerializeField] private float _modelYaw;

    [Header("Smog thresholds (0-1)")]
    [Tooltip("They appear when the smog drops to this level.")]
    [Range(0f, 1f)]
    [SerializeField] private float _appearSmog = 0.2f;
    [Tooltip("They leave if the smog rises above this level (otherwise they would come and go non-stop around 20%).")]
    [Range(0f, 1f)]
    [SerializeField] private float _leaveSmog = 0.3f;

    [Header("Flight")]
    [Tooltip("Center of the circles. Empty = this object.")]
    [SerializeField] private Transform _center;
    [Tooltip("Circle radius of the first flock and of the last.")]
    [SerializeField] private Vector2 _radius = new Vector2(55f, 95f);
    [Tooltip("Height of the first flock and of the last.")]
    [SerializeField] private Vector2 _height = new Vector2(24f, 32f);
    [Tooltip("Flight speed in m/s.")]
    [SerializeField] private float _speed = 9f;
    [Tooltip("Extra distance outside the circle from which they arrive and to which they leave.")]
    [SerializeField] private float _offscreenDistance = 160f;
    [Tooltip("Seconds they take to arrive or leave.")]
    [SerializeField] private float _transitionTime = 10f;
    [SerializeField] private float _bobAmplitude = 1.5f;
    [SerializeField] private float _bobFrequency = 0.25f;

    [Header("Sound")]
    [SerializeField] private AudioClip _birdsLoop;
    [SerializeField] private float _birdsVolume = 0.5f;
    [SerializeField] private float _audioFade = 2f;

    private class Flock
    {
        public Transform Root;
        public float Angle;
        public float Radius;
        public float Height;
        public float Direction;
        public float BobPhase;
        public float Presence; // 0 = outside the city, 1 = circling
    }

    private Flock[] _flocks = new Flock[0];
    private bool _inSky;
    private AudioSource _audio;

    private void Start()
    {
        if (_session == null) _session = FindAnyObjectByType<GameSession>();
        if (_center == null) _center = transform;
        if (_flockPrefab == null) return;

        _flocks = new Flock[Mathf.Max(0, _flockCount)];
        for (int i = 0; i < _flocks.Length; i++)
        {
            float t = _flocks.Length > 1 ? i / (_flocks.Length - 1f) : 0f;
            _flocks[i] = new Flock
            {
                Root = CreateFlock(i),
                Angle = i * Mathf.PI * 2f / _flocks.Length + Random.Range(-0.3f, 0.3f),
                Radius = Mathf.Lerp(_radius.x, _radius.y, t),
                Height = Mathf.Lerp(_height.x, _height.y, t),
                Direction = i % 2 == 0 ? 1f : -1f,
                BobPhase = Random.Range(0f, Mathf.PI * 2f),
            };
        }

        if (_birdsLoop != null)
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.clip = _birdsLoop;
            _audio.loop = true;
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _audio.volume = 0f;
            if (AudioManager.Instance != null) _audio.outputAudioMixerGroup = AudioManager.Instance.AmbienceGroup;
        }
    }

    private Transform CreateFlock(int index)
    {
        GameObject flock = Instantiate(_flockPrefab, transform);
        flock.name = $"BirdFlock_{index + 1}";
        flock.transform.localScale = Vector3.one * _flockScale;

        // Lightweight for mobile and WebGL: no shadows, one bone per vertex and no animation off-camera
        foreach (SkinnedMeshRenderer skin in flock.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            skin.quality = SkinQuality.Bone1;
            skin.updateWhenOffscreen = false;
            skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            skin.receiveShadows = false;
            skin.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            skin.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }
        foreach (Animator animator in flock.GetComponentsInChildren<Animator>(true))
        {
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            animator.speed = Random.Range(0.9f, 1.1f);
        }

        flock.SetActive(false);
        return flock.transform;
    }

    private void Update()
    {
        if (_flocks.Length == 0 || _session == null) return;

        // On victory they stay as they are; on defeat the smog will be high and they will leave
        if (!_session.IsOver || _session.Outcome != GameSession.Result.Victory)
        {
            if (!_inSky && _session.Smog <= _appearSmog) _inSky = true;
            else if (_inSky && _session.Smog > _leaveSmog) _inSky = false;
        }

        float step = Time.deltaTime / Mathf.Max(0.01f, _transitionTime);
        foreach (Flock flock in _flocks) Fly(flock, step);

        UpdateAudio();
    }

    private void Fly(Flock flock, float step)
    {
        flock.Presence = Mathf.MoveTowards(flock.Presence, _inSky ? 1f : 0f, step);
        bool visible = flock.Presence > 0f;
        if (flock.Root.gameObject.activeSelf != visible)
        {
            flock.Root.gameObject.SetActive(visible);
            if (visible)
            {
                // Flap offset so the flocks are not in sync
                foreach (Animator animator in flock.Root.GetComponentsInChildren<Animator>())
                    animator.Play(0, 0, Random.value);
            }
        }
        if (!visible) return;

        // They arrive and leave in a spiral: the radius opens outward from the city
        float ease = Mathf.SmoothStep(0f, 1f, flock.Presence);
        float radius = flock.Radius + _offscreenDistance * (1f - ease);
        flock.Angle += flock.Direction * _speed / radius * Time.deltaTime;

        Vector3 offset = new Vector3(Mathf.Cos(flock.Angle), 0f, Mathf.Sin(flock.Angle)) * radius;
        float bob = Mathf.Sin(Time.time * _bobFrequency * Mathf.PI * 2f + flock.BobPhase) * _bobAmplitude;
        Vector3 position = _center.position + offset + Vector3.up * (flock.Height + bob);

        Vector3 velocity = position - flock.Root.position;
        flock.Root.position = position;
        if (velocity.sqrMagnitude > 0.0001f)
            flock.Root.rotation = Quaternion.LookRotation(velocity) * Quaternion.Euler(0f, _modelYaw, 0f);
    }

    private void UpdateAudio()
    {
        if (_audio == null) return;

        float target = _inSky ? _birdsVolume : 0f;
        _audio.volume = Mathf.MoveTowards(_audio.volume, target, _birdsVolume * Time.deltaTime / Mathf.Max(0.01f, _audioFade));
        if (_audio.volume > 0f && !_audio.isPlaying) _audio.Play();
        else if (_audio.volume <= 0f && _audio.isPlaying) _audio.Stop();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = _center != null ? _center.position : transform.position;
        Gizmos.color = new Color(0.4f, 0.8f, 1f);
        DrawCircle(center + Vector3.up * _height.x, _radius.x);
        DrawCircle(center + Vector3.up * _height.y, _radius.y);
    }

    private static void DrawCircle(Vector3 center, float radius)
    {
        const int Segments = 48;
        Vector3 previous = center + Vector3.right * radius;
        for (int i = 1; i <= Segments; i++)
        {
            float a = i * Mathf.PI * 2f / Segments;
            Vector3 next = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}

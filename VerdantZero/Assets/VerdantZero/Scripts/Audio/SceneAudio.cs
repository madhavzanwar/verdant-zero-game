using UnityEngine;

/// <summary>
/// Defines a scene's music and ambience. When the scene starts it hands them to the AudioManager
/// (with a fade). If a clip is empty, that channel stops.
/// </summary>
public class SceneAudio : MonoBehaviour
{
    [SerializeField] private AudioClip _music;
    [Tooltip("Music volume for this scene (0-1). Multiplied by the music slider in Options.")]
    [Range(0f, 1f)]
    [SerializeField] private float _musicVolume = 1f;
    [SerializeField] private AudioClip _ambience;
    [Tooltip("Fade duration; -1 uses the AudioManager's default.")]
    [SerializeField] private float _fadeDuration = -1f;

    private void Start()
    {
        AudioManager audio = AudioManager.Instance;
        if (audio == null) return;

        audio.PlayMusic(_music, _fadeDuration, _musicVolume);
        audio.PlayAmbience(_ambience, _fadeDuration);
    }
}

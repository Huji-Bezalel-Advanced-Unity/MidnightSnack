using UnityEngine;
using System.Collections; // Required for Coroutines

public class SoundManager : MonoBehaviour
{
    // Singleton pattern (simple version)
    public static SoundManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource; // For general sound effects
    [SerializeField] private AudioSource voiceSource; // Dedicated for narrator/dialogue

    [Header("Settings")]
    [Tooltip("Prevents narrator lines from overlapping.")]
    [SerializeField] private bool interruptVoiceLines = true;

    private Coroutine currentVoiceCoroutine = null;

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Keep it across scenes if needed

        // Basic validation
        if (musicSource == null || sfxSource == null || voiceSource == null)
        {
            Debug.LogError("SoundManager is missing one or more AudioSource references!", this);
        }
    }

    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (musicSource == null || clip == null) return;
        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource == null) return;
        musicSource.Stop();
    }

    // Play a one-shot sound effect
    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip); // Plays without interrupting other SFX on this source
    }

    // Play a narrator/dialogue line
    public void PlayVoiceLine(AudioClip clip, float delay = 0f)
    {
        if (voiceSource == null || clip == null) return;

        if (interruptVoiceLines && voiceSource.isPlaying)
        {
            voiceSource.Stop();
            if (currentVoiceCoroutine != null)
            {
                StopCoroutine(currentVoiceCoroutine);
                currentVoiceCoroutine = null;
            }
        }

        // Don't play if already playing and interruption is off
        if (!interruptVoiceLines && voiceSource.isPlaying)
        {
            // Optionally queue it up, or just ignore it
            // Debug.Log("Voice line skipped (non-interruptible is playing).");
            return;
        }

        // Start a coroutine to handle potential delay
        currentVoiceCoroutine = StartCoroutine(PlayVoiceWithDelay(clip, delay));
    }

    private IEnumerator PlayVoiceWithDelay(AudioClip clip, float delay)
    {
        if (delay > 0)
        {
            yield return new WaitForSeconds(delay);
        }
        if (voiceSource != null && clip != null) // Check again in case things changed during delay
        {
             // Use PlayOneShot if you want potential overlap ONLY if interruptVoiceLines is false
             // voiceSource.PlayOneShot(clip);
             // Use Play if you want it to be the ONLY thing on the voiceSource
             voiceSource.clip = clip;
             voiceSource.Play();
        }
        currentVoiceCoroutine = null; // Coroutine finished
    }


    // Add methods for volume control, pausing, etc. as needed
    // public void SetMasterVolume(float volume) { AudioListener.volume = volume; }
    // public void SetMusicVolume(float volume) { if (musicSource) musicSource.volume = volume; }
    // ... etc ...
}
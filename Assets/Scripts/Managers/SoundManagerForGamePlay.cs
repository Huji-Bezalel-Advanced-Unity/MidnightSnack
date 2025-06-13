using UnityEngine;

public class SoundManagerForGamePlay : MonoBehaviour
{
    public static SoundManagerForGamePlay Instance;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;

    [Header("SFX Clips")]
    public AudioClip objectPushClip;
    public AudioClip doorMoveClip;
    public AudioClip platformMoveClip;
    public AudioClip pressurePlateClickClip;
    public AudioClip backGroundClip1;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }
        PlaySFX(backGroundClip1);
    }
    public void PlaySFX(AudioClip clip)
    {
        if (clip != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }
    public void PlayPushSound() => PlaySFX(objectPushClip);
    public void PlayDoorMoveSound() => PlaySFX(doorMoveClip);
    public void PlayPlatformMoveSound() => PlaySFX(platformMoveClip);
    public void PlayPressurePlateSound() => PlaySFX(pressurePlateClickClip);

    public void PlayClickSound() => PlayPressurePlateSound();
}
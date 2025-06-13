using UnityEngine;
using System.Collections;

public class StuckSequenceManager : MonoBehaviour
{
    public static StuckSequenceManager Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("The Canvas GameObject that acts as the black screen or transition visual.")]
    [SerializeField] private Canvas blackScreenCanvas;

    [Header("Sequence Settings")]
    [Tooltip("The sound to play when the sequence starts.")]
    [SerializeField] private AudioClip sequenceSound;
    [Tooltip("The sound to play after the sequence ends.")]
    [SerializeField] private AudioClip afterSound;
    [Tooltip("How long the screen stays black/canvas is visible (in seconds).")]
    [SerializeField] private float sequenceDuration = 2.5f;
    [Tooltip("Delay before showing black screen after stuck event (in seconds).")]
    [SerializeField] private float canvasDelayBeforeShow = 1.5f;

    private DarkPlayerController currentPlayerStuck;
    private Coroutine activeStuckSequenceCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (blackScreenCanvas != null)
        {
            blackScreenCanvas.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError("StuckSequenceManager: Black Screen Canvas is not assigned in the Inspector!", this);
        }
    }

    void OnEnable()
    {
        EventManager.OnShowStuckDecisionUI += StartStuckSequence;
    }

    void OnDisable()
    {
        EventManager.OnShowStuckDecisionUI -= StartStuckSequence;
    }

    private void StartStuckSequence(DarkPlayerController stuckPlayer)
    {
        if (stuckPlayer == null)
        {
            Debug.LogError("StuckSequenceManager: StartStuckSequence called with null player. Aborting.", this);
            return;
        }
        if (blackScreenCanvas == null)
        {
            Debug.LogError("StuckSequenceManager: Black Screen Canvas is not assigned. Falling back to direct event trigger.", this);
            EventManager.TriggerDarkPlayerStuckInLight(stuckPlayer);
            return;
        }
        if (activeStuckSequenceCoroutine != null)
        {
            Debug.LogWarning("StuckSequenceManager: Attempting to start sequence while another is active. Aborting new request.", this);
            return;
        }

        Debug.Log($"StuckSequenceManager: Starting stuck sequence for {stuckPlayer.name}", this);
        currentPlayerStuck = stuckPlayer;

        activeStuckSequenceCoroutine = StartCoroutine(HandleStuckSequenceCoroutine());
    }

    private IEnumerator HandleStuckSequenceCoroutine()
    {
        if (currentPlayerStuck == null)
        {
            Debug.LogError("StuckSequenceManager: currentPlayerStuck is null. Aborting sequence.", this);
            activeStuckSequenceCoroutine = null;
            yield break;
        }

        // Optional delay before showing canvas
        if (canvasDelayBeforeShow > 0)
        {
            Debug.Log($"StuckSequenceManager: Waiting {canvasDelayBeforeShow} seconds before showing black screen.", this);
            yield return new WaitForSecondsRealtime(canvasDelayBeforeShow);
        }

        // Turn ON the black screen Canvas
        if (blackScreenCanvas != null)
        {
            blackScreenCanvas.gameObject.SetActive(true);
            Debug.Log("StuckSequenceManager: Black screen canvas activated.");
        }

        // Play initial sound
        if (sequenceSound != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(sequenceSound);
            Debug.Log($"StuckSequenceManager: Played initial sound '{sequenceSound.name}'.");
        }

        // Wait while canvas is visible
        Debug.Log($"StuckSequenceManager: Waiting for {sequenceDuration} seconds with black screen.");
        yield return new WaitForSecondsRealtime(sequenceDuration);

        // Play after-sound
        if (afterSound != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(afterSound);
            Debug.Log($"StuckSequenceManager: Played after sound '{afterSound.name}'.");
        }

        // Turn OFF the black screen Canvas
        if (blackScreenCanvas != null)
        {
            blackScreenCanvas.gameObject.SetActive(false);
            Debug.Log("StuckSequenceManager: Black screen canvas deactivated.");
        }

        // Trigger event
        Debug.Log($"StuckSequenceManager: Triggering OnDarkPlayerStuckInLight for {currentPlayerStuck.name}.");
        EventManager.TriggerDarkPlayerStuckInLight(currentPlayerStuck);

        currentPlayerStuck = null;
        activeStuckSequenceCoroutine = null;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}

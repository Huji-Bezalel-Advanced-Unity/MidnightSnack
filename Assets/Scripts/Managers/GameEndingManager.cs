using UnityEngine;
using UnityEngine.Events; // For potential UnityEvents for endings

public class GameEndingManager : MonoBehaviour
{
    [Header("Ending Conditions")]
    [Tooltip("The EventManager ID sent when the door allowing the Light Player to pass is opened.")]
    [SerializeField] private string lightPlayerDoorUnlockID;
    [Tooltip("Tag assigned to the Player GameObject.")]
    [SerializeField] private string PlayerTag = "Player"; // Make sure your Dark Player has this tag!

    [Header("Trigger Setup")]
    [Tooltip("Assign the Collider2D (set to IsTrigger=true) placed at the final exit point.")]
    [SerializeField] private Collider2D finalExitTrigger;

    [Header("Ending Actions (Optional)")]
    [Tooltip("Actions to perform BEFORE the EventManager.OnGameEnded event is triggered for cooperative ending.")]
    public UnityEvent onCooperativeEnding; // Renamed slightly for clarity
    [Tooltip("Actions to perform BEFORE the EventManager.OnGameEnded event is triggered for solo ending.")]
    public UnityEvent onSoloEnding; // Renamed slightly for clarity

    // --- Internal State ---
    private bool lightPlayerDoorWasOpened = false;
    private bool endingTriggered = false;

    void Awake()
    {
        // Validation
        if (finalExitTrigger == null)
        {
            Debug.LogError("GameEndingManager: Final Exit Trigger Collider is not assigned!", this);
            enabled = false; // Disable if trigger isn't set
            return;
        }
        if (!finalExitTrigger.isTrigger)
        {
            Debug.LogWarning("GameEndingManager: Assigned Final Exit Trigger Collider is not set to 'Is Trigger'. Setting it now.", finalExitTrigger);
            finalExitTrigger.isTrigger = true;
        }
        if (string.IsNullOrEmpty(lightPlayerDoorUnlockID))
        {
            Debug.LogWarning("GameEndingManager: Light Player Door Unlock ID is not set. Cooperative ending condition cannot be checked.", this);
        }
        if (string.IsNullOrEmpty(PlayerTag))
        {
            Debug.LogError("GameEndingManager: Dark Player Tag is not set!", this);
        }

        // Reset state on awake in case of scene reload without disable/enable cycle
        endingTriggered = false;
        lightPlayerDoorWasOpened = false;
    }

    void OnEnable()
    {
        // Subscribe to the event that signifies the Light Player door opening
        // Only subscribe if we have an ID and haven't already triggered the end
        if (!endingTriggered && !string.IsNullOrEmpty(lightPlayerDoorUnlockID))
        {
            EventManager.OnObjectActivate += HandleLightPlayerDoorSignal;
        }
    }

    void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        EventManager.OnObjectActivate -= HandleLightPlayerDoorSignal;
    }

    // Listens for the specific signal
    private void HandleLightPlayerDoorSignal(string receivedID, GameObject source)
    {
        // Ignore if ending already triggered or ID doesn't match
        if (endingTriggered || string.IsNullOrEmpty(lightPlayerDoorUnlockID) || receivedID != lightPlayerDoorUnlockID)
        {
            return;
        }

        Debug.Log($"GameEndingManager: Detected Light Player door signal (ID: {receivedID}). Cooperative ending possible.");
        lightPlayerDoorWasOpened = true;

        // Optional: Unsubscribe now if it only needs to happen once
        // EventManager.OnObjectActivate -= HandleLightPlayerDoorSignal;
    }

    // Detects when the Dark Player enters the final exit trigger
    void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the ending has already been triggered or if it's not the dark player
        if (endingTriggered || !other.CompareTag(PlayerTag))
        {
            return;
        }

        Debug.Log("GameEndingManager: Dark Player entered the final exit trigger.");
        // Prevent the ending from triggering multiple times
        endingTriggered = true;

        // Unsubscribe from door signal now that ending is triggered
        EventManager.OnObjectActivate -= HandleLightPlayerDoorSignal;

        // Check the state of the Light Player's door flag and trigger appropriate ending
        if (lightPlayerDoorWasOpened)
        {
            TriggerCooperativeEnding();
        }
        else
        {
            TriggerSoloEnding();
        }

        // Optional: Disable this component after triggering to prevent further checks
        // this.enabled = false;
    }

    // --- Ending Action Methods ---

    private void TriggerCooperativeEnding()
    {
        Debug.LogWarning("GAME END: Cooperative Ending Triggered!");

        // 1. Invoke any immediate UnityEvents configured for this script
        onCooperativeEnding?.Invoke();

        // 2. Trigger the GLOBAL game ended event via EventManager
        EventManager.TriggerGameEndedStep1(true); // true for cooperative

        // Note: The GameManager will now listen for EventManager.OnGameEnded
        // and call its own TriggerEndingVideo method from there.
        // We no longer call GameManager directly from here.
    }

    private void TriggerSoloEnding()
    {
        Debug.LogWarning("GAME END: Solo Ending Triggered!");

        // 1. Invoke any immediate UnityEvents configured for this script
        onSoloEnding?.Invoke();

        // 2. Trigger the GLOBAL game ended event via EventManager
        EventManager.TriggerGameEndedStep1(false); // false for solo

        // Note: The GameManager will now listen for EventManager.OnGameEnded
        // and call its own TriggerEndingVideo method from there.
        // We no longer call GameManager directly from here.
    }

     // Optional: Reset for manual game reset systems
     // public void ResetEndingState()
     // {
     //     lightPlayerDoorWasOpened = false;
     //     endingTriggered = false;
     //      Debug.Log("GameEndingManager state reset.");
     //     // Re-subscribe on next enable
     //     OnEnable();
     // }
}
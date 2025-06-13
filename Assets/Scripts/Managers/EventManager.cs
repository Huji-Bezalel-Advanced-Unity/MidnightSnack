using System;
using UnityEngine;

public static class EventManager
{
    // --- Interaction Start/Stop Events (Player Focused) ---
    public static event Action<Rigidbody2D> OnRopeInteractStart;
    public static event Action<Rigidbody2D> OnLadderInteractStart;
    public static event Action<Rigidbody2D> OnTrajectoryInteractStart; // Retained if needed
    public static event Action<Rigidbody2D> OnRopeInteractStop;
    public static event Action<Rigidbody2D> OnLadderInteractStop;
    public static event Action<Rigidbody2D> OnTrajectoryInteractStop;
    // -------------------------------------------
    // --- Game State Example ---
    public static int CurrentLevelIndex { get; set; } = 0; // Default to level 0 or 1 as appropriate
    
    // --- Generic Object Activation Events (Button/Lever Focused) ---
    public static event Action<string, GameObject> OnObjectActivate;   // Added GameObject source
    public static event Action<string, GameObject> OnObjectDeactivate; // Added GameObject source
    public static event Action<string> OnObjectToggle;     // Optional: Can keep for things that only toggle without distinct on/off
    
    // Parameter: int for level index that was completed,
    // or string for a specific section ID if more granular.
    // Let's use int for now, matching CurrentLevelIndex.
    public static event Action<int> OnLevelSectionCompleted;

    public static event Action<bool> OnGameEndedStep1;
    public static event Action OnGameEndedStep2;
    public static event Action OnGameEndedFinal;
    public static event Action OnPlayersTeleportedForEndGame; // After players & camera are set
    public static event Action OnGameEndedFinalInputReceived;   // After DarkPlayer presses key
    // OnObjectActivate, etc.
    
    public static event Action<string> OnLoadSceneRequested;

    public static event Action<DarkPlayerController> OnDarkPlayerStuckInLight;
    public static event Action<DarkPlayerController> OnDarkPlayerStuckInLightCamera;
    
    public static event Action<int> OnBarrierOpened; // Parameter is the boundary point index of the opened barrier
    public static event Action<int> OnPlayersPassedBarrier; // Parameter is the boundary point index of the passed barrier
    
    public static event Action<DarkPlayerController> OnShowStuckDecisionUI;
    
    public static void TriggerGameEndedStep1(bool coop) => OnGameEndedStep1?.Invoke(coop);
    public static void TriggerPlayersTeleportedForEndGame() => OnPlayersTeleportedForEndGame?.Invoke();
    public static void TriggerGameEndedFinalInputReceived() => OnGameEndedFinalInputReceived?.Invoke();
    
    public static void TriggerBarrierOpened(int openedBarrierIndex) => OnBarrierOpened?.Invoke(openedBarrierIndex);
    public static void TriggerPlayersPassedBarrier(int passedBarrierIndex) => OnPlayersPassedBarrier?.Invoke(passedBarrierIndex);
    
    public static void TriggerDarkPlayerStuckInLight(DarkPlayerController player)
    {
        if (player == null)
        {
            Debug.LogWarning("EventManager: TriggerDarkPlayerStuckInLight called with null player.");
            return;
        }
        Debug.Log($"EventManager: Dark Player '{player.gameObject.name}' is STUCK in light!");
        OnDarkPlayerStuckInLight?.Invoke(player);
        OnDarkPlayerStuckInLightCamera?.Invoke(player);
    }
    
    public static void TriggerShowStuckDecisionUI(DarkPlayerController player)
    {
        if (player == null)
        {
            Debug.LogWarning("EventManager: TriggerShowStuckDecisionUI called with null player.");
            return;
        }
        Debug.Log($"EventManager: Requesting to show Stuck Decision UI for player {player.gameObject.name}");
        OnShowStuckDecisionUI?.Invoke(player);
    }
    
    public static void TriggerLoadScene(string sceneName)
    {
        Debug.Log($"EventManager: Requesting to load scene: {sceneName}");
        OnLoadSceneRequested?.Invoke(sceneName);
    }

    public static void TriggerOnGameEndedFinal()
    {
        Debug.Log("EventManager: Triggering OnGameEndedFinal.");
        OnGameEndedFinal?.Invoke();
    }
    public static void TriggerGameEndedStep2()
    {
        Debug.Log("EventManager: Triggering Game Ended Step 2.");
        OnGameEndedStep2?.Invoke();
    }
    
    /*public static void TriggerGameEndedStep1(bool wasCooperativeEnding)
    {
        Debug.Log($"EventManager: Triggering Game Ended Event. Cooperative = {wasCooperativeEnding}");
        Debug.Log($"SYSTEM_GAME_END: About to trigger OnGameEndedStep1. GameManager.Instance is {GameManager.Instance?.gameObject.name}. Number of OnGameEndedStep1 listeners: {EventManager.OnGameEndedStep1?.GetInvocationList()?.Length ?? 0}");
        OnGameEndedStep1?.Invoke(wasCooperativeEnding);
    }*/
    
    public static void TriggerLevelSectionCompleted(int completedLevelIndex)
    {
        Debug.Log($"EventManager: Level Section {completedLevelIndex} Completed!");
        OnLevelSectionCompleted?.Invoke(completedLevelIndex);
    }
    
    // --- Method to set the level index (call this when loading a level) ---
    public static void SetCurrentLevel(int levelIndex)
    {
        CurrentLevelIndex = levelIndex;
        Debug.Log($"EventManager: Current Level set to {CurrentLevelIndex}");
        // You might trigger other events here if needed (e.g., OnLevelLoaded)
    }
    
    // --- Methods for Button/Lever/Plate Events (Modified Signature) ---
    public static void TriggerObjectActivate(string objectID, GameObject source) // Added GameObject source
    {
        if (string.IsNullOrEmpty(objectID) || source == null) return;
        // Debug.Log($"EventManager: Triggering Object Activate for ID: {objectID} from Source: {source.name}");
        OnObjectActivate?.Invoke(objectID, source);
    }

    public static void TriggerObjectDeactivate(string objectID, GameObject source) // Added GameObject source
    {
        if (string.IsNullOrEmpty(objectID) || source == null) return;
        // Debug.Log($"EventManager: Triggering Object Deactivate for ID: {objectID} from Source: {source.name}");
        OnObjectDeactivate?.Invoke(objectID, source);
    }

    // -----------

    public static void TriggerObjectToggle(string objectID) // Kept for now, but lever/plate won't use it
    {
        if (string.IsNullOrEmpty(objectID)) return;
        // Debug.Log($"EventManager: Triggering Object Toggle for ID: {objectID}");
        OnObjectToggle?.Invoke(objectID);
    }

    /// <summary>
    /// Triggers the appropriate START interaction event based on the enum type.
    /// </summary>
    /// <param name="interactionType">The enum defining the type of interaction.</param>
    /// <param name="playerRb">The Rigidbody2D of the entity initiating the interaction.</param>
    public static void TriggerInteractionStart(InteractionType interactionType, Rigidbody2D initiatorRb) // Renamed parameter for clarity
    {
        if (initiatorRb == null)
        {
             Debug.LogWarning($"EventManager: TriggerInteractionStart called with null initiatorRb for type '{interactionType}'. Aborting trigger.");
             return;
        }

        switch (interactionType)
        {
            case InteractionType.Rope:
                // Debug.Log($"EventManager: Triggering Rope Interaction START Event for {initiatorRb.name}.");
                OnRopeInteractStart?.Invoke(initiatorRb);
                break;

            case InteractionType.Ladder:
                 // This type might become obsolete if all ladders use TrajectoryMover
                // Debug.Log($"EventManager: Triggering Ladder Interaction START Event for {initiatorRb.name}.");
                OnLadderInteractStart?.Invoke(initiatorRb);
                break;

             // Add Trajectory Start Case if needed
             // case InteractionType.Trajectory:
                 // Debug.Log($"EventManager: Triggering Trajectory Interaction START Event for {initiatorRb.name}.");
                 // OnTrajectoryInteractStart?.Invoke(initiatorRb);
                 // break;

            // Add Button Start Case if needed (Buttons usually don't need Start/Stop events)
             // case InteractionType.Button:
                 // break; // Buttons are often instantaneous

            case InteractionType.None:
                 Debug.LogWarning($"EventManager: TriggerInteractionStart called with InteractionType.None for player {initiatorRb.name}.");
                 break;
            default:
                // This case should ideally not be hit if all enum values are handled
                Debug.LogWarning($"EventManager: Received unhandled interaction START type '{interactionType}' for player {initiatorRb.name}. No event triggered.");
                break;
        }
    }

    /// <summary>
    /// Triggers the appropriate STOP interaction event based on the enum type.
    /// </summary>
    /// <param name="interactionType">The enum defining the type of interaction being stopped.</param>
    /// <param name="playerRb">The Rigidbody2D of the entity initiating the stop.</param>
    public static void TriggerInteractionStop(InteractionType interactionType, Rigidbody2D initiatorRb) // Renamed parameter
    {
         if (initiatorRb == null)
         {
             Debug.LogWarning($"EventManager: TriggerInteractionStop called with null initiatorRb for type '{interactionType}'. Aborting trigger.");
             return;
        }

        switch (interactionType)
        {
            case InteractionType.Rope:
                // Debug.Log($"EventManager: Triggering Rope Interaction STOP Event for {initiatorRb.name}.");
                OnRopeInteractStop?.Invoke(initiatorRb);
                break;

            case InteractionType.Ladder:
                // May become obsolete if Ladders use TrajectoryMover
                // Debug.Log($"EventManager: Triggering Ladder Interaction STOP Event for {initiatorRb.name}.");
                OnLadderInteractStop?.Invoke(initiatorRb);
                break;

            // --- ADDED TRAJECTORY CASE ---
            case InteractionType.Trajectory:
                 // Debug.Log($"EventManager: Triggering Trajectory Interaction STOP Event for {initiatorRb.name}.");
                 OnTrajectoryInteractStop?.Invoke(initiatorRb); // Invoke the specific event
                 break;
            // -----------------------------

            // Buttons typically don't have a "Stop" event
             // case InteractionType.Button:
                 // break;

             case InteractionType.None:
                 // Stopping 'None' doesn't usually make sense
                 // Debug.LogWarning($"EventManager: TriggerInteractionStop called with InteractionType.None for player {initiatorRb.name}.");
                 break;
            default:
                 // This warning should no longer appear for Trajectory
                 Debug.LogWarning($"EventManager: Received unhandled interaction STOP type '{interactionType}' for player {initiatorRb.name}. No event triggered.");
                break;
        }
    }
}
// ProgressiveBarrier.cs (Adapted for NewCameraController)
using UnityEngine;
using System.Collections.Generic;

public class ProgressiveBarrier : MonoBehaviour
{
    public enum BarrierState
    {
        BlockingForward,
        Open,
        WaitingForPlayersToPass,
        BlockingBackward
    }

    [Header("Barrier Identification")]
    [Tooltip("The index of this barrier GameObject in the NewCameraController's sectionBoundaryPoints list.")]
    [SerializeField] private int boundaryPointIndex = -1; // CRITICAL: Set this in the Inspector for each barrier!

    [Header("Unlock Mechanism")]
    [SerializeField] private string unlockSignalID;
    [SerializeField] [Min(1)] private int requiredUnlockSignals = 1;

    [Header("Scene Transition (Optional)")]
    [Tooltip("If this barrier triggers a scene load, specify the scene name here.")]
    [SerializeField] private string sceneToLoadOnUnlock = "";

    // [Header("Legacy Event (Optional)")] // Kept if other systems need it
    // [Tooltip("This barrier represents the end of this specific level index. For legacy OnLevelSectionCompleted event.")]
    // [SerializeField] private int levelIndexThisBarrierCompletes = -1;

    [Header("Player Tracking")]
    [SerializeField] private Transform player1Transform;
    [SerializeField] private Transform player2Transform;

    private Collider2D blockingCollider;
    private BarrierState currentState;
    private int currentUnlockSignalCount = 0;
    private float barrierXPosition;

    void Awake()
    {
        blockingCollider = GetComponent<Collider2D>();
        if (blockingCollider == null) { Debug.LogError($"ProgressiveBarrier '{gameObject.name}': Missing Collider2D.", this); enabled = false; return; }

        if (boundaryPointIndex < 0)
        {
            Debug.LogError($"ProgressiveBarrier '{gameObject.name}': boundaryPointIndex is not set or invalid! This is required for the new camera system.", this);
            // enabled = false; // Consider disabling if this is critical
        }
        if (string.IsNullOrEmpty(unlockSignalID) && requiredUnlockSignals > 0) { Debug.LogWarning($"ProgressiveBarrier '{gameObject.name}': unlockSignalID is empty but requiredUnlockSignals is > 0.", this); }
        if (requiredUnlockSignals < 1) { requiredUnlockSignals = 1; }

        barrierXPosition = transform.position.x;

        if (player1Transform == null || player2Transform == null)
        {
            Debug.LogError($"ProgressiveBarrier '{gameObject.name}': Player Transform references are missing!", this);
            enabled = false; return;
        }

        currentUnlockSignalCount = 0;
        SetState(BarrierState.BlockingForward);
    }

    void OnEnable()
    {
        if (currentState == BarrierState.BlockingForward &&
            !string.IsNullOrEmpty(unlockSignalID) &&
            currentUnlockSignalCount < requiredUnlockSignals)
        {
            EventManager.OnObjectActivate -= HandleUnlockSignal; // Prevent double subscription
            EventManager.OnObjectActivate += HandleUnlockSignal;
        }
    }

    void OnDisable()
    {
        EventManager.OnObjectActivate -= HandleUnlockSignal;
    }

    private void HandleUnlockSignal(string receivedID, GameObject source)
    {
        if (currentState != BarrierState.BlockingForward || currentUnlockSignalCount >= requiredUnlockSignals)
        {
            return;
        }

        if (receivedID == unlockSignalID)
        {
            currentUnlockSignalCount++;
            Debug.Log($"ProgressiveBarrier '{gameObject.name}': Signal {currentUnlockSignalCount}/{requiredUnlockSignals} for ID '{receivedID}'. Source: {source?.name}");

            if (currentUnlockSignalCount >= requiredUnlockSignals)
            {
                Debug.Log($"ProgressiveBarrier '{gameObject.name}': All signals received! Opening...");

                // --- Trigger event for NewCameraController ---
                if (boundaryPointIndex >= 0)
                {
                    EventManager.TriggerBarrierOpened(boundaryPointIndex);
                    Debug.Log($"ProgressiveBarrier '{gameObject.name}': Triggered OnBarrierOpened for boundaryPointIndex: {boundaryPointIndex}");
                }
                else
                {
                    Debug.LogWarning($"ProgressiveBarrier '{gameObject.name}': Cannot trigger OnBarrierOpened, boundaryPointIndex is not set!");
                }

                // --- Legacy Event (Optional: if other game systems rely on OnLevelSectionCompleted) ---
                // if (levelIndexThisBarrierCompletes >= 0)
                // {
                //     Debug.Log($"ProgressiveBarrier '{gameObject.name}': Triggering (legacy) OnLevelSectionCompleted for section index: {levelIndexThisBarrierCompletes}");
                //     EventManager.TriggerLevelSectionCompleted(levelIndexThisBarrierCompletes);
                // }
                // --------------------------------------------------------------------------------------

                if (!string.IsNullOrEmpty(sceneToLoadOnUnlock))
                {
                    Debug.Log($"ProgressiveBarrier '{gameObject.name}': Triggering scene load to '{sceneToLoadOnUnlock}'.");
                    EventManager.TriggerLoadScene(sceneToLoadOnUnlock);
                    // Barrier might not need to physically open if scene changes immediately.
                    // Setting state for robustness if scene load is delayed/fails.
                    SetState(BarrierState.Open);
                }
                else
                {
                    SetState(BarrierState.Open); // Normal physical opening
                }
                
                EventManager.OnObjectActivate -= HandleUnlockSignal; // Unsubscribe after opening
                Debug.Log($"ProgressiveBarrier '{gameObject.name}': Unsubscribed from OnObjectActivate.");
            }
        }
    }

    void Update()
    {
        if (currentState == BarrierState.WaitingForPlayersToPass)
        {
            if (player1Transform == null || player2Transform == null) return; // Should have been caught in Awake

            // Assumes players move left-to-right to pass this barrier.
            // Adjust if passage can be right-to-left for some barriers.
            bool player1Passed = player1Transform.position.x > barrierXPosition + 1f; // Small buffer
            bool player2Passed = player2Transform.position.x > barrierXPosition + 1f; // Small buffer

            if (player1Passed && player2Passed)
            {
                Debug.Log($"ProgressiveBarrier '{gameObject.name}': Both players passed X={barrierXPosition:F2}. Re-locking.");
                SetState(BarrierState.BlockingBackward);

                // --- Trigger event for NewCameraController ---
                if (boundaryPointIndex >= 0)
                {
                    EventManager.TriggerPlayersPassedBarrier(boundaryPointIndex);
                     Debug.Log($"ProgressiveBarrier '{gameObject.name}': Triggered OnPlayersPassedBarrier for boundaryPointIndex: {boundaryPointIndex}");
                }
                else
                {
                     Debug.LogWarning($"ProgressiveBarrier '{gameObject.name}': Cannot trigger OnPlayersPassedBarrier, boundaryPointIndex is not set!");
                }
                // -------------------------------------------
            }
        }
    }

    private void SetState(BarrierState newState)
    {
        BarrierState oldState = currentState;
        currentState = newState;
        // Debug.Log($"ProgressiveBarrier '{gameObject.name}': SetState: {oldState} -> {newState}. Collider Enabled: {blockingCollider?.enabled}");

        switch (currentState)
        {
            case BarrierState.BlockingForward:
                if (blockingCollider != null)
                {
                    blockingCollider.enabled = true;
                    blockingCollider.isTrigger = false; 
                }
                break;

            case BarrierState.Open:
                if (blockingCollider != null)
                {
                    blockingCollider.enabled = false;
                }
                // Automatically transition to waiting for players to pass
                SetState(BarrierState.WaitingForPlayersToPass); 
                break;

            case BarrierState.WaitingForPlayersToPass:
                // Collider remains disabled (from Open state).
                // No change needed to collider here.
                break;

            case BarrierState.BlockingBackward:
                if (blockingCollider != null)
                {
                    blockingCollider.enabled = true;
                    blockingCollider.isTrigger = false;
                }
                break;
        }
        if (oldState != currentState) { // Log only if state actually changed
             Debug.Log($"ProgressiveBarrier '{gameObject.name}': State changed to {currentState}. Collider Enabled: {blockingCollider?.enabled}");
        }
    }

    public void ResetBarrier() // Call this to reset the barrier to its initial state
    {
        Debug.Log($"ProgressiveBarrier '{gameObject.name}': ResetBarrier called.");
        currentUnlockSignalCount = 0;
        SetState(BarrierState.BlockingForward);

        // Re-subscribe if it was in a state to listen
        if (this.enabled && gameObject.activeInHierarchy) {
            OnEnable();
        }
    }
}
// MovingWalls.cs
using UnityEngine;
using System.Collections; // Needed if you add smooth movement coroutines later

/// <summary>
/// Controls a pair of walls (left and right) moving towards each other.
/// Supports two modes: Incremental steps triggered externally, or
/// continuous movement over a set time.
/// </summary>
public class MovingWalls : MonoBehaviour
{
    public enum WallMovementMode
    {
        Incremental,    // Moves a fixed distance per step command
        TimedContinuous // Moves smoothly from start to end over a set duration
    }

    [Header("References")]
    [SerializeField] private Transform leftWallTransform;
    [SerializeField] private Transform rightWallTransform;

    [Header("Movement Settings")]
    [SerializeField] private WallMovementMode movementMode = WallMovementMode.Incremental;
    [SerializeField]
    [Tooltip("Distance each wall moves towards the center per MoveStep() call (Incremental mode only).")]
    private float stepDistance = 0.5f;
    [SerializeField]
    [Tooltip("Total time in seconds for walls to move from initial to fully closed position (Timed Continuous mode only).")]
    private float totalTimeToClose = 5.0f;
    [SerializeField]
    [Tooltip("How much space (in X units) remains between walls when fully closed.")]
    private float closedOffset = 0.1f;

    [Header("Initial State & Activation")]
    [SerializeField]
    [Tooltip("If true, walls start in the 'closed' position.")]
    private bool startClosed = false;
    [SerializeField]
    [Tooltip("If true and mode is TimedContinuous, movement starts automatically on Awake/Start.")]
    private bool activateOnStart = false;

    // --- Private State ---
    private Vector3 initialLeftPos;
    private Vector3 initialRightPos;
    private Vector3 targetLeftClosedPos;
    private Vector3 targetRightClosedPos;

    // For Incremental mode
    private float currentLeftX;
    private float currentRightX;

    // For Timed mode
    private float timer = 0f;
    private bool isMoving = false; // Tracks if timed movement is active

    // Shared state
    private bool isFullyClosed = false;
    private bool referencesValid = false; // To prevent errors if references are missing

    void Awake()
    {
        // --- Validate References ---
        if (leftWallTransform == null || rightWallTransform == null)
        {
            Debug.LogError($"MovingWalls on {gameObject.name}: Left or Right Wall Transform not assigned!", this);
            referencesValid = false;
            enabled = false; // Disable script if setup is broken
            return;
        }
        referencesValid = true;

        // --- Store Initial Positions ---
        initialLeftPos = leftWallTransform.position;
        initialRightPos = rightWallTransform.position;

        // --- Calculate Target Closed Positions ---
        // Assume walls move only on X axis relative to their initial Y/Z
        float centerX = (initialLeftPos.x + initialRightPos.x) / 2.0f;
        float targetLeftClosedX = centerX - (closedOffset / 2.0f);
        float targetRightClosedX = centerX + (closedOffset / 2.0f);

        targetLeftClosedPos = new Vector3(targetLeftClosedX, initialLeftPos.y, initialLeftPos.z);
        targetRightClosedPos = new Vector3(targetRightClosedX, initialRightPos.y, initialRightPos.z);

        // --- Initialize Current Positions for Incremental ---
        currentLeftX = initialLeftPos.x;
        currentRightX = initialRightPos.x;

        // --- Handle 'Start Closed' ---
        if (startClosed)
        {
            ForceCloseWalls(); // Move immediately to closed state
        }
        else
        {
            isFullyClosed = false; // Ensure flag is correct if starting open
        }
    }

    void Start()
    {
        // Activate timed movement on start if configured
        if (referencesValid && movementMode == WallMovementMode.TimedContinuous && activateOnStart && !isFullyClosed)
        {
            StartTimedClosure();
        }
    }

    void Update()
    {
        // --- Handle Timed Continuous Movement ---
        if (!isMoving || isFullyClosed || movementMode != WallMovementMode.TimedContinuous || !referencesValid)
        {
            return; // Only proceed if actively moving in timed mode
        }

        timer += Time.deltaTime;
        float progress = Mathf.Clamp01(timer / totalTimeToClose);

        // Interpolate positions
        leftWallTransform.position = Vector3.Lerp(initialLeftPos, targetLeftClosedPos, progress);
        rightWallTransform.position = Vector3.Lerp(initialRightPos, targetRightClosedPos, progress);

        // Check for completion
        if (progress >= 1.0f)
        {
            isFullyClosed = true;
            isMoving = false;
            Debug.Log($"MovingWalls '{gameObject.name}': Timed closure complete.");
            // Ensure exact final position
            leftWallTransform.position = targetLeftClosedPos;
            rightWallTransform.position = targetRightClosedPos;
        }
    }

    /// <summary>
    /// Moves the walls one step closer in Incremental mode.
    /// Intended to be called externally (e.g., by a button's UnityEvent).
    /// </summary>
    public void MoveStep()
    {
        if (movementMode != WallMovementMode.Incremental || isFullyClosed || !referencesValid)
        {
            if(movementMode != WallMovementMode.Incremental) Debug.LogWarning($"MovingWalls '{gameObject.name}': MoveStep() called but mode is not Incremental.", this);
            return;
        }

        Debug.Log($"MovingWalls '{gameObject.name}': MoveStep() called.");

        // Calculate potential next positions
        float nextLeftX = currentLeftX + stepDistance;
        float nextRightX = currentRightX - stepDistance;

        // Clamp positions so they don't overshoot the target closed position
        nextLeftX = Mathf.Min(nextLeftX, targetLeftClosedPos.x); // Left wall shouldn't go past target X
        nextRightX = Mathf.Max(nextRightX, targetRightClosedPos.x); // Right wall shouldn't go past target X

        // --- Apply Movement (Instantaneous) ---
        // For smooth step movement, you'd use Lerp/MoveTowards in Update or a Coroutine
        leftWallTransform.position = new Vector3(nextLeftX, initialLeftPos.y, initialLeftPos.z);
        rightWallTransform.position = new Vector3(nextRightX, initialRightPos.y, initialRightPos.z);

        // Update current positions
        currentLeftX = nextLeftX;
        currentRightX = nextRightX;

        // Check if fully closed after this step
        // Use a small tolerance (epsilon) for float comparison
        if (Mathf.Abs(currentLeftX - targetLeftClosedPos.x) < Mathf.Epsilon &&
            Mathf.Abs(currentRightX - targetRightClosedPos.x) < Mathf.Epsilon)
        {
            isFullyClosed = true;
             Debug.Log($"MovingWalls '{gameObject.name}': Incremental closure complete.");
        }
    }

    /// <summary>
    /// Starts the continuous closing process in TimedContinuous mode.
    /// Does nothing if already moving, closed, or not in Timed mode.
    /// </summary>
    public void StartTimedClosure()
    {
        if (movementMode != WallMovementMode.TimedContinuous || isMoving || isFullyClosed || !referencesValid)
        {
             if(movementMode != WallMovementMode.TimedContinuous) Debug.LogWarning($"MovingWalls '{gameObject.name}': StartTimedClosure() called but mode is not TimedContinuous.", this);
             return;
        }

        Debug.Log($"MovingWalls '{gameObject.name}': Starting timed closure ({totalTimeToClose} seconds).");
        timer = 0f; // Reset timer
        isMoving = true; // Activate movement in Update
    }

    /// <summary>
    /// Resets the walls to their initial open positions and stops any movement.
    /// </summary>
    public void ResetWalls()
    {
        if (!referencesValid) return;

        Debug.Log($"MovingWalls '{gameObject.name}': Resetting walls.");
        leftWallTransform.position = initialLeftPos;
        rightWallTransform.position = initialRightPos;

        // Reset state variables
        currentLeftX = initialLeftPos.x;
        currentRightX = initialRightPos.x;
        isFullyClosed = false;
        isMoving = false;
        timer = 0f;

         // Re-activate if configured to start automatically and isn't starting closed
        if (!startClosed && movementMode == WallMovementMode.TimedContinuous && activateOnStart)
        {
            StartTimedClosure();
        }
    }

    /// <summary>
    /// Internal helper to force walls to the closed state immediately.
    /// </summary>
    private void ForceCloseWalls()
    {
        if (!referencesValid) return;

        leftWallTransform.position = targetLeftClosedPos;
        rightWallTransform.position = targetRightClosedPos;
        currentLeftX = targetLeftClosedPos.x;
        currentRightX = targetRightClosedPos.x;
        isFullyClosed = true;
        isMoving = false; // Ensure not moving if starting closed
        timer = totalTimeToClose; // Set timer to max if timed mode
        Debug.Log($"MovingWalls '{gameObject.name}': Set to start closed.");
    }

    // Optional: Gizmos to visualize target positions in editor
    void OnDrawGizmosSelected()
    {
        if (!referencesValid || !Application.isPlaying) // Calculate targets only if possible/needed
        {
            // Attempt to calculate for editor view (might be inaccurate if positions change before play)
            if (leftWallTransform == null || rightWallTransform == null) return;
            Vector3 currentInitialLeft = leftWallTransform.position;
            Vector3 currentInitialRight = rightWallTransform.position;
            float currentCenterX = (currentInitialLeft.x + currentInitialRight.x) / 2.0f;
            Vector3 currentTargetLeft = new Vector3(currentCenterX - (closedOffset / 2.0f), currentInitialLeft.y, currentInitialLeft.z);
            Vector3 currentTargetRight = new Vector3(currentCenterX + (closedOffset / 2.0f), currentInitialRight.y, currentInitialRight.z);

             Gizmos.color = Color.red;
             Gizmos.DrawWireSphere(currentTargetLeft, 0.2f);
             Gizmos.DrawWireSphere(currentTargetRight, 0.2f);
             Gizmos.DrawLine(currentTargetLeft, currentTargetRight);
        }
        else // Use calculated values during play mode
        {
             Gizmos.color = Color.red;
             Gizmos.DrawWireSphere(targetLeftClosedPos, 0.2f);
             Gizmos.DrawWireSphere(targetRightClosedPos, 0.2f);
             Gizmos.DrawLine(targetLeftClosedPos, targetRightClosedPos);
        }
    }
}
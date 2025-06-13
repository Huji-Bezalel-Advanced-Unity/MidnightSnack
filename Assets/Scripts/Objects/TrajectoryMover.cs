using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Collider2D))] // Needed for detection by PlayerInteraction
public class TrajectoryMover : MonoBehaviour, IInteractable // Implements IInteractable directly
{
    [Header("Path Definition")]
    [SerializeField] private Transform pathStart;
    [SerializeField] private Transform pathEnd;
    // [SerializeField] private PathType pathShape = PathType.Line; // For future expansion

    [Header("Movement Control")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField]
    [Tooltip("Which input axis controls movement along the path? ClosestToPath determines automatically based on path direction.")]
    private MovementAxis controlAxisPreference = MovementAxis.ClosestToPath;
    [SerializeField]
    [Tooltip("If true, only one entity can use this path at a time.")]
    private bool singleOccupancy = false; // Default to allowing multiple users

    // --- Per-User Data Structure ---
    private struct TrajectoryUserData
    {
        public float initialGravity;
        public IMovementInputProvider inputProvider;
        public float currentProgress; // Normalized position (0 to 1)
        public MovementAxis actualControlAxis; // The axis actually used after calculation
    }

    // --- Internal State ---
    private Dictionary<Rigidbody2D, TrajectoryUserData> activeUsers = new Dictionary<Rigidbody2D, TrajectoryUserData>();
    private Collider2D moverCollider;
    private Vector3 pathVector;
    private float pathLength; // Keep the private field
    private MovementAxis calculatedControlAxis = MovementAxis.Vertical;
    private bool setupValid = false;

    // --- Public Properties ---
    public Transform PathStart => pathStart;
    public Transform PathEnd => pathEnd;
    public float PathLength => pathLength; // *** ADD THIS LINE ***
    public float MoveSpeed => moveSpeed; // *** ADD THIS LINE (Needed by ClimbingState) ***
    public MovementAxis GetCalculatedAxis() {
        // Ensure calculation runs if called before Awake somehow (e.g. editor)
        if (!setupValid && pathStart != null && pathEnd != null) {
            pathVector = pathEnd.position - pathStart.position;
            if (controlAxisPreference == MovementAxis.ClosestToPath) {
                Vector2 pathDir2D = new Vector2(pathVector.x, pathVector.y).normalized;
                float dotUp = Mathf.Abs(Vector2.Dot(pathDir2D, Vector2.up));
                float dotRight = Mathf.Abs(Vector2.Dot(pathDir2D, Vector2.right));
                calculatedControlAxis = (dotUp >= dotRight) ? MovementAxis.Vertical : MovementAxis.Horizontal;
            } else {
                calculatedControlAxis = controlAxisPreference;
            }
        }
        return calculatedControlAxis;
    }
    
    // --- IInteractable Implementation ---
    public InteractionType InteractionType => InteractionType.Trajectory;

    public void Interact(GameObject initiatorGameObject)
    {
        if (!setupValid) {
             return;
        }

        Rigidbody2D userRb = initiatorGameObject.GetComponent<Rigidbody2D>();
        IMovementInputProvider inputProvider = initiatorGameObject.GetComponent<IMovementInputProvider>();

        if (userRb == null || inputProvider == null) {
            return;
        }
        if (activeUsers.ContainsKey(userRb)) {
             return; // Already using
        }
        if (singleOccupancy && activeUsers.Count > 0) {
            return; // Path busy
        }

        StartMovingFor(userRb, inputProvider);
    }
    // --- End IInteractable Implementation ---

    void Awake()
    {
        moverCollider = GetComponent<Collider2D>();
        if (moverCollider == null) { enabled = false; return; }
        if (!moverCollider.isTrigger) { /*Debug.LogWarning($"TrajectoryMover on {gameObject.name}: Collider2D should be a trigger for detection. Forcing Is Trigger = TRUE.", this); */ moverCollider.isTrigger = true; }

        if (pathStart == null || pathEnd == null) {
            setupValid = false;
            return;
        }

        pathVector = pathEnd.position - pathStart.position;
        pathLength = pathVector.magnitude;

        if (pathLength < Mathf.Epsilon) {
            setupValid = false;
            return;
        }

        if (controlAxisPreference == MovementAxis.ClosestToPath) {
            Vector2 pathDir2D = new Vector2(pathVector.x, pathVector.y).normalized;
            float dotUp = Mathf.Abs(Vector2.Dot(pathDir2D, Vector2.up));
            float dotRight = Mathf.Abs(Vector2.Dot(pathDir2D, Vector2.right));
            calculatedControlAxis = (dotUp >= dotRight) ? MovementAxis.Vertical : MovementAxis.Horizontal;
        } else {
            calculatedControlAxis = controlAxisPreference;
        }

        setupValid = true;
    }

    // --- MODIFIED OnEnable/OnDisable ---
    void OnEnable()
    {
        // Subscribe to the specific stop event for Trajectory interactions
        EventManager.OnTrajectoryInteractStop += HandleStopInteraction;
    }

    void OnDisable()
    {
        // Unsubscribe from the specific stop event
        EventManager.OnTrajectoryInteractStop -= HandleStopInteraction;

        // Clean up all active users
        // Using ToList() is essential here because StopMovingFor modifies the collection
        List<Rigidbody2D> usersToStop = activeUsers.Keys.ToList();
        foreach (Rigidbody2D userRb in usersToStop) {
            ForceStopController(userRb); // Notify initiator's controller FIRST
            StopMovingFor(userRb);      // Clean up this mover's state for the user (handles removal)
        }
        // Just in case something went wrong with StopMovingFor's removal
        activeUsers.Clear();
    }
    // --- END MODIFIED OnEnable/OnDisable ---


    // --- ADDED Handler for Stop Event ---
    /// <summary>
    /// Handles the specific stop event from the EventManager, initiated by PlayerInteraction or NPC.
    /// </summary>
    private void HandleStopInteraction(Rigidbody2D triggeringRb)
    {
        // Check if the Rigidbody triggering the event is currently using THIS TrajectoryMover
        if (activeUsers.ContainsKey(triggeringRb))
        {
            // Initiator's controller (e.g., PlayerInteraction) already reset its state.
            // We just need to clean up this mover's state for that user.
            StopMovingFor(triggeringRb);
        }
        // Else: The stop event was for a different trajectory object or type, ignore it.
    }
    // --- END ADDED Handler ---


    void Update()
    {
        if (!setupValid || activeUsers.Count == 0) return;

        List<Rigidbody2D> currentKeys = activeUsers.Keys.ToList();
        List<Rigidbody2D> keysToRemoveThisFrame = null;

        foreach (Rigidbody2D userRb in currentKeys)
        {
            if (!activeUsers.TryGetValue(userRb, out TrajectoryUserData userData)) { continue; } // Already removed
            if (userRb == null || userData.inputProvider == null) { /* Mark for removal */ continue; }

            Vector2 input = userData.inputProvider.GetMovementInput();
            float relevantInput = (userData.actualControlAxis == MovementAxis.Vertical) ? input.y : input.x;
            float deltaProgress = (pathLength > 0) ? (relevantInput * moveSpeed * Time.deltaTime / pathLength) : 0;
            // float oldProgress = userData.currentProgress; // Keep for verbose logging if needed
            userData.currentProgress = Mathf.Clamp01(userData.currentProgress + deltaProgress);
            Vector3 newPos = Vector3.Lerp(pathStart.position, pathEnd.position, userData.currentProgress);

            // --- Only move if STILL active (safety check against race conditions) ---
            if (activeUsers.ContainsKey(userRb)) // Check again right before moving
            {
                userRb.MovePosition(newPos);
                activeUsers[userRb] = userData; // Write back struct only if moved
            } else {
                 continue; // Skip completion check if removed mid-loop
            }


            // --- Enhanced Check for Completion ---
            bool completed = false;
            if (userData.currentProgress >= 1.0f || userData.currentProgress <= 0.0f) { completed = true; }
            else if (Mathf.Approximately(relevantInput, 0f))
            {
                float nearEndThreshold = 0.98f; float nearStartThreshold = 0.02f;
                if (userData.currentProgress >= nearEndThreshold || userData.currentProgress <= nearStartThreshold)
                {
                    completed = true;
                    // Optional snap
                    Vector3 finalPos = (userData.currentProgress >= nearEndThreshold) ? pathEnd.position : pathStart.position;
                    userRb.MovePosition(finalPos);
                    userData.currentProgress = (userData.currentProgress >= nearEndThreshold) ? 1.0f : 0.0f;
                    activeUsers[userRb] = userData;
                }
            }

            if (completed)
            {
                if (keysToRemoveThisFrame == null) keysToRemoveThisFrame = new List<Rigidbody2D>();
                if (!keysToRemoveThisFrame.Contains(userRb)) { keysToRemoveThisFrame.Add(userRb); }
            }
        }

        // --- Process Removals ---
        if (keysToRemoveThisFrame != null)
        {
            foreach (Rigidbody2D keyToRemove in keysToRemoveThisFrame)
            {
                if (activeUsers.ContainsKey(keyToRemove))
                {
                    ForceStopController(keyToRemove); // Notify controller first
                    StopMovingFor(keyToRemove);      // Then clean up this script's state
                }
            }
        }
    }


    void StartMovingFor(Rigidbody2D userRb, IMovementInputProvider inputProvider)
    {
        if (userRb == null || inputProvider == null || activeUsers.ContainsKey(userRb)) return;

        TrajectoryUserData data = new TrajectoryUserData();
        data.initialGravity = userRb.gravityScale; // Store gravity BEFORE changing it
        data.inputProvider = inputProvider;
        data.actualControlAxis = this.calculatedControlAxis;

        Vector3 userPos = userRb.position;
        Vector3 projectedPoint = ProjectPointOnLineSegment(pathStart.position, pathEnd.position, userPos);
        data.currentProgress = (pathLength > 0) ? Vector3.Distance(pathStart.position, projectedPoint) / pathLength : 0f;
        data.currentProgress = Mathf.Clamp01(data.currentProgress);

        activeUsers.Add(userRb, data); // Add BEFORE changing physics state

        userRb.gravityScale = 0f; // Set gravity to 0 AFTER storing initial
        userRb.linearVelocity = Vector2.zero;
        Vector3 startPosOnPath = Vector3.Lerp(pathStart.position, pathEnd.position, data.currentProgress);
        // Use MovePosition for initial snap to avoid physics weirdness
        userRb.MovePosition(startPosOnPath);
        // userRb.transform.position = startPosOnPath; // Less safe with physics
    }


    void StopMovingFor(Rigidbody2D userRb)
    {
        if (userRb != null && activeUsers.TryGetValue(userRb, out TrajectoryUserData data))
        {
            // Restore gravity if it was near zero (safety check)
            if (Mathf.Approximately(userRb.gravityScale, 0f)) { userRb.gravityScale = data.initialGravity; }
            bool removed = activeUsers.Remove(userRb); // Remove from dictionary
        }
        else { Debug.LogWarning($"TrajectoryMover '{gameObject.name}': StopMovingFor called for {userRb?.name}, but they were not found in activeUsers."); }
    }


    public Vector3 ProjectPointOnLineSegment(Vector3 lineStart, Vector3 lineEnd, Vector3 point)
    {
        Vector3 lineDirection = lineEnd - lineStart;
        float lineLengthSqr = lineDirection.sqrMagnitude;
        if (lineLengthSqr < Mathf.Epsilon) return lineStart;
        float t = Vector3.Dot(point - lineStart, lineDirection) / lineLengthSqr;
        t = Mathf.Clamp01(t);
        return lineStart + lineDirection * t;
    }


    /// <summary>
    /// Finds the appropriate controller (PlayerInteraction, NPCMovement, etc.)
    /// on the user's GameObject and calls its specific ForceStopInteraction method.
    /// </summary>
    private void ForceStopController(Rigidbody2D userRb) {
        if (userRb == null) return;

        LightPlayerController lpc = userRb.GetComponent<LightPlayerController>();
        if (lpc != null) { lpc.ForceStopInteraction(); return; }

        DarkPlayerController dpc = userRb.GetComponent<DarkPlayerController>();
        if (dpc != null) { dpc.ForceStopInteraction(); return; }

        NPCMovement npc = userRb.GetComponent<NPCMovement>();
        if (npc != null) { npc.ForceStopInteraction(); return; }

        Debug.LogWarning($"TrajectoryMover ({gameObject.name}): ForceStopController called for {userRb.name}, but couldn't find a recognized controller.", userRb.gameObject);
    }

    void OnTriggerExit2D(Collider2D other) {
        Rigidbody2D userRb = other.attachedRigidbody;
        if (userRb != null && activeUsers.ContainsKey(userRb)) {
            ForceStopController(userRb);
            StopMovingFor(userRb);
        }
    }

    void OnDrawGizmosSelected() {
        if (pathStart != null && pathEnd != null) {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(pathStart.position, pathEnd.position);
            Gizmos.DrawWireSphere(pathStart.position, 0.15f);
            Gizmos.DrawWireSphere(pathEnd.position, 0.15f);

             if(Application.isPlaying && setupValid) {
                Gizmos.color = Color.yellow;
                Vector3 midPoint = (pathStart.position + pathEnd.position) / 2f;
                Vector3 axisDir = (calculatedControlAxis == MovementAxis.Vertical) ? Vector3.up * 0.5f : Vector3.right * 0.5f;
                Gizmos.DrawLine(midPoint - axisDir, midPoint + axisDir);
             }
             else if (!Application.isPlaying) // Estimate in editor
             {
                 MovementAxis editorAxis = controlAxisPreference;
                 if(editorAxis == MovementAxis.ClosestToPath) {
                     Vector3 editorPathVec = pathEnd.position - pathStart.position;
                     Vector2 pathDir2D = new Vector2(editorPathVec.x, editorPathVec.y).normalized;
                     editorAxis = (Mathf.Abs(Vector2.Dot(pathDir2D, Vector2.up)) >= Mathf.Abs(Vector2.Dot(pathDir2D, Vector2.right))) ? MovementAxis.Vertical : MovementAxis.Horizontal;
                 }
                 Gizmos.color = Color.yellow;
                 Vector3 midPoint = (pathStart.position + pathEnd.position) / 2f;
                 Vector3 axisDir = (editorAxis == MovementAxis.Vertical) ? Vector3.up * 0.5f : Vector3.right * 0.5f;
                 Gizmos.DrawLine(midPoint - axisDir, midPoint + axisDir);
             }
        }
    }
}
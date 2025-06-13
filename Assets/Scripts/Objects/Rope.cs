using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Collider2D))]
public class Rope : MonoBehaviour, IInteractable
{
    
    public Transform RideStartPoint => rideStartPoint;
    public Transform RideEndPoint => rideEndPoint;
    
    [Header("Required Setup")]
    [SerializeField] private Transform rideStartPoint;
    [SerializeField] private Transform rideEndPoint;
    [SerializeField] private float rideSpeed = 2f;
    [Header("Behavior")]
    [SerializeField] private bool alwaysMoveStartToEnd = false;

    private struct RopeUserData
    {
        public float initialGravity;
        public float rideProgress;
        public Vector3 startLerpBase;
        public Vector3 endLerpBase;
    }

    private Dictionary<Rigidbody2D, RopeUserData> activeRiders = new Dictionary<Rigidbody2D, RopeUserData>();
    private Collider2D ropeCollider;

    // --- IInteractable Implementation ---
    public InteractionType InteractionType => InteractionType.Rope;

    public void Interact(GameObject initiatorGameObject)
    {
        Rigidbody2D userRb = initiatorGameObject.GetComponent<Rigidbody2D>();
        if (userRb == null)
        {
            //Debug.LogWarning($"Rope '{gameObject.name}': Interact called by {initiatorGameObject.name}, but it has no Rigidbody2D.", initiatorGameObject);
            return;
        }
        if (activeRiders.ContainsKey(userRb))
        {
            //Debug.Log($"Rope '{gameObject.name}': {userRb.name} is already riding.");
            return;
        }
        // Optional single occupancy check could go here
        //Debug.Log($"Rope '{gameObject.name}': Interact called by {userRb.name}. Conditions MET. Calling StartRidingFor().");
        StartRidingFor(userRb);
    }
    // --- End IInteractable Implementation ---

    void Awake()
    {
        ropeCollider = GetComponent<Collider2D>();
        if (ropeCollider == null) { Debug.LogError($"Rope {gameObject.name} needs Collider2D", this); enabled = false; return; }
        if (!ropeCollider.isTrigger) { /*Debug.LogWarning($"Rope {gameObject.name} Collider2D should be a trigger. Forcing Is Trigger = TRUE.", this);*/ ropeCollider.isTrigger = true; }
        if (rideStartPoint == null) { Debug.LogError($"Rope {gameObject.name} needs Ride Start Point assigned.", this); enabled = false; return; }
        if (rideEndPoint == null) { Debug.LogError($"Rope {gameObject.name} needs Ride End Point assigned.", this); enabled = false; return; }
    }

    void OnEnable()
    {
        EventManager.OnRopeInteractStop += HandleStopInteraction;
    }

    void OnDisable()
    {
        EventManager.OnRopeInteractStop -= HandleStopInteraction;
        List<Rigidbody2D> ridersToStop = activeRiders.Keys.ToList();
        foreach (Rigidbody2D riderRb in ridersToStop)
        {
            //Debug.Log($"Rope '{gameObject.name}' disabled. Forcing stop for rider {riderRb.name}.");
            ForceStopController(riderRb);
            StopRidingFor(riderRb, false); // Handles removal
        }
        activeRiders.Clear();
    }

    private void HandleStopInteraction(Rigidbody2D triggeringRb)
    {
        if (activeRiders.ContainsKey(triggeringRb))
        {
            //Debug.Log($"Rope '{gameObject.name}': HandleStopInteraction received for {triggeringRb.name}. Calling StopRidingFor().");
            StopRidingFor(triggeringRb, completed: false);
        }
    }

    // --- REVISED Update Method (Similar to TrajectoryMover's fix) ---
    void Update()
    {
        if (activeRiders.Count == 0) return;

        List<Rigidbody2D> keysToRemove = null; // Store keys for invalid riders
        List<Rigidbody2D> keysToComplete = null; // Store keys for riders who finish

        // Iterate over a COPY of the keys for safety
        foreach (Rigidbody2D riderRb in activeRiders.Keys.ToList())
        {
            // Re-check if rider still exists (could be removed by OnDisable/OnTriggerExit)
            if (!activeRiders.TryGetValue(riderRb, out RopeUserData userData))
            {
                continue; // Skip if removed concurrently
            }

            // Check for invalid Rigidbody (e.g., destroyed)
            if (riderRb == null) {
                if (keysToRemove == null) keysToRemove = new List<Rigidbody2D>();
                // We don't have the key if riderRb is null, but TryGetValue succeeded? This indicates a dictionary issue.
                // Best practice is to remove based on finding null values when iterating KVP, or handle this specific edge case if needed.
                // For now, we assume if TryGetValue worked, riderRb was valid at that instant.
                // If it becomes null later in the frame before removal, StopRidingFor handles null checks.
                continue;
            }

            // --- Perform Movement Calculation ---
            float totalDistance = Vector3.Distance(userData.startLerpBase, userData.endLerpBase);
            float step = (totalDistance > 0.01f) ? (rideSpeed / totalDistance) * Time.deltaTime : 1.0f;
            userData.rideProgress += step;
            userData.rideProgress = Mathf.Clamp01(userData.rideProgress);

            // --- Apply Movement ---
            Vector2 newPos = Vector2.Lerp(userData.startLerpBase, userData.endLerpBase, userData.rideProgress);
            riderRb.MovePosition(newPos);

            // --- Write updated progress back to the dictionary ---
            activeRiders[riderRb] = userData;

            // --- Check for Completion ---
            // Read the progress we just wrote back
            if (activeRiders[riderRb].rideProgress >= 1.0f)
            {
                 if (keysToComplete == null) keysToComplete = new List<Rigidbody2D>();
                 if (!keysToComplete.Contains(riderRb)) // Avoid duplicates
                 {
                     // Debug.Log($"Rope: Marking {riderRb.name} for completion.");
                     keysToComplete.Add(riderRb);
                 }
            }
        }

        // --- Process Completions AFTER the loop ---
        if (keysToComplete != null)
        {
            foreach (Rigidbody2D riderToComplete in keysToComplete)
            {
                // Check if still exists before completing
                if (activeRiders.ContainsKey(riderToComplete))
                {
                     // Debug.Log($"Rope: Processing completion for {riderToComplete.name}.");
                     CompleteRopeRideFor(riderToComplete); // This calls StopRidingFor internally
                }
            }
        }

        // --- Process Removals for Invalid Riders AFTER the loop (if collected) ---
        if (keysToRemove != null) {
            foreach(var key in keysToRemove) {
                 if (activeRiders.ContainsKey(key))
                 {
                      // Debug.LogWarning($"Rope '{gameObject.name}': Removing invalid rider entry {key?.name}.");
                      StopRidingFor(key, false); // Force stop, not completed
                 }
            }
        }
    }
    // --- END REVISED Update Method ---


    // MoveRider method removed/integrated into Update


    public Vector3 GetClosestPointOnSegment(Vector3 segmentStart, Vector3 segmentEnd, Vector3 point, out float progress)
    {
        Vector3 segmentDirection = segmentEnd - segmentStart;
        float segmentLengthSqr = segmentDirection.sqrMagnitude;
        if (segmentLengthSqr < Mathf.Epsilon) { progress = 0f; return segmentStart; }
        float t = Vector3.Dot(point - segmentStart, segmentDirection) / segmentLengthSqr;
        progress = Mathf.Clamp01(t);
        return segmentStart + segmentDirection * t;
    }


    void StartRidingFor(Rigidbody2D userRb)
    {
        if (userRb == null || rideStartPoint == null || rideEndPoint == null) return;
        if (activeRiders.ContainsKey(userRb)) return;

        Vector3 userPosAtInteraction = userRb.position;
        Vector3 pathStart = rideStartPoint.position;
        Vector3 pathEnd = rideEndPoint.position;
        float initialProgress;
        Vector3 snapToPoint = GetClosestPointOnSegment(pathStart, pathEnd, userPosAtInteraction, out initialProgress);

        RopeUserData userData = new RopeUserData();
        userData.initialGravity = userRb.gravityScale;
        userData.rideProgress = initialProgress;

        if (alwaysMoveStartToEnd) {
             userData.startLerpBase = pathStart; userData.endLerpBase = pathEnd;
        }
        else {
            float distToStart = Vector3.Distance(userPosAtInteraction, pathStart);
            float distToEnd = Vector3.Distance(userPosAtInteraction, pathEnd);
            if (distToStart <= distToEnd) { userData.startLerpBase = pathStart; userData.endLerpBase = pathEnd; }
            else { userData.startLerpBase = pathEnd; userData.endLerpBase = pathStart; userData.rideProgress = 1.0f - initialProgress; }
        }

        activeRiders.Add(userRb, userData);

        userRb.gravityScale = 0f;
        userRb.linearVelocity = Vector2.zero;
        userRb.transform.position = snapToPoint;

        // Debug.Log($"Rope '{gameObject.name}': {userRb.name} started riding.");
    }


    void StopRidingFor(Rigidbody2D userRb, bool completed)
    {
        // Ensure Rigidbody reference is valid before proceeding
        if (userRb == null) {
             // Debug.LogWarning($"Rope '{gameObject.name}': Attempted to stop a null Rigidbody.");
             // If needed, could iterate dictionary to find entry matching null if that's possible
             return;
        }

        // Try to get the user data and remove in one go if successful
        if (activeRiders.TryGetValue(userRb, out RopeUserData userData))
        {
             // Remove first to prevent potential issues if restoring gravity causes physics updates
            activeRiders.Remove(userRb);

            // Restore physics
            if (Mathf.Approximately(userRb.gravityScale, 0f))
            {
                userRb.gravityScale = userData.initialGravity;
            }

            // Debug.Log($"Rope '{gameObject.name}': {userRb.name} stopped riding. Completed: {completed}. Remaining riders: {activeRiders.Count}");
        }
         // else { Debug.LogWarning($"Rope '{gameObject.name}': Tried to stop {userRb.name}, but they were not in activeRiders.", userRb); }
    }


    void CompleteRopeRideFor(Rigidbody2D userRb)
    {
        // Check if userRb is valid and still considered riding before proceeding
         if (userRb != null && activeRiders.ContainsKey(userRb))
         {
            // Debug.Log($"Rope '{gameObject.name}': {userRb.name} attempting completion process.");
            ForceStopController(userRb); // Notify controller FIRST
            StopRidingFor(userRb, completed: true); // Clean up state and REMOVE from dictionary
         }
          // else { Debug.LogWarning($"Rope '{gameObject.name}': CompleteRopeRideFor called for {userRb?.name}, but they were not found in activeRiders (already stopped?)."); }
    }


    private void ForceStopController(Rigidbody2D userRb)
    {
        if (userRb == null) return;

        PlayerInteraction pi = userRb.GetComponent<PlayerInteraction>();
        if (pi != null) {
            // Debug.Log($"Rope: Forcing stop on PlayerInteraction for {userRb.name}");
            pi.ForceStopInteraction();
            return;
        }

        // --- ADD THIS ---
        NPCMovement npc = userRb.GetComponent<NPCMovement>();
        if (npc != null) {
            // Debug.Log($"Rope: Forcing stop on NPCMovement for {userRb.name}");
            npc.ForceStopInteraction();
            return;
        }
        // --- END ADD ---

        // Debug.LogWarning($"Rope ({gameObject.name}): ForceStopController called for {userRb.name}, but couldn't find controller.", userRb.gameObject);
    }


    void OnTriggerExit2D(Collider2D other)
    {
         Rigidbody2D userRb = other.attachedRigidbody;
         if (userRb != null && activeRiders.ContainsKey(userRb))
         {
             // Debug.Log($"Rope '{gameObject.name}': {userRb.name} EXITED trigger while riding. Forcing stop.");
             ForceStopController(userRb);
             StopRidingFor(userRb, completed: false);
         }
    }
}
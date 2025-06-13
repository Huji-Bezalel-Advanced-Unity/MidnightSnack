// Ladder.cs (Refactored for Multiple Users)
using UnityEngine;
using System.Collections.Generic; // Needed for Dictionary
using System.Linq; // Needed for ToList() if cleaning up multiple on disable

[RequireComponent(typeof(Collider2D))]
public class Ladder : MonoBehaviour, IInteractable
{
    [Header("Settings")]
    [SerializeField] private float climbSpeed = 3f; // This might become informational if movement is fully external

    // --- Per-Climber Data Structure ---
    private struct ClimbData
    {
        public float initialGravity;
        // Could add other per-climber state if needed later
    }

    // --- Internal State for Multiple Users ---
    private Dictionary<Rigidbody2D, ClimbData> activeClimbers = new Dictionary<Rigidbody2D, ClimbData>();
    private Collider2D ladderCollider;

    // Removed single-user state variables:
    // private Rigidbody2D playerRb;       // REMOVED
    // private bool isClimbing;             // REMOVED (implicit in activeClimbers.Count > 0)
    // private float playerInitGravity;    // REMOVED (now per-user)

    // --- IInteractable Implementation ---

    public InteractionType InteractionType => InteractionType.Ladder;

    /// <summary>
    /// Called by an initiator (PlayerInteraction, NPC script) to start climbing the ladder.
    /// </summary>
    /// <param name="initiatorGameObject">The GameObject initiating the interaction.</param>
    public void Interact(GameObject initiatorGameObject)
    {
        Rigidbody2D userRb = initiatorGameObject.GetComponent<Rigidbody2D>();
        if (userRb == null)
        {
            Debug.LogWarning($"Ladder '{gameObject.name}': Interact called by {initiatorGameObject.name}, but it has no Rigidbody2D.", initiatorGameObject);
            return;
        }

        // Check if this Rigidbody is already climbing this ladder
        if (activeClimbers.ContainsKey(userRb))
        {
            Debug.Log($"Ladder '{gameObject.name}': {userRb.name} is already climbing.");
            return;
        }

        // Optional: Single occupancy check
        // if (activeClimbers.Count > 0 && IsSingleOccupancy) { return; }

        Debug.Log($"Ladder '{gameObject.name}': Interact called by {userRb.name}. Conditions MET. Calling StartClimbingFor().");
        StartClimbingFor(userRb);
    }

    // --- End IInteractable Implementation ---

    void Awake()
    {
        ladderCollider = GetComponent<Collider2D>();
        if (ladderCollider == null) { Debug.LogError($"Ladder {gameObject.name} needs Collider2D", this); enabled = false; return; }
        if (!ladderCollider.isTrigger) { Debug.LogWarning($"Ladder {gameObject.name} Collider2D should be a trigger. Forcing Is Trigger = TRUE.", this); ladderCollider.isTrigger = true; }
        // playerRb validation removed
    }

    void OnEnable()
    {
        // Listen for the global STOP event
        EventManager.OnLadderInteractStop += HandleStopInteraction;
    }

    void OnDisable()
    {
        EventManager.OnLadderInteractStop -= HandleStopInteraction;

        // Clean up for all active climbers if the ladder object is disabled
        List<Rigidbody2D> climbersToStop = activeClimbers.Keys.ToList();
        foreach (Rigidbody2D climberRb in climbersToStop)
        {
            Debug.Log($"Ladder '{gameObject.name}' disabled. Forcing stop for climber {climberRb.name}.");
            ForceStopController(climberRb); // Notify controller
            StopClimbingFor(climberRb);     // Stop internal state
        }
        activeClimbers.Clear();
    }

    /// <summary>
    /// Handles the global STOP event. Stops the specific Rigidbody if it's climbing this ladder.
    /// </summary>
    private void HandleStopInteraction(Rigidbody2D triggeringRb)
    {
        if (activeClimbers.ContainsKey(triggeringRb))
        {
            Debug.Log($"Ladder '{gameObject.name}': HandleStopInteraction received for {triggeringRb.name}. Calling StopClimbingFor().");
            StopClimbingFor(triggeringRb);
        }
    }
    
    

    // --- Physics Updates for Active Climbers ---
    // We need FixedUpdate to consistently apply the X-lock physics change.
    void FixedUpdate()
    {
        // If no one is climbing, do nothing
        if (activeClimbers.Count == 0) return;

        // Apply X-lock to all active climbers
        foreach (Rigidbody2D climberRb in activeClimbers.Keys)
        {
            // Check if climberRb might have been destroyed or become invalid
            if (climberRb != null) {
                 LockXPositionFor(climberRb);
            }
             // Optional: Add logic here to automatically remove destroyed/invalid Rbs from the dictionary
             // else { climbersToRemove.Add(climberRb); } // Collect invalid keys to remove after loop
        }
        // Remove invalid keys if collected
    }

    /// <summary>
    /// Sets up the state for a specific Rigidbody to start climbing.
    /// </summary>
    void StartClimbingFor(Rigidbody2D userRb)
    {
        if (userRb == null || activeClimbers.ContainsKey(userRb)) return;

        ClimbData data = new ClimbData();
        data.initialGravity = userRb.gravityScale;

        activeClimbers.Add(userRb, data); // Add before changing physics

        userRb.gravityScale = 0f;
        userRb.linearVelocity = Vector2.zero; // Stop current movement

        LockXPositionFor(userRb); // Snap X position immediately

        Debug.Log($"Ladder '{gameObject.name}': {userRb.name} started climbing.");

        // The PlayerInteraction/NPC script is responsible for setting its IsInteracting state
        // and handling the actual vertical movement input.
    }

    /// <summary>
    /// Restores physics state for a specific Rigidbody when it stops climbing.
    /// </summary>
    void StopClimbingFor(Rigidbody2D userRb)
    {
        if (activeClimbers.TryGetValue(userRb, out ClimbData data))
        {
            // Restore gravity
            if (Mathf.Approximately(userRb.gravityScale, 0f))
            {
                userRb.gravityScale = data.initialGravity;
            }

            // Remove from active list AFTER restoring state
            activeClimbers.Remove(userRb);

            Debug.Log($"Ladder '{gameObject.name}': {userRb.name} stopped climbing.");

            // Let standard physics take over velocity.
        }
         else {
             // Debug.LogWarning($"Ladder '{gameObject.name}': Tried to stop {userRb.name}, but they were not in activeClimbers.", userRb);
         }
    }

    /// <summary>
    /// Locks the horizontal position and zeroes horizontal velocity for a specific climber.
    /// </summary>
    private void LockXPositionFor(Rigidbody2D userRb)
    {
        if (userRb == null) return;
        // Snap position if needed
        if (!Mathf.Approximately(userRb.position.x, transform.position.x))
        {
            userRb.position = new Vector2(transform.position.x, userRb.position.y);
        }
        // Zero out horizontal velocity constantly while climbing
        if (!Mathf.Approximately(userRb.linearVelocity.x, 0f))
        {
            userRb.linearVelocity = new Vector2(0f, userRb.linearVelocity.y);
        }
    }

     /// <summary>
    /// Helper to attempt to call ForceStopInteraction on the controller component of a Rigidbody.
    /// </summary>
    private void ForceStopController(Rigidbody2D userRb)
    {
        if (userRb == null) return;
        PlayerInteraction pi = userRb.GetComponent<PlayerInteraction>();
        if (pi != null) { pi.ForceStopInteraction(); return; }
        // Add checks for NPC controllers here
        // KamikazeNPC npc = userRb.GetComponent<KamikazeNPC>();
        // if (npc != null) { npc.ForceStopInteraction(); return; }
    }

    // --- Optional but Recommended: Trigger Exit for Safety ---
    void OnTriggerExit2D(Collider2D other)
    {
         Rigidbody2D userRb = other.attachedRigidbody;
         // Check if the Rigidbody that exited is one currently climbing this ladder
         if (userRb != null && activeClimbers.ContainsKey(userRb))
         {
             Debug.Log($"Ladder '{gameObject.name}': {userRb.name} EXITED trigger while climbing. Forcing stop.");
             ForceStopController(userRb); // Notify controller first
             StopClimbingFor(userRb);     // Stop internal logic
         }
    }
    // -------------------------------------------------

     // Removed methods that operated on the old single playerRb:
     // HandleClimbingMovement(), LockPlayerXPosition(), StartClimbing(), StopClimbing(), StopClimbingInternal()
     // Their logic is adapted into the 'For' versions or moved/removed.
}
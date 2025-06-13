// PlayerInteraction.cs (Revised to implement IMovementInputProvider and handle Trajectory type)
using UnityEngine;

// Requires PlayerMovement to control its state and Rigidbody2D
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(Rigidbody2D))]
// Implement the new input provider interface
public class PlayerInteraction : MonoBehaviour, IMovementInputProvider
{
    [Header("Interaction Settings")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private float interactionRadius = 1.0f;
    [SerializeField] private LayerMask interactableLayer;

    // --- Public State (Read-only) ---
    /// <summary>True if the player is engaged in a persistent interaction (Rope, Ladder, Trajectory).</summary>
    public bool IsInteracting { get; private set; } = false;
    /// <summary>The type of the active persistent interaction.</summary>
    public InteractionType ActiveInteractionType { get; private set; } = InteractionType.None;

    // --- Private Variables ---
    private PlayerMovement playerMovement; // Reference to movement script
    private Rigidbody2D rb;                // Reference to Rigidbody for EventManager
    private Collider2D[] overlapResults = new Collider2D[5];
    private IInteractable currentNearestInteractable = null; // Stores the component reference

    void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        rb = GetComponent<Rigidbody2D>();

        if (playerMovement == null || rb == null)
        {
            Debug.LogError($"PlayerInteraction on {gameObject.name} requires PlayerMovement and Rigidbody2D components!", this);
            enabled = false;
        }
    }

    void Update()
    {
        // Only look for new interactables if not currently in a persistent interaction
        if (!IsInteracting)
        {
            FindNearestInteractable();
        } else {
            // Clear potential target if already interacting
            currentNearestInteractable = null;
        }

        // Always handle input (needed to stop interactions)
        HandleInteractionInput();
    }

    /// <summary>
    /// Detects the nearest IInteractable using OverlapCircle and ClosestPoint.
    /// </summary>
    void FindNearestInteractable()
    {
        currentNearestInteractable = null;
        float closestDistSqr = Mathf.Infinity;
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, interactionRadius, overlapResults, interactableLayer);

        if (count > 0)
        {
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = overlapResults[i];
                if (hit.gameObject == this.gameObject) continue;

                IInteractable interactable = hit.GetComponent<IInteractable>();
                if (interactable != null)
                {
                    Vector2 closestPoint = Physics2D.ClosestPoint(transform.position, hit);
                    float distSqr = ((Vector2)transform.position - closestPoint).sqrMagnitude;

                    if (distSqr < closestDistSqr && distSqr <= (interactionRadius * interactionRadius))
                    {
                        closestDistSqr = distSqr;
                        currentNearestInteractable = interactable;
                    }
                }
            }
        }
        // Optional: Update UI Prompt based on currentNearestInteractable here
    }

    /// <summary>
    /// Handles the interact key press. Initiates interaction via IInteractable.Interact()
    /// or stops persistent interactions via EventManager.
    /// </summary>
    void HandleInteractionInput()
    {
        if (Input.GetKeyDown(interactKey))
        {
            // --- Attempt to START Interaction ---
            if (!IsInteracting && currentNearestInteractable != null)
            {
                //Debug.Log($"PlayerInteraction: Attempting Interact with {((MonoBehaviour)currentNearestInteractable).gameObject.name}");

                // Store type before calling Interact, in case Interact modifies the object state quickly
                InteractionType typeToStart = currentNearestInteractable.InteractionType;

                // Call Interact on the target object
                currentNearestInteractable.Interact(this.gameObject); // Pass this player's GameObject

                // --- Check if this interaction type requires disabling standard movement ---
                // Includes Rope, Ladder, AND the new Trajectory type
                if (typeToStart == InteractionType.Rope ||
                    typeToStart == InteractionType.Ladder ||
                    typeToStart == InteractionType.Trajectory) // Added Trajectory check
                {
                    //Debug.Log($"PlayerInteraction: Entering persistent interaction state for {typeToStart}");
                    IsInteracting = true;
                    ActiveInteractionType = typeToStart; // Store the type we are now engaged with
                    playerMovement.SetMovementEnabled(false); // Disable standard movement
                }
                // Buttons (and potentially others) complete instantly and don't set IsInteracting/disable movement.
            }
            // --- Attempt to STOP Persistent Interaction ---
            else if (IsInteracting)
            {
                // We should have an ActiveInteractionType if IsInteracting is true
                // (Rope, Ladder, or Trajectory)
                if (ActiveInteractionType != InteractionType.None)
                {
                   // Debug.Log($"PlayerInteraction: Attempting STOP interaction (Type: {ActiveInteractionType})");

                    // Trigger the specific STOP event for the ACTIVE interaction type
                    // Note: TrajectoryMover currently doesn't listen for a stop event,
                    // it relies on the initiator calling StopMovingFor or trigger exit.
                    // We still send the event in case other systems need it or if TrajectoryMover changes.
                    EventManager.TriggerInteractionStop(ActiveInteractionType, rb);

                    // Immediately reset player state and re-enable movement
                    ResetInteractionState();
                }
                else
                {
                    // This indicates a logic error somewhere - IsInteracting was true but type was None.
                    Debug.LogWarning("PlayerInteraction: Trying to stop interaction, but ActiveInteractionType was None. Forcing state reset.");
                    ResetInteractionState(); // Reset anyway
                }
            }
        }
    }

    /// <summary>
    /// Called externally (e.g., by Rope/Ladder/Trajectory OnTriggerExit) or internally to force stop.
    /// Resets interaction state and re-enables movement.
    /// </summary>
    public void ForceStopInteraction()
    {
        if (IsInteracting)
        {
            //Debug.Log($"PlayerInteraction: Force stopping interaction of type: {ActiveInteractionType}");
            if (ActiveInteractionType != InteractionType.None)
            {
                // Still trigger the event so the interactable object *might* clean up its state
                // (Rope/Ladder listen, Trajectory currently doesn't but might in future)
                EventManager.TriggerInteractionStop(ActiveInteractionType, rb);
            }
            // Reset player state regardless of whether the type was known
            ResetInteractionState();
        }
    }

    /// <summary>
    /// Helper method to reset flags and re-enable movement.
    /// </summary>
    private void ResetInteractionState()
    {
        IsInteracting = false;
        ActiveInteractionType = InteractionType.None;
        if(playerMovement != null) playerMovement.SetMovementEnabled(true); // Re-enable standard movement
        currentNearestInteractable = null; // Clear nearest target upon stopping
        //Debug.Log("PlayerInteraction: Interaction state reset, movement enabled.");
    }

    // --- IMovementInputProvider Implementation ---
    /// <summary>
    /// Provides the player's input axes, used by interactables like TrajectoryMover.
    /// </summary>
    /// <returns>Vector2 containing Horizontal (-1 to 1) and Vertical (-1 to 1) axis values.</returns>
    public Vector2 GetMovementInput()
    {
        // Read standard Unity input axes (ensure they are set up in Project Settings > Input Manager)
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    }
    // --- End IMovementInputProvider Implementation ---

    // --- Gizmos ---
    void OnDrawGizmosSelected()
    {
        // Draw interaction radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
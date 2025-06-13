/*// PlayerController.cs (Modified for Interface Interaction)
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    // --- Parameters (Movement, Interaction) ---
    [Header("Movement Parameters")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 7f;
    [Header("Interaction Parameters")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private float interactionRadius = 1.0f;
    [SerializeField] private LayerMask interactableLayer;

    // --- Public State ---
    public bool IsInteracting { get; private set; } = false;
    public InteractionType ActiveInteractionType { get; private set; } = InteractionType.None;

    // --- Private Variables ---
    private Rigidbody2D rb;
    // Using simple velocity check for testing jump
    private float verticalVelocityThreshold = 0.05f;

    // Interaction detection using Interfaces
    private Collider2D[] overlapResults = new Collider2D[5];
    private IInteractable currentNearestInteractable = null; // Store the Interface reference


    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        FindNearestInteractable(); // Detects nearest IInteractable
        HandleInteractionInput(); // Handles key press

        if (!IsInteracting) // Movement logic only if not in a persistent interaction state
        {
            HandleMovementInput();
            HandleJumpInput();
        }
    }

    /// <summary>
    /// Detects the nearest GameObject with an IInteractable component.
    /// Uses Physics2D.ClosestPoint for accurate distance checking with colliders.
    /// </summary>
    void FindNearestInteractable()
    {
        currentNearestInteractable = null; // Reset before check
        float closestDistSqr = Mathf.Infinity;

        int count = Physics2D.OverlapCircleNonAlloc(transform.position, interactionRadius, overlapResults, interactableLayer);

        if (count > 0)
        {
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = overlapResults[i];
                if (hit.gameObject == this.gameObject) continue;

                // Try to get the IInteractable component from the hit object
                IInteractable interactable = hit.GetComponent<IInteractable>();
                if (interactable != null) // Found an interactable object
                {
                    Vector2 closestPoint = Physics2D.ClosestPoint(transform.position, hit);
                    float distSqr = ((Vector2)transform.position - closestPoint).sqrMagnitude;

                    if (distSqr < closestDistSqr && distSqr <= (interactionRadius * interactionRadius))
                    {
                        closestDistSqr = distSqr;
                        currentNearestInteractable = interactable; // Store the component reference
                    }
                }
            }
        }
    }


    /// <summary>
    /// Handles interaction input key press.
    /// Calls Interact() on the nearest IInteractable or triggers stop event.
    /// </summary>
    void HandleInteractionInput()
    {
        if (Input.GetKeyDown(interactKey))
        {
            // --- Attempt to START Interaction ---
            // Can start if not already interacting AND we found a nearby interactable
            if (!IsInteracting && currentNearestInteractable != null)
            {
                Debug.Log($"Player pressing Interact near {((MonoBehaviour)currentNearestInteractable).gameObject.name}");
                // Call the Interact method ON the interactable object
                currentNearestInteractable.Interact(this); // Pass this PlayerController instance

                // --- Check if this interaction blocks movement ---
                // If the interaction was a Rope or Ladder, enter the IsInteracting state
                InteractionType typeStarted = currentNearestInteractable.InteractionType;
                if (typeStarted == InteractionType.Rope || typeStarted == InteractionType.Ladder)
                {
                     Debug.Log($"Entering persistent interaction state for {typeStarted}");
                     IsInteracting = true;
                     ActiveInteractionType = typeStarted; // Store the type we are now engaged with
                }
                // Buttons (and potentially others) don't set IsInteracting = true
            }
            // --- Attempt to STOP Interaction ---
            // Can stop if currently interacting (must be Rope or Ladder based on above logic)
            else if (IsInteracting)
            {
                // We should have an ActiveInteractionType if IsInteracting is true
                if (ActiveInteractionType != InteractionType.None)
                {
                    Debug.Log($"Player attempting STOP interaction (Type: {ActiveInteractionType})");
                    // Trigger the generic STOP event for the specific type
                    EventManager.TriggerInteractionStop(ActiveInteractionType, rb);

                    // Reset player state immediately (interactable will clean up upon receiving event)
                    IsInteracting = false;
                    ActiveInteractionType = InteractionType.None;
                }
                else
                {
                    // Safety fallback
                    Debug.LogWarning("PlayerController: Trying to stop interaction, but ActiveInteractionType was None. Forcing IsInteracting to false.");
                    IsInteracting = false;
                }
            }
        }
    }

    // --- Movement & Jump (Using simple velocity check for testing) ---
    void HandleMovementInput() { float moveInput = Input.GetAxis("Horizontal"); rb.velocity = new Vector2(moveInput * moveSpeed, rb.velocity.y); }
    void HandleJumpInput() { bool canJumpForTesting = !IsInteracting && Mathf.Abs(rb.velocity.y) < verticalVelocityThreshold; if (Input.GetButtonDown("Jump") && canJumpForTesting) { rb.velocity = new Vector2(rb.velocity.x, jumpForce); } }

    /// <summary>
    /// Called externally (e.g., by interactable's OnTriggerExit) or internally to force stop.
    /// </summary>
    public void ForceStopInteraction()
    {
        if (IsInteracting)
        {
            Debug.Log($"Force stopping interaction of type: {ActiveInteractionType}");
            if (ActiveInteractionType != InteractionType.None)
            {
                // Still good practice to trigger the stop event so the interactable cleans up fully
                EventManager.TriggerInteractionStop(ActiveInteractionType, rb);
            }
            // Reset player state
            IsInteracting = false;
            ActiveInteractionType = InteractionType.None;
        }
    }

    // --- Gizmos ---
    void OnDrawGizmosSelected() { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, interactionRadius); }
}*/
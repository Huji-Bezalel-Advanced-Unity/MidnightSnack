// LightPlayerController.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Animator))]
// Implement specific interfaces for Light Player's capabilities
public class LightPlayerController : MonoBehaviour, IDoubleJumpController, IInteractionController, IMovementInputProvider
{
    // --- Input Keys (Hardcoded) ---
    private KeyCode leftKey = KeyCode.LeftArrow;
    private KeyCode rightKey = KeyCode.RightArrow;
    private KeyCode upKey = KeyCode.UpArrow; // Also used for Vertical Input
    private KeyCode jumpKey = KeyCode.UpArrow;
    private KeyCode interactKey = KeyCode.Slash;
    private KeyCode abilityKey = KeyCode.RightControl;

    // --- Configuration ---
    [Header("Movement & Acceleration")]
    [SerializeField] private float maxMoveSpeed = 7f;
    [SerializeField] private float moveAcceleration = 80f;
    [SerializeField] private float moveDeceleration = 100f;
    [SerializeField] private float airAcceleration = 40f;
    [SerializeField] private float airDeceleration = 50f;
    [Header("Jumping")]
    [SerializeField] private float jumpForce = 12f;
    // Although MAX_JUMPS is 1, keep the field for potential future changes or consistency?
    // Let's keep it, but MAX_JUMPS will limit its use.
    [SerializeField] private float doubleJumpForce = 10f;
    [SerializeField] [Range(0f, 0.2f)] private float coyoteTimeDuration = 0.1f;
    [SerializeField] [Range(0f, 0.2f)] private float jumpBufferDuration = 0.1f;
    [Header("Ground Check (Raycast)")]
    [SerializeField] private float groundCheckDistance = 0.1f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float footOffset = 0.4f;
    [SerializeField] [Range(0f, 90f)] private float maxSlopeAngle = 45f;
    [Header("Physics & Gravity")]
    [SerializeField] private float fallMultiplier = 3.5f;
    [SerializeField] private float lowJumpMultiplier = 3f;
    [Tooltip("Rigidbody2D Linear Drag")]
    [SerializeField] private float linearDrag = 2f;
    [Header("Interaction")]
    [SerializeField] private float interactionRadius = 0.5f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private Transform interactionCheckPoint;
    [Header("Event")] 
    [SerializeField] private Vector2 newPositionMidlle;
    [SerializeField] private Vector2 newPositionEnd;

    // --- Components ---
    public Rigidbody2D Rb { get; private set; }
    public Collider2D Coll { get; private set; }
    public Transform Transform { get; private set; }

    // --- State Machine ---
    private IPlayerState currentState;
    public GroundedState GroundedStateInstance { get; private set; }
    public AirborneState AirborneStateInstance { get; private set; }
    public ClimbingState ClimbingStateInstance { get; private set; }
    // No PhasingState for Light Player

    // --- Current State Information ---
    public bool IsGrounded { get; private set; }
    public Vector2 GroundNormal { get; private set; }
    public int JumpsRemaining { get; private set; }
    public float CurrentGravityScale { get; private set; } // Store initial gravity

    // --- Input State ---
    public float HorizontalInput { get; private set; }
    public float VerticalInput { get; private set; }
    public bool JumpInputDownThisFrame { get; private set; }
    public bool JumpInputHeld { get; private set; }
    public bool InteractInputDownThisFrame { get; private set; }
    public bool AbilityInputDownThisFrame { get; private set; }
    public float JumpBufferCounter => jumpBufferCounter;
    public float CoyoteTimeCounter => coyoteTimeCounter;
    
    private Animator animator;
    private readonly int animParamIsGrounded = Animator.StringToHash("IsGrounded");
    private readonly int animParamHorizontalSpeed = Animator.StringToHash("HorizontalSpeed");
    private readonly int animParamVerticalSpeed = Animator.StringToHash("VerticalSpeed");
    private readonly int animParamJumpTrigger = Animator.StringToHash("JumpTrigger");
    private readonly int animParamIsWalk = Animator.StringToHash("EndGame");
    
    // --- Internal Timers & Flags ---
    private const int MAX_JUMPS = 0;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;

    private bool endGame = false;
    
    private bool isInFinalScene = false;
    


    // --- Unity Lifecycle Methods ---
    private void Awake()
    {
        Rb = GetComponent<Rigidbody2D>();
        Coll = GetComponent<Collider2D>();
        Transform = transform;
        animator = GetComponent<Animator>(); // <<< GET THE ANIMATOR COMPONENT
        
        Rb.linearDamping = linearDrag;
        CurrentGravityScale = Rb.gravityScale;

        if (interactionCheckPoint == null) interactionCheckPoint = transform;

        GroundedStateInstance = new GroundedState();
        AirborneStateInstance = new AirborneState();
        ClimbingStateInstance = new ClimbingState();
        currentState = null;
        
        EventManager.OnDarkPlayerStuckInLight += OnDarkPlayerStuckInLight;
        EventManager.OnGameEndedStep2 += HandleGameEndedStep2;
        SceneManager.activeSceneChanged += OnActiveSceneChanged_LightPlayer;
        Debug.Log($"{gameObject.name} (Light Player - StateMachine) Initialized.");
        endGame = false;
    }

    void OnActiveSceneChanged_LightPlayer(Scene previousScene, Scene newScene)
    {
        if (newScene.name == "Final") // Make sure "Final" is your exact scene name
        {
            Debug.Log($"LightPlayerController ({gameObject.name}): Entering FinalScene. Becoming static.");
            isInFinalScene = true;
            if (Rb != null)
            {
                Rb.linearVelocity = Vector2.zero;
                Rb.isKinematic = true;
            }
            // Change to a specific waiting/idle state if needed
            // This assumes GroundedStateInstance exists and is appropriate.
            if (currentState != null && !(currentState is ClimbingState)) ChangeState(GroundedStateInstance);


            // Optional: Set animator to a specific pose
            // Animator lightAnimator = GetComponent<Animator>(); // 'animator' field already exists
            if (animator != null)
            {
                animator.SetFloat(animParamHorizontalSpeed, 0f);
                animator.SetFloat(animParamVerticalSpeed, 0f);
                animator.SetBool(animParamIsGrounded, true);
            }
        }
        else
        {
            isInFinalScene = false;
            if (Rb != null) Rb.isKinematic = false; // Ensure physics is active for gameplay
        }
    }
    
    void OnCollisionEnter2D(Collision2D collision)
    {
        // --- START OF DEBUG LOGS ---

        // Log the basic information: name and layer of the object collided with.
        // The 'collision.gameObject' is the other GameObject involved.

        // Log details about the contact point(s) if available.
        if (collision.contacts.Length > 0)
        {
            ContactPoint2D firstContact = collision.contacts[0]; // Get the first contact point for simplicity
            
            // If you want to log all contact points (can be many for complex collisions):
            /*
            for (int i = 0; i < collision.contacts.Length; i++)
            {
                ContactPoint2D contact = collision.contacts[i];
                Debug.Log($"    Contact {i}: Point = {contact.point}, Normal = {contact.normal}, OtherCollider = {contact.otherCollider.name}");
            }
            */
        }
        else
        {
            Debug.Log("    No contact points reported for this collision (unusual for OnCollisionEnter2D).");
        }
        
        // Optional: Log the relative velocity of the collision.
        // This can tell you how fast the two objects were moving towards each other.
        // A high magnitude might indicate a forceful impact.
        // Debug.Log($"    Relative Velocity: {collision.relativeVelocity} (Magnitude: {collision.relativeVelocity.magnitude})");

        // Optional: Log the impulse applied (if interested in physics forces)
        // Debug.Log($"    Impulse: {collision.impulse}"); // This is often zero if you're not using impulse-based physics directly

        // --- END OF DEBUG LOGS ---
    }

    private void OnDestroy() // Always good practice to unsubscribe
    {
        EventManager.OnDarkPlayerStuckInLight -= OnDarkPlayerStuckInLight;
        EventManager.OnGameEndedStep2 -= HandleGameEndedStep2;
    }

    private void HandleGameEndedStep2()
    {
        transform.position = newPositionEnd;
        endGame = true;
    }

    private void OnDarkPlayerStuckInLight(DarkPlayerController stuckPlayer)
    {
        transform.position = newPositionMidlle;
    }

    private void Start()
    {
         CheckIfGrounded();
         if (!IsGrounded) { JumpsRemaining = 0; currentState = AirborneStateInstance; }
         else { JumpsRemaining = MAX_JUMPS; currentState = GroundedStateInstance; } // Starts with MAX_JUMPS (1)
         currentState?.EnterState(this);
    }

    private void Update() // Update is a good place for Animator parameter updates
    {
        if (endGame)
        {
            // Only listen for "any key" to trigger the final game end event
            if (Input.anyKeyDown)
            {
                // Optional: Ignore specific keys like Escape if Escape has its own quit functionality elsewhere
                // if (Input.GetKeyDown(KeyCode.Escape)) return; 
                
                Debug.Log($"DarkPlayerController ({gameObject.name}): 'Any Key' pressed during endGame. Triggering OnGameEndedFinal.");
                EventManager.TriggerOnGameEndedFinal();
                endGame = false; // Reset flag to prevent multiple triggers from one key-mash
                // Or, you might want to disable this script/player control entirely.
            }
            return; // Skip normal Update logic if endGame is true
        }
        ReadInput();
        currentState?.HandleInput(this);
        
        if (currentState != ClimbingStateInstance)
        {
            HandleSpriteFlipping(HorizontalInput);
        }
        
        UpdateTimers();
        currentState?.UpdateLogic(this);

        // --- NEW: Update Animator Parameters ---
        UpdateAnimatorParameters();
        // -------------------------------------
    }
    
    private void FixedUpdate()
    {
        bool previouslyGrounded = IsGrounded;
        CheckIfGrounded(); // This updates IsGrounded property
        HandleLandingAndCoyote(previouslyGrounded);

        currentState?.UpdatePhysics(this); 

        if (currentState != ClimbingStateInstance)
        {
            if (IsGrounded && currentState != GroundedStateInstance)
            { ChangeState(GroundedStateInstance); }
            else if (!IsGrounded && currentState == GroundedStateInstance && CoyoteTimeCounter <= 0)
            { ChangeState(AirborneStateInstance); }
        }

        if (currentState != ClimbingStateInstance)
        {
            bool jumpExecuted = false;
            if ((JumpBufferCounter > 0f || JumpInputDownThisFrame) && CoyoteTimeCounter > 0f)
            { ExecuteJump(true); jumpExecuted = true; }
            else if ((JumpBufferCounter > 0f || JumpInputDownThisFrame) && IsGrounded)
            { ExecuteJump(true); jumpExecuted = true; }
            else if ((JumpBufferCounter > 0f || JumpInputDownThisFrame) && !IsGrounded && CoyoteTimeCounter <= 0f && JumpsRemaining > 0)
            { ExecuteJump(false); jumpExecuted = true; }

            if (jumpExecuted) // If a jump was executed this frame
            {
                animator.SetTrigger(animParamJumpTrigger); // <<< TRIGGER JUMP ANIMATION
            }

            if (jumpExecuted || JumpBufferCounter > 0f && !IsGrounded && CoyoteTimeCounter <= 0f && JumpsRemaining <= 0)
            { ResetJumpBuffer(); }
        }
    }

    // --- NEW: Method to Update Animator ---
    private void UpdateAnimatorParameters()
    {
        if (animator == null) return;

        animator.SetBool(animParamIsGrounded, IsGrounded);
        animator.SetFloat(animParamHorizontalSpeed, Mathf.Abs(Rb.linearVelocity.x)); // Use actual velocity for run/idle
        animator.SetFloat(animParamVerticalSpeed, Rb.linearVelocity.y);
    }
    // --------------------------------------

    // --- State Machine Management ---
    public void ChangeState(IPlayerState newState)
    {
        if (newState == null || newState == currentState) return;
        currentState?.ExitState(this);
        currentState = newState;
        currentState.EnterState(this);
    }

    // --- Input Reading ---
    private void ReadInput()
    {
        JumpInputDownThisFrame = false;
        InteractInputDownThisFrame = false;
        AbilityInputDownThisFrame = false;

        // Handle different input reading based on state if needed (e.g., only vertical in climbing)
        if (currentState == ClimbingStateInstance)
        {
            HorizontalInput = 0f; // No horizontal movement when climbing
            VerticalInput = 0f;
            if (Input.GetKey(upKey)) VerticalInput = 1f;
            else if (Input.GetKey(KeyCode.DownArrow)) VerticalInput = -1f; // Use explicit DownArrow if needed

            JumpInputHeld = Input.GetKey(jumpKey);
            if (Input.GetKeyDown(jumpKey)) { JumpInputDownThisFrame = true; }
            if (Input.GetKeyDown(interactKey)) { InteractInputDownThisFrame = true; }
            return; // Skip standard input reading
        }

        // --- Standard Input Reading ---
        HorizontalInput = 0f;
        if (Input.GetKey(leftKey)) HorizontalInput -= 1f;
        if (Input.GetKey(rightKey)) HorizontalInput += 1f;

        VerticalInput = 0f; // Only used by Climbing state
        if (Input.GetKey(upKey)) VerticalInput = 1f;

        JumpInputHeld = Input.GetKey(jumpKey);

        if (Input.GetKeyDown(jumpKey)) { JumpInputDownThisFrame = true; jumpBufferCounter = jumpBufferDuration; }
        if (Input.GetKeyDown(interactKey)) { InteractInputDownThisFrame = true; }
        if (Input.GetKeyDown(abilityKey)) { AbilityInputDownThisFrame = true; }
    }

    // --- Timer Updates ---
    private void UpdateTimers()
    {
        if (coyoteTimeCounter > 0) coyoteTimeCounter -= Time.deltaTime;
        if (jumpBufferCounter > 0) jumpBufferCounter -= Time.deltaTime;
    }

    // --- Physics & Grounding ---
    private void CheckIfGrounded()
    {
        bool groundDetected = false;
        Vector2 bestGroundNormal = Vector2.up;
        Bounds colliderBounds = Coll.bounds;
        Vector2 centerPoint = new Vector2(colliderBounds.center.x, colliderBounds.min.y);
        Vector2 leftOrigin = centerPoint + Vector2.left * footOffset;
        Vector2 rightOrigin = centerPoint + Vector2.right * footOffset;
        Vector2 centerOrigin = centerPoint;
        Vector2[] origins = { leftOrigin, centerOrigin, rightOrigin };

        foreach (Vector2 origin in origins)
        {
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDistance, groundLayer);
             #if UNITY_EDITOR
             Color rayColor = Color.red;
             if(hit.collider != null) { rayColor = (Vector2.Angle(hit.normal, Vector2.up) < maxSlopeAngle) ? Color.green : Color.yellow; }
             Debug.DrawRay(origin, Vector2.down * groundCheckDistance, rayColor);
             #endif
            if (hit.collider != null)
            {
                float angle = Vector2.Angle(hit.normal, Vector2.up);
                if (angle < maxSlopeAngle) { groundDetected = true; bestGroundNormal = hit.normal; break; }
            }
        }
        IsGrounded = groundDetected;
        if (IsGrounded) GroundNormal = bestGroundNormal;
    }

    private void HandleLandingAndCoyote(bool previouslyGrounded)
    {
        if (!previouslyGrounded && IsGrounded)
        {
            JumpsRemaining = MAX_JUMPS; // Reset to 1
            coyoteTimeCounter = 0f;
        }
        else if (previouslyGrounded && !IsGrounded)
        {
             if (Rb.linearVelocity.y <= 0.01f) { coyoteTimeCounter = coyoteTimeDuration; }
             else { coyoteTimeCounter = 0f; }
        }
    }
    

    // --- Jump Execution ---
    private void ExecuteJump(bool isGroundOrCoyoteJump)
    {
        if (!isGroundOrCoyoteJump)
        {
            ConsumeAirJump();
        }
        
        float currentJumpForce = isGroundOrCoyoteJump ? jumpForce : doubleJumpForce;
        Rb.linearVelocity = new Vector2(Rb.linearVelocity.x, 0f);
        Rb.AddForce(Vector2.up * currentJumpForce, ForceMode2D.Impulse);

        // animator.SetTrigger(animParamJumpTrigger); // MOVED to FixedUpdate where jumpExecuted is checked

        ResetCoyoteTimer();
        ResetJumpBuffer();
    }

    // --- State Callbacks / Helpers ---
    // This method still exists for IDoubleJumpController interface compliance
    public void ConsumeAirJump() { if (JumpsRemaining > 0) { JumpsRemaining--; } }
    public void ResetJumpBuffer() { jumpBufferCounter = 0f; }
    public void ResetCoyoteTimer() { coyoteTimeCounter = 0f; }

    // --- Visuals ---
    public void HandleSpriteFlipping(float hInput)
    {
        // Add check !isInteracting if needed
        if (currentState != ClimbingStateInstance)
        {
            if (Mathf.Abs(hInput) > 0.01f)
            {
                Transform.localScale = new Vector3(Mathf.Sign(hInput) * Mathf.Abs(Transform.localScale.x), Transform.localScale.y, Transform.localScale.z);
            }
        }
    }

    // --- Interaction ---
    public void HandleInteractionAttempt() // Called by States
    {
        // Prevent starting interaction if already in a special state
        if (currentState == ClimbingStateInstance) 
            return;

        Collider2D[] nearbyInteractables = Physics2D.OverlapCircleAll(
            InteractionCheckPointProp.position, InteractionRadiusProp, InteractableLayer
        );
        if (nearbyInteractables.Length == 0) return;

        Collider2D closestInteractableCollider = FindClosestInteractable(nearbyInteractables, InteractionCheckPointProp.position);

        // --- Find IInteractable component ---
        IInteractable interactable = closestInteractableCollider.GetComponent<IInteractable>(); // Get the interface

        if (interactable != null)
        {
            InteractionType type = interactable.InteractionType; // Get the type via the interface
            Debug.Log($"{gameObject.name} Found Interactable: {closestInteractableCollider.name} (Type: {type})");

            // --- Call Interact on the found object ---
            interactable.Interact(this.gameObject); // Call the Interact method via the interface

            // --- Handle Player State Change based on Type (If necessary) ---
            if (type == InteractionType.Trajectory || type == InteractionType.Ladder || type == InteractionType.Rope)
            {
                 // Get the specific component *again* if needed for state setup
                 TrajectoryMover trajectory = closestInteractableCollider.GetComponent<TrajectoryMover>();
                 if (trajectory != null)
                 {
                     ClimbingStateInstance.SetTrajectory(trajectory);
                     ChangeState(ClimbingStateInstance);
                 } else {
                     Debug.LogError($"Interactable {closestInteractableCollider.name} has Trajectory type but missing TrajectoryMover script!", closestInteractableCollider);
                 }
            }
            // Buttons (and others maybe) don't require player state change - Interact() does the work.
        }
        // else: Object on layer doesn't implement IInteractable
    }

    private Collider2D FindClosestInteractable(Collider2D[] colliders, Vector2 checkPosition)
    {
        Collider2D closest = null;
        float minDistanceSqr = Mathf.Infinity;
        foreach (Collider2D coll in colliders)
        {
            Vector2 pointOnCollider = coll.ClosestPoint(checkPosition);
            float distSqr = (checkPosition - pointOnCollider).sqrMagnitude;
            if (distSqr < minDistanceSqr) { minDistanceSqr = distSqr; closest = coll; }
        }
        return closest;
    }

    // --- Force Stop Interaction ---
    public void ForceStopInteraction()
    {
        if (currentState == ClimbingStateInstance)
        {
             Debug.Log($"{gameObject.name} ForceStopInteraction called externally.");
             EventManager.TriggerInteractionStop(InteractionType.Trajectory, Rb);
             ChangeState(AirborneStateInstance);
        }
    }

    // --- IMovementInputProvider Implementation ---
    public Vector2 GetMovementInput() { return new Vector2(HorizontalInput, VerticalInput); }

    // --- Gizmos ---
    /*
    private void OnDrawGizmosSelected()
    {
        // Draw Ground Check Gizmos
        if (Coll != null)
        {
            Bounds colliderBounds = Coll.bounds;
            Vector2 centerPoint = new Vector2(colliderBounds.center.x, colliderBounds.min.y);
            Vector2 leftOrigin = centerPoint + Vector2.left * footOffset;
            Vector2 rightOrigin = centerPoint + Vector2.right * footOffset;
            Vector2 centerOrigin = centerPoint;
            Gizmos.color = Color.cyan; // Light Player ground check color
            Gizmos.DrawLine(leftOrigin, leftOrigin + Vector2.down * groundCheckDistance);
            Gizmos.DrawLine(centerOrigin, centerOrigin + Vector2.down * groundCheckDistance);
            Gizmos.DrawLine(rightOrigin, rightOrigin + Vector2.down * groundCheckDistance);
        }

         // Draw Interaction Check Gizmo
         Transform pointToCheck = (interactionCheckPoint != null) ? interactionCheckPoint : Transform;
         Gizmos.color = Color.blue;
         Gizmos.DrawWireSphere(pointToCheck.position, interactionRadius);
    }
    */


    // --- Accessors for Interfaces ---
    // IBasePlayerController + IDoubleJumpController + IInteractionController
    public float MaxMoveSpeed => maxMoveSpeed;
    public float MoveAcceleration => moveAcceleration;
    public float MoveDeceleration => moveDeceleration;
    public float AirAcceleration => airAcceleration;
    public float AirDeceleration => airDeceleration;
    public float JumpForce => jumpForce;
    public float DoubleJumpForce => doubleJumpForce; // Still implement, even if unused due to MAX_JUMPS
    public float FallMultiplier => fallMultiplier;
    public float LowJumpMultiplier => lowJumpMultiplier;
    public LayerMask InteractableLayer => interactableLayer;
    public float InteractionRadiusProp => interactionRadius;
    public Transform InteractionCheckPointProp => interactionCheckPoint;
    // State instances
    public IPlayerState CurrentState => currentState;
    IPlayerState IBasePlayerController.GroundedStateInstance => GroundedStateInstance; // Use base interface
    IPlayerState IBasePlayerController.AirborneStateInstance => AirborneStateInstance; // Use base interface
    IPlayerState IInteractionController.ClimbingStateInstance => ClimbingStateInstance; // Use interaction interface
}
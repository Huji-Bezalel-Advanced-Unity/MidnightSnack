// DarkPlayerController.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Animator))]
// Implement interfaces for Dark Player's capabilities
public class DarkPlayerController : MonoBehaviour, IPhasingController, IInteractionController, IMovementInputProvider
{
    [SerializeField] private AudioClip hitSound; 

    // --- Input Keys ---
    [SerializeField] private KeyCode leftKey = KeyCode.A;
    [SerializeField] private KeyCode rightKey = KeyCode.D;
    [SerializeField] private KeyCode upKey = KeyCode.W;
    [SerializeField] private KeyCode downKey = KeyCode.S; // Defined if needed for climbing down
    [SerializeField] private KeyCode jumpKey = KeyCode.W;
    [SerializeField] private KeyCode abilityKey = KeyCode.LeftShift; // Phase Key
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Respawn Settings")]
    [Tooltip("How long the player must be pushed by light AND nearly stationary to trigger respawn.")]
    [SerializeField] private float stuckInLightDuration = 0.5f;
    [Tooltip("The maximum speed magnitude considered 'stuck' while being pushed by light.")]
    [SerializeField] private float stuckVelocityThreshold = 0.1f;
// --- NEW OFFSET FIELDS ---
    [Tooltip("The small offset to apply to the player's position on respawn (e.g., to nudge them out of the floor).")]
    [SerializeField] private Vector2 respawnOffset = new Vector2(0, 0.5f); // Example: Nudge slightly upwards
// -------------------------
    
    [Header("Light Interaction")]
    [Tooltip("Points on the player used to check for light exposure (e.g., feet, center, head). Create Empty GameObjects as children.")]
    [SerializeField] private Transform[] lightCheckPoints;
    [Tooltip("Layer mask containing the 'LightArea' trigger layer.")]
    [SerializeField] private LayerMask lightAreaLayer; // Assign the LightArea layer here
    [Tooltip("Layer mask containing objects that block light (e.g., 'LightBlocker').")]
    [SerializeField] private LayerMask lightBlockerLayer; // Assign the LightBlocker layer here
    [Tooltip("Force applied to push the player out of unblocked light.")]
    [SerializeField] private float lightPushForce = 50f;

    [Header("Movement & Acceleration")]
    [SerializeField] private float maxMoveSpeed = 7f;
    [SerializeField] private float moveAcceleration = 80f;
    [SerializeField] private float moveDeceleration = 100f;
    [SerializeField] private float airAcceleration = 40f;
    [SerializeField] private float airDeceleration = 50f;
    [Header("Jumping")]
    [SerializeField] private float jumpForce = 12f;
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
    [Header("Phase Ability Settings")]
    [SerializeField] private float phaseSpeed = 18f;
    [SerializeField] private float phaseDuration = 0.25f;
    [SerializeField] private float phaseCooldown = 1.0f;
    [Tooltip("Layers player ignores during phase")]
    [SerializeField] private LayerMask layersToPhaseThrough;
    [Tooltip("Layers player should NOT end phase inside")]
    [SerializeField] private LayerMask solidLayers;
    [SerializeField] private float depenetrationPadding = 0.05f;
    [Header("Interaction")]
    [SerializeField] private float interactionRadius = 0.5f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private Transform interactionCheckPoint;
    [Header("Event")] 
    [SerializeField] private Vector2 newPositionMiddle;
    [SerializeField] private Vector2 newPositionEnd;
    [SerializeField] private Vector2 EndPoint;
    
    

    // --- Components ---
    public Rigidbody2D Rb { get; private set; }
    public Collider2D Coll { get; private set; }
    public Transform Transform { get; private set; }
    
    public LayerMask InteractableLayer => interactableLayer;
    public float InteractionRadiusProp => interactionRadius;
    public Transform InteractionCheckPointProp => interactionCheckPoint;

    // --- State Machine ---
    private IPlayerState currentState; // Use backing field for explicit interface implementation
    public GroundedState GroundedStateInstance { get; private set; }
    public AirborneState AirborneStateInstance { get; private set; }
    public ClimbingState ClimbingStateInstance { get; private set; }
    public PhasingState PhasingStateInstance { get; private set; }

    // --- Current State Info ---
    public bool IsGrounded { get; private set; }
    public Vector2 GroundNormal { get; private set; }
    public int JumpsRemaining { get; private set; } // Always 0 or 1
    public float CurrentGravityScale { get; private set; }

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
    // --- Animator Parameter Hashes ---
    private readonly int animParamIsGrounded = Animator.StringToHash("IsGrounded");
    private readonly int animParamHorizontalSpeed = Animator.StringToHash("HorizontalSpeed");
    private readonly int animParamVerticalSpeed = Animator.StringToHash("VerticalSpeed");
    private readonly int animParamJumpTrigger = Animator.StringToHash("JumpTrigger");
    private readonly int animParamStuckInLightTrigger = Animator.StringToHash("StuckInLightTrigger"); // ONLY THE TRIGGER
    
    private bool isCurrentlyStuckPlayingAnimation = false;
    
    // --- Internal Timers & Flags ---
    private const int MAX_JUMPS = 1;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private Vector2 lastInputDir = Vector2.right;
    private float phaseCooldownTimer = 0f;
    private Coroutine activePhaseCoroutine = null;
    
    private bool isInUnblockedLight = false;
    private Vector2 lightPushDirection = Vector2.zero;
    private float stuckInLightTimer = 0f; // Timer for checking if stuck

    private bool endGame = false;
    private bool endGameInputTriggeredThisSession = false;
    
    private bool isInFinalSceneWaitingForInput = false; 
    private bool finalInputProcessed = false;
    
    public Transform TheEndPoint; // Assign this in the Inspector
    public float delayBeforeFinalEvent = 2.0f; // Set your desired delay in seconds

    private bool finalEventTriggered = false;
    private Coroutine delayedFinalEventCoroutine = null;

    // --- Unity Lifecycle Methods ---

    private void Awake()
    {
        Rb = GetComponent<Rigidbody2D>();
        Coll = GetComponent<Collider2D>();
        Transform = transform;
        animator = GetComponent<Animator>();
        Rb.linearDamping = linearDrag;
        CurrentGravityScale = Rb.gravityScale;  
        lastInputDir = Vector2.right * Mathf.Sign(transform.localScale.x);
        if (interactionCheckPoint == null) interactionCheckPoint = transform;

        GroundedStateInstance = new GroundedState();
        AirborneStateInstance = new AirborneState();
        ClimbingStateInstance = new ClimbingState();
        PhasingStateInstance = new PhasingState();
        currentState = null;
        endGame = false;
        endGameInputTriggeredThisSession = false;
        isInFinalSceneWaitingForInput = false;
        finalInputProcessed = false;

        // --- SUBSCRIBE TO NEW EVENT ---
        EventManager.OnPlayersTeleportedForEndGame += PrepareForFinalInput;
        EventManager.OnDarkPlayerStuckInLight += OnThisPlayerStuckInLight;
        EventManager.OnGameEndedStep2 += HandleGameEndedStep2;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        // -----------------------------

        Debug.Log($"{gameObject.name} (Dark Player - StateMachine) Initialized.");
        
    }
    
    void OnActiveSceneChanged(Scene previousScene, Scene newScene)
    {
        Debug.Log($"DarkPlayer: Scene changed from '{previousScene.name}' to '{newScene.name}'");
        if (newScene.name == "Final") // <<< YOUR FINAL SCENE NAME HERE
        {
            EnterFinalSceneInputState();
        }
        else
        {
            // If loaded into any other scene (e.g., GameWorld from MainMenu), ensure not in final input mode
            isInFinalSceneWaitingForInput = false;
            finalInputProcessed = false;
            if (Rb != null) Rb.isKinematic = false; // Ensure physics is active for gameplay
            // Reset other relevant game state if necessary
        }
    }

    public void EnterFinalSceneInputState()
    {
        Debug.Log($"DarkPlayerController ({gameObject.name}): Entering FinalSceneInputState. Waiting for SPACE key.");
        isInFinalSceneWaitingForInput = true;
        finalInputProcessed = false; // Allow input for this new session in final scene

        // Make player static, disable normal controls/states
        if (Rb != null)
        {
            Rb.linearVelocity = Vector2.zero;
            Rb.isKinematic = true;
        }
        if (currentState != null && (currentState == GroundedStateInstance || currentState == AirborneStateInstance))
        {
            // Don't change state if already in a special state like Phasing/Climbing
        } else {
            ChangeState(GroundedStateInstance); // Or a specific "WaitingInFinalScene" state
        }


        // Optional: Set animator to a specific "waiting" pose for the final scene
        if (animator != null)
        {
            // Ensure not stuck in a "stuck" animation if carried over
            isCurrentlyStuckPlayingAnimation = false;
            animator.ResetTrigger(animParamStuckInLightTrigger); // Reset trigger if it was set
            // animator.SetBool(animParamIsStuckInLightBool, false); // If you were using a bool

            animator.SetFloat(animParamHorizontalSpeed, 0f);
            animator.SetFloat(animParamVerticalSpeed, 0f);
            animator.SetBool(animParamIsGrounded, true); // Assume standing pose
            // You might want a specific animation like "FinalSceneIdle"
            // animator.Play("FinalSceneIdle");
        }
    }


    private void OnDestroy()
    {
        EventManager.OnDarkPlayerStuckInLight -= OnThisPlayerStuckInLight;
        // EventManager.OnGameEndedStep2 -= HandleGameEndedStep2;
        // EventManager.OnPlayersTeleportedForEndGame -= PrepareForFinalInput;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

     private void Start()
     {
         CheckIfGrounded();
         if (!IsGrounded) { JumpsRemaining = 0; currentState = AirborneStateInstance; }
         else { JumpsRemaining = MAX_JUMPS; currentState = GroundedStateInstance; }
         currentState?.EnterState(this);
     }


     private void OnTriggerStay2D(Collider2D other)
     {
         if ( ((1 << other.gameObject.layer) & lightAreaLayer.value) != 0 )
         {
             CheckLightExposure(other); // Sets isInUnblockedLight = true if needed
         }
     }
     
     private void PrepareForFinalInput()
     {
         Debug.Log($"DarkPlayerController ({gameObject.name}): Received OnPlayersTeleportedForEndGame. Teleporting to end position.");
         transform.position = newPositionEnd; // Use the correct variable for final end position
         Rb.position = newPositionEnd;
         Rb.linearVelocity = Vector2.zero;
         Rb.isKinematic = true; 
    
         endGame = true; // Now enable waiting for 'any key'
         Debug.Log($"DarkPlayerController ({gameObject.name}): Teleported. 'endGame' flag set. Waiting for final input.");

         // Optional: Set animator to a specific pose
         if (animator != null)
         {
             animator.SetFloat(animParamHorizontalSpeed, 0f);
             animator.SetFloat(animParamVerticalSpeed, 0f);
             // animator.SetBool(animParamIsGrounded, true); // Or a specific "waiting" animation
         }
     }
     
     private void HandleGameEndedStep2()
     {
         transform.position = newPositionEnd;
         endGame = true;
     }

    private void CheckLightExposure(Collider2D lightAreaTrigger)
    {
        // Try to get the identifier script to find the light's actual source position
        LightSourceIdentifier lightSource = lightAreaTrigger.GetComponent<LightSourceIdentifier>();
        if (lightSource == null || lightSource.SourceTransform == null)
        {
            // If the trigger doesn't have the identifier, maybe use the trigger's center? Less accurate.
            // Or just ignore this light area trigger.
            // Debug.LogWarning($"Light Area trigger '{lightAreaTrigger.name}' missing LightSourceIdentifier.", lightAreaTrigger);
            return;
        }

        Vector2 lightOrigin = lightSource.SourceTransform.position;
        bool foundUnblockedLight = false;
        Vector2 cumulativePushDirection = Vector2.zero;

        if (lightCheckPoints == null || lightCheckPoints.Length == 0)
        {
             Debug.LogWarning($"DarkPlayerController on {gameObject.name} has no Light Check Points assigned!", this);
             return;
        }


        // Check each point on the player's body
        foreach (Transform checkPoint in lightCheckPoints)
        {
            if (checkPoint == null) continue; // Skip if a point wasn't assigned properly

            Vector2 playerPoint = checkPoint.position;
            Vector2 directionToLight = (lightOrigin - playerPoint).normalized;
            float distanceToLight = Vector2.Distance(playerPoint, lightOrigin);

            // Cast a line from the player check point towards the light source
            RaycastHit2D hit = Physics2D.Linecast(playerPoint, lightOrigin, lightBlockerLayer);

            Color rayColor = Color.yellow; // Default: Assume blocked or unclear

            // Analyze the hit result
            if (hit.collider == null)
            {
                // Nothing was hit on the LightBlocker layer between player and light source.
                // This means the light is UNBLOCKED for this check point.
                foundUnblockedLight = true;
                cumulativePushDirection += (playerPoint - lightOrigin).normalized; // Direction away from light
                rayColor = Color.red; // UNBLOCKED
                // Optional: break here if any unblocked point is enough to trigger reaction
                // break;
            }
            else
            {
                // Something on the LightBlocker layer was hit. Light is BLOCKED for this check point.
                 rayColor = Color.green; // BLOCKED
            }

#if UNITY_EDITOR
            Debug.DrawLine(playerPoint, lightOrigin, rayColor, 0.0f); // Make sure this line is active
#endif
        }

        // Update the player's overall state based on the checks
        if (foundUnblockedLight)
        {
            isInUnblockedLight = true;
            // Average the push directions (or just use direction from light source)
            lightPushDirection = (cumulativePushDirection.magnitude > 0.01f) ? cumulativePushDirection.normalized : (Vector2.right * -Mathf.Sign(lightOrigin.x - Rb.position.x)); // Fallback push direction
        }
        // If no unblocked light was found in THIS trigger area, isInUnblockedLight remains false
        // (it will be reset at the start of FixedUpdate)
    }

    private void Update()
    {
        if (isInFinalSceneWaitingForInput) // <<< CHECK NEW FLAG
        {
            if (!finalInputProcessed && Input.GetKeyDown(KeyCode.Space)) // Listen for SPACE specifically
            {
                Debug.Log($"DarkPlayerController ({gameObject.name}): SPACE key pressed in Final Scene. Triggering OnGameEndedFinal.");
                EventManager.TriggerOnGameEndedFinal();
                finalInputProcessed = true; // Prevent multiple triggers from one press or holding
                // Optionally, disable this script or further input processing here
                // this.enabled = false;
            }
            return; // Skip normal Update logic if in this special waiting state
        }
        if (endGame)
        {
            if (Input.anyKeyDown)
            {
                if (UnityEngine.EventSystems.EventSystem.current == null || 
                    !UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject)
                {
                    Debug.Log($"DarkPlayerController ({gameObject.name}): 'Any Key' pressed during (old) endGame. Triggering OnGameEndedFinalInputReceived (OBSOLETE PATH?).");
                    // EventManager.TriggerGameEndedFinalInputReceived(); // This was for the older sequence
                    endGame = false; 
                }
            }
            return; 
            /*if (Input.anyKeyDown)
            {
                // Check if a UI element is selected, if so, don't trigger.
                // This prevents UI clicks on potential "skip video" buttons from also triggering this.
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null)
                {
                    // A UI element is selected, probably a button from the video player UI
                    // Debug.Log("DarkPlayer: AnyKeyDown detected but UI element is selected. Ignoring for OnGameEndedFinalInputReceived.");
                }
                else
                {
                    Debug.Log($"DarkPlayerController ({gameObject.name}): 'Any Key' pressed during endGame. Triggering OnGameEndedFinalInputReceived.");
                    EventManager.TriggerGameEndedFinalInputReceived();
                    endGame = false; // Prevent re-triggering
                }
            }
            return; */
        }
        ReadInput();
        currentState?.HandleInput(this);

        if (currentState != PhasingStateInstance && currentState != ClimbingStateInstance)
        {
            HandleSpriteFlipping(HorizontalInput);
        }
        UpdateTimers();
        currentState?.UpdateLogic(this);

        // --- NEW: Update Animator Parameters ---
        UpdateAnimatorParameters();
        // -------------------------------------
    }

    private IEnumerator DelayedTriggerFinalEvent(float delay)
    {
        Debug.Log($"Waiting for {delay} seconds before triggering OnGameEndedFinal...");
        yield return new WaitForSeconds(delay); // Wait for the specified duration

        Debug.Log("Delay complete. Triggering EventManager.OnGameEndedFinal().");
        EventManager.TriggerOnGameEndedFinal();

        delayedFinalEventCoroutine = null; // Clear the coroutine reference
        // Optionally, you might want to disable this script or the GameObject now
        // this.enabled = false;
    }
    
    public void ResetFinalEventTrigger()
    {
        finalEventTriggered = false;
        if (delayedFinalEventCoroutine != null)
        {
            StopCoroutine(delayedFinalEventCoroutine);
            delayedFinalEventCoroutine = null;
        }
        Debug.Log("Final event trigger has been reset.");
    }
    
    private void FixedUpdate()
    {
        if (!finalEventTriggered && transform.position.x > TheEndPoint.position.x) // Use EndPoint.position.x
        {
            Debug.Log($"Condition met: Player X ({transform.position.x}) > EndPoint X ({TheEndPoint.position.x}). Starting delayed event trigger.");
            
            // Ensure we only start the coroutine once
            if (delayedFinalEventCoroutine == null)
            {
                delayedFinalEventCoroutine = StartCoroutine(DelayedTriggerFinalEvent(delayBeforeFinalEvent));
            }
            finalEventTriggered = true; // Mark that we've started the process
        }
            
        if (isInFinalSceneWaitingForInput || (endGame && Rb.isKinematic)) // Also check the old endGame if kinematic
        {
            if(!Rb.isKinematic) Rb.linearVelocity = Vector2.zero; // Ensure it's not moving if somehow not kinematic
            return;
        }
        
        bool applyLightForceThisFrame = isInUnblockedLight;
        isInUnblockedLight = false;

        bool previouslyGrounded = IsGrounded;
        CheckIfGrounded();
        HandleLandingAndCoyote(previouslyGrounded);

        bool isBeingPushedAndStuck = false;
        if (applyLightForceThisFrame)
        {
            SoundManagerForGamePlay.Instance.PlaySFX(hitSound);
            Rb.AddForce(lightPushDirection * lightPushForce, ForceMode2D.Force);
            if (Rb.linearVelocity.magnitude < stuckVelocityThreshold)
            {
                stuckInLightTimer += Time.fixedDeltaTime;
                if (stuckInLightTimer >= stuckInLightDuration)
                {
                    isBeingPushedAndStuck = true;
                }
            }
            else
            {
                stuckInLightTimer = 0f;
            }
        }
        else
        {
            stuckInLightTimer = 0f;
        }

        if (isBeingPushedAndStuck)
        {
            // --- MODIFICATION: Only set the Animator flag here ---
            if (animator != null && !isCurrentlyStuckPlayingAnimation) // Use script flag
            {
                Debug.Log($"Dark Player '{gameObject.name}' detected as stuck in light. Triggering 'StuckInLightTrigger' animation.");
                animator.SetTrigger(animParamStuckInLightTrigger); // <<< TRIGGER THE ANIMATION
                isCurrentlyStuckPlayingAnimation = true;          // <<< SET SCRIPT FLAG

                if (currentState != PhasingStateInstance && currentState != ClimbingStateInstance)
                {
                    Rb.linearVelocity = Vector2.zero;
                    // Consider disabling input or changing to a "stunned" player state here
                    // to prevent movement while the "stuck" animation plays.
                }
            }
            stuckInLightTimer = 0f; 
            // EventManager.TriggerDarkPlayerStuckInLight(this) is called by Animation Event.
        }
        // Removed else block for animator.SetBool(animParamIsStuckInLight, false);
        // This will be handled after teleport or by the state the player transitions to.

        currentState?.UpdatePhysics(this);

        if (!applyLightForceThisFrame && currentState != ClimbingStateInstance && currentState != PhasingStateInstance)
        {
            if (IsGrounded && currentState != GroundedStateInstance) ChangeState(GroundedStateInstance);
            else if (!IsGrounded && currentState == GroundedStateInstance && CoyoteTimeCounter <= 0) ChangeState(AirborneStateInstance);
        }
        if (!applyLightForceThisFrame && currentState != ClimbingStateInstance && currentState != PhasingStateInstance)
        {
            bool jumpExecuted = false;
            if ((JumpBufferCounter > 0f || JumpInputDownThisFrame) && CoyoteTimeCounter > 0f) { ExecuteJump(); jumpExecuted = true; }
            else if ((JumpBufferCounter > 0f || JumpInputDownThisFrame) && IsGrounded) { ExecuteJump(); jumpExecuted = true; }
            
            if (jumpExecuted && animator != null) // If a jump was executed this frame
            {
                animator.SetTrigger(animParamJumpTrigger); // <<< TRIGGER JUMP ANIMATION
            }

            if (jumpExecuted || JumpBufferCounter > 0f && !IsGrounded && CoyoteTimeCounter <= 0f) ResetJumpBuffer();
        }
    }
    
    public void TriggerStuckInLightEventFromAnimation()
    {
        Debug.Log($"DarkPlayerController ({gameObject.name}): AnimationEvent 'TriggerStuckInLightEventFromAnimation' called at end of death/stuck animation.");
        
        // --- MODIFIED BEHAVIOR ---
        // Instead of directly triggering the teleport and camera move (OnDarkPlayerStuckInLight),
        // we now trigger an event that the StuckEventUIManager will listen to.
        // The StuckEventUIManager will then handle the UI sequence (canvas, buttons, black screen, sound)
        // and after that sequence, it will trigger the OnDarkPlayerStuckInLight event.
        EventManager.TriggerShowStuckDecisionUI(this);
        // --- ----------------- ---
    }
    
    private void UpdateAnimatorParameters()
    {
        if (animator == null) return;

        // If the "StuckInLight" animation is playing, prevent normal movement animations.
        // isCurrentlyStuckPlayingAnimation is more reliable than GetCurrentAnimatorStateInfo if transitions are complex.
        if (isCurrentlyStuckPlayingAnimation || 
            currentState == PhasingStateInstance || 
            currentState == ClimbingStateInstance)
        {
            // If stuck, ensure horizontal speed is zeroed for animator if the animation itself doesn't do it
            // animator.SetFloat(animParamHorizontalSpeed, 0f); 
            return; 
        }

        animator.SetBool(animParamIsGrounded, IsGrounded);
        animator.SetFloat(animParamHorizontalSpeed, Mathf.Abs(Rb.linearVelocity.x));
        animator.SetFloat(animParamVerticalSpeed, Rb.linearVelocity.y);
    }

    private void OnThisPlayerStuckInLight(DarkPlayerController stuckPlayer)
    {
        if (stuckPlayer != this) return;
        Debug.Log($"DarkPlayerController ({gameObject.name}): Gameplay - Received OnDarkPlayerStuckInLight (from anim event). Teleporting (middle).");
        Vector2 oldPosition = transform.position;
        transform.position = newPositionMiddle; 
        Rb.position = newPositionMiddle;        
        Rb.linearVelocity = Vector2.zero; 
        Rb.angularVelocity = 0f;
        Debug.Log($"Dark Player '{gameObject.name}' teleported from {oldPosition} to {newPositionMiddle} (gameplay stuck).");
        isCurrentlyStuckPlayingAnimation = false; 
        if (animator != null) { animator.ResetTrigger(animParamStuckInLightTrigger); } // Reset trigger
        CheckIfGrounded();                        
        UpdateAnimatorParameters();               
        if (IsGrounded) ChangeState(GroundedStateInstance); else ChangeState(AirborneStateInstance);
    }
    
    public void EnterEndGameInputState()
    {
        Debug.Log($"DarkPlayerController ({gameObject.name}): Entering EndGameInputState. Waiting for any key.");
        this.endGame = true;
        this.Rb.isKinematic = true; // Ensure player is static
        this.Rb.linearVelocity = Vector2.zero;

        // Optional: Set animator to a specific "waiting for input" pose
        if (animator != null)
        {
            animator.SetFloat(animParamHorizontalSpeed, 0f);
            animator.SetFloat(animParamVerticalSpeed, 0f);
            // You might want a specific "EndWait" animation/state
            // animator.Play("EndGameWaitingPose"); 
        }
    }
    
    public void HandleStuckInLight() // This is the method you'll customize
    {
        Debug.Log($"DarkPlayerController ({gameObject.name}): Executing HandleStuckInLight().");

        // --- Implement your desired "stuck" behavior here ---
        // Examples:
        // 1. Play a "struggling" or "dissolving" animation.
        // 2. Make the player invulnerable for a short time if they "break free".
        // 3. Reduce player health (if applicable).
        // 4. Trigger a game over for this player.
        // 5. Teleport to a safe spot (similar to old RespawnPlayer but now event-driven).

        // For example, let's just stop movement and print a message for now.
        Rb.linearVelocity = Vector2.zero;
        Rb.angularVelocity = 0f;

        // Maybe change to a specific "StunnedByLightState" if you want complex behavior
        // ChangeState(StunnedByLightStateInstance); 

        // Or if you want to simply "kill" or reset the player:
        // SceneManager.LoadScene(SceneManager.GetActiveScene().name); // Simple level reset
        // Or call a GameManager to handle player death/respawn.

        Debug.LogWarning($"Dark Player '{gameObject.name}' is now officially handled as STUCK. Implement specific game logic here!");
    }
    // -----------------------------------------------------------

    /*
    // --- NEW Respawn Method ---
    private void RespawnPlayer()
    {
        // 1. Calculate Spawn Position (Current position + offset)
        Vector2 currentPosition = Rb.position;
        Vector2 spawnPosition = currentPosition + respawnOffset; // Apply the small offset

        // --- Safety Check: Ensure new position isn't inside a solid object ---
        // This is crucial to prevent respawning into another stuck state.
        // We can do a quick overlap check.
        Collider2D[] collidersAtSpawn = Physics2D.OverlapBoxAll(
            spawnPosition + (Vector2)Coll.offset, // Adjust for collider offset
            Coll.bounds.size,                     // Use player's collider size
            Transform.rotation.eulerAngles.z,     // Player's rotation
            solidLayers                           // Use the same solidLayers mask as phase ability
        );

        if (collidersAtSpawn.Length > 0)
        {
            Debug.LogWarning($"Respawn offset ({respawnOffset}) would place player inside a solid object. Trying opposite offset or a fallback.");
            // Option 1: Try opposite offset
            Vector2 alternativeSpawnPosition = currentPosition - respawnOffset;
            Collider2D[] collidersAtAlternativeSpawn = Physics2D.OverlapBoxAll(
                alternativeSpawnPosition + (Vector2)Coll.offset,
                Coll.bounds.size,
                Transform.rotation.eulerAngles.z,
                solidLayers
            );

            if (collidersAtAlternativeSpawn.Length == 0)
            {
                spawnPosition = alternativeSpawnPosition;
            }
            else
            {
                // Option 2: Fallback to a very small upward nudge if both fail, or a "safe spot" if you have one
                Debug.LogWarning("Both primary and alternative respawn offsets failed. Nudging slightly upwards from current.");
                spawnPosition = currentPosition + new Vector2(0, Coll.bounds.extents.y * 1.1f); // Nudge just above current
                // Or, you could teleport to a predefined safe spot if available in the scene/level data.
            }
        }
        // ---------------------------------------------------------------------

        // 2. Reset Physics State
        Rb.linearVelocity = Vector2.zero;
        Rb.angularVelocity = 0f;
        Rb.position = spawnPosition; // Instantly move to the (hopefully) safe offset position
        Rb.Sleep();
    
        // 3. Reset Internal Logic State
        stuckInLightTimer = 0f;
        isInUnblockedLight = false;
        ResetJumpBuffer();
        ResetCoyoteTimer();
        JumpsRemaining = MAX_JUMPS;
        // phaseCooldownTimer = 0f; // Optional

        // 4. Recalculate Grounding and Set State at Spawn Point
        CheckIfGrounded();
        if (IsGrounded)
        {
            ChangeState(GroundedStateInstance);
        }
        else
        {
            JumpsRemaining = 0;
            ChangeState(AirborneStateInstance);
        }

        Debug.Log($"Player respawned with offset at {spawnPosition}");
    }
    */
    

    // --- State Machine Management ---
    public void ChangeState(IPlayerState newState)
    {
        if (newState == null || newState == currentState) return;

        if (currentState == PhasingStateInstance && activePhaseCoroutine != null)
        {
            StopCoroutine(activePhaseCoroutine);
            activePhaseCoroutine = null;
        }

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

        if (currentState == ClimbingStateInstance) {
             VerticalInput = 0f;
             if (Input.GetKey(upKey)) VerticalInput = 1f;
             else if (Input.GetKey(downKey)) VerticalInput = -1f;
             if (Input.GetKeyDown(jumpKey)) { JumpInputDownThisFrame = true; }
             if (Input.GetKeyDown(interactKey)) { InteractInputDownThisFrame = true; }
             HorizontalInput = 0;
             JumpInputHeld = Input.GetKey(jumpKey);
             return;
        }
         if (currentState == PhasingStateInstance) {
              HorizontalInput = 0; VerticalInput = 0; JumpInputHeld = false;
              if (Input.GetKeyDown(interactKey)) { InteractInputDownThisFrame = true; }
              return;
         }

        HorizontalInput = 0f;
        if (Input.GetKey(leftKey)) HorizontalInput -= 1f;
        if (Input.GetKey(rightKey)) HorizontalInput += 1f;
        VerticalInput = 0f;
        if (Input.GetKey(upKey)) VerticalInput = 1f;
        JumpInputHeld = Input.GetKey(jumpKey);
        if (Input.GetKeyDown(jumpKey)) { JumpInputDownThisFrame = true; jumpBufferCounter = jumpBufferDuration; }
        if (Input.GetKeyDown(interactKey)) { InteractInputDownThisFrame = true; }
        if (Input.GetKeyDown(abilityKey)) { AbilityInputDownThisFrame = true; }

        bool directionKeyPressedThisFrame = false;
        if (Input.GetKeyDown(upKey))    { lastInputDir = Vector2.up; directionKeyPressedThisFrame = true; }
        if (Input.GetKeyDown(leftKey))  { lastInputDir = Vector2.left; directionKeyPressedThisFrame = true; }
        if (Input.GetKeyDown(rightKey)) { lastInputDir = Vector2.right; directionKeyPressedThisFrame = true; }
        if (!directionKeyPressedThisFrame && HorizontalInput != 0) { lastInputDir = Vector2.right * Mathf.Sign(HorizontalInput); }
        if (lastInputDir == Vector2.down || lastInputDir == Vector2.zero) { lastInputDir = Vector2.right * Mathf.Sign(Transform.localScale.x); }
    }

    // --- Timer Updates ---
    private void UpdateTimers()
    {
        if (coyoteTimeCounter > 0) coyoteTimeCounter -= Time.deltaTime;
        if (jumpBufferCounter > 0) jumpBufferCounter -= Time.deltaTime;
        if (phaseCooldownTimer > 0) phaseCooldownTimer -= Time.deltaTime;
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
        if (!previouslyGrounded && IsGrounded) { JumpsRemaining = MAX_JUMPS; coyoteTimeCounter = 0f; }
        else if (previouslyGrounded && !IsGrounded)
        {
             if (Rb.linearVelocity.y <= 0.01f) { coyoteTimeCounter = coyoteTimeDuration; }
             else { coyoteTimeCounter = 0f; }
        }
    }

    // --- Jump Execution ---
    private void ExecuteJump()
    {
        Rb.linearVelocity = new Vector2(Rb.linearVelocity.x, 0f);
        Rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        ResetCoyoteTimer();
        ResetJumpBuffer();
    }

    // --- State Callbacks / Helpers ---
    public void ResetJumpBuffer() { jumpBufferCounter = 0f; }
    public void ResetCoyoteTimer() { coyoteTimeCounter = 0f; }

    // --- Visuals ---
    public void HandleSpriteFlipping(float hInput)
    {
        if (currentState == PhasingStateInstance || currentState == ClimbingStateInstance) return;

        if (Mathf.Abs(hInput) > 0.01f)
        {
            Transform.localScale = new Vector3(Mathf.Sign(hInput) * Mathf.Abs(Transform.localScale.x), Transform.localScale.y, Transform.localScale.z);
        }
    }

    // --- Interaction ---
    public void HandleInteractionAttempt() // Called by States
    {
        // Prevent starting interaction if already in a special state
        if (currentState == ClimbingStateInstance || currentState == PhasingStateInstance) // Add Phasing check for Dark Player
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

            // --- Call Interact on the found object ---
            interactable.Interact(this.gameObject); // Call the Interact method via the interface

            // --- Handle Player State Change based on Type (If necessary) ---
            if (type == InteractionType.Trajectory || type == InteractionType.Ladder || type == InteractionType.Rope)
            {
                 // Get the specific component again if needed for state setup
                 TrajectoryMover trajectory = closestInteractableCollider.GetComponent<TrajectoryMover>();
                 if (trajectory != null)
                 {
                     ClimbingStateInstance.SetTrajectory(trajectory);
                     ChangeState(ClimbingStateInstance);
                 } else {
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
        // Check if currently in a state that needs stopping
        if (currentState == ClimbingStateInstance /* || currentState == RopeStateInstance etc. */)
        {

            // *** ADD THIS LINE: Trigger the stop event ***
            EventManager.TriggerInteractionStop(InteractionType.Trajectory, Rb);
            // *******************************************

            // Now change state. ExitState will still run and restore gravity.
            ChangeState(AirborneStateInstance);
        }
    }

    // --- Phase Ability Implementation ---
    public void TryActivatePhase()
    {
        if (phaseCooldownTimer <= 0f && currentState != PhasingStateInstance && currentState != ClimbingStateInstance)
        {
             if (lastInputDir == Vector2.down || lastInputDir == Vector2.zero) { lastInputDir = Vector2.right * Mathf.Sign(Transform.localScale.x); }
             PhasingStateInstance.SetPhaseDirection(lastInputDir);
             ChangeState(PhasingStateInstance);
        }
    }

    public void StartPhaseCoroutine(Vector2 direction)
    {
         if (activePhaseCoroutine != null) StopCoroutine(activePhaseCoroutine);
         activePhaseCoroutine = StartCoroutine(PhasePhysicsSequence(direction));
    }

    private IEnumerator PhasePhysicsSequence(Vector2 direction)
    {
        phaseCooldownTimer = phaseCooldown;
        float startTime = Time.time;

        Vector2 phaseDirectionVector = direction;
        Vector2 targetVelocity = phaseDirectionVector * phaseSpeed;
        Vector2 lastSafePosition = Rb.position;

        while (Time.time < startTime + phaseDuration)
        {
            if (currentState != PhasingStateInstance) { yield break; }

            Vector2 currentPosition = Rb.position;
            Vector2 nextPosition = currentPosition + targetVelocity * Time.fixedDeltaTime;

            RaycastHit2D solidCheck = Physics2D.Raycast(currentPosition, phaseDirectionVector,
                targetVelocity.magnitude * Time.fixedDeltaTime + Coll.bounds.extents.magnitude, solidLayers);
            if (solidCheck.collider != null && solidCheck.distance < Coll.bounds.extents.magnitude * 0.5f) { break; }

            Rb.MovePosition(nextPosition);
            lastSafePosition = nextPosition;
            yield return new WaitForFixedUpdate();
        }

         activePhaseCoroutine = null;

         if (currentState == PhasingStateInstance)
         {
              Vector2 endPosition = Rb.position;

              // --- Modified End Sequence ---
              // 1. Re-enable collider temporarily for depenetration checks
              if(Coll != null) Coll.enabled = true;

              // 2. Depenetration Check (using the now-enabled collider)
              Vector2 moveDirection = (endPosition - lastSafePosition).normalized;
              if (moveDirection == Vector2.zero) moveDirection = phaseDirectionVector;
              float checkDistance = Coll.bounds.extents.magnitude + depenetrationPadding; // Check slightly further back
              RaycastHit2D hit = Physics2D.Raycast(endPosition, -moveDirection, checkDistance, solidLayers);

              if (hit.collider != null)
              {
                  float pushDistance = checkDistance - hit.distance + depenetrationPadding;
                  Vector2 correction = -moveDirection * pushDistance;
                  Vector2 correctedPosition = endPosition + correction;

                   // Re-check overlap at corrected position (collider is enabled now)
                  Collider2D[] overlaps = Physics2D.OverlapBoxAll(correctedPosition + (Vector2)Coll.offset, Coll.bounds.size, Rb.rotation, solidLayers);
                  if (overlaps.Length == 0) {
                      Rb.position = correctedPosition; // Teleport only if correction is safe
                  }
                  else {
                       // Fallback: Try smaller correction or revert? Reverting might be safer.
                       // Rb.position = lastSafePosition; // Revert to last known good position
                       Rb.position = endPosition + correction * 0.5f; // Try half correction
                  }
              }
               // If no hit, endPosition is already safe relative to solids in the check direction

              // 3. Check if grounded AFTER potential position correction
              CheckIfGrounded();

              // 4. Change state (ExitState will restore collisions via SetLayerCollisions(false)
              // and re-enable collider if it was disabled by this method before ExitState ran)
              ChangeState(IsGrounded ? GroundedStateInstance : AirborneStateInstance);
              // -------------------------
         }
         // If state changed mid-coroutine, ChangeState already handled stopping this coroutine
         // and ExitState should have run.
    }


    // Method called by PhasingState to handle layer collisions
    public void SetLayerCollisions(bool ignore)
    {
        int playerLayer = gameObject.layer;
        if (layersToPhaseThrough.value == 0) {
            return;
        }
        for (int i = 0; i < 32; i++)
        {
            // Check if layer 'i' is IN the mask
            if (((1 << i) & layersToPhaseThrough.value) != 0)
            {
                Physics2D.IgnoreLayerCollision(playerLayer, i, ignore);

                // Log AFTER applying change to verify
                // bool nowIgnoring = Physics2D.GetIgnoreLayerCollision(playerLayer, i);
                // Debug.Log($"    -> After Setting, Now Ignoring: {nowIgnoring}");
            }
            // Optional: Log layers NOT in the mask
            // else if (LayerMask.LayerToName(i) != "") // Only log named layers
            // {
            //     Debug.Log($"Target Layer '{LayerMask.LayerToName(i)}' ({i}) is NOT in mask. Skipping.");
            // }
        }
    }


    // --- IMovementInputProvider Implementation ---
    public Vector2 GetMovementInput() { return new Vector2(HorizontalInput, VerticalInput); }

    // --- Gizmos ---
    /*
    private void OnDrawGizmosSelected()
    {
         if (Coll != null)
        {
            Bounds colliderBounds = Coll.bounds;
            Vector2 centerPoint = new Vector2(colliderBounds.center.x, colliderBounds.min.y);
            Vector2 leftOrigin = centerPoint + Vector2.left * footOffset;
            Vector2 rightOrigin = centerPoint + Vector2.right * footOffset;
            Vector2 centerOrigin = centerPoint;
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(leftOrigin, leftOrigin + Vector2.down * groundCheckDistance);
            Gizmos.DrawLine(centerOrigin, centerOrigin + Vector2.down * groundCheckDistance);
            Gizmos.DrawLine(rightOrigin, rightOrigin + Vector2.down * groundCheckDistance);
        }

         Transform pointToCheck = (interactionCheckPoint != null) ? interactionCheckPoint : Transform;
         Gizmos.color = Color.blue;
         Gizmos.DrawWireSphere(pointToCheck.position, interactionRadius);
    }
    */


    // --- Explicit Interface Implementations ---
    // These ensure the class satisfies the interfaces it declares.
    // Public getters above handle most of these implicitly if names match.
    // Only need explicit for properties/methods NOT defined publicly above OR if name differs.

    // IBasePlayerController
    float IBasePlayerController.MaxMoveSpeed => maxMoveSpeed;
    float IBasePlayerController.MoveAcceleration => moveAcceleration;
    float IBasePlayerController.MoveDeceleration => moveDeceleration;
    float IBasePlayerController.AirAcceleration => airAcceleration;
    float IBasePlayerController.AirDeceleration => airDeceleration;
    float IBasePlayerController.JumpForce => jumpForce;
    float IBasePlayerController.FallMultiplier => fallMultiplier;
    float IBasePlayerController.LowJumpMultiplier => lowJumpMultiplier;
    IPlayerState IBasePlayerController.CurrentState => currentState; // Use backing field
    IPlayerState IBasePlayerController.GroundedStateInstance => GroundedStateInstance;
    IPlayerState IBasePlayerController.AirborneStateInstance => AirborneStateInstance;

    // IPhasingController
    // SetLayerCollisions, StartPhaseCoroutine, TryActivatePhase are public methods above
    IPlayerState IPhasingController.PhasingStateInstance => PhasingStateInstance;

    // IInteractionController
    // HandleInteractionAttempt, ForceStopInteraction are public methods above
    LayerMask IInteractionController.InteractableLayer => interactableLayer;
    float IInteractionController.InteractionRadiusProp => interactionRadius;
    Transform IInteractionController.InteractionCheckPointProp => interactionCheckPoint;
    IPlayerState IInteractionController.ClimbingStateInstance => ClimbingStateInstance;

    // IMovementInputProvider is handled by public GetMovementInput() above
}
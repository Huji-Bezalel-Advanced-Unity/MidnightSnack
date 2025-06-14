// DarkPlayerController.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Animator))]
public class DarkPlayerController : MonoBehaviour, IPhasingController, IInteractionController, IMovementInputProvider
{
    [SerializeField] private AudioClip hitSound; 

    [SerializeField] private KeyCode leftKey = KeyCode.A;
    [SerializeField] private KeyCode rightKey = KeyCode.D;
    [SerializeField] private KeyCode upKey = KeyCode.W;
    [SerializeField] private KeyCode downKey = KeyCode.S; 
    [SerializeField] private KeyCode jumpKey = KeyCode.W;
    [SerializeField] private KeyCode abilityKey = KeyCode.LeftShift; 
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Respawn Settings")]
    [Tooltip("How long the player must be pushed by light AND nearly stationary to trigger respawn.")]
    [SerializeField] private float stuckInLightDuration = 0.5f;
    [Tooltip("The maximum speed magnitude considered 'stuck' while being pushed by light.")]
    [SerializeField] private float stuckVelocityThreshold = 0.1f;
    [Tooltip("The small offset to apply to the player's position on respawn (e.g., to nudge them out of the floor).")]
    [SerializeField] private Vector2 respawnOffset = new Vector2(0, 0.5f); 
    
    [Header("Light Interaction")]
    [Tooltip("Points on the player used to check for light exposure (e.g., feet, center, head). Create Empty GameObjects as children.")]
    [SerializeField] private Transform[] lightCheckPoints;
    [Tooltip("Layer mask containing the 'LightArea' trigger layer.")]
    [SerializeField] private LayerMask lightAreaLayer; 
    [Tooltip("Layer mask containing objects that block light (e.g., 'LightBlocker').")]
    [SerializeField] private LayerMask lightBlockerLayer; 
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
    
    

    public Rigidbody2D Rb { get; private set; }
    public Collider2D Coll { get; private set; }
    public Transform Transform { get; private set; }
    
    public LayerMask InteractableLayer => interactableLayer;
    public float InteractionRadiusProp => interactionRadius;
    public Transform InteractionCheckPointProp => interactionCheckPoint;

    private IPlayerState currentState; 
    public GroundedState GroundedStateInstance { get; private set; }
    public AirborneState AirborneStateInstance { get; private set; }
    public ClimbingState ClimbingStateInstance { get; private set; }
    public PhasingState PhasingStateInstance { get; private set; }

    public bool IsGrounded { get; private set; }
    public Vector2 GroundNormal { get; private set; }
    public int JumpsRemaining { get; private set; } // Always 0 or 1
    public float CurrentGravityScale { get; private set; }

    
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
    private readonly int animParamStuckInLightTrigger = Animator.StringToHash("StuckInLightTrigger"); // ONLY THE TRIGGER
    
    private bool isCurrentlyStuckPlayingAnimation = false;
    
    private const int MAX_JUMPS = 1;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private Vector2 lastInputDir = Vector2.right;
    private float phaseCooldownTimer = 0f;
    private Coroutine activePhaseCoroutine = null;
    
    private bool isInUnblockedLight = false;
    private Vector2 lightPushDirection = Vector2.zero;
    private float stuckInLightTimer = 0f; 

    private bool endGame = false;
    private bool endGameInputTriggeredThisSession = false;
    
    private bool isInFinalSceneWaitingForInput = false; 
    private bool finalInputProcessed = false;
    
    public Transform TheEndPoint; 
    public float delayBeforeFinalEvent = 2.0f; 

    private bool finalEventTriggered = false;
    private Coroutine delayedFinalEventCoroutine = null;


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

        EventManager.OnPlayersTeleportedForEndGame += PrepareForFinalInput;
        EventManager.OnDarkPlayerStuckInLight += OnThisPlayerStuckInLight;
        EventManager.OnGameEndedStep2 += HandleGameEndedStep2;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;

        Debug.Log($"{gameObject.name} (Dark Player - StateMachine) Initialized.");
        
    }
    
    void OnActiveSceneChanged(Scene previousScene, Scene newScene)
    {
        Debug.Log($"DarkPlayer: Scene changed from '{previousScene.name}' to '{newScene.name}'");
        if (newScene.name == "Final") 
        {
            EnterFinalSceneInputState();
        }
        else
        {
            isInFinalSceneWaitingForInput = false;
            finalInputProcessed = false;
            if (Rb != null) Rb.isKinematic = false; 
        }
    }

    public void EnterFinalSceneInputState()
    {
        Debug.Log($"DarkPlayerController ({gameObject.name}): Entering FinalSceneInputState. Waiting for SPACE key.");
        isInFinalSceneWaitingForInput = true;
        finalInputProcessed = false; 

        if (Rb != null)
        {
            Rb.linearVelocity = Vector2.zero;
            Rb.isKinematic = true;
        }
        if (currentState != null && (currentState == GroundedStateInstance || currentState == AirborneStateInstance))
        {
        } else {
            ChangeState(GroundedStateInstance); 
        }


        if (animator != null)
        {
            isCurrentlyStuckPlayingAnimation = false;
            animator.ResetTrigger(animParamStuckInLightTrigger); 


            animator.SetFloat(animParamHorizontalSpeed, 0f);
            animator.SetFloat(animParamVerticalSpeed, 0f);
            animator.SetBool(animParamIsGrounded, true); 
         
        }
    }


    private void OnDestroy()
    {
        EventManager.OnDarkPlayerStuckInLight -= OnThisPlayerStuckInLight;
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
             CheckLightExposure(other); 
         }
     }
     
     private void PrepareForFinalInput()
     {
         Debug.Log($"DarkPlayerController ({gameObject.name}): Received OnPlayersTeleportedForEndGame. Teleporting to end position.");
         transform.position = newPositionEnd; 
         Rb.position = newPositionEnd;
         Rb.linearVelocity = Vector2.zero;
         Rb.isKinematic = true; 
    
         endGame = true; 
         Debug.Log($"DarkPlayerController ({gameObject.name}): Teleported. 'endGame' flag set. Waiting for final input.");

         if (animator != null)
         {
             animator.SetFloat(animParamHorizontalSpeed, 0f);
             animator.SetFloat(animParamVerticalSpeed, 0f);
         }
     }
     
     private void HandleGameEndedStep2()
     {
         transform.position = newPositionEnd;
         endGame = true;
     }

    private void CheckLightExposure(Collider2D lightAreaTrigger)
    {
        LightSourceIdentifier lightSource = lightAreaTrigger.GetComponent<LightSourceIdentifier>();
        if (lightSource == null || lightSource.SourceTransform == null)
        {
            
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


        foreach (Transform checkPoint in lightCheckPoints)
        {
            if (checkPoint == null) continue; 

            Vector2 playerPoint = checkPoint.position;
            Vector2 directionToLight = (lightOrigin - playerPoint).normalized;
            float distanceToLight = Vector2.Distance(playerPoint, lightOrigin);

            RaycastHit2D hit = Physics2D.Linecast(playerPoint, lightOrigin, lightBlockerLayer);

            Color rayColor = Color.yellow; 

            if (hit.collider == null)
            {
     
                foundUnblockedLight = true;
                cumulativePushDirection += (playerPoint - lightOrigin).normalized; 
                rayColor = Color.red;
               
            }
            else
            {
                 rayColor = Color.green; 
            }

#if UNITY_EDITOR
            Debug.DrawLine(playerPoint, lightOrigin, rayColor, 0.0f); 
#endif
        }

        if (foundUnblockedLight)
        {
            isInUnblockedLight = true;
            lightPushDirection = (cumulativePushDirection.magnitude > 0.01f) ? cumulativePushDirection.normalized : (Vector2.right * -Mathf.Sign(lightOrigin.x - Rb.position.x)); // Fallback push direction
        }

    }

    private void Update()
    {
        if (isInFinalSceneWaitingForInput) 
        {
            if (!finalInputProcessed && Input.GetKeyDown(KeyCode.Space)) 
            {
                Debug.Log($"DarkPlayerController ({gameObject.name}): SPACE key pressed in Final Scene. Triggering OnGameEndedFinal.");
                EventManager.TriggerOnGameEndedFinal();
                finalInputProcessed = true; 
       
            }
            return; 
        }
        if (endGame)
        {
            if (Input.anyKeyDown)
            {
                if (UnityEngine.EventSystems.EventSystem.current == null || 
                    !UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject)
                {
                    Debug.Log($"DarkPlayerController ({gameObject.name}): 'Any Key' pressed during (old) endGame. Triggering OnGameEndedFinalInputReceived (OBSOLETE PATH?).");
                    endGame = false; 
                }
            }
            return; 
          
        }
        ReadInput();
        currentState?.HandleInput(this);

        if (currentState != PhasingStateInstance && currentState != ClimbingStateInstance)
        {
            HandleSpriteFlipping(HorizontalInput);
        }
        UpdateTimers();
        currentState?.UpdateLogic(this);

        UpdateAnimatorParameters();
    }

    private IEnumerator DelayedTriggerFinalEvent(float delay)
    {
        Debug.Log($"Waiting for {delay} seconds before triggering OnGameEndedFinal...");
        yield return new WaitForSeconds(delay); 

        Debug.Log("Delay complete. Triggering EventManager.OnGameEndedFinal().");
        EventManager.TriggerOnGameEndedFinal();

        delayedFinalEventCoroutine = null; 
      
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
        if (!finalEventTriggered && transform.position.x > TheEndPoint.position.x) 
        {
            Debug.Log($"Condition met: Player X ({transform.position.x}) > EndPoint X ({TheEndPoint.position.x}). Starting delayed event trigger.");
            
            if (delayedFinalEventCoroutine == null)
            {
                delayedFinalEventCoroutine = StartCoroutine(DelayedTriggerFinalEvent(delayBeforeFinalEvent));
            }
            finalEventTriggered = true; 
        }
            
        if (isInFinalSceneWaitingForInput || (endGame && Rb.isKinematic)) 
        {
            if(!Rb.isKinematic) Rb.linearVelocity = Vector2.zero; 
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
            if (animator != null && !isCurrentlyStuckPlayingAnimation) 
            {
                Debug.Log($"Dark Player '{gameObject.name}' detected as stuck in light. Triggering 'StuckInLightTrigger' animation.");
                animator.SetTrigger(animParamStuckInLightTrigger); 
                isCurrentlyStuckPlayingAnimation = true;          

                if (currentState != PhasingStateInstance && currentState != ClimbingStateInstance)
                {
                    Rb.linearVelocity = Vector2.zero;
                }
            }
            stuckInLightTimer = 0f; 
        }
     

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
            
            if (jumpExecuted && animator != null) 
            {
                animator.SetTrigger(animParamJumpTrigger); 
            }

            if (jumpExecuted || JumpBufferCounter > 0f && !IsGrounded && CoyoteTimeCounter <= 0f) ResetJumpBuffer();
        }
    }
    
    public void TriggerStuckInLightEventFromAnimation()
    {
        Debug.Log($"DarkPlayerController ({gameObject.name}): AnimationEvent 'TriggerStuckInLightEventFromAnimation' called at end of death/stuck animation.");

        EventManager.TriggerShowStuckDecisionUI(this);
    }
    
    private void UpdateAnimatorParameters()
    {
        if (animator == null) return;

        if (isCurrentlyStuckPlayingAnimation || 
            currentState == PhasingStateInstance || 
            currentState == ClimbingStateInstance)
        {
          
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
        this.Rb.isKinematic = true; 
        this.Rb.linearVelocity = Vector2.zero;

        if (animator != null)
        {
            animator.SetFloat(animParamHorizontalSpeed, 0f);
            animator.SetFloat(animParamVerticalSpeed, 0f);
 
        }
    }
    
    public void HandleStuckInLight() 
    {
        Debug.Log($"DarkPlayerController ({gameObject.name}): Executing HandleStuckInLight().");
        
        Rb.linearVelocity = Vector2.zero;
        Rb.angularVelocity = 0f;

        Debug.LogWarning($"Dark Player '{gameObject.name}' is now officially handled as STUCK. Implement specific game logic here!");
    }
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

    private void UpdateTimers()
    {
        if (coyoteTimeCounter > 0) coyoteTimeCounter -= Time.deltaTime;
        if (jumpBufferCounter > 0) jumpBufferCounter -= Time.deltaTime;
        if (phaseCooldownTimer > 0) phaseCooldownTimer -= Time.deltaTime;
    }

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

    private void ExecuteJump()
    {
        Rb.linearVelocity = new Vector2(Rb.linearVelocity.x, 0f);
        Rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        ResetCoyoteTimer();
        ResetJumpBuffer();
    }

    public void ResetJumpBuffer() { jumpBufferCounter = 0f; }
    public void ResetCoyoteTimer() { coyoteTimeCounter = 0f; }

    public void HandleSpriteFlipping(float hInput)
    {
        if (currentState == PhasingStateInstance || currentState == ClimbingStateInstance) return;

        if (Mathf.Abs(hInput) > 0.01f)
        {
            Transform.localScale = new Vector3(Mathf.Sign(hInput) * Mathf.Abs(Transform.localScale.x), Transform.localScale.y, Transform.localScale.z);
        }
    }

    public void HandleInteractionAttempt() 
    {
        if (currentState == ClimbingStateInstance || currentState == PhasingStateInstance) 
            return;

        Collider2D[] nearbyInteractables = Physics2D.OverlapCircleAll(
            InteractionCheckPointProp.position, InteractionRadiusProp, InteractableLayer
        );
        if (nearbyInteractables.Length == 0) return;

        Collider2D closestInteractableCollider = FindClosestInteractable(nearbyInteractables, InteractionCheckPointProp.position);

        IInteractable interactable = closestInteractableCollider.GetComponent<IInteractable>(); 

        if (interactable != null)
        {
            InteractionType type = interactable.InteractionType; 

            interactable.Interact(this.gameObject); 

            if (type == InteractionType.Trajectory || type == InteractionType.Ladder || type == InteractionType.Rope)
            {
                 TrajectoryMover trajectory = closestInteractableCollider.GetComponent<TrajectoryMover>();
                 if (trajectory != null)
                 {
                     ClimbingStateInstance.SetTrajectory(trajectory);
                     ChangeState(ClimbingStateInstance);
                 } else {
                 }
            }
        }

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

    public void ForceStopInteraction()
    {
        if (currentState == ClimbingStateInstance)
        {

            EventManager.TriggerInteractionStop(InteractionType.Trajectory, Rb);

            ChangeState(AirborneStateInstance);
        }
    }
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
              
              if(Coll != null) Coll.enabled = true;

              Vector2 moveDirection = (endPosition - lastSafePosition).normalized;
              if (moveDirection == Vector2.zero) moveDirection = phaseDirectionVector;
              float checkDistance = Coll.bounds.extents.magnitude + depenetrationPadding; 
              RaycastHit2D hit = Physics2D.Raycast(endPosition, -moveDirection, checkDistance, solidLayers);

              if (hit.collider != null)
              {
                  float pushDistance = checkDistance - hit.distance + depenetrationPadding;
                  Vector2 correction = -moveDirection * pushDistance;
                  Vector2 correctedPosition = endPosition + correction;

                  Collider2D[] overlaps = Physics2D.OverlapBoxAll(correctedPosition + (Vector2)Coll.offset, Coll.bounds.size, Rb.rotation, solidLayers);
                  if (overlaps.Length == 0) {
                      Rb.position = correctedPosition; 
                  }
                  else {
                 
                       Rb.position = endPosition + correction * 0.5f; 
                  }
              }
              CheckIfGrounded();

              ChangeState(IsGrounded ? GroundedStateInstance : AirborneStateInstance);
         }
  
    }

    public void SetLayerCollisions(bool ignore)
    {
        int playerLayer = gameObject.layer;
        if (layersToPhaseThrough.value == 0) {
            return;
        }
        for (int i = 0; i < 32; i++)
        {
            if (((1 << i) & layersToPhaseThrough.value) != 0)
            {
                Physics2D.IgnoreLayerCollision(playerLayer, i, ignore);

                
            }
            
        }
    }


    public Vector2 GetMovementInput() { return new Vector2(HorizontalInput, VerticalInput); }

    
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

    
    IPlayerState IPhasingController.PhasingStateInstance => PhasingStateInstance;

    
    LayerMask IInteractionController.InteractableLayer => interactableLayer;
    float IInteractionController.InteractionRadiusProp => interactionRadius;
    Transform IInteractionController.InteractionCheckPointProp => interactionCheckPoint;
    IPlayerState IInteractionController.ClimbingStateInstance => ClimbingStateInstance;

}
using UnityEngine;

// Interface defining what states need from the controller
public interface IPlayerControllerForStates
{
    // --- Core Components ---
    Rigidbody2D Rb { get; }
    Collider2D Coll { get; }
    Transform Transform { get; }

    // --- Current State Info ---
    bool IsGrounded { get; }
    Vector2 GroundNormal { get; }
    int JumpsRemaining { get; }
    IPlayerState CurrentState { get; }

    // --- Configuration Values ---
    float MaxMoveSpeed { get; }
    float MoveAcceleration { get; }
    float MoveDeceleration { get; }
    float AirAcceleration { get; }
    float AirDeceleration { get; }
    float JumpForce { get; }
    float DoubleJumpForce { get; }
    float FallMultiplier { get; }
    float LowJumpMultiplier { get; }

    // --- Input Values ---
    float HorizontalInput { get; }
    float VerticalInput { get; }
    bool JumpInputDownThisFrame { get; }
    bool JumpInputHeld { get; }
    bool InteractInputDownThisFrame { get; }
    float JumpBufferCounter { get; }
    float CoyoteTimeCounter { get; }
    Vector2 GetMovementInput();

    // --- Interaction Parameters ---
    LayerMask InteractableLayer { get; }
    float InteractionRadiusProp { get; }
    Transform InteractionCheckPointProp { get; }

    // --- State Management ---
    void ChangeState(IPlayerState newState);

    // --- State Helper Actions ---
    void ConsumeAirJump();
    void ResetJumpBuffer();
    void ResetCoyoteTimer();
    void HandleSpriteFlipping(float horizontalInput);
    void HandleInteractionAttempt(); // *** ADDED THIS LINE ***
    void SetLayerCollisions(bool ignore); // *** ADDED for Phase ***
    void StartPhaseCoroutine(Vector2 direction); // *** ADDED for Phase ***

    // --- State Instances (For transitions) ---
    IPlayerState GroundedStateInstance { get; }
    IPlayerState AirborneStateInstance { get; }
    IPlayerState ClimbingStateInstance { get; }
    IPlayerState PhasingStateInstance { get; }
    // Add other state instances here as needed
}
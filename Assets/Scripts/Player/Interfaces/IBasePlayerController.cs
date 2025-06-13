// IBasePlayerController.cs
using UnityEngine;

public interface IBasePlayerController
{
    // --- Core Components ---
    Rigidbody2D Rb { get; }
    Collider2D Coll { get; }
    Transform Transform { get; }

    // --- Current State Info ---
    bool IsGrounded { get; }
    Vector2 GroundNormal { get; }
    // JumpsRemaining moved to IDoubleJumpController
    IPlayerState CurrentState { get; }

    // --- Configuration Values (Common ones) ---
    float MaxMoveSpeed { get; } // ADDED
    float MoveAcceleration { get; } // ADDED
    float MoveDeceleration { get; } // ADDED
    float AirAcceleration { get; } // ADDED
    float AirDeceleration { get; } // ADDED
    float JumpForce { get; } // ADDED (Needed by Climbing jump-off)
    float FallMultiplier { get; }
    float LowJumpMultiplier { get; }

    // --- Input Values (Common ones) ---
    float HorizontalInput { get; }
    float VerticalInput { get; }
    bool JumpInputDownThisFrame { get; }
    bool JumpInputHeld { get; }
    bool InteractInputDownThisFrame { get; }
    bool AbilityInputDownThisFrame { get; }
    float JumpBufferCounter { get; }
    float CoyoteTimeCounter { get; }
    float CurrentGravityScale { get; }
    Vector2 GetMovementInput(); // Keep here

    // --- State Management ---
    void ChangeState(IPlayerState newState);

    // --- Basic State Helpers ---
    void ResetJumpBuffer();
    void ResetCoyoteTimer();
    void HandleSpriteFlipping(float horizontalInput);

    // --- State Instances (Needed for basic transitions) ---
    IPlayerState GroundedStateInstance { get; }
    IPlayerState AirborneStateInstance { get; }
    // Specific states like Climbing are on IInteractionController
}
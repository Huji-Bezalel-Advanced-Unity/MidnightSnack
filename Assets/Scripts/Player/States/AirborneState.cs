// AirborneState.cs
using UnityEngine;

// Use the BASE interface for general access
public class AirborneState : IPlayerState
{
    // Parameter changed to IBasePlayerController
    public void EnterState(IBasePlayerController controller) { /* ... */ }
    // Parameter changed to IBasePlayerController
    public void ExitState(IBasePlayerController controller) { /* ... */ }

    // Parameter changed to IBasePlayerController
    public void HandleInput(IBasePlayerController controller)
    {
        // Interaction Check
        if (controller.InteractInputDownThisFrame)
        {
            IInteractionController interactionController = controller as IInteractionController;
            if (interactionController != null) { interactionController.HandleInteractionAttempt(); }
        }

        // --- ADD PHASE ABILITY CHECK ---
        IPhasingController phasingController = controller as IPhasingController;
        if (phasingController != null && controller.AbilityInputDownThisFrame) // Check flag
        {
            // Tell the controller to try and start phasing
            phasingController.TryActivatePhase();
        }
        // -----------------------------

        // Double Jump check (only relevant for Light Player, safe to leave here)
        // IDoubleJumpController djController = controller as IDoubleJumpController;
        // if (djController != null && controller.JumpInputDownThisFrame && djController.JumpsRemaining > 0) { /* Intent noted */ }
    }

    // Parameter changed to IBasePlayerController
     public void UpdateLogic(IBasePlayerController controller)
     {
         // Landing transition handled by controller's FixedUpdate
     }

    // Parameter changed to IBasePlayerController
    public void UpdatePhysics(IBasePlayerController controller)
    {
        HandleAirMovement(controller);
        ApplyBetterGravity(controller);
    }

    // Parameter changed to IBasePlayerController
    private void HandleAirMovement(IBasePlayerController controller)
    {
        // Need specific move config values - cast or ensure they are on Base interface
        // --> Need AirAcceleration, AirDeceleration on IBasePlayerController interface!

        // Assuming they will be added:
        float targetSpeed = controller.HorizontalInput * controller.MaxMoveSpeed; // Assume MaxMoveSpeed on Base
        float accel = controller.AirAcceleration; // Assume this will be on Base
        float decel = controller.AirDeceleration; // Assume this will be on Base
        float accelerationRate = (Mathf.Abs(controller.HorizontalInput) > 0.01f) ? accel : decel;
        float currentVelocityX = controller.Rb.linearVelocity.x;
        float newVelocityX = Mathf.MoveTowards(currentVelocityX, targetSpeed, accelerationRate * Time.fixedDeltaTime);
        controller.Rb.linearVelocity = new Vector2(newVelocityX, controller.Rb.linearVelocity.y);
    }

    // Parameter changed to IBasePlayerController
    private void ApplyBetterGravity(IBasePlayerController controller)
    {
        Rigidbody2D rb = controller.Rb;
        // Need FallMultiplier, LowJumpMultiplier on Base interface
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (controller.FallMultiplier - 1) * Time.fixedDeltaTime; // Assume on Base
        }
        else if (rb.linearVelocity.y > 0 && !controller.JumpInputHeld)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (controller.LowJumpMultiplier - 1) * Time.fixedDeltaTime; // Assume on Base
        }
    }
}
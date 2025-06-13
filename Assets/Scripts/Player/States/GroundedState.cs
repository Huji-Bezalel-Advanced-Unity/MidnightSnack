// GroundedState.cs
using UnityEngine;

// Use the BASE interface for general access
public class GroundedState : IPlayerState
{
    // Parameter changed to IBasePlayerController
    public void EnterState(IBasePlayerController controller)
    {
        // Debug.Log("Entering Grounded State");
    }

    // Parameter changed to IBasePlayerController
    public void ExitState(IBasePlayerController controller)
    {
        // Debug.Log("Exiting Grounded State");
    }

    // Parameter changed to IBasePlayerController
    public void HandleInput(IBasePlayerController controller)
    {
        // Interaction Check
        if (controller.InteractInputDownThisFrame)
        {
            IInteractionController interactionController = controller as IInteractionController;
            if (interactionController != null) { interactionController.HandleInteractionAttempt(); }
            // Return here if interaction starts a state change? Maybe not needed yet.
        }

        // --- ADD PHASE ABILITY CHECK ---
        // Check if this controller *can* phase
        IPhasingController phasingController = controller as IPhasingController;
        if (phasingController != null && controller.AbilityInputDownThisFrame) // Check flag
        {
            // Tell the controller to try and start phasing
            phasingController.TryActivatePhase();
            // If TryActivatePhase changes state, subsequent checks might be skipped implicitly
        }
        // -----------------------------

        // Jump decision/buffer handled by controller's FixedUpdate
    }

    // Parameter changed to IBasePlayerController
     public void UpdateLogic(IBasePlayerController controller)
     {
         // Check for leaving ground is handled centrally by controller
     }

    // Parameter changed to IBasePlayerController
    public void UpdatePhysics(IBasePlayerController controller)
    {
        HandleGroundMovement(controller);

        // Stick to slopes slightly
        // Need to check buffer - assuming JumpBufferCounter is on IBasePlayerController now
        if (controller.JumpBufferCounter <= 0f && !controller.JumpInputDownThisFrame)
        {
            controller.Rb.AddForce(Vector2.down * 10f);
        }
    }

    // Parameter changed to IBasePlayerController
    private void HandleGroundMovement(IBasePlayerController controller)
    {
        // Need specific move config values - cast or ensure they are on Base interface
        // Let's assume MaxMoveSpeed, MoveAcceleration, MoveDeceleration ARE on IBasePlayerController
        // If not, add them to IBasePlayerController interface definition.
        // (Checking interface... they are NOT currently on IBasePlayerController)
        // --> Need to add them to IBasePlayerController interface definition!

        // For now, let's assume they will be added to IBasePlayerController:
        float targetSpeed = controller.HorizontalInput * controller.MaxMoveSpeed;
        float accel = controller.MoveAcceleration; // Assumes this will be on Base
        float decel = controller.MoveDeceleration; // Assumes this will be on Base
        float accelerationRate = (Mathf.Abs(controller.HorizontalInput) > 0.01f) ? accel : decel;
        float currentVelocityX = controller.Rb.linearVelocity.x;
        float newVelocityX = Mathf.MoveTowards(currentVelocityX, targetSpeed, accelerationRate * Time.fixedDeltaTime);
        controller.Rb.linearVelocity = new Vector2(newVelocityX, controller.Rb.linearVelocity.y);
    }
}
// ClimbingState.cs
using UnityEngine;

public class ClimbingState : IPlayerState
{
    private TrajectoryMover currentTrajectory;

    // Method to set the trajectory when entering the state
    public void SetTrajectory(TrajectoryMover trajectory)
    {
        currentTrajectory = trajectory;
    }

    public void EnterState(IBasePlayerController controller)
    {
        if (currentTrajectory == null) { /* Error handling & revert state */ return; }

        // *** CHANGE: Directly set gravity to 0. Don't store it here. ***
        // The TrajectoryMover's StartMovingFor already does this, but doing it here ensures it
        // happens even if StartMovingFor logic changes. It also signals the state's intent.
        controller.Rb.gravityScale = 0f;
        controller.Rb.linearVelocity = Vector2.zero;
    }

    public void ExitState(IBasePlayerController controller)
    {
        // *** CHANGE: Restore gravity using controller's stored default value ***
        if (controller.Rb != null)
        {
            // Use the default gravity scale stored in the controller
            controller.Rb.gravityScale = controller.CurrentGravityScale;
        }
        currentTrajectory = null;
    }

    public void HandleInput(IBasePlayerController controller)
    {
        // --- Stop Interaction Check ---
        // *** KEY CHANGE: Use INTERACT key to detach ***
        if (controller.InteractInputDownThisFrame)
        {
            StopInteraction(controller); // Call helper to stop interaction and change state
            // No jump-off force when using interact key to detach
            return; // Stop processing input for this frame
        }

        // *** JUMP key press is IGNORED for actions in this state ***
        // The continuous HOLDING of the Jump/Up key provides VerticalInput used in UpdatePhysics.
        // The GetKeyDown event (JumpInputDownThisFrame) doesn't trigger detach here anymore.
    }

    public void UpdateLogic(IBasePlayerController controller)
    {
        // Check if trajectory still exists
        IInteractionController interactionController = controller as IInteractionController;
        if (currentTrajectory == null && interactionController != null && controller.CurrentState == interactionController.ClimbingStateInstance)
        {
             StopInteraction(controller, false);
        }
    }

    public void UpdatePhysics(IBasePlayerController controller)
    {
        if (currentTrajectory == null) return;

        // --- Movement Along Trajectory ---
        // Get input FROM THE CONTROLLER (which implements IMovementInputProvider)
        Vector2 input = controller.GetMovementInput(); // Gets Vector2(Horizontal, Vertical)

        // Determine which axis (Vertical/Horizontal) controls movement based on the TrajectoryMover
        MovementAxis controlAxis = currentTrajectory.GetCalculatedAxis();
        // *** Use the input corresponding to the control axis ***
        float relevantInput = (controlAxis == MovementAxis.Vertical) ? input.y : input.x;

        float trajectoryMoveSpeed = currentTrajectory.MoveSpeed;
        float trajectoryPathLength = currentTrajectory.PathLength;

        if (trajectoryPathLength <= 0) return;

        // Calculate movement delta based on the relevant input axis
        float moveDelta = relevantInput * trajectoryMoveSpeed * Time.fixedDeltaTime;
        float deltaProgress = moveDelta / trajectoryPathLength;

        // Calculate current progress (more robustly)
        Vector3 currentPosOnPath = currentTrajectory.ProjectPointOnLineSegment(
                                        currentTrajectory.PathStart.position,
                                        currentTrajectory.PathEnd.position,
                                        controller.Rb.position);
        float currentProgress = 0f;
        if (trajectoryPathLength > 0) { // Avoid division by zero
             currentProgress = Vector3.Distance(currentTrajectory.PathStart.position, currentPosOnPath) / trajectoryPathLength;
             currentProgress = Mathf.Clamp01(currentProgress); // Clamp initial progress calculation too
        }


        float targetProgress = Mathf.Clamp01(currentProgress + deltaProgress);
        Vector3 newPos = Vector3.Lerp(currentTrajectory.PathStart.position, currentTrajectory.PathEnd.position, targetProgress);

        controller.Rb.MovePosition(newPos);

        // NOTE: Reaching the end of the ladder (progress 0 or 1 with zero input)
        // is handled by the TrajectoryMover's Update loop, which calls ForceStopController -> player.ForceStopInteraction -> ChangeState.
        // We don't need to explicitly check for reaching the end *here*.
    }

    // Helper to trigger stop event and change state
    private void StopInteraction(IBasePlayerController controller, bool triggerEvent = true)
    {
        if (triggerEvent && currentTrajectory != null)
        {
            EventManager.TriggerInteractionStop(InteractionType.Trajectory, controller.Rb);
        }
        IInteractionController interactionController = controller as IInteractionController;
        // Use BASE interface to get Airborne state instance
        if (interactionController != null) controller.ChangeState(controller.AirborneStateInstance);
        else controller.ChangeState(null);
    }
}
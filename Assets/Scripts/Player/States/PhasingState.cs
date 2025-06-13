// PhasingState.cs
using UnityEngine;
using System.Collections;

public class PhasingState : IPlayerState
{
    private Vector2 phaseDirection;
    private float originalGravityScale;

    public void SetPhaseDirection(Vector2 direction)
    {
        if (direction == Vector2.down || direction == Vector2.zero) { phaseDirection = Vector2.right; }
        else { phaseDirection = direction.normalized; }
    }

    public void EnterState(IBasePlayerController controller)
    {
        IPhasingController phasingController = controller as IPhasingController;
        if (phasingController == null) { /* Error handling */ return; }

        originalGravityScale = controller.Rb.gravityScale;
        controller.Rb.gravityScale = 0f;
        controller.Rb.linearVelocity = Vector2.zero;

        // --- Change: Disable Collider ---
        if (controller.Coll != null)
        {
            // Debug.Log("Disabling Player Collider for Phase");
            controller.Coll.enabled = false;
        }
        // --------------------------------

        // Keep IgnoreLayerCollision for now, although collider disabling might make it redundant
        phasingController.SetLayerCollisions(true);

        phasingController.StartPhaseCoroutine(phaseDirection);
        // Visual effects start
    }

    public void ExitState(IBasePlayerController controller)
    {
        IPhasingController phasingController = controller as IPhasingController;
        // Restore gravity/velocity first
        if (controller.Rb != null)
        {
             if (Mathf.Approximately(controller.Rb.gravityScale, 0f)) { controller.Rb.gravityScale = originalGravityScale; }
             controller.Rb.linearVelocity = Vector2.zero;
        }

        // --- Change: Re-enable Collider ---
        // Do this *before* restoring collisions, otherwise it might immediately collide if inside something
        if (controller.Coll != null)
        {
             // Debug.Log("Re-enabling Player Collider");
             controller.Coll.enabled = true;
             // Potential Issue: If player ends phase inside a wall, re-enabling collider here WILL cause problems.
             // The depenetration check in the controller's coroutine becomes even MORE important now.
        }
        // ---------------------------------

        // Restore collisions AFTER re-enabling collider (and after potential depenetration)
        if (phasingController != null) { phasingController.SetLayerCollisions(false); }

        // Visual effects end
    }

    // --- HandleInput, UpdateLogic, UpdatePhysics remain the same ---
    public void HandleInput(IBasePlayerController controller)
    {
        if (controller.InteractInputDownThisFrame) {
             // Debug.Log("Phase cancelled by Interact key.");
             controller.ChangeState(controller.AirborneStateInstance);
        }
    }
    public void UpdateLogic(IBasePlayerController controller) { }
    public void UpdatePhysics(IBasePlayerController controller) { }
}
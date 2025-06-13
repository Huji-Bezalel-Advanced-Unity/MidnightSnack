// IInteractionController.cs
using UnityEngine;

public interface IInteractionController : IBasePlayerController // Inherits base members
{
    // --- Interaction Config/Parameters ---
    LayerMask InteractableLayer { get; }
    float InteractionRadiusProp { get; }
    Transform InteractionCheckPointProp { get; }

    // --- Interaction Actions ---
    void HandleInteractionAttempt();  // Method state calls to try interacting
    void ForceStopInteraction();    // Method external objects (like TrajectoryMover) call

    // --- Interaction Specific State Instances ---
    IPlayerState ClimbingStateInstance { get; } // Reference to climbing/trajectory state
    // Add RopeStateInstance etc. if needed later
}
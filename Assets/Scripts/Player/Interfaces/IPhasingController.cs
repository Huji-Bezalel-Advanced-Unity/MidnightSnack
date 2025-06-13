// IPhasingController.cs
using UnityEngine;

public interface IPhasingController : IBasePlayerController // Inherits base members
{
    // --- Phasing Specific Actions ---
    void SetLayerCollisions(bool ignore);        // To enable/disable collisions
    void StartPhaseCoroutine(Vector2 direction); // To initiate the phase movement physics
    void TryActivatePhase();                     // Method state calls to request phase start

    // --- Phasing Specific State Instances ---
    IPlayerState PhasingStateInstance { get; } // Reference to the phasing state
}
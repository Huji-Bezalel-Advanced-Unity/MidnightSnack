// IDoubleJumpController.cs
using UnityEngine;

public interface IDoubleJumpController : IBasePlayerController // Inherits base members
{
    // --- Double Jump Specific Config ---
    float DoubleJumpForce { get; }

    // --- Double Jump Specific State Info ---
    int JumpsRemaining { get; } // Needed to check if air jump available

    // --- Double Jump Specific Actions ---
    void ConsumeAirJump(); // Method to decrement jump count
}
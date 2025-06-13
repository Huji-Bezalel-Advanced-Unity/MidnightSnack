using UnityEngine;

public interface IMovementInputProvider
{
    /// <summary>
    /// Provides the player's directional input axes.
    /// </summary>
    /// <returns>Vector2 containing Horizontal (-1 to 1) and Vertical (-1 to 1) axis values.</returns>
    Vector2 GetMovementInput();
}
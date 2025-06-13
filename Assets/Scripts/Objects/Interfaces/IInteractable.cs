// IInteractable.cs (Revised)
using UnityEngine;

public interface IInteractable
{
    InteractionType InteractionType { get; }

    /// <summary>
    /// Called when interaction is initiated with this object.
    /// </summary>
    /// <param name="initiatorGameObject">The GameObject initiating the interaction (e.g., Player, NPC).</param>
    void Interact(GameObject initiatorGameObject); // Changed parameter
}
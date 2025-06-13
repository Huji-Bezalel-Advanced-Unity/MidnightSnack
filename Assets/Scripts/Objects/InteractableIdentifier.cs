// InteractableIdentifier.cs
using UnityEngine;

/// <summary>
/// Simple component added to interactable GameObjects
/// to identify their InteractionType via an enum.
/// </summary>
public class InteractableIdentifier : MonoBehaviour
{
    [Tooltip("The type of interaction this object represents.")]
    public InteractionType Type = InteractionType.None;
}
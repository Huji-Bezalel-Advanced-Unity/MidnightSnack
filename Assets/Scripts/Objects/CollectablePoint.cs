// CollectablePoint.cs (Adapted for EventManager with Source)
using UnityEngine;
using UnityEngine.Events; // For optional feedback

/// <summary>
/// A point that can be collected by a specific player type (Light or Dark).
/// Sends an activation signal via EventManager upon collection.
/// Destroys itself after being collected.
///
/// Required Setup:
/// 1. Collider2D (set to Is Trigger = true).
/// 2. This CollectablePoint script.
/// 3. Player GameObjects must have either LightPlayerController or DarkPlayerController component.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CollectablePoint : MonoBehaviour
{
    public enum CollectableType { Light, Dark }

    [Header("Collection Settings")]
    [Tooltip("Which type of player can collect this point?")]
    [SerializeField] private CollectableType requiredPlayerType = CollectableType.Light;

    [Header("Event Manager Signal")]
    [Tooltip("The unique ID of the object this point activates via EventManager upon collection.")]
    [SerializeField] private string targetObjectID;

    [Header("Optional Feedback")]
    [Tooltip("Invoked when the point is successfully collected (before destruction).")]
    public UnityEvent OnCollectedFeedback;

    private bool isCollected = false;

    void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"CollectablePoint on '{gameObject.name}' requires Collider2D trigger.", this);
            col.isTrigger = true;
        }
        // Optional visual setup
    }

// In CollectablePoint.cs
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCollected) return;

        bool lightPlayerDetected = other.TryGetComponent<LightPlayerController>(out LightPlayerController lightController);
        bool darkPlayerDetected = other.TryGetComponent<DarkPlayerController>(out DarkPlayerController darkController);

        bool canCollect = (requiredPlayerType == CollectableType.Light && lightPlayerDetected) ||
                          (requiredPlayerType == CollectableType.Dark && darkPlayerDetected);

        if (canCollect)
        {
            Collect();
        }
        else
        {
            Debug.Log($"COLLECTABLE_DEBUG: '{gameObject.name}' cannot be collected by '{other.gameObject.name}' (Required: {requiredPlayerType}).");
        }
    }

    private void Collect()
    {
        isCollected = true;

        // Debug.Log($"CollectablePoint '{gameObject.name}' collected. Triggering Activate for ID: {targetObjectID}");

        // --- 1. Trigger EventManager Signal ---
        if (!string.IsNullOrEmpty(targetObjectID))
        {
            // --- Pass this.gameObject as the source ---
            EventManager.TriggerObjectActivate(targetObjectID, this.gameObject);
            // ------------------------------------------
            // Note: Still only sending Activate. Deactivate wouldn't make sense for a consumed item.
        }

        // --- 2. Trigger Optional Feedback ---
        OnCollectedFeedback?.Invoke();

        // --- 3. Destroy the Collectable Point ---
        Destroy(gameObject);
    }
}
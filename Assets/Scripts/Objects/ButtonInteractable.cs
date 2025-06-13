// ButtonInteractable.cs (Stateful - Sends Activate/Deactivate)
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class ButtonInteractable : MonoBehaviour, IInteractable
{
    [Header("Event Manager Settings")]
    [Tooltip("The unique ID of the object(s) this button should activate/deactivate via EventManager.")]
    [SerializeField] private string targetObjectID;

    [Header("Button State")]
    [Tooltip("Does the button start in the 'On' state? (Affects first signal sent)")]
    [SerializeField] private bool startsOn = false;
    // Optional: Add visual feedback for On/Off states if desired (like the Lever)
    // [SerializeField] private Sprite spriteOn;
    // [SerializeField] private Sprite spriteOff;
    // private SpriteRenderer spriteRenderer;

    [Header("Optional Feedback")]
    [Tooltip("Feedback when the button transitions to the ON state.")]
    public UnityEvent OnTurnedOnFeedback;
    [Tooltip("Feedback when the button transitions to the OFF state.")]
    public UnityEvent OnTurnedOffFeedback;

    private bool isOn; // Internal state tracking

    void Awake()
    {
        // spriteRenderer = GetComponent<SpriteRenderer>(); // Uncomment if using visual feedback
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) { Debug.LogError($"ButtonInteractable '{gameObject.name}' missing Collider2D.", this); }
        if (string.IsNullOrEmpty(targetObjectID)) { Debug.LogWarning($"ButtonInteractable '{gameObject.name}' has no Target Object ID.", this); }
    }

    void Start()
    {
        isOn = startsOn;
        UpdateVisualState(false); // Set initial visual state without triggering events
    }

    public InteractionType InteractionType => InteractionType.Button;

    public void Interact(GameObject initiatorGameObject)
    {
        if (string.IsNullOrEmpty(targetObjectID))
        {
            Debug.LogWarning($"Button '{gameObject.name}' pressed, but no Target Object ID!", this);
            return;
        }

        // Toggle the state
        isOn = !isOn;

        // Trigger events and update visuals based on the NEW state
        UpdateVisualState(true);
    }

    private void UpdateVisualState(bool triggerEventsAndFeedback)
    {
        // --- Optional Visual Update ---
        // if (spriteRenderer != null)
        // {
        //     spriteRenderer.sprite = isOn ? spriteOn : spriteOff;
        // }
        // --- End Optional Visual Update ---

        if (triggerEventsAndFeedback)
        {
            if (isOn)
            {
                Debug.Log($"Button '{gameObject.name}' toggled ON. Sending ACTIVATE for ID: '{targetObjectID}'");
                EventManager.TriggerObjectActivate(targetObjectID, this.gameObject);
                OnTurnedOnFeedback?.Invoke();
            }
            else
            {
                Debug.Log($"Button '{gameObject.name}' toggled OFF. Sending DEACTIVATE for ID: '{targetObjectID}'");
                EventManager.TriggerObjectDeactivate(targetObjectID, this.gameObject);
                OnTurnedOffFeedback?.Invoke();
            }
        }
    }
}
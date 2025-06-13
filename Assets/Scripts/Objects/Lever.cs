// Lever.cs (Modified Event Calls)
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class Lever : MonoBehaviour, IInteractable
{
    // ... (Fields, Awake, Start unchanged) ...
    [Header("Event Manager Settings")]
    [SerializeField] private string targetObjectID;
    [Header("Lever State & Visuals")]
    [SerializeField] private bool startsOn = false;
    [SerializeField] private Color colorStateOn = Color.green;
    [SerializeField] private Color colorStateOff = Color.red;
    [Header("Optional Feedback")]
    public UnityEvent OnSwitchedOnFeedback;
    public UnityEvent OnSwitchedOffFeedback;

    private bool isOn;
    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        // ... (get components, check setup) ...
        spriteRenderer = GetComponent<SpriteRenderer>();
        Collider2D col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger) col.isTrigger = true;
        if (string.IsNullOrEmpty(targetObjectID)) Debug.LogWarning($"Lever '{gameObject.name}' has no Target Object ID.", this);
    }
     void Start() { isOn = startsOn; UpdateVisualState(false); }


    public InteractionType InteractionType => InteractionType.Lever;

    public void Interact(GameObject initiatorGameObject)
    {
        if (string.IsNullOrEmpty(targetObjectID)) return;
        isOn = !isOn;
        UpdateVisualState(true); // Now triggers event within
    }

    private void UpdateVisualState(bool triggerEventsAndFeedback)
    {
        if (spriteRenderer != null) { spriteRenderer.color = isOn ? colorStateOn : colorStateOff; }

        if (triggerEventsAndFeedback)
        {
            if (isOn)
            {
                // --- Pass this.gameObject as the source ---
                EventManager.TriggerObjectActivate(targetObjectID, this.gameObject);
                // ------------------------------------------
                OnSwitchedOnFeedback?.Invoke();
            }
            else
            {
                 // --- Pass this.gameObject as the source ---
                EventManager.TriggerObjectDeactivate(targetObjectID, this.gameObject);
                 // ------------------------------------------
                OnSwitchedOffFeedback?.Invoke();
            }
        }
    }
}
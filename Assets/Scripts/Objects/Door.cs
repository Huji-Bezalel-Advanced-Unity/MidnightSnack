using UnityEngine;
using System.Collections;
using System.Collections.Generic; // Required for HashSet

[RequireComponent(typeof(Collider2D))]
public class Door : MonoBehaviour
{
    [SerializeField] private AudioClip doorSound;

    public enum DoorMode { UntimedToggle, TimedOpen }
    public enum GateMovementDirection { Up, Down, Left, Right }

    [Header("Door Identification & Activation")]
    [SerializeField] private string doorID;
    [SerializeField] [Min(1)] private int requiredActivations = 1;

    [Header("Door Behaviour")]
    [SerializeField] private DoorMode doorMode = DoorMode.UntimedToggle;
    [SerializeField] private float timedOpenDuration = 3.0f;
    [SerializeField] private bool startsOpen = false;
    [SerializeField] private bool stayOpenPermanently = false;

    [Header("Gate Movement")]
    [SerializeField] private GateMovementDirection openDirection = GateMovementDirection.Up;
    [SerializeField] private float moveDistance = 2.0f;
    [SerializeField] private float moveSpeed = 3.0f;
    [SerializeField] private bool instantMove = false;

    [Header("Components")]
    [SerializeField] private Collider2D doorCollider;
    [SerializeField] private SpriteRenderer doorRenderer;

    [Header("Visuals (Optional Color Change)")]
    [SerializeField] private Color openColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
    [SerializeField] private Color closedColor = Color.white;

    private bool isOpen = false;
    private Coroutine timedCloseCoroutine = null;
    private HashSet<GameObject> activeSources = new HashSet<GameObject>();
    private bool isPermanentlyOpen = false;
    private bool initialStartsOpenState;

    private Vector3 closedPosition;
    private Vector3 openPosition;
    private Coroutine activeMoveCoroutine = null;

    void Awake()
    {
        initialStartsOpenState = startsOpen;

        if (doorCollider == null) doorCollider = GetComponent<Collider2D>();
        if (doorRenderer == null) doorRenderer = GetComponent<SpriteRenderer>();
        if (doorRenderer == null && !instantMove) Debug.LogWarning($"Door '{gameObject.name}' has no SpriteRenderer but is not set to instantMove.", this);
        if (doorCollider == null) { Debug.LogError($"Door '{gameObject.name}' needs a Collider2D!", this); enabled = false; return; }
        if (string.IsNullOrEmpty(doorID)) { Debug.LogError($"Door '{gameObject.name}' needs a Door ID!", this); enabled = false; return; }

        closedPosition = transform.position;
        CalculateOpenPosition();
        ResetStateInternal();
    }

    private void CalculateOpenPosition()
    {
        Vector3 moveOffset = Vector3.zero;
        switch (openDirection)
        {
            case GateMovementDirection.Up: moveOffset = Vector3.up * moveDistance; break;
            case GateMovementDirection.Down: moveOffset = Vector3.down * moveDistance; break;
            case GateMovementDirection.Left: moveOffset = Vector3.left * moveDistance; break;
            case GateMovementDirection.Right: moveOffset = Vector3.right * moveDistance; break;
        }
        openPosition = closedPosition + moveOffset;
    }

    void OnEnable()
    {
        if (!isPermanentlyOpen || !stayOpenPermanently)
        {
            EventManager.OnObjectActivate += HandleActivation;
            EventManager.OnObjectDeactivate += HandleDeactivation;
        }
    }

    void OnDisable()
    {
        EventManager.OnObjectActivate -= HandleActivation;
        EventManager.OnObjectDeactivate -= HandleDeactivation;
        StopRunningCoroutine();
    }

    private void HandleActivation(string receivedID, GameObject source)
    {
        if (stayOpenPermanently && isPermanentlyOpen) return;

        if (enabled && !string.IsNullOrEmpty(doorID) && doorID == receivedID)
        {
            activeSources.Add(source);

            if (activeSources.Count >= requiredActivations && !isOpen)
            {
                OpenDoor();
                if (doorMode == DoorMode.TimedOpen && !stayOpenPermanently)
                {
                    StartTimer();
                }
            }
            else if (activeSources.Count >= requiredActivations && isOpen && doorMode == DoorMode.TimedOpen && !stayOpenPermanently)
            {
                StartTimer();
            }
        }
    }

    private void HandleDeactivation(string receivedID, GameObject source)
    {
        if (stayOpenPermanently && isPermanentlyOpen) return;

        if (source != null && enabled && !string.IsNullOrEmpty(doorID) && doorID == receivedID)
        {
            bool wasRequirementMet = activeSources.Count >= requiredActivations;
            bool removed = activeSources.Remove(source);

            if (removed && wasRequirementMet && activeSources.Count < requiredActivations && isOpen && !stayOpenPermanently)
            {
                CloseDoor();
            }
        }
    }

    private void OpenDoor(bool logAction = true)
    {
        if (isOpen && logAction && !isPermanentlyOpen) return;
        if (logAction && !(stayOpenPermanently && isPermanentlyOpen)) Debug.Log($"Door '{gameObject.name}' Opening. Requirement met.");

        isOpen = true;
        StopRunningCoroutine();
        UpdateVisualAndPhysicsState();

        if (stayOpenPermanently && !isPermanentlyOpen)
        {
            isPermanentlyOpen = true;
            Debug.Log($"Door '{gameObject.name}' is now permanently open.");
            EventManager.OnObjectActivate -= HandleActivation;
            EventManager.OnObjectDeactivate -= HandleDeactivation;
        }
    }

    private void CloseDoor(bool logAction = true)
    {
        if (stayOpenPermanently && isPermanentlyOpen) return;
        if (!isOpen && logAction) return;

        if (logAction) Debug.Log($"Door '{gameObject.name}' Closing. Requirement lost or timed out.");
        isOpen = false;
        StopRunningCoroutine();
        UpdateVisualAndPhysicsState();
    }

    private void UpdateVisualAndPhysicsState()
    {
        if (doorCollider != null) doorCollider.enabled = !isOpen;
        if (doorRenderer != null) doorRenderer.color = isOpen ? openColor : closedColor;

        if (doorSound != null)
            SoundManagerForGamePlay.Instance?.PlaySFX(doorSound);
        else
            SoundManagerForGamePlay.Instance?.PlayDoorMoveSound();

        if (activeMoveCoroutine != null)
        {
            StopCoroutine(activeMoveCoroutine);
            activeMoveCoroutine = null;
        }

        Vector3 targetPosition = isOpen ? openPosition : closedPosition;

        if (instantMove)
        {
            transform.position = targetPosition;
        }
        else
        {
            activeMoveCoroutine = StartCoroutine(MoveToPosition(targetPosition));
        }
    }

    private IEnumerator MoveToPosition(Vector3 target)
    {
        while (Vector3.Distance(transform.position, target) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = target;
        activeMoveCoroutine = null;
    }

    private void StartTimer()
    {
        if (stayOpenPermanently && isPermanentlyOpen) return;

        StopRunningCoroutine();
        timedCloseCoroutine = StartCoroutine(TimedCloseRoutine());
    }

    private void StopRunningCoroutine()
    {
        if (timedCloseCoroutine != null)
        {
            StopCoroutine(timedCloseCoroutine);
            timedCloseCoroutine = null;
        }
    }

    private IEnumerator TimedCloseRoutine()
    {
        yield return new WaitForSeconds(timedOpenDuration);

        if (stayOpenPermanently && isPermanentlyOpen) yield break;

        if (doorMode == DoorMode.TimedOpen)
        {
            CloseDoor();
        }
    }

    private void ResetStateInternal()
    {
        StopRunningCoroutine();
        if (activeMoveCoroutine != null) StopCoroutine(activeMoveCoroutine);
        activeMoveCoroutine = null;

        activeSources.Clear();
        isPermanentlyOpen = false;

        isOpen = initialStartsOpenState;
        if (isOpen && requiredActivations > 0)
        {
            isOpen = false;
        }

        transform.position = closedPosition;
        CalculateOpenPosition();
        UpdateVisualAndPhysicsState();

        if (gameObject.activeInHierarchy && this.enabled)
        {
            OnDisable();
            OnEnable();
        }
    }
}

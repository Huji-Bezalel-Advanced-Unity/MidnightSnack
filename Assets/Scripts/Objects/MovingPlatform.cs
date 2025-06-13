// MovingPlatform.cs (כולל סאונד תנועה)
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class MovingPlatform : MonoBehaviour
{
    public enum PlatformActivationMode { SingleTrip, StartContinuousLoop }

    [Header("Activation Behaviour")]
    [SerializeField] private PlatformActivationMode activationMode = PlatformActivationMode.SingleTrip;
    [SerializeField] private bool startActivated = false;

    [Header("Movement Settings")]
    [SerializeField] private Vector2 movementDisplacement = Vector2.right * 5f;
    [SerializeField] private float speed = 2f;
    [SerializeField] private float pauseAtEnds = 0.5f;

    [Header("Setup (Optional)")]
    [SerializeField] private Transform startPointMarker;
    [SerializeField] private Rigidbody2D rb;

    [Header("Event Settings")]
    [SerializeField] private string platformID;
    [SerializeField] [Min(1)] private int requiredActivations = 1;
    [SerializeField] private bool listenForActivate = true;
    [SerializeField] private bool listenForDeactivate = true;
    [SerializeField] private bool ignoreDeactivationAfterActivation = false;

    [Header("Audio")]
    [SerializeField] private AudioClip movementSound;

    private Vector2 startPoint;
    private Vector2 endPoint;
    private Vector2 currentTargetPoint;
    private bool isMoving = false;
    private bool isLooping = false;
    private bool movingToEndLoop = true;
    private bool isPaused = false;
    private float pauseTimer = 0f;
    private HashSet<GameObject> activeSources = new HashSet<GameObject>();
    private bool activationRequirementMetOnce = false;

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (rb != null) { rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation2D.Interpolate; rb.freezeRotation = true; }
        else { Debug.LogError($"Platform '{gameObject.name}' needs RB2D", this); enabled = false; return; }

        if (startPointMarker != null) startPoint = startPointMarker.position;
        else startPoint = rb.position;

        if (movementDisplacement.sqrMagnitude < 0.001f)
        {
            Debug.LogWarning($"Platform '{gameObject.name}': movementDisplacement is very small or zero. Platform will not move.", this);
            endPoint = startPoint;
        }
        else
        {
            endPoint = startPoint + movementDisplacement;
        }

        activationRequirementMetOnce = false;
        activeSources.Clear();
        isMoving = false;
        isLooping = false;
        currentTargetPoint = startPoint;
        rb.position = startPoint;
        movingToEndLoop = true;
        isPaused = false;
        pauseTimer = 0f;

        if (string.IsNullOrEmpty(platformID) && (listenForActivate || listenForDeactivate))
            Debug.LogWarning($"Platform '{gameObject.name}' listens for events but has no Platform ID.", this);
        if (requiredActivations > 1 && !listenForActivate && !startActivated)
            Debug.LogWarning($"Platform '{gameObject.name}' needs {requiredActivations} activations but doesn't listen for Activate and doesn't start activated.", this);
        if (ignoreDeactivationAfterActivation && !listenForDeactivate)
            Debug.LogWarning($"Platform '{gameObject.name}' ignoreDeactivation flag ineffective without listening for Deactivate.", this);
        if (speed <= 0f)
            Debug.LogWarning($"Platform '{gameObject.name}' has speed <= 0. It might not move as expected.", this);
    }

    void Start()
    {
        if (startActivated)
        {
            Debug.Log($"Platform '{gameObject.name}' starting activated.");
            activationRequirementMetOnce = true;

            if (movementDisplacement.sqrMagnitude < 0.001f) {
                Debug.Log($"Platform '{gameObject.name}' startActivated is true, but movementDisplacement is zero. No movement initiated.", this);
                return;
            }

            if (activationMode == PlatformActivationMode.SingleTrip)
            {
                StartSingleTripToEnd();
            }
            else
            {
                StartLoopingMovement();
            }
        }
    }

    void OnEnable()
    {
        if (listenForActivate) EventManager.OnObjectActivate += HandleActivation;
        if (listenForDeactivate) EventManager.OnObjectDeactivate += HandleDeactivation;
    }

    void OnDisable()
    {
        EventManager.OnObjectActivate -= HandleActivation;
        EventManager.OnObjectDeactivate -= HandleDeactivation;
        activeSources.Clear();
    }

    void FixedUpdate()
    {
        if (rb == null || !isMoving || movementDisplacement.sqrMagnitude < 0.001f) return;

        if (isPaused)
        {
            pauseTimer -= Time.fixedDeltaTime;
            if (pauseTimer <= 0f)
            {
                isPaused = false;
                if (isLooping)
                {
                    currentTargetPoint = movingToEndLoop ? endPoint : startPoint;
                }
            }
            return;
        }

        Vector2 currentPosition = rb.position;
        Vector2 newPosition = Vector2.MoveTowards(currentPosition, currentTargetPoint, speed * Time.fixedDeltaTime);
        rb.MovePosition(newPosition);

        if (Vector2.Distance(newPosition, currentTargetPoint) < 0.01f)
        {
            rb.position = currentTargetPoint;
            if (isLooping)
            {
                movingToEndLoop = !movingToEndLoop;
                currentTargetPoint = movingToEndLoop ? endPoint : startPoint;
                if (pauseAtEnds > 0f)
                {
                    isPaused = true;
                    pauseTimer = pauseAtEnds;
                }
            }
            else
            {
                isMoving = false;
                if (pauseAtEnds > 0f)
                {
                    isPaused = true;
                    pauseTimer = pauseAtEnds;
                }
            }
        }
    }

    private void HandleActivation(string receivedID, GameObject source)
    {
        if (!enabled || !listenForActivate || string.IsNullOrEmpty(platformID) || platformID != receivedID || movementDisplacement.sqrMagnitude < 0.001f) return;

        bool alreadyMet = activeSources.Count >= requiredActivations;
        activeSources.Add(source);

        if (activeSources.Count >= requiredActivations && !alreadyMet)
        {
            activationRequirementMetOnce = true;
            if (activationMode == PlatformActivationMode.SingleTrip)
            {
                if (Vector2.Distance(rb.position, startPoint) < 0.1f && !isMoving && !isPaused)
                {
                    StartSingleTripToEnd();
                }
            }
            else
            {
                if (!isMoving && !isPaused)
                {
                    StartLoopingMovement();
                }
            }
        }
    }

    private void HandleDeactivation(string receivedID, GameObject source)
    {
        if (!enabled || !listenForDeactivate || string.IsNullOrEmpty(platformID) || platformID != receivedID || movementDisplacement.sqrMagnitude < 0.001f) return;

        bool wasRequirementMet = activeSources.Count >= requiredActivations;
        bool removed = activeSources.Remove(source);

        if (removed && wasRequirementMet && activeSources.Count < requiredActivations)
        {
            if (ignoreDeactivationAfterActivation && activationRequirementMetOnce) return;
            if (!ignoreDeactivationAfterActivation) activationRequirementMetOnce = false;

            if (activationMode == PlatformActivationMode.SingleTrip)
            {
                if (Vector2.Distance(rb.position, endPoint) < 0.1f && !isMoving && !isPaused)
                {
                    StartReturnTripToStart();
                }
            }
            else
            {
                if (isMoving || isPaused)
                {
                    StopLoopingMovement();
                }
            }
        }
    }

    private void StartSingleTripToEnd()
    {
        Debug.Log($"MovingPlatform '{gameObject.name}' Single Trip: Moving to End Point (B). Target: {endPoint}");
        isLooping = false;
        currentTargetPoint = endPoint;
        isMoving = true;
        isPaused = false;
        movingToEndLoop = true;

        if (movementSound != null)
            SoundManagerForGamePlay.Instance?.PlaySFX(movementSound);
        else
            SoundManagerForGamePlay.Instance?.PlayPlatformMoveSound();
    }

    private void StartReturnTripToStart()
    {
        Debug.Log($"MovingPlatform '{gameObject.name}' Single Trip: Returning to Start Point (A). Target: {startPoint}");
        isLooping = false;
        currentTargetPoint = startPoint;
        isMoving = true;
        isPaused = false;
        movingToEndLoop = false;

        if (movementSound != null)
            SoundManagerForGamePlay.Instance?.PlaySFX(movementSound);
        else
            SoundManagerForGamePlay.Instance?.PlayPlatformMoveSound();
    }

    private void StartLoopingMovement()
    {
        if (isMoving) return;

        Debug.Log($"MovingPlatform '{gameObject.name}' Continuous Loop: Starting.");
        isLooping = true;

        float distToA = Vector2.Distance(rb.position, startPoint);
        float distToB = Vector2.Distance(rb.position, endPoint);

        if (distToA < distToB)
        {
            movingToEndLoop = true;
            currentTargetPoint = endPoint;
        }
        else
        {
            movingToEndLoop = false;
            currentTargetPoint = startPoint;
        }

        isMoving = true;
        isPaused = false;

        if (movementSound != null)
            SoundManagerForGamePlay.Instance?.PlaySFX(movementSound);
        else
            SoundManagerForGamePlay.Instance?.PlayPlatformMoveSound();
    }

    private void StopLoopingMovement()
    {
        Debug.Log($"MovingPlatform '{gameObject.name}' Continuous Loop: Stopping.");
        isLooping = false;
        isMoving = false;
        isPaused = false;
    }

    // Gizmos remain unchanged...
}

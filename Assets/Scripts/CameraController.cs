using System;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("List of Transforms defining the boundaries. N sections require N+1 points. Point[i] is start of Section i, Point[i+1] is end of Section i.")]
    public List<Transform> sectionBoundaryPoints = new List<Transform>();
    [SerializeField] private Transform playerTransform1;
    [SerializeField] private Transform playerTransform2;
    private Camera mainCam;

    [Header("Player Following")]
    [SerializeField] private float followSmoothTime = 0.3f;
    [SerializeField] private Vector2 followOffset = Vector2.zero;

    [Header("Section Transitions")]
    [SerializeField] private float viewCenterTransitionSmoothTime = 0.8f;
    [SerializeField] private float viewCenterTransitionTimeout = 2.0f; // This field is correctly present
    [SerializeField] private float delayBeforeFullTransition = 0.5f;

    [Header("Dynamic Zoom (Orthographic)")]
    [SerializeField] private bool enableDynamicZoom = false;
    [SerializeField] private float minOrthographicSize = 5f;
    [SerializeField] private float maxOrthographicSize = 10f;
    [SerializeField] private float minPlayerDistanceForZoom = 2f;
    [SerializeField] private float maxPlayerDistanceForZoom = 15f;
    [SerializeField] private float zoomSmoothTime = 0.5f;

    // Internal State
    private float _cameraHalfWidth;
    private float _targetOrthographicSize;
    private float _orthographicSizeVelocity = 0f;
    private Vector3 _cameraMoveVelocity = Vector3.zero;

    private int _currentActiveSectionIndex = 0;
    private List<bool> _sectionTasksCompleted;

    private bool _isTransitioningToNewSectionCenter = false;
    private Vector3 _targetPositionForViewCenterTransition;
    private float _playerCrossBoundaryTimer = 0f;
    private bool _arePlayersBeyondCurrentSectionCommitLine = false;

    private float _minCameraCenterX;
    private float _maxCameraCenterX;
    
    private float _currentPanTimer = 0f; // This field is correctly present

    void Start()
    {
        mainCam = GetComponentInChildren<Camera>();
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) { Debug.LogError("Camera: No Camera component found!", this); enabled = false; return; }
        if (!mainCam.orthographic && enableDynamicZoom) { Debug.LogWarning("Camera: Dynamic Zoom is designed for an Orthographic camera. Disabling zoom.", this); enableDynamicZoom = false; }

        if (sectionBoundaryPoints.Count < 2) { Debug.LogError("Camera: Not enough sectionBoundaryPoints (need N+1 for N sections). Disabling.", this); enabled = false; return; }
        for (int i = 0; i < sectionBoundaryPoints.Count; ++i) {
            if (sectionBoundaryPoints[i] == null) { Debug.LogError($"Camera: sectionBoundaryPoint at index {i} is null. Disabling.", this); enabled = false; return; }
        }
        if (playerTransform1 == null || playerTransform2 == null) { Debug.LogError("Camera: Player transforms not assigned!", this); enabled = false; return; }

        _sectionTasksCompleted = new List<bool>();
        for (int i = 0; i < sectionBoundaryPoints.Count - 1; i++)
        {
            _sectionTasksCompleted.Add(false);
        }

        _targetOrthographicSize = mainCam.orthographicSize;
        RecalculateCameraHalfWidth(); 

        InitializeCameraForSection(_currentActiveSectionIndex, true); 
        EventManager.SetCurrentLevel(_currentActiveSectionIndex); 
        EventManager.OnLevelSectionCompleted += HandleLevelSectionCompleted;
        EventManager.OnDarkPlayerStuckInLightCamera += HandleDarkPlayerStuckEvent; // Correct subscription
        EventManager.OnGameEndedStep2 += HandleGameEndedStep2;
        EventManager.OnPlayersTeleportedForEndGame += HandlePlayersTeleportedForEndGame_AdvanceCamera;
    }

    void OnDestroy()
    {
        EventManager.OnLevelSectionCompleted -= HandleLevelSectionCompleted;
        EventManager.OnDarkPlayerStuckInLightCamera -= HandleDarkPlayerStuckEvent; // Correct unsubscription
        EventManager.OnGameEndedStep2 -= HandleGameEndedStep2;
        EventManager.OnPlayersTeleportedForEndGame -= HandlePlayersTeleportedForEndGame_AdvanceCamera;
    }
    
    private void HandlePlayersTeleportedForEndGame_AdvanceCamera()
    {
        Debug.Log("CameraController: Received OnPlayersTeleportedForEndGame. Forcing camera advance for end game.");
        ForceAdvanceToNextSection(); // Assuming this targets the final view correctly
        // You might need a specific method if "next section" isn't right.
        // e.g., ForceAdvanceToSection(int targetSectionIndex)
    }
    
    private void HandleGameEndedStep2()
    {
        ForceAdvanceToNextSection();
    }

    private void HandleDarkPlayerStuckEvent(DarkPlayerController stuckPlayer)
    {
        ForceAdvanceToNextSection();
    }

    private void InitializeCameraForSection(int sectionIndex, bool snapPositionInstantly)
    {
        _currentActiveSectionIndex = sectionIndex;
        _isTransitioningToNewSectionCenter = false;
        _arePlayersBeyondCurrentSectionCommitLine = false;
        _playerCrossBoundaryTimer = 0f;
        _currentPanTimer = 0f; // **ADDITION**: Initialize pan timer here too for safety

        UpdateEffectiveCameraLimits(); 

        if (snapPositionInstantly)
        {
            Vector3 initialPos = transform.position;
            initialPos.x = (_minCameraCenterX + _maxCameraCenterX) / 2.0f;
            if (float.IsInfinity(_maxCameraCenterX)) 
            {
                 initialPos.x = _minCameraCenterX + _cameraHalfWidth / 2f; 
            }
            initialPos.x = Mathf.Clamp(initialPos.x, _minCameraCenterX, _maxCameraCenterX);
            transform.position = new Vector3(initialPos.x, transform.position.y, transform.position.z);
        }
        // Removed redundant logging from here, it's in UpdateEffectiveCameraLimits
    }


    private void HandleLevelSectionCompleted(int completedSectionIndex)
    {
        // Your existing logging for this method is good
        if (completedSectionIndex >= 0 && completedSectionIndex < _sectionTasksCompleted.Count)
        {
            if (!_sectionTasksCompleted[completedSectionIndex])
            {
                _sectionTasksCompleted[completedSectionIndex] = true;

                if (completedSectionIndex == _currentActiveSectionIndex)
                {
                    UpdateEffectiveCameraLimits();
                    if (_isTransitioningToNewSectionCenter)
                    {
                        _isTransitioningToNewSectionCenter = false;
                        _cameraMoveVelocity = Vector3.zero; 
                        _currentPanTimer = 0f; // **ADDITION**: Reset pan timer if pan is cancelled
                    }
                }
            }
            // else: Log for already completed is fine
        }
        // else: Log for invalid index is fine
    }

    void UpdateEffectiveCameraLimits()
    {
        RecalculateCameraHalfWidth(); 

        if (_currentActiveSectionIndex < 0 || _currentActiveSectionIndex >= sectionBoundaryPoints.Count - 1)
        {
            _minCameraCenterX = -float.PositiveInfinity; _maxCameraCenterX = float.PositiveInfinity;
            return;
        }

        Transform leftPhysicalBoundaryTransform = sectionBoundaryPoints[_currentActiveSectionIndex];
        Transform rightPhysicalBoundaryTransform;
        int rightBoundaryPointIndex = _currentActiveSectionIndex + 1;

        bool taskForCurrentDone = _sectionTasksCompleted[_currentActiveSectionIndex]; // Cache for clarity

        if (taskForCurrentDone && (_currentActiveSectionIndex + 2 < sectionBoundaryPoints.Count) )
        {
            rightBoundaryPointIndex = _currentActiveSectionIndex + 2;
            // Debug.Log($"Camera: Task for section {_currentActiveSectionIndex} is complete. Extending right view to boundary point {rightBoundaryPointIndex} ({sectionBoundaryPoints[rightBoundaryPointIndex].name})."); // Keep this log if helpful
        }
        rightPhysicalBoundaryTransform = sectionBoundaryPoints[rightBoundaryPointIndex];

        float physicalLeftEdgeX = leftPhysicalBoundaryTransform.position.x;
        float physicalRightEdgeX = rightPhysicalBoundaryTransform.position.x;
        _minCameraCenterX = physicalLeftEdgeX + _cameraHalfWidth;
        _maxCameraCenterX = physicalRightEdgeX - _cameraHalfWidth;

        if (physicalRightEdgeX - physicalLeftEdgeX < (_cameraHalfWidth * 2f - 0.001f))
        {
            _minCameraCenterX = _maxCameraCenterX = (physicalLeftEdgeX + physicalRightEdgeX) / 2f;
        }
        else if (_minCameraCenterX > _maxCameraCenterX) 
        {
             _minCameraCenterX = _maxCameraCenterX = (physicalLeftEdgeX + physicalRightEdgeX) / 2f;
        }
    }


    void RecalculateCameraHalfWidth()
    {
        if (mainCam.orthographic)
            _cameraHalfWidth = mainCam.orthographicSize * mainCam.aspect;
        else
            _cameraHalfWidth = Mathf.Abs(transform.position.z) * Mathf.Tan(mainCam.fieldOfView * 0.5f * Mathf.Deg2Rad) * mainCam.aspect;
    }

    void LateUpdate()
    {
        if (!enabled || playerTransform1 == null || playerTransform2 == null) return;

        // Condense or remove some verbose logging if it's too much for normal operation
        // string taskDoneStatus = "N/A";
        // if (_currentActiveSectionIndex >= 0 && _currentActiveSectionIndex < _sectionTasksCompleted.Count) {
        //     taskDoneStatus = _sectionTasksCompleted[_currentActiveSectionIndex].ToString();
        // }
        // Debug.Log($"--- FRAME {Time.frameCount} LU START --- S:{_currentActiveSectionIndex}, TD:{taskDoneStatus}, Trans:{_isTransitioningToNewSectionCenter}, CamLims:[{_minCameraCenterX:F2},{_maxCameraCenterX:F2}], CamX:{transform.position.x:F2}, P1X:{playerTransform1.position.x:F2}, P2X:{playerTransform2.position.x:F2}");


        if (enableDynamicZoom && mainCam.orthographic) HandleDynamicZoom();

        // --- Handle Smooth Transition to New Section's Center ---
        if (_isTransitioningToNewSectionCenter)
        {
            // **MODIFICATION START: Incorporate Pan Timer Logic**
            _currentPanTimer += Time.deltaTime;
            bool panTargetReached = Mathf.Abs(transform.position.x - _targetPositionForViewCenterTransition.x) < 0.05f;
            bool panTimedOut = _currentPanTimer >= viewCenterTransitionTimeout;

            // Debug.Log($"LATE_UPDATE: IsTransitioning. TargetPan.X: {_targetPositionForViewCenterTransition.x:F2}, CurrentCam.X: {transform.position.x:F2}, PanTimer: {_currentPanTimer:F2}, Reached: {panTargetReached}, TimedOut: {panTimedOut}");

            if (panTargetReached || panTimedOut)
            {
                if (panTimedOut && !panTargetReached)
                {
                    Debug.LogWarning($"Camera: Pan to section {_currentActiveSectionIndex} TIMED OUT. Snapping to target.");
                }
                
                transform.position = new Vector3(_targetPositionForViewCenterTransition.x, transform.position.y, transform.position.z); 
                _isTransitioningToNewSectionCenter = false;
                _currentPanTimer = 0f; // Reset timer for next use
                
                UpdateEffectiveCameraLimits(); 
                EnsureCameraWithinCalculatedBoundaries(); // Ensure snapped position is valid
            }
            else
            {
                // Continue panning
                transform.position = Vector3.SmoothDamp(transform.position, _targetPositionForViewCenterTransition, ref _cameraMoveVelocity, viewCenterTransitionSmoothTime);
                // It might be better to call EnsureCameraWithinCalculatedBoundaries() *after* the SmoothDamp,
                // using the limits of the section we are *panning from*, or simply not clamp too aggressively during the pan.
                // For now, let's keep it as is, it will clamp to current _min/_max.
                EnsureCameraWithinCalculatedBoundaries(); 
            }
            // **MODIFICATION END**
            
            // Debug.Log($"--- FRAME {Time.frameCount} LU END (Transition) --- CamX:{transform.position.x:F2}");
            return; // IMPORTANT: If transitioning, skip player following for this frame
        }

        // --- Check for Committing to the Next Section (Player-Triggered) ---
        // This block only runs if NOT _isTransitioningToNewSectionCenter
        int nextPotentialSectionIndex = _currentActiveSectionIndex + 1;
        if (nextPotentialSectionIndex <= sectionBoundaryPoints.Count - 2) 
        {
            Transform commitLineTransform = sectionBoundaryPoints[nextPotentialSectionIndex]; 
            float commitLineX = commitLineTransform.position.x;
            // Debug.Log($"LATE_UPDATE: CommitCheck. CurrentSection: {_currentActiveSectionIndex}. CommitLine: {commitLineTransform.name} (X={commitLineX:F2}). ArePlayersBeyondFlag: {_arePlayersBeyondCurrentSectionCommitLine}, Timer: {_playerCrossBoundaryTimer:F2}");

            bool p1Over = playerTransform1.position.x > commitLineX;
            bool p2Over = playerTransform2.position.x > commitLineX;

            if (p1Over && p2Over)
            {
                if (!_arePlayersBeyondCurrentSectionCommitLine)
                {
                    _arePlayersBeyondCurrentSectionCommitLine = true;
                    _playerCrossBoundaryTimer = 0f;
                    // Debug.Log($"LATE_UPDATE: Commit Condition MET... Starting timer.");
                }
            }
            else 
            {
                if (_arePlayersBeyondCurrentSectionCommitLine) 
                {
                    // Debug.Log($"LATE_UPDATE: Commit Condition LOST... Resetting timer.");
                    _arePlayersBeyondCurrentSectionCommitLine = false;
                    _playerCrossBoundaryTimer = 0f;
                }
            }

            if (_arePlayersBeyondCurrentSectionCommitLine)
            {
                _playerCrossBoundaryTimer += Time.deltaTime;
                if (_playerCrossBoundaryTimer >= delayBeforeFullTransition)
                {
                    // Debug.Log($"LATE_UPDATE: Commit Timer complete... Calling CommitToNextSection.");
                    CommitToNextSection();
                    // After CommitToNextSection, _isTransitioningToNewSectionCenter = true,
                    // so the 'return' in the transition block above will catch it on the NEXT frame.
                }
            }
        } 
        // else: Log for no next section is fine

        // --- Normal Player Following (Only if NOT transitioning and no commit happened this frame to start one) ---
        if (!_isTransitioningToNewSectionCenter) // Double check
        {
            Vector3 playersMidpoint = (playerTransform1.position + playerTransform2.position) / 2f;
            Vector3 desiredCameraPosition = new Vector3(playersMidpoint.x + followOffset.x, transform.position.y, transform.position.z);
            
            // Debug.Log($"LATE_UPDATE_FOLLOW: Mid.X: {playersMidpoint.x:F2}, RawDesired.X: {desiredCameraPosition.x:F2}, Limits:[{_minCameraCenterX:F2},{_maxCameraCenterX:F2}]");

            float originalDesiredX = desiredCameraPosition.x; 
            desiredCameraPosition.x = Mathf.Clamp(desiredCameraPosition.x, _minCameraCenterX, _maxCameraCenterX);

            // Debug.Log($"LATE_UPDATE_FOLLOW: ClampedDesired.X: {desiredCameraPosition.x:F2} (Orig:{originalDesiredX:F2}), Vel.X:{_cameraMoveVelocity.x:F2}");
            
            if (Vector3.SqrMagnitude(transform.position - desiredCameraPosition) > 0.0001f) // Adjusted threshold
            {
                transform.position = Vector3.SmoothDamp(transform.position, desiredCameraPosition, ref _cameraMoveVelocity, followSmoothTime);
            }
            else
            {
                transform.position = desiredCameraPosition; // Snap if very close
                _cameraMoveVelocity.x = 0f; // Stop horizontal velocity if at target
            }
            
            EnsureCameraWithinCalculatedBoundaries();
            // Debug.Log($"LATE_UPDATE_FOLLOW: FinalCam.X: {transform.position.x:F2}");
        }
        // Debug.Log($"--- FRAME {Time.frameCount} LU END --- CamX:{transform.position.x:F2}");
    }

    void CommitToNextSection()
    {
        int newSectionIndex = _currentActiveSectionIndex + 1;
        if (newSectionIndex >= sectionBoundaryPoints.Count - 1) 
        {
            _arePlayersBeyondCurrentSectionCommitLine = false; 
            _playerCrossBoundaryTimer = 0f;
            _isTransitioningToNewSectionCenter = false; 
            return;
        }
        // Debug.Log($"Camera: Committing (player-triggered) to section {newSectionIndex}. Current section was {_currentActiveSectionIndex}.");
        AdvanceSectionInternal(newSectionIndex, "Player-Triggered Commit");
    }
    
    public void ForceAdvanceToNextSection()
    {
        int newSectionIndex = _currentActiveSectionIndex + 1;
        Debug.Log("ForceAdvanceToNextSection");
        if (newSectionIndex >= sectionBoundaryPoints.Count - 1)
        {
            Debug.LogWarning($"Camera: ForceAdvanceToNextSection: Cannot advance. Section {newSectionIndex} is out of bounds.");
            return;
        }
        // Debug.Log($"Camera: Forcing advance to section {newSectionIndex}.");
        AdvanceSectionInternal(newSectionIndex, "Forced Advance");
    }

    private void AdvanceSectionInternal(int newSectionIndex, string triggerSource)
    {
        if (_isTransitioningToNewSectionCenter)
        {
            // Debug.Log($"Camera: AdvanceSectionInternal ({triggerSource}): Was already transitioning. Overriding with new target for section {newSectionIndex}.");
        }

        _currentActiveSectionIndex = newSectionIndex;
        EventManager.SetCurrentLevel(_currentActiveSectionIndex); 

        _arePlayersBeyondCurrentSectionCommitLine = false;
        _playerCrossBoundaryTimer = 0f;

        // **MODIFICATION**: Recalculate half-width here if dynamic zoom could have changed it
        // due to player teleportation before this method is effectively used for pan target.
        // This ensures pan target calculation is based on potentially new zoom.
        if (enableDynamicZoom) {
            // HandleDynamicZoom(); // This might be too much here as it also tries to move the camera.
                                 // We just need the updated orthographic size for _cameraHalfWidth.
                                 // Assuming HandleDynamicZoom in LateUpdate handles the actual size change.
                                 // For pan target, let's ensure _cameraHalfWidth is fresh.
            RecalculateCameraHalfWidth(); // Get latest half-width based on current mainCam.orthographicSize
        }


        Transform newSectionLeftBoundary = sectionBoundaryPoints[_currentActiveSectionIndex];     
        Transform newSectionRightBoundary = sectionBoundaryPoints[_currentActiveSectionIndex + 1]; 
        float tempTargetMinCamX = newSectionLeftBoundary.position.x + _cameraHalfWidth;
        float tempTargetMaxCamX = newSectionRightBoundary.position.x - _cameraHalfWidth;
        
        _targetPositionForViewCenterTransition.y = transform.position.y; 
        _targetPositionForViewCenterTransition.z = transform.position.z; 

        if (tempTargetMinCamX > tempTargetMaxCamX) 
        {
             _targetPositionForViewCenterTransition.x = (newSectionLeftBoundary.position.x + newSectionRightBoundary.position.x) / 2f;
        } else {
             _targetPositionForViewCenterTransition.x = (tempTargetMinCamX + tempTargetMaxCamX) / 2.0f;
        }

        _isTransitioningToNewSectionCenter = true;
        _cameraMoveVelocity = Vector3.zero; 
        _currentPanTimer = 0f; // **ADDITION**: Reset pan timer when starting a new pan
        // Debug.Log($"Camera: AdvanceSectionInternal ({triggerSource}): Started smooth pan to section {_currentActiveSectionIndex} X: {_targetPositionForViewCenterTransition.x:F2}. HalfWidth used: {_cameraHalfWidth:F2}");
    }

    void HandleDynamicZoom()
    {
        float distance = Vector3.Distance(playerTransform1.position, playerTransform2.position);
        float normalizedDistance = Mathf.Clamp01((distance - minPlayerDistanceForZoom) / (maxPlayerDistanceForZoom > minPlayerDistanceForZoom ? (maxPlayerDistanceForZoom - minPlayerDistanceForZoom) : 1f));
        _targetOrthographicSize = Mathf.Lerp(minOrthographicSize, maxOrthographicSize, normalizedDistance);

        if (Mathf.Abs(mainCam.orthographicSize - _targetOrthographicSize) > 0.01f)
        {
            mainCam.orthographicSize = Mathf.SmoothDamp(mainCam.orthographicSize, _targetOrthographicSize, ref _orthographicSizeVelocity, zoomSmoothTime);
            UpdateEffectiveCameraLimits(); 
            EnsureCameraWithinCalculatedBoundaries();
        }
    }

    void EnsureCameraWithinCalculatedBoundaries()
    {
        Vector3 currentPosition = transform.position;
        currentPosition.x = Mathf.Clamp(currentPosition.x, _minCameraCenterX, _maxCameraCenterX);
        if (Vector3.SqrMagnitude(transform.position - currentPosition) > 0.00001f) // Smaller threshold for snapping
        {
            transform.position = currentPosition;
        }
    }
    
    public void ResetCameraToStartOfGame()
    {
        _cameraMoveVelocity = Vector3.zero;
        _orthographicSizeVelocity = 0f;
        mainCam.orthographicSize = enableDynamicZoom ? minOrthographicSize : (sectionBoundaryPoints.Count > 0 ? mainCam.orthographicSize : 5f);
        if (enableDynamicZoom) _targetOrthographicSize = minOrthographicSize;

        for (int i = 0; i < _sectionTasksCompleted.Count; i++)
        {
            _sectionTasksCompleted[i] = false;
        }
        InitializeCameraForSection(0, true); 
        EventManager.SetCurrentLevel(0);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (mainCam == null || sectionBoundaryPoints.Count < 2) return;

        float currentHalfWidth = mainCam.orthographic ? mainCam.orthographicSize * mainCam.aspect : 10f; // Approx for editor
        if(Application.isPlaying) currentHalfWidth = _cameraHalfWidth; // Use runtime if available

        float camHeight = mainCam.orthographic ? mainCam.orthographicSize * 2f : 10f;
        Vector3 camGizmoPosition = transform.position;

        Gizmos.color = Color.cyan; // Current camera actual view
        Gizmos.DrawWireCube(new Vector3(camGizmoPosition.x, camGizmoPosition.y, 0), new Vector3(currentHalfWidth * 2f, camHeight, 0.1f));

        float debugY = transform.position.y;

        // Draw Effective Camera Center Travel Limits (if calculated)
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(new Vector3(_minCameraCenterX, debugY - camHeight * 0.6f, 0),
                            new Vector3(_minCameraCenterX, debugY + camHeight * 0.6f, 0));
            UnityEditor.Handles.Label(new Vector3(_minCameraCenterX, debugY + camHeight * 0.6f, 0), "Min Cam Center X");

            if (!float.IsInfinity(_maxCameraCenterX))
            {
                Gizmos.DrawLine(new Vector3(_maxCameraCenterX, debugY - camHeight * 0.6f, 0),
                                new Vector3(_maxCameraCenterX, debugY + camHeight * 0.6f, 0));
                UnityEditor.Handles.Label(new Vector3(_maxCameraCenterX, debugY + camHeight * 0.6f, 0), "Max Cam Center X");
            }


            // Draw Physical View Boundaries derived from _min/_maxCameraCenterX
            Gizmos.color = Color.green; // Leftmost physical point camera can see
            Gizmos.DrawLine(new Vector3(_minCameraCenterX - currentHalfWidth, debugY - camHeight * 1.5f, 0),
                            new Vector3(_minCameraCenterX - currentHalfWidth, debugY + camHeight * 1.5f, 0));
            UnityEditor.Handles.Label(new Vector3(_minCameraCenterX - currentHalfWidth, debugY + camHeight * 1.5f, 0), "Effective View Left");
            
            if (!float.IsInfinity(_maxCameraCenterX))
            {
                Gizmos.color = Color.red; // Rightmost physical point camera can see
                Gizmos.DrawLine(new Vector3(_maxCameraCenterX + currentHalfWidth, debugY - camHeight * 1.5f, 0),
                                new Vector3(_maxCameraCenterX + currentHalfWidth, debugY + camHeight * 1.5f, 0));
                UnityEditor.Handles.Label(new Vector3(_maxCameraCenterX + currentHalfWidth, debugY + camHeight * 1.5f, 0), "Effective View Right");
            }
        }


        // Visualize all defined section boundary points
        for (int i = 0; i < sectionBoundaryPoints.Count; i++)
        {
            if (sectionBoundaryPoints[i] != null)
            {
                bool isCurrentActiveLeft = Application.isPlaying && i == _currentActiveSectionIndex;
                // Determine if it's the current effective right boundary for view extension
                bool isCurrentEffectiveRight = false;
                if (Application.isPlaying && _currentActiveSectionIndex >=0 && _currentActiveSectionIndex < _sectionTasksCompleted.Count) {
                    int effectiveRightIdx = _currentActiveSectionIndex + 1;
                    if(_sectionTasksCompleted[_currentActiveSectionIndex] && _currentActiveSectionIndex + 2 < sectionBoundaryPoints.Count) {
                        effectiveRightIdx = _currentActiveSectionIndex + 2;
                    }
                    if (i == effectiveRightIdx) isCurrentEffectiveRight = true;
                }


                if (isCurrentActiveLeft) Gizmos.color = Color.blue;
                else if (isCurrentEffectiveRight) Gizmos.color = Color.magenta;
                else Gizmos.color = Color.gray;
                
                Gizmos.DrawSphere(sectionBoundaryPoints[i].position, 0.4f);
                Gizmos.DrawLine(sectionBoundaryPoints[i].position - Vector3.up * camHeight, sectionBoundaryPoints[i].position + Vector3.up * camHeight);
                UnityEditor.Handles.Label(sectionBoundaryPoints[i].position + Vector3.up * 0.5f, $"Boundary Pt {i}");
            }
        }
    }
#endif
}
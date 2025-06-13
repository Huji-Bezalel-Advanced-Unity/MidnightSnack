// NewCameraController.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public class NewCameraController : MonoBehaviour
{
    [Header("Player Transforms")]
    [SerializeField] private Transform player1;
    [SerializeField] private Transform player2;

    [Header("Camera Setup")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Vector2 followOffset = Vector2.zero; // Offset from players' midpoint
    [SerializeField] private float smoothTime = 0.3f;

    [Header("Section Boundaries")]
    [Tooltip("List of Transforms representing potential barrier positions. Sorted by X position from left to right. N sections imply N+1 boundary points.")]
    public List<Transform> sectionBoundaryPoints = new List<Transform>();
    [Tooltip("The index of the boundary point currently acting as the LEFT edge of the camera's allowed view.")]
    [SerializeField] private int currentMinBoundaryIndex = 0;
    [Tooltip("The index of the boundary point currently acting as the RIGHT edge of the camera's allowed view.")]
    [SerializeField] private int currentMaxBoundaryIndex = 1;

    [Header("Out-of-View Correction")]
    [SerializeField] private bool forcePlayersInView = true;
    // [SerializeField] private float correctionSnapSpeed = 10f; // This wasn't used in the refined EnsurePlayersAreTrulyVisible

    // Internal State
    private float _cameraHalfWidth;
    private Vector3 _currentVelocity = Vector3.zero;

    private float _currentMinViewX; // The actual X-coordinate for the leftmost camera view
    private float _currentMaxViewX; // The actual X-coordinate for the rightmost camera view

    private bool _darkPlayerStuckEventProcessed = false; // Flag for one-time event processing

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) { Debug.LogError("Main Camera not assigned or found!", this); enabled = false; return; }
        if (player1 == null || player2 == null) { Debug.LogError("Player transforms not assigned!", this); enabled = false; return; }
        if (sectionBoundaryPoints.Count < 2) { Debug.LogError("Not enough sectionBoundaryPoints (need at least 2).", this); enabled = false; return; }

        for (int i = 0; i < sectionBoundaryPoints.Count; ++i)
        {
            if (sectionBoundaryPoints[i] == null)
            {
                Debug.LogError($"SectionBoundaryPoint at index {i} is null. Disabling.", this);
                enabled = false; return;
            }
        }

        RecalculateCameraHalfWidth();
        UpdateViewBoundaries(); // Initialize min/max view X based on initial indices

        // Snap camera to initial valid position
        Vector3 initialTarget = CalculateTargetPosition();
        transform.position = new Vector3(initialTarget.x, transform.position.y, transform.position.z);

        // Subscribe to relevant events
        EventManager.OnBarrierOpened += HandleBarrierOpened;
        EventManager.OnPlayersPassedBarrier += HandlePlayersPassedBarrier;
        EventManager.OnDarkPlayerStuckInLightCamera += HandleDarkPlayerStuckCameraEvent; // Use specific handler name
    }

    void OnDestroy()
    {
        EventManager.OnBarrierOpened -= HandleBarrierOpened;
        EventManager.OnPlayersPassedBarrier -= HandlePlayersPassedBarrier;
        EventManager.OnDarkPlayerStuckInLightCamera -= HandleDarkPlayerStuckCameraEvent;
    }

    void LateUpdate()
    {
        if (!enabled) return;

        RecalculateCameraHalfWidth();

        Vector3 primaryTargetPosition = CalculateTargetPosition();
        float targetX = primaryTargetPosition.x;

        if (forcePlayersInView)
        {
            targetX = EnsurePlayersAreTrulyVisible(targetX, primaryTargetPosition.y, primaryTargetPosition.z);
        }
    
        Vector3 finalTargetPosition = new Vector3(targetX, primaryTargetPosition.y, primaryTargetPosition.z);

        // Optional: Reset velocity for large jumps - test if this feels good
        // if (Vector3.Distance(transform.position, finalTargetPosition) > _cameraHalfWidth * 1.5f) {
        //     _currentVelocity = Vector3.zero;
        // }

        transform.position = Vector3.SmoothDamp(transform.position, finalTargetPosition, ref _currentVelocity, smoothTime);
    }
    
    Vector3 CalculateTargetPosition()
    {
        if (player1 == null || player2 == null) return transform.position; // Safety check

        Vector3 playersMidpoint = (player1.position + player2.position) / 2f;
        float desiredCameraX = playersMidpoint.x + followOffset.x;

        float minAllowedCamX = _currentMinViewX + _cameraHalfWidth;
        float maxAllowedCamX = _currentMaxViewX - _cameraHalfWidth;

        // Handle cases where the viewable area for the camera center is zero or negative
        if (minAllowedCamX > maxAllowedCamX) {
            minAllowedCamX = maxAllowedCamX = (_currentMinViewX + _currentMaxViewX) / 2f;
        }
        
        float clampedCameraX = Mathf.Clamp(desiredCameraX, minAllowedCamX, maxAllowedCamX);
        
        return new Vector3(clampedCameraX, transform.position.y + followOffset.y, transform.position.z);
    }

    void RecalculateCameraHalfWidth()
    {
        if (mainCamera.orthographic)
        {
            _cameraHalfWidth = mainCamera.orthographicSize * mainCamera.aspect;
        }
        else
        {
            float halfFovRad = mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            _cameraHalfWidth = Mathf.Abs(mainCamera.transform.position.z - transform.position.z) * Mathf.Tan(halfFovRad) * mainCamera.aspect;
        }
    }

    void UpdateViewBoundaries()
    {
        if (currentMinBoundaryIndex < 0 || currentMinBoundaryIndex >= sectionBoundaryPoints.Count)
        {
            Debug.LogError($"Invalid currentMinBoundaryIndex: {currentMinBoundaryIndex}");
            _currentMinViewX = -Mathf.Infinity;
        }
        else
        {
            _currentMinViewX = sectionBoundaryPoints[currentMinBoundaryIndex].position.x;
        }

        if (currentMaxBoundaryIndex < 0 || currentMaxBoundaryIndex >= sectionBoundaryPoints.Count || currentMaxBoundaryIndex <= currentMinBoundaryIndex)
        {
            // If max is invalid or not greater than min, set it to a practical value if possible, or infinity.
            if (currentMinBoundaryIndex + 1 < sectionBoundaryPoints.Count && currentMaxBoundaryIndex <= currentMinBoundaryIndex) {
                 Debug.LogWarning($"MaxBoundaryIndex ({currentMaxBoundaryIndex}) was not greater than MinBoundaryIndex ({currentMinBoundaryIndex}). Attempting to set to MinIndex + 1.");
                 currentMaxBoundaryIndex = currentMinBoundaryIndex + 1; // Try to set to next valid
            }

            if (currentMaxBoundaryIndex < 0 || currentMaxBoundaryIndex >= sectionBoundaryPoints.Count || currentMaxBoundaryIndex <= currentMinBoundaryIndex) {
                Debug.LogError($"Invalid currentMaxBoundaryIndex: {currentMaxBoundaryIndex} (Min index is {currentMinBoundaryIndex}). MaxViewX will be Infinity.");
                _currentMaxViewX = Mathf.Infinity;
            } else {
                 _currentMaxViewX = sectionBoundaryPoints[currentMaxBoundaryIndex].position.x;
            }
        }
        else
        {
            _currentMaxViewX = sectionBoundaryPoints[currentMaxBoundaryIndex].position.x;
        }
        Debug.Log($"View Boundaries Updated: MinViewX (Boundary {currentMinBoundaryIndex} at {_currentMinViewX:F2}), MaxViewX (Boundary {currentMaxBoundaryIndex} at {_currentMaxViewX:F2})");
    }

    public void HandleBarrierOpened(int openedBarrierPointIndex)
    {
        Debug.Log($"HandleBarrierOpened: Received for barrier at boundary point index {openedBarrierPointIndex}. Current Max Index was: {currentMaxBoundaryIndex}");
        int potentialNewMaxIndex = openedBarrierPointIndex + 1;

        if (potentialNewMaxIndex > currentMinBoundaryIndex &&
            potentialNewMaxIndex < sectionBoundaryPoints.Count)
        {
            if (potentialNewMaxIndex > currentMaxBoundaryIndex)
            {
                currentMaxBoundaryIndex = potentialNewMaxIndex;
                UpdateViewBoundaries();
                _currentVelocity = Vector3.zero; // Reset velocity
                Debug.Log($"HandleBarrierOpened: Max boundary extended. New currentMaxBoundaryIndex: {currentMaxBoundaryIndex}. Velocity reset.");
            }
            else
            {
                Debug.Log($"HandleBarrierOpened: New potential max index ({potentialNewMaxIndex}) does not extend current max ({currentMaxBoundaryIndex}). No change to max boundary.");
            }
        }
        else
        {
            Debug.LogWarning($"HandleBarrierOpened: Cannot set new max boundary. PotentialNewMaxIndex ({potentialNewMaxIndex}) is invalid or not > minIndex ({currentMinBoundaryIndex}) or out of bounds. Max boundary not changed from {currentMaxBoundaryIndex}.");
        }
    }

    public void HandlePlayersPassedBarrier(int passedBarrierPointIndex)
    {
        Debug.Log($"HandlePlayersPassedBarrier: Received for boundary point index {passedBarrierPointIndex}. Current Min/Max Indices were: {currentMinBoundaryIndex}/{currentMaxBoundaryIndex}");
        if (passedBarrierPointIndex >= 0 && passedBarrierPointIndex < sectionBoundaryPoints.Count - 1)
        {
            int oldMinIndex = currentMinBoundaryIndex;
            int oldMaxIndex = currentMaxBoundaryIndex;

            currentMinBoundaryIndex = passedBarrierPointIndex;
            currentMaxBoundaryIndex = Mathf.Max(currentMaxBoundaryIndex, passedBarrierPointIndex + 1);

            if (currentMaxBoundaryIndex >= sectionBoundaryPoints.Count)
            {
                currentMaxBoundaryIndex = sectionBoundaryPoints.Count - 1;
            }
            
            if (currentMinBoundaryIndex != oldMinIndex || currentMaxBoundaryIndex != oldMaxIndex) {
                UpdateViewBoundaries();
                _currentVelocity = Vector3.zero; // Reset velocity
                Debug.Log($"HandlePlayersPassedBarrier: Boundaries updated. New Min/Max Indices: {currentMinBoundaryIndex}/{currentMaxBoundaryIndex}. Velocity reset.");
            } else {
                Debug.Log($"HandlePlayersPassedBarrier: Indices did not change. No boundary update or velocity reset needed. Min/Max Indices: {currentMinBoundaryIndex}/{currentMaxBoundaryIndex}");
            }
        }
        else
        {
            Debug.LogWarning($"HandlePlayersPassedBarrier: Invalid passedBarrierPointIndex {passedBarrierPointIndex}. Min boundary not changed.");
        }
    }

    /// <summary>
    /// Handles the event when the dark player gets stuck, forcing a camera advance if not already done.
    /// </summary>
    private void HandleDarkPlayerStuckCameraEvent(DarkPlayerController darkPlayerController) // Assuming event signature: Action
    {
        Debug.Log("HandleDarkPlayerStuckCameraEvent received.");
        if (!_darkPlayerStuckEventProcessed)
        {
            Debug.Log("Dark player stuck event not yet processed. Forcing camera advance.");
            ForceAdvanceToNextSectionView(); // Use the existing force advance method
            _darkPlayerStuckEventProcessed = true;
        }
        else
        {
            Debug.Log("Dark player stuck event ALREADY processed. No further camera advance from this event.");
        }
    }
    
    public void ForceAdvanceToNextSectionView()
    {
        Debug.Log($"ForceAdvanceToNextSectionView called. Current Min/Max Indices: {currentMinBoundaryIndex}/{currentMaxBoundaryIndex}");
        if (currentMaxBoundaryIndex < sectionBoundaryPoints.Count - 1)
        {
            currentMinBoundaryIndex = currentMaxBoundaryIndex;
            currentMaxBoundaryIndex = currentMaxBoundaryIndex + 1; // This is the core logic: advance one section
            
            UpdateViewBoundaries();
            _currentVelocity = Vector3.zero; // Reset velocity for a fresh smooth move

            Debug.Log($"Forced advance. New Min Index: {currentMinBoundaryIndex}, New Max Index: {currentMaxBoundaryIndex}. Camera will now follow within these new bounds.");
        }
        else
        {
            Debug.LogWarning("Cannot force advance view, already at or beyond the last defined max boundary point.");
        }
    }

    private float EnsurePlayersAreTrulyVisible(float inputTargetX, float camY, float camZ)
    {
        if (player1 == null || player2 == null) return inputTargetX; // Safety

        float prospectiveViewLeft = inputTargetX - _cameraHalfWidth;
        float prospectiveViewRight = inputTargetX + _cameraHalfWidth;

        float minPlayerX = Mathf.Min(player1.position.x, player2.position.x);
        float maxPlayerX = Mathf.Max(player1.position.x, player2.position.x);

        bool p1WouldBeOffScreenLeft = minPlayerX < prospectiveViewLeft - 0.1f;
        bool p2WouldBeOffScreenRight = maxPlayerX > prospectiveViewRight + 0.1f;

        if (p1WouldBeOffScreenLeft || p2WouldBeOffScreenRight)
        {
            float emergencyTargetX = inputTargetX; 

            if (p1WouldBeOffScreenLeft && p2WouldBeOffScreenRight) {
                emergencyTargetX = (player1.position.x + player2.position.x) / 2f + followOffset.x;
                // Debug.LogWarning($"Players would be off-screen on BOTH sides of target {inputTargetX:F2}! Emergency centering. New Target: {emergencyTargetX:F2}");
            } else if (p1WouldBeOffScreenLeft) {
                emergencyTargetX = minPlayerX + _cameraHalfWidth - followOffset.x; 
                // Debug.LogWarning($"Player 1 would be off-screen left of target {inputTargetX:F2}! Emergency Correct Target: {emergencyTargetX:F2}");
            } else { // p2WouldBeOffScreenRight
                emergencyTargetX = maxPlayerX - _cameraHalfWidth - followOffset.x; 
                // Debug.LogWarning($"Player 2 would be off-screen right of target {inputTargetX:F2}! Emergency Correct Target: {emergencyTargetX:F2}");
            }
            
            float finalClampedEmergencyTargetX = Mathf.Clamp(emergencyTargetX, _currentMinViewX + _cameraHalfWidth, _currentMaxViewX - _cameraHalfWidth);
            if(Mathf.Abs(finalClampedEmergencyTargetX - inputTargetX) > 0.01f) { // Only log if it's a meaningful correction
                 Debug.LogWarning($"EnsurePlayersAreTrulyVisible: Corrected Target from {inputTargetX:F2} to {finalClampedEmergencyTargetX:F2}. CurrentCamX: {transform.position.x:F2}");
            }
            return finalClampedEmergencyTargetX;
        }
        return inputTargetX; 
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (mainCamera == null || sectionBoundaryPoints.Count == 0) return;

        float halfWidth = _cameraHalfWidth;
        // In editor, if not playing, _cameraHalfWidth might be 0. Estimate for Gizmos.
        if (!Application.isPlaying || halfWidth < 0.01f) 
        {
            if (mainCamera != null && mainCamera.orthographic) halfWidth = mainCamera.orthographicSize * mainCamera.aspect;
            else if (mainCamera != null) halfWidth = 10f; 
            else halfWidth = 5f; // Last resort default
        }


        float camHeight = mainCamera.orthographic ? mainCamera.orthographicSize * 2f : 10f;
        Vector3 camGizmoCenter = transform.position;

        // Draw current camera viewport (Cyan)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(camGizmoCenter, new Vector3(halfWidth * 2f, camHeight, 0.1f));

        if (Application.isPlaying)
        {
            // Min Allowed View X (Green Line)
            Gizmos.color = Color.green;
            Gizmos.DrawLine(new Vector3(_currentMinViewX, transform.position.y - camHeight * 1.5f, 0), new Vector3(_currentMinViewX, transform.position.y + camHeight * 1.5f, 0));
            UnityEditor.Handles.Label(new Vector3(_currentMinViewX, transform.position.y + camHeight * 0.5f, 0), $"Min View X\n(Pt {currentMinBoundaryIndex})");

            // Max Allowed View X (Red Line)
            Gizmos.color = Color.red;
            Gizmos.DrawLine(new Vector3(_currentMaxViewX, transform.position.y - camHeight * 1.5f, 0), new Vector3(_currentMaxViewX, transform.position.y + camHeight * 1.5f, 0));
            UnityEditor.Handles.Label(new Vector3(_currentMaxViewX, transform.position.y + camHeight * 0.5f, 0), $"Max View X\n(Pt {currentMaxBoundaryIndex})");
        
            // Draw effective camera center travel limits (Yellow)
            float minAllowedCamCenterX = _currentMinViewX + halfWidth;
            float maxAllowedCamCenterX = _currentMaxViewX - halfWidth;
            if (minAllowedCamCenterX <= maxAllowedCamCenterX) { // Only draw if valid range
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(new Vector3(minAllowedCamCenterX, transform.position.y - camHeight * 0.75f, 0), 
                                new Vector3(minAllowedCamCenterX, transform.position.y + camHeight * 0.75f, 0));
                UnityEditor.Handles.Label(new Vector3(minAllowedCamCenterX, transform.position.y - camHeight * 0.85f, 0), "Min Cam Center");

                Gizmos.DrawLine(new Vector3(maxAllowedCamCenterX, transform.position.y - camHeight * 0.75f, 0), 
                                new Vector3(maxAllowedCamCenterX, transform.position.y + camHeight * 0.75f, 0));
                UnityEditor.Handles.Label(new Vector3(maxAllowedCamCenterX, transform.position.y - camHeight * 0.85f, 0), "Max Cam Center");
            }
        }

        // All defined section boundary points
        for (int i = 0; i < sectionBoundaryPoints.Count; i++)
        {
            if (sectionBoundaryPoints[i] != null)
            {
                Gizmos.color = Color.gray;
                if (Application.isPlaying)
                {
                    if (i == currentMinBoundaryIndex) Gizmos.color = Color.green;
                    else if (i == currentMaxBoundaryIndex) Gizmos.color = Color.red;
                }
                Gizmos.DrawSphere(sectionBoundaryPoints[i].position, 0.5f);
                UnityEditor.Handles.Label(sectionBoundaryPoints[i].position + Vector3.up * 0.6f, $"Pt {i}");
            }
        }
    }
#endif
}
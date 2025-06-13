using UnityEngine;

public class ParallaxLayer : MonoBehaviour
{
    [Header("Player References")]
    [Tooltip("Assign the Transform of Player 1.")]
    [SerializeField] private Transform player1Transform;
    [Tooltip("Assign the Transform of Player 2.")]
    [SerializeField] private Transform player2Transform;

    [Header("Parallax Effect Strength")]
    [Tooltip("How much the layer moves relative to the players' midpoint movement. X for horizontal, Y for vertical.")]
    [SerializeField] private Vector2 parallaxFactor = Vector2.one; // Default to (1,1) if it's core

    [Header("Player-Driven Horizontal Oscillation")] // RENAMED SECTION
    [Tooltip("Enable the horizontal oscillation based on player-driven parallax travel.")]
    [SerializeField] private bool enableHorizontalOscillation = false;
    [Tooltip("The total horizontal distance the layer will travel due to parallax in one direction before its parallax response inverts.")]
    [SerializeField] private float oscillationTravelDistanceX = 5.0f;

    // --- State for Parallax & Oscillation ---
    private Vector3 layerInitialAnchorPosition;     // The absolute starting position of this layer
    private Vector3 playersMidpointStartPosition;
    private bool setupComplete = false;

    private float currentOscillationOriginX;        // The X-coord this layer started its current oscillation leg from
    private int currentParallaxDirectionX = 1;  // 1 or -1, determines how player X-movement affects layer X
                                                // Initially, player right -> layer right (if parallaxFactor.x > 0)

    void Start()
    {
        if (player1Transform == null || player2Transform == null)
        {
            Debug.LogError($"ParallaxLayer '{gameObject.name}': Player transforms are not assigned. Disabling script.", this);
            enabled = false;
            return;
        }

        layerInitialAnchorPosition = transform.position;
        playersMidpointStartPosition = CalculateMidpoint(player1Transform.position, player2Transform.position);
        
        if (enableHorizontalOscillation)
        {
            currentOscillationOriginX = layerInitialAnchorPosition.x; // Start oscillation from the initial position
            if (oscillationTravelDistanceX <= 0)
            {
                Debug.LogWarning($"ParallaxLayer '{gameObject.name}': Oscillation Travel Distance X is zero or negative. Oscillation will be disabled.", this);
                enableHorizontalOscillation = false;
            }
        }
        setupComplete = true;
    }

    void LateUpdate()
    {
        if (!setupComplete) return; // Players should be assigned due to Start check

        // 1. Calculate player midpoint displacement (same as before)
        Vector3 currentPlayersMidpoint = CalculateMidpoint(player1Transform.position, player2Transform.position);
        Vector3 playersMidpointDisplacement = currentPlayersMidpoint - playersMidpointStartPosition;

        // 2. Calculate raw parallax displacement based on player movement
        // This is how much the layer *would* move if there were no oscillation.
        float rawParallaxDisplacementX = playersMidpointDisplacement.x * parallaxFactor.x;
        float parallaxDisplacementY = playersMidpointDisplacement.y * parallaxFactor.y; // Y parallax is direct

        // 3. Apply horizontal oscillation logic (if enabled)
        float effectiveParallaxDisplacementX = rawParallaxDisplacementX;

        if (enableHorizontalOscillation)
        {
            // Calculate the layer's current X position based *only* on the raw parallax displacement
            // and the current oscillation direction, relative to its *current oscillation origin*.
            // This is NOT the final position yet, just how far it has "tried" to move in the current leg.
            float currentLegTravelX = (rawParallaxDisplacementX * currentParallaxDirectionX);
            
            // The target X if we just apply the current leg's travel to the oscillation origin
            float potentialLayerX = currentOscillationOriginX + currentLegTravelX;


            // Check if this potential movement exceeds the oscillationTravelDistanceX from currentOscillationOriginX
            // The actual distance traveled in the current direction from the oscillation origin
            float distanceFromOscillationOrigin = Mathf.Abs(potentialLayerX - currentOscillationOriginX);

            if (distanceFromOscillationOrigin >= oscillationTravelDistanceX)
            {
                // Reached the end of this oscillation leg.
                // 1. Clamp the movement for this frame to the boundary.
                //    The amount we overshot by is: distanceFromOscillationOrigin - oscillationTravelDistanceX
                //    We need to subtract this overshoot (in the correct direction) from potentialLayerX.
                float overshoot = distanceFromOscillationOrigin - oscillationTravelDistanceX;
                if (potentialLayerX > currentOscillationOriginX) // Moving right
                {
                    effectiveParallaxDisplacementX = currentOscillationOriginX + oscillationTravelDistanceX - layerInitialAnchorPosition.x;
                }
                else // Moving left
                {
                    effectiveParallaxDisplacementX = currentOscillationOriginX - oscillationTravelDistanceX - layerInitialAnchorPosition.x;
                }
                
                // 2. Flip the parallax direction for the *next* frame's calculations.
                currentParallaxDirectionX *= -1;

                // 3. Set the new origin for the next oscillation leg to be this clamped boundary.
                currentOscillationOriginX = layerInitialAnchorPosition.x + effectiveParallaxDisplacementX;

                Debug.Log($"ParallaxLayer '{gameObject.name}': Oscillation limit reached. New DirectionX: {currentParallaxDirectionX}, New OscillationOriginX: {currentOscillationOriginX}");
            }
            else
            {
                // Still within the current leg, apply the modified parallax
                effectiveParallaxDisplacementX = rawParallaxDisplacementX * currentParallaxDirectionX;
            }
        }
        // If not enableHorizontalOscillation, effectiveParallaxDisplacementX remains rawParallaxDisplacementX

        // 4. Calculate final target position for the layer
        // The X position is relative to its absolute starting point, modified by oscillation
        float targetX = layerInitialAnchorPosition.x + effectiveParallaxDisplacementX;
        // The Y position is always relative to its absolute starting point
        float targetY = layerInitialAnchorPosition.y + parallaxDisplacementY;

        Vector3 targetPosition = new Vector3(targetX, targetY, layerInitialAnchorPosition.z);
        transform.position = targetPosition;
    }

    private Vector3 CalculateMidpoint(Vector3 pos1, Vector3 pos2)
    {
        return (pos1 + pos2) / 2.0f;
    }
}
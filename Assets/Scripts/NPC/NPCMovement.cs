using UnityEngine;
using System.Collections.Generic; // Needed for List<Vector2Int> path

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class NPCMovement : MonoBehaviour, IMovementInputProvider // Still need this for TrajectoryMover
{
    [Header("Targeting & Sensing")]
    [SerializeField] private string playerTag = "Player";
    // [SerializeField] private float detectionRadius = 15f; // Optional: Use for initial activation
    // Removed LOS checks for simplicity, pathfinding handles obstacles

    [Header("Movement")]
    [SerializeField] private float groundMoveSpeed = 3f;
    [SerializeField] private float pathRecalculateTime = 0.5f; // How often to find a new path
    [SerializeField] private float activationDistance = 1.0f; // Distance to stop path following and just face/move towards player

    [Header("Grid Navigation")]
    [SerializeField] private LevelGridData levelGrid; // Assign GridManager object here

    [Header("Interaction")]
    [SerializeField] private LayerMask interactableLayer; // Layer mask for finding interactable objects

    [Header("Jumping (Grid Based)")]
    [SerializeField] private float maxJumpDistanceX = 2.5f; // Max horizontal cells to jump
    [SerializeField] private float maxJumpHeightY = 1.5f; // Max vertical cells up to jump
    [SerializeField] private Vector2 jumpVelocity = new Vector2(3f, 7f); // X/Y velocity for jump arc

    // --- Internal State ---
    private Rigidbody2D rb;
    private Transform playerTransform;
    private float horizontalInput = 0f; // For direct player engagement / path following direction

    // REMOVED: Exploding state
    private enum NpcState { Idle, FollowingPath, UsingInteractable, EngagingPlayer, Jumping, FallingToRepath }
    private NpcState currentState = NpcState.Idle;

    // Pathfinding State
    private List<Vector2Int> currentPath = null;
    private int currentPathIndex = 0;
    private Vector2Int currentTargetCell; // The cell the NPC is currently moving towards
    private float pathTimer = 0f;
    private Vector2Int lastKnownValidPlayerCell = Vector2Int.one * -1;

    // Interaction State
    private IInteractable currentInteractable = null; // The ladder/rope/trajectory being used
    private int interactionStartIndex = -1; // Store path index where interaction began
    private Vector3 interactionTargetWorldPos;
    private bool suppressTimerPathRecalcOnce = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 1;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (levelGrid == null) {
            levelGrid = FindObjectOfType<LevelGridData>();
        }
        if (levelGrid == null) {
             Debug.LogError($"NPCMovement on {gameObject.name}: LevelGridData not found!", this);
             enabled = false;
        }
    }

    void Start()
    {
        FindPlayer();
        if (playerTransform != null) {
             TransitionToState(NpcState.Idle); // Start Idle
             pathTimer = 0; // Force path request on first Update
        }
    }

    void FindPlayer() {
        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        playerTransform = playerObject?.transform;
        if (playerTransform == null) {
            Debug.LogError($"NPCMovement: Cannot find player with tag '{playerTag}'!", this);
            TransitionToState(NpcState.Idle);
            enabled = false;
        }
    }

    void Update()
    {
        if (playerTransform == null || levelGrid == null) return;

        pathTimer -= Time.deltaTime;

        bool timerRequiresRecalc = pathTimer <= 0;
        bool suitableStateForTimerRecalc = (currentState == NpcState.Idle || currentState == NpcState.FollowingPath || currentState == NpcState.EngagingPlayer);
        bool pathMissing = (currentState == NpcState.Idle || currentState == NpcState.FollowingPath) && currentPath == null;

        // --- MODIFIED needsNewPath Calculation ---
        bool needsNewPath = false;
        if (suppressTimerPathRecalcOnce)
        {
            // If flag is set, only recalculate if path is missing, ignore timer.
            needsNewPath = pathMissing;
            suppressTimerPathRecalcOnce = false; // Reset flag after checking
            // if (needsNewPath) Debug.Log("Timer suppressed, but path missing - requesting path.");
        }
        else
        {
            // Flag not set, use normal logic.
            needsNewPath = (suitableStateForTimerRecalc && timerRequiresRecalc) || pathMissing;
        }
        // --- End MODIFIED needsNewPath Calculation ---

        if (needsNewPath)
        {
            // Debug.Log($"Update: Requesting new path. Reason: {(pathMissing ? "PathMissing" : (timerRequiresRecalc ? "Timer" : "Other"))}. State={currentState}");
            RequestNewPath();
        }
        
        // State Machine Update
        switch (currentState)
        {
            case NpcState.Idle:              UpdateIdle();              break;
            case NpcState.FollowingPath:     UpdateFollowingPath();     break;
            case NpcState.UsingInteractable: UpdateUsingInteractable(); break;
            case NpcState.EngagingPlayer:    UpdateEngagingPlayer();    break;
            case NpcState.Jumping:           UpdateJumping();           break;
            case NpcState.FallingToRepath:   UpdateFallingToRepath();   break;
            // REMOVED: Exploding case
        }
    }

    void FixedUpdate()
    {
        // Apply movement based on state and calculated horizontal input
        ApplyMovementPhysics();
    }

    // --- Pathfinding & State Logic ---
    void RequestNewPath() {
        // Only call if not interacting or jumping/falling etc.
        if (currentState != NpcState.UsingInteractable && currentState != NpcState.Jumping && currentState != NpcState.FallingToRepath) {
            RequestNewPathFromPosition(transform.position);
        }
    }

    void UpdateIdle()
    {
         // Check if path available (might have been calculated in Update)
         if (currentPath != null && currentPath.Count > 1)
         {
              TransitionToState(NpcState.FollowingPath);
         }
         // Optional: Add check for player distance to immediately engage if close?
         else if (Vector2.Distance(transform.position, playerTransform.position) <= activationDistance * 1.5f)
         {
             TransitionToState(NpcState.EngagingPlayer);
         }
    }

    // Modify UpdateFallingToRepath
    void UpdateFallingToRepath()
    {
        // Check if grounded using the overlap circle
        bool isGrounded = Physics2D.OverlapCircle((Vector2)transform.position - new Vector2(0, 0.3f), 0.2f, levelGrid.WalkableGroundLayerMask());

        // --- CHANGE: Only request path ONCE upon landing ---
        if (isGrounded)
        {
            Debug.Log("NPC Landed after strategic fall. Attempting path request...");
            // Request path FROM the slightly more reliable landing position if possible
            // (Can still use transform.position, but RequestNewPathFromPosition has checks)
            RequestNewPathFromPosition(transform.position);

            // CRITICAL: If RequestNewPathFromPosition finds a path or decides to go Idle/Engage,
            // it WILL call TransitionToState, changing the state away from FallingToRepath.
            // If it FAILS completely (e.g., start position invalid even after adjustment),
            // the state might remain FallingToRepath. We need to handle that.
            // Add a safety check: If after requesting path we are STILL in FallingToRepath, force Idle.
            if (currentState == NpcState.FallingToRepath) {
                Debug.LogWarning("Path request after landing failed to change state. Forcing Idle.");
                TransitionToState(NpcState.Idle); // Force transition out of the falling state
            }
        }
        // If not grounded, do nothing, just continue falling.
    }

    void InitiateStrategicFall()
    {
         TransitionToState(NpcState.FallingToRepath);
         rb.gravityScale = 1; // Ensure gravity is on
         currentPath = null; // Clear any existing path
    }

    bool TryPredictiveFall(Vector2Int currentNpcCell, Vector2Int currentTargetCell)
    {
        // Check if NPC is on a platform
        float checkDist = 1.5f * levelGrid.CellSize;
        RaycastHit2D groundHit = Physics2D.Raycast(transform.position, Vector2.down, checkDist, levelGrid.WalkableGroundLayerMask());
        bool onPlatform = (groundHit.collider == null);

        if (!onPlatform) return false;

        // Simulate Landing Spot
        RaycastHit2D landingHit = Physics2D.Raycast(transform.position, Vector2.down, 50f, levelGrid.WalkableGroundLayerMask());
        if (landingHit.collider == null) return false;

        Vector3 potentialLandingPos = landingHit.point;
        if (!levelGrid.WorldToCell(potentialLandingPos, out Vector2Int landingGridCell)) return false;
        if (!levelGrid.IsWalkable(landingGridCell)) return false; // Could try FindNearestWalkableCell here

        // Simulate Pathfinding from Landing Spot
        List<Vector2Int> simulatedPath = GridPathfinder.FindPathBFS(levelGrid, landingGridCell, currentTargetCell);

        // Decision
        if (simulatedPath != null && simulatedPath.Count > 1)
        {
            InitiateStrategicFall();
            return true; // Indicate fall was initiated
        }
        return false; // Falling wouldn't help
    }

    void UpdateFollowingPath()
    {
        // --- Initial checks (path validity, engage distance) ---
        if (currentPath == null || currentPathIndex >= currentPath.Count)
        {
            RequestNewPath();
            return;
        }
        if (Vector2.Distance(transform.position, playerTransform.position) <= activationDistance)
        {
            TransitionToState(NpcState.EngagingPlayer);
            return;
        }
        // --- End Initial Checks ---

        Vector2 targetWorldPos = levelGrid.CellToWorld(currentTargetCell);
        Vector2 currentPos = transform.position;
        float distanceToTargetCell = Vector2.Distance(new Vector2(currentPos.x, currentPos.y), targetWorldPos);
        float arrivalThreshold = levelGrid.CellSize * 0.4f;

        // If close enough to the current target cell in the path...
        if (distanceToTargetCell <= arrivalThreshold)
        {
            // We've arrived at 'currentTargetCell'. Now decide what to do next based on the path.
            int arrivedAtIndex = currentPathIndex; // Index of the cell we just reached
            Vector2Int arrivedAtCell = currentTargetCell;
            int plannedNextPathIndex = arrivedAtIndex + 1; // Index of the *next* cell the path wants us to go to

            // Check if we are at the very end of the calculated path
            if (plannedNextPathIndex >= currentPath.Count)
            {
                 // Debug.Log("FollowingPath: Reached END of planned path.");
                 RequestNewPath(); // Get a new path from here
                 return;
            }

            // Get the cell the path wants us to move to next
            Vector2Int nextPlannedCell = currentPath[plannedNextPathIndex];
            int arrivedAtCellType = levelGrid.GetCellType(arrivedAtCell); // Type of cell we just arrived at
            int nextPlannedCellType = levelGrid.GetCellType(nextPlannedCell); // Type of cell we intend to move to next

            // --- Handle Transitions based on Arrived and Next Planned Cell Types ---
            bool transitionHandled = false;

            // 1. Moving to Normal Ground
            if (nextPlannedCellType == levelGrid.groundValue)
            {
                 currentPathIndex = plannedNextPathIndex; // Advance path index
                 currentTargetCell = nextPlannedCell;    // Set next ground cell as target
                 transitionHandled = true;
                 // Debug.Log($"FollowingPath: Advancing to next ground cell: {currentTargetCell}");
            }
            // 2. Moving to Jump Edge (Initiate Jump)
            else if (arrivedAtCellType == levelGrid.jumpEdgeValue || nextPlannedCellType == levelGrid.jumpEdgeValue) // Simplified Jump Check
            {
                 Vector2Int landingCell = FindNextWalkableAfterJump(arrivedAtCell, nextPlannedCell);
                 if (CanJumpTo(arrivedAtCell, landingCell))
                 {
                      PerformJump(arrivedAtCell, landingCell); // Changes state to Jumping
                      transitionHandled = true;
                 }
            }
            // 3. Moving TO an Interactable Entry Point (Ladder Bottom/Middle/Top, Rope Point) from Ground/Another Interactable Part
            else if ((arrivedAtCellType == levelGrid.groundValue || IsLadderCell(arrivedAtCellType)) && IsInteractableEntryCell(nextPlannedCellType))
            {
                  // Special Check: If already on a ladder part and next is also ladder part, DON'T re-interact yet.
                  // Interaction should only start when moving ONTO the first interactable cell.
                  // The movement WITHIN the interactable is handled by the UsingInteractable state + interactable's script.
                  // This block handles STARTING interaction.
                   if (IsLadderCell(arrivedAtCellType) && IsLadderCell(nextPlannedCellType)) {
                       // We are already on a ladder, moving to another ladder part.
                       // This shouldn't happen in FollowingPath state if interaction is working correctly.
                       // It implies interaction ended prematurely or state is wrong.
                       // Force a repath or log error.
                       Debug.LogWarning($"FollowingPath: Unexpected move between ladder cells ({arrivedAtCellType} -> {nextPlannedCellType}). Forcing repath.");
                       RequestNewPath();
                       transitionHandled = true; // Prevent falling into unhandled case below
                   }
                   else if (TryStartInteractionAt(nextPlannedCell, plannedNextPathIndex)) // Try starting interaction at the next cell
                   {
                        transitionHandled = true; // State changes to UsingInteractable inside TryStart
                   }
            }
            // --- ADDED: Handling moving *down* a ladder (already on ladder top/middle) ---
            else if (IsLadderCell(arrivedAtCellType) && IsLadderCell(nextPlannedCellType))
            {
                 // We just arrived at a ladder cell (e.g., LadderTop after ForceStopInteraction).
                 // The path wants us to go to another ladder cell (e.g., LadderMiddle).
                 // This means we need to *re-initiate* the interaction to move downwards.
                 Debug.Log($"FollowingPath: Path requires moving down ladder ({arrivedAtCellType} -> {nextPlannedCellType}). Initiating interaction at {arrivedAtCell}.");
                 // Start interaction at the cell we *just arrived at* to move down.
                 if (TryStartInteractionAt(arrivedAtCell, arrivedAtIndex)) // Use current cell/index to start downward move
                 {
                     transitionHandled = true;
                 }
                 else
                 {
                     Debug.LogError($"FollowingPath: Failed to re-initiate downward ladder interaction at {arrivedAtCell}.");
                     // Path is likely blocked or interactable missing, request new path
                     RequestNewPath();
                     transitionHandled = true; // Prevent falling into unhandled case below
                 }
            }
            // --- END ADDED ---

            // --- Handle Unhandled/Blocked Transitions ---
            if (!transitionHandled)
            {
                 Debug.LogWarning($"NPCMovement: UpdateFollowingPath: Unhandled/Blocked path node transition from {arrivedAtCell}({arrivedAtCellType}) to {nextPlannedCell}({nextPlannedCellType}). Requesting new path.");
                 RequestNewPath(); // Request path around potential blockage
            }
        }
        // If not close enough to the current target cell, ApplyMovementPhysics handles moving towards it.
    }

    // Helper function to check if a cell type is any part of a ladder
    bool IsLadderCell(int cellType) {
        return cellType == levelGrid.ladderBottomValue ||
               cellType == levelGrid.ladderMiddleValue ||
               cellType == levelGrid.ladderTopValue;
    }

    // Helper function to check if a cell type is an entry point for interaction
    bool IsInteractableEntryCell(int cellType) {
         return cellType == levelGrid.ladderBottomValue || // Can enter from bottom
                cellType == levelGrid.ladderTopValue ||    // Can enter from top (maybe?)
                // cellType == levelGrid.ladderMiddleValue || // Usually don't ENTER at middle
                cellType == levelGrid.ropePointValue;     // Can enter at rope points
    }

    bool TryStartInteractionAt(Vector2Int cell, int pathIndexOfNextCell) {
         Vector2 checkPos = levelGrid.CellToWorld(cell);
         float checkRadius = levelGrid.CellSize * 0.6f;
         Collider2D[] hits = Physics2D.OverlapCircleAll(checkPos, checkRadius, interactableLayer);

         foreach(var hit in hits) {
              IInteractable interactable = hit.GetComponentInParent<IInteractable>();
              if (interactable == null) continue;

              // --- Handle Trajectory (Ladders) ---
              if (interactable.InteractionType == InteractionType.Trajectory)
              {
                   currentInteractable = interactable;
                   horizontalInput = 0;
                   rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                   interactionStartIndex = pathIndexOfNextCell;

                   TrajectoryMover tm = ((MonoBehaviour)interactable).GetComponent<TrajectoryMover>();
                   if (tm != null && tm.PathStart != null && tm.PathEnd != null && currentPath != null && levelGrid != null && interactionStartIndex >= 0)
                   {
                        Vector2Int exitNodeCell = FindExitNodeOfInteractableSequence(currentPath, interactionStartIndex);
                        if (levelGrid.IsValidCell(exitNodeCell)) {
                            Vector3 pathExitWorldPos = levelGrid.CellToWorld(exitNodeCell);
                            float distToStart = Vector3.Distance(tm.PathStart.position, pathExitWorldPos);
                            float distToEnd = Vector3.Distance(tm.PathEnd.position, pathExitWorldPos);
                            interactionTargetWorldPos = (distToStart < distToEnd) ? tm.PathStart.position : tm.PathEnd.position;
                            
                            Debug.Log($"NPCInteraction (Trajectory DOWN?): StartIndex={interactionStartIndex}, ExitNode={exitNodeCell}, TargetPos={interactionTargetWorldPos}");
                        } else {
                            Debug.LogWarning($"NPCInteraction (Trajectory): Could not determine valid exit node after index {interactionStartIndex}. Guessing target end.");
                            Vector3 entryWorldPos = levelGrid.CellToWorld(cell);
                            float distEntryToStart = Vector3.Distance(tm.PathStart.position, entryWorldPos);
                            float distEntryToEnd = Vector3.Distance(tm.PathEnd.position, entryWorldPos);
                            interactionTargetWorldPos = (distEntryToStart < distEntryToEnd) ? tm.PathEnd.position : tm.PathStart.position;
                            Debug.LogWarning($"NPCInteraction (Trajectory DOWN?): Fallback TargetPos={interactionTargetWorldPos}");
                        }
                   } else {
                         Debug.LogError($"NPCInteraction (Trajectory): Missing references to calculate interaction target position!");
                         interactionTargetWorldPos = tm != null && tm.PathEnd != null ? tm.PathEnd.position : transform.position;
                   }

                   interactable.Interact(this.gameObject);
                   TransitionToState(NpcState.UsingInteractable);
                   return true;
              }
              // --- Handle Rope ---
              else if (interactable.InteractionType == InteractionType.Rope)
              {
                   // Debug.Log($"NPCInteraction: Found Rope interactable at cell {cell}");
                   currentInteractable = interactable;
                   horizontalInput = 0;
                   rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                   interactionStartIndex = pathIndexOfNextCell;

                   Rope rope = ((MonoBehaviour)interactable).GetComponent<Rope>();
                   if (rope != null && rope.RideStartPoint != null && rope.RideEndPoint != null)
                   {
                       Vector3 currentWorldPos = levelGrid.CellToWorld(cell);
                       float distToRopeStart = Vector3.Distance(currentWorldPos, rope.RideStartPoint.position);
                       float distToRopeEnd = Vector3.Distance(currentWorldPos, rope.RideEndPoint.position);
                       interactionTargetWorldPos = (distToRopeStart < distToRopeEnd) ? rope.RideEndPoint.position : rope.RideStartPoint.position;
                   } else {
                       Debug.LogError($"NPCInteraction (Rope): Rope component or its points are missing!");
                       interactionTargetWorldPos = transform.position;
                   }

                   interactable.Interact(this.gameObject);
                   TransitionToState(NpcState.UsingInteractable);
                   return true;
              }
         }
          return false; // No suitable interactable found or started
    }

    void UpdateUsingInteractable() {
        // If player gets close while using interactable, stop and engage
        if (playerTransform != null && Vector2.Distance(transform.position, playerTransform.position) <= activationDistance) {
            // Stop the current interaction cleanly
            StopCurrentInteraction(false); // False indicates not naturally completed
            TransitionToState(NpcState.EngagingPlayer); // Engage instead of exploding
            return;
        }
        // Otherwise, rely on ForceStopInteraction called by the interactable when done
    }

    void UpdateJumping() {
          // Check if falling or landed
          if (rb.linearVelocity.y < 0.1f)
          {
                // Confirm landing with ground check
                bool actuallyLanded = Physics2D.OverlapCircle((Vector2)transform.position - new Vector2(0, 0.3f), 0.2f, levelGrid.WalkableGroundLayerMask());
                if (actuallyLanded) {
                     RequestNewPath(); // Recalculate path from landing position
                     // State transition will happen in RequestNewPath
                }
          }
     }

    void UpdateEngagingPlayer() {
        float dist = Vector2.Distance(transform.position, playerTransform.position);

        // If player is very close, stop moving horizontally
        if (dist <= activationDistance) {
             horizontalInput = 0;
             rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
             // Stay in EngagingPlayer state
             // Other scripts might trigger attacks from here based on proximity
        }
        // If player moved far away, go back to path following
        else if (dist > activationDistance * 1.5f) { // Use a larger buffer to prevent rapid switching
             // Debug.Log("Player moved away while engaging. Requesting path.");
             RequestNewPath(); // This will transition state if path found
        }
        // If player is nearby but not extremely close, continue moving towards them
        else {
             Vector2 directionToPlayer = (playerTransform.position - transform.position);
             horizontalInput = Mathf.Sign(directionToPlayer.x);
             // Movement applied in ApplyMovementPhysics
        }
    }

    void ApplyMovementPhysics() {
        // Apply horizontal velocity based on input calculated by state updates
        if (currentState == NpcState.FollowingPath || currentState == NpcState.EngagingPlayer)
        {
            // --- Calculate Horizontal Input (if not already set by EngagingPlayer) ---
            if (currentState == NpcState.FollowingPath) {
                if (currentPath != null && currentPathIndex < currentPath.Count) {
                    Vector2 targetPos = levelGrid.CellToWorld(currentTargetCell);
                    Vector2 currentPos = transform.position;
                    Vector2 directionToTarget = targetPos - currentPos;
                    float arrivalThreshold = levelGrid.CellSize * 0.4f;

                    if (directionToTarget.magnitude > arrivalThreshold) {
                         horizontalInput = Mathf.Sign(directionToTarget.x);
                         if (Mathf.Abs(directionToTarget.x) < 0.1f) horizontalInput = 0; // Prevent jitter
                    } else {
                         horizontalInput = 0; // Close enough to target cell
                    }
                } else {
                     horizontalInput = 0; // No path/target cell
                }
            }
            // Note: EngagingPlayer state sets horizontalInput directly in its Update method

            // Apply velocity using the determined horizontal input
            rb.linearVelocity = new Vector2(horizontalInput * groundMoveSpeed, rb.linearVelocity.y);
        }
        // In other states (Idle, UsingInteractable, Jumping, Falling),
        // velocity is controlled elsewhere or should be zeroed on transition.
        // We don't apply groundMoveSpeed velocity in those states here.
    }

    // --- Helper to find the end of the interactable sequence in the path ---
    Vector2Int FindExitNodeOfInteractableSequence(List<Vector2Int> path, int startIndex)
    {
        Vector2Int fallbackTarget = Vector2Int.one * -1;
        if (playerTransform != null && levelGrid != null) {
            levelGrid.WorldToCell(playerTransform.position, out fallbackTarget);
        }
        if (path == null || startIndex < 0 || startIndex >= path.Count) {
            return fallbackTarget;
        }

        int initialInteractableCellType = levelGrid.GetCellType(path[startIndex]);
        bool isLadderSequence = (initialInteractableCellType == levelGrid.ladderBottomValue || initialInteractableCellType == levelGrid.ladderMiddleValue || initialInteractableCellType == levelGrid.ladderTopValue);
        bool isRopeSequence = (initialInteractableCellType == levelGrid.ropePointValue);

        for (int i = startIndex + 1; i < path.Count; i++)
        {
            Vector2Int currentCell = path[i];
            int currentCellType = levelGrid.GetCellType(currentCell);
            bool isPartOfSequence = (isLadderSequence && (currentCellType == levelGrid.ladderBottomValue || currentCellType == levelGrid.ladderMiddleValue || currentCellType == levelGrid.ladderTopValue)) ||
                                    (isRopeSequence && currentCellType == levelGrid.ropePointValue);

            if (!isPartOfSequence) return currentCell; // Found first node after sequence
        }
        return path[path.Count - 1]; // Path ended within sequence
    }

    // --- Jump Logic Helpers ---
    Vector2Int FindNextWalkableAfterJump(Vector2Int currentCell, Vector2Int jumpEdgeCell) {
        int directionX = Mathf.Clamp(jumpEdgeCell.x - currentCell.x, -1, 1);
        if (directionX == 0) return Vector2Int.one * -1;
        Vector2Int checkCell = jumpEdgeCell + new Vector2Int(directionX, 0);
        for(int i = 0; i < Mathf.CeilToInt(maxJumpDistanceX) + 1; i++) {
             if (!levelGrid.IsValidCell(checkCell)) return Vector2Int.one * -1;
             if (levelGrid.GetCellType(checkCell) == levelGrid.groundValue) {
                  Vector2Int cellBelow = checkCell + Vector2Int.down;
                  if (levelGrid.IsValidCell(cellBelow) && ((1 << levelGrid.GetCellLayer(cellBelow)) & levelGrid.WalkableGroundLayerMask()) != 0) {
                       return checkCell;
                  }
             }
             if (levelGrid.GetCellType(checkCell) == levelGrid.obstacleValue) return Vector2Int.one * -1;
             checkCell.x += directionX;
        }
        return Vector2Int.one * -1;
    }
    bool CanJumpTo(Vector2Int startCell, Vector2Int targetLandingCell) { if (!levelGrid.IsValidCell(targetLandingCell)) return false; int deltaX = Mathf.Abs(targetLandingCell.x - startCell.x); int deltaY = targetLandingCell.y - startCell.y; if (levelGrid.GetCellType(targetLandingCell) != levelGrid.groundValue) return false; return deltaX > 0 && deltaX <= maxJumpDistanceX && deltaY <= maxJumpHeightY; }
    void PerformJump(Vector2Int startCell, Vector2Int targetCell) { float directionX = Mathf.Sign(targetCell.x - startCell.x); float jumpX = Mathf.Max(Mathf.Abs(jumpVelocity.x), 1.0f) * directionX; rb.linearVelocity = new Vector2(jumpX, jumpVelocity.y); TransitionToState(NpcState.Jumping); currentPath = null; }

    // --- State Management & Interaction ---
    void TransitionToState(NpcState newState) {
        if (currentState == newState) return;
        Debug.Log($"{gameObject.name} FSM: {currentState} -> {newState}");
        NpcState previousState = currentState;
        currentState = newState;
        horizontalInput = 0f; // Reset horizontal input on state change

        // --- Set flag to suppress timer check next frame ---
        // If we just finished using an interactable OR just started using one
        if (previousState == NpcState.UsingInteractable || newState == NpcState.UsingInteractable)
        {
            suppressTimerPathRecalcOnce = true;
            // Debug.Log("Suppressing timer path recalc for next frame.");
        }
        // Reset interaction index when leaving UsingInteractable
        if (previousState == NpcState.UsingInteractable)
        {
            interactionStartIndex = -1;
        }
        // --- End flag setting ---

        // Handle state entry logic
        switch (currentState) {
            case NpcState.Idle:
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y); // Stop horizontal move
                currentPath = null;
                break;
            case NpcState.FollowingPath:
                // No immediate action needed, UpdateFollowingPath handles movement
                break;
            case NpcState.EngagingPlayer:
                currentPath = null; // Stop following path when engaging
                // Horizontal movement handled in UpdateEngagingPlayer and ApplyMovementPhysics
                break;
            case NpcState.UsingInteractable:
                rb.linearVelocity = Vector2.zero; // Stop all movement when starting interaction
                horizontalInput = 0;
                break;
            case NpcState.Jumping:
                horizontalInput = 0; // Jumping controls its own velocity
                break;
            case NpcState.FallingToRepath:
                 rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.5f, rb.linearVelocity.y); // Optionally dampen horizontal vel
                 horizontalInput = 0;
                 break;
            // REMOVED: Exploding case
        }
    }

    void StopCurrentInteraction(bool completed) {
        // Only trigger stop event if actually interacting
        if (currentState == NpcState.UsingInteractable && currentInteractable != null) {
            // Use EventManager or direct call if preferred
            // EventManager.TriggerInteractionStop(currentInteractable.InteractionType, rb);

            // Call ForceStopInteraction on the interactable itself if it needs cleanup
            // (This is often done by the interactable calling ForceStopInteraction back on the NPC)

            currentInteractable = null; // Clear reference
             // State transition should happen via ForceStopInteraction calling RequestNewPath
             // or by this method transitioning if needed (e.g., player gets close)
        } else if (currentInteractable != null) {
            // Clear reference even if not in the UsingInteractable state, just in case
            currentInteractable = null;
        }
    }

    // Called by interactables (Ladder, Rope) when interaction ends
    public void ForceStopInteraction() {
        // Debug.Log($"{gameObject.name} received ForceStopInteraction call. Current State: {currentState}");
        if (currentState == NpcState.UsingInteractable) {
            Vector3 positionForPathfinding = interactionTargetWorldPos; // Use the stored end position
            if (currentInteractable == null) { // Sanity check
                // Debug.LogWarning("ForceStopInteraction: currentInteractable was null, falling back to transform.position for pathfinding.");
                positionForPathfinding = transform.position;
            }
            currentInteractable = null; // Clear interactable reference
            RequestNewPathFromPosition(positionForPathfinding); // Request path from interaction end point
        }
        // If called when not interacting, do nothing
    }

    void RequestNewPathFromPosition(Vector3 startWorldPos)
    {
        pathTimer = pathRecalculateTime; // Reset timer regardless
        Debug.Log($"RequestNewPathFromPosition: Timer RESET to {pathTimer:F2} (RecalcTime={pathRecalculateTime:F2})");

        // Prevent path requests during certain critical states (but allow when called from FallingToRepath after landing)
        if (playerTransform == null || levelGrid == null || currentState == NpcState.Jumping)
        {
            // Note: We explicitly ALLOW calling this when currentState is FallingToRepath (triggered by UpdateFallingToRepath)
            // Debug.LogWarning($"RequestNewPathFromPosition: Aborting due to invalid core state ({currentState}) or missing refs.");
            return;
        }

        NpcState stateWhenCalled = currentState; // Store state for logging context

        // --- Determine Target Cell ---
        Vector2Int targetCell = Vector2Int.one * -1; // Initialize as invalid
        bool targetIsCurrentlyValid = false;
        bool targetCellObtained = false; // Flag to check if we successfully got ANY target cell

        // Try getting the player's current cell
        bool playerCellLookupSuccess = levelGrid.WorldToCell(playerTransform.position, out Vector2Int potentialTargetCell);

        // Check if the current player cell is valid *and* walkable according to the grid rules
        if (playerCellLookupSuccess && levelGrid.IsWalkable(potentialTargetCell))
        {
            targetCell = potentialTargetCell;
            lastKnownValidPlayerCell = targetCell; // Update the last known good position
            targetIsCurrentlyValid = true;
            targetCellObtained = true;
        }
        // If current player cell isn't valid/walkable, try using the last known good one
        else if (lastKnownValidPlayerCell.x >= 0 && levelGrid.IsValidCell(lastKnownValidPlayerCell))
        {
            targetCell = lastKnownValidPlayerCell;
            targetIsCurrentlyValid = false; // Mark that we're using a fallback
            targetCellObtained = true;
        }

        // If neither current nor last known position is valid, we cannot find a path target
        if (!targetCellObtained)
        {
            Debug.LogWarning($"RequestNewPathFromPosition (called from {stateWhenCalled}): Player current position invalid/unwalkable, and no valid last known position available. Cannot determine target.");
            currentPath = null; // Ensure no old path is followed
            // Only transition if not already handled by the calling state (like UpdateFallingToRepath)
            if (stateWhenCalled != NpcState.FallingToRepath) {
                TransitionToState(NpcState.Idle);
            }
            return;
        }
        // --- End Target Cell Determination ---


        // --- Determine Start Cell ---
        bool startLookupSuccess = levelGrid.WorldToCell(startWorldPos, out Vector2Int startCell);
        if (!startLookupSuccess) {
             Debug.LogWarning($"RequestNewPathFromPosition (called from {stateWhenCalled}): Start position {startWorldPos:F2} is outside grid bounds.");
             currentPath = null; // Invalidate path if start is bad
             if (stateWhenCalled != NpcState.FallingToRepath) {
                TransitionToState(NpcState.Idle);
             }
             return;
        }

        // --- IMPROVED Start Cell Walkability Check ---
        if (!levelGrid.IsWalkable(startCell)) {
            Debug.LogWarning($"RequestNewPathFromPosition (called from {stateWhenCalled}): Calculated start cell {startCell} at {startWorldPos:F2} is not walkable! Trying adjustment...");
            Vector2Int adjustedStartCell = FindNearestWalkableCell(startCell);

            // Check if adjustment found a DIFFERENT, VALID, and WALKABLE cell
            if (adjustedStartCell != startCell && levelGrid.IsValidCell(adjustedStartCell) && levelGrid.IsWalkable(adjustedStartCell)) {
                Debug.Log($"RequestNewPathFromPosition (called from {stateWhenCalled}): Adjusted start cell to {adjustedStartCell}");
                startCell = adjustedStartCell; // Use the adjusted cell
            } else {
                // Log details ONLY if adjustment failed
                 string failureReason = "";
                 if(adjustedStartCell == startCell) failureReason = "FindNearestWalkableCell returned original cell";
                 else if (!levelGrid.IsValidCell(adjustedStartCell)) failureReason = $"Adjusted cell {adjustedStartCell} is invalid";
                 else failureReason = $"Adjusted cell {adjustedStartCell} is still not walkable";

                Debug.LogError($"RequestNewPathFromPosition (called from {stateWhenCalled}): Start cell {startCell} (derived from {startWorldPos:F2}) and its neighbours are not walkable. CANNOT PATHFIND. Reason: {failureReason}");
                currentPath = null; // Invalidate path
                 if (stateWhenCalled != NpcState.FallingToRepath) {
                    TransitionToState(NpcState.Idle);
                 }
                return; // Critical failure if start isn't walkable
            }
        }
        // --- End Start Cell Determination ---


        // --- Perform Pathfinding ---
        // Debug.Log($"RequestNewPathFromPosition (called from {stateWhenCalled}): Attempting path from {startCell} to {targetCell}");

        List<Vector2Int> foundPath = GridPathfinder.FindPathBFS(levelGrid, startCell, targetCell);

        if (foundPath != null && foundPath.Count > 1)
        {
            // --- Path Found! ---
            currentPath = foundPath;
            currentPathIndex = 1; // Start from the second node
            currentTargetCell = currentPath[currentPathIndex];
            Debug.Log($"RequestNewPathFromPosition (called from {stateWhenCalled}): Path FOUND. Length: {currentPath.Count}. Target was {(targetIsCurrentlyValid ? "current" : "last known")}. Transitioning to FollowingPath.");
            // Always transition if path is found, especially important after falling or interaction ends
            TransitionToState(NpcState.FollowingPath);
        }
        else // --- Path NOT Found ---
        {
            Debug.LogWarning($"RequestNewPathFromPosition (called from {stateWhenCalled}): Path NOT found from {startCell} to {targetCell} (Target was {(targetIsCurrentlyValid ? "current" : "last known")}).");

            // Check if we should try falling (only if not already in a transition state)
            bool fallInitiated = false;
            if (stateWhenCalled != NpcState.FallingToRepath && stateWhenCalled != NpcState.Jumping && stateWhenCalled != NpcState.UsingInteractable)
            {
                fallInitiated = TryPredictiveFall(startCell, targetCell);
            }

            // If fall wasn't initiated (or wasn't attempted), decide the final state
            if (!fallInitiated)
            {
                // Debug.Log($"RequestNewPathFromPosition (called from {stateWhenCalled}): Predictive Fall check failed or skipped. Deciding final state.");
                currentPath = null; // Clear any old path since we couldn't find a new one AND didn't fall

                // Only transition state IF this request wasn't triggered by UpdateFallingToRepath
                // (because that method handles the final transition if pathfinding fails)
                if (stateWhenCalled != NpcState.FallingToRepath)
                {
                     if (Vector2.Distance(transform.position, playerTransform.position) <= activationDistance * 1.5f) {
                         TransitionToState(NpcState.EngagingPlayer); // Engage if close and stuck
                     } else {
                         TransitionToState(NpcState.Idle); // Go idle if far and stuck
                     }
                } else {
                     // If called from FallingToRepath and path still not found, let UpdateFallingToRepath force Idle
                     Debug.LogWarning($"RequestNewPathFromPosition (called from {stateWhenCalled}): Path request failed. UpdateFallingToRepath will handle forced Idle.");
                }
            }
            // If fall WAS initiated (fallInitiated is true), the TransitionToState(FallingToRepath)
            // happened inside InitiateStrategicFall(), so we don't transition state here.
        }
    } // End RequestNewPathFromPosition

    Vector2Int FindNearestWalkableCell(Vector2Int cell) {
        if (levelGrid.IsWalkable(cell)) return cell;
        Vector2Int[] neighbours = { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1) };
        for (int i = 0; i < neighbours.Length; i++) {
            Vector2Int checkCell = cell + neighbours[i];
            if (levelGrid.IsValidCell(checkCell) && levelGrid.IsWalkable(checkCell)) { return checkCell; }
        }
        return cell;
    }

    // --- IMovementInputProvider Implementation ---
    public Vector2 GetMovementInput() {
        if (currentState == NpcState.UsingInteractable && currentInteractable != null) {
             if (currentInteractable.InteractionType == InteractionType.Trajectory) {
                  TrajectoryMover tm = ((MonoBehaviour)currentInteractable)?.GetComponent<TrajectoryMover>();
                   if (tm != null && tm.PathStart != null && tm.PathEnd != null && playerTransform != null && levelGrid != null && currentPath != null && interactionStartIndex != -1) {
                       MovementAxis controlAxis = tm.GetCalculatedAxis();
                       Vector2Int exitNodeCell = FindExitNodeOfInteractableSequence(currentPath, interactionStartIndex);
                        if (levelGrid.IsValidCell(exitNodeCell)) {
                            Vector3 targetWorldPos = levelGrid.CellToWorld(exitNodeCell);
                            Vector3 pathStartPos = tm.PathStart.position;
                            Vector3 pathEndPos = tm.PathEnd.position;
                            Vector3 currentPos = transform.position;
                            float distToStart = Vector3.Distance(pathStartPos, targetWorldPos);
                            float distToEnd = Vector3.Distance(pathEndPos, targetWorldPos);
                            Vector3 targetEndOnTrajectory = (distToStart < distToEnd) ? pathStartPos : pathEndPos;
                            Vector3 currentOnLine = tm.ProjectPointOnLineSegment(pathStartPos, pathEndPos, currentPos);
                            float direction = 0;
                            
                            Debug.Log($"GetMovementInput(DOWN?): ExitNode={exitNodeCell}, TargetEnd={targetEndOnTrajectory.y:F2}, CurrentY={currentOnLine.y:F2}, RawDir={direction:F1}");
                            
                            if(controlAxis == MovementAxis.Vertical) { direction = Mathf.Sign(targetEndOnTrajectory.y - currentOnLine.y); }
                            else { direction = Mathf.Sign(targetEndOnTrajectory.x - currentOnLine.x); }
                            if (Vector3.Distance(currentOnLine, targetEndOnTrajectory) < 0.15f) direction = 0;
                            if (controlAxis == MovementAxis.Vertical) { return new Vector2(0, direction); }
                            else { return new Vector2(direction, 0); }
                        }
                   }
             }
             // Add Rope movement input logic if needed (e.g., if ropes allow user input)
             // else if (currentInteractable.InteractionType == InteractionType.Rope) { ... }
        }
        return Vector2.zero; // Default to no movement input
    }

    // --- Public Accessors for TrajectoryMover/Rope ---
    public List<Vector2Int> GetCurrentPathForInteraction() { return currentPath; }
    public int GetInteractionStartIndex() { return interactionStartIndex; }
    public LevelGridData GetLevelGrid() { return levelGrid; }
}
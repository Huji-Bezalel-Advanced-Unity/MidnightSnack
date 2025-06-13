using UnityEngine;
using System.Collections.Generic;

public class LevelGridData : MonoBehaviour
{
    [Header("Grid Definition")]
    [SerializeField] private int gridWidth = 20;
    [SerializeField] private int gridHeight = 15;
    [SerializeField] private Vector2 gridOrigin = Vector2.zero; // World pos of bottom-left cell's CENTER
    [SerializeField] private float cellSize = 1.0f;

    [Header("Generation Settings")]
    [Tooltip("Layers considered as walkable ground.")]
    [SerializeField] private LayerMask groundLayer;
    [Tooltip("Layers considered as obstacles/walls.")]
    [SerializeField] private LayerMask obstacleLayer;
    [Tooltip("Layers containing interactable entry points (ladders, ropes).")]
    [SerializeField] private LayerMask interactableLayer;
    [Tooltip("How much of the cell needs to be clear of obstacles to be walkable (0 to 1). Checks slightly smaller area.")]
    [Range(0.1f, 1.0f)]
    [SerializeField] private float walkableOverlapRadiusFactor = 0.8f; // Reduced slightly maybe?
    [Tooltip("Vertical offset from cell center to check for ground below.")]
    [SerializeField] private float groundCheckOffsetY = -0.5f; // Check near the bottom of the cell
    [Tooltip("Radius for the ground check below the cell center.")]
    [SerializeField] private float groundCheckRadius = 0.1f; // Small radius for ground check

    [Header("Cell Type Definitions")]
    public int obstacleValue = 0;
    public int groundValue = 1;
    public int jumpEdgeValue = 3; // We might need a different way to detect these now
    public int ladderBottomValue = 4;
    public int ladderMiddleValue = 5;
    public int ladderTopValue = 6;
    public int ropePointValue = 7;
    // Add more as needed

    // REMOVED: Inspector array for manual editing
    // [SerializeField] private GridCellData[] gridCells;
    // [SerializeField] private GridRow[] gridRows;

    // Internal grid data
    private int[,] gridData;
    private bool isInitialized = false; // Flag to check if generation ran

    // --- Public Accessors ---
    public int Width => gridWidth;
    public int Height => gridHeight;
    public float CellSize => cellSize;
    public Vector2 Origin => gridOrigin;
    public bool IsInitialized => isInitialized;

    void Awake()
    {
        // Generation now happens on Awake or explicitly via button
        GenerateGridData();
    }

    // --- Grid Generation ---
    public void GenerateGridData()
    {
        Debug.Log($"LevelGridData: Starting grid generation ({gridWidth}x{gridHeight})...");
        gridData = new int[gridWidth, gridHeight];
        int generatedGround = 0;
        int generatedObstacles = 0;
        int generatedInteractables = 0;
        int generatedEmpty = 0;

        float obstacleCheckRadius = (cellSize / 2.0f) * walkableOverlapRadiusFactor;

        // --- Loop through cells and determine type ---
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                // ... (existing logic to determine cell type and store in gridData[x, y])
                 Vector2 cellCenter = CellToWorld(x, y);
                int determinedValue = obstacleValue; // Start by assuming obstacle

                // --- CHECK 1: Is this cell ITSELF blocked by an obstacle? ---
                if (Physics2D.OverlapCircle(cellCenter, obstacleCheckRadius, obstacleLayer))
                {
                    determinedValue = obstacleValue;
                    generatedObstacles++;
                }
                else // Cell is not directly blocked by an obstacle
                {
                    // --- CHECK 2: Is there GROUND directly below this cell? ---
                    Vector2 groundCheckPos = cellCenter + new Vector2(0, groundCheckOffsetY * cellSize);
                    bool groundBelow = Physics2D.OverlapCircle(groundCheckPos, groundCheckRadius, groundLayer);

                    // --- CHECK 3: Does this cell contain an INTERACTABLE entry point? ---
                    Collider2D interactableHit = Physics2D.OverlapCircle(cellCenter, obstacleCheckRadius, interactableLayer);
                    int interactableValue = obstacleValue;
                    if (interactableHit != null)
                    {
                        interactableValue = GetInteractableCellValue(interactableHit, cellCenter);
                        if(interactableValue != obstacleValue) generatedInteractables++;
                    }

                    // --- Determine Final Cell Type ---
                    if (interactableValue != obstacleValue)
                    {
                         determinedValue = interactableValue;
                    }
                    else if (groundBelow)
                    {
                        determinedValue = groundValue;
                        generatedGround++;
                    }
                    else
                    {
                        determinedValue = obstacleValue;
                        generatedEmpty++;
                    }
                }
                gridData[x, y] = determinedValue;
            }
        }
        // --- End Loop ---

        isInitialized = true; // Mark grid as ready
        Debug.Log($"LevelGridData: Generation complete. Ground: {generatedGround}, Obstacles: {generatedObstacles}, Interactables: {generatedInteractables}, EmptyAir(Obstacle): {generatedEmpty}");

        // ***** ADD THIS LINE *****
        // After the grid is generated and initialized, build the rope lookup table.
        RopeLookupData.BuildLookup(this); // Pass self (this LevelGridData instance)
        // *************************
    } // End of GenerateGridData method

    // Added cellCenter parameter to help determine position relative to interactable
    private int GetInteractableCellValue(Collider2D hitCollider, Vector2 cellCenter)
    {
        IInteractable interactable = hitCollider.GetComponentInParent<IInteractable>();
        if (interactable == null) return obstacleValue;

        switch (interactable.InteractionType)
        {
            case InteractionType.Trajectory:
                TrajectoryMover tm = ((MonoBehaviour)interactable).GetComponent<TrajectoryMover>();
                if (tm != null && tm.PathStart != null && tm.PathEnd != null)
                {
                    float distToStart = Vector2.Distance(cellCenter, tm.PathStart.position);
                    float distToEnd = Vector2.Distance(cellCenter, tm.PathEnd.position);
                    // Project cell center onto path to check if it's along the main line
                    Vector3 projected = tm.ProjectPointOnLineSegment(tm.PathStart.position, tm.PathEnd.position, cellCenter);
                    float distToProjected = Vector2.Distance(cellCenter, projected);

                    float endTolerance = cellSize * 0.7f; // How close to end points counts as top/bottom
                    float lineTolerance = cellSize * 0.5f; // How close to the line counts as middle

                    // Check if close to the line segment itself first
                    if (distToProjected < lineTolerance) {
                        // Now check proximity to ends
                         if (distToStart < endTolerance && distToStart < distToEnd) return ladderBottomValue; // Closer to start
                         if (distToEnd < endTolerance && distToEnd < distToStart) return ladderTopValue;   // Closer to end
                         // If close to line but not ends, it's ladder middle
                         return ladderMiddleValue;
                    }
                }
                // If not clearly on the ladder path, maybe return ground if ground is also present?
                // Or return a specific "near ladder" value? For now, fallback.
                return obstacleValue; // Fallback if trajectory data invalid or not close enough

            case InteractionType.Rope:
                // Check proximity to rope start/end points
                 Rope rope = ((MonoBehaviour)interactable).GetComponent<Rope>();
                 if (rope != null && rope.RideStartPoint != null && rope.RideEndPoint != null) {
                       float distToStart = Vector2.Distance(cellCenter, rope.RideStartPoint.position);
                       float distToEnd = Vector2.Distance(cellCenter, rope.RideEndPoint.position);
                       float tolerance = cellSize * 0.7f;
                        if (distToStart < tolerance || distToEnd < tolerance) {
                             return ropePointValue; // Mark cell near rope ends
                        }
                 }
                return obstacleValue; // Not near rope ends

            default:
                return obstacleValue;
        }
    }


    // --- WorldToCell, CellToWorld, GetCellType, IsValidCell, IsWalkable ---
    // --- (No changes needed in these methods, but use isInitialized flag) ---
     public bool WorldToCell(Vector2 worldPos, out Vector2Int cellCoords)
    {
        Vector2 relativePos = worldPos - gridOrigin;
        int x = Mathf.FloorToInt((relativePos.x / cellSize) + 0.5f);
        int y = Mathf.FloorToInt((relativePos.y / cellSize) + 0.5f);
        cellCoords = new Vector2Int(x, y);
        return IsValidCell(x, y);
    }
    public Vector2 CellToWorld(Vector2Int cellCoords) { return CellToWorld(cellCoords.x, cellCoords.y); }
    public Vector2 CellToWorld(int x, int y) { return new Vector2(gridOrigin.x + x * cellSize, gridOrigin.y + y * cellSize); }
    public int GetCellType(int x, int y)
    {
        if (!isInitialized || !IsValidCell(x, y)) { return obstacleValue; } // Check initialization
        return gridData[x, y];
    }
    public int GetCellType(Vector2Int cellCoords) { return GetCellType(cellCoords.x, cellCoords.y); }
    public bool IsValidCell(int x, int y) { return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight; }
    public bool IsValidCell(Vector2Int cellCoords) { return IsValidCell(cellCoords.x, cellCoords.y); }
    
    // Now needs to consider more than just obstacles
    public bool IsWalkable(int x, int y)
    {
        int cellType = GetCellType(x,y);
        // Define which cell types the pathfinder can traverse
        return cellType == groundValue ||
               cellType == jumpEdgeValue ||       // Can stand on edge before jumping
               cellType == ladderBottomValue ||   // Can stand at bottom
               cellType == ladderMiddleValue ||   // Can move through middle
               cellType == ladderTopValue ||      // Can stand at top
               cellType == ropePointValue;        // Can stand near rope points
        // Add any other "traversable" cell types here
        // Note: obstacleValue (0) should not be included
    }
    public bool IsWalkable(Vector2Int cellCoords) { return IsWalkable(cellCoords.x, cellCoords.y); }


    public LayerMask WalkableGroundLayerMask() {
        // Combine layers considered walkable ground for physics checks
        // Assuming groundLayer is already set in Inspector
        return groundLayer; // Or combine multiple layers if needed: return groundLayer | anotherWalkableLayer;
    }

    // Optional helper if needed for jump check, assumes grid stores layer info (it doesn't currently)
    public int GetCellLayer(Vector2Int cellCoords) {
         // This requires more complex grid generation storing layer per cell,
         // or performing a Physics check here.
         // Simplification: Assume groundLayer is the only walkable one for now.
          if (GetCellType(cellCoords) == groundValue) {
               // Find the layer int value corresponding to the first layer in groundLayer mask
               for(int i=0; i<32; i++){
                    if((groundLayer.value & (1 << i)) != 0) return i;
               }
          }
          return -1; // Indicate not found or not ground
    }

    // --- Gizmo Visualization ---
    void OnDrawGizmos()
    {
        if (!isInitialized || gridData == null || gridWidth <= 0 || gridHeight <= 0 || cellSize <= 0) return; // Check initialization

        Vector3 cubeSize = new Vector3(cellSize, cellSize, 0.1f);

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                // Get type directly from the generated gridData
                int cellType = gridData[x,y];
                Gizmos.color = GetCellColor(cellType);
                Vector3 cellCenter = CellToWorld(x, y);
                Gizmos.DrawWireCube(cellCenter, cubeSize * 0.9f);
            }
        }
    }

    // GetCellColor unchanged
    private Color GetCellColor(int cellType)
    {
        if (cellType == groundValue) return Color.green;
        if (cellType == jumpEdgeValue) return Color.yellow;
        if (cellType == ladderBottomValue) return new Color(1.0f, 0.5f, 0.0f); // Orange
        if (cellType == ladderMiddleValue) return new Color(1.0f, 0.7f, 0.3f); // Lighter Orange
        if (cellType == ladderTopValue) return new Color(1.0f, 0.9f, 0.5f); // Yellowish Orange
        if (cellType == ropePointValue) return Color.cyan;
        if (cellType == obstacleValue) return Color.red;
        return Color.gray; // Unknown type
    }

    // Removed struct GridCellData / GridRow[]
}

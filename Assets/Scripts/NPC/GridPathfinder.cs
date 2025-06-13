using UnityEngine;
using System.Collections.Generic;

public static class GridPathfinder
{
    // Get the rope lookup data (consider passing it in if you prefer)
    private static Dictionary<Vector2Int, Rope> GetRopeDataLookup() {
        // Assumes RopeLookupData.BuildLookup() has been called elsewhere
        return RopeLookupData.GetLookup();
    }

    public static List<Vector2Int> FindPathBFS(LevelGridData gridData, Vector2Int startCell, Vector2Int targetCell)
    {
        if (gridData == null || !gridData.IsValidCell(startCell) || !gridData.IsValidCell(targetCell))
        {
            return null; // Invalid input
        }

        if (!gridData.IsWalkable(startCell) || !gridData.IsWalkable(targetCell)) {
             // Allow pathfinding *from* a rope point even if it's the only walkable start
            // But still need the target to be generally walkable
             // Let's refine: Check if target is walkable. Start walkability is less strict if it's a rope point.
             if (!gridData.IsWalkable(targetCell)) {
                  // Debug.LogWarning("Pathfinding Warning: Target cell is not walkable.");
                  return null;
             }
             if (!gridData.IsWalkable(startCell) && gridData.GetCellType(startCell) != gridData.ropePointValue) {
                  // Debug.LogWarning("Pathfinding Warning: Start cell is not walkable and not a rope point.");
                  return null;
             }
        }

        if (startCell == targetCell) {
             return new List<Vector2Int> { startCell };
        }

        // Get the rope lookup dictionary
        Dictionary<Vector2Int, Rope> ropeLookup = GetRopeDataLookup();
        if (ropeLookup == null) {
            Debug.LogError("GridPathfinder: Rope Lookup Data is null! Cannot perform rope transitions.");
            // Decide: Continue without ropes, or fail? Let's fail for now.
            return null;
        }

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        queue.Enqueue(startCell);
        visited.Add(startCell);
        cameFrom[startCell] = startCell;

        Vector2Int[] neighbours = new Vector2Int[] {
            new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0)
        };

        bool targetFound = false;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            if (current == targetCell)
            {
                targetFound = true;
                break;
            }

            // --- 1. Explore NORMAL Adjacent Neighbours ---
            foreach (Vector2Int offset in neighbours)
            {
                Vector2Int neighbour = current + offset;

                if (gridData.IsValidCell(neighbour) &&
                    gridData.IsWalkable(neighbour) && // Standard walkability check
                    !visited.Contains(neighbour))
                {
                    visited.Add(neighbour);
                    cameFrom[neighbour] = current;
                    queue.Enqueue(neighbour);
                }
            }

            // --- 2. Explore ROPE Transitions ---
            int currentCellType = gridData.GetCellType(current);
            if (currentCellType == gridData.ropePointValue) // Check if the current cell is a rope end
            {
                // Find which rope this cell belongs to using the lookup
                if (ropeLookup.TryGetValue(current, out Rope currentRope))
                {
                    // Determine the world position of the *other* end of this specific rope
                    Vector3 currentEndPos = gridData.CellToWorld(current); // Or use rope start/end directly
                    Vector3 otherEndPos;
                    // Check distance to physical points to identify which one 'current' represents
                    if (Vector3.Distance(currentEndPos, currentRope.RideStartPoint.position) < gridData.CellSize * 0.5f) {
                        otherEndPos = currentRope.RideEndPoint.position; // 'current' is the start, target the end
                    } else {
                        otherEndPos = currentRope.RideStartPoint.position; // 'current' is the end, target the start
                    }

                    // Convert the other end's world position back to a grid cell
                    if (gridData.WorldToCell(otherEndPos, out Vector2Int otherRopeCell))
                    {
                        // Check if the other end cell is valid, is also a rope point, and hasn't been visited
                        if (otherRopeCell != current && // Ensure it's not the same cell
                            gridData.IsValidCell(otherRopeCell) &&
                            gridData.GetCellType(otherRopeCell) == gridData.ropePointValue &&
                            !visited.Contains(otherRopeCell))
                        {
                            // Treat the other end of the rope as a directly reachable neighbour
                            // Debug.Log($"Pathfinding: Found rope transition from {current} to {otherRopeCell} via {currentRope.name}");
                            visited.Add(otherRopeCell);
                            cameFrom[otherRopeCell] = current; // Record path coming from 'current'
                            queue.Enqueue(otherRopeCell);    // Add the other end to the queue to explore from it
                        }
                    }
                }
                // else: This ropePointValue cell wasn't in our lookup - maybe BuildLookup failed?
            }
            // --- End Rope Transitions ---

             // --- TODO LATER: Add Ladder/Jump Transitions here in a similar way if needed ---

        } // End While Loop

        // --- Reconstruct Path --- (No changes needed here)
        if (targetFound)
        {
            List<Vector2Int> path = new List<Vector2Int>();
            Vector2Int pathCurrent = targetCell; // Use temp variable for reconstruction
            while (pathCurrent != startCell)
            {
                path.Add(pathCurrent);
                if (!cameFrom.TryGetValue(pathCurrent, out pathCurrent)) {
                     Debug.LogError($"Path reconstruction failed: cameFrom dictionary incomplete. Target={targetCell}, Start={startCell}. Current Broken Link Point: {path[path.Count-1]}");
                     // Log dictionary contents for debugging
                     // foreach(var kvp in cameFrom) { Debug.Log($"cameFrom[{kvp.Key}] = {kvp.Value}"); }
                     return null; // Path is broken
                 }
            }
            path.Add(startCell);
            path.Reverse();
            return path;
        }
        else
        {
            return null; // No path exists
        }
    }

     // --- TODO LATER: Add FindPathAStar(..., Func<Vector2Int, Vector2Int, float> costFunction) ---
}
using UnityEngine;
using System.Collections.Generic;

public static class RopeLookupData
{
    // Static dictionary to store the mapping
    private static Dictionary<Vector2Int, Rope> ropeCellLookup = null;
    private static bool isInitialized = false;

    // Call this ONCE during level initialization (e.g., from a GameManager Start/Awake)
    // AFTER the LevelGridData has been generated.
    public static void BuildLookup(LevelGridData gridData)
    {
        if (gridData == null || !gridData.IsInitialized)
        {
            Debug.LogError("RopeLookupData: Cannot build lookup, LevelGridData is null or not initialized!");
            return;
        }

        ropeCellLookup = new Dictionary<Vector2Int, Rope>();
        Rope[] allRopes = Object.FindObjectsOfType<Rope>(); // Find all Rope objects in the scene

        Debug.Log($"RopeLookupData: Found {allRopes.Length} ropes. Building lookup...");

        foreach (Rope rope in allRopes)
        {
            if (rope.RideStartPoint == null || rope.RideEndPoint == null)
            {
                Debug.LogWarning($"RopeLookupData: Skipping rope '{rope.gameObject.name}' because start or end point is missing.", rope);
                continue;
            }

            // Find grid cell for Start Point
            if (gridData.WorldToCell(rope.RideStartPoint.position, out Vector2Int startCell))
            {
                // Verify the cell type matches (optional sanity check)
                if (gridData.GetCellType(startCell) == gridData.ropePointValue)
                {
                    if (!ropeCellLookup.ContainsKey(startCell)) {
                        ropeCellLookup.Add(startCell, rope);
                         // Debug.Log($"RopeLookupData: Added {startCell} -> {rope.gameObject.name} (Start Point)");
                    } else if (ropeCellLookup[startCell] != rope) {
                        Debug.LogWarning($"RopeLookupData: Cell {startCell} already mapped to a different rope ({ropeCellLookup[startCell].name})! Overwriting with {rope.name}. Check for overlapping rope ends.", rope);
                        ropeCellLookup[startCell] = rope; // Overwrite might hide issues
                    }
                } else {
                     // Debug.LogWarning($"RopeLookupData: Cell {startCell} for rope '{rope.gameObject.name}' start point is not type 'ropePointValue' (Type: {gridData.GetCellType(startCell)}). Check Grid Generation.", rope);
                }
            } else {
                 // Debug.LogWarning($"RopeLookupData: Start point for rope '{rope.gameObject.name}' at {rope.RideStartPoint.position} is outside grid bounds.", rope);
            }


            // Find grid cell for End Point
             if (gridData.WorldToCell(rope.RideEndPoint.position, out Vector2Int endCell))
             {
                 if (gridData.GetCellType(endCell) == gridData.ropePointValue)
                 {
                     if (!ropeCellLookup.ContainsKey(endCell)) {
                         ropeCellLookup.Add(endCell, rope);
                          // Debug.Log($"RopeLookupData: Added {endCell} -> {rope.gameObject.name} (End Point)");
                     } else if (ropeCellLookup[endCell] != rope) {
                         Debug.LogWarning($"RopeLookupData: Cell {endCell} already mapped to a different rope ({ropeCellLookup[endCell].name})! Overwriting with {rope.name}. Check for overlapping rope ends.", rope);
                         ropeCellLookup[endCell] = rope;
                     }
                 } else {
                     // Debug.LogWarning($"RopeLookupData: Cell {endCell} for rope '{rope.gameObject.name}' end point is not type 'ropePointValue' (Type: {gridData.GetCellType(endCell)}). Check Grid Generation.", rope);
                 }
             } else {
                  // Debug.LogWarning($"RopeLookupData: End point for rope '{rope.gameObject.name}' at {rope.RideEndPoint.position} is outside grid bounds.", rope);
             }
        }
        isInitialized = true;
        Debug.Log($"RopeLookupData: Lookup built. Mapped {ropeCellLookup.Count} rope endpoints.");
    }

    // Method for the pathfinder to access the lookup
    public static Dictionary<Vector2Int, Rope> GetLookup()
    {
        if (!isInitialized)
        {
            Debug.LogError("RopeLookupData: Attempted to GetLookup before BuildLookup was called!");
            return null; // Or return an empty dictionary: new Dictionary<Vector2Int, Rope>();
        }
        return ropeCellLookup;
    }

     // Optional: Add a Reset method if you need to rebuild the lookup dynamically
     public static void ResetLookup() {
         ropeCellLookup = null;
         isInitialized = false;
     }
}
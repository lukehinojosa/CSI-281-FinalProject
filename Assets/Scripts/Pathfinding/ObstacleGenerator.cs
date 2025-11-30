using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ObstacleGenerator : MonoBehaviour
{
    [Header("References")]
    public Grid grid;
    public GameObject obstaclePrefab;
    public Transform obstacleContainer;

    [Header("Settings")]
    public int obstacleCount = 20;
    public float obstacleHeight = 1.0f;
    [Tooltip("The minimum distance between generated obstacles.")]
    public float minSpacing = 2.0f;
    [Tooltip("Layers to check for existing objects to avoid placing on top of.")]
    public LayerMask collisionMask;

    [Header("Actions")]
    public bool generateObstacles = false;

    private struct GridCoord
    {
        public int x;
        public int y;
        public GridCoord(int x, int y) { this.x = x; this.y = y; }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (generateObstacles)
        {
            EditorApplication.delayCall += GenerateLevel;
            generateObstacles = false;
        }
    }
#endif

    public void GenerateLevel()
    {
        if (grid == null || obstaclePrefab == null)
        {
            Debug.LogError("Grid or Obstacle Prefab not assigned!");
            return;
        }

        // 1. Remove Old Obstacles
        if (obstacleContainer == null)
        {
            GameObject containerObj = new GameObject("GeneratedObstacles");
            obstacleContainer = containerObj.transform;
        }

        while (obstacleContainer.childCount > 0)
        {
            DestroyImmediate(obstacleContainer.GetChild(0).gameObject);
        }

        // 2. Setup Grid Dimensions
        float nodeDiameter = grid.nodeRadius * 2;
        int gridSizeX = Mathf.RoundToInt(grid.gridWorldSize.x / nodeDiameter);
        int gridSizeY = Mathf.RoundToInt(grid.gridWorldSize.y / nodeDiameter);
        
        Vector3 worldBottomLeft = grid.transform.position 
                                - Vector3.right * grid.gridWorldSize.x / 2 
                                - Vector3.forward * grid.gridWorldSize.y / 2;

        // 3. Create a list of all possible coordinates
        List<GridCoord> allCoords = new List<GridCoord>();
        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                allCoords.Add(new GridCoord(x, y));
            }
        }

        // 4. Shuffle the list (Fisher-Yates Shuffle)
        for (int i = 0; i < allCoords.Count; i++)
        {
            GridCoord temp = allCoords[i];
            int randomIndex = Random.Range(i, allCoords.Count);
            allCoords[i] = allCoords[randomIndex];
            allCoords[randomIndex] = temp;
        }

        // 5. Placement Loop
        List<GridCoord> placedCoords = new List<GridCoord>();
        int placedCount = 0;

        // Iterate through pre-shuffled coordinates
        foreach (GridCoord coord in allCoords)
        {
            // Stop if target count is hit
            if (placedCount >= obstacleCount) break;

            // Spacing Check
            // Check if this coordinate is too close to any already generated
            if (IsTooCloseToGenerated(coord, placedCoords))
            {
                continue; // Skip this spot
            }

            // Collision Check
            Vector3 worldPoint = worldBottomLeft 
                               + Vector3.right * (coord.x * nodeDiameter + grid.nodeRadius) 
                               + Vector3.forward * (coord.y * nodeDiameter + grid.nodeRadius);
            worldPoint.y = grid.transform.position.y;

            // Check if something tile is occupied
            if (!Physics.CheckBox(worldPoint, new Vector3(nodeDiameter * 0.9f, 2f, nodeDiameter * 0.9f) / 2, Quaternion.identity, collisionMask))
            {
                // Spot is valid, so place obstacle.
                SpawnObstacle(worldPoint, nodeDiameter);
                
                // Record placement
                placedCoords.Add(coord);
                placedCount++;
            }
        }

        Debug.Log($"Generated {placedCount} obstacles.");
        grid.UpdateGridObstacles();
    }

    private bool IsTooCloseToGenerated(GridCoord candidate, List<GridCoord> existing)
    {
        for (int i = existing.Count - 1; i >= 0; i--)
        {
            float dist = Mathf.Sqrt(Mathf.Pow(candidate.x - existing[i].x, 2) + Mathf.Pow(candidate.y - existing[i].y, 2));
            if (dist < minSpacing)
            {
                return true;
            }
        }
        return false;
    }

    private void SpawnObstacle(Vector3 position, float diameter)
    {
        #if UNITY_EDITOR
            GameObject newWall = (GameObject)PrefabUtility.InstantiatePrefab(obstaclePrefab);
        #else
            GameObject newWall = Instantiate(obstaclePrefab);
        #endif
        
        newWall.transform.position = position;
        newWall.transform.SetParent(obstacleContainer);
        
        // Scale
        newWall.transform.localScale = new Vector3(diameter * 0.95f, obstacleHeight, diameter * 0.95f);
        
        int unwalkableLayer = LayerMask.NameToLayer("Unwalkable");
        if (unwalkableLayer != -1) newWall.layer = unwalkableLayer;
    }
}
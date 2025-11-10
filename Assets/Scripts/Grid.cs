using UnityEngine;
using System.Collections.Generic;

public class Grid : MonoBehaviour
{
    [Tooltip("Enable this for live editing of obstacles in the Scene view during Play mode. Very slow, disable for actual gameplay.")]
    public bool allowRuntimeGridUpdates = false;

    public LayerMask unwalkableMask;
    public Vector2 gridWorldSize;
    public float nodeRadius;

    private Node[,] grid;
    private float nodeDiameter;
    private int gridSizeX, gridSizeY;

    void Awake()
    {
        nodeDiameter = nodeRadius * 2;
        gridSizeX = Mathf.RoundToInt(gridWorldSize.x / nodeDiameter);
        gridSizeY = Mathf.RoundToInt(gridWorldSize.y / nodeDiameter);
        CreateGrid();
    }
    
    // This is new! It allows for live updates for debugging.
    void Update()
    {
        if (allowRuntimeGridUpdates)
        {
            UpdateGridObstacles();
        }
    }

    void CreateGrid()
    {
        grid = new Node[gridSizeX, gridSizeY];
        Vector3 worldBottomLeft = transform.position - Vector3.right * gridWorldSize.x / 2 - Vector3.up * gridWorldSize.y / 2;

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Vector3 worldPoint = worldBottomLeft + Vector3.right * (x * nodeDiameter + nodeRadius) + Vector3.up * (y * nodeDiameter + nodeRadius);
                
                // Check for obstacles using a collision check
                bool walkable = !(Physics2D.OverlapCircle(worldPoint, nodeRadius, unwalkableMask));
                
                grid[x, y] = new Node(walkable, worldPoint, x, y);
            }
        }
    }

    // This is the new public method for updating the grid.
    public void UpdateGridObstacles()
    {
        if (grid == null)
        {
            return; // Don't run if grid hasn't been created yet.
        }

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                // We don't need to recalculate world position, just the walkable status.
                bool walkable = !(Physics2D.OverlapCircle(grid[x, y].worldPosition, nodeRadius, unwalkableMask));
                grid[x, y].isWalkable = walkable;
            }
        }
    }
    
    // The rest of your Grid.cs script (NodeFromWorldPoint, GetNeighbours, OnDrawGizmos) remains the same.
    // ...
    public Node NodeFromWorldPoint(Vector3 worldPosition)
    {
        float percentX = (worldPosition.x + gridWorldSize.x / 2) / gridWorldSize.x;
        float percentY = (worldPosition.y + gridWorldSize.y / 2) / gridWorldSize.y;
        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);

        int x = Mathf.RoundToInt((gridSizeX - 1) * percentX);
        int y = Mathf.RoundToInt((gridSizeY - 1) * percentY);
        return grid[x, y];
    }

    // Helper function to get the neighbors of a given node (for A*)
    public List<Node> GetNeighbours(Node node)
    {
        List<Node> neighbours = new List<Node>();

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                // Skip the node itself
                if (x == 0 && y == 0)
                    continue;

                int checkX = node.gridX + x;
                int checkY = node.gridY + y;

                // Check if the neighbor is within the grid boundaries
                if (checkX >= 0 && checkX < gridSizeX && checkY >= 0 && checkY < gridSizeY)
                {
                    neighbours.Add(grid[checkX, checkY]);
                }
            }
        }
        return neighbours;
    }
    
    public Node NodeFromGridPoint(int x, int y)
    {
        // Check if the coordinates are within the grid bounds
        if (x >= 0 && x < gridSizeX && y >= 0 && y < gridSizeY)
        {
            return grid[x, y];
        }
        return null; // Return null if out of bounds
    }

    // For debugging: Draw the grid in the Scene view
    void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(transform.position, new Vector3(gridWorldSize.x, gridWorldSize.y, 1));

        if (grid != null)
        {
            foreach (Node n in grid)
            {
                // Set the color based on whether the node is walkable or an obstacle
                Gizmos.color = (n.isWalkable) ? Color.white : Color.red;
                
                // Visibility Visualization
                if (n.isVisible)
                {
                    Gizmos.color = Color.cyan;
                }

                Gizmos.DrawCube(n.worldPosition, Vector3.one * (nodeDiameter - .1f));
            }
        }
    }
}
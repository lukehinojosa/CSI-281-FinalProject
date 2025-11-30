using UnityEngine;
using System.Collections.Generic;

public class Grid : MonoBehaviour
{
    [Tooltip("Enable this for live editing of obstacles in the Scene view during Play mode. Can be slow.")]
    public bool allowRuntimeGridUpdates = false;

    [Tooltip("The layer that represents obstacles. Use 3D colliders (e.g., Box Collider) for objects on this layer.")]
    public LayerMask unwalkableMask;
    public Vector2 gridWorldSize;
    public float nodeRadius;

    public List<Node> path; // For path visualization

    private Node[,] grid;
    private float nodeDiameter;
    private int gridSizeX, gridSizeY;
    
    public List<Node> debugPath;
    public Color debugPathColor = Color.green;
    private LineRenderer lineRenderer;
    
    public Node[,] GetGridNodes()
    {
        return grid;
    }

    void Awake()
    {
        nodeDiameter = nodeRadius * 2;
        gridSizeX = Mathf.RoundToInt(gridWorldSize.x / nodeDiameter);
        gridSizeY = Mathf.RoundToInt(gridWorldSize.y / nodeDiameter);
        CreateGrid();
    }
    
    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 0; // Hide by default
        
        // Draw line on top of the floor
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
    }

    void Update()
    {
        if (allowRuntimeGridUpdates)
        {
            UpdateGridObstacles();
        }
        
        DrawPath();
    }

    void CreateGrid()
    {
        grid = new Node[gridSizeX, gridSizeY];
        Vector3 worldBottomLeft = transform.position - Vector3.right * gridWorldSize.x / 2 - Vector3.forward * gridWorldSize.y / 2;

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                // Calculate the world point on the XZ plane.
                Vector3 worldPoint = worldBottomLeft + Vector3.right * (x * nodeDiameter + nodeRadius) + Vector3.forward * (y * nodeDiameter + nodeRadius);
                
                // Physics Check
                bool walkable = !(Physics.CheckSphere(worldPoint, nodeRadius, unwalkableMask));
                
                grid[x, y] = new Node(walkable, worldPoint, x, y);
            }
        }
    }

    public void UpdateGridObstacles()
    {
        if (grid == null) return;

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                // Recheck the walkable status using the physics check.
                bool walkable = !(Physics.CheckSphere(grid[x, y].worldPosition, nodeRadius, unwalkableMask));
                grid[x, y].isWalkable = walkable;
            }
        }
    }

    public Node NodeFromWorldPoint(Vector3 worldPosition)
    {
        // Convert the world position's X and Z coordinates to grid percentages.
        float percentX = (worldPosition.x - (transform.position.x - gridWorldSize.x / 2)) / gridWorldSize.x;
        float percentZ = (worldPosition.z - (transform.position.z - gridWorldSize.y / 2)) / gridWorldSize.y;
        percentX = Mathf.Clamp01(percentX);
        percentZ = Mathf.Clamp01(percentZ);

        int x = Mathf.RoundToInt((gridSizeX - 1) * percentX);
        int y = Mathf.RoundToInt((gridSizeY - 1) * percentZ);
        return grid[x, y];
    }

    public Node NodeFromGridPoint(int x, int y)
    {
        if (x >= 0 && x < gridSizeX && y >= 0 && y < gridSizeY)
        {
            return grid[x, y];
        }
        return null;
    }

    public List<Node> GetNeighbours(Node node)
    {
        List<Node> neighbours = new List<Node>();

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;

                int checkX = node.gridX + x;
                int checkY = node.gridY + y;

                if (checkX >= 0 && checkX < gridSizeX && checkY >= 0 && checkY < gridSizeY)
                {
                    // Diagonal check now ensures clear paths in 3D space
                    if (Mathf.Abs(x) == 1 && Mathf.Abs(y) == 1)
                    {
                        if (!grid[checkX, node.gridY].isWalkable || !grid[node.gridX, checkY].isWalkable)
                        {
                            continue;
                        }
                    }
                    neighbours.Add(grid[checkX, checkY]);
                }
            }
        }
        return neighbours;
    }
    
    void DrawPath()
    {
        if (debugPath != null && debugPath.Count > 0)
        {
            lineRenderer.enabled = true;
            lineRenderer.positionCount = debugPath.Count;
            
            // Set Color
            lineRenderer.startColor = debugPathColor;
            lineRenderer.endColor = debugPathColor;

            for (int i = 0; i < debugPath.Count; i++)
            {
                // Lift the line slightly
                Vector3 pos = debugPath[i].worldPosition;
                pos.y += 0.5f; 
                lineRenderer.SetPosition(i, pos);
            }
        }
        else
        {
            lineRenderer.enabled = false;
        }
    }

    void OnDrawGizmos()
    {
        // Draw the grid wireframe on the XZ plane.
        Gizmos.DrawWireCube(transform.position, new Vector3(gridWorldSize.x, 1, gridWorldSize.y));

        if (grid != null)
        {
            foreach (Node n in grid)
            {
                // Set the color based on whether the node is walkable or an obstacle
                Gizmos.color = (n.isWalkable) ? Color.white : Color.red;

                if (n.isVisible) Gizmos.color = Color.cyan;
                
                if (path != null && path.Contains(n)) Gizmos.color = Color.green;

                Gizmos.DrawCube(n.worldPosition, new Vector3(nodeDiameter - .1f, 0.1f, nodeDiameter - .1f));
            }
        }
    }
}
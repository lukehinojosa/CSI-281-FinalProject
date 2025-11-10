using UnityEngine;
using System.Collections.Generic;

public class FieldOfView : MonoBehaviour
{
    public int viewRadius;

    private Grid grid;
    public HashSet<Node> visibleNodes = new HashSet<Node>();

    // Represents the start and end slopes of a shadow.
    private class Shadow
    {
        public float start;
        public float end;

        public Shadow(float start, float end)
        {
            this.start = start;
            this.end = end;
        }

        // Checks if a slope is contained within this shadow.
        public bool Contains(float slope)
        {
            return start <= slope && slope <= end;
        }
    }

    void Start()
    {
        grid = FindObjectOfType<Grid>();
    }

    void Update()
    {
        CalculateFieldOfView();
    }

    public void CalculateFieldOfView()
    {
        // Reset previous FOV
        foreach (Node node in visibleNodes)
        {
            if (node != null) node.isVisible = false;
        }
        visibleNodes.Clear();

        Node originNode = grid.NodeFromWorldPoint(transform.position);
        if (originNode == null) return;

        // The origin is always visible.
        originNode.isVisible = true;
        visibleNodes.Add(originNode);

        // Scan all 8 octants.
        for (int i = 0; i < 8; i++)
        {
            ScanOctant(originNode, i);
        }
    }

    private void ScanOctant(Node origin, int octant)
    {
        var shadows = new List<Shadow>();
        
        // Iterate through each row, moving outwards from the origin.
        for (int row = 1; row < viewRadius; row++)
        {
            // Keep track of the last obstacle to correctly handle shadow casting.
            Node lastObstacle = null;

            // Iterate through each column in the current row.
            for (int col = 0; col <= row; col++)
            {
                // Get the grid coordinates for the current tile based on the octant.
                Vector2Int gridPos = TransformOctant(origin.gridX, origin.gridY, row, col, octant);
                Node currentNode = grid.NodeFromGridPoint(gridPos.x, gridPos.y);

                // If the node is outside the grid, skip it.
                if (currentNode == null) continue;

                // Check distance instead of just row to get a circular FOV.
                if (Vector3.Distance(origin.worldPosition, currentNode.worldPosition) > viewRadius) continue;
                
                // Calculate the slopes to the center of the current tile.
                float currentSlope = (float)col / (float)row;
                
                bool isVisible = true;
                // Check if the current tile is within any existing shadow.
                foreach (var shadow in shadows)
                {
                    if (shadow.Contains(currentSlope))
                    {
                        isVisible = false;
                        break;
                    }
                }

                if (isVisible)
                {
                    // If the tile is visible, mark it and add it to the set.
                    visibleNodes.Add(currentNode);
                    currentNode.isVisible = true;

                    // If this tile is an obstacle, it will cast a shadow in the next row.
                    if (!currentNode.isWalkable)
                    {
                        // If this is the first obstacle in a chain, start a new shadow.
                        if (lastObstacle == null)
                        {
                            shadows.Add(new Shadow(GetSlope(row, col, false), GetSlope(row, col, true)));
                        }
                        // If the adjacent tile was also an obstacle, extend the last shadow.
                        else
                        {
                            shadows[shadows.Count - 1].end = GetSlope(row, col, true);
                        }
                        lastObstacle = currentNode;
                    }
                    else
                    {
                        // This tile is walkable, so it's not part of the current shadow chain.
                        lastObstacle = null;
                    }
                }
            }
        }
    }

    // Helper to calculate the slope to the near (isStart=false) or far (isStart=true) edge of the tile.
    private float GetSlope(int row, int col, bool isEnd)
    {
        if (isEnd)
            return (float)(col + 0.5f) / (float)(row - 0.5f);
        else
            return (float)(col - 0.5f) / (float)(row + 0.5f);
    }
    
    // Helper to transform octant coordinates to grid coordinates.
    private Vector2Int TransformOctant(int originX, int originY, int row, int col, int octant)
    {
        switch (octant)
        {
            case 0: return new Vector2Int(originX + col, originY + row);
            case 1: return new Vector2Int(originX + row, originY + col);
            case 2: return new Vector2Int(originX - row, originY + col);
            case 3: return new Vector2Int(originX - col, originY + row);
            case 4: return new Vector2Int(originX - col, originY - row);
            case 5: return new Vector2Int(originX - row, originY - col);
            case 6: return new Vector2Int(originX + row, originY - col);
            case 7: return new Vector2Int(originX + col, originY - row);
            default: return new Vector2Int(originX, originY);
        }
    }
}
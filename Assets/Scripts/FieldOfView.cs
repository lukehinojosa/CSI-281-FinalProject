using UnityEngine;
using System.Collections.Generic;

public class FieldOfView : MonoBehaviour
{
    public int viewRadius;

    private Grid grid;
    public HashSet<Node> visibleNodes = new HashSet<Node>();

    // A simple struct to represent the start and end slopes of a shadow.
    private class Shadow
    {
        public float start;
        public float end;

        public Shadow(float start, float end)
        {
            this.start = start;
            this.end = end;
        }

        // Checks if another shadow overlaps with this one.
        public bool Overlaps(Shadow other)
        {
            return start <= other.end && end >= other.start;
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
        // Reset the visibility of all previously visible nodes.
        foreach (Node node in visibleNodes)
        {
            node.isVisible = false;
        }
        visibleNodes.Clear();

        Node originNode = grid.NodeFromWorldPoint(transform.position);
        originNode.isVisible = true;
        visibleNodes.Add(originNode);

        // Scan all 8 octants from the origin.
        for (int i = 0; i < 8; i++)
        {
            ScanOctant(originNode, i);
        }
    }

    private void ScanOctant(Node origin, int octant)
    {
        var shadows = new List<Shadow>();
        
        // The main loop iterates outwards from the origin.
        for (int row = 1; row < viewRadius; row++)
        {
            // The column loop iterates across the scanline.
            for (int col = 0; col <= row; col++)
            {
                // Get the grid coordinates for the current tile based on the octant.
                Vector2Int gridPos = TransformOctant(origin.gridX, origin.gridY, row, col, octant);
                Node currentNode = grid.NodeFromGridPoint(gridPos.x, gridPos.y);

                // If the node is outside the grid, skip it.
                if (currentNode == null) continue;

                // Calculate the start and end slopes for the current tile.
                float tileStartSlope = (float)col / (row + 1);
                float tileEndSlope = (float)(col + 1) / row;

                bool isVisible = true;
                // Check if the tile is shadowed by any existing shadows.
                foreach (var shadow in shadows)
                {
                    if (tileStartSlope >= shadow.start && tileEndSlope <= shadow.end)
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

                    // If this visible tile is an obstacle, it casts a new shadow.
                    if (!currentNode.isWalkable)
                    {
                        // Add the new shadow to the list.
                        shadows.Add(new Shadow(tileStartSlope, tileEndSlope));
                    }
                }
            }
        }
    }

    // Helper function to transform row/col coordinates into grid x/y based on the octant.
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
using UnityEngine;
using System;
using System.Collections.Generic;

public static class ShadowCaster
{
    struct Shadow
    {
        public float start, end;
        public Shadow(float start, float end) { this.start = start; this.end = end; }
        public bool Contains(float slope) { return start <= slope && slope <= end; }
    }
    
    public static void ComputeVisibility(int gridWidth, int gridHeight, Vector2Int origin, int radius, Func<int, int, bool> IsBlocking, Action<int, int> SetVisible)
    {
        // Origin is always visible
        SetVisible(origin.x, origin.y);

        for (int i = 0; i < 8; i++)
        {
            ScanOctant(gridWidth, gridHeight, origin, radius, i, IsBlocking, SetVisible);
        }
    }

    private static void ScanOctant(int gridWidth, int gridHeight, Vector2Int origin, int radius, int octant, Func<int, int, bool> IsBlocking, Action<int, int> SetVisible)
    {
        List<Shadow> shadows = new List<Shadow>();
        
        // Iterate rows
        for (int row = 1; row < radius; row++)
        {
            Vector2Int pos = TransformOctant(origin, row, 0, octant);
            
            Node lastObstacle = null; // Used to merge shadows

            for (int col = 0; col <= row; col++)
            {
                pos = TransformOctant(origin, row, col, octant);

                // Bounds Check
                if (pos.x < 0 || pos.x >= gridWidth || pos.y < 0 || pos.y >= gridHeight) continue;

                // Circular Radius Check
                int dx = pos.x - origin.x;
                int dy = pos.y - origin.y;
                if ((dx*dx + dy*dy) > radius * radius) continue;

                // Slope Calculation
                float currentSlope = (float)col / (float)row;

                // Visibility Check
                bool isVisible = true;
                foreach (var s in shadows)
                {
                    if (s.Contains(currentSlope))
                    {
                        isVisible = false;
                        break;
                    }
                }

                if (isVisible)
                {
                    SetVisible(pos.x, pos.y);

                    // Obstacle Handling
                    if (IsBlocking(pos.x, pos.y))
                    {
                        if (lastObstacle != null)
                        {
                            // Extend existing shadow
                            shadows[shadows.Count - 1] = new Shadow(shadows[shadows.Count - 1].start, GetSlope(row, col, true));
                        }
                        else
                        {
                            // Start new shadow
                            shadows.Add(new Shadow(GetSlope(row, col, false), GetSlope(row, col, true)));
                        }
                        // Mark that we are currently processing an obstacle
                        lastObstacle = new Node(false, Vector3.zero, 0,0); // Dummy node logic
                    }
                    else
                    {
                        lastObstacle = null;
                    }
                }
            }
        }
    }

    private static float GetSlope(int row, int col, bool isEnd)
    {
        if (isEnd) return (float)(col + 0.5f) / (float)(row - 0.5f);
        else return (float)(col - 0.5f) / (float)(row + 0.5f);
    }

    private static Vector2Int TransformOctant(Vector2Int origin, int row, int col, int octant)
    {
        switch (octant)
        {
            case 0: return new Vector2Int(origin.x + col, origin.y + row);
            case 1: return new Vector2Int(origin.x + row, origin.y + col);
            case 2: return new Vector2Int(origin.x - row, origin.y + col);
            case 3: return new Vector2Int(origin.x - col, origin.y + row);
            case 4: return new Vector2Int(origin.x - col, origin.y - row);
            case 5: return new Vector2Int(origin.x - row, origin.y - col);
            case 6: return new Vector2Int(origin.x + row, origin.y - col);
            case 7: return new Vector2Int(origin.x + col, origin.y - row);
            default: return origin;
        }
    }
    
    // Internal dummy class just for the 'lastObstacle' check logic in the loop
    class Node { public Node(bool w, Vector3 p, int x, int y){} }
}
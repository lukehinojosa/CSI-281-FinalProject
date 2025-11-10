using UnityEngine;

// No MonoBehaviour here! This is a plain C# object.
public class Node
{
    public int gridX;
    public int gridY;

    public bool isWalkable;
    public Vector3 worldPosition;

    // A* Pathfinding variables
    public int gCost;
    public int hCost;
    public Node parent;

    // Field of View variable
    public bool isVisible;

    public Node(bool _isWalkable, Vector3 _worldPos, int _gridX, int _gridY)
    {
        isWalkable = _isWalkable;
        worldPosition = _worldPos;
        gridX = _gridX;
        gridY = _gridY;

        // Initialize with default values
        parent = null;
        gCost = 0;
        hCost = 0;
        isVisible = false;
    }

    // Calculated property for A*
    public int fCost
    {
        get { return gCost + hCost; }
    }
}
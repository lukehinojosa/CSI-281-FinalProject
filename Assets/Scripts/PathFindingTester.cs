using UnityEngine;

public class PathfindingTester : MonoBehaviour
{
    public Transform seeker;
    public Transform target;

    private Pathfinding pathfinder;
    private Grid grid;

    void Start()
    {
        pathfinder = FindObjectOfType<Pathfinding>();
        grid = FindObjectOfType<Grid>();
    }

    void Update()
    {
        if (seeker != null && target != null && pathfinder != null)
        {
            // Find the path
            var foundPath = pathfinder.FindPath(seeker.position, target.position);
            
            // If a path was found, assign it to the grid for visualization.
            if (foundPath != null)
            {
                grid.path = foundPath;
            }
        }
    }
}
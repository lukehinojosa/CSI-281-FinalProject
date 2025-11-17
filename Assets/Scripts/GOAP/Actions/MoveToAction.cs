using UnityEngine;
using System.Collections.Generic;

public class MoveToAction : GOAPAction
{
    private Pathfinding pathfinder;
    private List<Node> path;
    private int pathIndex = 0;
    private float moveSpeed = 5f;
    
    private Vector3 targetPosition;

    void Awake()
    {
        pathfinder = FindObjectOfType<Pathfinding>();
        AddPrecondition("hasDestination", true);
        AddEffect("isAtDestination", true);
    }

    public override void Reset()
    {
        path = null;
        pathIndex = 0;
        target = null;
        targetPosition = Vector3.zero;
        isDone = false;
    }

    public override bool RequiresInRange() { return true; }

    public override void OnPlanAborted()
    {
        if (target != null) Destroy(target);
    }

    public override bool CheckProceduralPrecondition(GameObject agent)
    {
        // Find and store the position, but DO NOT create a GameObject.
        GOAPAgent goapAgent = agent.GetComponent<GOAPAgent>();
        if (goapAgent.lastKnownPosition != Vector3.zero)
        {
            targetPosition = goapAgent.lastKnownPosition;
            return true;
        }
        return false;
    }

    public override bool Perform(GameObject agent)
    {
        // Create the target GameObject only on the first run of Perform.
        if (target == null)
        {
            target = new GameObject("DynamicMoveTarget");
            target.transform.position = targetPosition;
        }

        if (path == null)
        {
            // Calculate the path for the first time.
            path = pathfinder.FindPath(agent.transform.position, target.transform.position);
            if (path == null)
            {
                return false; // No path found, action fails.
            }
        }

        // Move along the path.
        if (pathIndex < path.Count)
        {
            Vector3 worldTargetPos = path[pathIndex].worldPosition;
            agent.transform.position = Vector3.MoveTowards(agent.transform.position, worldTargetPos, moveSpeed * Time.deltaTime);

            if (Vector3.Distance(agent.transform.position, worldTargetPos) < 0.1f)
            {
                pathIndex++;
            }
        }
        else
        {
            // Reached the end of the path.
            isDone = true;
            if (target != null) Destroy(target);
            return true;
        }

        return true; // Action is still in progress.
    }
}
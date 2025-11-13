using UnityEngine;
using System.Collections.Generic;

public class MoveToAction : GOAPAction
{
    private Pathfinding pathfinder;
    private List<Node> path;
    private int pathIndex = 0;
    private float moveSpeed = 5f;

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
        isDone = false;
    }

    public override bool RequiresInRange()
    {
        return true;
    }

    public override bool CheckProceduralPrecondition(GameObject agent)
    {
        // Needs a target to move to.
        GOAPAgent goapAgent = agent.GetComponent<GOAPAgent>();
        
        Debug.Log("Checking Precondition... lastKnownPosition is: " + goapAgent.lastKnownPosition);
        
        if (goapAgent.lastKnownPosition != Vector3.zero)
        {
            target = new GameObject("DynamicTarget");
            target.transform.position = goapAgent.lastKnownPosition;
            return true;
        }
        return false;
    }

    public override bool Perform(GameObject agent)
    {
        if (path == null)
        {
            // Calculate the path for the first time.
            path = pathfinder.FindPath(agent.transform.position, target.transform.position);
            if (path == null)
            {
                // No path found, action fails.
                return false;
            }
        }

        // Move along the path.
        if (pathIndex < path.Count)
        {
            Vector3 targetPosition = path[pathIndex].worldPosition;
            agent.transform.position = Vector3.MoveTowards(agent.transform.position, targetPosition, moveSpeed * Time.deltaTime);

            if (Vector3.Distance(agent.transform.position, targetPosition) < 0.1f)
            {
                pathIndex++;
            }
        }
        else
        {
            // Reached the end of the path.
            isDone = true;
            Destroy(target); // Clean up the temporary target
            return true;
        }

        return true; // Action is still in progress.
    }
    
    public override void OnPlanAborted()
    {
        // This is called when a plan is interrupted. Clean up the dynamic target.
        if (target != null)
        {
            Destroy(target);
        }
    }
}
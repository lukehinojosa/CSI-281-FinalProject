using UnityEngine;

public class RoamAction : GOAPAction
{
    [Tooltip("How far the agent will look for a random roam point.")]
    public float roamRadius = 15.0f;

    private Pathfinding pathfinder;
    private Grid grid;
    private System.Collections.Generic.List<Node> path;
    private int pathIndex = 0;
    private float moveSpeed = 3f;
    
    private Vector3 targetPosition;

    void Awake()
    {
        pathfinder = FindObjectOfType<Pathfinding>();
        grid = FindObjectOfType<Grid>();

        // This action is only possible if there is nothing better to do.
        AddPrecondition("hasDestination", false);
        AddPrecondition("isLowOnEnergy", false);

        // Satisfied the need to roam
        AddEffect("hasRoamed", true);
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
        // Find and store the position, without create a GameObject.
        Vector3 randomDirection = Random.insideUnitCircle * roamRadius;
        Vector3 roamPoint = agent.transform.position + randomDirection;
        Node roamNode = grid.NodeFromWorldPoint(roamPoint);

        if (roamNode != null && roamNode.isWalkable)
        {
            targetPosition = roamNode.worldPosition;
            return true;
        }

        // Failed to find a valid roam point this time.
        return false;
    }

    public override bool Perform(GameObject agent)
    {
        // Create the target GameObject only on the first run of Perform.
        if (target == null)
        {
            target = new GameObject("DynamicRoamTarget");
            target.transform.position = targetPosition;
        }

        if (path == null)
        {
            path = pathfinder.FindPath(agent.transform.position, target.transform.position);
            if (path == null) return false;
        }

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
            // Arrived at the roam point.
            isDone = true;
            if (target != null) Destroy(target);
            return true;
        }
        return true;
    }
}
using UnityEngine;
using System.Linq;

public class RechargeAction : GOAPAction
{
    private Pathfinding pathfinder;
    private Grid grid;
    private System.Collections.Generic.List<Node> path;
    private int pathIndex = 0;
    private float moveSpeed = 5f;
    private GOAPAgent goapAgent;

    private Vector3 targetPosition;

    void Awake()
    {
        pathfinder = FindObjectOfType<Pathfinding>();
        grid = FindObjectOfType<Grid>();
        goapAgent = GetComponent<GOAPAgent>();

        // This action is only possible if the agent is low on energy.
        AddPrecondition("isLowOnEnergy", true);
        // The effect of this action is that the agent is no longer low on energy.
        AddEffect("isLowOnEnergy", false);
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
        if (goapAgent.energyStations == null || goapAgent.energyStations.Length == 0)
            return false;

        // Sort stations by distance
        var sortedStations = goapAgent.energyStations
            .OrderBy(station => Vector3.Distance(station.transform.position, agent.transform.position));

        foreach (var station in sortedStations)
        {
            // Get the node closest to the station
            Node stationNode = grid.NodeFromWorldPoint(station.transform.position);
            
            // Check for a walkable station
            if (stationNode.isWalkable)
            {
                targetPosition = station.transform.position;
                return true;
            }
            
            // Check for a walkable neighbor of the nearest station
            Node validNeighbor = GetWalkableNeighbor(stationNode);
            if (validNeighbor != null)
            {
                targetPosition = validNeighbor.worldPosition;
                return true;
            }
            
            // If completely unreachable, go to the next closest station
        }

        // No reachable stations found
        return false;
    }
    
    private Node GetWalkableNeighbor(Node node)
    {
        foreach (Node neighbor in grid.GetNeighbours(node))
        {
            if (neighbor.isWalkable) return neighbor;
        }
        return null;
    }

    public override bool Perform(GameObject agent)
    {
        // Create the target GameObject only on the first run of Perform.
        if (target == null)
        {
            target = new GameObject("DynamicRechargeTarget");
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
            goapAgent.MoveTowards(worldTargetPos, moveSpeed);

            if (Vector3.Distance(agent.transform.position, worldTargetPos) < 0.1f)
            {
                pathIndex++;
            }
        }
        else
        {
            // Arrived at the station.
            goapAgent.ReplenishEnergy();
            isDone = true;
            if (target != null) Destroy(target);
            return true;
        }

        return true; // Still in progress.
    }
}
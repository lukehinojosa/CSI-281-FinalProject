using UnityEngine;
using System.Linq;

public class RechargeAction : GOAPAction
{
    private Pathfinding pathfinder;
    private System.Collections.Generic.List<Node> path;
    private int pathIndex = 0;
    private float moveSpeed = 5f;

    // A reference to the agent's script to call the replenish method
    private GOAPAgent goapAgent;

    void Awake()
    {
        pathfinder = FindObjectOfType<Pathfinding>();
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
        isDone = false;
    }

    public override bool RequiresInRange()
    {
        return true; // We need to move to the station.
    }

    public override void OnPlanAborted()
    {
        if (target != null) Destroy(target);
    }

    public override bool CheckProceduralPrecondition(GameObject agent)
    {
        // Can this action run? Yes, if there are energy stations available.
        if (goapAgent.energyStations == null || goapAgent.energyStations.Length == 0)
            return false;

        // Find the closest energy station.
        GameObject closestStation = goapAgent.energyStations
            .OrderBy(station => Vector3.Distance(station.transform.position, agent.transform.position))
            .FirstOrDefault();

        if (closestStation == null)
            return false;

        // Set this closest station as our target for this action instance.
        target = new GameObject("DynamicRechargeTarget");
        target.transform.position = closestStation.transform.position;
        return true;
    }

    public override bool Perform(GameObject agent)
    {
        if (path == null)
        {
            path = pathfinder.FindPath(agent.transform.position, target.transform.position);
            if (path == null) return false; // No path to station, action fails.
        }

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
            // We've arrived at the station.
            goapAgent.ReplenishEnergy();
            isDone = true;
            if (target != null) Destroy(target);
            return true;
        }

        return true; // Still in progress.
    }
}
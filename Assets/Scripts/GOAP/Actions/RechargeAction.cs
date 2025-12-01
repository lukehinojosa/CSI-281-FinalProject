using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class RechargeAction : GOAPAction
{
    private Pathfinding pathfinder;
    private Grid grid;
    private GOAPAgent goapAgent;

    // Cache the successful path and target found during the check
    private List<Node> cachedPath;
    private Vector3 targetPosition;
    private GameObject targetStation; 
    
    private int pathIndex = 0;
    private float moveSpeed = 5f;
    
    public override List<Node> GetPath()
    {
        return cachedPath;
    }

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
        pathIndex = 0;
        target = null;
        targetPosition = Vector3.zero;
        cachedPath = null;
        targetStation = null;
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
        
        // Null Filtering
        List<GameObject> validStations = new List<GameObject>();
        
        foreach (var s in goapAgent.energyStations)
        {
            if (s != null) 
            {
                validStations.Add(s);
            }
        }
        
        if (validStations.Count == 0) 
            return false; // No stations left in the world

        // Sort stations by distance to find the best candidate first
        Vector3 agentPos = agent.transform.position;
        validStations.Sort((a, b) => {
            if (a == null) return 1;
            if (b == null) return -1;
            float distA = (a.transform.position - agentPos).sqrMagnitude;
            float distB = (b.transform.position - agentPos).sqrMagnitude;
            return distA.CompareTo(distB);
        });

        // Iterate through stations until a reachable one is found
        foreach (var station in validStations)
        {
            if (station == null) continue;
            
            Node stationNode = grid.NodeFromWorldPoint(station.transform.position);
            Vector3 potentialTarget = Vector3.zero;
            bool foundValidNode = false;

            // Check if the station's node is valid
            if (stationNode.isWalkable)
            {
                potentialTarget = station.transform.position;
                foundValidNode = true;
            }
            // If not, check if it has a valid neighbor
            else
            {
                Node validNeighbor = GetWalkableNeighbor(stationNode);
                if (validNeighbor != null)
                {
                    potentialTarget = validNeighbor.worldPosition;
                    foundValidNode = true;
                }
            }

            // If valid endpoint found, verify a path exists
            if (foundValidNode)
            {
                // Run A* now. If it returns a path, this station walkable
                List<Node> testPath = pathfinder.FindPath(agent.transform.position, potentialTarget);
                
                if (testPath != null && testPath.Count > 0)
                {
                    // Success, cache the data so Perform doesn't need to recalculate.
                    cachedPath = testPath;
                    targetPosition = potentialTarget;
                    targetStation = station; 
                    return true; 
                }
                // If path is null, this station is walled off. Loop to the next station.
            }
        }

        // No reachable stations found, action fails
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
        // If the player destroyed it, targetStation becomes null
        if (targetStation == null)
        {
            // Find a new station
            return false; 
        }
        
        if (target == null)
        {
            target = new GameObject("DynamicRechargeTarget");
            target.transform.position = targetPosition;
        }

        // Use the path already calculated in CheckProceduralPrecondition
        if (cachedPath == null)
        {
            // Fallback
            cachedPath = pathfinder.FindPath(agent.transform.position, target.transform.position);
            if (cachedPath == null) return false;
        }

        if (pathIndex < cachedPath.Count)
        {
            Vector3 worldTargetPos = cachedPath[pathIndex].worldPosition;
            goapAgent.MoveTowards(worldTargetPos, moveSpeed);

            if (Vector3.Distance(agent.transform.position, worldTargetPos) < 0.1f)
            {
                pathIndex++;
            }
        }
        else
        {
            goapAgent.ReplenishEnergy();
            isDone = true;
            if (target != null) Destroy(target);
            return true;
        }

        return true; 
    }
}
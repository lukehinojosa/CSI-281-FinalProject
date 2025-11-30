using UnityEngine;
using System.Collections.Generic;

public class GOAPAgent : MonoBehaviour, IGoap
{
    public Transform target;
    [Tooltip("How far the target must move from its last known position to trigger a replan.")]
    public float replanThreshold = 2.0f;

    public FSM stateMachine;

    public HashSet<GOAPAction> availableActions;
    public Queue<GOAPAction> currentActions;
    public HashSet<KeyValuePair<string, object>> currentGoal;

    public Vector3 lastKnownPosition;

    private GOAPPlanner planner;
    private FieldOfView fov;
    private Pathfinding pathfinder;
    
    [Header("Agent Vitals")]
    public float currentEnergy = 100f;
    public float maxEnergy = 100f;
    public float lowEnergyThreshold = 25f;
    public float energyDepletionRate = 2f; // energy depleted per second
    
    [HideInInspector]
    public GameObject[] energyStations;
    
    private float turnSmoothVelocity;
    [Tooltip("How fast the enemy smooths its rotation.")]
    public float turnSmoothTime = 0.1f; 
    
    [Header("Debug")]
    public string debugStateName = "None"; // For the Visualizer
    
    private Renderer agentRenderer;
    private Color colorPursue = Color.red;
    private Color colorRecharge = new Color(0.6f, 0f, 1f);
    
    [Header("Performance")]
    public float repathRate = 0.25f; // Calculate path 4 times per second
    private float nextRepathTime = 0;

    void Awake()
    {
        availableActions = new HashSet<GOAPAction>();
        currentActions = new Queue<GOAPAction>();
        planner = new GOAPPlanner();
        fov = GetComponent<FieldOfView>();
        pathfinder = FindObjectOfType<Pathfinding>();
        
        foreach (var action in GetComponents<GOAPAction>())
        {
            availableActions.Add(action);
        }
        
        agentRenderer = GetComponentInChildren<Renderer>();

        stateMachine = new FSM();
        stateMachine.PushState(IdleState); // Start in the Idle state
        
        energyStations = GameObject.FindGameObjectsWithTag("EnergyStation");
    }

    void Update()
    {
        currentEnergy -= energyDepletionRate * Time.deltaTime;
        if (currentEnergy <= 0)
        {
            Debug.Log("<color=red>Agent ran out of energy and died!</color>");
            Destroy(gameObject);
            return;
        }
        
        stateMachine.Update(gameObject);
        
        UpdateColor();
    }

    private void IdleState(FSM fsm, object data)
    {
        debugStateName = "IDLE (Scanning)";
        
        // First Priority: React to seeing the player
        if (IsTargetVisible())
        {
            // Target is visible, update its position and start planning.
            lastKnownPosition = target.position;
            fsm.ChangeState(PlanState);
        }
        // Second Priority: React to low energy
        else if (currentEnergy < lowEnergyThreshold)
        {
            // Even if the player isn't seen, create survival plan
            fsm.ChangeState(PlanState);
        }
        // Third Priority: Investigate stale location
        else if (lastKnownPosition != Vector3.zero)
        {
            // This only runs if energy is fine and the player isn't visible
            fsm.ChangeState(PlanState);
        }
        // Lowest Priority: If there is nothing else to do, find something to do.
        else
        {
            fsm.ChangeState(PlanState);
        }
    }

    private void PlanState(FSM fsm, object data)
    {
        debugStateName = "PLANNING (Calculating)";
        
        // Create a plan to get to the last known position.
        var worldState = GetWorldState();
        var goal = CreateGoalState();

        Queue<GOAPAction> plan = planner.Plan((GameObject)data, availableActions, worldState, goal);

        if (plan != null)
        {
            currentActions = plan;
            currentGoal = goal;
            PlanFound(goal, plan);
            fsm.ChangeState(MoveState);
        }
        else
        {
            PlanFailed(goal);
            lastKnownPosition = Vector3.zero; // Give up on this destination
            fsm.ChangeState(IdleState);
        }
    }

    private void MoveState(FSM fsm, object data)
    {
        debugStateName = "MOVING (Executing Plan)";
        
        if (currentActions.Count == 0)
        {
            ActionsFinished();
            fsm.ChangeState(IdleState);
            return;
        }

        GOAPAction action = currentActions.Peek();
        if (action.isDone)
        {
            currentActions.Dequeue();
            return;
        }
        
        // Situational awareness
        if (IsTargetVisible())
        {
            lastKnownPosition = target.position;
        }
        
        // Priority 1: Survival
        bool isLowOnEnergy = currentEnergy < lowEnergyThreshold;
        bool isCurrentlyRecharging = currentGoal.Contains(new KeyValuePair<string, object>("isLowOnEnergy", false));
        
        if (isLowOnEnergy && !isCurrentlyRecharging)
        {
            Debug.Log("<color=red>ENERGY CRITICAL! Interrupting current action to find a station.</color>");
            action.OnPlanAborted();
            currentActions.Clear();
            fsm.ChangeState(PlanState); // Force a replan with survival as the goal
            return;
        }
        
        // Priority 2: Chase player
        // This will only run if energy is not under threshold
        if (IsTargetVisible())
        {
            bool isCurrentlyChasing = currentGoal.Contains(new KeyValuePair<string, object>("isAtDestination", true));

            // Not currently recharging and not currently chasing.
            if (!isCurrentlyRecharging && !isCurrentlyChasing)
            {
                Debug.Log("<color=magenta>Player spotted! Interrupting current action to chase.</color>");
                action.OnPlanAborted();
                currentActions.Clear();
                lastKnownPosition = target.position;
                fsm.ChangeState(PlanState);
                return;
            }
            // Already chasing. Path update check.
            else if (isCurrentlyChasing && action.target != null)
            {
                // Check distance
                Vector3 currentWaypointAim = action.target.transform.position;
                if (Vector3.Distance(target.position, currentWaypointAim) > replanThreshold)
                {
                    // Only update if enough time has passed since the last calculation
                    if (Time.time >= nextRepathTime)
                    {
                        nextRepathTime = Time.time + repathRate;

                        if (action is MoveToAction moveAction)
                        {
                            // Debug.Log("Updating chase path");
                            moveAction.UpdatePath(lastKnownPosition);
                        }
                        else
                        {
                            action.OnPlanAborted();
                            currentActions.Clear();
                            fsm.ChangeState(PlanState);
                            return;
                        }
                    }
                    // If timer is not ready, keep moving towards the old point.
                }
            }
        }

        // Continue with the current plan
        if (!action.Perform((GameObject)data))
        {
            // If trying to go to a destination, forget it.
            if (currentGoal != null && currentGoal.Contains(new KeyValuePair<string, object>("isAtDestination", true)))
            {
                Debug.Log("<color=red>Move Action Failed (Unreachable?). Clearing memory.</color>");
                lastKnownPosition = Vector3.zero;
            }
            
            action.OnPlanAborted();
            fsm.ChangeState(IdleState);
            PlanAborted(action);
        }
    }

    public HashSet<KeyValuePair<string, object>> GetWorldState()
    {
        HashSet<KeyValuePair<string, object>> worldData = new HashSet<KeyValuePair<string, object>>();
        worldData.Add(new KeyValuePair<string, object>("hasDestination", lastKnownPosition != Vector3.zero));
        worldData.Add(new KeyValuePair<string, object>("isLowOnEnergy", currentEnergy < lowEnergyThreshold));
        return worldData;
    }

    public HashSet<KeyValuePair<string, object>> CreateGoalState()
    {
        HashSet<KeyValuePair<string, object>> goal = new HashSet<KeyValuePair<string, object>>();

        if (currentEnergy < lowEnergyThreshold)
        {
            // Priority 1: Survive
            // The goal is to no longer be in a low energy state.
            goal.Add(new KeyValuePair<string, object>("isLowOnEnergy", false));
        }
        else if (lastKnownPosition != Vector3.zero)
        {
            // Priority 2: Chase Player
            // If energy is fine, the goal is to investigate the player's last known position.
            goal.Add(new KeyValuePair<string, object>("isAtDestination", true));
        }
        else
        {
            // PRIORITY 3: Roam
            goal.Add(new KeyValuePair<string, object>("hasRoamed", true));
        }

        return goal;
    }

    public void PlanFailed(HashSet<KeyValuePair<string, object>> failedGoal)
    {
        Debug.Log("<color=red>Plan Failed</color>");
    }

    public void PlanFound(HashSet<KeyValuePair<string, object>> goal, Queue<GOAPAction> actions)
    {
        Debug.Log("<color=green>Plan Found!</color> " + actions.Count + " actions.");
    }

    public void ActionsFinished()
    {
        Debug.Log("Plan Finished.");

        // Only reset the lastKnownPosition if the goal of the completed plan was to get to that destination.
        if (currentGoal != null && currentGoal.Contains(new KeyValuePair<string, object>("isAtDestination", true)))
        {
            Debug.Log("Investigation complete. Clearing lastKnownPosition.");
            lastKnownPosition = Vector3.zero;
        }
    }

    public void PlanAborted(GOAPAction aborter)
    {
        Debug.Log("<color=orange>Plan Aborted by: " + aborter.GetType().Name + "</color>");
    }
    
    private bool IsTargetVisible()
    {
        if (fov.visibleNodes.Count <= 1) // <= 1 because the agent's own node is always visible
            return false;

        foreach (var node in fov.visibleNodes)
        {
            // Check if the target's position is very close to a visible node's position.
            if (Vector3.Distance(node.worldPosition, target.position) < 1.0f)
            {
                return true;
            }
        }
        return false;
    }
    
    public void ReplenishEnergy()
    {
        Debug.Log("<color=cyan>Energy Replenished!</color>");
        currentEnergy = maxEnergy;
    }
    
    public void MoveTowards(Vector3 targetPosition, float speed)
    {
        // Calculate Direction
        Vector3 direction = (targetPosition - transform.position).normalized;
        // Flatten Y so it only rotates on the Y axis
        direction.y = 0;

        // Rotation
        if (direction != Vector3.zero && direction.sqrMagnitude > 0.001f)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }

        // Update Position
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
    }
    
    private void UpdateColor()
    {
        if (agentRenderer == null) return;

        Color targetColor = colorPursue; // Default to Red (Idle/Roam/Chase)

        // Check if currently performing an action
        if (currentActions != null && currentActions.Count > 0)
        {
            GOAPAction currentAction = currentActions.Peek();
            
            // If the current action is Recharge, switch to Purple
            if (currentAction is RechargeAction)
            {
                targetColor = colorRecharge;
            }
        }

        // Apply the color
        if (agentRenderer.material.color != targetColor)
        {
            agentRenderer.material.color = targetColor;
        }
    }
}
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

    public Vector3 lastKnownPosition;

    private GOAPPlanner planner;
    private FieldOfView fov;
    private Pathfinding pathfinder;

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

        stateMachine = new FSM();
        stateMachine.PushState(IdleState); // Start in the Idle state
    }

    void Update()
    {
        stateMachine.Update(gameObject);
    }

    private void IdleState(FSM fsm, object data)
    {
        if (IsTargetVisible())
        {
            // Target is visible, update its position and start planning.
            lastKnownPosition = target.position;
            fsm.PushState(PlanState);
        }
        else if (lastKnownPosition != Vector3.zero)
        {
            // Investigate last known position
            fsm.PushState(PlanState);
        }
    }

    private void PlanState(FSM fsm, object data)
    {
        // Create a plan to get to the last known position.
        var worldState = GetWorldState();
        var goal = CreateGoalState();

        Queue<GOAPAction> plan = planner.Plan((GameObject)data, availableActions, worldState, goal);

        if (plan != null)
        {
            currentActions = plan;
            PlanFound(goal, plan);
            fsm.PopState(); // Pop PlanState
            fsm.PushState(MoveState); // Go to MoveState
        }
        else
        {
            PlanFailed(goal);
            lastKnownPosition = Vector3.zero; // Give up on this destination
            fsm.PopState(); // Pop PlanState
            fsm.PushState(IdleState); // Go back to Idle
        }
    }

    private void MoveState(FSM fsm, object data)
    {
        // If the plan is empty, done. Go back to idle.
        if (currentActions.Count == 0)
        {
            ActionsFinished();
            fsm.PopState();
            fsm.PushState(IdleState);
            return;
        }

        // Get the current action, but don't remove it from the queue yet.
        GOAPAction action = currentActions.Peek();

        // Check if the current action is finished.
        if (action.isDone)
        {
            // It's done, so remove it from the plan.
            currentActions.Dequeue();
            // Return immediately to process the next action or finish the plan on the next frame.
            // This prevents trying to use a completed action.
            return;
        }

        if (IsTargetVisible())
        {
            // The action's target is our current destination.
            Vector3 currentDestination = action.target.transform.position;

            if (Vector3.Distance(target.position, currentDestination) > replanThreshold)
            {
                Debug.Log("<color=yellow>Target has moved. Replanning...</color>");

                // Abort the current action so it can clean up its resources (the DynamicTarget).
                action.OnPlanAborted();
                
                // Discard the entire old plan.
                currentActions.Clear();

                // Set the new destination and go back to planning.
                lastKnownPosition = target.position;
                fsm.PopState(); // Exit MoveState
                fsm.PushState(PlanState);
                return;
            }
        }

        // If we are here, it means we don't need to replan and the action is not done.
        // So, perform the action.
        if (!action.Perform((GameObject)data))
        {
            // Action failed to perform, abort the plan.
            action.OnPlanAborted(); // Clean up the failed action
            fsm.PopState();
            fsm.PushState(IdleState);
            PlanAborted(action);
        }
    }

    public HashSet<KeyValuePair<string, object>> GetWorldState()
    {
        HashSet<KeyValuePair<string, object>> worldData = new HashSet<KeyValuePair<string, object>>();
        worldData.Add(new KeyValuePair<string, object>("hasDestination", lastKnownPosition != Vector3.zero));
        return worldData;
    }

    public HashSet<KeyValuePair<string, object>> CreateGoalState()
    {
        HashSet<KeyValuePair<string, object>> goal = new HashSet<KeyValuePair<string, object>>();
        goal.Add(new KeyValuePair<string, object>("isAtDestination", true));
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
        Debug.Log("Actions Finished.");
        // Reached the last known position, reset it so it doesn't plan again unless we see the player.
        lastKnownPosition = Vector3.zero;
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
}
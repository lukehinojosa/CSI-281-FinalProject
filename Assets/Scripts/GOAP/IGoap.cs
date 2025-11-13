using System.Collections.Generic;

// Interface for any agent that can use the GOAP planner.
public interface IGoap
{
    // Returns the current state of the world from the agent's perspective.
    HashSet<KeyValuePair<string, object>> GetWorldState();

    // The goal the agent wants to achieve.
    HashSet<KeyValuePair<string, object>> CreateGoalState();

    // Called when a plan fails to find a solution.
    void PlanFailed(HashSet<KeyValuePair<string, object>> failedGoal);

    // Called when a plan is found.
    void PlanFound(HashSet<KeyValuePair<string, object>> goal, Queue<GOAPAction> actions);

    // Called when all actions in the plan are complete.
    void ActionsFinished();

    // Called when a plan is aborted.
    void PlanAborted(GOAPAction aborter);
}
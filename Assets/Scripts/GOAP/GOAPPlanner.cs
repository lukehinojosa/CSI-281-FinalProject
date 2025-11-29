using System.Collections.Generic;
using UnityEngine;

public class GOAPPlanner
{
    // Finds a sequence of actions to satisfy the goal.
    public Queue<GOAPAction> Plan(GameObject agent, HashSet<GOAPAction> availableActions, HashSet<KeyValuePair<string, object>> worldState, HashSet<KeyValuePair<string, object>> goal)
    {
        // Reset all actions before planning.
        foreach (GOAPAction action in availableActions)
        {
            action.DoReset();
        }

        // Check which actions can run.
        HashSet<GOAPAction> usableActions = new HashSet<GOAPAction>();
        foreach (GOAPAction action in availableActions)
        {
            if (action.CheckProceduralPrecondition(agent))
                usableActions.Add(action);
        }

        // Build a graph of possible actions.
        List<Node> leaves = new List<Node>();
        Node start = new Node(null, 0, worldState, null);

        bool success = BuildGraph(start, leaves, usableActions, goal);

        if (!success)
        {
            // No plan found.
            return null;
        }

        // Find the cheapest plan.
        Node cheapest = null;
        foreach (Node leaf in leaves)
        {
            if (cheapest == null || leaf.runningCost < cheapest.runningCost)
            {
                cheapest = leaf;
            }
        }

        // Backtrack from the cheapest leaf to build the plan.
        List<GOAPAction> result = new List<GOAPAction>();
        Node n = cheapest;
        while (n != null)
        {
            if (n.action != null)
            {
                result.Insert(0, n.action); // Insert at the front to reverse the order.
            }
            n = n.parent;
        }

        Queue<GOAPAction> queue = new Queue<GOAPAction>();
        foreach (GOAPAction a in result)
        {
            queue.Enqueue(a);
        }

        return queue;
    }

    // A*-like graph building, working backwards from the goal.
    private bool BuildGraph(Node parent, List<Node> leaves, HashSet<GOAPAction> usableActions, HashSet<KeyValuePair<string, object>> goal)
    {
        bool foundPath = false;

        foreach (GOAPAction action in usableActions)
        {
            // Debug
            Debug.Log("Checking Action: " + action.GetType().Name);
            Debug.Log("World State: {" + PrintState(parent.state) + "}");
            Debug.Log("Action Preconditions: {" + PrintState(action.Preconditions) + "}");
            bool preconditionsMet = StateContains(parent.state, action.Preconditions);
            Debug.Log("Precondition Met? " + (preconditionsMet ? "<color=green>YES</color>" : "<color=red>NO</color>"));
            // Debug
            
            // If the action can satisfy a condition of the goal
            if (preconditionsMet)
            {
                // Apply the action's preconditions to the parent's state.
                HashSet<KeyValuePair<string, object>> currentState = ApplyState(parent.state, action.Effects);
                
                Node node = new Node(parent, parent.runningCost + action.cost, currentState, action);

                // If the new state satisfies the goal
                if (StateContains(currentState, goal))
                {
                    // Valid plan found
                    leaves.Add(node);
                    foundPath = true;
                }
                else
                {
                    // If not, keep building the graph from this new node.
                    HashSet<GOAPAction> subset = ActionSubset(usableActions, action);
                    bool found = BuildGraph(node, leaves, subset, goal);
                    if (found)
                        foundPath = true;
                }
            }
        }

        return foundPath;
    }

    private HashSet<GOAPAction> ActionSubset(HashSet<GOAPAction> actions, GOAPAction removeMe)
    {
        HashSet<GOAPAction> subset = new HashSet<GOAPAction>();
        foreach (GOAPAction a in actions)
        {
            if (!a.Equals(removeMe))
                subset.Add(a);
        }
        return subset;
    }

    // Checks if all conditions in 'test' are met by 'state'.
    private bool StateContains(HashSet<KeyValuePair<string, object>> state, HashSet<KeyValuePair<string, object>> test)
    {
        return test.IsSubsetOf(state);
    }

    // Applies a new state 'changes' to the 'currentState'.
    private HashSet<KeyValuePair<string, object>> ApplyState(HashSet<KeyValuePair<string, object>> currentState, HashSet<KeyValuePair<string, object>> changes)
    {
        HashSet<KeyValuePair<string, object>> state = new HashSet<KeyValuePair<string, object>>();
        foreach (var s in currentState)
            state.Add(s);

        foreach (var change in changes)
        {
            // If the key already exists, update it.
            bool exists = false;
            foreach (var s in state)
            {
                if (s.Key.Equals(change.Key))
                {
                    exists = true;
                    break;
                }
            }

            if (exists)
            {
                // Remove and re-add.
                state.RemoveWhere((KeyValuePair<string, object> kvp) => { return kvp.Key.Equals(change.Key); });
            }
            state.Add(change);
        }
        return state;
    }

    // Private helper class for the planner's A* nodes.
    private class Node
    {
        public Node parent;
        public float runningCost;
        public HashSet<KeyValuePair<string, object>> state;
        public GOAPAction action;

        public Node(Node parent, float runningCost, HashSet<KeyValuePair<string, object>> state, GOAPAction action)
        {
            this.parent = parent;
            this.runningCost = runningCost;
            this.state = state;
            this.action = action;
        }
    }
    
    private string PrintState(HashSet<KeyValuePair<string, object>> state)
    {
        if (state == null) return "null";
        string s = "";
        foreach (var kvp in state)
        {
            s += kvp.Key + ":" + kvp.Value + ", ";
        }
        return s;
    }
}
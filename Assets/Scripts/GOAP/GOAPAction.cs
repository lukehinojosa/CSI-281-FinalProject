using UnityEngine;
using System.Collections.Generic;

public abstract class GOAPAction : MonoBehaviour
{
    public float cost = 1f;
    public GameObject target;

    private HashSet<KeyValuePair<string, object>> preconditions;
    private HashSet<KeyValuePair<string, object>> effects;

    public bool isDone = false;

    public GOAPAction()
    {
        preconditions = new HashSet<KeyValuePair<string, object>>();
        effects = new HashSet<KeyValuePair<string, object>>();
    }

    public void DoReset()
    {
        isDone = false;
        target = null;
        Reset();
    }

    // Abstract methods to be implemented by child classes.
    public abstract void Reset();
    public abstract bool CheckProceduralPrecondition(GameObject agent);
    public abstract bool Perform(GameObject agent);
    public abstract bool RequiresInRange();
    public abstract void OnPlanAborted();

    // Methods to manage preconditions and effects.
    public void AddPrecondition(string key, object value)
    {
        preconditions.Add(new KeyValuePair<string, object>(key, value));
    }

    public void RemovePrecondition(string key)
    {
        // Implementation to remove a precondition
    }

    public void AddEffect(string key, object value)
    {
        effects.Add(new KeyValuePair<string, object>(key, value));
    }

    public void RemoveEffect(string key)
    {
        // Implementation to remove an effect
    }

    public HashSet<KeyValuePair<string, object>> Preconditions { get { return preconditions; } }
    public HashSet<KeyValuePair<string, object>> Effects { get { return effects; } }
}
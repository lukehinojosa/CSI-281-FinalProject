using System.Collections.Generic;

// Finite State Machine
public class FSM
{
    private Stack<FSMState> stateStack = new Stack<FSMState>();

    public delegate void FSMState(FSM fsm, object data);

    public void Update(object data)
    {
        if (stateStack.Count > 0)
        {
            stateStack.Peek().Invoke(this, data);
        }
    }

    public void PushState(FSMState state)
    {
        stateStack.Push(state);
    }

    public void PopState()
    {
        stateStack.Pop();
    }
    
    public void ChangeState(FSMState state)
    {
        if (stateStack.Count > 0)
        {
            stateStack.Pop();
        }
        stateStack.Push(state);
    }
}
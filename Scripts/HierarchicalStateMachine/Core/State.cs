using System.Collections.Generic;

namespace HSM
{
    public abstract class State
    {
        private readonly List<IActivity> _activities = new();

        public StateMachine Machine { get; internal set; }
        public State Parent { get; }
        public State ActiveChild { get; set; }
        public IReadOnlyList<IActivity> Activities => _activities;

        public State(StateMachine machine, State parent = null)
        {
            Machine = machine;
            Parent = parent;
        }

        public void Add(IActivity activity)
        {
            _activities.Add(activity);
        }

        protected virtual State GetInitialState() => null;
        protected virtual State GetTransition() => null;

        protected virtual void OnEnter() { }
        protected virtual void OnExit() { }
        protected virtual void OnUpdate(float deltaTime) { }
        protected virtual void OnFixedUpdate(float fixedDeltaTime) { }

        internal void Enter()
        {
            if (Parent != null) Parent.ActiveChild = this;
            OnEnter();

            State initial = GetInitialState();
            initial?.Enter();
        }

        internal void Exit()
        {
            ActiveChild?.Exit();
            ActiveChild = null;
            OnExit();
        }

        internal void Update(float deltaTime)
        {
            State transition = GetTransition();
            if (transition != null)
            {
                Machine.Sequencer.RequestTransition(this, transition);
                return;
            }

            ActiveChild?.Update(deltaTime);
            OnUpdate(deltaTime);
        }

        internal void FixedUpdate(float fixedDeltaTime)
        {
            State transition = GetTransition();
            if (transition != null)
            {
                Machine.Sequencer.RequestTransition(this, transition);
                return;
            }

            ActiveChild?.FixedUpdate(fixedDeltaTime);
            OnFixedUpdate(fixedDeltaTime);
        }

        public State Leaf()
        {
            State current = this;
            while (current.ActiveChild != null) current = current.ActiveChild;
            return current;
        }

        public IEnumerable<State> PathToRoot()
        {
            for (State current = this; current != null; current = current.Parent)
                yield return current;
        }
    }
}

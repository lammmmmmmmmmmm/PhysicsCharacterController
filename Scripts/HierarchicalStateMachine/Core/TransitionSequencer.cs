using System;
using System.Collections.Generic;
using System.Threading;

namespace HSM
{
    public class TransitionSequencer
    {
        private IPhase _currentPhase;
        private Action _nextPhase;
        private (State from, State to)? _pendingTransition;
        private CancellationTokenSource _cancellationSource;
        private readonly Action<float> _stateMachineTick;
        private readonly Action<float> _stateMachineFixedTick;

        public StateMachine Machine { get; }
        public bool IsSequential { get; set; }

        #region Public Methods

        public TransitionSequencer(StateMachine machine)
        {
            Machine = machine;
            _stateMachineTick = machine.InternalTick;
            _stateMachineFixedTick = machine.InternalFixedTick;
        }

        public void RequestTransition(State from, State to)
        {
            if (to == null || from == to) return;

            if (_currentPhase != null)
            {
                _pendingTransition = (from, to);
                return;
            }

            BeginTransition(from, to);
        }

        public void Tick(float deltaTime)
        {
            TickInternal(deltaTime, _stateMachineTick);
        }

        public void FixedTick(float deltaTime)
        {
            TickInternal(deltaTime, _stateMachineFixedTick);
        }

        public static State FindLowestCommonAncestor(State a, State b)
        {
            var ancestorsOfA = new HashSet<State>();
            for (var current = a; current != null; current = current.Parent)
                ancestorsOfA.Add(current);

            for (var current = b; current != null; current = current.Parent)
            {
                if (ancestorsOfA.Contains(current))
                    return current;
            }

            return null;
        }

        #endregion

        #region Private Methods

        private void TickInternal(float deltaTime, Action<float> stateMachineTick)
        {
            if (_currentPhase != null)
            {
                TickTransition();
                return;
            }

            stateMachineTick(deltaTime);
        }

        private void TickTransition()
        {
            if (!_currentPhase.Update()) return;

            if (_nextPhase != null)
            {
                Action phase = _nextPhase;
                _nextPhase = null;
                phase();
                return;
            }

            EndTransition();
        }

        private void BeginTransition(State from, State to)
        {
            _cancellationSource?.Cancel();
            _cancellationSource = new CancellationTokenSource();

            State lca = FindLowestCommonAncestor(from, to);
            List<State> exitChain = CollectStatesToExit(from, lca);
            List<State> enterChain = CollectStatesToEnter(to, lca);

            StartDeactivationPhase(exitChain);
            ScheduleActivationPhase(from, to, enterChain);
        }

        private void StartDeactivationPhase(List<State> exitChain)
        {
            List<PhaseStep> exitSteps = GatherPhaseSteps(exitChain, isDeactivation: true);
            _currentPhase = CreatePhase(exitSteps);
            _currentPhase.Start();
        }

        private void ScheduleActivationPhase(State from, State to, List<State> enterChain)
        {
            List<PhaseStep> enterSteps = GatherPhaseSteps(enterChain, isDeactivation: false);
            if (enterSteps.Count == 0)
            {
                Machine.ChangeState(from, to);
                _nextPhase = null;
                EndTransition();
                return;
            }

            _nextPhase = () =>
            {
                Machine.ChangeState(from, to);
                _currentPhase = CreatePhase(enterSteps);
                _currentPhase.Start();
            };
        }

        private IPhase CreatePhase(List<PhaseStep> steps)
        {
            return IsSequential
                ? new SequentialPhase(steps, _cancellationSource.Token)
                : new ParallelPhase(steps, _cancellationSource.Token);
        }

        private void EndTransition()
        {
            _currentPhase = null;

            if (!_pendingTransition.HasValue) return;

            var pendingTransition = _pendingTransition.Value;
            _pendingTransition = null;
            BeginTransition(pendingTransition.from, pendingTransition.to);
        }

        private static List<PhaseStep> GatherPhaseSteps(List<State> chain, bool isDeactivation)
        {
            List<PhaseStep> steps = new();

            for (int i = 0; i < chain.Count; i++)
            {
                var activities = chain[i].Activities;
                for (int j = 0; j < activities.Count; j++)
                {
                    var activity = activities[j];
                    bool isEligible = isDeactivation
                        ? activity.Mode == ActivityMode.Active
                        : activity.Mode == ActivityMode.Inactive;

                    if (!isEligible) continue;

                    steps.Add(ct => isDeactivation
                        ? activity.DeactivateAsync(ct)
                        : activity.ActivateAsync(ct));
                }
            }

            return steps;
        }

        private static List<State> CollectStatesToExit(State from, State lca)
        {
            var chain = new List<State>();
            for (var current = from; current != null && current != lca; current = current.Parent)
                chain.Add(current);
            return chain;
        }

        private static List<State> CollectStatesToEnter(State to, State lca)
        {
            var stack = new Stack<State>();
            for (var current = to; current != lca; current = current.Parent)
                stack.Push(current);
            return new List<State>(stack);
        }

        #endregion

    }
}

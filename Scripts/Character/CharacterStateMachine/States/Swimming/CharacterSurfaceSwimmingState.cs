using HSM;

namespace PhysicsCharacterController.CharacterStateMachine.States
{
    public sealed class CharacterSurfaceSwimmingState : State
    {
        private readonly CharacterStateContext _context;

        public CharacterSurfaceSwimmingState(StateMachine machine, State parent, CharacterStateContext context)
            : base(machine, parent)
        {
            _context = context;
        }

        protected override State GetTransition()
        {
            if (_context.CharacterJump.TryExecuteWaterSurfaceJump())
            {
                _context.SwimmingMovement.BeginWaterSurfaceJumpReentrySuppression();
                CharacterRootState rootState = (CharacterRootState)Parent.Parent;
                return rootState.Terrestrial.Airborne;
            }

            if (_context.SwimmingMovement.ShouldDive() && _context.SwimmingMovement.TryEnterUnderwater())
            {
                return ((CharacterSwimmingState)Parent).Underwater;
            }

            return null;
        }

        protected override void OnEnter()
        {
            _context.Input.SetWaterSurfaceJumpsEnabled(true);
            _context.CharacterRotationPolicy.SetAutomaticRotationEnabled(true);
            _context.SwimmingMovement.BeginSurfaceVisualHandoff(UnityEngine.Time.fixedDeltaTime);
            _context.Animator.SetBase(
                _context.SurfaceSwimmingAnimationData.LocomotionMixer,
                _context.SurfaceSwimmingStateId,
                _context.SwimmingMovement.CurrentSpeedMetersPerSecond);
        }

        protected override void OnExit()
        {
            _context.Input.SetWaterSurfaceJumpsEnabled(false);
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            _context.SwimmingMovement.MoveAtSurface(fixedDeltaTime);
            float currentSpeedMetersPerSecond = _context.SwimmingMovement.CurrentSpeedMetersPerSecond;
            _context.Animator.UpdateLocomotionAnimationParameter(currentSpeedMetersPerSecond);
            _context.Animator.UpdateTransitionMixerParameter(currentSpeedMetersPerSecond);
        }
    }
}

using HSM;

namespace PhysicsCharacterController.CharacterStateMachine.States
{
    public sealed class CharacterUnderwaterSwimmingState : State
    {
        private readonly CharacterStateContext _context;

        public CharacterUnderwaterSwimmingState(StateMachine machine, State parent, CharacterStateContext context)
            : base(machine, parent)
        {
            _context = context;
        }

        protected override State GetTransition()
        {
            if (!_context.SwimmingMovement.ShouldReturnToSurface())
            {
                return null;
            }

            _context.SwimmingMovement.CancelUpwardVelocity();
            if (_context.SwimmingMovement.TryExitUnderwater())
            {
                return ((CharacterSwimmingState)Parent).Surface;
            }

            return null;
        }

        protected override void OnEnter()
        {
            _context.CharacterRotationPolicy.SetAutomaticRotationEnabled(false);
            _context.Animator.SetBase(
                _context.UnderwaterSwimmingAnimationData.LocomotionMixer,
                _context.UnderwaterSwimmingStateId,
                _context.SwimmingMovement.CurrentSpeedMetersPerSecond);
        }

        protected override void OnExit()
        {
            _context.CharacterRotationPolicy.SetAutomaticRotationEnabled(true);
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            if (_context.SwimmingMovement.IsTerrestrialExitRecoveryActive)
            {
                _context.SwimmingMovement.MoveForTerrestrialExitRecovery(fixedDeltaTime);
            }
            else if (_context.WaterSensor.IsSufficientlyImmersed)
            {
                _context.SwimmingMovement.MoveUnderwater(fixedDeltaTime);
            }
            else
            {
                _context.SwimmingMovement.MoveForTerrestrialExitRecovery(fixedDeltaTime);
            }

            float currentSpeedMetersPerSecond = _context.SwimmingMovement.CurrentSpeedMetersPerSecond;
            _context.Animator.UpdateLocomotionAnimationParameter(currentSpeedMetersPerSecond);
            _context.Animator.UpdateTransitionMixerParameter(currentSpeedMetersPerSecond);
        }
    }
}

using HSM;

namespace PhysicsCharacterController.CharacterStateMachine.States
{
    public sealed class CharacterCrouchingState : State
    {
        private readonly CharacterStateContext _context;

        public CharacterCrouchingState(StateMachine machine, State parent, CharacterStateContext context)
            : base(machine, parent)
        {
            _context = context;
        }

        protected override State GetTransition()
        {
            if (_context.CharacterCrouch.ShouldStayCrouched())
            {
                return null;
            }

            return ((CharacterGroundedState)Parent).Standing;
        }

        protected override void OnEnter()
        {
            _context.CharacterCrouch.ApplyCrouchState();
            _context.Animator.SetBase(_context.CrouchingAnimationData, _context.CrouchingStateId, _context.CharacterMove.CurrentSpeed);
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            float currentSpeed = _context.CharacterMove.CurrentSpeed;
            _context.Animator.UpdateLocomotionAnimationParameter(currentSpeed);
            _context.Animator.UpdateTransitionMixerParameter(currentSpeed);
        }
    }
}

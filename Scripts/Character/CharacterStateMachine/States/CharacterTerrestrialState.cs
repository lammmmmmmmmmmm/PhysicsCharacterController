using HSM;

namespace PhysicsCharacterController.CharacterStateMachine.States
{
    public sealed class CharacterTerrestrialState : State
    {
        public readonly CharacterGroundedState Grounded;
        public readonly CharacterAirborneState Airborne;

        private readonly CharacterStateContext _context;

        public CharacterTerrestrialState(StateMachine machine, State parent, CharacterStateContext context)
            : base(machine, parent)
        {
            _context = context;
            Grounded = new CharacterGroundedState(machine, this, context);
            Airborne = new CharacterAirborneState(machine, this, context);
        }

        protected override State GetInitialState()
        {
            return _context.IsGrounded ? Grounded : Airborne;
        }

        protected override State GetTransition()
        {
            _context.SwimmingMovement.RefreshWaterSurfaceJumpReentrySuppression();
            return _context.SwimmingMovement.CanEnterSwimming
                ? ((CharacterRootState)Parent).Swimming
                : null;
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            // A transition requested during this tick enters swimming immediately. Do not let the
            // outgoing terrestrial branch reapply gravity after swimming has stopped the fall.
            if (_context.SwimmingMovement.CanEnterSwimming)
            {
                return;
            }

            _context.CharacterGravity.ApplyGravity();
            ApplyMovementByInput();
            _context.CharacterJump.HandleCoyoteTime(fixedDeltaTime);
            _context.CharacterJump.HandleJumpBuffer(fixedDeltaTime);
        }

        private void ApplyMovementByInput()
        {
            if (_context.CharacterMove.HasMovementInput)
            {
                _context.CharacterMove.MoveWithInput();
                return;
            }

            _context.CharacterMove.Decelerate();
        }
    }
}

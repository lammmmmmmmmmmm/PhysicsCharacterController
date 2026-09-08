using HSM;

namespace PhysicsCharacterController.CharacterStateMachine.States
{
    public sealed class CharacterRootState : State
    {
        public readonly CharacterTerrestrialState Terrestrial;
        public readonly CharacterSwimmingState Swimming;

        private readonly CharacterStateContext _context;

        public CharacterRootState(StateMachine machine, CharacterStateContext context)
            : base(machine, null)
        {
            _context = context;
            Terrestrial = new CharacterTerrestrialState(machine, this, context);
            Swimming = new CharacterSwimmingState(machine, this, context);
        }

        protected override State GetInitialState()
        {
            _context.SwimmingMovement.RefreshWaterSurfaceJumpReentrySuppression();
            return _context.SwimmingMovement.CanEnterSwimming
                ? Swimming
                : Terrestrial;
        }

    }
}

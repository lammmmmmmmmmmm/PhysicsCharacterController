using HSM;
using UnityEngine;

namespace PhysicsCharacterController.CharacterStateMachine.States
{
    public sealed class CharacterSwimmingState : State
    {
        private const float BLOCKED_EXIT_WARNING_INTERVAL_SECONDS = 2f;

        public readonly CharacterSurfaceSwimmingState Surface;
        public readonly CharacterUnderwaterSwimmingState Underwater;

        private readonly CharacterStateContext _context;
        private float _nextBlockedExitWarningTimeSeconds;

        public CharacterSwimmingState(StateMachine machine, State parent, CharacterStateContext context)
            : base(machine, parent)
        {
            _context = context;
            Surface = new CharacterSurfaceSwimmingState(machine, this, context);
            Underwater = new CharacterUnderwaterSwimmingState(machine, this, context);
        }

        protected override State GetInitialState()
        {
            bool shouldBeginUnderwater = !_context.SwimmingMovement.ShouldReturnToSurface();
            if (shouldBeginUnderwater && _context.SwimmingMovement.TryEnterUnderwater())
            {
                return Underwater;
            }

            return Surface;
        }

        protected override State GetTransition()
        {
            if (_context.WaterSensor.IsSufficientlyImmersed)
            {
                _context.SwimmingMovement.CancelTerrestrialExitRecovery();
                return null;
            }

            if (!TryPrepareTerrestrialExit())
            {
                return null;
            }

            return ((CharacterRootState)Parent).Terrestrial;
        }

        protected override void OnEnter()
        {
            _context.Input.SetTerrestrialActionsEnabled(false);
            _context.CharacterCrouch.ApplyStandState();
        }

        protected override void OnExit()
        {
            _context.Input.SetTerrestrialActionsEnabled(true);
            _context.CharacterRotationPolicy.SetAutomaticRotationEnabled(true);
            _context.SwimmingMovement.ResetMovement();
        }

        private bool TryPrepareTerrestrialExit()
        {
            if (_context.SwimmingMovement.TryExitUnderwater())
            {
                return true;
            }

            if (_context.SwimmingMovement.IsTerrestrialExitRecoveryActive
                || _context.SwimmingMovement.TryBeginTerrestrialExitRecovery())
            {
                return false;
            }

            if (Time.time >= _nextBlockedExitWarningTimeSeconds)
            {
                Debug.LogWarning(
                    "Swimming exit recovery could not find a nearby collision-free upright pose. " +
                    "Swimming remains active until space becomes available.");
                _nextBlockedExitWarningTimeSeconds = Time.time + BLOCKED_EXIT_WARNING_INTERVAL_SECONDS;
            }

            return false;
        }
    }
}

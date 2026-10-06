using HSM;

namespace PhysicsCharacterController.CharacterStateMachine.States
{
    public sealed class CharacterAirborneState : State
    {
        private const float MINIMUM_AIRBORNE_SECONDS = 0.1f;

        private readonly CharacterStateContext _context;
        private bool _isPlayingJumpClip;
        private float _minimumAirborneTimer;

        public CharacterAirborneState(StateMachine machine, State parent, CharacterStateContext context)
            : base(machine, parent)
        {
            _context = context;
        }

        protected override State GetTransition()
        {
            if (_context.CharacterJump.TryExecuteJump())
            {
                return null;
            }

            if (_context.IsGrounded && _minimumAirborneTimer <= 0f)
            {
                return ((CharacterTerrestrialState)Parent).Grounded;
            }

            return null;
        }

        protected override void OnEnter()
        {
            _minimumAirborneTimer = MINIMUM_AIRBORNE_SECONDS;

            bool isRising = _context.CharacterGravity.VerticalVelocity >= 0f;
            PlayAirborneClip(isRising);
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            if (_minimumAirborneTimer > 0f)
            {
                _minimumAirborneTimer -= fixedDeltaTime;
            }

            _context.Animator.UpdateTransitionMixerParameter(_context.CharacterMove.CurrentSpeed);

            bool isRising = _context.CharacterGravity.VerticalVelocity >= 0f;

            if (_isPlayingJumpClip && !isRising)
            {
                PlayAirborneClip(isRising: false);
            }
        }

        private void PlayAirborneClip(bool isRising)
        {
            _isPlayingJumpClip = isRising;
            _context.Animator.SetBase(_context.AirborneAnimationData, isRising, _context.AirborneStateId,
                _context.CharacterMove.CurrentSpeed);
        }
    }
}

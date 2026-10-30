using UnityEngine;

namespace PhysicsCharacterController.Tests.PlayMode
{
    [DefaultExecutionOrder(10)]
    public sealed class StepMovementTestDriver : BaseCharacterInput
    {
        [SerializeField] private CharacterMove _characterMove;
        [SerializeField] private CharacterGravity _characterGravity;
        [SerializeField] private CharacterJump _characterJump;

        public bool IsMovementEnabled { get; set; }

        #region Unity Lifecycle

        private void FixedUpdate()
        {
            _characterJump.TryExecuteJump();
            _characterGravity.ApplyGravity();
            if (IsMovementEnabled)
            {
                _characterMove.MoveWithInput();
            }
            else
            {
                _characterMove.Decelerate();
            }
        }

        #endregion

        #region Public Methods

        public override Vector2 GetMoveInput() => IsMovementEnabled ? Vector2.up : Vector2.zero;
        public override float GetMoveAngle() => 0f;
        public void RequestJump() => InvokeJumpPressed();

        #endregion
    }
}

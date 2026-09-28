using UnityEngine;

namespace PhysicsCharacterController
{
    public sealed class AICharacterRotationPolicy : CharacterRotationPolicy
    {
        [SerializeField] private BaseCharacterInput _input;

        private Vector3 _facingDirection;
        private bool _hasFacingOverride;
        public override Vector3 MovementForwardDirection => Vector3.forward;
        public override Vector3 MovementRightDirection => Vector3.right;

        #region Public Methods

        public void FacePosition(Vector3 positionMeters)
        {
            _facingDirection = positionMeters - transform.position;
            _hasFacingOverride = true;
        }

        public void ClearFacingOverride() => _hasFacingOverride = false;

        #endregion

        #region Protected Methods

        protected override bool TryResolveFacingDirection(out Vector3 worldDirection)
        {
            worldDirection = _hasFacingOverride ? _facingDirection : _input.HorizontalMoveDirection;
            return _input.AreNormalActionsEnabled && (_hasFacingOverride || _input.GetMoveInput().sqrMagnitude > 0f);
        }

        #endregion
    }
}

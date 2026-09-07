using UnityEngine;

namespace PhysicsCharacterController
{
    /// <summary>
    /// Selects player facing and movement axes from player input and the active camera.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCharacterRotationPolicy : CharacterRotationPolicy
    {
        [Header("Dependencies")]
        [SerializeField] private BaseCharacterInput _input;
        [SerializeField] private Transform _characterCamera;

        private bool _isLockedToCamera;
        private bool _isCameraFacingOverrideEnabled;

        public override Vector3 MovementForwardDirection => _characterCamera.forward;
        public override Vector3 MovementRightDirection => _characterCamera.right;

        #region Public Methods

        public void SetLockedToCamera(bool isLockedToCamera)
        {
            _isLockedToCamera = isLockedToCamera;
        }

        public void SetCameraFacingOverride(bool isEnabled)
        {
            _isCameraFacingOverrideEnabled = isEnabled;
        }

        #endregion

        #region Protected Methods

        protected override bool TryResolveFacingDirection(out Vector3 worldDirection)
        {
            if (!_input.AreNormalActionsEnabled)
            {
                worldDirection = default;
                return false;
            }

            if (_isLockedToCamera || _isCameraFacingOverrideEnabled)
            {
                worldDirection = _characterCamera.forward;
                return true;
            }

            if (_input.GetMoveInput().sqrMagnitude <= Mathf.Epsilon)
            {
                worldDirection = default;
                return false;
            }

            worldDirection = _input.HorizontalMoveDirection;
            return true;
        }

        #endregion
    }
}

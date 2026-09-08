using UnityEngine;

namespace PhysicsCharacterController
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CharacterSwimmingMovement : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private SwimmingMovementSettingsSO _settingsSO;
        [SerializeField] private CharacterWaterSensor _waterSensor;
        [SerializeField] private BaseCharacterInput _input;
        [SerializeField] private CharacterRotation _characterRotation;
        [SerializeField] private CharacterRotationPolicy _characterRotationPolicy;
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private UnderwaterSwimmingCollider _underwaterCollider;
        [SerializeField] private CharacterSwimmingVisualOrientation _visualOrientation;

        private readonly SwimmingMotionSolver _motionSolver = new();
        private readonly SwimmingStateResolver _stateResolver = new();

        public float CurrentSpeedMetersPerSecond { get; private set; }
        public Vector3 RequestedDirection { get; private set; }
        public float SurfaceTargetRootHeightMeters => _waterSensor.WaterSurfaceHeightMeters - _settingsSO.SurfaceRootDepthMeters;
        public bool IsTerrestrialExitRecoveryActive => _underwaterCollider.IsTerrestrialExitRecoveryActive;

        #region Public Methods

        public void StopSinkingOnSwimmingEntry()
        {
            _rigidbody.linearVelocity = _motionSolver.RemoveDownwardVelocity(_rigidbody.linearVelocity);
        }

        public bool ShouldDive()
        {
            Vector2 movementInput = _input.GetMoveInput();
            Vector3 underwaterDirection = CalculateUnderwaterDirection(movementInput);
            return _stateResolver.ShouldDive(
                movementInput.magnitude,
                underwaterDirection.y,
                _settingsSO.MovementInputThreshold,
                _settingsSO.DiveDirectionYThreshold);
        }

        public bool ShouldReturnToSurface()
        {
            Vector2 movementInput = _input.GetMoveInput();
            Vector3 underwaterDirection = CalculateUnderwaterDirection(movementInput);
            return _stateResolver.ShouldReturnToSurface(
                transform.position.y,
                SurfaceTargetRootHeightMeters,
                underwaterDirection.y,
                _settingsSO.DiveDirectionYThreshold,
                _settingsSO.SurfaceToleranceMeters);
        }

        public bool TryEnterUnderwater()
        {
            Vector3 underwaterDirection = CalculateUnderwaterDirection(_input.GetMoveInput());
            return _underwaterCollider.TryActivate(underwaterDirection);
        }

        public bool TryExitUnderwater()
        {
            bool wasUnderwaterColliderActive = _underwaterCollider.IsActive;
            Quaternion previousCharacterRootRotation = _rigidbody.rotation;
            if (!_underwaterCollider.TryDeactivate())
            {
                return false;
            }

            if (wasUnderwaterColliderActive)
            {
                _characterRotation.SetFacingDirectionImmediately(_underwaterCollider.AcceptedDirection);
                _visualOrientation.PreserveWorldRotationAfterRootRotation(previousCharacterRootRotation, _rigidbody.rotation);
            }

            return true;
        }

        public bool TryBeginTerrestrialExitRecovery()
        {
            return _underwaterCollider.TryBeginTerrestrialExitRecovery();
        }

        public void MoveForTerrestrialExitRecovery(float fixedDeltaTime)
        {
            if (!_underwaterCollider.AdvanceTerrestrialExitRecovery(fixedDeltaTime))
            {
                MoveUnderwater(fixedDeltaTime);
                return;
            }

            RequestedDirection = _underwaterCollider.TerrestrialExitRecoveryDirection;
            UpdateAnimationSpeed(RequestedDirection.magnitude, _settingsSO.ExitRecoverySpeedMetersPerSecond, fixedDeltaTime);
            float swimmingAnimationBlend01 = CurrentSpeedMetersPerSecond / _settingsSO.NormalSpeedMetersPerSecond;
            _visualOrientation.AlignToColliderRotation(
                _underwaterCollider.AcceptedRotation,
                _rigidbody.rotation,
                swimmingAnimationBlend01,
                fixedDeltaTime);
        }

        public void CancelTerrestrialExitRecovery()
        {
            _underwaterCollider.CancelTerrestrialExitRecovery();
        }

        public void MoveAtSurface(float fixedDeltaTime)
        {
            Vector2 movementInput = _input.GetMoveInput();
            RequestedDirection = _motionSolver.CalculateSurfaceDirection(
                movementInput,
                _characterRotationPolicy.MovementForwardDirection,
                _characterRotationPolicy.MovementRightDirection);

            float speedMetersPerSecond = ResolveRequestedSpeedMetersPerSecond();
            Vector3 targetVelocity = _motionSolver.CalculateSurfaceTargetVelocity(
                RequestedDirection,
                speedMetersPerSecond,
                transform.position.y,
                SurfaceTargetRootHeightMeters,
                _settingsSO.SurfaceToleranceMeters,
                _settingsSO.MaximumSurfaceStabilizationSpeedMetersPerSecond,
                fixedDeltaTime);

            ApplyTargetVelocity(targetVelocity, fixedDeltaTime);
            _rigidbody.linearVelocity = _motionSolver.ClampUpwardVelocityToSurface(
                _rigidbody.linearVelocity,
                transform.position.y,
                SurfaceTargetRootHeightMeters,
                fixedDeltaTime);
            UpdateAnimationSpeed(RequestedDirection.magnitude, speedMetersPerSecond, fixedDeltaTime);
            _visualOrientation.ReturnToUpright(fixedDeltaTime);
        }

        public void MoveUnderwater(float fixedDeltaTime)
        {
            Vector2 movementInput = _input.GetMoveInput();
            RequestedDirection = CalculateUnderwaterDirection(movementInput);

            if (RequestedDirection.sqrMagnitude > _settingsSO.MovementInputThreshold * _settingsSO.MovementInputThreshold)
            {
                _underwaterCollider.TryAlign(
                    RequestedDirection,
                    fixedDeltaTime,
                    _settingsSO.UnderwaterColliderRotationSpeedDegreesPerSecond);
            }

            float speedMetersPerSecond = ResolveRequestedSpeedMetersPerSecond();
            Vector3 targetVelocity = RequestedDirection * speedMetersPerSecond;
            ApplyTargetVelocity(targetVelocity, fixedDeltaTime);
            UpdateAnimationSpeed(RequestedDirection.magnitude, speedMetersPerSecond, fixedDeltaTime);
            float swimmingAnimationBlend01 = CurrentSpeedMetersPerSecond / _settingsSO.NormalSpeedMetersPerSecond;
            _visualOrientation.AlignToColliderRotation(
                _underwaterCollider.AcceptedRotation,
                _rigidbody.rotation,
                swimmingAnimationBlend01,
                fixedDeltaTime);
        }

        public void CancelUpwardVelocity()
        {
            Vector3 velocity = _rigidbody.linearVelocity;
            if (velocity.y > 0f)
            {
                velocity.y = 0f;
                _rigidbody.linearVelocity = velocity;
            }
        }

        public void ResetMovement()
        {
            CancelTerrestrialExitRecovery();
            CurrentSpeedMetersPerSecond = 0f;
            RequestedDirection = Vector3.zero;
            _visualOrientation.ResetImmediately();
        }

        #endregion

        #region Private Methods

        private Vector3 CalculateUnderwaterDirection(Vector2 movementInput)
        {
            return _motionSolver.CalculateUnderwaterDirection(
                movementInput,
                _characterRotationPolicy.MovementForwardDirection,
                _characterRotationPolicy.MovementRightDirection);
        }

        private float ResolveRequestedSpeedMetersPerSecond()
        {
            return _input.IsSprintRequested
                ? _settingsSO.FastSpeedMetersPerSecond
                : _settingsSO.NormalSpeedMetersPerSecond;
        }

        private void UpdateAnimationSpeed(
            float requestedDirectionMagnitude,
            float requestedSpeedMetersPerSecond,
            float fixedDeltaTime)
        {
            CurrentSpeedMetersPerSecond = _motionSolver.MoveAnimationSpeedMetersPerSecond(
                CurrentSpeedMetersPerSecond,
                requestedDirectionMagnitude,
                requestedSpeedMetersPerSecond,
                _settingsSO.AccelerationMetersPerSecondSquared,
                _settingsSO.DecelerationMetersPerSecondSquared,
                fixedDeltaTime);
        }

        private void ApplyTargetVelocity(Vector3 targetVelocity, float fixedDeltaTime)
        {
            Vector3 velocity = _motionSolver.MoveVelocity(
                _rigidbody.linearVelocity,
                targetVelocity,
                _settingsSO.AccelerationMetersPerSecondSquared,
                _settingsSO.DecelerationMetersPerSecondSquared,
                fixedDeltaTime);

            float travelDistanceMeters = velocity.magnitude * fixedDeltaTime;
            if (travelDistanceMeters > 0f && _rigidbody.SweepTest(
                    velocity.normalized,
                    out RaycastHit hit,
                    travelDistanceMeters + _settingsSO.CollisionSweepSkinMeters,
                    QueryTriggerInteraction.Ignore))
            {
                velocity = _motionSolver.ProjectVelocityOnCollisionPlane(velocity, hit.normal);
            }

            _rigidbody.linearVelocity = velocity;
        }

        #endregion
    }
}

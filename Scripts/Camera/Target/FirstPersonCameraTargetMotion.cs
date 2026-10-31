using UnityEngine;

namespace PhysicsCharacterController
{
    /// <summary>
    /// Owns the complete local motion of the first-person camera target. It keeps the target ahead
    /// of the animated head using only the bone's forward position, then adds grounded speed-scaled
    /// bob. Clearance cannot retreat during locomotion, so the gait cycle cannot add depth shake.
    /// Animation height, animation rotation, and competing transform writers are excluded.
    /// </summary>
    [DefaultExecutionOrder(-5)]
    public class FirstPersonCameraTargetMotion : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _characterTransform;
        [SerializeField] private Transform _animatedHeadBone;
        [SerializeField] private Rigidbody _characterRigidbody;
        [SerializeField] private GroundChecker _groundChecker;
        [SerializeField] private CharacterMove _characterMove;

        [Header("Head Clearance")]
        [SerializeField, Min(0f)] private float _headClearanceMeters = 0.18f;
        [SerializeField, Min(0f)] private float _safetyMarginMeters = 0.03f;
        [SerializeField, Min(0f)] private float _peakHoldSeconds = 0.1f;
        [SerializeField, Min(0f)] private float _clearanceReturnSmoothTimeSeconds = 0.12f;
        [SerializeField, Min(0f)] private float _maximumClearanceCorrectionMeters = 0.75f;

        [Header("Movement Bob")]
        [SerializeField, Min(0f)] private float _minimumBobSpeedMetersPerSecond = 0.1f;
        [SerializeField, Min(0f)] private float _maximumLateralBobMeters = 0.025f;
        [SerializeField, Min(0f)] private float _maximumVerticalBobMeters = 0.035f;
        [SerializeField, Min(0f)] private float _minimumBobFrequencyHertz = 1.5f;
        [SerializeField, Min(0f)] private float _maximumBobFrequencyHertz = 2.4f;
        [SerializeField, Min(0f)] private float _bobActivationSeconds = 0.1f;
        [SerializeField, Min(0f)] private float _bobReturnSeconds = 0.15f;

        private HeadClearanceCalculator _headClearanceCalculator;
        private HeadBobOscillator _headBobOscillator;
        private bool _hasLoggedClearanceWarning;

        #region Unity Lifecycle

        private void Awake()
        {
            _headClearanceCalculator = new HeadClearanceCalculator(
                _headClearanceMeters,
                _safetyMarginMeters,
                _peakHoldSeconds,
                _clearanceReturnSmoothTimeSeconds,
                _maximumClearanceCorrectionMeters);

            _headBobOscillator = new HeadBobOscillator(
                _minimumBobSpeedMetersPerSecond,
                _maximumLateralBobMeters,
                _maximumVerticalBobMeters,
                _minimumBobFrequencyHertz,
                _maximumBobFrequencyHertz,
                _bobActivationSeconds,
                _bobReturnSeconds);
        }

        private void OnEnable()
        {
            _headClearanceCalculator.Reset();
            _headBobOscillator.Reset();
            transform.localPosition = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (Time.deltaTime <= 0f)
            {
                return;
            }

            ApplyFirstPersonTargetMotion();
        }

        #endregion

        #region Private Methods

        private void ApplyFirstPersonTargetMotion()
        {
            Vector3 characterForward = Vector3.ProjectOnPlane(_characterTransform.forward, Vector3.up).normalized;
            Vector3 characterRight = Vector3.Cross(Vector3.up, characterForward);
            Vector3 headFromStablePoint = _animatedHeadBone.position - transform.parent.position;
            float headForwardFromStablePointMeters = Vector3.Dot(headFromStablePoint, characterForward);
            Vector3 planarVelocity = Vector3.ProjectOnPlane(_characterRigidbody.linearVelocity, Vector3.up);
            bool isActivelyMoving = _characterMove.HasMovementInput && planarVelocity.magnitude > _minimumBobSpeedMetersPerSecond;
            float clearanceCorrectionMeters = _headClearanceCalculator.UpdateCorrectionMeters(headForwardFromStablePointMeters, Time.deltaTime, !isActivelyMoving, out bool hasExceededMaximumCorrection);

            LogClearanceWarningOnce(hasExceededMaximumCorrection);

            Vector3 bobOffsetMeters = _headBobOscillator.UpdateOffsetMeters(
                planarVelocity.magnitude,
                _characterMove.SprintSpeedMetersPerSecond,
                _groundChecker.IsGrounded,
                _characterMove.HasMovementInput,
                Time.deltaTime);
            Vector3 worldOffset = characterForward * clearanceCorrectionMeters + characterRight * bobOffsetMeters.x + Vector3.up * bobOffsetMeters.y;

            transform.localPosition = transform.parent.InverseTransformVector(worldOffset);
        }

        private void LogClearanceWarningOnce(bool hasExceededMaximumCorrection)
        {
            if (!hasExceededMaximumCorrection || _hasLoggedClearanceWarning)
            {
                return;
            }

            Debug.LogWarning(
                $"{nameof(FirstPersonCameraTargetMotion)} on '{name}' reached its maximum " +
                "head-clearance correction. Increase the configured envelope or use a " +
                "first-person-safe character mesh to prevent clipping.",
                this);
            _hasLoggedClearanceWarning = true;
        }

        #endregion
    }
}

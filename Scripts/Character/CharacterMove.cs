#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace PhysicsCharacterController
{
    public class CharacterMove : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float _walkSpeedMetersPerSecond = 14f;
        [SerializeField] private float _sprintSpeedMetersPerSecond = 20f;
        [Range(0.01f, 0.99f)]
        [Tooltip("Minimum input value to trigger movement")]
        [SerializeField] private float _movementThreshold = 0.01f;
        [SerializeField] private float _acceleration = 10f;
        [SerializeField] private float _deceleration = 10f;

        [Header("Locomotion Feedback")]
        [Tooltip("Duration of the grounded animation's run-to-idle blend, beginning when measured movement slows down.")]
        [SerializeField, Min(0.01f)] private float _idleAnimationBlendSeconds = 0.15f;

        [Header("References")]
        [SerializeField] private BaseCharacterInput _input;
        [SerializeField] private Rigidbody _rigidbody;

        private IMovementModifier[] _speedModifiers;
        private IVelocityModifier[] _velocityModifiers;
        private IMovementStepResolver[] _stepResolvers;

        private readonly PlanarLocomotionSpeedSampler _locomotionSpeedSampler = new();
        private readonly LocomotionAnimationSpeedBlend _locomotionAnimationSpeedBlend = new();
        private float _commandedSpeedMetersPerSecond;
        private Vector2 _currentInput => _input.GetMoveInput();
        private bool _isSprinting;

        public float WalkSpeedMetersPerSecond => _walkSpeedMetersPerSecond;
        public float SprintSpeedMetersPerSecond => _sprintSpeedMetersPerSecond;
        public float CommandedSpeedMetersPerSecond => _commandedSpeedMetersPerSecond;
        public float MeasuredPlanarSpeedMetersPerSecond => _locomotionSpeedSampler.SpeedMetersPerSecond;
        public float LocomotionAnimationSpeedMetersPerSecond => _locomotionAnimationSpeedBlend.SpeedMetersPerSecond;
        public float MovementThreshold => _movementThreshold;
        public bool IsSprinting => _isSprinting;
        public bool HasMovementInput => _currentInput.sqrMagnitude > _movementThreshold * _movementThreshold;

        #region Unity Lifecycle

        private void Awake()
        {
            _speedModifiers = GetComponents<IMovementModifier>();
            _velocityModifiers = GetComponents<IVelocityModifier>();
            _stepResolvers = GetComponents<IMovementStepResolver>();
        }

        private void OnEnable()
        {
            ResetLocomotionMeasurement();
            _input.OnSprint += OnSprintInput;
        }

        private void OnDisable()
        {
            ResetLocomotionMeasurement();
            _input.OnSprint -= OnSprintInput;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            var offset = new Vector3(0f, 0.1f, 0f);
            Gizmos.DrawLine(transform.position + offset, transform.position + offset + _input.HorizontalMoveDirection * 2f);
#if UNITY_EDITOR
            Handles.Label(transform.position + offset + _input.HorizontalMoveDirection * 2f, "Input Direction");
#endif
        }

        #endregion

        #region Public Methods

        public void ResetLocomotionMeasurement()
        {
            _locomotionSpeedSampler.Reset(_rigidbody.position);
            _locomotionAnimationSpeedBlend.Reset();
        }

        public void SampleLocomotionSpeed(float deltaSeconds)
        {
            if (!_locomotionSpeedSampler.Sample(_rigidbody.position, deltaSeconds, Mathf.Max(_walkSpeedMetersPerSecond, _sprintSpeedMetersPerSecond)))
            {
                _locomotionAnimationSpeedBlend.Reset();
                return;
            }

            _locomotionAnimationSpeedBlend.UpdateSpeed(MeasuredPlanarSpeedMetersPerSecond, deltaSeconds, _idleAnimationBlendSeconds);
        }

        public void MoveWithInput()
        {
            float targetSpeed = CalculateTargetSpeed();
            float combinedMultiplier = GetCombinedSpeedMultiplier();
            targetSpeed *= combinedMultiplier;

            _commandedSpeedMetersPerSecond = Mathf.MoveTowards(_commandedSpeedMetersPerSecond, targetSpeed, _acceleration * Time.fixedDeltaTime);

            ApplyMovementVelocity();
        }

        public void Decelerate()
        {
            _commandedSpeedMetersPerSecond = Mathf.MoveTowards(_commandedSpeedMetersPerSecond, 0f, _deceleration * Time.fixedDeltaTime);
            ApplyDecelerationVelocity();
        }

        #endregion

        #region Private Methods

        private void OnSprintInput(bool isSprinting) => _isSprinting = isSprinting;

        private float CalculateTargetSpeed()
        {
            return _isSprinting ? _sprintSpeedMetersPerSecond : _walkSpeedMetersPerSecond;
        }

        private float GetCombinedSpeedMultiplier()
        {
            float combinedMultiplier = 1f;
            foreach (var modifier in _speedModifiers)
            {
                combinedMultiplier *= modifier.GetSpeedMultiplier(_input.HorizontalMoveDirection);
            }

            return combinedMultiplier;
        }

        private void ApplyMovementVelocity()
        {
            Vector3 baseMovementVelocity = CalculateBaseVelocity();
            Vector3 stepVelocityMetersPerSecond = baseMovementVelocity;
            stepVelocityMetersPerSecond.y = _rigidbody.linearVelocity.y;
            foreach (IMovementStepResolver stepResolver in _stepResolvers)
            {
                if (stepResolver.TryApplyStep(stepVelocityMetersPerSecond, Time.fixedDeltaTime))
                {
                    return;
                }
            }

            Vector3 additiveVelocity = CalculateAdditiveVelocity(baseMovementVelocity);

            // Combine: Base Input + Gravity (preserved from RB) + Additive Modifiers
            Vector3 finalVelocity = baseMovementVelocity;
            finalVelocity.x += additiveVelocity.x;
            // This can increase or decrease Y velocity way too much
            finalVelocity.y = _rigidbody.linearVelocity.y + additiveVelocity.y;
            finalVelocity.z += additiveVelocity.z;

            _rigidbody.linearVelocity = finalVelocity;
        }

        private Vector3 CalculateBaseVelocity()
        {
            return _input.HorizontalMoveDirection * _commandedSpeedMetersPerSecond;
        }

        private Vector3 CalculateAdditiveVelocity(Vector3 baseVelocity)
        {
            Vector3 totalAdditive = Vector3.zero;

            foreach (var modifier in _velocityModifiers)
            {
                totalAdditive += modifier.GetVelocityContribution(_rigidbody.linearVelocity, baseVelocity);
            }

            return totalAdditive;
        }

        private void ApplyDecelerationVelocity()
        {
            Vector3 baseMovementVelocity = CalculateBaseVelocity();

            Vector3 finalVelocity = baseMovementVelocity;
            finalVelocity.y = _rigidbody.linearVelocity.y;

            _rigidbody.linearVelocity = finalVelocity;
        }

        #endregion
    }
}

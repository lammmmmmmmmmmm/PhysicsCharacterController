#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace PhysicsCharacterController
{
    /// <summary>
    /// Detects whether the character is touching a climbable step.
    /// Uses raycasts in multiple directions to check for step geometry.
    /// </summary>
    public class StepChecker : MonoBehaviour, IVelocityModifier
    {
        [Header("Configuration")]
        [SerializeField] private LayerMask _groundMask;
        [Tooltip("Distance from the player center used to check if the player is touching a step")]
        [SerializeField] private float _stepCheckerThreshold = 0.6f;
        [Tooltip("Angle offset in degrees for the two additional step checks")]
        [Range(0f, 180f)]
        [SerializeField] private float _checkAngleDegrees = 45f;
        [Tooltip("Max climbable step height")]
        [SerializeField] private float _maxStepHeight = 0.74f;
        [Tooltip("Vertical speed used while automatically climbing a step")]
        [SerializeField] private float _stepUpSpeedMetersPerSecond = 1.5f;

        [Header("References")]
        [SerializeField] private BaseCharacterInput _input;
        [SerializeField] private GroundChecker _groundChecker;
        [SerializeField] private CharacterJump _characterJump;

        private float _feetOffset;

        public bool IsTouchingStep { get; private set; }

        public void SetFeetOffset(float feetOffset)
        {
            _feetOffset = feetOffset;
        }

        private void FixedUpdate()
        {
            Check(_input.HorizontalMoveDirection);
        }

        public Vector3 GetVelocityContribution(Vector3 currentVelocity, Vector3 desiredMovement)
        {
            if (!IsTouchingStep || _characterJump.IsJumpInProgress)
            {
                return Vector3.zero;
            }

            float requiredUpwardSpeedMetersPerSecond = Mathf.Max(0f, _stepUpSpeedMetersPerSecond - currentVelocity.y);
            return Vector3.up * requiredUpwardSpeedMetersPerSecond;
        }

        private void Check(Vector3 globalForward)
        {
            bool canContinueStep = IsTouchingStep && !_characterJump.IsJumpInProgress;
            if (!_groundChecker.IsGrounded && !canContinueStep)
            {
                IsTouchingStep = false;
                return;
            }

            bool isTouchingStep = false;
            Vector3 bottomStepPos = transform.position - new Vector3(0f, _feetOffset, 0f) + new Vector3(0f, 0.05f, 0f);

            if (CheckStepInDirection(bottomStepPos, globalForward))
            {
                isTouchingStep = true;
            }

            if (CheckStepInDirection(bottomStepPos, Quaternion.AngleAxis(_checkAngleDegrees, transform.up) * globalForward))
            {
                isTouchingStep = true;
            }

            if (CheckStepInDirection(bottomStepPos, Quaternion.AngleAxis(-_checkAngleDegrees, transform.up) * globalForward))
            {
                isTouchingStep = true;
            }

            IsTouchingStep = isTouchingStep;
        }

        private bool CheckStepInDirection(Vector3 bottomStepPos, Vector3 direction)
        {
            if (!Physics.Raycast(bottomStepPos, direction, out var stepLowerHit, _stepCheckerThreshold, _groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            bool isVerticalSurface = RoundValue(stepLowerHit.normal.y) == 0;
            bool hasSpaceAbove = !Physics.Raycast(
                bottomStepPos + new Vector3(0f, _maxStepHeight, 0f),
                direction,
                out _,
                _stepCheckerThreshold + 0.05f,
                _groundMask,
                QueryTriggerInteraction.Ignore);

            return isVerticalSurface && hasSpaceAbove;
        }

        private static float RoundValue(float value)
        {
            float unit = Mathf.Round(value);

            if (value - unit < 0.000001f && value - unit > -0.000001f)
            {
                return unit;
            }

            return value;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_feetOffset <= 0f)
            {
                return;
            }

            Vector3 bottomStepPos = transform.position - new Vector3(0f, _feetOffset, 0f) + new Vector3(0f, 0.05f, 0f);
            Vector3 maxStepHeightPos = bottomStepPos + Vector3.up * _maxStepHeight;
            Vector3 forward = transform.forward;

            Gizmos.color = IsTouchingStep ? Color.green : Color.black;

            // Forward
            Gizmos.DrawLine(bottomStepPos, bottomStepPos + forward * _stepCheckerThreshold);
            Handles.Label(bottomStepPos + forward * _stepCheckerThreshold, "Step Forward");
            Gizmos.DrawLine(maxStepHeightPos, maxStepHeightPos + forward * (_stepCheckerThreshold + 0.05f));
            Handles.Label(maxStepHeightPos + forward * (_stepCheckerThreshold + 0.05f), "Step Forward");

            // Positive angle
            Vector3 forwardPositiveAngle = Quaternion.AngleAxis(_checkAngleDegrees, transform.up) * forward;
            Gizmos.DrawLine(bottomStepPos, bottomStepPos + forwardPositiveAngle * _stepCheckerThreshold);
            Handles.Label(bottomStepPos + forwardPositiveAngle * _stepCheckerThreshold, $"Step {_checkAngleDegrees:0.#}");
            Gizmos.DrawLine(maxStepHeightPos, maxStepHeightPos + forwardPositiveAngle * (_stepCheckerThreshold + 0.05f));
            Handles.Label(maxStepHeightPos + forwardPositiveAngle * (_stepCheckerThreshold + 0.05f), $"Step {_checkAngleDegrees:0.#}");

            // Negative angle
            Vector3 forwardNegativeAngle = Quaternion.AngleAxis(-_checkAngleDegrees, transform.up) * forward;
            Gizmos.DrawLine(bottomStepPos, bottomStepPos + forwardNegativeAngle * _stepCheckerThreshold);
            Handles.Label(bottomStepPos + forwardNegativeAngle * _stepCheckerThreshold, $"Step -{_checkAngleDegrees:0.#}");
            Gizmos.DrawLine(maxStepHeightPos, maxStepHeightPos + forwardNegativeAngle * (_stepCheckerThreshold + 0.05f));
            Handles.Label(maxStepHeightPos + forwardNegativeAngle * (_stepCheckerThreshold + 0.05f), $"Step -{_checkAngleDegrees:0.#}");
        }
#endif
    }
}

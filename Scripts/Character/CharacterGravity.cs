using UnityEngine;

namespace PhysicsCharacterController
{
    /// <summary>
    /// Applies gravity to the character, boosted while sliding on changing ground or standing on
    /// unclimbable slopes. Boosts only apply near vertical rest: during jump lift-off they would
    /// eat launch velocity, and when walking off an edge the ground check still sees the cliff
    /// lip as a steep slope for several ticks, which would slam the character downward instead
    /// of letting it fall naturally.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CharacterGravity : MonoBehaviour
    {
        private const float BOOST_MAX_ASCENT_SPEED_METERS_PER_SECOND = 0.1f;
        private const float BOOST_MAX_FALL_SPEED_METERS_PER_SECOND = 1.5f;

        [Header("Gravity Settings")]
        [SerializeField] private float _gravityMultiplier = 6f;
        [Tooltip("Multiplier factor for gravity used on change of normal")]
        [SerializeField] private float _gravityMultiplierOnSlideChange = 3f;
        [Tooltip("Multiplier factor for gravity used on non climbable slope")]
        [SerializeField] private float _gravityMultiplierIfUnclimbableSlope = 30f;
        [SerializeField] private float _maxDownwardSpeed = 200f;

        [Header("Dependencies")]
        [SerializeField] private SlopeChecker _slopeChecker;
        [SerializeField] private StepChecker _stepChecker;
        [SerializeField] private Rigidbody _rigidbody;

        public float VerticalVelocity => _rigidbody.linearVelocity.y;

        public void ApplyGravity()
        {
            Vector3 gravity = CalculateBaseGravity();

            if (IsNearVerticalRest())
            {
                if (HasGroundNormalChanged())
                {
                    gravity *= _gravityMultiplierOnSlideChange;
                }

                if (IsOnUnclimbableSlope())
                {
                    gravity = CalculateUnclimbableSlopeGravity();
                }
            }

            if (_rigidbody.linearVelocity.y > -_maxDownwardSpeed)
            {
                _rigidbody.AddForce(gravity);
            }
        }

        private bool IsNearVerticalRest()
        {
            float verticalSpeedMetersPerSecond = _rigidbody.linearVelocity.y;

            return verticalSpeedMetersPerSecond < BOOST_MAX_ASCENT_SPEED_METERS_PER_SECOND
                   && verticalSpeedMetersPerSecond > -BOOST_MAX_FALL_SPEED_METERS_PER_SECOND;
        }

        private Vector3 CalculateBaseGravity()
        {
            bool shouldUseNormalGravity = _slopeChecker.IsSlidePreventionActive || _stepChecker.IsTouchingStep;

            if (shouldUseNormalGravity)
            {
                return _slopeChecker.GravityProjectedOnNormal * (_gravityMultiplier * -Physics.gravity.y);
            }

            return Vector3.down * (_gravityMultiplier * -Physics.gravity.y);
        }

        private bool HasGroundNormalChanged()
        {
            return !_stepChecker.IsTouchingStep
                   && !Mathf.Approximately(_slopeChecker.GroundNormal.y, 1)
                   && _slopeChecker.GroundNormal.y != 0
                   && _slopeChecker.IsTouchingSlope
                   && _slopeChecker.PreviousGroundNormal != _slopeChecker.GroundNormal;
        }

        private bool IsOnUnclimbableSlope()
        {
            return !Mathf.Approximately(_slopeChecker.GroundNormal.y, 1)
                   && _slopeChecker.GroundNormal.y != 0
                   && _slopeChecker.CurrentSurfaceAngle > _slopeChecker.MaxClimbableSlopeAngle
                   && !_stepChecker.IsTouchingStep;
        }

        private Vector3 CalculateUnclimbableSlopeGravity()
        {
            float surfaceAngle = _slopeChecker.CurrentSurfaceAngle;

            if (surfaceAngle is > 0f and <= 30f)
            {
                return Vector3.down * (_gravityMultiplierIfUnclimbableSlope * -Physics.gravity.y);
            }

            if (surfaceAngle is > 30f and <= 89f)
            {
                return Vector3.down * (_gravityMultiplierIfUnclimbableSlope * 0.5f * -Physics.gravity.y);
            }

            return Vector3.down * (_gravityMultiplier * -Physics.gravity.y);
        }
    }
}

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace PhysicsCharacterController
{
    // Must evaluate after GroundChecker and before consumers (jump, gravity, state machine),
    // otherwise they read slope state that is one physics tick stale on landing.
    [DefaultExecutionOrder(-10)]
    public class SlopeChecker : MonoBehaviour, IMovementModifier, IVelocityModifier
    {
        private const float DOT_THRESHOLD_MOVING_UP_SLOPE = 0.1f;

        [Header("Slope Settings")]
        [Range(1f, 89f)]
        [Tooltip("Maximum angle a slope can be for the player to walk up it")]
        [SerializeField] private float _maxClimbableSlopeAngle = 53.6f;

        [Tooltip("Animation curve for slope speed reduction based on angle (0-90 degrees)")]
        [SerializeField] private AnimationCurve _slopeSpeedCurve = AnimationCurve.Linear(0f, 1f, 90f, 0.3f);

        [Tooltip("If true, the player won't slide down on walkable slopes")]
        [SerializeField] private bool _preventSlideOnSlope;

        [Header("References")]
        [SerializeField] private GroundChecker _groundChecker;
        [SerializeField] private CharacterMove _characterMove;
        [SerializeField] private BaseCharacterInput _input;

        private float _currentSurfaceAngle;
        private bool _isTouchingSlope;
        private bool _isSlidePreventionActive;

        private Vector3 _groundNormal;
        private Vector3 _previousGroundNormal;
        private Vector3 _slopeAlignedForward;
        private Vector3 _gravityProjectedOnNormal;

        #region Properties - State

        public float CurrentSurfaceAngle => _currentSurfaceAngle;
        public float MaxClimbableSlopeAngle => _maxClimbableSlopeAngle;
        public bool IsTouchingSlope => _isTouchingSlope;

        /// <summary>
        /// Unlike IsUnclimbableSlope this survives going airborne, so consumers like coyote time
        /// can still know what surface the character just left.
        /// </summary>
        public bool WasLastGroundedSurfaceUnclimbable { get; private set; }

        public bool IsSlidePreventionActive => _isSlidePreventionActive;
        public Vector3 GroundNormal => _groundNormal;
        public Vector3 PreviousGroundNormal => _previousGroundNormal;

        #endregion

        #region Properties - Direction Vectors

        /// <summary>
        /// Forward direction aligned to the slope surface. Y component changes based on slope.
        /// Use this for movement to make the character follow the slope contour.
        /// </summary>
        public Vector3 SlopeAlignedForward => _slopeAlignedForward;

        /// <summary>
        /// The component of gravity that is perpendicular to the current surface.
        /// Used to pin the character to slopes without causing sliding.
        /// </summary>
        public Vector3 GravityProjectedOnNormal => _gravityProjectedOnNormal;

        #endregion

        private void Start()
        {
            _isSlidePreventionActive = _preventSlideOnSlope;
        }

        private void FixedUpdate()
        {
            CheckSlopeAndDirections(_input.HorizontalMoveDirection);
        }

        public Vector3 GetVelocityContribution(Vector3 currentVelocity, Vector3 desiredMovement)
        {
            if (!_isTouchingSlope)
            {
                return Vector3.zero;
            }

            // An unclimbable surface acts as a wall. WallChecker owns the push cancellation via
            // collision contacts; cancelling here as well would double up and reverse the push.
            if (IsUnclimbableSlope())
            {
                return Vector3.zero;
            }

            // Calculate what the velocity should be if aligned with the slope
            Vector3 slopeAlignedVelocity = _slopeAlignedForward * _characterMove.CommandedSpeedMetersPerSecond;

            // The contribution is the difference between slope-aligned and horizontal
            Vector3 contribution = slopeAlignedVelocity - desiredMovement;
            contribution.y = 0;

            return contribution;
        }

        public void CheckSlopeAndDirections(Vector3 horizontalInputForward)
        {
            _previousGroundNormal = _groundNormal;

            if (!_groundChecker.IsGrounded)
            {
                ResetToAirborneState(horizontalInputForward);
                return;
            }

            RaycastHit groundHit = _groundChecker.GroundHit;
            _groundNormal = groundHit.normal;
            bool isOnFlatGround = Mathf.Approximately(_groundNormal.y, 1f);

            if (isOnFlatGround)
            {
                ApplyFlatGroundState(horizontalInputForward);
            }
            else
            {
                ApplySlopeState(groundHit, horizontalInputForward);
            }

            _gravityProjectedOnNormal = Vector3.Project(Vector3.down, _groundNormal);
        }

        private void ApplyFlatGroundState(Vector3 horizontalInputForward)
        {
            _slopeAlignedForward = horizontalInputForward;

            _isSlidePreventionActive = _preventSlideOnSlope;

            _currentSurfaceAngle = 0f;
            _isTouchingSlope = false;
            WasLastGroundedSurfaceUnclimbable = false;
        }

        private void ApplySlopeState(RaycastHit groundHit, Vector3 horizontalInputForward)
        {
            _currentSurfaceAngle = Vector3.Angle(Vector3.up, groundHit.normal);
            _isTouchingSlope = true;
            WasLastGroundedSurfaceUnclimbable = IsUnclimbableSlope();

            _slopeAlignedForward = Vector3.ProjectOnPlane(horizontalInputForward, groundHit.normal).normalized;

            UpdateSlidePreventionState();
        }

        private void UpdateSlidePreventionState()
        {
            bool isOnWalkableSlope = !IsUnclimbableSlope();
            if (isOnWalkableSlope)
            {
                _isSlidePreventionActive = _preventSlideOnSlope;
            }
            else
            {
                _isSlidePreventionActive = false;
            }
        }

        private void ResetToAirborneState(Vector3 horizontalInputForward)
        {
            _groundNormal = Vector3.zero;

            _slopeAlignedForward = horizontalInputForward;

            _gravityProjectedOnNormal = Vector3.down;

            _isSlidePreventionActive = _preventSlideOnSlope;

            _currentSurfaceAngle = 0f;
            _isTouchingSlope = false;
        }

        public float GetSpeedMultiplier(Vector3 intendedDirection)
        {
            if (!_isTouchingSlope)
            {
                return 1f;
            }

            bool isNotMovingUpSlope = !IsMovingUpSlope(intendedDirection);
            if (isNotMovingUpSlope)
            {
                return 1f;
            }

            if (IsUnclimbableSlope())
            {
                return 0f;
            }

            return _slopeSpeedCurve.Evaluate(_currentSurfaceAngle);
        }

        private bool IsMovingUpSlope(Vector3 intendedDirection)
        {
            Vector3 horizontalIntendedDirection = Vector3.ProjectOnPlane(intendedDirection, Vector3.up).normalized;
            Vector3 horizontalSlopeDownDirection = Vector3.ProjectOnPlane(-_groundNormal, Vector3.up).normalized;
            float dotProduct = Vector3.Dot(horizontalIntendedDirection, horizontalSlopeDownDirection);

            return dotProduct > DOT_THRESHOLD_MOVING_UP_SLOPE;
        }

        public bool IsUnclimbableSlope()
        {
            return _currentSurfaceAngle > _maxClimbableSlopeAngle;
        }

        /// <summary>
        /// Stateless variant for callers that need a fresh answer about a specific surface,
        /// e.g. jump gates on the landing tick, when this checker's cached state can be one
        /// tick stale.
        /// </summary>
        public bool IsSurfaceUnclimbable(Vector3 surfaceNormal)
        {
            return Vector3.Angle(Vector3.up, surfaceNormal) > _maxClimbableSlopeAngle;
        }

#if UNITY_EDITOR
        [Header("Debug")]
        [SerializeField] private bool _debug = true;

        private void OnDrawGizmosSelected()
        {
            if (!_debug) return;

            DrawDirectionVectors();
        }

        private void DrawDirectionVectors()
        {
            if (_slopeAlignedForward == Vector3.zero || _gravityProjectedOnNormal == Vector3.zero)
            {
                return;
            }

            Vector3 slopeDirectionEnd = transform.position + _slopeAlignedForward * 2f;
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, slopeDirectionEnd);
            Handles.Label(slopeDirectionEnd, "Slope Direction");

            Vector3 gravityProjectionEnd = transform.position + _gravityProjectedOnNormal * 2f;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, gravityProjectionEnd);
            Handles.Label(gravityProjectionEnd, "Gravity Projection");
        }
#endif
    }
}

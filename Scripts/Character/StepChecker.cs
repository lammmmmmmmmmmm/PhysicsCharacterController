using UnityEngine;
using UnityEngine.Serialization;

namespace PhysicsCharacterController
{
    /// <summary>Tests the actual collider along a step path before moving the rigidbody onto its landing surface.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class StepChecker : MonoBehaviour, IMovementStepResolver, IStepCollisionQuery
    {
        private const int MAX_HIT_COUNT = 32;
        private const float PENETRATION_TOLERANCE_METERS = 0.0001f;

        [Header("Configuration")]
        [SerializeField] private LayerMask _groundMask = Physics.DefaultRaycastLayers;
        [Tooltip("Maximum ledge height in meters. Full blocks must remain above this limit.")]
        [FormerlySerializedAs("_maxStepHeight")]
        [SerializeField, Min(0f)] private float _maxStepHeightMeters = 0.6f;
        [Tooltip("Clearance in meters maintained along the tested movement path.")]
        [SerializeField, Min(0.001f)] private float _skinWidthMeters = 0.01f;

        [Header("References")]
        [SerializeField] private GroundChecker _groundChecker;
        [SerializeField] private CharacterJump _characterJump;

        private readonly RaycastHit[] _castHits = new RaycastHit[MAX_HIT_COUNT];
        private readonly Collider[] _overlaps = new Collider[MAX_HIT_COUNT];
        private CharacterColliderShape _colliderShape;
        private Rigidbody _rigidbody;
        private SlopeChecker _slopeChecker;
        private StepMovementSolver _solver;
        private float _lastStepFixedTimeSeconds = float.NegativeInfinity;

        public bool IsTouchingStep => isActiveAndEnabled && Time.fixedTime - _lastStepFixedTimeSeconds < Time.fixedDeltaTime * 1.5f;
        public float MaxStepHeightMeters => _maxStepHeightMeters;

        #region Unity Lifecycle

        private void Awake()
        {
            _colliderShape = GetComponent<CharacterColliderShape>();
            _rigidbody = GetComponent<Rigidbody>();
            _slopeChecker = GetComponent<SlopeChecker>();
            _solver = new StepMovementSolver(this);
        }

        private void OnDisable()
        {
            _lastStepFixedTimeSeconds = float.NegativeInfinity;
        }

        #endregion

        #region Public Methods

        public bool TryApplyStep(Vector3 velocityMetersPerSecond, float deltaTimeSeconds)
        {
            _lastStepFixedTimeSeconds = float.NegativeInfinity;
            if (!isActiveAndEnabled || !_colliderShape.IsPhysicsEnabled || deltaTimeSeconds <= 0f ||
                (_characterJump.IsJumpInProgress && velocityMetersPerSecond.y >= 0f))
            {
                return false;
            }

            float minGroundNormalY = Mathf.Cos(_slopeChecker.MaxClimbableSlopeAngle * Mathf.Deg2Rad);
            if (!_solver.TryFindStep(velocityMetersPerSecond * deltaTimeSeconds, _groundChecker.IsGrounded,
                    _maxStepHeightMeters, _skinWidthMeters, minGroundNormalY, out Vector3 displacementMeters))
            {
                return false;
            }

            // The complete tick's displacement is consumed here. Physics must not apply it a second time.
            _rigidbody.position += displacementMeters;
            _rigidbody.linearVelocity = Vector3.zero;
            _lastStepFixedTimeSeconds = Time.fixedTime;
            return true;
        }

        public bool TryCast(Vector3 offsetMeters, Vector3 direction, float distanceMeters, out float hitDistanceMeters, out Vector3 normal)
        {
            GetColliderPose(out Vector3 positionMeters, out Quaternion rotation);
            float insetMeters = direction.y < -0.99f ? 0f : _skinWidthMeters * 0.5f;
            int hitCount = _colliderShape.CastAtPoseNonAlloc(
                positionMeters + offsetMeters, rotation, direction, distanceMeters,
                _castHits, _groundMask, QueryTriggerInteraction.Ignore, insetMeters);
            hitDistanceMeters = float.PositiveInfinity;
            normal = Vector3.zero;
            if (hitCount == _castHits.Length)
            {
                // A saturated buffer cannot prove clearance.
                hitDistanceMeters = 0f;
                return true;
            }

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                RaycastHit hit = _castHits[hitIndex];
                if (hit.collider.attachedRigidbody == _rigidbody || hit.distance >= hitDistanceMeters)
                {
                    continue;
                }

                hitDistanceMeters = hit.distance;
                normal = FindSurfaceNormal(hit, direction);
            }

            return hitDistanceMeters < float.PositiveInfinity;
        }

        public bool CanOccupy(Vector3 offsetMeters)
        {
            Collider characterCollider = _colliderShape.PhysicsCollider;
            GetColliderPose(out Vector3 positionMeters, out Quaternion rotation);
            positionMeters += offsetMeters;
            int overlapCount = _colliderShape.OverlapAtPoseNonAlloc(
                positionMeters, rotation, _overlaps, _groundMask, QueryTriggerInteraction.Ignore);
            if (overlapCount == _overlaps.Length)
            {
                return false;
            }

            for (int overlapIndex = 0; overlapIndex < overlapCount; overlapIndex++)
            {
                Collider obstacle = _overlaps[overlapIndex];
                if (obstacle.attachedRigidbody == _rigidbody)
                {
                    continue;
                }

                if (Physics.ComputePenetration(characterCollider, positionMeters, rotation,
                        obstacle, obstacle.transform.position, obstacle.transform.rotation, out _, out float penetrationMeters) &&
                    penetrationMeters > PENETRATION_TOLERANCE_METERS)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Private Methods

        private Vector3 FindSurfaceNormal(RaycastHit hit, Vector3 castDirection)
        {
            if (castDirection.y > -0.99f || hit.distance <= 0f || hit.normal.y <= 0f)
            {
                return hit.normal;
            }

            // Capsule sweeps report a rounded edge normal. Verify the actual tread beneath the contact
            // so a short movement tick can step onto it without first forcing the capsule into the riser.
            Vector3 horizontalNormal = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
            Vector3 rayOriginMeters = hit.point - horizontalNormal * _skinWidthMeters + Vector3.up * (_skinWidthMeters * 2f);
            Ray surfaceRay = new(rayOriginMeters, Vector3.down);
            return hit.collider.Raycast(surfaceRay, out RaycastHit surfaceHit, _skinWidthMeters * 4f)
                ? surfaceHit.normal
                : hit.normal;
        }

        private void GetColliderPose(out Vector3 positionMeters, out Quaternion rotation)
        {
            // Interpolated transforms can lag behind the physics pose, including immediately after a step.
            Transform colliderTransform = _colliderShape.PhysicsCollider.transform;
            Quaternion rotationDelta = _rigidbody.rotation * Quaternion.Inverse(_rigidbody.transform.rotation);
            positionMeters = _rigidbody.position + rotationDelta * (colliderTransform.position - _rigidbody.transform.position);
            rotation = rotationDelta * colliderTransform.rotation;
        }

        #endregion
    }
}

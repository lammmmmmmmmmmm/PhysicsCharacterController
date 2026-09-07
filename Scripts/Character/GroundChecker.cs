#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace PhysicsCharacterController
{
    /// <summary>
    /// Detects ground beneath any supported character collider shape. A sphere cast provides broad
    /// slope and edge contact. If that cast starts inside a wall belonging to the same concave mesh
    /// as the floor, Unity reports only an invalid zero-distance overlap for that collider; a center
    /// ray then recovers the floor surface and its normal for SlopeChecker.
    /// </summary>
    // Must evaluate before SlopeChecker and all other consumers each physics tick.
    [DefaultExecutionOrder(-20)]
    [RequireComponent(typeof(Rigidbody))]
    public class GroundChecker : MonoBehaviour
    {
        private const int MAX_GROUND_HIT_COUNT = 16;
        private const float MIN_CHECK_RADIUS_METERS = 0.01f;
        private const float MIN_GROUND_TOLERANCE_METERS = 0.001f;

        [SerializeField] private LayerMask _groundMask;
        [SerializeField, Min(MIN_CHECK_RADIUS_METERS)] private float _checkRadiusMeters = 0.2f;
        [SerializeField, Min(MIN_GROUND_TOLERANCE_METERS)] private float _groundToleranceMeters = 0.1f;

        private readonly RaycastHit[] _groundHits = new RaycastHit[MAX_GROUND_HIT_COUNT];
        private CharacterColliderShape _characterColliderShape;
        private Rigidbody _characterRigidbody;
        private bool _wasGrounded;
        private bool _isGrounded;
        private RaycastHit _groundHit;

        public bool IsGrounded => _isGrounded;
        public bool WasGrounded => _wasGrounded;
        public bool JustLanded => _isGrounded && !_wasGrounded;
        public bool JustLeftGround => !_isGrounded && _wasGrounded;
        public RaycastHit GroundHit => _groundHit;

        #region Unity Lifecycle

        private void Awake()
        {
            _characterColliderShape = GetComponent<CharacterColliderShape>();
            _characterRigidbody = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            Check();
        }

        private void OnDrawGizmosSelected()
        {
            CharacterColliderShape characterColliderShape = GetComponent<CharacterColliderShape>();
            characterColliderShape.RefreshColliderCache();

            Vector3 castOrigin = GetCastOrigin(characterColliderShape);
            float castDistanceMeters = GetSphereCastDistanceMeters(characterColliderShape);
            Vector3 castEnd = castOrigin + Vector3.down * castDistanceMeters;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(castOrigin, _checkRadiusMeters);

            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(castEnd, _checkRadiusMeters);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(castOrigin, castEnd);

#if UNITY_EDITOR
            Handles.Label(castOrigin, "Ground Check Start");
            Handles.Label(castEnd, "Ground Check End");
#endif
        }

        #endregion

        #region Private Methods

        private void Check()
        {
            _wasGrounded = _isGrounded;

            Vector3 castOrigin = GetCastOrigin(_characterColliderShape);
            float castDistanceMeters = GetSphereCastDistanceMeters(_characterColliderShape);
            int hitCount = Physics.SphereCastNonAlloc(
                castOrigin,
                _checkRadiusMeters,
                Vector3.down,
                _groundHits,
                castDistanceMeters,
                _groundMask,
                QueryTriggerInteraction.Ignore);

            _isGrounded = TrySelectGroundHit(hitCount, out _groundHit);
            if (_isGrounded)
            {
                return;
            }

            CheckGroundWithCenterRay(castOrigin);
        }

        private Vector3 GetCastOrigin(CharacterColliderShape characterColliderShape)
        {
            Vector3 colliderCenter = characterColliderShape.Center;
            Vector3 localCastOrigin = new(colliderCenter.x, 0f, colliderCenter.z);
            return transform.TransformPoint(localCastOrigin);
        }

        private float GetSphereCastDistanceMeters(CharacterColliderShape characterColliderShape)
        {
            float feetOffsetMeters = characterColliderShape.FeetOffsetMeters * Mathf.Abs(transform.lossyScale.y);
            return Mathf.Max(0f, feetOffsetMeters - _checkRadiusMeters + _groundToleranceMeters);
        }

        private void CheckGroundWithCenterRay(Vector3 castOrigin)
        {
            float feetOffsetMeters = _characterColliderShape.FeetOffsetMeters * Mathf.Abs(transform.lossyScale.y);
            float rayDistanceMeters = feetOffsetMeters + _groundToleranceMeters;
            int hitCount = Physics.RaycastNonAlloc(
                castOrigin,
                Vector3.down,
                _groundHits,
                rayDistanceMeters,
                _groundMask,
                QueryTriggerInteraction.Ignore);

            _isGrounded = TrySelectGroundHit(hitCount, out _groundHit);
        }

        private bool TrySelectGroundHit(int hitCount, out RaycastHit nearestGroundHit)
        {
            nearestGroundHit = default;
            float nearestDistanceMeters = float.PositiveInfinity;

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                RaycastHit candidateHit = _groundHits[hitIndex];
                if (candidateHit.distance <= 0f ||
                    candidateHit.collider.attachedRigidbody == _characterRigidbody ||
                    Vector3.Dot(candidateHit.normal, Vector3.up) <= 0f ||
                    candidateHit.distance >= nearestDistanceMeters)
                {
                    continue;
                }

                nearestGroundHit = candidateHit;
                nearestDistanceMeters = candidateHit.distance;
            }

            return nearestDistanceMeters < float.PositiveInfinity;
        }

        #endregion
    }
}

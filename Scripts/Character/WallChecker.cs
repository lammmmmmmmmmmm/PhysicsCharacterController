using UnityEngine;

namespace PhysicsCharacterController
{
    /// <summary>
    /// Detects walls (including surfaces too steep to walk) and cancels the velocity component
    /// pushing into them, so the character slides along instead of ramming the physics solver —
    /// ramming reads as shaking, and on inclined faces the solver converts the push into an
    /// unintended climb. Detection combines proactive raycasts with the physics engine's own
    /// collision contacts: rays cancel when the requested movement reaches the body-to-wall gap, while contacts are authoritative
    /// whenever the capsule actually presses a surface, at any angle, grounded or airborne.
    /// </summary>
    public class WallChecker : MonoBehaviour, IVelocityModifier, ICharacterPhysicsSensor
    {
        // Contacts pointing meaningfully downward are ceilings, not walls.
        private const float MIN_WALL_CONTACT_NORMAL_Y = -0.1f;

        [Tooltip("Distance from the player head used to check if the player is touching a wall")]
        [SerializeField] private float _checkDistance = 0.8f;
        [Tooltip("Heights above the character center to cast wall rays. Near-vertical walls lean away, so their distance grows with height — a single high ray misses them while the capsule is already touching lower down")]
        [SerializeField] private float[] _checkHeightOffsetsMeters = { 0f, 0.5f };
        [SerializeField] private LayerMask _wallMask;
        [SerializeField] private BaseCharacterInput _input;
        [Tooltip("Source of the max climbable angle: surfaces steeper than it count as walls")]
        [SerializeField] private SlopeChecker _slopeChecker;
        [Tooltip("Seconds between proactive wall-ray samples. Zero samples every physics step; contact handling always runs every step.")]
        [Min(0f)] [SerializeField] private float _checkIntervalSeconds;
#if UNITY_EDITOR
        [Header("Debug")]
        [SerializeField] private bool _debug;
        [SerializeField] private Vector3 _debugForwardDirection = Vector3.forward;
#endif

        private readonly WallCheckSampling _sampling = new();
        private readonly Vector3[] _checkDirections = new Vector3[WallCheckSampling.DIRECTION_COUNT];

        public bool IsTouchingWall { get; private set; }
        public Vector3 WallNormal { get; private set; }

        // How long a steep contact keeps counting as a wall after the capsule separates. Without
        // this memory, cancelling the push breaks the contact, the flag drops, the push resumes,
        // and the push/cancel oscillation ratchets the character up the face every other tick.
        private const float WALL_CONTACT_MEMORY_SECONDS = 0.25f;

        private bool _hasSteepContact;
        private Vector3 _steepContactNormal;
        private Vector3 _steepContactPointMeters;
        private CharacterColliderShape _colliderShape;
        private Rigidbody _rigidbody;
        private float _secondsSinceSteepContact = float.PositiveInfinity;
        private bool _hasProactiveWall;
        private Vector3 _proactiveWallNormal;
        private Vector3 _proactiveWallPointMeters;
        private bool _isSteepContactRemembered;
        private float _simulationCheckIntervalSeconds;

        #region Unity Lifecycle

        private void Awake()
        {
            _colliderShape = GetComponent<CharacterColliderShape>();
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            _sampling.Reset();
            _hasProactiveWall = false;
            _hasSteepContact = false;
            _isSteepContactRemembered = false;
            _secondsSinceSteepContact = float.PositiveInfinity;
            IsTouchingWall = false;
            WallNormal = Vector3.zero;
        }

        private void FixedUpdate()
        {
            Vector3 forwardDirection = _input.HorizontalMoveDirection;
            float intervalSeconds = Mathf.Max(_checkIntervalSeconds, _simulationCheckIntervalSeconds);
            if (_sampling.AdvanceAndConsumeSample(Time.fixedDeltaTime, intervalSeconds, forwardDirection))
                CheckWall(forwardDirection);

            IsTouchingWall = _hasProactiveWall;
            WallNormal = _proactiveWallNormal;

            if (_hasSteepContact)
            {
                _secondsSinceSteepContact = 0f;
            }
            else
            {
                _secondsSinceSteepContact += Time.fixedDeltaTime;
            }

            // Interpolation and solver contact slop must not preserve the riser after the feet clear it.
            float physicsFeetYMeters = _colliderShape.CalculateWorldBounds().min.y +
                _rigidbody.position.y - _rigidbody.transform.position.y;
            float minimumWallContactYMeters = physicsFeetYMeters + _colliderShape.PhysicsCollider.contactOffset;
            if (_steepContactPointMeters.y <= minimumWallContactYMeters)
            {
                _secondsSinceSteepContact = float.PositiveInfinity;
            }

            _isSteepContactRemembered = _secondsSinceSteepContact <= WALL_CONTACT_MEMORY_SECONDS;
            if (_isSteepContactRemembered)
            {
                IsTouchingWall = true;
                WallNormal = _steepContactNormal;
            }

            // Contact callbacks fire after this tick's simulation, repopulating for the next one.
            _hasSteepContact = false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            RecordSteepContact(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            RecordSteepContact(collision);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_debug) return;

            Gizmos.color = Color.black;
            WallCheckSampling.FillDirections(_debugForwardDirection, transform.up, _checkDirections);
            foreach (float heightOffsetMeters in _checkHeightOffsetsMeters)
            {
                Vector3 checkOrigin = transform.position + Vector3.up * heightOffsetMeters;
                foreach (Vector3 checkDirection in _checkDirections)
                    Gizmos.DrawLine(checkOrigin, checkOrigin + checkDirection * _checkDistance);
            }
        }
#endif

        #endregion

        #region Public Methods

        public void SetSimulationCheckIntervalSeconds(float intervalSeconds)
        {
            _simulationCheckIntervalSeconds = Mathf.Max(0f, intervalSeconds);
        }

        public Vector3 GetVelocityContribution(Vector3 currentVelocity, Vector3 desiredMovement)
        {
            if (!IsTouchingWall || (!_isSteepContactRemembered && !WillReachProactiveWall(desiredMovement)))
            {
                return Vector3.zero;
            }

            return SurfaceVelocityCancellation.CalculateCancellation(desiredMovement, WallNormal);
        }

        #endregion

        #region Private Methods

        private bool WillReachProactiveWall(Vector3 desiredVelocityMetersPerSecond)
        {
            // Detection range must not create a dead zone outside the per-tick step sweep.
            Vector3 nearestBodyPointMeters = _colliderShape.PhysicsCollider.ClosestPoint(_proactiveWallPointMeters);
            float gapMeters = Vector3.Dot(nearestBodyPointMeters - _proactiveWallPointMeters, _proactiveWallNormal);
            float requestedTravelIntoWallMeters = -Vector3.Dot(
                desiredVelocityMetersPerSecond * Time.fixedDeltaTime, _proactiveWallNormal);
            return gapMeters <= Mathf.Max(0f, requestedTravelIntoWallMeters);
        }

        private void CheckWall(Vector3 forwardDirection)
        {
            _hasProactiveWall = false;
            _proactiveWallNormal = Vector3.zero;
            if (forwardDirection.sqrMagnitude <= Mathf.Epsilon)
                return;

            Vector3 positionMeters = transform.position;
            WallCheckSampling.FillDirections(forwardDirection, transform.up, _checkDirections);

            foreach (float heightOffsetMeters in _checkHeightOffsetsMeters)
            {
                Vector3 checkOrigin = positionMeters + Vector3.up * heightOffsetMeters;

                foreach (Vector3 checkDirection in _checkDirections)
                {
                    if (Physics.Raycast(checkOrigin, checkDirection, out RaycastHit hit, _checkDistance, _wallMask, QueryTriggerInteraction.Ignore)
                        && IsTooSteepToWalk(hit.normal))
                    {
                        _proactiveWallNormal = hit.normal;
                        _proactiveWallPointMeters = hit.point;
                        _hasProactiveWall = true;
                        return;
                    }
                }
            }
        }

        private void RecordSteepContact(Collision collision)
        {
            if (!IsInWallMask(collision.gameObject.layer))
            {
                return;
            }

            for (int contactIndex = 0; contactIndex < collision.contactCount; contactIndex++)
            {
                Vector3 contactNormal = collision.GetContact(contactIndex).normal;

                bool isCeiling = contactNormal.y < MIN_WALL_CONTACT_NORMAL_Y;
                if (isCeiling || !IsTooSteepToWalk(contactNormal))
                {
                    continue;
                }

                _hasSteepContact = true;
                _steepContactNormal = contactNormal;
                _steepContactPointMeters = collision.GetContact(contactIndex).point;
                return;
            }
        }

        private bool IsInWallMask(int layer)
        {
            return (_wallMask.value & (1 << layer)) != 0;
        }

        private bool IsTooSteepToWalk(Vector3 surfaceNormal)
        {
            return _slopeChecker.IsSurfaceUnclimbable(surfaceNormal);
        }

        #endregion

    }
}

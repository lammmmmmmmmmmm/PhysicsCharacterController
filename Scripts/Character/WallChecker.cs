using UnityEngine;

namespace PhysicsCharacterController
{
    /// <summary>
    /// Detects walls (including surfaces too steep to walk) and cancels the velocity component
    /// pushing into them, so the character slides along instead of ramming the physics solver —
    /// ramming reads as shaking, and on inclined faces the solver converts the push into an
    /// unintended climb. Detection combines proactive raycasts with the physics engine's own
    /// collision contacts: rays cancel slightly before touching, while contacts are authoritative
    /// whenever the capsule actually presses a surface, at any angle, grounded or airborne.
    /// </summary>
    public class WallChecker : MonoBehaviour, IVelocityModifier
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
        private float _secondsSinceSteepContact = float.PositiveInfinity;
        private bool _hasProactiveWall;
        private Vector3 _proactiveWallNormal;

        #region Unity Lifecycle

        private void OnEnable()
        {
            _sampling.Reset();
            _hasProactiveWall = false;
            _hasSteepContact = false;
            _secondsSinceSteepContact = float.PositiveInfinity;
            IsTouchingWall = false;
            WallNormal = Vector3.zero;
        }

        private void FixedUpdate()
        {
            Vector3 forwardDirection = _input.HorizontalMoveDirection;
            if (_sampling.AdvanceAndConsumeSample(Time.fixedDeltaTime, _checkIntervalSeconds, forwardDirection))
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

            bool isSteepContactRemembered = _secondsSinceSteepContact <= WALL_CONTACT_MEMORY_SECONDS;
            if (!IsTouchingWall && isSteepContactRemembered)
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

        public Vector3 GetVelocityContribution(Vector3 currentVelocity, Vector3 desiredMovement)
        {
            if (!IsTouchingWall)
            {
                return Vector3.zero;
            }

            return SurfaceVelocityCancellation.CalculateCancellation(desiredMovement, WallNormal);
        }

        #endregion

        #region Private Methods

        private void CheckWall(Vector3 forwardDirection)
        {
            _hasProactiveWall = false;
            _proactiveWallNormal = Vector3.zero;
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

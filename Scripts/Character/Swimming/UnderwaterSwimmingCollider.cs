using UnityEngine;

namespace PhysicsCharacterController
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class UnderwaterSwimmingCollider : MonoBehaviour
    {
        private const int MAX_OVERLAP_COUNT = 32;
        private const int MAX_CAST_COUNT = 32;
        private const int MAX_RECOVERY_DIRECTION_COUNT = 64;
        private const int MAX_RECOVERY_CANDIDATE_COUNT = 64;

        [Header("Colliders")]
        [SerializeField] private CharacterColliderShape _uprightCollider;
        [SerializeField] private Transform _underwaterColliderPivot;
        [SerializeField] private CharacterColliderShape _underwaterCollider;
        [SerializeField] private PhysicsMaterial _swimmingPhysicsMaterial;

        [Header("Collision")]
        [SerializeField] private LayerMask _solidCollisionMask = ~0;

        [Header("Dependencies")]
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private SwimmingMovementSettingsSO _settingsSO;

        private readonly Collider[] _overlapResults = new Collider[MAX_OVERLAP_COUNT];
        private readonly RaycastHit[] _castResults = new RaycastHit[MAX_CAST_COUNT];
        private readonly Vector3[] _penetrationDisplacements = new Vector3[MAX_OVERLAP_COUNT];
        private readonly Vector3[] _recoverySearchDirections = new Vector3[MAX_RECOVERY_DIRECTION_COUNT];
        private readonly Vector3[] _recoveryCandidateRootPositions = new Vector3[MAX_RECOVERY_CANDIDATE_COUNT];
        private readonly SwimmingColliderRotationSolver _rotationSolver = new();
        private readonly SwimmingExitRecoverySolver _exitRecoverySolver = new();

        private Vector3 _recoveryTargetRootPosition;
        private Quaternion _defaultLocalRotation;
        private bool _isReverseDivePitchAscentActive;

        public bool IsActive => _underwaterCollider.IsPhysicsEnabled;
        public Vector3 AcceptedDirection => _underwaterColliderPivot.forward;
        public Quaternion AcceptedRotation => _underwaterColliderPivot.rotation;
        public bool IsTerrestrialExitRecoveryActive { get; private set; }
        public Vector3 TerrestrialExitRecoveryDirection { get; private set; }

        #region Unity Lifecycle

        private void Awake()
        {
            _defaultLocalRotation = _underwaterColliderPivot.localRotation;
            _underwaterCollider.PhysicsCollider.sharedMaterial = _swimmingPhysicsMaterial;
            _underwaterCollider.SetPhysicsEnabled(false);
            _uprightCollider.SetPhysicsEnabled(true);
        }

        #endregion

        #region Public Methods

        public bool TryActivate(Vector3 worldDirection)
        {
            CancelTerrestrialExitRecovery();
            _isReverseDivePitchAscentActive = false;
            Quaternion candidateRotation = _underwaterColliderPivot.rotation;
            if (worldDirection.sqrMagnitude > Mathf.Epsilon)
            {
                candidateRotation = _rotationSolver.CalculateDirectionalTransitionTargetRotation(
                    worldDirection,
                    _underwaterColliderPivot.up,
                    _rigidbody.rotation * Vector3.forward);
            }

            if (!IsColliderPoseClear(_underwaterCollider, candidateRotation))
            {
                return false;
            }

            _underwaterColliderPivot.rotation = candidateRotation;
            _uprightCollider.SetPhysicsEnabled(false);
            _underwaterCollider.SetPhysicsEnabled(true);
            return true;
        }

        public bool TryAlign(Vector3 worldDirection, float fixedDeltaTime, float rotationSpeedDegreesPerSecond)
        {
            if (!IsActive || worldDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return false;
            }

            UpdateReverseDivePitchAscentState(worldDirection);
            float maximumRotationDegrees = rotationSpeedDegreesPerSecond * fixedDeltaTime;
            Quaternion candidateRotation = _isReverseDivePitchAscentActive
                ? _rotationSolver.CalculateReverseDivePitchAscentStep(
                    _underwaterColliderPivot.rotation,
                    _rigidbody.rotation * Vector3.right,
                    maximumRotationDegrees)
                : CalculateStandardAlignmentStep(worldDirection, maximumRotationDegrees);

            if (!IsColliderPoseClear(_underwaterCollider, candidateRotation))
            {
                return false;
            }

            _underwaterColliderPivot.rotation = candidateRotation;
            if (_isReverseDivePitchAscentActive && _rotationSolver.IsVerticalAscent(AcceptedDirection))
            {
                _isReverseDivePitchAscentActive = false;
            }

            return true;
        }

        public bool TryDeactivate()
        {
            if (!IsActive)
            {
                ResetInactiveRotation();
                CancelTerrestrialExitRecovery();
                return true;
            }

            if (!IsColliderPoseClear(_uprightCollider))
            {
                return false;
            }

            _underwaterCollider.SetPhysicsEnabled(false);
            _uprightCollider.SetPhysicsEnabled(true);
            ResetInactiveRotation();
            CancelTerrestrialExitRecovery();
            return true;
        }

        public bool TryBeginTerrestrialExitRecovery()
        {
            if (!IsActive)
            {
                Debug.LogWarning(
                    $"Cannot begin swimming exit recovery for '{name}' because the underwater collider is inactive.",
                    this);
                CancelTerrestrialExitRecovery();
                return false;
            }

            if (!TryFindTerrestrialExitRecoveryTarget(out Vector3 targetRootPosition))
            {
                CancelTerrestrialExitRecovery();
                return false;
            }

            _recoveryTargetRootPosition = targetRootPosition;
            TerrestrialExitRecoveryDirection = (targetRootPosition - _rigidbody.position).normalized;
            IsTerrestrialExitRecoveryActive = true;
            return true;
        }

        public bool AdvanceTerrestrialExitRecovery(float fixedDeltaTime)
        {
            if (!IsTerrestrialExitRecoveryActive && !TryBeginTerrestrialExitRecovery())
            {
                return false;
            }

            if (!IsUprightPoseClearAtRootPosition(_recoveryTargetRootPosition)
                || !IsRecoveryPathClear(_recoveryTargetRootPosition))
            {
                CancelTerrestrialExitRecovery();
                if (!TryBeginTerrestrialExitRecovery())
                {
                    return false;
                }
            }

            Vector3 currentRootPosition = _rigidbody.position;
            Vector3 targetOffset = _recoveryTargetRootPosition - currentRootPosition;
            float remainingDistanceMeters = targetOffset.magnitude;
            if (remainingDistanceMeters <= Mathf.Epsilon)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                return true;
            }

            TerrestrialExitRecoveryDirection = targetOffset / remainingDistanceMeters;
            float stepDistanceMeters = Mathf.Min(_settingsSO.ExitRecoverySpeedMetersPerSecond * fixedDeltaTime, remainingDistanceMeters);
            Vector3 nextRootPosition = currentRootPosition + TerrestrialExitRecoveryDirection * stepDistanceMeters;

            if (!IsRecoveryPathClear(nextRootPosition))
            {
                CancelTerrestrialExitRecovery();
                return false;
            }

            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.MovePosition(nextRootPosition);
            return true;
        }

        public void CancelTerrestrialExitRecovery()
        {
            IsTerrestrialExitRecoveryActive = false;
            TerrestrialExitRecoveryDirection = Vector3.zero;
            _recoveryTargetRootPosition = Vector3.zero;
        }

        #endregion

        #region Private Methods

        private void ResetInactiveRotation()
        {
            _isReverseDivePitchAscentActive = false;
            _underwaterColliderPivot.localRotation = _defaultLocalRotation;
        }

        private void UpdateReverseDivePitchAscentState(Vector3 worldDirection)
        {
            if (!_rotationSolver.IsVerticalAscent(worldDirection))
            {
                _isReverseDivePitchAscentActive = false;
                return;
            }

            if (!_isReverseDivePitchAscentActive)
            {
                _isReverseDivePitchAscentActive = _rotationSolver.ShouldBeginReverseDivePitchAscent(
                    worldDirection,
                    AcceptedDirection);
            }
        }

        private Quaternion CalculateStandardAlignmentStep(Vector3 worldDirection, float maximumRotationDegrees)
        {
            Quaternion targetRotation = _rotationSolver.CalculateDirectionalTransitionTargetRotation(
                worldDirection,
                _underwaterColliderPivot.up,
                _rigidbody.rotation * Vector3.forward);
            return Quaternion.RotateTowards(
                _underwaterColliderPivot.rotation,
                targetRotation,
                maximumRotationDegrees);
        }

        private bool IsColliderPoseClear(CharacterColliderShape colliderShape)
        {
            int overlapCount = colliderShape.OverlapNonAlloc(_overlapResults, _solidCollisionMask, QueryTriggerInteraction.Ignore);
            return AreOverlapsClear(overlapCount);
        }

        private bool TryFindTerrestrialExitRecoveryTarget(out Vector3 targetRootPosition)
        {
            Vector3 currentRootPosition = _rigidbody.position;
            int recoveryCandidateCount = 0;

            TryAddDepenetratedCandidate(
                currentRootPosition,
                shouldCombinePenetrations: true,
                _recoveryCandidateRootPositions,
                ref recoveryCandidateCount);
            TryAddDepenetratedCandidate(
                currentRootPosition,
                shouldCombinePenetrations: false,
                _recoveryCandidateRootPositions,
                ref recoveryCandidateCount);

            int penetrationCount = CollectPenetrationDisplacementsAtRootPosition(currentRootPosition, out _);
            if (penetrationCount < 0)
            {
                targetRootPosition = currentRootPosition;
                return false;
            }

            int searchDirectionCount = _exitRecoverySolver.FillOrderedSearchDirections(
                _penetrationDisplacements,
                penetrationCount,
                AcceptedDirection,
                _settingsSO.ExitRecoveryHorizontalDirectionCount,
                _recoverySearchDirections);
            AddDirectionalRecoveryCandidates(
                currentRootPosition,
                searchDirectionCount,
                _recoveryCandidateRootPositions,
                ref recoveryCandidateCount);

            return _exitRecoverySolver.TrySelectNearestPosition(
                currentRootPosition,
                _recoveryCandidateRootPositions,
                recoveryCandidateCount,
                _settingsSO.ExitRecoveryMaximumDistanceMeters,
                out targetRootPosition);
        }

        private void TryAddDepenetratedCandidate(
            Vector3 currentRootPosition,
            bool shouldCombinePenetrations,
            Vector3[] candidateRootPositions,
            ref int candidateCount)
        {
            if (candidateCount >= candidateRootPositions.Length)
            {
                return;
            }

            if (!TryResolveUprightPenetration(
                    currentRootPosition,
                    shouldCombinePenetrations,
                    out Vector3 candidateRootPosition)
                || !IsRecoveryPathClear(candidateRootPosition))
            {
                return;
            }

            candidateRootPositions[candidateCount] = candidateRootPosition;
            candidateCount++;
        }

        private bool TryResolveUprightPenetration(Vector3 currentRootPosition, bool shouldCombinePenetrations, out Vector3 resolvedRootPosition)
        {
            resolvedRootPosition = currentRootPosition;
            for (int iterationIndex = 0;
                 iterationIndex < _settingsSO.ExitRecoveryMaximumDepenetrationIterations;
                 iterationIndex++)
            {
                int penetrationCount = CollectPenetrationDisplacementsAtRootPosition(resolvedRootPosition, out bool hasBlockingOverlap);
                if (penetrationCount < 0)
                {
                    return false;
                }

                if (penetrationCount == 0)
                {
                    return !hasBlockingOverlap && resolvedRootPosition != currentRootPosition;
                }

                Vector3 penetrationDisplacement = shouldCombinePenetrations
                    ? _exitRecoverySolver.CalculateCombinedDepenetration(_penetrationDisplacements, penetrationCount)
                    : _exitRecoverySolver.CalculateDeepestDepenetration(_penetrationDisplacements, penetrationCount);
                if (penetrationDisplacement.sqrMagnitude <= Mathf.Epsilon)
                {
                    return false;
                }

                resolvedRootPosition += penetrationDisplacement + penetrationDisplacement.normalized * _settingsSO.CollisionSweepSkinMeters;
                if ((resolvedRootPosition - currentRootPosition).sqrMagnitude
                    > _settingsSO.ExitRecoveryMaximumDistanceMeters
                    * _settingsSO.ExitRecoveryMaximumDistanceMeters)
                {
                    return false;
                }
            }

            return IsUprightPoseClearAtRootPosition(resolvedRootPosition) && resolvedRootPosition != currentRootPosition;
        }

        private void AddDirectionalRecoveryCandidates(
            Vector3 currentRootPosition,
            int searchDirectionCount,
            Vector3[] candidateRootPositions,
            ref int candidateCount)
        {
            float maximumDistanceMeters = _settingsSO.ExitRecoveryMaximumDistanceMeters;
            float probeIntervalMeters = Mathf.Min(_settingsSO.ExitRecoveryProbeIntervalMeters, maximumDistanceMeters);

            for (int directionIndex = 0;
                 directionIndex < searchDirectionCount && candidateCount < candidateRootPositions.Length;
                 directionIndex++)
            {
                Vector3 searchDirection = _recoverySearchDirections[directionIndex];
                for (float distanceMeters = probeIntervalMeters;
                     distanceMeters <= maximumDistanceMeters + Mathf.Epsilon;
                     distanceMeters += probeIntervalMeters)
                {
                    float clampedDistanceMeters = Mathf.Min(distanceMeters, maximumDistanceMeters);
                    Vector3 candidateRootPosition = currentRootPosition + searchDirection * clampedDistanceMeters;
                    if (!IsUprightPoseClearAtRootPosition(candidateRootPosition))
                    {
                        continue;
                    }

                    if (IsRecoveryPathClear(candidateRootPosition))
                    {
                        candidateRootPositions[candidateCount] = candidateRootPosition;
                        candidateCount++;
                    }

                    break;
                }
            }
        }

        private int CollectPenetrationDisplacementsAtRootPosition(Vector3 rootPosition, out bool hasBlockingOverlap)
        {
            CalculateColliderPoseAtRootPosition(
                _uprightCollider,
                rootPosition,
                out Vector3 colliderPosition,
                out Quaternion colliderRotation);
            int blockingOverlapCount = CollectBlockingOverlaps(
                _uprightCollider,
                colliderPosition,
                colliderRotation);
            hasBlockingOverlap = blockingOverlapCount > 0;
            if (blockingOverlapCount <= 0)
            {
                return blockingOverlapCount;
            }

            int penetrationCount = 0;
            Collider uprightPhysicsCollider = _uprightCollider.PhysicsCollider;
            bool wasUprightColliderTrigger = uprightPhysicsCollider.isTrigger;
            // ComputePenetration ignores disabled colliders. A temporary trigger probe keeps the
            // underwater collider as the only solid character shape during the synchronous query.
            uprightPhysicsCollider.isTrigger = true;
            _uprightCollider.SetPhysicsEnabled(true);
            for (int overlapIndex = 0; overlapIndex < blockingOverlapCount; overlapIndex++)
            {
                Collider obstacle = _overlapResults[overlapIndex];
                bool hasPenetration = Physics.ComputePenetration(
                    uprightPhysicsCollider,
                    colliderPosition,
                    colliderRotation,
                    obstacle,
                    obstacle.transform.position,
                    obstacle.transform.rotation,
                    out Vector3 depenetrationDirection,
                    out float depenetrationDistanceMeters);
                if (!hasPenetration || depenetrationDistanceMeters <= Mathf.Epsilon)
                {
                    continue;
                }

                _penetrationDisplacements[penetrationCount] = depenetrationDirection
                    * depenetrationDistanceMeters;
                penetrationCount++;
            }

            _uprightCollider.SetPhysicsEnabled(false);
            uprightPhysicsCollider.isTrigger = wasUprightColliderTrigger;

            return penetrationCount;
        }

        private bool IsUprightPoseClearAtRootPosition(Vector3 rootPosition)
        {
            CalculateColliderPoseAtRootPosition(
                _uprightCollider,
                rootPosition,
                out Vector3 colliderPosition,
                out Quaternion colliderRotation);
            return CollectBlockingOverlaps(
                _uprightCollider,
                colliderPosition,
                colliderRotation) == 0;
        }

        private int CollectBlockingOverlaps(
            CharacterColliderShape colliderShape,
            Vector3 colliderPosition,
            Quaternion colliderRotation)
        {
            int overlapCount = colliderShape.OverlapAtPoseNonAlloc(
                colliderPosition,
                colliderRotation,
                _overlapResults,
                _solidCollisionMask,
                QueryTriggerInteraction.Ignore);
            if (overlapCount == MAX_OVERLAP_COUNT)
            {
                Debug.LogWarning($"Swimming exit recovery for '{name}' filled the overlap buffer; no recovery pose will be accepted.", this);
                return -1;
            }

            int blockingOverlapCount = 0;
            for (int overlapIndex = 0; overlapIndex < overlapCount; overlapIndex++)
            {
                Collider overlap = _overlapResults[overlapIndex];
                if (overlap.attachedRigidbody == _rigidbody)
                {
                    continue;
                }

                _overlapResults[blockingOverlapCount] = overlap;
                blockingOverlapCount++;
            }

            return blockingOverlapCount;
        }

        private bool IsRecoveryPathClear(Vector3 targetRootPosition)
        {
            Vector3 rootDisplacement = targetRootPosition - _rigidbody.position;
            float distanceMeters = rootDisplacement.magnitude;
            if (distanceMeters <= Mathf.Epsilon)
            {
                return true;
            }

            Vector3 worldDirection = rootDisplacement / distanceMeters;
            int castCount = _underwaterCollider.CastAtPoseNonAlloc(
                _underwaterCollider.PhysicsCollider.transform.position,
                _underwaterCollider.PhysicsCollider.transform.rotation,
                worldDirection,
                distanceMeters + _settingsSO.CollisionSweepSkinMeters,
                _castResults,
                _solidCollisionMask,
                QueryTriggerInteraction.Ignore);
            if (castCount == MAX_CAST_COUNT)
            {
                Debug.LogWarning($"Swimming exit recovery for '{name}' filled the cast buffer; the recovery path is treated as blocked.", this);
                return false;
            }

            for (int castIndex = 0; castIndex < castCount; castIndex++)
            {
                RaycastHit hit = _castResults[castIndex];
                if (hit.collider.attachedRigidbody == _rigidbody
                    || !_exitRecoverySolver.IsSurfaceBlockingDirection(worldDirection, hit.normal))
                {
                    continue;
                }

                if (hit.distance <= Mathf.Epsilon
                    && IsSeparatingFromInitialContact(
                        hit.collider,
                        worldDirection,
                        distanceMeters))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private bool IsSeparatingFromInitialContact(
            Collider contactCollider,
            Vector3 worldDirection,
            float pathDistanceMeters)
        {
            float separationProbeDistanceMeters = Mathf.Min(_settingsSO.CollisionSweepSkinMeters, pathDistanceMeters);
            Vector3 separationProbeRootPosition = _rigidbody.position + worldDirection * separationProbeDistanceMeters;
            CalculateColliderPoseAtRootPosition(
                _underwaterCollider,
                separationProbeRootPosition,
                out Vector3 colliderPosition,
                out Quaternion colliderRotation);
            int overlapCount = _underwaterCollider.OverlapAtPoseNonAlloc(
                colliderPosition,
                colliderRotation,
                _overlapResults,
                _solidCollisionMask,
                QueryTriggerInteraction.Ignore);
            if (overlapCount == MAX_OVERLAP_COUNT)
            {
                Debug.LogWarning(
                    $"Swimming exit separation probe for '{name}' filled the overlap buffer; " +
                    "the initial contact remains blocking.",
                    this);
                return false;
            }

            for (int overlapIndex = 0; overlapIndex < overlapCount; overlapIndex++)
            {
                if (_overlapResults[overlapIndex] == contactCollider)
                {
                    return false;
                }
            }

            return true;
        }

        private void CalculateColliderPoseAtRootPosition(
            CharacterColliderShape colliderShape,
            Vector3 rootPosition,
            out Vector3 colliderPosition,
            out Quaternion colliderRotation)
        {
            colliderPosition = colliderShape.PhysicsCollider.transform.position + rootPosition - _rigidbody.position;
            colliderRotation = colliderShape.PhysicsCollider.transform.rotation;
        }

        private bool IsColliderPoseClear(CharacterColliderShape colliderShape, Quaternion candidateRotation)
        {
            int overlapCount = colliderShape.OverlapAtPoseNonAlloc(
                colliderShape.PhysicsCollider.transform.position,
                candidateRotation,
                _overlapResults,
                _solidCollisionMask,
                QueryTriggerInteraction.Ignore);

            return AreOverlapsClear(overlapCount);
        }

        private bool AreOverlapsClear(int overlapCount)
        {
            for (int overlapIndex = 0; overlapIndex < overlapCount; overlapIndex++)
            {
                Collider overlap = _overlapResults[overlapIndex];
                if (overlap.attachedRigidbody == _rigidbody)
                {
                    continue;
                }

                return false;
            }

            if (overlapCount == MAX_OVERLAP_COUNT)
            {
                Debug.LogWarning($"Swimming collider clearance for '{name}' filled the overlap buffer; treating the candidate pose as blocked.", this);
                return false;
            }

            return true;
        }

        #endregion
    }
}

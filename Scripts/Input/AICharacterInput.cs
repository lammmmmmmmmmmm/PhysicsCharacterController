using Pathfinding;
using UnityEngine;

namespace PhysicsCharacterController
{
    public enum NavigationStatus { Stopped, Pending, Moving, Arrived, Unreachable }

    public class AICharacterInput : BaseCharacterInput
    {
        [SerializeField] private Seeker _seeker;
        [SerializeField, Min(0.01f)] private float _nextWaypointDistance = 3f;
        [SerializeField, Min(0.01f)] private float _stoppingDistanceMeters = 0.3f;
        [Tooltip("Maximum horizontal offset accepted when A* projects the requested destination onto the graph.")]
        [SerializeField, Min(0f)] private float _pathEndpointToleranceMeters = 0.75f;

        private Path _currentPath;
        private Path _requestedPath;
        private int _currentWaypointIndex;
        private ulong _requestVersion;
        private Vector3 _destinationMeters;
        private Vector3 _waypointMeters;
        public NavigationStatus Status { get; private set; }
        // Preserve the legacy idle/completed flag; Status distinguishes failure from arrival.
        public bool HasReachedDestination => Status != NavigationStatus.Pending && Status != NavigationStatus.Moving;
        public Vector3 CurrentTargetPositionMeters => _destinationMeters;

        #region Unity Lifecycle

        private void Update()
        {
            AdvanceWaypoint();
        }

        private void OnDisable()
        {
            Stop();
        }

        #endregion

        #region Public Methods

        public override float GetMoveAngle()
        {
            if (!AreNormalActionsEnabled || Status != NavigationStatus.Moving)
            {
                return transform.eulerAngles.y;
            }

            Vector3 direction = _waypointMeters - transform.position;
            return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }

        public override Vector2 GetMoveInput()
        {
            return AreNormalActionsEnabled && Status == NavigationStatus.Moving ? Vector2.one : Vector2.zero;
        }

        public void SetTarget(Vector3 targetPosition)
        {
            SetDestination(targetPosition, _stoppingDistanceMeters);
        }

        public void SetDestination(Vector3 positionMeters, float stoppingDistanceMeters)
        {
            Stop();
            _destinationMeters = positionMeters;
            _stoppingDistanceMeters = Mathf.Max(0.01f, stoppingDistanceMeters);
            if (!isActiveAndEnabled)
            {
                Debug.LogWarning("AI navigation request ignored because the input is disabled.", this);
                return;
            }
            if (HorizontalDistanceMeters(transform.position, positionMeters) <= _stoppingDistanceMeters)
            {
                Status = NavigationStatus.Arrived;
                return;
            }
            Status = NavigationStatus.Pending;
            var request = new PathRequest(this, _requestVersion);
            _requestedPath = ABPath.Construct(transform.position, positionMeters, null);
            _requestedPath.Claim(this);
            // Seeker callbacks run after endpoint projection and the other path modifiers.
            _seeker.StartPath(_requestedPath, request.AcceptPath);
        }

        public void Stop()
        {
            bool wasMoving = Status == NavigationStatus.Moving;
            CancelPendingPath();
            ReleasePath();
            _currentWaypointIndex = 0;
            _destinationMeters = transform.position;
            _waypointMeters = transform.position;
            Status = NavigationStatus.Stopped;
            if (wasMoving) InvokeMoveStop();
        }

        #endregion

        #region Private Methods

        private void CancelPendingPath()
        {
            _requestVersion++;
            Path pendingPath = _requestedPath;
            _requestedPath = null;
            _seeker.CancelCurrentPathRequest();
            if (pendingPath != null) pendingPath.Release(this);
        }

        private void AcceptPath(Path path, ulong requestVersion)
        {
            if (requestVersion != _requestVersion || path != _requestedPath || !isActiveAndEnabled)
            {
                Debug.Log("Discarded a superseded AI path result.", this);
                return;
            }

            _requestedPath = null;
            if (path.error || path.vectorPath == null || path.vectorPath.Count == 0 ||
                HorizontalDistanceMeters(path.vectorPath[path.vectorPath.Count - 1], _destinationMeters) > Mathf.Max(_stoppingDistanceMeters, _pathEndpointToleranceMeters))
            {
                string failure = path.errorLog;
                if (!path.error)
                {
                    failure = path.vectorPath == null || path.vectorPath.Count == 0
                        ? "A* returned no waypoints."
                        : $"Path endpoint {path.vectorPath[path.vectorPath.Count - 1]} is " +
                          $"{HorizontalDistanceMeters(path.vectorPath[path.vectorPath.Count - 1], _destinationMeters):F2} meters " +
                          $"from the destination; allowed offset is {Mathf.Max(_stoppingDistanceMeters, _pathEndpointToleranceMeters):F2} meters.";
                }
                path.Release(this);
                ReleasePath();
                Status = NavigationStatus.Unreachable;
                InvokeMoveStop();
                Debug.LogWarning($"AI destination {_destinationMeters} is unreachable: {failure}", this);
                return;
            }
            // Transfer the pending request's claim to the active path without returning it to the pool.
            ReleasePath();
            _currentPath = path;
            _currentWaypointIndex = 0;
            _waypointMeters = path.vectorPath[0];
            Status = NavigationStatus.Moving;
            AdvanceWaypoint();
            if (Status == NavigationStatus.Moving && AreNormalActionsEnabled) InvokeMoveStart(Vector2.one);
        }

        private void AdvanceWaypoint()
        {
            if (_currentPath == null || (Status != NavigationStatus.Moving && Status != NavigationStatus.Pending))
            {
                return;
            }

            if (HorizontalDistanceMeters(_destinationMeters, transform.position) <= _stoppingDistanceMeters)
            {
                CancelPendingPath();
                ReleasePath();
                Status = NavigationStatus.Arrived;
                InvokeMoveStop();
                return;
            }

            while (_currentWaypointIndex < _currentPath.vectorPath.Count - 1 &&
                HorizontalDistanceMeters(transform.position, _currentPath.vectorPath[_currentWaypointIndex]) <= _nextWaypointDistance)
            {
                _currentWaypointIndex++;
            }
            _waypointMeters = _currentWaypointIndex == _currentPath.vectorPath.Count - 1
                ? _destinationMeters : _currentPath.vectorPath[_currentWaypointIndex];
        }

        private void ReleasePath()
        {
            if (_currentPath == null) return;
            _currentPath.Release(this);
            _currentPath = null;
        }

        // Graph nodes lie on the floor; the controller root sits at the collider center.
        private static float HorizontalDistanceMeters(Vector3 firstMeters, Vector3 secondMeters)
        {
            return new Vector2(firstMeters.x - secondMeters.x, firstMeters.z - secondMeters.z).magnitude;
        }

        private sealed class PathRequest
        {
            private readonly AICharacterInput _input;
            private readonly ulong _version;

            public PathRequest(AICharacterInput input, ulong version) { _input = input; _version = version; }
            public void AcceptPath(Path path) => _input.AcceptPath(path, _version);
        }

        #endregion
    }
}

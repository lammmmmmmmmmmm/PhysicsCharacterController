using UnityEngine;

namespace PhysicsCharacterController
{
    /// <summary>Chooses a supported up-forward-down path only when it improves horizontal progress.</summary>
    public sealed class StepMovementSolver
    {
        private const float MIN_PROGRESS_METERS = 0.0001f;
        private readonly IStepCollisionQuery _collisionQuery;

        #region Public Methods

        public StepMovementSolver(IStepCollisionQuery collisionQuery)
        {
            _collisionQuery = collisionQuery;
        }

        public bool TryFindStep(
            Vector3 requestedDisplacementMeters,
            bool isGrounded,
            float maxStepHeightMeters,
            float skinWidthMeters,
            float minGroundNormalY,
            out Vector3 displacementMeters)
        {
            displacementMeters = Vector3.zero;
            Vector3 horizontalDisplacementMeters = Vector3.ProjectOnPlane(requestedDisplacementMeters, Vector3.up);
            float horizontalDistanceMeters = horizontalDisplacementMeters.magnitude;
            if (horizontalDistanceMeters <= MIN_PROGRESS_METERS || requestedDisplacementMeters.y > MIN_PROGRESS_METERS ||
                maxStepHeightMeters <= 0f || skinWidthMeters <= 0f)
            {
                return false;
            }

            if (!isGrounded && !WillLand(requestedDisplacementMeters.y, skinWidthMeters, minGroundNormalY))
            {
                return false;
            }

            Vector3 direction = horizontalDisplacementMeters / horizontalDistanceMeters;
            float plainDistanceMeters = FindTravelDistance(Vector3.zero, direction, horizontalDistanceMeters, skinWidthMeters);
            if (plainDistanceMeters >= horizontalDistanceMeters - MIN_PROGRESS_METERS)
            {
                return false;
            }

            float liftMeters = FindTravelDistance(
                Vector3.zero, Vector3.up, maxStepHeightMeters + skinWidthMeters, skinWidthMeters);
            if (liftMeters <= skinWidthMeters)
            {
                return false;
            }

            Vector3 raisedOffsetMeters = Vector3.up * liftMeters;
            float steppedDistanceMeters = FindTravelDistance(raisedOffsetMeters, direction, horizontalDistanceMeters, skinWidthMeters);
            if (steppedDistanceMeters <= plainDistanceMeters + MIN_PROGRESS_METERS)
            {
                return false;
            }

            Vector3 forwardOffsetMeters = raisedOffsetMeters + direction * steppedDistanceMeters;
            if (!_collisionQuery.TryCast(forwardOffsetMeters, Vector3.down, liftMeters + skinWidthMeters,
                    out float landingDistanceMeters, out Vector3 landingNormal) ||
                landingDistanceMeters <= MIN_PROGRESS_METERS || landingNormal.y < minGroundNormalY)
            {
                return false;
            }

            float stepRiseMeters = liftMeters - Mathf.Max(0f, landingDistanceMeters - skinWidthMeters);
            if (stepRiseMeters <= skinWidthMeters || stepRiseMeters > maxStepHeightMeters + skinWidthMeters)
            {
                return false;
            }

            Vector3 candidateDisplacementMeters = direction * steppedDistanceMeters + Vector3.up * stepRiseMeters;
            if (!_collisionQuery.CanOccupy(raisedOffsetMeters) || !_collisionQuery.CanOccupy(candidateDisplacementMeters))
            {
                return false;
            }

            displacementMeters = candidateDisplacementMeters;
            return true;
        }

        #endregion

        #region Private Methods

        private bool WillLand(float verticalDisplacementMeters, float skinWidthMeters, float minGroundNormalY)
        {
            return _collisionQuery.TryCast(Vector3.zero, Vector3.down, -verticalDisplacementMeters + skinWidthMeters,
                       out float groundDistanceMeters, out Vector3 groundNormal) &&
                   groundDistanceMeters > MIN_PROGRESS_METERS && groundNormal.y >= minGroundNormalY;
        }

        private float FindTravelDistance(Vector3 offsetMeters, Vector3 direction, float distanceMeters, float skinWidthMeters)
        {
            return _collisionQuery.TryCast(offsetMeters, direction, distanceMeters + skinWidthMeters,
                out float hitDistanceMeters, out _)
                ? Mathf.Clamp(hitDistanceMeters - skinWidthMeters, 0f, distanceMeters)
                : distanceMeters;
        }

        #endregion
    }
}

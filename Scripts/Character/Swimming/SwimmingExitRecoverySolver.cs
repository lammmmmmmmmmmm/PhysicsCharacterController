using UnityEngine;

namespace PhysicsCharacterController
{
    public sealed class SwimmingExitRecoverySolver
    {
        private const float MINIMUM_DIRECTION_SQUARED_MAGNITUDE = 0.0001f;
        private const float DUPLICATE_DIRECTION_DOT_THRESHOLD = 0.995f;
        private const float BLOCKING_SURFACE_DOT_THRESHOLD = -0.001f;

        #region Public Methods

        public int FillOrderedSearchDirections(
            Vector3[] penetrationDisplacements,
            int penetrationCount,
            Vector3 acceptedSwimmingDirection,
            int horizontalDirectionCount,
            Vector3[] searchDirections)
        {
            int searchDirectionCount = 0;
            AddUniqueDirection(Vector3.up, searchDirections, ref searchDirectionCount);

            for (int penetrationIndex = 0; penetrationIndex < penetrationCount; penetrationIndex++)
            {
                Vector3 horizontalDirection = Vector3.ProjectOnPlane(penetrationDisplacements[penetrationIndex], Vector3.up);
                AddUniqueDirection(horizontalDirection, searchDirections, ref searchDirectionCount);
            }

            Vector3 horizontalSeed = Vector3.ProjectOnPlane(acceptedSwimmingDirection, Vector3.up);
            if (horizontalSeed.sqrMagnitude < MINIMUM_DIRECTION_SQUARED_MAGNITUDE)
            {
                horizontalSeed = Vector3.forward;
            }

            int safeHorizontalDirectionCount = Mathf.Max(horizontalDirectionCount, 1);
            float angleStepDegrees = 360f / safeHorizontalDirectionCount;
            for (int directionIndex = 0; directionIndex < safeHorizontalDirectionCount; directionIndex++)
            {
                Vector3 horizontalDirection = Quaternion.AngleAxis(angleStepDegrees * directionIndex, Vector3.up) * horizontalSeed;
                AddUniqueDirection(horizontalDirection, searchDirections, ref searchDirectionCount);
            }

            return searchDirectionCount;
        }

        public Vector3 CalculateCombinedDepenetration(Vector3[] penetrationDisplacements, int penetrationCount)
        {
            Vector3 combinedDisplacement = Vector3.zero;
            for (int penetrationIndex = 0; penetrationIndex < penetrationCount; penetrationIndex++)
            {
                combinedDisplacement += penetrationDisplacements[penetrationIndex];
            }

            return combinedDisplacement;
        }

        public Vector3 CalculateDeepestDepenetration(Vector3[] penetrationDisplacements, int penetrationCount)
        {
            Vector3 deepestDisplacement = Vector3.zero;
            for (int penetrationIndex = 0; penetrationIndex < penetrationCount; penetrationIndex++)
            {
                Vector3 penetrationDisplacement = penetrationDisplacements[penetrationIndex];
                if (penetrationDisplacement.sqrMagnitude > deepestDisplacement.sqrMagnitude)
                {
                    deepestDisplacement = penetrationDisplacement;
                }
            }

            return deepestDisplacement;
        }

        public bool IsSurfaceBlockingDirection(Vector3 worldDirection, Vector3 surfaceNormal)
        {
            return Vector3.Dot(worldDirection.normalized, surfaceNormal.normalized) < BLOCKING_SURFACE_DOT_THRESHOLD;
        }

        public bool TrySelectNearestPosition(
            Vector3 origin,
            Vector3[] candidatePositions,
            int candidateCount,
            float maximumDistanceMeters,
            out Vector3 nearestPosition)
        {
            nearestPosition = origin;
            float nearestDistanceSquaredMetersSquared = float.PositiveInfinity;
            float maximumDistanceSquaredMetersSquared = maximumDistanceMeters * maximumDistanceMeters;
            bool hasNearestPosition = false;

            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                Vector3 candidatePosition = candidatePositions[candidateIndex];
                float distanceSquaredMetersSquared = (candidatePosition - origin).sqrMagnitude;
                if (distanceSquaredMetersSquared > maximumDistanceSquaredMetersSquared
                    || distanceSquaredMetersSquared >= nearestDistanceSquaredMetersSquared)
                {
                    continue;
                }

                nearestDistanceSquaredMetersSquared = distanceSquaredMetersSquared;
                nearestPosition = candidatePosition;
                hasNearestPosition = true;
            }

            return hasNearestPosition;
        }

        #endregion

        #region Private Methods

        private static void AddUniqueDirection(
            Vector3 direction,
            Vector3[] searchDirections,
            ref int searchDirectionCount)
        {
            if (direction.sqrMagnitude < MINIMUM_DIRECTION_SQUARED_MAGNITUDE || searchDirectionCount >= searchDirections.Length)
            {
                return;
            }

            Vector3 normalizedDirection = direction.normalized;
            for (int directionIndex = 0; directionIndex < searchDirectionCount; directionIndex++)
            {
                if (Vector3.Dot(searchDirections[directionIndex], normalizedDirection) >= DUPLICATE_DIRECTION_DOT_THRESHOLD)
                {
                    return;
                }
            }

            searchDirections[searchDirectionCount] = normalizedDirection;
            searchDirectionCount++;
        }

        #endregion
    }
}

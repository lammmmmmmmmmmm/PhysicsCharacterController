using UnityEngine;

namespace PhysicsCharacterController
{
    public sealed class SwimmingColliderRotationSolver
    {
        private const float VERTICAL_DIRECTION_DOT_THRESHOLD = 0.999f;

        public Quaternion CalculateDirectionalTransitionTargetRotation(
            Vector3 worldDirection,
            Vector3 currentUpDirection,
            Vector3 characterForwardDirection)
        {
            Vector3 normalizedDirection = worldDirection.normalized;
            bool isDescendingVertically = Vector3.Dot(normalizedDirection, Vector3.down)
                                          >= VERTICAL_DIRECTION_DOT_THRESHOLD;
            bool isAscendingVertically = Vector3.Dot(normalizedDirection, Vector3.up)
                                         >= VERTICAL_DIRECTION_DOT_THRESHOLD;
            Vector3 fallbackUpDirection = currentUpDirection;
            if (isDescendingVertically)
            {
                fallbackUpDirection = characterForwardDirection;
            }
            else if (isAscendingVertically)
            {
                fallbackUpDirection = -characterForwardDirection;
            }

            return CalculateTargetRotation(normalizedDirection, fallbackUpDirection);
        }

        public Quaternion CalculateTargetRotation(Vector3 worldDirection, Vector3 fallbackUpDirection)
        {
            Vector3 normalizedDirection = worldDirection.normalized;
            Vector3 stableUpDirection = Vector3.ProjectOnPlane(Vector3.up, normalizedDirection);

            if (stableUpDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                stableUpDirection = Vector3.ProjectOnPlane(fallbackUpDirection, normalizedDirection);
            }

            if (stableUpDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                stableUpDirection = Vector3.ProjectOnPlane(Vector3.forward, normalizedDirection);
            }

            return Quaternion.LookRotation(normalizedDirection, stableUpDirection.normalized);
        }

        public bool ShouldBeginReverseDivePitchAscent(Vector3 requestedDirection, Vector3 currentDirection)
        {
            return IsVerticalAscent(requestedDirection)
                   && Vector3.Dot(currentDirection.normalized, Vector3.down) >= VERTICAL_DIRECTION_DOT_THRESHOLD;
        }

        public bool IsVerticalAscent(Vector3 worldDirection)
        {
            return worldDirection.sqrMagnitude > Mathf.Epsilon
                   && Vector3.Dot(worldDirection.normalized, Vector3.up) >= VERTICAL_DIRECTION_DOT_THRESHOLD;
        }

        public Quaternion CalculateReverseDivePitchAscentStep(
            Quaternion currentRotation,
            Vector3 characterRightDirection,
            float maximumRotationDegrees)
        {
            Vector3 currentDirection = currentRotation * Vector3.forward;
            float remainingRotationDegrees = Vector3.Angle(currentDirection, Vector3.up);
            float rotationStepDegrees = Mathf.Min(
                Mathf.Max(0f, maximumRotationDegrees),
                remainingRotationDegrees);
            return Quaternion.AngleAxis(-rotationStepDegrees, characterRightDirection.normalized) * currentRotation;
        }
    }
}

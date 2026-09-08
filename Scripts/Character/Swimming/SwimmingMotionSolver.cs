using UnityEngine;

namespace PhysicsCharacterController
{
    public sealed class SwimmingMotionSolver
    {
        public Vector3 CalculateSurfaceDirection(Vector2 movementInput, Vector3 cameraForward, Vector3 cameraRight)
        {
            Vector3 planarForward = Vector3.ProjectOnPlane(cameraForward, Vector3.up).normalized;
            Vector3 planarRight = Vector3.ProjectOnPlane(cameraRight, Vector3.up).normalized;
            return Vector3.ClampMagnitude(planarForward * movementInput.y + planarRight * movementInput.x, 1f);
        }

        public Vector3 CalculateUnderwaterDirection(Vector2 movementInput, Vector3 cameraForward, Vector3 cameraRight)
        {
            Vector3 forward = cameraForward.normalized;
            Vector3 right = cameraRight.normalized;
            return Vector3.ClampMagnitude(forward * movementInput.y + right * movementInput.x, 1f);
        }

        public Vector3 DampVerticalVelocityTowards(
            Vector3 currentVelocityMetersPerSecond,
            float targetVerticalVelocityMetersPerSecond,
            float dampingSharpnessPerSecond,
            float fixedDeltaTime)
        {
            float damping01 = 1f - Mathf.Exp(-Mathf.Max(0f, dampingSharpnessPerSecond) * Mathf.Max(0f, fixedDeltaTime));
            currentVelocityMetersPerSecond.y = Mathf.Lerp(
                currentVelocityMetersPerSecond.y,
                targetVerticalVelocityMetersPerSecond,
                damping01);
            return currentVelocityMetersPerSecond;
        }

        public Vector3 CalculateSurfaceTargetVelocity(
            Vector3 surfaceDirection,
            float speedMetersPerSecond,
            float characterRootHeightMeters,
            float surfaceTargetRootHeightMeters,
            float surfaceToleranceMeters,
            float maximumStabilizationSpeedMetersPerSecond,
            float fixedDeltaTime)
        {
            Vector3 targetVelocity = surfaceDirection * speedMetersPerSecond;
            float heightErrorMeters = surfaceTargetRootHeightMeters - characterRootHeightMeters;

            if (Mathf.Abs(heightErrorMeters) <= surfaceToleranceMeters)
            {
                targetVelocity.y = 0f;
                return targetVelocity;
            }

            float safeFixedDeltaTime = Mathf.Max(fixedDeltaTime, Mathf.Epsilon);
            targetVelocity.y = Mathf.Clamp(
                heightErrorMeters / safeFixedDeltaTime,
                -maximumStabilizationSpeedMetersPerSecond,
                maximumStabilizationSpeedMetersPerSecond);
            return targetVelocity;
        }

        public Vector3 MoveVelocity(
            Vector3 currentVelocity,
            Vector3 targetVelocity,
            float accelerationMetersPerSecondSquared,
            float decelerationMetersPerSecondSquared,
            float fixedDeltaTime)
        {
            float rateMetersPerSecondSquared = targetVelocity.sqrMagnitude > currentVelocity.sqrMagnitude
                ? accelerationMetersPerSecondSquared
                : decelerationMetersPerSecondSquared;

            return Vector3.MoveTowards(currentVelocity, targetVelocity, rateMetersPerSecondSquared * fixedDeltaTime);
        }

        public Vector3 ClampUpwardVelocityToSurface(
            Vector3 velocity,
            float characterRootHeightMeters,
            float surfaceTargetRootHeightMeters,
            float fixedDeltaTime)
        {
            float safeFixedDeltaTime = Mathf.Max(fixedDeltaTime, Mathf.Epsilon);
            float remainingUpwardDistanceMeters = Mathf.Max(surfaceTargetRootHeightMeters - characterRootHeightMeters, 0f);
            float maximumUpwardSpeedMetersPerSecond = remainingUpwardDistanceMeters / safeFixedDeltaTime;
            velocity.y = Mathf.Min(velocity.y, maximumUpwardSpeedMetersPerSecond);
            return velocity;
        }

        public Vector3 ProjectVelocityOnCollisionPlane(Vector3 velocity, Vector3 collisionNormal)
        {
            return Vector3.ProjectOnPlane(velocity, collisionNormal);
        }

        public float MoveAnimationSpeedMetersPerSecond(
            float currentAnimationSpeedMetersPerSecond,
            float requestedDirectionMagnitude,
            float requestedSpeedMetersPerSecond,
            float accelerationMetersPerSecondSquared,
            float decelerationMetersPerSecondSquared,
            float fixedDeltaTime)
        {
            float targetAnimationSpeedMetersPerSecond = Mathf.Clamp01(requestedDirectionMagnitude)
                * requestedSpeedMetersPerSecond;
            float rateMetersPerSecondSquared = targetAnimationSpeedMetersPerSecond > currentAnimationSpeedMetersPerSecond
                ? accelerationMetersPerSecondSquared
                : decelerationMetersPerSecondSquared;
            return Mathf.MoveTowards(
                currentAnimationSpeedMetersPerSecond,
                targetAnimationSpeedMetersPerSecond,
                rateMetersPerSecondSquared * fixedDeltaTime);
        }
    }
}

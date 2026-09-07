using UnityEngine;

namespace PhysicsCharacterController
{
    public sealed class SwimmingStateResolver
    {
        public bool ResolveSwimmingAvailability(
            float immersion01,
            bool wasSufficientlyImmersed,
            float enterSwimmingImmersion01,
            float exitSwimmingImmersion01)
        {
            float threshold01 = wasSufficientlyImmersed
                ? exitSwimmingImmersion01
                : enterSwimmingImmersion01;

            return immersion01 >= threshold01;
        }

        public bool ShouldUseTerrestrialMovementInShallowWater(
            bool isGrounded,
            float waterSurfaceHeightMeters,
            float groundHeightMeters,
            float standingColliderHeightMeters,
            float enterSwimmingImmersion01)
        {
            if (!isGrounded || standingColliderHeightMeters <= 0f)
            {
                return false;
            }

            float waterDepthMeters = Mathf.Max(0f, waterSurfaceHeightMeters - groundHeightMeters);
            float groundedStandingImmersion01 = Mathf.Clamp01(waterDepthMeters / standingColliderHeightMeters);
            return groundedStandingImmersion01 < enterSwimmingImmersion01;
        }

        public bool ShouldDive(float inputMagnitude, float requestedDirectionY, float movementInputThreshold, float diveDirectionYThreshold)
        {
            return inputMagnitude > movementInputThreshold && requestedDirectionY <= diveDirectionYThreshold;
        }

        public bool ShouldReturnToSurface(
            float characterRootHeightMeters,
            float surfaceTargetRootHeightMeters,
            float requestedDirectionY,
            float diveDirectionYThreshold,
            float surfaceToleranceMeters)
        {
            bool isAtSurface = characterRootHeightMeters >= surfaceTargetRootHeightMeters - surfaceToleranceMeters;
            bool isStillDiving = requestedDirectionY <= diveDirectionYThreshold;
            return isAtSurface && !isStillDiving;
        }
    }
}

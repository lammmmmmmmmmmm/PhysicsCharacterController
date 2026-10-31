using UnityEngine;

namespace PhysicsCharacterController
{
    public sealed class LocomotionAnimationSpeedBlend
    {
        private float _decelerationStartSpeedMetersPerSecond;

        public float SpeedMetersPerSecond { get; private set; }

        #region Public Methods

        public void Reset()
        {
            SpeedMetersPerSecond = 0f;
            _decelerationStartSpeedMetersPerSecond = 0f;
        }

        public void UpdateSpeed(float measuredSpeedMetersPerSecond, float deltaSeconds, float idleBlendSeconds)
        {
            if (deltaSeconds <= 0f)
            {
                return;
            }

            measuredSpeedMetersPerSecond = Mathf.Max(0f, measuredSpeedMetersPerSecond);
            if (measuredSpeedMetersPerSecond >= SpeedMetersPerSecond)
            {
                SpeedMetersPerSecond = measuredSpeedMetersPerSecond;
                _decelerationStartSpeedMetersPerSecond = measuredSpeedMetersPerSecond;
                return;
            }

            // Keep the starting speed through the entire slowdown, including nonzero physical deceleration.
            // Replacing it with each smaller sample would either snap the mixer or stretch the configured duration.
            float decelerationMetersPerSecondSquared = _decelerationStartSpeedMetersPerSecond / Mathf.Max(0.01f, idleBlendSeconds);
            SpeedMetersPerSecond = Mathf.MoveTowards(SpeedMetersPerSecond, measuredSpeedMetersPerSecond,
                decelerationMetersPerSecondSquared * deltaSeconds);
        }

        #endregion
    }
}

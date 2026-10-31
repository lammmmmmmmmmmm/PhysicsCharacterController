using UnityEngine;

namespace PhysicsCharacterController
{
    public sealed class PlanarLocomotionSpeedSampler
    {
        private Vector3 _previousPositionMeters;
        private bool _hasSample;

        public float SpeedMetersPerSecond { get; private set; }

        #region Public Methods

        public void Reset(Vector3 positionMeters)
        {
            _previousPositionMeters = positionMeters;
            _hasSample = true;
            SpeedMetersPerSecond = 0f;
        }

        public bool Sample(Vector3 positionMeters, float deltaSeconds, float maximumSpeedMetersPerSecond)
        {
            if (!_hasSample || deltaSeconds <= 0f)
            {
                Reset(positionMeters);
                return false;
            }

            // WARNING: Movement code can write Rigidbody.linearVelocity before physics resolves collisions, so its value depends on sampling order.
            // Measure displacement to avoid animating attempted movement against a wall; velocity sampling requires verified execution order.
            Vector3 displacementMeters = positionMeters - _previousPositionMeters;
            displacementMeters.y = 0f;
            _previousPositionMeters = positionMeters;
            float speedMetersPerSecond = displacementMeters.magnitude / deltaSeconds;
            if (speedMetersPerSecond > maximumSpeedMetersPerSecond * 2f)
            {
                // Relocation must not appear as a frame of extremely fast locomotion.
                Reset(positionMeters);
                return false;
            }

            SpeedMetersPerSecond = speedMetersPerSecond;
            return true;
        }

        #endregion
    }
}

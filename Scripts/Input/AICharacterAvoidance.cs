using Pathfinding.RVO;
using UnityEngine;

namespace PhysicsCharacterController
{
    /// <summary>
    /// Local avoidance for an AI character. <see cref="AICharacterInput"/> hands it the next waypoint every frame; the
    /// A* RVO simulator answers with a steered direction and speed that keep this agent clear of the other agents.
    /// Standing agents lock themselves, so moving agents route around them instead of shoving them.
    /// Requires one <see cref="RVOSimulator"/> in the scene.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RVOController))]
    public sealed class AICharacterAvoidance : MonoBehaviour, IMovementModifier
    {
        // A* recommends letting agents briefly exceed their desired speed to dodge.
        private const float MAXIMUM_SPEED_MULTIPLIER = 1.2f;
        private const float MINIMUM_STEER_DISTANCE_METERS = 0.001f;

        [SerializeField] private RVOController _rvoController;
        [SerializeField] private CharacterMove _characterMove;

        private float _desiredSpeedMetersPerSecond;

        #region Public Methods

        public void SetDesiredTarget(Vector3 waypointMeters, Vector3 destinationMeters, bool isMoving)
        {
            if (!isMoving)
            {
                _desiredSpeedMetersPerSecond = 0f;
                _rvoController.SetTarget(transform.position, 0f, 0f, transform.position);
                return;
            }

            _desiredSpeedMetersPerSecond = _characterMove.WalkSpeed;
            _rvoController.SetTarget(
                waypointMeters,
                _desiredSpeedMetersPerSecond,
                _desiredSpeedMetersPerSecond * MAXIMUM_SPEED_MULTIPLIER,
                destinationMeters);
        }

        /// <returns>The horizontal direction RVO steers toward, or <paramref name="pathDirection"/> while it has no answer.</returns>
        public Vector3 GetSteeredDirection(Vector3 pathDirection)
        {
            Vector3 steeredDeltaMeters = _rvoController.CalculateMovementDelta(transform.position, Time.deltaTime);
            steeredDeltaMeters.y = 0f;
            return steeredDeltaMeters.sqrMagnitude > MINIMUM_STEER_DISTANCE_METERS * MINIMUM_STEER_DISTANCE_METERS
                ? steeredDeltaMeters
                : pathDirection;
        }

        public float GetSpeedMultiplier(Vector3 intendedDirection)
        {
            // The agent joins the simulation on enable; until its first step there is no avoidance answer yet.
            if (_desiredSpeedMetersPerSecond <= 0f || _rvoController.rvoAgent == null)
            {
                return 1f;
            }

            return Mathf.Clamp(
                _rvoController.rvoAgent.CalculatedSpeed / _desiredSpeedMetersPerSecond,
                0f,
                MAXIMUM_SPEED_MULTIPLIER);
        }

        #endregion
    }
}

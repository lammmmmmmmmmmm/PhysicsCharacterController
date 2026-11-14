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

        [Tooltip("Within this horizontal distance of its destination the character follows its path without avoidance. " +
                 "A destination at another agent (a chase target) lies inside that agent's avoidance zone, so avoiding it " +
                 "there pushes the character around the agent, or into a nearby obstacle, instead of up to it.")]
        [SerializeField, Min(0f)] private float _finalApproachDistanceMeters = 3f;

        private float _desiredSpeedMetersPerSecond;
        private Vector3 _destinationMeters;

        #region Public Methods

        public void SetDesiredTarget(Vector3 waypointMeters, Vector3 destinationMeters, bool isMoving)
        {
            if (!isMoving)
            {
                _desiredSpeedMetersPerSecond = 0f;
                _rvoController.SetTarget(transform.position, 0f, 0f, transform.position);
                return;
            }

            // The agent keeps its target even on the final approach, so other agents still read where it is heading.
            _destinationMeters = destinationMeters;
            _desiredSpeedMetersPerSecond = _characterMove.WalkSpeedMetersPerSecond;
            _rvoController.SetTarget(
                waypointMeters,
                _desiredSpeedMetersPerSecond,
                _desiredSpeedMetersPerSecond * MAXIMUM_SPEED_MULTIPLIER,
                destinationMeters);
        }

        /// <returns>The horizontal direction RVO steers toward, or <paramref name="pathDirection"/> while it has no answer.</returns>
        public Vector3 GetSteeredDirection(Vector3 pathDirection)
        {
            if (IsOnFinalApproach())
            {
                return pathDirection;
            }

            Vector3 steeredDeltaMeters = Horizontal(_rvoController.CalculateMovementDelta(transform.position, Time.deltaTime));
            return steeredDeltaMeters.sqrMagnitude > MINIMUM_STEER_DISTANCE_METERS * MINIMUM_STEER_DISTANCE_METERS
                ? steeredDeltaMeters
                : pathDirection;
        }

        public float GetSpeedMultiplier(Vector3 intendedDirection)
        {
            // The agent joins the simulation on enable; until its first step there is no avoidance answer yet.
            if (_desiredSpeedMetersPerSecond <= 0f || IsOnFinalApproach() || _rvoController.rvoAgent == null)
            {
                return 1f;
            }

            return Mathf.Clamp(
                _rvoController.rvoAgent.CalculatedSpeed / _desiredSpeedMetersPerSecond,
                0f,
                MAXIMUM_SPEED_MULTIPLIER);
        }

        #endregion

        #region Private Methods

        private bool IsOnFinalApproach()
        {
            if (_desiredSpeedMetersPerSecond <= 0f)
            {
                return false;
            }

            Vector3 toDestinationMeters = Horizontal(_destinationMeters - transform.position);
            return toDestinationMeters.sqrMagnitude <= _finalApproachDistanceMeters * _finalApproachDistanceMeters;
        }

        private static Vector3 Horizontal(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }

        #endregion
    }
}

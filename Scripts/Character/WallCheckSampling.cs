using UnityEngine;

namespace PhysicsCharacterController
{
    public sealed class WallCheckSampling
    {
        public const int DIRECTION_COUNT = 8;
        private const float DIAGONAL_COMPONENT = 0.70710678118f;
        private const float MINIMUM_DIRECTION_ALIGNMENT = 0.95f;
        private const float DEADLINE_TOLERANCE_SECONDS = 0.000001f;
        private static readonly Vector2[] _rotations =
        {
            new(1f, 0f), new(DIAGONAL_COMPONENT, DIAGONAL_COMPONENT), new(0f, 1f), new(-DIAGONAL_COMPONENT, DIAGONAL_COMPONENT),
            new(-1f, 0f), new(-DIAGONAL_COMPONENT, -DIAGONAL_COMPONENT), new(0f, -1f), new(DIAGONAL_COMPONENT, -DIAGONAL_COMPONENT)
        };

        private float _remainingSeconds;
        private Vector3 _sampledDirection;
        private bool _hasSampled;

        #region Public Methods

        public void Reset()
        {
            _remainingSeconds = 0f;
            _hasSampled = false;
        }

        public bool AdvanceAndConsumeSample(float elapsedSeconds, float intervalSeconds, Vector3 direction)
        {
            _remainingSeconds -= elapsedSeconds;
            bool isStationary = direction.sqrMagnitude <= Mathf.Epsilon;
            bool wasStationary = _sampledDirection.sqrMagnitude <= Mathf.Epsilon;
            bool hasDirectionChanged = isStationary != wasStationary ||
                (!isStationary && Vector3.Dot(_sampledDirection, direction) < MINIMUM_DIRECTION_ALIGNMENT);
            if (_hasSampled && _remainingSeconds > DEADLINE_TOLERANCE_SECONDS && !hasDirectionChanged)
                return false;

            _remainingSeconds = Mathf.Max(0f, intervalSeconds);
            _sampledDirection = direction;
            _hasSampled = true;
            return true;
        }

        public static void FillDirections(Vector3 forwardDirection, Vector3 up, Vector3[] directions)
        {
            Vector3 perpendicular = Vector3.Cross(up, forwardDirection);
            Vector3 parallel = up * Vector3.Dot(up, forwardDirection);
            for (int directionIndex = 0; directionIndex < DIRECTION_COUNT; directionIndex++)
            {
                Vector2 rotation = _rotations[directionIndex];
                directions[directionIndex] = forwardDirection * rotation.x + perpendicular * rotation.y + parallel * (1f - rotation.x);
            }
        }

        #endregion
    }
}

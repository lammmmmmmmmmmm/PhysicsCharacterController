using UnityEngine;

namespace PhysicsCharacterController
{
    public sealed class PhysicsCheckSampling
    {
        private const float DEADLINE_TOLERANCE_SECONDS = 0.000001f;
        private float _remainingSeconds;
        private float _intervalSeconds;

        #region Public Methods

        public void SetIntervalSeconds(float intervalSeconds, float phase01 = 0f)
        {
            intervalSeconds = Mathf.Max(0f, intervalSeconds);
            if (_intervalSeconds == intervalSeconds)
                return;

            _intervalSeconds = intervalSeconds;
            Reset(_intervalSeconds * Mathf.Clamp01(phase01));
        }

        public void Reset(float initialDelaySeconds = 0f)
        {
            _remainingSeconds = Mathf.Max(0f, initialDelaySeconds);
        }

        public bool AdvanceAndConsumeSample(float elapsedSeconds, bool requiresImmediateSample = false)
        {
            _remainingSeconds -= Mathf.Max(0f, elapsedSeconds);
            if (!requiresImmediateSample && _intervalSeconds > 0f && _remainingSeconds > DEADLINE_TOLERANCE_SECONDS)
                return false;

            _remainingSeconds = _intervalSeconds;
            return true;
        }

        #endregion
    }
}

using System;
using UnityEngine;

namespace PhysicsCharacterController
{
    /// <summary>
    /// Updates the smoothing mode from grounded state. Airborne smoothing starts after an activation
    /// delay and remains active on landing until the mesh's vertical offset has nearly settled.
    /// The delay prevents brief ground-contact flicker from starting a landing recovery.
    /// </summary>
    [Serializable]
    public class AirborneSmoothingSwitch
    {
        private const float LANDING_SETTLE_TOLERANCE_METERS = 0.005f;

        [Tooltip("Smoothing time while grounded, absorbs terrain bumps")]
        [SerializeField] private float _groundedSmoothTimeSeconds;
        [Tooltip("Smoothing time once airborne, keep small for 1:1 tracking of jumps and falls")]
        [SerializeField] private float _airborneSmoothTimeSeconds;
        [Tooltip("Continuous off-ground time required before airborne smoothing engages. Filters out ground-contact flicker")]
        [SerializeField] private float _airborneActivationDelaySeconds = 0.1f;

        private float _offGroundSeconds;
        private bool _isRecoveringFromLanding;

        #region Public Methods

        public AirborneSmoothingSwitch(float groundedSmoothTimeSeconds, float airborneSmoothTimeSeconds)
        {
            _groundedSmoothTimeSeconds = groundedSmoothTimeSeconds;
            _airborneSmoothTimeSeconds = airborneSmoothTimeSeconds;
        }

        public float UpdateSmoothTimeSeconds(bool isGrounded, float deltaTimeSeconds, float verticalOffsetMeters, float roughness01)
        {
            if (isGrounded)
            {
                if (_offGroundSeconds >= _airborneActivationDelaySeconds)
                {
                    _isRecoveringFromLanding = true;
                }

                _offGroundSeconds = 0f;
                if (_isRecoveringFromLanding && Mathf.Abs(verticalOffsetMeters) > LANDING_SETTLE_TOLERANCE_METERS)
                {
                    return Mathf.Max(_airborneSmoothTimeSeconds, _groundedSmoothTimeSeconds * roughness01);
                }

                _isRecoveringFromLanding = false;
                return _groundedSmoothTimeSeconds * roughness01;
            }

            _isRecoveringFromLanding = false;
            _offGroundSeconds += deltaTimeSeconds;
            bool hasClearedActivationDelay = _offGroundSeconds >= _airborneActivationDelaySeconds;

            return hasClearedActivationDelay ? _airborneSmoothTimeSeconds : _groundedSmoothTimeSeconds;
        }

        public void Reset()
        {
            _offGroundSeconds = 0f;
            _isRecoveringFromLanding = false;
        }

        #endregion
    }
}

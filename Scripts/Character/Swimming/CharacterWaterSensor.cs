using System;
using System.Collections.Generic;
using UnityEngine;

namespace PhysicsCharacterController
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterColliderShape))]
    public sealed class CharacterWaterSensor : MonoBehaviour, ICharacterPhysicsSensor
    {
        private const int MAX_WATER_OVERLAP_COUNT = 16;

        [Header("Dependencies")]
        [SerializeField] private CharacterColliderShape _characterColliderShape;
        [SerializeField] private SwimmingMovementSettingsSO _settingsSO;

        [Header("Detection")]
        [SerializeField] private LayerMask _waterVolumeMask = 1 << 4;

        private readonly Collider[] _waterOverlapResults = new Collider[MAX_WATER_OVERLAP_COUNT];
        private readonly SwimmingStateResolver _stateResolver = new();
        private IReadOnlyList<ICharacterWaterQuery> _additionalWaterQueries = Array.Empty<ICharacterWaterQuery>();
        private readonly PhysicsCheckSampling _sampling = new();

        public bool HasWaterVolume { get; private set; }
        public bool IsSufficientlyImmersed { get; private set; }
        public bool IsSwimmingEntryThresholdReached => HasWaterVolume && Immersion01 >= _settingsSO.EnterSwimmingImmersion01;
        public float Immersion01 { get; private set; }
        public float WaterSurfaceHeightMeters { get; private set; }

        #region Unity Lifecycle

        private void FixedUpdate()
        {
            if (!_sampling.AdvanceAndConsumeSample(Time.fixedDeltaTime, requiresImmediateSample: HasWaterVolume))
                return;

            RefreshWaterState();
        }

        private void OnDisable()
        {
            _sampling.Reset();
            HasWaterVolume = false;
            WaterSurfaceHeightMeters = 0f;
            IsSufficientlyImmersed = false;
            Immersion01 = 0f;
        }

        #endregion

        #region Public Methods

        public void SetSimulationCheckIntervalSeconds(float intervalSeconds)
        {
            _sampling.SetIntervalSeconds(intervalSeconds, (GetInstanceID() & 255) / 256f);
        }

        public void SetAdditionalWaterQueries(IReadOnlyList<ICharacterWaterQuery> waterQueries)
        {
            _additionalWaterQueries = waterQueries;
        }

        public bool ShouldUseTerrestrialMovementInShallowWater(bool isGrounded, float groundHeightMeters)
        {
            float standingColliderHeightMeters = _characterColliderShape.HeightMeters * Mathf.Abs(transform.lossyScale.y);
            return HasWaterVolume && _stateResolver.ShouldUseTerrestrialMovementInShallowWater(
                isGrounded,
                WaterSurfaceHeightMeters,
                groundHeightMeters,
                standingColliderHeightMeters,
                _settingsSO.EnterSwimmingImmersion01);
        }

        #endregion

        #region Private Methods

        private void RefreshWaterState()
        {
            WaterVolume selectedVolume = SelectHighestWaterVolume();
            float selectedSurfaceHeightMeters = selectedVolume != null ? selectedVolume.SurfaceHeightMeters : float.NegativeInfinity;
            // The upright collider is disabled while diving; Collider.bounds would become empty.
            Bounds characterBoundsMeters = _additionalWaterQueries.Count > 0 ? _characterColliderShape.CalculateWorldBounds() : default;
            for (int index = 0; index < _additionalWaterQueries.Count; index++)
            {
                if (_additionalWaterQueries[index].TryGetSurfaceHeight(characterBoundsMeters, out float surfaceHeightMeters))
                {
                    selectedSurfaceHeightMeters = Mathf.Max(selectedSurfaceHeightMeters, surfaceHeightMeters);
                }
            }

            HasWaterVolume = !float.IsNegativeInfinity(selectedSurfaceHeightMeters);
            if (!HasWaterVolume)
            {
                WaterSurfaceHeightMeters = 0f;
                Immersion01 = 0f;
                IsSufficientlyImmersed = false;
                return;
            }

            WaterSurfaceHeightMeters = selectedSurfaceHeightMeters;
            CalculateStandingColliderExtents(out float bottomHeightMeters, out float topHeightMeters);
            Immersion01 = Mathf.InverseLerp(bottomHeightMeters, topHeightMeters, WaterSurfaceHeightMeters);
            IsSufficientlyImmersed = _stateResolver.ResolveSwimmingAvailability(
                Immersion01,
                IsSufficientlyImmersed,
                _settingsSO.EnterSwimmingImmersion01,
                _settingsSO.ExitSwimmingImmersion01);
        }

        private WaterVolume SelectHighestWaterVolume()
        {
            WaterVolume selectedVolume = null;
            float selectedSurfaceHeightMeters = float.NegativeInfinity;

            int overlapCount = _characterColliderShape.OverlapNonAlloc(_waterOverlapResults, _waterVolumeMask, QueryTriggerInteraction.Collide);

            for (int overlapIndex = 0; overlapIndex < overlapCount; overlapIndex++)
            {
                Collider waterCollider = _waterOverlapResults[overlapIndex];
                if (!waterCollider.TryGetComponent(out WaterVolume waterVolume) || waterVolume.SurfaceHeightMeters <= selectedSurfaceHeightMeters)
                {
                    continue;
                }

                selectedVolume = waterVolume;
                selectedSurfaceHeightMeters = waterVolume.SurfaceHeightMeters;
            }

            if (overlapCount == MAX_WATER_OVERLAP_COUNT)
            {
                Debug.LogWarning($"Water detection for '{name}' filled the overlap buffer; the highest detected surface is used.", this);
            }

            return selectedVolume;
        }

        private void CalculateStandingColliderExtents(out float bottomHeightMeters, out float topHeightMeters)
        {
            float colliderHeightMeters = _characterColliderShape.HeightMeters * Mathf.Abs(transform.lossyScale.y);
            float colliderCenterHeightMeters = transform.TransformPoint(_characterColliderShape.Center).y;
            bottomHeightMeters = colliderCenterHeightMeters - colliderHeightMeters * 0.5f;
            topHeightMeters = colliderCenterHeightMeters + colliderHeightMeters * 0.5f;
        }

        #endregion
    }
}

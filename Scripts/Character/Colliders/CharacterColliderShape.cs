using UnityEngine;

namespace PhysicsCharacterController
{
    /// <summary>
    /// Defines the character-controller measurements shared by movement, crouching, and effects.
    /// Concrete shape components translate these measurements to a specific Unity collider while
    /// keeping the collider's original bottom position fixed during height changes.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public abstract class CharacterColliderShape : MonoBehaviour
    {
        private float _originalBottomOffsetMeters;
        private float _requestedHeightMeters;
        private float _heightOverrideMeters;
        private bool _hasHeightOverride;

        public abstract Collider PhysicsCollider { get; }
        public abstract float HeightMeters { get; }
        public abstract Vector3 Center { get; }
        public bool IsPhysicsEnabled => PhysicsCollider.enabled;

        public float OriginalHeightMeters { get; private set; }
        public float OriginalTopOffsetMeters => _originalBottomOffsetMeters + OriginalHeightMeters;
        public float CurrentTopOffsetMeters => Center.y + HeightMeters * 0.5f;
        public float FeetOffsetMeters => HeightMeters * 0.5f - Center.y;

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            RefreshColliderCache();
            OriginalHeightMeters = HeightMeters;
            _originalBottomOffsetMeters = Center.y - OriginalHeightMeters * 0.5f;
            _requestedHeightMeters = OriginalHeightMeters;
        }

        #endregion

        #region Public Methods

        public void SetHeightPreservingBottom(float heightMeters)
        {
            _requestedHeightMeters = heightMeters;
            float effectiveHeightMeters = _hasHeightOverride ? _heightOverrideMeters : heightMeters;
            ApplyHeightPreservingBottom(effectiveHeightMeters);
        }

        public void SetHeightOverridePreservingBottom(float heightMeters)
        {
            _heightOverrideMeters = heightMeters;
            _hasHeightOverride = true;
            ApplyHeightPreservingBottom(heightMeters);
        }

        public void ClearHeightOverride()
        {
            if (!_hasHeightOverride)
            {
                return;
            }

            _hasHeightOverride = false;
            ApplyHeightPreservingBottom(_requestedHeightMeters);
        }

        public void SetPhysicsEnabled(bool isEnabled)
        {
            PhysicsCollider.enabled = isEnabled;
        }

        public int OverlapNonAlloc(Collider[] overlapResults, LayerMask collisionMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            return OverlapAtPoseNonAlloc(
                PhysicsCollider.transform.position,
                PhysicsCollider.transform.rotation,
                overlapResults,
                collisionMask,
                queryTriggerInteraction);
        }

        public abstract int OverlapAtPoseNonAlloc(
            Vector3 worldPosition,
            Quaternion worldRotation,
            Collider[] overlapResults,
            LayerMask collisionMask,
            QueryTriggerInteraction queryTriggerInteraction);

        public abstract int CastAtPoseNonAlloc(
            Vector3 worldPosition,
            Quaternion worldRotation,
            Vector3 worldDirection,
            float distanceMeters,
            RaycastHit[] castResults,
            LayerMask collisionMask,
            QueryTriggerInteraction queryTriggerInteraction);

        #endregion

        #region Internal Methods

        internal void RefreshColliderCache()
        {
            CacheCollider();
        }

        #endregion

        #region Private Methods

        private void ApplyHeightPreservingBottom(float heightMeters)
        {
            float centerYMeters = _originalBottomOffsetMeters + heightMeters * 0.5f;
            SetHeightAndCenter(heightMeters, new Vector3(Center.x, centerYMeters, Center.z));
        }

        protected abstract void CacheCollider();
        protected abstract void SetHeightAndCenter(float heightMeters, Vector3 center);

        #endregion
    }
}

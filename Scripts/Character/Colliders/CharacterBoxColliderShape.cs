using UnityEngine;

namespace PhysicsCharacterController
{
    /// <summary>
    /// Adapts a BoxCollider to the measurements required by the character controller.
    /// Crouching changes only the box height and vertical center, preserving its configured
    /// width, depth, horizontal center, and original feet position.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CharacterBoxColliderShape : CharacterColliderShape
    {
        private readonly BoxColliderGeometryCalculator _geometryCalculator = new();
        private BoxCollider _boxCollider;

        public override Collider PhysicsCollider => _boxCollider;
        public override float HeightMeters => _boxCollider.size.y;
        public override Vector3 Center => _boxCollider.center;

        #region Public Methods

        public override int OverlapAtPoseNonAlloc(
            Vector3 worldPosition,
            Quaternion worldRotation,
            Collider[] overlapResults,
            LayerMask collisionMask,
            QueryTriggerInteraction queryTriggerInteraction)
        {
            BoxColliderGeometry geometry = _geometryCalculator.Calculate(
                worldPosition,
                worldRotation,
                _boxCollider.transform.lossyScale,
                _boxCollider.center,
                _boxCollider.size);
            return Physics.OverlapBoxNonAlloc(
                geometry.Center,
                geometry.HalfExtentsMeters,
                overlapResults,
                geometry.Rotation,
                collisionMask,
                queryTriggerInteraction);
        }

        public override int CastAtPoseNonAlloc(
            Vector3 worldPosition,
            Quaternion worldRotation,
            Vector3 worldDirection,
            float distanceMeters,
            RaycastHit[] castResults,
            LayerMask collisionMask,
            QueryTriggerInteraction queryTriggerInteraction)
        {
            BoxColliderGeometry geometry = _geometryCalculator.Calculate(
                worldPosition,
                worldRotation,
                _boxCollider.transform.lossyScale,
                _boxCollider.center,
                _boxCollider.size);
            return Physics.BoxCastNonAlloc(
                geometry.Center,
                geometry.HalfExtentsMeters,
                worldDirection,
                castResults,
                geometry.Rotation,
                distanceMeters,
                collisionMask,
                queryTriggerInteraction);
        }

        #endregion

        #region Private Methods

        protected override void CacheCollider()
        {
            _boxCollider = GetComponent<BoxCollider>();
        }

        protected override void SetHeightAndCenter(float heightMeters, Vector3 center)
        {
            Vector3 sizeMeters = _boxCollider.size;
            sizeMeters.y = heightMeters;

            _boxCollider.size = sizeMeters;
            _boxCollider.center = center;
        }

        #endregion
    }
}

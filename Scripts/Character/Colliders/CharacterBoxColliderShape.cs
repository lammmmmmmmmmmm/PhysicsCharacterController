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

        public override Bounds CalculateWorldBounds()
        {
            BoxColliderGeometry geometry = _geometryCalculator.Calculate(transform.position, transform.rotation, transform.lossyScale,
                _boxCollider.center, _boxCollider.size);
            Vector3 rightMeters = geometry.Rotation * new Vector3(geometry.HalfExtentsMeters.x, 0f, 0f);
            Vector3 upMeters = geometry.Rotation * new Vector3(0f, geometry.HalfExtentsMeters.y, 0f);
            Vector3 forwardMeters = geometry.Rotation * new Vector3(0f, 0f, geometry.HalfExtentsMeters.z);
            Vector3 extentsMeters = new(
                Mathf.Abs(rightMeters.x) + Mathf.Abs(upMeters.x) + Mathf.Abs(forwardMeters.x),
                Mathf.Abs(rightMeters.y) + Mathf.Abs(upMeters.y) + Mathf.Abs(forwardMeters.y),
                Mathf.Abs(rightMeters.z) + Mathf.Abs(upMeters.z) + Mathf.Abs(forwardMeters.z));
            return new Bounds(geometry.Center, extentsMeters * 2f);
        }

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

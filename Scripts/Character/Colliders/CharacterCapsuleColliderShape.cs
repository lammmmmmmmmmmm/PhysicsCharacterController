using UnityEngine;

namespace PhysicsCharacterController
{
    /// <summary>
    /// Adapts a vertical CapsuleCollider to the measurements required by the character controller.
    /// Character systems depend on CharacterColliderShape, so capsule-specific properties remain
    /// isolated here and can be replaced without changing movement or crouching behavior.
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class CharacterCapsuleColliderShape : CharacterColliderShape
    {
        private readonly CapsuleColliderGeometryCalculator _geometryCalculator = new();
        private CapsuleCollider _capsuleCollider;

        public override Collider PhysicsCollider => _capsuleCollider;
        public override float HeightMeters => _capsuleCollider.height;
        public override Vector3 Center => _capsuleCollider.center;

        #region Public Methods

        public override Bounds CalculateWorldBounds()
        {
            CapsuleColliderGeometry geometry = _geometryCalculator.Calculate(transform.position, transform.rotation, transform.lossyScale,
                _capsuleCollider.center, _capsuleCollider.height, _capsuleCollider.radius, _capsuleCollider.direction);
            Vector3 minimumMeters = Vector3.Min(geometry.PointA, geometry.PointB) - Vector3.one * geometry.RadiusMeters;
            Vector3 maximumMeters = Vector3.Max(geometry.PointA, geometry.PointB) + Vector3.one * geometry.RadiusMeters;
            return new Bounds((minimumMeters + maximumMeters) * 0.5f, maximumMeters - minimumMeters);
        }

        public override int OverlapAtPoseNonAlloc(
            Vector3 worldPosition,
            Quaternion worldRotation,
            Collider[] overlapResults,
            LayerMask collisionMask,
            QueryTriggerInteraction queryTriggerInteraction)
        {
            CapsuleColliderGeometry geometry = _geometryCalculator.Calculate(
                worldPosition,
                worldRotation,
                _capsuleCollider.transform.lossyScale,
                _capsuleCollider.center,
                _capsuleCollider.height,
                _capsuleCollider.radius,
                _capsuleCollider.direction);
            return Physics.OverlapCapsuleNonAlloc(
                geometry.PointA,
                geometry.PointB,
                geometry.RadiusMeters,
                overlapResults,
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
            QueryTriggerInteraction queryTriggerInteraction,
            float insetMeters = 0f)
        {
            CapsuleColliderGeometry geometry = _geometryCalculator.Calculate(
                worldPosition,
                worldRotation,
                _capsuleCollider.transform.lossyScale,
                _capsuleCollider.center,
                _capsuleCollider.height,
                _capsuleCollider.radius,
                _capsuleCollider.direction);
            return Physics.CapsuleCastNonAlloc(
                geometry.PointA,
                geometry.PointB,
                Mathf.Max(0.0001f, geometry.RadiusMeters - insetMeters),
                worldDirection,
                castResults,
                distanceMeters,
                collisionMask,
                queryTriggerInteraction);
        }

        #endregion

        #region Private Methods

        protected override void CacheCollider()
        {
            _capsuleCollider = GetComponent<CapsuleCollider>();
        }

        protected override void SetHeightAndCenter(float heightMeters, Vector3 center)
        {
            _capsuleCollider.height = heightMeters;
            _capsuleCollider.center = center;
        }

        #endregion
    }
}

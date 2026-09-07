using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace PhysicsCharacterController.Tests
{
    public sealed class CharacterColliderShapeCastTests
    {
        private const int TEST_LAYER_MASK = 1;

        private GameObject _shapeGameObject;
        private GameObject _obstacleGameObject;
        private readonly RaycastHit[] _castResults = new RaycastHit[8];

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_shapeGameObject);
            Object.DestroyImmediate(_obstacleGameObject);
        }

        [Test]
        public void CapsuleCastAtPoseNonAlloc_UsesRequestedWorldPose()
        {
            _shapeGameObject = new GameObject("Capsule Shape");
            CharacterCapsuleColliderShape shape = _shapeGameObject.AddComponent<CharacterCapsuleColliderShape>();
            CapsuleCollider capsuleCollider = _shapeGameObject.GetComponent<CapsuleCollider>();
            capsuleCollider.direction = 1;
            capsuleCollider.height = 2f;
            capsuleCollider.radius = 0.5f;
            RefreshColliderCache(shape);
            Vector3 requestedWorldPosition = new Vector3(1000f, 2000f, 0f);
            CreateObstacle(requestedWorldPosition + Vector3.forward * 3f);
            Physics.SyncTransforms();

            int hitCount = shape.CastAtPoseNonAlloc(
                requestedWorldPosition,
                Quaternion.identity,
                Vector3.forward,
                5f,
                _castResults,
                TEST_LAYER_MASK,
                QueryTriggerInteraction.Ignore);

            Assert.That(ContainsObstacle(hitCount), Is.True);
        }

        [Test]
        public void BoxCastAtPoseNonAlloc_UsesRequestedWorldPose()
        {
            _shapeGameObject = new GameObject("Box Shape");
            CharacterBoxColliderShape shape = _shapeGameObject.AddComponent<CharacterBoxColliderShape>();
            BoxCollider boxCollider = _shapeGameObject.GetComponent<BoxCollider>();
            boxCollider.size = Vector3.one;
            RefreshColliderCache(shape);
            Vector3 requestedWorldPosition = new Vector3(1100f, 2000f, 0f);
            CreateObstacle(requestedWorldPosition + Vector3.forward * 3f);
            Physics.SyncTransforms();

            int hitCount = shape.CastAtPoseNonAlloc(
                requestedWorldPosition,
                Quaternion.identity,
                Vector3.forward,
                5f,
                _castResults,
                TEST_LAYER_MASK,
                QueryTriggerInteraction.Ignore);

            Assert.That(ContainsObstacle(hitCount), Is.True);
        }

        private void CreateObstacle(Vector3 worldPosition)
        {
            _obstacleGameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _obstacleGameObject.name = "Cast Obstacle";
            _obstacleGameObject.transform.position = worldPosition;
        }

        private static void RefreshColliderCache(CharacterColliderShape shape)
        {
            MethodInfo refreshMethod = typeof(CharacterColliderShape).GetMethod(
                "RefreshColliderCache",
                BindingFlags.Instance | BindingFlags.NonPublic);
            refreshMethod.Invoke(shape, null);
        }

        private bool ContainsObstacle(int hitCount)
        {
            Collider obstacleCollider = _obstacleGameObject.GetComponent<Collider>();
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                if (_castResults[hitIndex].collider == obstacleCollider)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace PhysicsCharacterController.Tests.PlayMode
{
    public sealed class StepMovementPrefabTests
    {
        private const string BOX_PREFAB_GUID = "8293cc18259c08447a639333d385fdcc";
        private const string CAPSULE_PREFAB_GUID = "173a1bb4e62b8ef4a971f0a9d7ee9983";
        private const string ONE_METER_LEDGE_PREFAB_GUID = "b6e62afdc98b2e149bcd7670d42c36b5";
        private static readonly Vector3 TEST_ORIGIN_METERS = new(2000f, 1000f, 2000f);

        private readonly List<GameObject> _objects = new();
        private readonly List<Mesh> _meshes = new();
        private IObjectResolver _container;
        private GameObject _character;
        private Rigidbody _rigidbody;
        private StepMovementTestDriver _driver;

        #region Unity Lifecycle

        [SetUp]
        public void SetUp()
        {
            _container = new ContainerBuilder().Build();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_character);
            _container.Dispose();
            foreach (GameObject testObject in _objects)
            {
                Object.DestroyImmediate(testObject);
            }

            _objects.Clear();
            foreach (Mesh mesh in _meshes)
            {
                Object.DestroyImmediate(mesh);
            }

            _meshes.Clear();
        }

        #endregion

        #region Public Methods

        [UnityTest]
        public IEnumerator Box_WithGravityAndWallContacts_WalksConsecutiveStairs()
        {
            return UniTask.ToCoroutine(() => AssertWalksStairs(BOX_PREFAB_GUID));
        }

        [UnityTest]
        public IEnumerator Capsule_WithGravityAndWallContacts_WalksConsecutiveStairs()
        {
            return UniTask.ToCoroutine(() => AssertWalksStairs(CAPSULE_PREFAB_GUID));
        }

        [UnityTest]
        public IEnumerator JumpingIntoLedge_PreservesTakeoffWithoutAutomaticStep()
        {
            return UniTask.ToCoroutine(AssertJumpTakeoff);
        }

        [UnityTest]
        public IEnumerator WalkingIntoFullBlock_DoesNotClimbOrBounce()
        {
            return UniTask.ToCoroutine(AssertFullBlockStopsWalking);
        }

        [UnityTest]
        public IEnumerator WalkingTowardOneMeterVoxel_WithSufficientStepHeight_ClimbsWithoutReleasingMovement()
        {
            return UniTask.ToCoroutine(() => AssertApproachesOneMeterLedge(true));
        }

        [UnityTest]
        public IEnumerator WalkingTowardOneMeterCube_WithSufficientStepHeight_ClimbsWithoutReleasingMovement()
        {
            return UniTask.ToCoroutine(() => AssertApproachesOneMeterLedge(false));
        }

        [UnityTest]
        public IEnumerator WalkingAcrossOneMeterVoxel_DoesNotPauseAfterClimbing()
        {
            return UniTask.ToCoroutine(() => AssertApproachesOneMeterLedge(true, maxPostClimbStalledTickCount: 1));
        }

        #endregion

        #region Private Methods

        private async UniTask AssertApproachesOneMeterLedge(bool isVoxelMesh, int maxPostClimbStalledTickCount = 15)
        {
            CreateCharacter(ONE_METER_LEDGE_PREFAB_GUID, hasBoxFloor: !isVoxelMesh);
            _character.transform.position = TEST_ORIGIN_METERS + Vector3.up * 1.005f;
            if (isVoxelMesh)
            {
                CreateVoxelLedge();
            }
            else
            {
                CreateBox(new Vector3(0f, 0.5f, 1.5f), Vector3.one);
            }

            _driver.IsMovementEnabled = true;
            _character.SetActive(true);
            float previousZMeters = _rigidbody.position.z;
            int stalledTickCount = 0;
            bool hasClimbed = false;
            for (int tickIndex = 0; tickIndex < 150 && _rigidbody.position.z < TEST_ORIGIN_METERS.z + 1.4f; tickIndex++)
            {
                await UniTask.Yield(PlayerLoopTiming.LastFixedUpdate);
                float progressMeters = _rigidbody.position.z - previousZMeters;
                stalledTickCount = progressMeters < 0.0005f ? stalledTickCount + 1 : 0;
                hasClimbed |= _rigidbody.position.y > TEST_ORIGIN_METERS.y + 1.7f;
                GroundChecker ground = _character.GetComponent<GroundChecker>();
                WallChecker wall = _character.GetComponent<WallChecker>();
                string state = " position=" + (_rigidbody.position - TEST_ORIGIN_METERS) +
                    " wall=" + wall.IsTouchingWall + " wallNormal=" + wall.WallNormal +
                    " cancellation=" + wall.GetVelocityContribution(_rigidbody.linearVelocity, Vector3.forward * 5f) +
                    " grounded=" + ground.IsGrounded + " normal=" + ground.GroundHit.normal +
                    " speed=" + _character.GetComponent<CharacterMove>().CurrentSpeed;
                int maxStalledTickCount = hasClimbed ? maxPostClimbStalledTickCount : 15;
                Assert.That(stalledTickCount, Is.LessThan(maxStalledTickCount), "Movement paused at ledge:" + state);
                previousZMeters = _rigidbody.position.z;
            }

            Assert.That(_rigidbody.position.z - TEST_ORIGIN_METERS.z, Is.GreaterThanOrEqualTo(1.4f));
            Assert.That(_rigidbody.position.y - TEST_ORIGIN_METERS.y, Is.InRange(1.95f, 2.05f));
        }

        private void CreateVoxelLedge()
        {
            List<Vector3> vertices = new();
            List<int> triangles = new();
            AddQuad(vertices, triangles, new Vector3(-5f, 0f, -5f), new Vector3(-5f, 0f, 5f),
                new Vector3(5f, 0f, 5f), new Vector3(5f, 0f, -5f));
            AddQuad(vertices, triangles, new Vector3(-0.5f, 0f, 1f), new Vector3(-0.5f, 1f, 1f),
                new Vector3(0.5f, 1f, 1f), new Vector3(0.5f, 0f, 1f));
            AddQuad(vertices, triangles, new Vector3(-0.5f, 1f, 1f), new Vector3(-0.5f, 1f, 2f),
                new Vector3(0.5f, 1f, 2f), new Vector3(0.5f, 1f, 1f));
            AddQuad(vertices, triangles, new Vector3(0.5f, 0f, 2f), new Vector3(0.5f, 1f, 2f),
                new Vector3(-0.5f, 1f, 2f), new Vector3(-0.5f, 0f, 2f));
            AddQuad(vertices, triangles, new Vector3(-0.5f, 0f, 2f), new Vector3(-0.5f, 1f, 2f),
                new Vector3(-0.5f, 1f, 1f), new Vector3(-0.5f, 0f, 1f));
            AddQuad(vertices, triangles, new Vector3(0.5f, 0f, 1f), new Vector3(0.5f, 1f, 1f),
                new Vector3(0.5f, 1f, 2f), new Vector3(0.5f, 0f, 2f));
            Mesh mesh = new();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            _meshes.Add(mesh);
            GameObject obstacle = new("Voxel Step Integration Obstacle");
            _objects.Add(obstacle);
            obstacle.transform.position = TEST_ORIGIN_METERS;
            obstacle.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private static void AddQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int firstVertexIndex = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            triangles.AddRange(new[]
            {
                firstVertexIndex, firstVertexIndex + 1, firstVertexIndex + 2,
                firstVertexIndex, firstVertexIndex + 2, firstVertexIndex + 3
            });
        }

        private async UniTask AssertWalksStairs(string prefabGuid)
        {
            CreateCharacter(prefabGuid);
            for (int treadIndex = 0; treadIndex < 5; treadIndex++)
            {
                float heightMeters = (treadIndex + 1) * 0.5f;
                CreateBox(new Vector3(0f, heightMeters * 0.5f, 0.6f + treadIndex * 0.5f),
                    new Vector3(3f, heightMeters, 0.5f));
            }

            _driver.IsMovementEnabled = true;
            _character.SetActive(true);
            float previousZMeters = _rigidbody.position.z;
            int stalledTickCount = 0;
            for (int tickIndex = 0; tickIndex < 250 && _rigidbody.position.z < TEST_ORIGIN_METERS.z + 2.7f; tickIndex++)
            {
                await UniTask.Yield(PlayerLoopTiming.LastFixedUpdate);
                float progressMeters = _rigidbody.position.z - previousZMeters;
                stalledTickCount = progressMeters < 0.0005f ? stalledTickCount + 1 : 0;
                Assert.That(stalledTickCount, Is.LessThan(15), "Stalled on stairs at " + (_rigidbody.position - TEST_ORIGIN_METERS));
                previousZMeters = _rigidbody.position.z;
            }

            Assert.That(_rigidbody.position.z - TEST_ORIGIN_METERS.z, Is.GreaterThanOrEqualTo(2.7f));
            Assert.That(_rigidbody.position.y - TEST_ORIGIN_METERS.y, Is.InRange(3.3f, 3.5f));
        }

        private async UniTask AssertJumpTakeoff()
        {
            CreateCharacter(BOX_PREFAB_GUID);
            CreateBox(new Vector3(0f, 0.25f, 1.35f), new Vector3(3f, 0.5f, 2f));
            _character.SetActive(true);
            await UniTask.Yield(PlayerLoopTiming.LastFixedUpdate);
            await UniTask.Yield(PlayerLoopTiming.LastFixedUpdate);
            _driver.RequestJump();
            _driver.IsMovementEnabled = true;

            for (int tickIndex = 0; tickIndex < 4; tickIndex++)
            {
                await UniTask.Yield(PlayerLoopTiming.LastFixedUpdate);
                Assert.That(_character.GetComponent<StepChecker>().IsTouchingStep, Is.False);
                Assert.That(_rigidbody.linearVelocity.y, Is.GreaterThan(1f));
            }

            Assert.That(_rigidbody.position.y - TEST_ORIGIN_METERS.y, Is.GreaterThan(1.2f));
        }

        private async UniTask AssertFullBlockStopsWalking()
        {
            CreateCharacter(BOX_PREFAB_GUID);
            CreateBox(new Vector3(0f, 0.5f, 1.35f), new Vector3(3f, 1f, 2f));
            _driver.IsMovementEnabled = true;
            _character.SetActive(true);
            for (int tickIndex = 0; tickIndex < 60; tickIndex++)
            {
                await UniTask.Yield(PlayerLoopTiming.LastFixedUpdate);
                Assert.That(_character.GetComponent<StepChecker>().IsTouchingStep, Is.False);
            }

            Assert.That(_rigidbody.position.z - TEST_ORIGIN_METERS.z, Is.LessThan(0.08f));
            Assert.That(_rigidbody.position.y - TEST_ORIGIN_METERS.y, Is.InRange(0.89f, 0.94f));
        }

        private void CreateCharacter(string prefabGuid, bool hasBoxFloor = true)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(prefabGuid));
            _character = _container.Instantiate(prefab);
            _character.transform.position = TEST_ORIGIN_METERS + Vector3.up * 0.905f;
            _rigidbody = _character.GetComponent<Rigidbody>();
            _driver = _character.GetComponent<StepMovementTestDriver>();
            if (hasBoxFloor)
            {
                CreateBox(new Vector3(0f, -0.5f, 0f), new Vector3(10f, 1f, 10f));
            }
        }

        private void CreateBox(Vector3 positionMeters, Vector3 sizeMeters)
        {
            GameObject obstacle = new("Step Integration Obstacle");
            _objects.Add(obstacle);
            obstacle.transform.position = TEST_ORIGIN_METERS + positionMeters;
            obstacle.AddComponent<BoxCollider>().size = sizeMeters;
        }

        #endregion
    }
}
#endif

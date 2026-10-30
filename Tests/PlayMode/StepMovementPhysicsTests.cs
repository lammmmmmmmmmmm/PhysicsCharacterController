using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace PhysicsCharacterController.Tests.PlayMode
{
    public sealed class StepMovementPhysicsTests
    {
        private const float SKIN_WIDTH_METERS = 0.01f;
        private const float MAX_STEP_HEIGHT_METERS = 0.6f;
        private const float MIN_GROUND_NORMAL_Y = 0.65f;
        private static readonly Vector3 TEST_ORIGIN_METERS = new(1000f, 1000f, 1000f);

        private readonly List<GameObject> _objects = new();
        private readonly List<Mesh> _meshes = new();
        private BoxCollider _floorCollider;
        private Rigidbody _rigidbody;
        private StepChecker _collisionQuery;
        private StepMovementSolver _solver;

        #region Unity Lifecycle

        [TearDown]
        public void TearDown()
        {
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

        [TestCase(false)]
        [TestCase(true)]
        public void TryFindStep_HalfMeterLedge_MakesRequestedHorizontalProgress(bool isCapsule)
        {
            CreateCharacter(isCapsule);
            CreateLedge(0.5f);

            Physics.SyncTransforms();
            Assert.That(_collisionQuery.TryCast(Vector3.zero, Vector3.forward, 0.2f, out _, out _), Is.True,
                "The character sweep must detect the ledge before comparing movement paths.");
            bool hasStep = FindStep(new Vector3(0f, 0f, 0.15f), true, out Vector3 displacementMeters);

            Assert.That(hasStep, Is.True);
            Assert.That(displacementMeters.z, Is.EqualTo(0.15f).Within(0.002f));
            Assert.That(displacementMeters.y, Is.InRange(0.3f, 0.51f));
            Assert.That(_collisionQuery.CanOccupy(displacementMeters), Is.True);
        }

        [TestCase(0.6f, true)]
        [TestCase(0.63f, false)]
        [TestCase(1f, false)]
        public void TryFindStep_HeightLimit_RejectsTallerLedges(float heightMeters, bool expectedHasStep)
        {
            CreateCharacter(false);
            CreateLedge(heightMeters);

            bool hasStep = FindStep(new Vector3(0f, 0f, 0.15f), true, out _);

            Assert.That(hasStep, Is.EqualTo(expectedHasStep));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TryFindStep_LowCeiling_DoesNotLiftIntoCeiling(bool isCapsule)
        {
            CreateCharacter(isCapsule);
            CreateLedge(0.5f);
            CreateBox(new Vector3(0f, 2f, 0f), new Vector3(4f, 0.2f, 4f));

            bool hasStep = FindStep(new Vector3(0f, 0f, 0.15f), true, out _);

            Assert.That(hasStep, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TryFindStep_JumpingAgainstLedge_DoesNotAutoLift(bool isCapsule)
        {
            CreateCharacter(isCapsule);
            CreateLedge(0.5f);

            bool hasStep = FindStep(new Vector3(0f, 0.04f, 0.15f), true, out Vector3 displacementMeters);

            Assert.That(hasStep, Is.False);
            Assert.That(displacementMeters, Is.EqualTo(Vector3.zero));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TryFindStep_FallingBesideLedgeWithoutSupport_DoesNotClimbInAir(bool isCapsule)
        {
            CreateCharacter(isCapsule);
            CreateLedge(0.5f);
            _rigidbody.position += Vector3.up * 0.2f;

            bool hasStep = FindStep(new Vector3(0f, -0.02f, 0.15f), false, out _);

            Assert.That(hasStep, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TryFindStep_FallingOntoGround_CanStepOnLandingTick(bool isCapsule)
        {
            CreateCharacter(isCapsule);
            CreateLedge(0.5f);

            bool hasStep = FindStep(new Vector3(0f, -0.03f, 0.15f), false, out _);

            Assert.That(hasStep, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TryFindStep_TriggerLedge_DoesNotStep(bool isCapsule)
        {
            CreateCharacter(isCapsule);
            CreateLedge(0.5f).isTrigger = true;

            bool hasStep = FindStep(new Vector3(0f, 0f, 0.15f), true, out _);

            Assert.That(hasStep, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TryFindStep_ClearGround_DoesNotCreateVerticalMotion(bool isCapsule)
        {
            CreateCharacter(isCapsule);

            bool hasStep = FindStep(new Vector3(0f, 0f, 0.15f), true, out _);

            Assert.That(hasStep, Is.False);
        }

        [Test]
        public void TryFindStep_NoHorizontalMovement_DoesNotLiftAgainstEdge()
        {
            CreateCharacter(false);
            CreateLedge(0.5f);

            bool hasStep = FindStep(Vector3.zero, true, out _);

            Assert.That(hasStep, Is.False);
        }

        [Test]
        public void TryFindStep_CapsuleAtRiserWithShortMovement_DoesNotWaitForRoundedEdgeToBecomeWalkable()
        {
            CreateCharacter(true);
            CreateLedge(0.5f);
            _rigidbody.position += Vector3.forward * 0.045f;

            bool hasStep = FindStep(new Vector3(0f, 0f, 0.04f), true, out Vector3 displacementMeters);

            Assert.That(hasStep, Is.True);
            Assert.That(displacementMeters.z, Is.EqualTo(0.04f).Within(0.001f));
            Assert.That(displacementMeters.y, Is.GreaterThan(0.1f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TryFindStep_FallingWhileOverlappingRiser_DoesNotTreatOverlapAsGround(bool isCapsule)
        {
            CreateCharacter(isCapsule);
            CreateLedge(0.5f);
            _rigidbody.position += new Vector3(0f, 0.1f, 0.1f);

            bool hasStep = FindStep(new Vector3(0f, -0.01f, 0.04f), false, out _);

            Assert.That(hasStep, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TryFindStep_DiagonalApproach_PreservesBothHorizontalComponents(bool isCapsule)
        {
            CreateCharacter(isCapsule);
            CreateLedge(0.5f);

            bool hasStep = FindStep(new Vector3(0.15f, 0f, 0.15f), true, out Vector3 displacementMeters);

            Assert.That(hasStep, Is.True);
            Assert.That(displacementMeters.x, Is.EqualTo(0.15f).Within(0.001f));
            Assert.That(displacementMeters.z, Is.EqualTo(0.15f).Within(0.001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TryFindStep_FloorAndLedgeShareConcaveMesh_FindsLanding(bool isCapsule)
        {
            CreateCharacter(isCapsule);
            Object.DestroyImmediate(_floorCollider.gameObject);
            CreateCompoundFloorAndLedge();

            bool hasStep = FindStep(new Vector3(0f, 0f, 0.15f), true, out Vector3 displacementMeters);

            Assert.That(hasStep, Is.True);
            Assert.That(_collisionQuery.CanOccupy(displacementMeters), Is.True);
        }

        [Test]
        public void TryFindStep_ThinObstacleWithoutRaisedLanding_DoesNotStepOverGap()
        {
            CreateCharacter(false);
            CreateBox(new Vector3(0f, 0.25f, 0.355f), new Vector3(3f, 0.5f, 0.01f));

            bool hasStep = FindStep(Vector3.forward, true, out _);

            Assert.That(hasStep, Is.False);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void TryFindStep_DisabledHeight_ReturnsNoStep(float maxStepHeightMeters)
        {
            CreateCharacter(false);
            CreateLedge(0.5f);
            Physics.SyncTransforms();

            bool hasStep = _solver.TryFindStep(new Vector3(0f, 0f, 0.15f), true, maxStepHeightMeters,
                SKIN_WIDTH_METERS, MIN_GROUND_NORMAL_Y, out _);

            Assert.That(hasStep, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TryFindStep_ConsecutiveStairTreads_ContinuesWithoutStopping(bool isCapsule)
        {
            CreateCharacter(isCapsule);
            for (int treadIndex = 0; treadIndex < 5; treadIndex++)
            {
                float heightMeters = (treadIndex + 1) * 0.5f;
                CreateBox(new Vector3(0f, heightMeters * 0.5f, 0.6f + treadIndex * 0.5f),
                    new Vector3(3f, heightMeters, 0.5f));
            }

            for (int tickIndex = 0; tickIndex < 28; tickIndex++)
            {
                Vector3 requestedDisplacementMeters = Vector3.forward * 0.1f;
                bool hasStep = FindStep(requestedDisplacementMeters, true, out Vector3 displacementMeters);
                if (!hasStep)
                {
                    bool isBlocked = _collisionQuery.TryCast(Vector3.zero, Vector3.forward, 0.1f,
                        out float hitDistanceMeters, out _);
                    float travelMeters = isBlocked ? Mathf.Clamp(hitDistanceMeters - SKIN_WIDTH_METERS, 0f, 0.1f) : 0.1f;
                    Assert.That(travelMeters, Is.GreaterThan(0.001f), "Stopped on stair tick " + tickIndex);
                    displacementMeters = Vector3.forward * travelMeters;
                }

                _rigidbody.position += displacementMeters;
            }

            Assert.That(_rigidbody.position.z - TEST_ORIGIN_METERS.z, Is.InRange(2.7f, 2.81f));
            Assert.That(_rigidbody.position.y - TEST_ORIGIN_METERS.y, Is.InRange(2.49f, 2.52f));
        }

        #endregion

        #region Private Methods

        private void CreateCharacter(bool isCapsule)
        {
            GameObject character = CreateObject("Step Test Character");
            character.transform.position = TEST_ORIGIN_METERS + Vector3.up * 0.005f;
            _rigidbody = character.AddComponent<Rigidbody>();
            _rigidbody.useGravity = false;
            if (isCapsule)
            {
                CapsuleCollider collider = character.AddComponent<CapsuleCollider>();
                collider.height = 1.8f;
                collider.radius = 0.3f;
                collider.center = Vector3.up * 0.9f;
                character.AddComponent<CharacterCapsuleColliderShape>();
            }
            else
            {
                BoxCollider collider = character.AddComponent<BoxCollider>();
                collider.size = new Vector3(0.6f, 1.8f, 0.6f);
                collider.center = Vector3.up * 0.9f;
                character.AddComponent<CharacterBoxColliderShape>();
            }

            _collisionQuery = character.AddComponent<StepChecker>();
            _solver = new StepMovementSolver(_collisionQuery);
            _floorCollider = CreateBox(new Vector3(0f, -0.5f, 0f), new Vector3(10f, 1f, 10f));
        }

        private void CreateCompoundFloorAndLedge()
        {
            Mesh cubeMesh = new()
            {
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                    new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
                    new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
                    new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
                },
                triangles = new[]
                {
                    0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4,
                    3, 7, 6, 3, 6, 2, 0, 4, 7, 0, 7, 3, 1, 2, 6, 1, 6, 5
                }
            };
            _meshes.Add(cubeMesh);
            Mesh compoundMesh = new();
            _meshes.Add(compoundMesh);
            compoundMesh.CombineMeshes(new[]
            {
                new CombineInstance
                {
                    mesh = cubeMesh,
                    transform = Matrix4x4.TRS(new Vector3(0f, -0.5f, 0f), Quaternion.identity, new Vector3(10f, 1f, 10f))
                },
                new CombineInstance
                {
                    mesh = cubeMesh,
                    transform = Matrix4x4.TRS(new Vector3(0f, 0.25f, 1.35f), Quaternion.identity, new Vector3(3f, 0.5f, 2f))
                }
            });
            GameObject obstacle = CreateObject("Compound Step Test Obstacle");
            obstacle.transform.position = TEST_ORIGIN_METERS;
            obstacle.AddComponent<MeshCollider>().sharedMesh = compoundMesh;
        }

        private BoxCollider CreateLedge(float heightMeters)
        {
            return CreateBox(new Vector3(0f, heightMeters * 0.5f, 1.35f), new Vector3(3f, heightMeters, 2f));
        }

        private BoxCollider CreateBox(Vector3 positionMeters, Vector3 sizeMeters)
        {
            GameObject obstacle = CreateObject("Step Test Obstacle");
            obstacle.transform.position = TEST_ORIGIN_METERS + positionMeters;
            BoxCollider collider = obstacle.AddComponent<BoxCollider>();
            collider.size = sizeMeters;
            return collider;
        }

        private GameObject CreateObject(string name)
        {
            GameObject testObject = new(name);
            _objects.Add(testObject);
            return testObject;
        }

        private bool FindStep(Vector3 requestedDisplacementMeters, bool isGrounded, out Vector3 displacementMeters)
        {
            Physics.SyncTransforms();
            return _solver.TryFindStep(requestedDisplacementMeters, isGrounded, MAX_STEP_HEIGHT_METERS,
                SKIN_WIDTH_METERS, MIN_GROUND_NORMAL_Y, out displacementMeters);
        }

        #endregion
    }
}

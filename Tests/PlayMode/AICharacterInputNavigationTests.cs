#if UNITY_EDITOR
using System.Collections;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Pathfinding;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace PhysicsCharacterController.Tests.PlayMode
{
    public sealed class AICharacterInputNavigationTests
    {
        private const string TEST_RIG_PREFAB_GUID = "2d56a217ed2ed2d4abb976930dff46aa";

        private IObjectResolver _container;
        private GameObject _testRig;
        private AICharacterInput _input;
        private Seeker _seeker;

        #region Unity Lifecycle

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(TEST_RIG_PREFAB_GUID));
            _container = new ContainerBuilder().Build();
            _testRig = _container.Instantiate(prefab);
            _input = _testRig.GetComponentInChildren<AICharacterInput>();
            _seeker = _input.GetComponent<Seeker>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_testRig);
            _container.Dispose();
        }

        #endregion

        #region Public Methods

        [Test]
        public void SetDestination_OnLargeNavmeshTriangle_UsesProcessedEndpointInsteadOfRejectingNodeCenter()
        {
            Vector3 destinationMeters = new Vector3(18f, 0f, 2f);
            Vector3 nodeCenterMeters = (Vector3)AstarPath.active.GetNearest(destinationMeters).node.position;
            Assert.That(Vector3.Distance(nodeCenterMeters, destinationMeters), Is.GreaterThan(3f));

            _input.SetDestination(destinationMeters, 0.3f);
            _seeker.GetCurrentPath().BlockUntilCalculated();

            Assert.That(_input.Status, Is.EqualTo(NavigationStatus.Moving));
            Assert.That(_input.GetMoveInput(), Is.EqualTo(Vector2.one));
            Assert.That(_input.GetMoveAngle(), Is.EqualTo(Mathf.Atan2(17f, 1f) * Mathf.Rad2Deg).Within(0.01f));
            Assert.That(Vector3.Distance(_seeker.GetCurrentPath().vectorPath[^1], destinationMeters), Is.LessThan(0.001f));
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(20.75f, 0.3f)]
        [TestCase(21f, 1f)]
        public void SetDestination_AtAllowedProjectionOffset_AcceptsPath(float destinationXMeters, float stoppingDistanceMeters)
        {
            _input.SetDestination(new Vector3(destinationXMeters, 0f, 2f), stoppingDistanceMeters);
            _seeker.GetCurrentPath().BlockUntilCalculated();

            Assert.That(_input.Status, Is.EqualTo(NavigationStatus.Moving));
        }

        [Test]
        public void SetDestination_OutsideProjectionTolerance_ReportsEndpointOffsetAndStops()
        {
            LogAssert.Expect(LogType.Warning, new Regex("AI destination .* is unreachable: Path endpoint .*2.00 meters.*0.75 meters"));

            _input.SetDestination(new Vector3(22f, 0f, 2f), 0.3f);
            _seeker.GetCurrentPath().BlockUntilCalculated();

            Assert.That(_input.Status, Is.EqualTo(NavigationStatus.Unreachable));
            Assert.That(_input.GetMoveInput(), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void SetDestination_BeyondNearestNodeSearchDistance_ReportsPathfindingFailureAndStops()
        {
            LogAssert.Expect(LogType.Warning, new Regex("AI destination .* is unreachable: .+"));

            _input.SetDestination(new Vector3(1000f, 0f, 2f), 0.3f);
            _seeker.GetCurrentPath().BlockUntilCalculated();

            Assert.That(_input.Status, Is.EqualTo(NavigationStatus.Unreachable));
            Assert.That(_input.GetMoveInput(), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void Stop_BeforePathReturns_DoesNotResumeMovement()
        {
            _input.SetDestination(new Vector3(18f, 0f, 2f), 0.3f);
            Path pendingPath = _seeker.GetCurrentPath();
            LogAssert.Expect(LogType.Log, "Discarded a superseded AI path result.");

            _input.Stop();
            pendingPath.BlockUntilCalculated();

            Assert.That(_input.Status, Is.EqualTo(NavigationStatus.Stopped));
            Assert.That(_input.GetMoveInput(), Is.EqualTo(Vector2.zero));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SetDestination_SupersedesPendingRequest_OnlyAcceptsLatestDestination()
        {
            _input.SetDestination(new Vector3(18f, 0f, 2f), 0.3f);
            Vector3 latestDestinationMeters = new Vector3(2f, 0f, 18f);

            _input.SetDestination(latestDestinationMeters, 0.3f);
            _seeker.GetCurrentPath().BlockUntilCalculated();

            Assert.That(_input.Status, Is.EqualTo(NavigationStatus.Moving));
            Assert.That(_input.CurrentTargetPositionMeters, Is.EqualTo(latestDestinationMeters));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ReachingDestination_StopsMovementAtRequestedHorizontalPosition()
        {
            return UniTask.ToCoroutine(ReachDestinationAsync);
        }

        #endregion

        #region Private Methods

        private async UniTask ReachDestinationAsync()
        {
            Vector3 destinationMeters = new Vector3(18f, 0f, 2f);
            _input.SetDestination(destinationMeters, 0.3f);
            _seeker.GetCurrentPath().BlockUntilCalculated();

            _input.transform.position = destinationMeters + Vector3.up;
            await UniTask.NextFrame(PlayerLoopTiming.LastUpdate);

            Assert.That(_input.Status, Is.EqualTo(NavigationStatus.Arrived));
            Assert.That(_input.GetMoveInput(), Is.EqualTo(Vector2.zero));
        }

        #endregion
    }
}
#endif

using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace PhysicsCharacterController.Tests
{
    public sealed class SwimmingExitRecoverySolverTests
    {
        private SwimmingExitRecoverySolver _solver;

        [SetUp]
        public void SetUp()
        {
            _solver = new SwimmingExitRecoverySolver();
        }

        [TearDown]
        public void TearDown()
        {
            _solver = null;
        }

        [Test]
        public void CalculateDeepestDepenetration_FloorContact_ReturnsUpwardDisplacement()
        {
            Vector3[] penetrationDisplacements =
            {
                Vector3.up * 0.4f,
                Vector3.right * 0.1f
            };

            Vector3 displacement = _solver.CalculateDeepestDepenetration(
                penetrationDisplacements,
                penetrationDisplacements.Length);

            Assert.That(displacement, Is.EqualTo(Vector3.up * 0.4f).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [TestCase(1f, 0f, 0f)]
        [TestCase(0f, -1f, 0f)]
        public void CalculateDeepestDepenetration_WallOrCeilingContact_ReturnsContactDisplacement(
            float x,
            float y,
            float z)
        {
            Vector3 expectedDisplacement = new Vector3(x, y, z) * 0.4f;
            Vector3[] penetrationDisplacements =
            {
                expectedDisplacement,
                Vector3.forward * 0.1f
            };

            Vector3 displacement = _solver.CalculateDeepestDepenetration(
                penetrationDisplacements,
                penetrationDisplacements.Length);

            Assert.That(displacement, Is.EqualTo(expectedDisplacement).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void FillOrderedSearchDirections_IgnoresDuplicateAndZeroNormalsAndSearchesUpFirst()
        {
            Vector3[] penetrationDisplacements =
            {
                Vector3.zero,
                Vector3.right,
                Vector3.right * 0.5f
            };
            var searchDirections = new Vector3[8];

            int directionCount = _solver.FillOrderedSearchDirections(
                penetrationDisplacements,
                penetrationDisplacements.Length,
                Vector3.forward,
                horizontalDirectionCount: 1,
                searchDirections: searchDirections);

            Assert.That(directionCount, Is.EqualTo(3));
            Assert.That(searchDirections[0], Is.EqualTo(Vector3.up).Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(searchDirections[1], Is.EqualTo(Vector3.right).Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(searchDirections[2], Is.EqualTo(Vector3.forward).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void TrySelectNearestPosition_SelectsNearestCandidateAndRejectsBeyondMaximumDistance()
        {
            Vector3[] candidatePositions =
            {
                Vector3.right * 3f,
                Vector3.up * 1.5f,
                Vector3.forward
            };

            bool hasNearestPosition = _solver.TrySelectNearestPosition(
                Vector3.zero,
                candidatePositions,
                candidatePositions.Length,
                maximumDistanceMeters: 2f,
                out Vector3 nearestPosition);

            Assert.That(hasNearestPosition, Is.True);
            Assert.That(nearestPosition, Is.EqualTo(Vector3.forward).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void TrySelectNearestPosition_OnlyCandidateBeyondMaximumDistance_ReturnsFalse()
        {
            Vector3[] candidatePositions = { Vector3.right * 2.01f };

            bool hasNearestPosition = _solver.TrySelectNearestPosition(
                Vector3.zero,
                candidatePositions,
                candidatePositions.Length,
                maximumDistanceMeters: 2f,
                out _);

            Assert.That(hasNearestPosition, Is.False);
        }

        [Test]
        public void IsSurfaceBlockingDirection_DetectsMotionIntoSurfaceOnly()
        {
            Assert.That(_solver.IsSurfaceBlockingDirection(Vector3.right, Vector3.left), Is.True);
            Assert.That(_solver.IsSurfaceBlockingDirection(Vector3.right, Vector3.up), Is.False);
            Assert.That(_solver.IsSurfaceBlockingDirection(Vector3.left, Vector3.left), Is.False);
        }
    }
}

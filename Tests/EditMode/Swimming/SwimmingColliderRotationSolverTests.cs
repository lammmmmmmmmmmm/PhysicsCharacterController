using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace PhysicsCharacterController.Tests
{
    public sealed class SwimmingColliderRotationSolverTests
    {
        private SwimmingColliderRotationSolver _solver;

        [SetUp]
        public void SetUp()
        {
            _solver = new SwimmingColliderRotationSolver();
        }

        [TearDown]
        public void TearDown()
        {
            _solver = null;
        }

        [TestCase(0f, 0f, 1f)]
        [TestCase(1f, 0f, 0f)]
        [TestCase(0f, 0f, -1f)]
        [TestCase(0.5f, 0.5f, 0.5f)]
        public void CalculateTargetRotation_AlwaysPointsForwardAlongSwimmingDirection(float x, float y, float z)
        {
            Vector3 direction = new Vector3(x, y, z).normalized;

            Quaternion rotation = _solver.CalculateTargetRotation(direction, Vector3.forward);

            Assert.That(rotation * Vector3.forward, Is.EqualTo(direction).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void CalculateTargetRotation_HorizontalDirection_KeepsColliderUpAlignedWithWorldUp()
        {
            Quaternion rotation = _solver.CalculateTargetRotation(Vector3.back, Vector3.right);

            Assert.That(rotation * Vector3.up, Is.EqualTo(Vector3.up).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void CalculateTargetRotation_VerticalDirection_UsesFallbackWithoutRollingBetweenCalls()
        {
            Quaternion firstRotation = _solver.CalculateTargetRotation(Vector3.down, Vector3.forward);
            Quaternion secondRotation = _solver.CalculateTargetRotation(Vector3.down, firstRotation * Vector3.up);

            Assert.That(Quaternion.Angle(firstRotation, secondRotation), Is.LessThan(0.001f));
        }

        [Test]
        public void CalculateDirectionalTransitionTargetRotation_IdleSurfaceDive_UsesCharacterForwardAsUpDirection()
        {
            Quaternion rotation = _solver.CalculateDirectionalTransitionTargetRotation(
                Vector3.down,
                Vector3.back,
                Vector3.forward);

            Assert.That(rotation * Vector3.forward, Is.EqualTo(Vector3.down).Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(rotation * Vector3.up, Is.EqualTo(Vector3.forward).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void CalculateDirectionalTransitionTargetRotation_AutomaticAscent_UsesCharacterBackwardAsUpDirection()
        {
            Quaternion rotation = _solver.CalculateDirectionalTransitionTargetRotation(
                Vector3.up,
                Vector3.forward,
                Vector3.forward);

            Assert.That(rotation * Vector3.forward, Is.EqualTo(Vector3.up).Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(rotation * Vector3.up, Is.EqualTo(Vector3.back).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void CalculateDirectionalTransitionTargetRotation_HorizontalDirection_PreservesCurrentUpDirection()
        {
            Quaternion rotation = _solver.CalculateDirectionalTransitionTargetRotation(
                Vector3.right,
                Vector3.up,
                Vector3.forward);

            Assert.That(rotation * Vector3.forward, Is.EqualTo(Vector3.right).Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(rotation * Vector3.up, Is.EqualTo(Vector3.up).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void CalculateReverseDivePitchAscentStep_FromHeadDown_RetracesTowardOriginalUprightRotation()
        {
            Quaternion headDownRotation = _solver.CalculateDirectionalTransitionTargetRotation(
                Vector3.down,
                Vector3.back,
                Vector3.forward);

            Quaternion halfwayRotation = _solver.CalculateReverseDivePitchAscentStep(
                headDownRotation,
                Vector3.right,
                90f);
            Quaternion uprightRotation = _solver.CalculateReverseDivePitchAscentStep(
                halfwayRotation,
                Vector3.right,
                90f);

            Assert.That(halfwayRotation * Vector3.forward, Is.EqualTo(Vector3.forward).Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(halfwayRotation * Vector3.right, Is.EqualTo(Vector3.right).Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(uprightRotation * Vector3.forward, Is.EqualTo(Vector3.up).Using(Vector3ComparerWithEqualsOperator.Instance));
        }
    }
}

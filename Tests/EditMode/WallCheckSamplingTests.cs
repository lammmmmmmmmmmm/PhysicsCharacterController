using NUnit.Framework;
using UnityEngine;

namespace PhysicsCharacterController.Tests
{
    public sealed class WallCheckSamplingTests
    {
        private WallCheckSampling _sampling;

        #region Public Methods

        [SetUp]
        public void SetUp()
        {
            _sampling = new WallCheckSampling();
        }

        [Test]
        public void FirstSample_IsImmediate()
        {
            Assert.That(_sampling.AdvanceAndConsumeSample(0f, 0.1f, Vector3.forward), Is.True);
        }

        [Test]
        public void RepeatedDirection_WaitsUntilIntervalElapses()
        {
            _sampling.AdvanceAndConsumeSample(0f, 0.1f, Vector3.forward);

            Assert.That(_sampling.AdvanceAndConsumeSample(0.05f, 0.1f, Vector3.forward), Is.False);
            Assert.That(_sampling.AdvanceAndConsumeSample(0.05f, 0.1f, Vector3.forward), Is.True);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void NonpositiveInterval_SamplesEveryStep(float intervalSeconds)
        {
            _sampling.AdvanceAndConsumeSample(0f, intervalSeconds, Vector3.forward);

            Assert.That(_sampling.AdvanceAndConsumeSample(0f, intervalSeconds, Vector3.forward), Is.True);
        }

        [Test]
        public void Turning_RechecksBeforeIntervalElapses()
        {
            _sampling.AdvanceAndConsumeSample(0f, 0.1f, Vector3.forward);

            Assert.That(_sampling.AdvanceAndConsumeSample(0.01f, 0.1f, Vector3.right), Is.True);
        }

        [Test]
        public void Reset_RequiresFreshSampleOnReactivation()
        {
            _sampling.AdvanceAndConsumeSample(0f, 0.1f, Vector3.forward);
            _sampling.Reset();

            Assert.That(_sampling.AdvanceAndConsumeSample(0f, 0.1f, Vector3.forward), Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        public void Directions_MatchRotationsAroundInclinedCharacterUp(int directionIndex)
        {
            Vector3 forwardDirection = new Vector3(1f, 0f, 2f).normalized;
            Vector3 up = new Vector3(0.2f, 1f, 0.1f).normalized;
            var directions = new Vector3[WallCheckSampling.DIRECTION_COUNT];

            WallCheckSampling.FillDirections(forwardDirection, up, directions);

            Vector3 expectedDirection = Quaternion.AngleAxis(directionIndex * 45f, up) * forwardDirection;
            Assert.That(Vector3.Distance(directions[directionIndex], expectedDirection), Is.LessThan(0.00001f));
        }

        #endregion
    }
}

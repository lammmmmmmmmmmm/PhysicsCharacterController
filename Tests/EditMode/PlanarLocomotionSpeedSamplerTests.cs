using NUnit.Framework;
using UnityEngine;

namespace PhysicsCharacterController.Tests
{
    public sealed class PlanarLocomotionSpeedSamplerTests
    {
        #region Public Methods

        [Test]
        public void ReportsPhysicalDisplacementInsteadOfCommandedSpeed()
        {
            PlanarLocomotionSpeedSampler sampler = new();
            sampler.Reset(Vector3.zero);
            sampler.Sample(Vector3.forward * 0.2f, 0.02f, 20f);
            Assert.That(sampler.SpeedMetersPerSecond, Is.EqualTo(10f).Within(0.001f));
        }

        [Test]
        public void ReportsReducedPhysicalSpeedWithoutSmoothingTheMeasurement()
        {
            PlanarLocomotionSpeedSampler sampler = new();
            sampler.Reset(Vector3.zero);
            sampler.Sample(Vector3.forward, 0.1f, 20f);

            sampler.Sample(Vector3.forward * 1.2f, 0.1f, 20f);

            Assert.That(sampler.SpeedMetersPerSecond, Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void BlockedMovementReportsZeroRawSpeedImmediately()
        {
            PlanarLocomotionSpeedSampler sampler = new();
            sampler.Reset(Vector3.zero);
            sampler.Sample(Vector3.forward * 0.2f, 0.02f, 20f);

            sampler.Sample(Vector3.forward * 0.2f, 0.02f, 20f);

            Assert.That(sampler.SpeedMetersPerSecond, Is.Zero);
        }

        [Test]
        public void VerticalMotionDoesNotAnimateGroundedRunning()
        {
            PlanarLocomotionSpeedSampler sampler = new();
            sampler.Reset(Vector3.zero);
            sampler.Sample(Vector3.up, 0.02f, 20f);
            Assert.That(sampler.SpeedMetersPerSecond, Is.Zero);
        }

        [Test]
        public void TeleportDiscontinuityResetsTheMeasurement()
        {
            PlanarLocomotionSpeedSampler sampler = new();
            sampler.Reset(Vector3.zero);
            sampler.Sample(Vector3.forward * 100f, 0.02f, 20f);
            Assert.That(sampler.SpeedMetersPerSecond, Is.Zero);
            sampler.Sample(Vector3.forward * 100.2f, 0.02f, 20f);
            Assert.That(sampler.SpeedMetersPerSecond, Is.EqualTo(10f).Within(0.01f));
        }

        [Test]
        public void ResetAfterDisableOrRelocationRemovesPreviousSpeed()
        {
            PlanarLocomotionSpeedSampler sampler = new();
            sampler.Reset(Vector3.zero);
            sampler.Sample(Vector3.forward * 0.2f, 0.02f, 20f);
            sampler.Reset(Vector3.right * 0.3f);
            sampler.Sample(Vector3.right * 0.3f, 0.02f, 20f);
            Assert.That(sampler.SpeedMetersPerSecond, Is.Zero);
        }

        [Test]
        public void ZeroDurationDoesNotCreateInvalidSpeed()
        {
            PlanarLocomotionSpeedSampler sampler = new();
            sampler.Sample(Vector3.forward, 0f, 20f);
            Assert.That(sampler.SpeedMetersPerSecond, Is.Zero);
        }

        #endregion
    }
}

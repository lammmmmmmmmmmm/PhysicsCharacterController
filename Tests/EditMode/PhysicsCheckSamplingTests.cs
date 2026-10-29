using NUnit.Framework;

namespace PhysicsCharacterController.Tests
{
    public sealed class PhysicsCheckSamplingTests
    {
        #region Public Methods

        [Test]
        public void Default_SamplesEveryFixedStep()
        {
            var sampling = new PhysicsCheckSampling();
            Assert.That(sampling.AdvanceAndConsumeSample(0f), Is.True);
            Assert.That(sampling.AdvanceAndConsumeSample(0.02f), Is.True);
        }

        [Test]
        public void DistantSampling_WaitsUntilIntervalElapses()
        {
            var sampling = new PhysicsCheckSampling();
            sampling.SetIntervalSeconds(0.1f);
            Assert.That(sampling.AdvanceAndConsumeSample(0f), Is.True);
            Assert.That(sampling.AdvanceAndConsumeSample(0.08f), Is.False);
            Assert.That(sampling.AdvanceAndConsumeSample(0.02f), Is.True);
        }

        [Test]
        public void ActiveInteraction_BypassesDistantCadence()
        {
            var sampling = new PhysicsCheckSampling();
            sampling.SetIntervalSeconds(0.1f);
            sampling.AdvanceAndConsumeSample(0f);

            Assert.That(sampling.AdvanceAndConsumeSample(0.02f, requiresImmediateSample: true), Is.True);
            Assert.That(sampling.AdvanceAndConsumeSample(0.02f, requiresImmediateSample: true), Is.True);
        }

        [Test]
        public void ReturningToFullSimulation_SamplesImmediately()
        {
            var sampling = new PhysicsCheckSampling();
            sampling.SetIntervalSeconds(0.1f, 1f);
            sampling.SetIntervalSeconds(0f);
            Assert.That(sampling.AdvanceAndConsumeSample(0f), Is.True);
        }

        [Test]
        public void RepeatedConfiguration_DoesNotPostponeSampling()
        {
            var sampling = new PhysicsCheckSampling();
            sampling.SetIntervalSeconds(0.1f, 1f);
            Assert.That(sampling.AdvanceAndConsumeSample(0.05f), Is.False);
            sampling.SetIntervalSeconds(0.1f, 1f);
            Assert.That(sampling.AdvanceAndConsumeSample(0.05f), Is.True);
        }

        [Test]
        public void Reset_RequiresFreshSample()
        {
            var sampling = new PhysicsCheckSampling();
            sampling.SetIntervalSeconds(0.1f, 1f);
            sampling.Reset();
            Assert.That(sampling.AdvanceAndConsumeSample(0f), Is.True);
        }

        [Test]
        public void NegativeInterval_ClampsToFullSimulation()
        {
            var sampling = new PhysicsCheckSampling();
            sampling.SetIntervalSeconds(-1f);
            Assert.That(sampling.AdvanceAndConsumeSample(0f), Is.True);
            Assert.That(sampling.AdvanceAndConsumeSample(0f), Is.True);
        }

        #endregion
    }
}

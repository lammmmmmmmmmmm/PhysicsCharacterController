using NUnit.Framework;

namespace PhysicsCharacterController.Tests
{
    public sealed class LocomotionAnimationSpeedBlendTests
    {
        #region Public Methods

        [Test]
        public void NonzeroMeasuredDecelerationDoesNotSnapTheAnimationParameter()
        {
            LocomotionAnimationSpeedBlend blend = new();
            blend.UpdateSpeed(10f, 0.1f, 10f);

            blend.UpdateSpeed(6f, 0.1f, 10f);
            Assert.That(blend.SpeedMetersPerSecond, Is.EqualTo(9.9f).Within(0.001f));
            blend.UpdateSpeed(2f, 0.1f, 10f);
            Assert.That(blend.SpeedMetersPerSecond, Is.EqualTo(9.8f).Within(0.001f));
            blend.UpdateSpeed(0f, 0.1f, 10f);
            Assert.That(blend.SpeedMetersPerSecond, Is.EqualTo(9.7f).Within(0.001f));
        }

        [TestCase(0.15f)]
        [TestCase(2f)]
        [TestCase(10f)]
        public void ConfiguredDurationControlsTheWholeRunToIdleBlend(float durationSeconds)
        {
            LocomotionAnimationSpeedBlend blend = new();
            blend.UpdateSpeed(6f, 0.02f, durationSeconds);

            blend.UpdateSpeed(0f, durationSeconds * 0.25f, durationSeconds);
            Assert.That(blend.SpeedMetersPerSecond, Is.EqualTo(4.5f).Within(0.001f));
            blend.UpdateSpeed(0f, durationSeconds * 0.25f, durationSeconds);
            Assert.That(blend.SpeedMetersPerSecond, Is.EqualTo(3f).Within(0.001f));
            blend.UpdateSpeed(0f, durationSeconds * 0.5f, durationSeconds);
            Assert.That(blend.SpeedMetersPerSecond, Is.Zero.Within(0.001f));
        }

        [Test]
        public void ResumedMovementRespondsImmediatelyWhenItExceedsTheBlendedSpeed()
        {
            LocomotionAnimationSpeedBlend blend = new();
            blend.UpdateSpeed(6f, 0.1f, 10f);
            blend.UpdateSpeed(0f, 5f, 10f);

            blend.UpdateSpeed(8f, 0.02f, 10f);

            Assert.That(blend.SpeedMetersPerSecond, Is.EqualTo(8f));
        }

        [Test]
        public void ResetClearsAContinuingIdleBlend()
        {
            LocomotionAnimationSpeedBlend blend = new();
            blend.UpdateSpeed(6f, 0.1f, 10f);
            blend.UpdateSpeed(0f, 1f, 10f);

            blend.Reset();

            Assert.That(blend.SpeedMetersPerSecond, Is.Zero);
            blend.UpdateSpeed(0f, 1f, 10f);
            Assert.That(blend.SpeedMetersPerSecond, Is.Zero);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void NonPositiveDeltaDoesNotAdvanceTheAnimation(float deltaSeconds)
        {
            LocomotionAnimationSpeedBlend blend = new();
            blend.UpdateSpeed(6f, 0.1f, 10f);

            blend.UpdateSpeed(0f, deltaSeconds, 10f);

            Assert.That(blend.SpeedMetersPerSecond, Is.EqualTo(6f));
        }

        [Test]
        public void NonPositiveDurationUsesTheMinimumBlendDuration()
        {
            LocomotionAnimationSpeedBlend blend = new();
            blend.UpdateSpeed(6f, 0.1f, 10f);

            blend.UpdateSpeed(-2f, 0.01f, 0f);

            Assert.That(blend.SpeedMetersPerSecond, Is.Zero);
        }

        #endregion
    }
}

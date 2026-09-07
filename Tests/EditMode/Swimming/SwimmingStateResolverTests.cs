using NUnit.Framework;

namespace PhysicsCharacterController.Tests
{
    public sealed class SwimmingStateResolverTests
    {
        private SwimmingStateResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _resolver = new SwimmingStateResolver();
        }

        [TearDown]
        public void TearDown()
        {
            _resolver = null;
        }

        [Test]
        public void ResolveSwimmingAvailability_AtEnterThreshold_EntersSwimming()
        {
            bool isAvailable = _resolver.ResolveSwimmingAvailability(0.65f, false, 0.65f, 0.45f);

            Assert.That(isAvailable, Is.True);
        }

        [Test]
        public void ResolveSwimmingAvailability_BetweenThresholdsWhileSwimming_RemainsSwimming()
        {
            bool isAvailable = _resolver.ResolveSwimmingAvailability(0.5f, true, 0.65f, 0.45f);

            Assert.That(isAvailable, Is.True);
        }

        [Test]
        public void ResolveSwimmingAvailability_BelowExitThreshold_ExitsSwimming()
        {
            bool isAvailable = _resolver.ResolveSwimmingAvailability(0.44f, true, 0.65f, 0.45f);

            Assert.That(isAvailable, Is.False);
        }

        [Test]
        public void ShouldUseTerrestrialMovementInShallowWater_GroundedBelowEntryDepth_ReturnsTrue()
        {
            bool shouldUseTerrestrialMovement = _resolver.ShouldUseTerrestrialMovementInShallowWater(
                isGrounded: true,
                waterSurfaceHeightMeters: 1.2f,
                groundHeightMeters: 0f,
                standingColliderHeightMeters: 2f,
                enterSwimmingImmersion01: 0.65f);

            Assert.That(shouldUseTerrestrialMovement, Is.True);
        }

        [Test]
        public void ShouldUseTerrestrialMovementInShallowWater_AtEntryDepth_ReturnsFalse()
        {
            bool shouldUseTerrestrialMovement = _resolver.ShouldUseTerrestrialMovementInShallowWater(
                isGrounded: true,
                waterSurfaceHeightMeters: 1.3f,
                groundHeightMeters: 0f,
                standingColliderHeightMeters: 2f,
                enterSwimmingImmersion01: 0.65f);

            Assert.That(shouldUseTerrestrialMovement, Is.False);
        }

        [Test]
        public void ShouldUseTerrestrialMovementInShallowWater_DeepOrAirborne_ReturnsFalse()
        {
            bool deepGroundShouldUseTerrestrialMovement = _resolver.ShouldUseTerrestrialMovementInShallowWater(
                isGrounded: true,
                waterSurfaceHeightMeters: 3f,
                groundHeightMeters: 0f,
                standingColliderHeightMeters: 2f,
                enterSwimmingImmersion01: 0.65f);
            bool airborneShouldUseTerrestrialMovement = _resolver.ShouldUseTerrestrialMovementInShallowWater(
                isGrounded: false,
                waterSurfaceHeightMeters: 1f,
                groundHeightMeters: 0f,
                standingColliderHeightMeters: 2f,
                enterSwimmingImmersion01: 0.65f);

            Assert.That(deepGroundShouldUseTerrestrialMovement, Is.False);
            Assert.That(airborneShouldUseTerrestrialMovement, Is.False);
        }

        [Test]
        public void ShouldDive_AtDownwardThresholdWithInput_ReturnsTrue()
        {
            bool shouldDive = _resolver.ShouldDive(1f, -0.2f, 0.01f, -0.2f);

            Assert.That(shouldDive, Is.True);
        }

        [Test]
        public void ShouldDive_WithoutMovementInput_ReturnsFalse()
        {
            bool shouldDive = _resolver.ShouldDive(0f, -1f, 0.01f, -0.2f);

            Assert.That(shouldDive, Is.False);
        }

        [Test]
        public void ShouldReturnToSurface_AtSurfaceWhileAscending_ReturnsTrue()
        {
            bool shouldSurface = _resolver.ShouldReturnToSurface(1.95f, 2f, 0.5f, -0.2f, 0.08f);

            Assert.That(shouldSurface, Is.True);
        }

        [Test]
        public void ShouldReturnToSurface_AtSurfaceWhileDiving_ReturnsFalse()
        {
            bool shouldSurface = _resolver.ShouldReturnToSurface(2f, 2f, -0.2f, -0.2f, 0.08f);

            Assert.That(shouldSurface, Is.False);
        }
    }
}

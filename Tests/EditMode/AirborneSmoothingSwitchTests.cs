using NUnit.Framework;

namespace PhysicsCharacterController.Tests
{
    public class AirborneSmoothingSwitchTests
    {
        private AirborneSmoothingSwitch _smoothingSwitch;

        [SetUp]
        public void SetUp()
        {
            _smoothingSwitch = new AirborneSmoothingSwitch(groundedSmoothTimeSeconds: 0.15f, airborneSmoothTimeSeconds: 0.05f);
        }

        [TearDown]
        public void TearDown()
        {
            _smoothingSwitch = null;
        }

        [Test]
        public void UpdateSmoothTimeSeconds_OnFlatGroundWithoutLanding_LocksMeshToCollider()
        {
            float smoothTimeSeconds = _smoothingSwitch.UpdateSmoothTimeSeconds(true, 0.016f, 0.25f, 0f);

            Assert.That(smoothTimeSeconds, Is.Zero);
        }

        [Test]
        public void UpdateSmoothTimeSeconds_OnRoughGround_UsesRoughnessScaledSmoothing()
        {
            float smoothTimeSeconds = _smoothingSwitch.UpdateSmoothTimeSeconds(true, 0.016f, 0.25f, 0.5f);

            Assert.That(smoothTimeSeconds, Is.EqualTo(0.075f).Within(0.0001f));
        }

        [Test]
        public void UpdateSmoothTimeSeconds_AfterSustainedAirTime_KeepsAirborneSmoothingUntilLandingOffsetSettles()
        {
            _smoothingSwitch.UpdateSmoothTimeSeconds(false, 0.11f, 0f, 0f);

            float landingSmoothTimeSeconds = _smoothingSwitch.UpdateSmoothTimeSeconds(true, 0.016f, 0.25f, 0f);
            float recoverySmoothTimeSeconds = _smoothingSwitch.UpdateSmoothTimeSeconds(true, 0.016f, 0.02f, 0f);
            float settledSmoothTimeSeconds = _smoothingSwitch.UpdateSmoothTimeSeconds(true, 0.016f, 0.004f, 0f);

            Assert.That(landingSmoothTimeSeconds, Is.EqualTo(0.05f));
            Assert.That(recoverySmoothTimeSeconds, Is.EqualTo(0.05f));
            Assert.That(settledSmoothTimeSeconds, Is.Zero);
        }

        [Test]
        public void UpdateSmoothTimeSeconds_LandingOnRoughGround_KeepsStrongerTerrainSmoothing()
        {
            _smoothingSwitch.UpdateSmoothTimeSeconds(false, 0.11f, 0f, 0f);

            float smoothTimeSeconds = _smoothingSwitch.UpdateSmoothTimeSeconds(true, 0.016f, 0.25f, 1f);

            Assert.That(smoothTimeSeconds, Is.EqualTo(0.15f));
        }

        [Test]
        public void UpdateSmoothTimeSeconds_AfterBriefGroundContactLoss_DoesNotStartLandingRecovery()
        {
            _smoothingSwitch.UpdateSmoothTimeSeconds(false, 0.02f, 0f, 0f);

            float smoothTimeSeconds = _smoothingSwitch.UpdateSmoothTimeSeconds(true, 0.016f, 0.25f, 0f);

            Assert.That(smoothTimeSeconds, Is.Zero);
        }

        [Test]
        public void Reset_AfterAirTime_DiscardsPendingLandingRecovery()
        {
            _smoothingSwitch.UpdateSmoothTimeSeconds(false, 0.11f, 0f, 0f);
            _smoothingSwitch.Reset();

            float smoothTimeSeconds = _smoothingSwitch.UpdateSmoothTimeSeconds(true, 0.016f, 0.25f, 0f);

            Assert.That(smoothTimeSeconds, Is.Zero);
        }
    }
}

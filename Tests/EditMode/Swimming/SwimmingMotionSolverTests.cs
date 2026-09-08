using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace PhysicsCharacterController.Tests
{
    public sealed class SwimmingMotionSolverTests
    {
        private SwimmingMotionSolver _solver;

        [SetUp]
        public void SetUp()
        {
            _solver = new SwimmingMotionSolver();
        }

        [TearDown]
        public void TearDown()
        {
            _solver = null;
        }

        [Test]
        public void CalculateSurfaceDirection_WithPitchedCamera_RemovesVerticalDirection()
        {
            Vector3 direction = _solver.CalculateSurfaceDirection(
                Vector2.up,
                new Vector3(0f, -0.8f, 0.6f),
                Vector3.right);

            Assert.That(direction, Is.EqualTo(Vector3.forward).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void CalculateUnderwaterDirection_WithPitchedCamera_PreservesVerticalDirection()
        {
            Vector3 cameraForward = new Vector3(0f, -0.8f, 0.6f).normalized;

            Vector3 direction = _solver.CalculateUnderwaterDirection(Vector2.up, cameraForward, Vector3.right);

            Assert.That(direction, Is.EqualTo(cameraForward).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void CalculateUnderwaterDirection_WithDiagonalInput_ClampsMagnitudeToOne()
        {
            Vector3 direction = _solver.CalculateUnderwaterDirection(Vector2.one, Vector3.forward, Vector3.right);

            Assert.That(direction.magnitude, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void RemoveDownwardVelocity_WhenFalling_PreservesHorizontalVelocityAndStopsDescent()
        {
            Vector3 velocityMetersPerSecond = _solver.RemoveDownwardVelocity(new Vector3(4f, -20f, -3f));

            Assert.That(
                velocityMetersPerSecond,
                Is.EqualTo(new Vector3(4f, 0f, -3f)).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void RemoveDownwardVelocity_WhenRising_PreservesJumpVelocity()
        {
            Vector3 velocityMetersPerSecond = _solver.RemoveDownwardVelocity(new Vector3(4f, 5f, -3f));

            Assert.That(
                velocityMetersPerSecond,
                Is.EqualTo(new Vector3(4f, 5f, -3f)).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void CalculateSurfaceTargetVelocity_BelowSurface_ClampsUpwardStabilizationSpeed()
        {
            Vector3 velocity = _solver.CalculateSurfaceTargetVelocity(
                Vector3.forward,
                3f,
                characterRootHeightMeters: 0f,
                surfaceTargetRootHeightMeters: 2f,
                surfaceToleranceMeters: 0.08f,
                maximumStabilizationSpeedMetersPerSecond: 2.5f,
                fixedDeltaTime: 0.02f);

            Assert.That(velocity, Is.EqualTo(new Vector3(0f, 2.5f, 3f)).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void CalculateSurfaceTargetVelocity_InsideTolerance_DoesNotAddVerticalVelocity()
        {
            Vector3 velocity = _solver.CalculateSurfaceTargetVelocity(
                Vector3.zero,
                3f,
                characterRootHeightMeters: 1.95f,
                surfaceTargetRootHeightMeters: 2f,
                surfaceToleranceMeters: 0.08f,
                maximumStabilizationSpeedMetersPerSecond: 2.5f,
                fixedDeltaTime: 0.02f);

            Assert.That(velocity, Is.EqualTo(Vector3.zero).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void MoveVelocity_TowardFasterTarget_UsesAcceleration()
        {
            Vector3 velocity = _solver.MoveVelocity(Vector3.zero, Vector3.forward * 6f, 10f, 8f, 0.1f);

            Assert.That(velocity, Is.EqualTo(Vector3.forward).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void MoveVelocity_TowardZero_UsesDeceleration()
        {
            Vector3 velocity = _solver.MoveVelocity(Vector3.forward * 2f, Vector3.zero, 10f, 8f, 0.1f);

            Assert.That(velocity, Is.EqualTo(Vector3.forward * 1.2f).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void ClampUpwardVelocityToSurface_PreventsCrossingWaterlineInFixedStep()
        {
            Vector3 velocity = _solver.ClampUpwardVelocityToSurface(
                new Vector3(1f, 5f, 2f),
                characterRootHeightMeters: 9.95f,
                surfaceTargetRootHeightMeters: 10f,
                fixedDeltaTime: 0.02f);

            Assert.That(velocity, Is.EqualTo(new Vector3(1f, 2.5f, 2f)).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void ProjectVelocityOnCollisionPlane_AgainstWall_PreservesParallelMotion()
        {
            Vector3 velocity = _solver.ProjectVelocityOnCollisionPlane(new Vector3(2f, 1f, 3f), Vector3.left);

            Assert.That(velocity, Is.EqualTo(new Vector3(0f, 1f, 3f)).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void MoveAnimationSpeed_DuringAcceleration_BlendsGradually()
        {
            float animationSpeedMetersPerSecond = _solver.MoveAnimationSpeedMetersPerSecond(
                currentAnimationSpeedMetersPerSecond: 0f,
                requestedDirectionMagnitude: 1f,
                requestedSpeedMetersPerSecond: 3f,
                accelerationMetersPerSecondSquared: 10f,
                decelerationMetersPerSecondSquared: 8f,
                fixedDeltaTime: 0.02f);

            Assert.That(animationSpeedMetersPerSecond, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(animationSpeedMetersPerSecond, Is.LessThan(3f));
        }

        [Test]
        public void MoveAnimationSpeed_WhenFloorBlocksMovement_PreservesSwimmingBlend()
        {
            float animationSpeedMetersPerSecond = _solver.MoveAnimationSpeedMetersPerSecond(
                currentAnimationSpeedMetersPerSecond: 3f,
                requestedDirectionMagnitude: 1f,
                requestedSpeedMetersPerSecond: 3f,
                accelerationMetersPerSecondSquared: 10f,
                decelerationMetersPerSecondSquared: 8f,
                fixedDeltaTime: 0.02f);

            Assert.That(animationSpeedMetersPerSecond, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void MoveAnimationSpeed_AfterInputRelease_DeceleratesGradually()
        {
            float animationSpeedMetersPerSecond = _solver.MoveAnimationSpeedMetersPerSecond(
                currentAnimationSpeedMetersPerSecond: 3f,
                requestedDirectionMagnitude: 0f,
                requestedSpeedMetersPerSecond: 3f,
                accelerationMetersPerSecondSquared: 10f,
                decelerationMetersPerSecondSquared: 8f,
                fixedDeltaTime: 0.02f);

            Assert.That(animationSpeedMetersPerSecond, Is.EqualTo(2.84f).Within(0.0001f));
        }
    }
}

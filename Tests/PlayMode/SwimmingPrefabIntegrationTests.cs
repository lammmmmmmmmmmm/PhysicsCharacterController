using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace PhysicsCharacterController.Tests.PlayMode
{
    public sealed class SwimmingPrefabIntegrationTests
    {
        private const float TEST_POOL_ROOT_HEIGHT_METERS = 1000f;
        private const float EXIT_RECOVERY_SPEED_METERS_PER_SECOND = 4f;
        private const int MAXIMUM_EXIT_RECOVERY_FIXED_STEPS = 40;

        private IObjectResolver _container;
        private AsyncOperationHandle<GameObject> _playerPrefabHandle;
        private AsyncOperationHandle<GameObject> _poolPrefabHandle;
        private GameObject _playerInstance;
        private GameObject _poolInstance;
        private UnderwaterSwimmingCollider _underwaterCollider;
        private WaterVolume _waterVolume;
        private float _testPoolSurfaceHeightMeters;
        private float _testPoolFloorTopHeightMeters;
        private SwimmingMovementSettingsSO _settingsSO;
        private SwimmingControlMode _originalControlMode;
        private InputActionMap _gameplayActionMap;
        private Keyboard _keyboard;
        private Gamepad _gamepad;
        private bool _wasGameplayActionMapEnabled;
        private bool _wasKeyboardAddedForTest;
        private bool _wasGamepadAddedForTest;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SwimmingTestAssetConfigSO configSO = Resources.Load<SwimmingTestAssetConfigSO>("SwimmingTestAssetConfigSO");
            _playerPrefabHandle = Addressables.LoadAssetAsync<GameObject>(configSO.PlayerPrefab.RuntimeKey);
            _poolPrefabHandle = Addressables.LoadAssetAsync<GameObject>(configSO.SwimmingPoolPrefab.RuntimeKey);
            yield return _playerPrefabHandle;
            yield return _poolPrefabHandle;

            _container = new ContainerBuilder().Build();
            _playerInstance = _container.Instantiate(_playerPrefabHandle.Result);
            _poolInstance = _container.Instantiate(_poolPrefabHandle.Result);
            _underwaterCollider = _playerInstance.GetComponentInChildren<UnderwaterSwimmingCollider>(true);
            _waterVolume = _poolInstance.GetComponentInChildren<WaterVolume>(true);
            _poolInstance.transform.position = Vector3.up * TEST_POOL_ROOT_HEIGHT_METERS;
            Physics.SyncTransforms();
            _testPoolSurfaceHeightMeters = _waterVolume.SurfaceHeightMeters;
            _testPoolFloorTopHeightMeters = _poolInstance.transform.Find("Pool Bottom").GetComponent<Collider>().bounds.max.y;

            FindProductionInputAndSettings();
        }

        [TearDown]
        public void TearDown()
        {
            ReleaseAllKeyboardKeys();
            _settingsSO.SetControlMode(_originalControlMode);
            if (!_wasGameplayActionMapEnabled)
            {
                _gameplayActionMap.Disable();
            }

            if (_wasKeyboardAddedForTest)
            {
                InputSystem.RemoveDevice(_keyboard);
            }

            if (_wasGamepadAddedForTest)
            {
                InputSystem.RemoveDevice(_gamepad);
            }

            Object.DestroyImmediate(_playerInstance);
            Object.DestroyImmediate(_poolInstance);
            _container?.Dispose();

            if (_playerPrefabHandle.IsValid())
            {
                ReleaseAddressableAsset(_playerPrefabHandle);
            }

            if (_poolPrefabHandle.IsValid())
            {
                ReleaseAddressableAsset(_poolPrefabHandle);
            }
        }

        [UnityTest]
        public IEnumerator SurfaceImmersion_ActivatesSurfaceSwimmingAnimationState()
        {
            PlaceCharacter(new Vector3(0f, _testPoolSurfaceHeightMeters - 2f, 0f));

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            CharacterSwimmingMovement swimmingMovement = _underwaterCollider.GetComponent<CharacterSwimmingMovement>();
            PlaceCharacter(new Vector3(0f, swimmingMovement.SurfaceTargetRootHeightMeters, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Transform characterRoot = _underwaterCollider.transform;
            CharacterAnimator animator = _underwaterCollider.GetComponent<CharacterAnimator>();
            CharacterWaterSensor waterSensor = _underwaterCollider.GetComponent<CharacterWaterSensor>();
            Assert.That(
                animator.CurrentTag.name,
                Is.EqualTo("Surface Swimming State"),
                $"root={characterRoot.position}, immersion={waterSensor.Immersion01}, " +
                $"surface={waterSensor.WaterSurfaceHeightMeters}, target={swimmingMovement.SurfaceTargetRootHeightMeters}, " +
                $"shouldDive={swimmingMovement.ShouldDive()}, shouldReturn={swimmingMovement.ShouldReturnToSurface()}");
            Assert.That(_underwaterCollider.IsActive, Is.False);
            Assert.That(_underwaterCollider.GetComponent<CharacterColliderShape>().IsPhysicsEnabled, Is.True);
        }

        [TestCase(SwimmingControlMode.CameraDirected)]
        [TestCase(SwimmingControlMode.DiveButtonWithAutomaticFloat)]
        public void ProductionSwimmingSettings_ControlModeCanBeSelected(SwimmingControlMode controlMode)
        {
            _settingsSO.SetControlMode(controlMode);

            Assert.That(_settingsSO.ControlMode, Is.EqualTo(controlMode));
            Assert.That(_settingsSO.DiveSpeedMetersPerSecond, Is.EqualTo(3f));
            Assert.That(_settingsSO.AutomaticFloatSpeedMetersPerSecond, Is.EqualTo(2f));
        }

        [UnityTest]
        public IEnumerator DiveAction_GamepadWest_HoldsAndReleases()
        {
            _settingsSO.SetControlMode(SwimmingControlMode.DiveButtonWithAutomaticFloat);
            BaseCharacterInput input = _underwaterCollider.GetComponent<BaseCharacterInput>();
            PlaceCharacter(new Vector3(0f, _testPoolSurfaceHeightMeters - 2f, 0f));

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(input.IsDiveActionEnabled, Is.True);
            InputSystem.QueueStateEvent(_gamepad, new GamepadState().WithButton(GamepadButton.West));
            InputSystem.Update();
            Assert.That(input.IsDiveRequested, Is.True);

            InputSystem.QueueStateEvent(_gamepad, new GamepadState());
            InputSystem.Update();
            Assert.That(input.IsDiveRequested, Is.False);
        }

        [UnityTest]
        public IEnumerator DiveButtonControl_HoldDivesAndReleaseFloatsToSurface()
        {
            _settingsSO.SetControlMode(SwimmingControlMode.DiveButtonWithAutomaticFloat);
            CharacterSwimmingMovement swimmingMovement = _underwaterCollider.GetComponent<CharacterSwimmingMovement>();
            BaseCharacterInput input = _underwaterCollider.GetComponent<BaseCharacterInput>();
            PlaceCharacter(new Vector3(0f, _testPoolSurfaceHeightMeters - 2f, 0f));

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            PlaceCharacter(new Vector3(0f, swimmingMovement.SurfaceTargetRootHeightMeters, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(input.IsDiveActionEnabled, Is.True);
            PressKeyboardKey(Key.E);
            for (int fixedStepIndex = 0; fixedStepIndex < 20; fixedStepIndex++)
            {
                yield return new WaitForFixedUpdate();
            }

            Rigidbody characterRigidbody = _underwaterCollider.GetComponent<Rigidbody>();
            Assert.That(input.IsDiveRequested, Is.True);
            Assert.That(_underwaterCollider.IsActive, Is.True);
            Assert.That(characterRigidbody.linearVelocity.y, Is.LessThan(-0.1f));

            ReleaseAllKeyboardKeys();
            bool wasUpwardFloatObserved = false;
            for (int fixedStepIndex = 0; fixedStepIndex < 80; fixedStepIndex++)
            {
                yield return new WaitForFixedUpdate();
                wasUpwardFloatObserved |= characterRigidbody.linearVelocity.y > 0.1f;
                if (!_underwaterCollider.IsActive)
                {
                    break;
                }
            }

            CharacterAnimator animator = _underwaterCollider.GetComponent<CharacterAnimator>();
            Assert.That(input.IsDiveRequested, Is.False);
            Assert.That(wasUpwardFloatObserved, Is.True);
            Assert.That(_underwaterCollider.IsActive, Is.False);
            Assert.That(animator.CurrentTag.name, Is.EqualTo("Surface Swimming State"));
            Assert.That(swimmingMovement.ShouldDive(), Is.False);
        }

        [UnityTest]
        public IEnumerator SurfaceJump_TransitionsToAirborneWithoutImmediateSwimmingReentry()
        {
            CharacterSwimmingMovement swimmingMovement = _underwaterCollider.GetComponent<CharacterSwimmingMovement>();
            BaseCharacterInput input = _underwaterCollider.GetComponent<BaseCharacterInput>();
            CharacterJump characterJump = _underwaterCollider.GetComponent<CharacterJump>();
            Rigidbody characterRigidbody = _underwaterCollider.GetComponent<Rigidbody>();
            PlaceCharacter(new Vector3(0f, _testPoolSurfaceHeightMeters - 2f, 0f));

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            PlaceCharacter(new Vector3(0f, swimmingMovement.SurfaceTargetRootHeightMeters, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(input.AreWaterSurfaceJumpsEnabled, Is.True);
            PressKeyboardKey(Key.Space);
            yield return new WaitForFixedUpdate();

            CharacterAnimator animator = _underwaterCollider.GetComponent<CharacterAnimator>();
            Assert.That(characterJump.IsJumpInProgress, Is.True);
            Assert.That(characterRigidbody.linearVelocity.y, Is.GreaterThan(0f));
            Assert.That(swimmingMovement.IsSwimmingEntrySuppressedAfterWaterSurfaceJump, Is.True);
            Assert.That(animator.CurrentTag.name, Is.EqualTo("Airborne State"));
            Assert.That(input.AreTerrestrialActionsEnabled, Is.True);
            Assert.That(input.AreWaterSurfaceJumpsEnabled, Is.False);

            characterRigidbody.linearVelocity = Vector3.zero;
            swimmingMovement.RefreshWaterSurfaceJumpReentrySuppression();
            Assert.That(swimmingMovement.IsSwimmingEntrySuppressedAfterWaterSurfaceJump, Is.False);
        }

        [UnityTest]
        public IEnumerator FallingIntoDeepWater_SlowsDownwardVelocityGraduallyOnSwimmingEntry()
        {
            var stateDriver = _underwaterCollider.GetComponent<CharacterStateMachine.CharacterStateMachineDriver>();
            Rigidbody characterRigidbody = _underwaterCollider.GetComponent<Rigidbody>();
            stateDriver.enabled = false;
            PlaceCharacter(new Vector3(0f, _testPoolSurfaceHeightMeters - 0.5f, 0f));

            yield return new WaitForFixedUpdate();

            CharacterWaterSensor waterSensor = _underwaterCollider.GetComponent<CharacterWaterSensor>();
            Assert.That(waterSensor.IsSwimmingEntryThresholdReached, Is.True);
            characterRigidbody.linearVelocity = new Vector3(2f, -20f, 1f);
            stateDriver.enabled = true;

            yield return new WaitForFixedUpdate();
            float firstSwimmingStepVerticalSpeedMetersPerSecond = characterRigidbody.linearVelocity.y;

            yield return new WaitForFixedUpdate();

            Assert.That(firstSwimmingStepVerticalSpeedMetersPerSecond, Is.GreaterThan(-20f).And.LessThan(0f));
            Assert.That(characterRigidbody.linearVelocity.y, Is.GreaterThan(firstSwimmingStepVerticalSpeedMetersPerSecond));
            Assert.That(new Vector2(characterRigidbody.linearVelocity.x, characterRigidbody.linearVelocity.z).magnitude, Is.GreaterThan(1f));
            Assert.That(_underwaterCollider.GetComponent<BaseCharacterInput>().AreTerrestrialActionsEnabled, Is.False);
        }

        [UnityTest]
        public IEnumerator RisingIntoDeepWater_SlowsUpwardJumpVelocityGraduallyOnSwimmingEntry()
        {
            var stateDriver = _underwaterCollider.GetComponent<CharacterStateMachine.CharacterStateMachineDriver>();
            Rigidbody characterRigidbody = _underwaterCollider.GetComponent<Rigidbody>();
            stateDriver.enabled = false;
            PlaceCharacter(new Vector3(0f, _testPoolSurfaceHeightMeters - 0.5f, 0f));

            yield return new WaitForFixedUpdate();

            CharacterWaterSensor waterSensor = _underwaterCollider.GetComponent<CharacterWaterSensor>();
            Assert.That(waterSensor.IsSwimmingEntryThresholdReached, Is.True);
            characterRigidbody.linearVelocity = Vector3.up * 10f;
            stateDriver.enabled = true;

            yield return new WaitForFixedUpdate();

            Assert.That(characterRigidbody.linearVelocity.y, Is.GreaterThan(0f).And.LessThan(10f));
            Assert.That(_underwaterCollider.GetComponent<BaseCharacterInput>().AreTerrestrialActionsEnabled, Is.False);
        }

        [UnityTest]
        public IEnumerator ShallowImmersion_RetainsTerrestrialColliderAndMovementState()
        {
            PlaceCharacter(new Vector3(0f, _testPoolSurfaceHeightMeters + 0.4f, 0f));

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            CharacterWaterSensor waterSensor = _underwaterCollider.GetComponent<CharacterWaterSensor>();
            Assert.That(waterSensor.IsSufficientlyImmersed, Is.False);
            Assert.That(waterSensor.IsSwimmingEntryThresholdReached, Is.False);
            Assert.That(_underwaterCollider.IsActive, Is.False);
            Assert.That(_underwaterCollider.GetComponent<CharacterColliderShape>().IsPhysicsEnabled, Is.True);
        }

        [UnityTest]
        public IEnumerator SwimmingFromDeepWaterOntoShallowRamp_TransitionsToTerrestrialMovement()
        {
            PlaceCharacter(new Vector3(0f, _testPoolSurfaceHeightMeters - 2f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(_underwaterCollider.IsActive, Is.True);

            Collider shallowRampCollider = _poolInstance.transform.Find("Shallow Entry Ramp").GetComponent<Collider>();
            var rampRay = new Ray(
                new Vector3(0f, _testPoolSurfaceHeightMeters + 1f, -3.5f),
                Vector3.down);
            bool didHitRamp = shallowRampCollider.Raycast(rampRay, out RaycastHit rampHit, 10f);
            Assert.That(
                didHitRamp,
                Is.True,
                $"Ramp bounds={shallowRampCollider.bounds}, ray origin={rampRay.origin}");
            PlaceCharacter(rampHit.point + rampHit.normal * 0.46f);

            bool wasGroundedShallowWaterObserved = false;
            GroundChecker groundChecker = _underwaterCollider.GetComponent<GroundChecker>();
            CharacterWaterSensor waterSensor = _underwaterCollider.GetComponent<CharacterWaterSensor>();
            for (int fixedStepIndex = 0;
                 fixedStepIndex < MAXIMUM_EXIT_RECOVERY_FIXED_STEPS && _underwaterCollider.IsActive;
                 fixedStepIndex++)
            {
                yield return new WaitForFixedUpdate();
                wasGroundedShallowWaterObserved |= waterSensor.ShouldUseTerrestrialMovementInShallowWater(
                    groundChecker.IsGrounded,
                    groundChecker.GroundHit.point.y);
            }

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(
                wasGroundedShallowWaterObserved,
                Is.True,
                $"root={_underwaterCollider.transform.position}, ramp hit={rampHit.point}, " +
                $"grounded={groundChecker.IsGrounded}, immersion={waterSensor.Immersion01}");
            Assert.That(
                _underwaterCollider.IsActive,
                Is.False,
                $"Shallow-water exit did not complete. root={_underwaterCollider.transform.position}, " +
                $"recovery={_underwaterCollider.IsTerrestrialExitRecoveryActive}, " +
                $"grounded={groundChecker.IsGrounded}, immersion={waterSensor.Immersion01}");
            Assert.That(waterSensor.IsSwimmingEntryThresholdReached, Is.False);
            Assert.That(_underwaterCollider.GetComponent<CharacterColliderShape>().IsPhysicsEnabled, Is.True);
            Assert.That(_underwaterCollider.GetComponent<BaseCharacterInput>().AreTerrestrialActionsEnabled, Is.True);
        }

        [UnityTest]
        public IEnumerator SwimmingNearDeepPoolFloor_RemainsSwimming()
        {
            PlaceCharacter(new Vector3(0f, _testPoolSurfaceHeightMeters - 2f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(_underwaterCollider.IsActive, Is.True);

            PlaceCharacter(new Vector3(0f, _testPoolFloorTopHeightMeters + 0.46f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            GroundChecker groundChecker = _underwaterCollider.GetComponent<GroundChecker>();
            CharacterWaterSensor waterSensor = _underwaterCollider.GetComponent<CharacterWaterSensor>();
            Assert.That(
                waterSensor.ShouldUseTerrestrialMovementInShallowWater(
                    groundChecker.IsGrounded,
                    groundChecker.GroundHit.point.y),
                Is.False);
            Assert.That(_underwaterCollider.IsActive, Is.True);
        }

        [Test]
        public void TryActivate_EnablesOnlyDirectionAlignedUnderwaterColliderShape()
        {
            CharacterColliderShape uprightCollider = _underwaterCollider.GetComponent<CharacterColliderShape>();
            CharacterColliderShape[] colliderShapes = _underwaterCollider.GetComponentsInChildren<CharacterColliderShape>(true);

            bool didActivate = _underwaterCollider.TryActivate(Vector3.forward);

            Assert.That(didActivate, Is.True);
            Assert.That(_underwaterCollider.IsActive, Is.True);
            Assert.That(uprightCollider.IsPhysicsEnabled, Is.False);
            Assert.That(CountEnabledColliderShapes(colliderShapes), Is.EqualTo(1));
            Assert.That(Vector3.Dot(_underwaterCollider.AcceptedDirection, Vector3.forward), Is.GreaterThan(0.999f));
        }

        [Test]
        public void ProductionUnderwaterCollider_WhenInactive_DefaultsToVertical()
        {
            Assert.That(_underwaterCollider.IsActive, Is.False);
            Assert.That(Vector3.Dot(_underwaterCollider.AcceptedDirection, Vector3.up), Is.GreaterThan(0.999f));
        }

        [Test]
        public void UnderwaterEntryWithoutMovement_KeepsTreadingWaterBodyUpright()
        {
            Rigidbody characterRigidbody = _underwaterCollider.GetComponent<Rigidbody>();
            _underwaterCollider.transform.rotation = Quaternion.Euler(0f, 73f, 0f);
            Physics.SyncTransforms();
            Transform meshTransform = _underwaterCollider.transform.Find("Mesh");
            Vector3 facingDirectionBeforeEntry = meshTransform.forward;
            Assert.That(_underwaterCollider.TryActivate(Vector3.zero), Is.True);
            CharacterSwimmingVisualOrientation visualOrientation = _underwaterCollider.GetComponent<CharacterSwimmingVisualOrientation>();
            visualOrientation.AlignToColliderRotation(
                _underwaterCollider.AcceptedRotation,
                characterRigidbody.rotation,
                swimmingAnimationBlend01: 0f,
                fixedDeltaTime: 1f);

            Assert.That(Vector3.Dot(meshTransform.up, Vector3.up), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(meshTransform.forward, facingDirectionBeforeEntry), Is.GreaterThan(0.999f));
        }

        [Test]
        public void IdleSurfaceDive_TransitionsHeadDownByTiltingForward()
        {
            _underwaterCollider.transform.rotation = Quaternion.Euler(0f, 73f, 0f);
            Physics.SyncTransforms();
            Rigidbody characterRigidbody = _underwaterCollider.GetComponent<Rigidbody>();
            Transform meshTransform = _underwaterCollider.transform.Find("Mesh");
            Vector3 characterForwardDirection = characterRigidbody.rotation * Vector3.forward;
            Vector3 characterRightDirection = characterRigidbody.rotation * Vector3.right;
            Assert.That(_underwaterCollider.TryActivate(Vector3.down), Is.True);
            CharacterSwimmingVisualOrientation visualOrientation = _underwaterCollider.GetComponent<CharacterSwimmingVisualOrientation>();

            visualOrientation.AlignToColliderRotation(
                _underwaterCollider.AcceptedRotation,
                characterRigidbody.rotation,
                swimmingAnimationBlend01: 0f,
                fixedDeltaTime: 0.05f);

            Assert.That(Vector3.Dot(meshTransform.up, characterForwardDirection), Is.GreaterThan(0.5f));
            Assert.That(Mathf.Abs(Vector3.Dot(meshTransform.up, characterRightDirection)), Is.LessThan(0.01f));

            visualOrientation.AlignToColliderRotation(
                _underwaterCollider.AcceptedRotation,
                characterRigidbody.rotation,
                swimmingAnimationBlend01: 0f,
                fixedDeltaTime: 1f);
            Assert.That(Vector3.Dot(meshTransform.up, Vector3.down), Is.GreaterThan(0.999f));
        }

        [Test]
        public void AutomaticFloatAfterVerticalDive_RetracesDivePitchWithoutRolling()
        {
            _underwaterCollider.transform.rotation = Quaternion.Euler(0f, 73f, 0f);
            Physics.SyncTransforms();
            Rigidbody characterRigidbody = _underwaterCollider.GetComponent<Rigidbody>();
            Transform meshTransform = _underwaterCollider.transform.Find("Mesh");
            Vector3 characterForwardDirection = characterRigidbody.rotation * Vector3.forward;
            Vector3 characterRightDirection = characterRigidbody.rotation * Vector3.right;
            Assert.That(_underwaterCollider.TryActivate(Vector3.down), Is.True);
            CharacterSwimmingVisualOrientation visualOrientation = _underwaterCollider.GetComponent<CharacterSwimmingVisualOrientation>();
            visualOrientation.AlignToColliderRotation(
                _underwaterCollider.AcceptedRotation,
                characterRigidbody.rotation,
                swimmingAnimationBlend01: 1f,
                fixedDeltaTime: 1f);
            Assert.That(Vector3.Dot(meshTransform.forward, Vector3.down), Is.GreaterThan(0.999f));
            Assert.That(_underwaterCollider.TryAlign(Vector3.up, 0.02f, 360f), Is.True);

            visualOrientation.AlignToColliderRotation(
                _underwaterCollider.AcceptedRotation,
                characterRigidbody.rotation,
                swimmingAnimationBlend01: 1f,
                fixedDeltaTime: 0.02f);

            Assert.That(Vector3.Dot(_underwaterCollider.AcceptedDirection, characterForwardDirection), Is.GreaterThan(0.1f));
            Assert.That(Vector3.Dot(meshTransform.forward, characterForwardDirection), Is.GreaterThan(0f));
            Assert.That(Mathf.Abs(Vector3.Dot(meshTransform.forward, characterRightDirection)), Is.LessThan(0.01f));
            Assert.That(Vector3.Dot(meshTransform.right, characterRightDirection), Is.GreaterThan(0.999f));

            for (int alignmentStepIndex = 1; alignmentStepIndex < 25; alignmentStepIndex++)
            {
                Assert.That(_underwaterCollider.TryAlign(Vector3.up, 0.02f, 360f), Is.True);
                visualOrientation.AlignToColliderRotation(
                    _underwaterCollider.AcceptedRotation,
                    characterRigidbody.rotation,
                    swimmingAnimationBlend01: 1f,
                    fixedDeltaTime: 0.02f);
            }

            visualOrientation.AlignToColliderRotation(
                _underwaterCollider.AcceptedRotation,
                characterRigidbody.rotation,
                swimmingAnimationBlend01: 1f,
                fixedDeltaTime: 1f);
            Assert.That(Vector3.Dot(_underwaterCollider.AcceptedDirection, Vector3.up), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(meshTransform.forward, Vector3.up), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(characterRigidbody.rotation * Vector3.forward, characterForwardDirection), Is.GreaterThan(0.999f));
        }

        [Test]
        public void ProductionAnimator_DoesNotConsumeRootMotion()
        {
            Animator animator = _playerInstance.GetComponentInChildren<Animator>(true);

            Assert.That(animator.applyRootMotion, Is.False);
        }

        [Test]
        public void UnderwaterVisualOrientation_AtSwimmingBlend_UsesAnimatedForwardAxis()
        {
            Assert.That(_underwaterCollider.TryActivate(Vector3.right), Is.True);
            CharacterSwimmingVisualOrientation visualOrientation = _underwaterCollider.GetComponent<CharacterSwimmingVisualOrientation>();

            visualOrientation.AlignToColliderRotation(
                _underwaterCollider.AcceptedRotation,
                _underwaterCollider.GetComponent<Rigidbody>().rotation,
                swimmingAnimationBlend01: 1f,
                fixedDeltaTime: 1f);

            Transform meshTransform = _underwaterCollider.transform.Find("Mesh");
            Assert.That(Vector3.Dot(meshTransform.forward, Vector3.right), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(meshTransform.up, Vector3.up), Is.GreaterThan(0.999f));
        }

        [Test]
        public void DownwardFloorCollision_WithContinuedSwimIntent_PreservesVisualRotation()
        {
            PlaceCharacter(new Vector3(0f, _testPoolFloorTopHeightMeters + 1.02f, 0f));
            Assert.That(_underwaterCollider.TryActivate(Vector3.down), Is.True);
            CharacterSwimmingVisualOrientation visualOrientation = _underwaterCollider.GetComponent<CharacterSwimmingVisualOrientation>();
            Rigidbody characterRigidbody = _underwaterCollider.GetComponent<Rigidbody>();
            Transform meshTransform = _underwaterCollider.transform.Find("Mesh");
            var motionSolver = new SwimmingMotionSolver();

            visualOrientation.AlignToColliderRotation(
                _underwaterCollider.AcceptedRotation,
                characterRigidbody.rotation,
                swimmingAnimationBlend01: 1f,
                fixedDeltaTime: 1f);
            Quaternion downwardSwimmingRotation = meshTransform.localRotation;

            bool didHitFloor = characterRigidbody.SweepTest(
                Vector3.down,
                out RaycastHit hit,
                0.05f,
                QueryTriggerInteraction.Ignore);
            Vector3 collisionResolvedVelocity = motionSolver.ProjectVelocityOnCollisionPlane(
                Vector3.down * 3f,
                hit.normal);
            float animationSpeedMetersPerSecond = motionSolver.MoveAnimationSpeedMetersPerSecond(
                currentAnimationSpeedMetersPerSecond: 3f,
                requestedDirectionMagnitude: 1f,
                requestedSpeedMetersPerSecond: 3f,
                accelerationMetersPerSecondSquared: 10f,
                decelerationMetersPerSecondSquared: 8f,
                fixedDeltaTime: Time.fixedDeltaTime);
            characterRigidbody.linearVelocity = collisionResolvedVelocity;
            visualOrientation.AlignToColliderRotation(
                _underwaterCollider.AcceptedRotation,
                characterRigidbody.rotation,
                animationSpeedMetersPerSecond / 3f,
                fixedDeltaTime: Time.fixedDeltaTime);

            Assert.That(didHitFloor, Is.True);
            Assert.That(hit.collider.name, Is.EqualTo("Pool Bottom"));
            Assert.That(Vector3.Dot(_underwaterCollider.AcceptedDirection, Vector3.down), Is.GreaterThan(0.999f));
            Assert.That(characterRigidbody.linearVelocity.magnitude, Is.LessThan(0.001f));
            Assert.That(animationSpeedMetersPerSecond, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(Quaternion.Angle(meshTransform.localRotation, downwardSwimmingRotation), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator SwimmingAnimation_OverMultipleCycles_DoesNotMoveModelAwayFromRoot()
        {
            PlaceCharacter(new Vector3(0f, _testPoolSurfaceHeightMeters - 0.5f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            var stateDriver = _underwaterCollider.GetComponent<CharacterStateMachine.CharacterStateMachineDriver>();
            stateDriver.enabled = false;
            CharacterAnimator characterAnimator = _underwaterCollider.GetComponent<CharacterAnimator>();
            characterAnimator.UpdateLocomotionAnimationParameter(3f);
            Animator unityAnimator = _playerInstance.GetComponentInChildren<Animator>(true);
            unityAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Vector3 authoredLocalPosition = unityAnimator.transform.localPosition;

            yield return new WaitForSeconds(4.6f);

            Assert.That(Vector3.Distance(unityAnimator.transform.localPosition, authoredLocalPosition), Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator UnderwaterSwimmingAnimation_PointsVisibleHeadAlongAcceptedDirection()
        {
            PlaceCharacter(new Vector3(0f, _testPoolSurfaceHeightMeters - 2f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            var stateDriver = _underwaterCollider.GetComponent<CharacterStateMachine.CharacterStateMachineDriver>();
            stateDriver.enabled = false;
            CharacterAnimator characterAnimator = _underwaterCollider.GetComponent<CharacterAnimator>();
            Animator animator = _playerInstance.GetComponentInChildren<Animator>(true);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            characterAnimator.UpdateLocomotionAnimationParameter(3f);
            CharacterSwimmingVisualOrientation visualOrientation = _underwaterCollider.GetComponent<CharacterSwimmingVisualOrientation>();
            visualOrientation.AlignToColliderRotation(
                _underwaterCollider.AcceptedRotation,
                _underwaterCollider.GetComponent<Rigidbody>().rotation,
                swimmingAnimationBlend01: 1f,
                fixedDeltaTime: 1f);

            yield return new WaitForSeconds(0.5f);

            Vector3 hipsToHeadDirection = (
                animator.GetBoneTransform(HumanBodyBones.Head).position
                - animator.GetBoneTransform(HumanBodyBones.Hips).position).normalized;
            Assert.That(Vector3.Dot(hipsToHeadDirection, _underwaterCollider.AcceptedDirection), Is.GreaterThan(0.85f));
            Assert.That(Vector3.Dot(hipsToHeadDirection, Vector3.down), Is.LessThan(0.5f));
        }

        [UnityTest]
        public IEnumerator LeavingWaterVolume_ClearsSwimmingAndRestoresUprightCollider()
        {
            Transform meshTransform = _underwaterCollider.transform.Find("Mesh");
            Quaternion authoredLocalRotation = meshTransform.localRotation;
            PlaceCharacter(new Vector3(0f, _testPoolSurfaceHeightMeters - 2f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(_underwaterCollider.IsActive, Is.True);

            PlaceCharacter(new Vector3(20f, _testPoolSurfaceHeightMeters - 2f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            CharacterWaterSensor waterSensor = _underwaterCollider.GetComponent<CharacterWaterSensor>();
            Assert.That(waterSensor.HasWaterVolume, Is.False);
            Assert.That(waterSensor.IsSufficientlyImmersed, Is.False);
            Assert.That(_underwaterCollider.IsActive, Is.False);
            Assert.That(_underwaterCollider.GetComponent<CharacterColliderShape>().IsPhysicsEnabled, Is.True);
            Assert.That(_underwaterCollider.GetComponent<BaseCharacterInput>().AreTerrestrialActionsEnabled, Is.True);
            Assert.That(Quaternion.Angle(meshTransform.localRotation, authoredLocalRotation), Is.LessThan(0.1f));
        }

        [UnityTest]
        public IEnumerator LeavingWaterNearPoolFloor_RecoversSmoothlyAndRestoresTerrestrialControl()
        {
            PlaceCharacter(new Vector3(0f, _testPoolSurfaceHeightMeters - 2f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(_underwaterCollider.IsActive, Is.True);
            Assert.That(_underwaterCollider.TryAlign(Vector3.forward, 1f, 360f), Is.True);

            Vector3 blockedExitPosition = new Vector3(
                0f,
                _testPoolFloorTopHeightMeters + 0.46f,
                -4.55f);
            PlaceCharacter(blockedExitPosition);

            bool wasRecoveryObserved = false;
            CharacterColliderShape[] colliderShapes = _underwaterCollider.GetComponentsInChildren<CharacterColliderShape>(true);
            for (int fixedStepIndex = 0;
                 fixedStepIndex < MAXIMUM_EXIT_RECOVERY_FIXED_STEPS && _underwaterCollider.IsActive;
                 fixedStepIndex++)
            {
                Vector3 previousPosition = _underwaterCollider.transform.position;
                yield return new WaitForFixedUpdate();

                float displacementMeters = Vector3.Distance(previousPosition, _underwaterCollider.transform.position);
                float maximumStepDistanceMeters = EXIT_RECOVERY_SPEED_METERS_PER_SECOND
                    * Time.fixedDeltaTime
                    + 0.01f;
                Assert.That(displacementMeters, Is.LessThanOrEqualTo(maximumStepDistanceMeters));
                Assert.That(CountEnabledColliderShapes(colliderShapes), Is.EqualTo(1));
                wasRecoveryObserved |= _underwaterCollider.IsTerrestrialExitRecoveryActive;
            }

            CharacterWaterSensor waterSensor = _underwaterCollider.GetComponent<CharacterWaterSensor>();
            Assert.That(wasRecoveryObserved, Is.True);
            Assert.That(waterSensor.IsSufficientlyImmersed, Is.False);
            Assert.That(_underwaterCollider.IsActive, Is.False);
            Assert.That(_underwaterCollider.transform.position.y, Is.GreaterThan(blockedExitPosition.y + 0.4f));
            Assert.That(_underwaterCollider.GetComponent<CharacterColliderShape>().IsPhysicsEnabled, Is.True);
            Assert.That(_underwaterCollider.GetComponent<BaseCharacterInput>().AreTerrestrialActionsEnabled, Is.True);
            Assert.That(CountEnabledColliderShapes(colliderShapes), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator LosingWaterNearFloorAndWall_UsesContactNormalForDiagonalRecovery()
        {
            PlaceCharacter(new Vector3(0f, _testPoolSurfaceHeightMeters - 2f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(_underwaterCollider.IsActive, Is.True);

            Vector3 blockedExitPosition = new Vector3(
                -1.36f,
                _testPoolFloorTopHeightMeters + 0.46f,
                0f);
            PlaceCharacter(blockedExitPosition);
            _waterVolume.GetComponent<Collider>().enabled = false;
            Physics.SyncTransforms();

            for (int fixedStepIndex = 0;
                 fixedStepIndex < MAXIMUM_EXIT_RECOVERY_FIXED_STEPS && _underwaterCollider.IsActive;
                 fixedStepIndex++)
            {
                yield return new WaitForFixedUpdate();
            }

            Vector3 recoveredPosition = _underwaterCollider.transform.position;
            Assert.That(_underwaterCollider.IsActive, Is.False);
            Assert.That(recoveredPosition.y, Is.GreaterThan(blockedExitPosition.y + 0.4f));
            Assert.That(recoveredPosition.x, Is.GreaterThan(blockedExitPosition.x + 0.01f));
            Assert.That(_underwaterCollider.GetComponent<BaseCharacterInput>().AreTerrestrialActionsEnabled, Is.True);
        }

        [Test]
        public void TerrestrialExitRecovery_InFullyConstrainedSpace_RetainsUnderwaterControlAndCollider()
        {
            Vector3 blockedExitPosition = new Vector3(
                0f,
                _testPoolFloorTopHeightMeters + 0.46f,
                -4.55f);
            PlaceCharacter(blockedExitPosition);
            Assert.That(_underwaterCollider.TryActivate(Vector3.forward), Is.True);
            Transform lowOverhang = _poolInstance.transform.Find("Low Overhang");
            lowOverhang.position = blockedExitPosition + Vector3.up * 0.9f;
            lowOverhang.localScale = new Vector3(10f, 0.4f, 10f);
            Physics.SyncTransforms();

            bool didBeginRecovery = _underwaterCollider.TryBeginTerrestrialExitRecovery();

            CharacterColliderShape[] colliderShapes = _underwaterCollider.GetComponentsInChildren<CharacterColliderShape>(true);
            Assert.That(didBeginRecovery, Is.False);
            Assert.That(_underwaterCollider.IsTerrestrialExitRecoveryActive, Is.False);
            Assert.That(_underwaterCollider.IsActive, Is.True);
            Assert.That(CountEnabledColliderShapes(colliderShapes), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MovingObstruction_InvalidatesRecoveryTargetAndReplansAfterClearanceReturns()
        {
            PlaceCharacter(new Vector3(0f, _testPoolSurfaceHeightMeters - 2f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Vector3 blockedExitPosition = new Vector3(
                0f,
                _testPoolFloorTopHeightMeters + 0.46f,
                -4.55f);
            PlaceCharacter(blockedExitPosition);
            for (int fixedStepIndex = 0;
                 fixedStepIndex < MAXIMUM_EXIT_RECOVERY_FIXED_STEPS
                 && !_underwaterCollider.IsTerrestrialExitRecoveryActive;
                 fixedStepIndex++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.That(_underwaterCollider.IsTerrestrialExitRecoveryActive, Is.True);

            Transform lowOverhang = _poolInstance.transform.Find("Low Overhang");
            lowOverhang.position = _underwaterCollider.transform.position + Vector3.up * 0.9f;
            lowOverhang.localScale = new Vector3(3f, 0.4f, 3f);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();

            Assert.That(_underwaterCollider.IsActive, Is.True);
            Assert.That(_underwaterCollider.IsTerrestrialExitRecoveryActive, Is.False);

            lowOverhang.position = Vector3.right * 20f + Vector3.up * TEST_POOL_ROOT_HEIGHT_METERS;
            Physics.SyncTransforms();
            for (int fixedStepIndex = 0;
                 fixedStepIndex < MAXIMUM_EXIT_RECOVERY_FIXED_STEPS && _underwaterCollider.IsActive;
                 fixedStepIndex++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(_underwaterCollider.IsActive, Is.False);
            Assert.That(_underwaterCollider.GetComponent<BaseCharacterInput>().AreTerrestrialActionsEnabled, Is.True);
        }

        [UnityTest]
        public IEnumerator HorizontalUnderwaterExit_PreservesSwimmingWorldHeadingDuringUprightHandoff()
        {
            PlaceCharacter(new Vector3(0f, _testPoolSurfaceHeightMeters - 2f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(_underwaterCollider.IsActive, Is.True);

            Rigidbody characterRigidbody = _underwaterCollider.GetComponent<Rigidbody>();
            Vector3 swimmingDirection = characterRigidbody.rotation * Vector3.right;
            Assert.That(_underwaterCollider.TryAlign(swimmingDirection, 1f, 360f), Is.True);
            CharacterSwimmingVisualOrientation visualOrientation = _underwaterCollider.GetComponent<CharacterSwimmingVisualOrientation>();
            visualOrientation.AlignToColliderRotation(
                _underwaterCollider.AcceptedRotation,
                characterRigidbody.rotation,
                swimmingAnimationBlend01: 1f,
                fixedDeltaTime: 1f);

            PlaceCharacter(new Vector3(20f, _testPoolSurfaceHeightMeters - 2f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Transform meshTransform = _underwaterCollider.transform.Find("Mesh");
            Vector3 uprightWorldHeading = Vector3.ProjectOnPlane(meshTransform.forward, Vector3.up).normalized;
            Assert.That(_underwaterCollider.IsActive, Is.False);
            Assert.That(Vector3.Dot(characterRigidbody.rotation * Vector3.forward, swimmingDirection), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(uprightWorldHeading, swimmingDirection), Is.GreaterThan(0.999f));
        }

        [UnityTest]
        public IEnumerator UnderwaterToSurface_PreservesWorldPoseDuringHeadingHandoff()
        {
            PlaceCharacter(new Vector3(0f, _testPoolSurfaceHeightMeters - 2f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(_underwaterCollider.IsActive, Is.True);

            Rigidbody characterRigidbody = _underwaterCollider.GetComponent<Rigidbody>();
            Vector3 horizontalHeading = characterRigidbody.rotation * Vector3.right;
            Vector3 ascendingSwimmingDirection = (horizontalHeading + Vector3.up).normalized;
            Assert.That(_underwaterCollider.TryAlign(ascendingSwimmingDirection, 1f, 360f), Is.True);
            CharacterSwimmingVisualOrientation visualOrientation = _underwaterCollider.GetComponent<CharacterSwimmingVisualOrientation>();
            Transform meshTransform = _underwaterCollider.transform.Find("Mesh");
            Quaternion authoredMeshLocalRotation = meshTransform.localRotation;
            visualOrientation.AlignToColliderRotation(
                _underwaterCollider.AcceptedRotation,
                characterRigidbody.rotation,
                swimmingAnimationBlend01: 1f,
                fixedDeltaTime: 1f);
            Quaternion underwaterWorldRotation = meshTransform.rotation;

            CharacterSwimmingMovement swimmingMovement = _underwaterCollider.GetComponent<CharacterSwimmingMovement>();
            PlaceCharacter(new Vector3(0f, swimmingMovement.SurfaceTargetRootHeightMeters, 0f));
            yield return new WaitForFixedUpdate();

            CharacterAnimator animator = _underwaterCollider.GetComponent<CharacterAnimator>();
            Assert.That(_underwaterCollider.IsActive, Is.False);
            Assert.That(animator.CurrentTag.name, Is.EqualTo("Surface Swimming State"));
            Quaternion uprightWorldRotation = characterRigidbody.rotation * authoredMeshLocalRotation;
            float underwaterToUprightAngle = Quaternion.Angle(underwaterWorldRotation, uprightWorldRotation);
            float recoveredToUprightAngle = Quaternion.Angle(meshTransform.rotation, uprightWorldRotation);
            Assert.That(Vector3.Dot(characterRigidbody.rotation * Vector3.forward, horizontalHeading), Is.GreaterThan(0.999f));
            Assert.That(recoveredToUprightAngle, Is.LessThan(underwaterToUprightAngle));
        }

        [Test]
        public void TryDeactivate_UnderProductionOverhang_RemainsUnderwater()
        {
            PlaceCharacter(new Vector3(3f, TEST_POOL_ROOT_HEIGHT_METERS - 1.4f, -1f));
            Assert.That(_underwaterCollider.TryActivate(Vector3.forward), Is.True);

            bool didDeactivate = _underwaterCollider.TryDeactivate();

            Assert.That(didDeactivate, Is.False);
            Assert.That(_underwaterCollider.IsActive, Is.True);
            Assert.That(_underwaterCollider.IsTerrestrialExitRecoveryActive, Is.False);
        }

        [Test]
        public void TryDeactivate_InClearWater_RestoresOnlyUprightColliderShape()
        {
            CharacterColliderShape uprightCollider = _underwaterCollider.GetComponent<CharacterColliderShape>();
            Assert.That(_underwaterCollider.TryActivate(Vector3.forward), Is.True);

            bool didDeactivate = _underwaterCollider.TryDeactivate();

            Assert.That(didDeactivate, Is.True);
            Assert.That(_underwaterCollider.IsActive, Is.False);
            Assert.That(uprightCollider.IsPhysicsEnabled, Is.True);
            Assert.That(Vector3.Dot(_underwaterCollider.AcceptedDirection, Vector3.up), Is.GreaterThan(0.999f));
        }

        [Test]
        public void ReactivateWithoutInput_AfterDownwardSwim_UsesVerticalDefault()
        {
            Assert.That(_underwaterCollider.TryActivate(Vector3.down), Is.True);
            Assert.That(_underwaterCollider.TryDeactivate(), Is.True);

            bool didReactivate = _underwaterCollider.TryActivate(Vector3.zero);

            Assert.That(didReactivate, Is.True);
            Assert.That(Vector3.Dot(_underwaterCollider.AcceptedDirection, Vector3.up), Is.GreaterThan(0.999f));
        }

        [Test]
        public void TryAlign_TowardSubmergedWall_RetainsLastCollisionSafeDirection()
        {
            PlaceCharacter(new Vector3(-1.25f, TEST_POOL_ROOT_HEIGHT_METERS - 2f, 0f));
            Assert.That(_underwaterCollider.TryActivate(Vector3.forward), Is.True);
            Vector3 previousDirection = _underwaterCollider.AcceptedDirection;

            bool didAlign = _underwaterCollider.TryAlign(Vector3.left, 1f, 360f);

            Assert.That(didAlign, Is.False);
            Assert.That(Vector3.Dot(_underwaterCollider.AcceptedDirection, previousDirection), Is.GreaterThan(0.999f));
        }

        [Test]
        public void HorizontalCapsuleSweep_DetectsThinWallBeyondUprightRadius()
        {
            PlaceCharacter(new Vector3(-1.25f, TEST_POOL_ROOT_HEIGHT_METERS - 2f, 0f));
            Assert.That(_underwaterCollider.TryActivate(Vector3.forward), Is.True);
            Rigidbody characterRigidbody = _underwaterCollider.GetComponent<Rigidbody>();

            bool didHit = characterRigidbody.SweepTest(
                Vector3.left,
                out RaycastHit hit,
                1f,
                QueryTriggerInteraction.Ignore);

            Assert.That(didHit, Is.True);
            Assert.That(hit.collider.name, Is.EqualTo("Submerged Thin Wall"));
            Assert.That(hit.distance, Is.LessThan(0.3f));
        }

        private void PlaceCharacter(Vector3 worldPosition)
        {
            _underwaterCollider.transform.position = worldPosition;
            _underwaterCollider.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        private void FindProductionInputAndSettings()
        {
            SwimmingMovementSettingsSO[] settingsAssets = Resources.FindObjectsOfTypeAll<SwimmingMovementSettingsSO>();
            foreach (SwimmingMovementSettingsSO settingsAsset in settingsAssets)
            {
                if (settingsAsset.name == "Swimming Movement Settings")
                {
                    _settingsSO = settingsAsset;
                    break;
                }
            }

            Assert.That(_settingsSO, Is.Not.Null);
            _originalControlMode = _settingsSO.ControlMode;
            _settingsSO.SetControlMode(SwimmingControlMode.CameraDirected);

            InputActionAsset[] inputActionAssets = Resources.FindObjectsOfTypeAll<InputActionAsset>();
            foreach (InputActionAsset inputActionAsset in inputActionAssets)
            {
                InputActionMap gameplayActionMap = inputActionAsset.FindActionMap("Gameplay", throwIfNotFound: false);
                if (gameplayActionMap?.FindAction("Dive", throwIfNotFound: false) == null)
                {
                    continue;
                }

                _gameplayActionMap = gameplayActionMap;
                break;
            }

            Assert.That(_gameplayActionMap, Is.Not.Null);
            _wasGameplayActionMapEnabled = _gameplayActionMap.enabled;
            _gameplayActionMap.Enable();

            _keyboard = Keyboard.current;
            if (_keyboard == null)
            {
                _keyboard = InputSystem.AddDevice<Keyboard>();
                _wasKeyboardAddedForTest = true;
            }

            _gamepad = InputSystem.AddDevice<Gamepad>();
            _wasGamepadAddedForTest = true;
        }

        private void PressKeyboardKey(Key key)
        {
            var keyboardState = new KeyboardState(key);
            InputSystem.QueueStateEvent(_keyboard, keyboardState);
            InputSystem.Update();
        }

        private void ReleaseAllKeyboardKeys()
        {
            if (_keyboard == null || !_keyboard.added)
            {
                return;
            }

            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
        }

        private static int CountEnabledColliderShapes(CharacterColliderShape[] colliderShapes)
        {
            int enabledColliderCount = 0;
            foreach (CharacterColliderShape colliderShape in colliderShapes)
            {
                if (colliderShape.IsPhysicsEnabled)
                {
                    enabledColliderCount++;
                }
            }

            return enabledColliderCount;
        }

        private static void ReleaseAddressableAsset(AsyncOperationHandle<GameObject> handle)
        {
            Addressables.Release(handle);
        }
    }
}

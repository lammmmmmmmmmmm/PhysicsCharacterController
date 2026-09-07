namespace PhysicsCharacterController.CharacterStateMachine
{
    public sealed class CharacterStateContext
    {
        public CharacterMove CharacterMove { get; }
        public CharacterCrouch CharacterCrouch { get; }
        public CharacterJump CharacterJump { get; }
        public CharacterGravity CharacterGravity { get; }
        public GroundChecker GroundChecker { get; }
        public BaseCharacterInput Input { get; }
        public CharacterRotationPolicy CharacterRotationPolicy { get; }
        public CharacterWaterSensor WaterSensor { get; }
        public CharacterSwimmingMovement SwimmingMovement { get; }
        public CharacterAnimator Animator { get; }
        public LocomotionAnimationDataSO StandingAnimationData { get; }
        public LocomotionAnimationDataSO CrouchingAnimationData { get; }
        public AirborneAnimationDataSO AirborneAnimationData { get; }
        public LocomotionAnimationDataSO SurfaceSwimmingAnimationData { get; }
        public LocomotionAnimationDataSO UnderwaterSwimmingAnimationData { get; }
        public StateId StandingStateId { get; }
        public StateId CrouchingStateId { get; }
        public StateId AirborneStateId { get; }
        public StateId SurfaceSwimmingStateId { get; }
        public StateId UnderwaterSwimmingStateId { get; }

        public CharacterStateContext(
            CharacterMove characterMove,
            CharacterCrouch characterCrouch,
            CharacterJump characterJump,
            CharacterGravity characterGravity,
            GroundChecker groundChecker,
            BaseCharacterInput input,
            CharacterRotationPolicy characterRotationPolicy,
            CharacterWaterSensor waterSensor,
            CharacterSwimmingMovement swimmingMovement,
            CharacterAnimator animator,
            LocomotionAnimationDataSO standingAnimationData,
            LocomotionAnimationDataSO crouchingAnimationData,
            AirborneAnimationDataSO airborneAnimationData,
            LocomotionAnimationDataSO surfaceSwimmingAnimationData,
            LocomotionAnimationDataSO underwaterSwimmingAnimationData,
            StateId standingStateId,
            StateId crouchingStateId,
            StateId airborneStateId,
            StateId surfaceSwimmingStateId,
            StateId underwaterSwimmingStateId)
        {
            CharacterMove = characterMove;
            CharacterCrouch = characterCrouch;
            CharacterJump = characterJump;
            CharacterGravity = characterGravity;
            GroundChecker = groundChecker;
            Input = input;
            CharacterRotationPolicy = characterRotationPolicy;
            WaterSensor = waterSensor;
            SwimmingMovement = swimmingMovement;
            Animator = animator;
            StandingAnimationData = standingAnimationData;
            CrouchingAnimationData = crouchingAnimationData;
            AirborneAnimationData = airborneAnimationData;
            SurfaceSwimmingAnimationData = surfaceSwimmingAnimationData;
            UnderwaterSwimmingAnimationData = underwaterSwimmingAnimationData;
            StandingStateId = standingStateId;
            CrouchingStateId = crouchingStateId;
            AirborneStateId = airborneStateId;
            SurfaceSwimmingStateId = surfaceSwimmingStateId;
            UnderwaterSwimmingStateId = underwaterSwimmingStateId;
        }

        public bool IsGrounded => GroundChecker.IsGrounded;
        public bool IsGroundedInShallowWater => WaterSensor.ShouldUseTerrestrialMovementInShallowWater(IsGrounded, GroundChecker.GroundHit.point.y);
    }
}

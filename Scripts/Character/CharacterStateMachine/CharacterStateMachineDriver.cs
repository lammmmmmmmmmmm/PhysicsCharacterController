using System;
using System.Linq;
using HSM;
using PhysicsCharacterController.CharacterStateMachine.States;
using UnityEngine;

namespace PhysicsCharacterController.CharacterStateMachine
{
    [RequireComponent(typeof(CharacterMove))]
    [RequireComponent(typeof(CharacterCrouch))]
    [RequireComponent(typeof(CharacterJump))]
    [RequireComponent(typeof(CharacterGravity))]
    [RequireComponent(typeof(GroundChecker))]
    public sealed class CharacterStateMachineDriver : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private CharacterMove _characterMove;
        [SerializeField] private CharacterCrouch _characterCrouch;
        [SerializeField] private CharacterJump _characterJump;
        [SerializeField] private CharacterGravity _characterGravity;
        [SerializeField] private GroundChecker _groundChecker;
        [SerializeField] private BaseCharacterInput _input;
        [SerializeField] private CharacterRotationPolicy _characterRotationPolicy;
        [SerializeField] private CharacterWaterSensor _waterSensor;
        [SerializeField] private CharacterSwimmingMovement _swimmingMovement;
        [SerializeField] private CharacterAnimator _characterAnimator;

        [Header("Animation Data")]
        [SerializeField] private LocomotionAnimationDataSO _standingAnimationData;
        [SerializeField] private LocomotionAnimationDataSO _crouchingAnimationData;
        [SerializeField] private AirborneAnimationDataSO _airborneAnimationData;
        [SerializeField] private LocomotionAnimationDataSO _surfaceSwimmingAnimationData;
        [SerializeField] private LocomotionAnimationDataSO _underwaterSwimmingAnimationData;

        [Header("State Ids")]
        [SerializeField] private StateId _standingStateId;
        [SerializeField] private StateId _crouchingStateId;
        [SerializeField] private StateId _airborneStateId;
        [SerializeField] private StateId _surfaceSwimmingStateId;
        [SerializeField] private StateId _underwaterSwimmingStateId;

        [Header("Debug")]
        [SerializeField] private bool _logActiveStatePath;

        private CharacterStateContext _context;
        private StateMachine _machine;
        private CharacterRootState _root;
        private string _lastStatePath;

        public bool IsSwimming => _root != null && _root.ActiveChild == _root.Swimming;
        public event Action<bool> OnSwimmingStateChanged;

        private void Reset()
        {
            _characterMove = GetComponent<CharacterMove>();
            _characterCrouch = GetComponent<CharacterCrouch>();
            _characterJump = GetComponent<CharacterJump>();
            _characterGravity = GetComponent<CharacterGravity>();
            _groundChecker = GetComponent<GroundChecker>();
            _input = GetComponent<BaseCharacterInput>();
            _characterRotationPolicy = GetComponentInParent<CharacterRotationPolicy>();
            _waterSensor = GetComponent<CharacterWaterSensor>();
            _swimmingMovement = GetComponent<CharacterSwimmingMovement>();
        }

        private void Awake()
        {
            _context = new CharacterStateContext(
                _characterMove,
                _characterCrouch,
                _characterJump,
                _characterGravity,
                _groundChecker,
                _input,
                _characterRotationPolicy,
                _waterSensor,
                _swimmingMovement,
                _characterAnimator,
                _standingAnimationData,
                _crouchingAnimationData,
                _airborneAnimationData,
                _surfaceSwimmingAnimationData,
                _underwaterSwimmingAnimationData,
                _standingStateId,
                _crouchingStateId,
                _airborneStateId,
                _surfaceSwimmingStateId,
                _underwaterSwimmingStateId);

            _root = new CharacterRootState(null, _context);
            _machine = new StateMachineBuilder(_root).Build();
            _root.Swimming.OnSwimmingStateChanged += ForwardSwimmingStateChanged;
        }

        private void OnDestroy()
        {
            if (_root == null)
            {
                return;
            }

            _root.Swimming.OnSwimmingStateChanged -= ForwardSwimmingStateChanged;
        }

        private void FixedUpdate()
        {
            _machine.FixedTick(Time.fixedDeltaTime);
            LogStatePathIfChanged();
        }

        private void ForwardSwimmingStateChanged(bool isSwimming)
        {
            OnSwimmingStateChanged?.Invoke(isSwimming);
        }

        private void LogStatePathIfChanged()
        {
            if (!_logActiveStatePath)
            {
                return;
            }

            string statePath = string.Join(
                " > ",
                _machine.Root.Leaf().PathToRoot().Reverse().Select(state => state.GetType().Name));

            if (statePath == _lastStatePath)
            {
                return;
            }

            Debug.Log($"Character State: {statePath}");
            _lastStatePath = statePath;
        }
    }
}

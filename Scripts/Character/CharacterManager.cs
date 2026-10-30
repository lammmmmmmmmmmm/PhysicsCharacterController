using UnityEngine;
using UnityEngine.Events;

namespace PhysicsCharacterController
{
    /// <summary>
    /// Coordinates shared character state, landing events, and collider friction.
    /// Collider geometry comes from a CharacterColliderShape component, allowing the controller to
    /// use different physical shapes without leaking shape-specific logic into character behavior.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CharacterMove))]
    [RequireComponent(typeof(CharacterCrouch))]
    [RequireComponent(typeof(CharacterGravity))]
    [RequireComponent(typeof(CharacterRotation))]
    [RequireComponent(typeof(SlopeChecker))]
    [RequireComponent(typeof(CharacterStateMachine.CharacterStateMachineDriver))]
    public class CharacterManager : MonoBehaviour
    {
        [Header("Character components")]
        [SerializeField] private CharacterMove _characterMove;
        [SerializeField] private CharacterCrouch _characterCrouch;

        [Header("Checkers")]
        [SerializeField] private GroundChecker _groundChecker;

        [Header("Events")]
        [SerializeField] private float _minimumVerticalSpeedToLightLandEvent;
        [SerializeField] private UnityEvent _onLightLand;

        private Rigidbody _rigidbody;
        private CharacterColliderShape _characterColliderShape;
        private float _previousYVelocity;

        #region Unity Lifecycle

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _characterColliderShape = GetComponent<CharacterColliderShape>();
        }

        private void Start()
        {
            SetNoFriction();
        }

        private void Update()
        {
            UpdateEvents();
        }

        #endregion

        #region Public Methods

        public bool IsJumping()
        {
            return _rigidbody.linearVelocity.y > 1f;
        }

        public bool IsFalling()
        {
            return _rigidbody.linearVelocity.y < 0f && !_groundChecker.IsGrounded;
        }

        public bool IsRunning()
        {
            return _characterMove.HasMovementInput && !_characterCrouch.IsCrouching && _groundChecker.IsGrounded;
        }

        public float GetOriginalColliderHeight()
        {
            return _characterColliderShape.OriginalHeightMeters;
        }

        #endregion

        #region Private Methods

        private void UpdateEvents()
        {
            if (_groundChecker.JustLanded)
            {
                if (_previousYVelocity < -_minimumVerticalSpeedToLightLandEvent)
                {
                    _onLightLand.Invoke();
                }
            }

            _previousYVelocity = _rigidbody.linearVelocity.y;
        }

        private void SetNoFriction()
        {
            PhysicsMaterial colliderMaterial = _characterColliderShape.PhysicsCollider.material;
            colliderMaterial.dynamicFriction = 0f;
            colliderMaterial.staticFriction = 0f;
            colliderMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;
        }

        #endregion
    }
}

using System;
using UnityEngine;

namespace PhysicsCharacterController
{
    public class CharacterJump : MonoBehaviour
    {
        [Header("Jump Settings")]
        [SerializeField] private float _maxJumpForce = 30f;
        [Tooltip("Time window in seconds to buffer jump input when not grounded")]
        [SerializeField] private float _jumpBufferTime = 0.2f;
        [SerializeField] private float _coyoteTime = 0.2f;

        [Header("References")]
        [SerializeField] private GroundChecker _groundChecker;
        [SerializeField] private SlopeChecker _slopeChecker;
        [SerializeField] private BaseCharacterInput _input;

        public event Action OnJump;

        public bool IsJumpInProgress { get; private set; }

        public float MaxJumpForce
        {
            get => _maxJumpForce;
            set => _maxJumpForce = value;
        }

        public float CurrentJumpForce { get; set; }

        private Rigidbody _rigidbody;
        private float _jumpBufferTimer;
        private bool _hasJumpRequested;
        private bool _hasJumpBuffered;
        private bool _hasWaterSurfaceJumpRequested;
        private bool _hasCoyoteTime;
        private float _coyoteTimeCounter;

        // Sliding down an unclimbable slope constantly toggles grounded state; granting coyote
        // windows there would let the player jump off a surface they are not allowed to jump from.
        private bool ShouldStartCoyoteTime =>
            !_groundChecker.IsGrounded &&
            _groundChecker.WasGrounded &&
            _rigidbody.linearVelocity.y < -0.5f &&
            !_slopeChecker.WasLastGroundedSurfaceUnclimbable;

        #region Unity Lifecycle

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            CurrentJumpForce = _maxJumpForce;
        }

        private void OnEnable()
        {
            _input.OnJumpPressed += HandleJumpInput;
            _input.OnNormalActionsAvailabilityChanged += CancelPendingJumpWhenActionsBecomeUnavailable;
            _input.OnTerrestrialActionsAvailabilityChanged += CancelPendingJumpWhenActionsBecomeUnavailable;
            _input.OnWaterSurfaceJumpsAvailabilityChanged += CancelPendingWaterSurfaceJumpWhenUnavailable;
        }

        private void OnDisable()
        {
            _input.OnJumpPressed -= HandleJumpInput;
            _input.OnNormalActionsAvailabilityChanged -= CancelPendingJumpWhenActionsBecomeUnavailable;
            _input.OnTerrestrialActionsAvailabilityChanged -= CancelPendingJumpWhenActionsBecomeUnavailable;
            _input.OnWaterSurfaceJumpsAvailabilityChanged -= CancelPendingWaterSurfaceJumpWhenUnavailable;
        }

        #endregion

        #region Public Methods

        public bool TryExecuteWaterSurfaceJump()
        {
            if (!_hasWaterSurfaceJumpRequested)
            {
                return false;
            }

            ExecuteJump();
            ResetAllPendingJumpRequests();
            return true;
        }

        public bool TryExecuteJump()
        {
            ClearJumpStateWhenLanded();

            bool shouldExecuteRequestedJump = _hasJumpRequested && CanJumpNow();
            // Evaluate the landing surface from the fresh ground hit: SlopeChecker's cached state
            // is one tick stale on the landing tick, which let buffered jumps fire off
            // unclimbable slopes.
            bool shouldExecuteBufferedJump = _hasJumpBuffered
                                            && _groundChecker.JustLanded
                                            && !_slopeChecker.IsSurfaceUnclimbable(_groundChecker.GroundHit.normal);

            if (!shouldExecuteRequestedJump && !shouldExecuteBufferedJump)
            {
                return false;
            }

            ExecuteJump();
            ResetAllPendingJumpRequests();
            return true;
        }

        public void HandleJumpBuffer(float deltaTime)
        {
            UpdateJumpBufferTimer(deltaTime);
            ClearExpiredBufferOnLanding();
        }

        public void HandleCoyoteTime(float deltaTime)
        {
            if (ShouldStartCoyoteTime)
            {
                _hasCoyoteTime = true;
                _coyoteTimeCounter = _coyoteTime;
            }

            if (!_hasCoyoteTime)
            {
                return;
            }

            _coyoteTimeCounter -= deltaTime;

            if (_coyoteTimeCounter <= 0f)
            {
                _hasCoyoteTime = false;
                _coyoteTimeCounter = 0f;
            }
        }

        #endregion

        #region Private Methods

        private void HandleJumpInput()
        {
            if (_input.AreWaterSurfaceJumpsEnabled)
            {
                _hasWaterSurfaceJumpRequested = true;
                return;
            }

            if (CanJumpNow())
            {
                _hasJumpRequested = true;
            }
            else if (!_groundChecker.IsGrounded)
            {
                _hasJumpBuffered = true;
                _jumpBufferTimer = 0f;
            }
        }

        private void CancelPendingJumpWhenActionsBecomeUnavailable(bool areActionsEnabled)
        {
            if (!areActionsEnabled)
            {
                ResetAllPendingJumpRequests();
            }
        }

        private void CancelPendingWaterSurfaceJumpWhenUnavailable(bool areWaterSurfaceJumpsEnabled)
        {
            if (!areWaterSurfaceJumpsEnabled)
            {
                _hasWaterSurfaceJumpRequested = false;
            }
        }

        private bool CanJumpNow()
        {
            bool canJumpFromGround = _groundChecker.IsGrounded
                                     && !_slopeChecker.IsSurfaceUnclimbable(_groundChecker.GroundHit.normal);
            bool canJumpFromCoyoteWindow = _hasCoyoteTime;
            return canJumpFromGround || canJumpFromCoyoteWindow;
        }

        private void ExecuteJump()
        {
            ResetVerticalVelocity();
            _rigidbody.AddForce(Vector3.up * CurrentJumpForce, ForceMode.VelocityChange);
            IsJumpInProgress = true;
            OnJump?.Invoke();
        }

        private void ClearJumpStateWhenLanded()
        {
            if (_groundChecker.JustLanded)
            {
                IsJumpInProgress = false;
            }
        }

        private void UpdateJumpBufferTimer(float deltaTime)
        {
            if (!_hasJumpBuffered || _groundChecker.IsGrounded)
            {
                return;
            }

            _jumpBufferTimer += deltaTime;

            if (_jumpBufferTimer > _jumpBufferTime)
            {
                ResetJumpBuffer();
            }
        }

        private void ClearExpiredBufferOnLanding()
        {
            if (!_groundChecker.JustLanded)
            {
                return;
            }

            if (!_hasJumpBuffered || _jumpBufferTimer > _jumpBufferTime)
            {
                ResetJumpBuffer();
            }
        }

        private void ResetAllPendingJumpRequests()
        {
            _hasJumpRequested = false;
            _hasWaterSurfaceJumpRequested = false;
            _hasCoyoteTime = false;
            _coyoteTimeCounter = 0f;
            ResetJumpBuffer();
        }

        private void ResetJumpBuffer()
        {
            _hasJumpBuffered = false;
            _jumpBufferTimer = 0f;
        }

        private void ResetVerticalVelocity()
        {
            _rigidbody.linearVelocity = new Vector3(
                _rigidbody.linearVelocity.x,
                0f,
                _rigidbody.linearVelocity.z);
        }

        #endregion
    }
}

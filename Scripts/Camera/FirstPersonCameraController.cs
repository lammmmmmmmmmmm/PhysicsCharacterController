using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PhysicsCharacterController
{
    /// <summary>
    /// Converts camera input into smoothed first-person pan and tilt angles for Cinemachine 3.
    /// Desktop input is applied only while the shared cursor is locked. Mobile and Device Simulator
    /// input ignores cursor state because touch camera control does not use a desktop cursor.
    /// </summary>
    public class FirstPersonCameraController : MonoBehaviour
    {
        [SerializeField] private InputActionReference _cameraActionReference;
        [SerializeField] private LockCursor _lockCursor;

        [Header("Camera controls")]
        [SerializeField] private Vector2 _mouseSensitivity = new(8f, -50f);
        [SerializeField] private float _smoothTimeSeconds = 0.01f;

        private CinemachinePanTilt _panTilt;
        private Vector2 _smoothVelocityDegreesPerSecond;
        private Vector2 _currentAxisDegrees;
        private Vector2 _targetAxisDegrees;

        #region Unity Lifecycle

        private void Awake()
        {
            _panTilt = GetComponent<CinemachinePanTilt>();
        }

        private void Update()
        {
            bool canReadCameraInput = UnityEngine.Device.Application.isMobilePlatform || _lockCursor.IsCursorLocked;
            if (!canReadCameraInput)
            {
                return;
            }

            UpdateTargetAxisDegrees();
            SmoothAndApplyAxisDegrees();
        }

        #endregion

        #region Public Methods

        public void SetInitialValue(float panDegrees, float tiltDegrees)
        {
            _targetAxisDegrees = new Vector2(panDegrees, tiltDegrees);
            _currentAxisDegrees = _targetAxisDegrees;

            ApplyAxisDegrees(_currentAxisDegrees);
        }

        #endregion

        #region Private Methods

        private void UpdateTargetAxisDegrees()
        {
            Vector2 input = _cameraActionReference.action.ReadValue<Vector2>();
            _targetAxisDegrees += input * _mouseSensitivity * new Vector2(0.01f, 0.001f);
            _targetAxisDegrees.y = _panTilt.TiltAxis.ClampValue(_targetAxisDegrees.y);
        }

        private void SmoothAndApplyAxisDegrees()
        {
            _currentAxisDegrees = Vector2.SmoothDamp(_currentAxisDegrees, _targetAxisDegrees, ref _smoothVelocityDegreesPerSecond, _smoothTimeSeconds);

            ApplyAxisDegrees(_currentAxisDegrees);
        }

        private void ApplyAxisDegrees(Vector2 axisDegrees)
        {
            _panTilt.PanAxis.Value = axisDegrees.x;
            _panTilt.TiltAxis.Value = axisDegrees.y;
        }

        #endregion
    }
}

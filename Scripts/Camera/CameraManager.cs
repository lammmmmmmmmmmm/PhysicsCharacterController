using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PhysicsCharacterController
{
    public class CameraManager : MonoBehaviour
    {
        [Header("Camera References")]
        [SerializeField] private CinemachineCamera _firstPersonCamera;
        [SerializeField] private CinemachineCamera _thirdPersonCamera;
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private PlayerCharacterRotationPolicy _playerCharacterRotationPolicy;

        [Header("First Person Settings")]
        [SerializeField] private LayerMask _firstPersonMask;
        [SerializeField] private float _firstPersonMaskChangeDelaySeconds = 0.1f;
        [SerializeField] private float _firstPersonHeightOnTransition;

        [Header("Third Person Settings")]
        [SerializeField] private LayerMask _thirdPersonMask;
        [SerializeField] private float _thirdPersonMaskChangeDelaySeconds = 0.1f;
        [SerializeField] private float _thirdPersonHeightOnTransition = 0.5f;

        [Header("State")]
        [SerializeField] private bool _isThirdPersonActive = true;
        [SerializeField] private Key _toggleKey = Key.F2;

        private FirstPersonCameraController _firstPersonCameraController;
        private ThirdPersonCameraController _thirdPersonCameraController;
        private CinemachineBrain _brain;
        private Coroutine _maskUpdateCoroutine;

        public bool IsThirdPersonActive => _isThirdPersonActive;

        private void Awake()
        {
            CacheComponents();
            ApplyCameraMode(null);
        }

        private void Update()
        {
            HandleCameraToggleInput();
        }

        private void CacheComponents()
        {
            _firstPersonCameraController = _firstPersonCamera.GetComponent<FirstPersonCameraController>();
            _thirdPersonCameraController = _thirdPersonCamera.GetComponent<ThirdPersonCameraController>();
            _brain = _mainCamera.GetComponent<CinemachineBrain>();
        }

        private void HandleCameraToggleInput()
        {
            if (Keyboard.current?[_toggleKey].wasPressedThisFrame == true)
            {
                ToggleCameraMode();
            }
        }

        public void ToggleCameraMode()
        {
            SetThirdPersonMode(!_isThirdPersonActive);
        }

        public void SetThirdPersonMode(bool isThirdPerson)
        {
            if (_isThirdPersonActive == isThirdPerson)
            {
                return;
            }

            _isThirdPersonActive = isThirdPerson;
            ApplyCameraMode(_mainCamera.transform.rotation);
        }

        private void ApplyCameraMode(Quaternion? outgoingRotation)
        {
            StopMaskUpdateCoroutine();

            if (_isThirdPersonActive)
            {
                ActivateThirdPersonCamera(outgoingRotation);
            }
            else
            {
                ActivateFirstPersonCamera(outgoingRotation);
            }
        }

        private void ActivateThirdPersonCamera(Quaternion? outgoingRotation)
        {
            _playerCharacterRotationPolicy.SetLockedToCamera(false);

            _firstPersonCamera.gameObject.SetActive(false);
            _thirdPersonCamera.gameObject.SetActive(true);

            if (outgoingRotation.HasValue)
            {
                _thirdPersonCameraController.MatchLookDirection(_thirdPersonCamera, outgoingRotation.Value, _brain.DefaultWorldUp);
            }
            else
            {
                _thirdPersonCameraController.SetInitialValue(0f, _thirdPersonHeightOnTransition);
            }

            _maskUpdateCoroutine = StartCoroutine(UpdateMaskCoroutine(_thirdPersonMaskChangeDelaySeconds, _thirdPersonMask));
        }

        private void ActivateFirstPersonCamera(Quaternion? outgoingRotation)
        {
            _playerCharacterRotationPolicy.SetLockedToCamera(true);

            _firstPersonCamera.gameObject.SetActive(true);
            _thirdPersonCamera.gameObject.SetActive(false);

            if (outgoingRotation.HasValue)
            {
                _firstPersonCameraController.MatchLookDirection(_firstPersonCamera, outgoingRotation.Value, _brain.DefaultWorldUp);
            }
            else
            {
                _firstPersonCameraController.SetInitialValue(0f, _firstPersonHeightOnTransition);
            }

            _maskUpdateCoroutine = StartCoroutine(UpdateMaskCoroutine(_firstPersonMaskChangeDelaySeconds, _firstPersonMask));
        }

        private IEnumerator UpdateMaskCoroutine(float delaySeconds, LayerMask mask)
        {
            yield return new WaitForSeconds(delaySeconds);
            _mainCamera.cullingMask = mask;
        }

        private void StopMaskUpdateCoroutine()
        {
            if (_maskUpdateCoroutine != null)
            {
                StopCoroutine(_maskUpdateCoroutine);
                _maskUpdateCoroutine = null;
            }
        }
    }
}

using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PhysicsCharacterController
{
	/// <summary>
	/// Converts camera input into smoothed third-person orbital axis values. Desktop input is accepted
	/// only while the shared cursor is locked; mobile and Device Simulator input ignores cursor state
	/// because touch camera control does not use a desktop cursor.
	/// </summary>
	public class ThirdPersonCameraController : MonoBehaviour
	{
		[SerializeField] private InputActionReference _cameraActionReference;
		[SerializeField] private LockCursor _lockCursor;

		[Header("Camera controls")]
		[SerializeField] private Vector2 _mouseSensitivity = new(5f, 1f);
		[SerializeField] private float _smoothSpeed = 0.05f;

		private CinemachineOrbitalFollow _orbitalFollow;

		private Vector2 _smoothVelocity;
		private Vector2 _currentInputVector;
		private Vector2 _input;

		private void Awake()
		{
			CacheOrbitalFollow();
		}

		private void Update()
		{
			bool canReadCameraInput = UnityEngine.Device.Application.isMobilePlatform || _lockCursor.IsCursorLocked;
			if (!canReadCameraInput)
			{
				return;
			}

			_input += _cameraActionReference.action.ReadValue<Vector2>() * _mouseSensitivity * new Vector2(0.01f, 0.001f);

			if (_input.y > 1f) _input.y = 1f;
			else if (_input.y < 0f) _input.y = 0f;

			_currentInputVector = Vector2.SmoothDamp(_currentInputVector, _input, ref _smoothVelocity, _smoothSpeed);
			_orbitalFollow.HorizontalAxis.Value = _currentInputVector.x;
			_orbitalFollow.VerticalAxis.Value = 1 - _currentInputVector.y;
		}

		public void SetInitialValue(float valueX, float valueY)
		{
			CacheOrbitalFollow();
			_input = new Vector2(valueX, valueY);
			_currentInputVector = _input;

			_orbitalFollow.HorizontalAxis.Value = valueX;
			_orbitalFollow.VerticalAxis.Value = valueY;
		}

		private void CacheOrbitalFollow()
		{
			_orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
		}
	}
}

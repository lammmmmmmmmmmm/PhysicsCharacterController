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
		private const int PITCH_SEARCH_STEPS = 12;

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

		/// <summary>
		/// Adds look input on top of the player's own, in orbital axis units: x is horizontal degrees,
		/// y is the normalized vertical axis where larger values look further up. Used by aim assist.
		/// </summary>
		public void AddLookInput(Vector2 axisDelta)
		{
			_input += axisDelta;
			_input.y = Mathf.Clamp01(_input.y);
		}

		public void SetInitialValue(float yawDegrees, float orbitHeight01)
		{
			CacheOrbitalFollow();
			_input = new Vector2(yawDegrees, 1f - orbitHeight01);
			_currentInputVector = _input;
			_smoothVelocity = Vector2.zero;

			_orbitalFollow.HorizontalAxis.Value = yawDegrees;
			_orbitalFollow.VerticalAxis.Value = orbitHeight01;
		}

		public void MatchLookDirection(CinemachineCamera camera, Quaternion worldRotation, Vector3 worldUp)
		{
			CacheOrbitalFollow();
			Vector3 targetForward = worldRotation * Vector3.forward;
			Vector3 targetHeading = Vector3.ProjectOnPlane(targetForward, worldUp);
			float targetElevation = Vector3.Dot(targetForward, worldUp);
			float yawDegrees = _orbitalFollow.HorizontalAxis.Value;
			float orbitHeight01 = _orbitalFollow.VerticalAxis.Value;
			var verticalRecentering = _orbitalFollow.VerticalAxis.Recentering;
			_orbitalFollow.VerticalAxis.Recentering.Enabled = false;
			_orbitalFollow.VerticalAxis.CancelRecentering();

			// The three-ring spline, aim offset, and tracking frame all affect the rendered direction.
			// Evaluate the actual rig to map world-space look back to orbital controls.
			for (int pass = 0; pass < 2; pass++)
			{
				Vector3 forward = EvaluateForward(camera, worldUp, yawDegrees, orbitHeight01);
				float headingErrorDegrees = Vector3.SignedAngle(Vector3.ProjectOnPlane(forward, worldUp), targetHeading, worldUp);
				yawDegrees = _orbitalFollow.HorizontalAxis.ClampValue(yawDegrees + headingErrorDegrees);
				orbitHeight01 = FindOrbitHeight(camera, worldUp, yawDegrees, targetElevation);
			}
			Vector3 matchedForward = EvaluateForward(camera, worldUp, yawDegrees, orbitHeight01);
			float finalHeadingErrorDegrees = Vector3.SignedAngle(Vector3.ProjectOnPlane(matchedForward, worldUp), targetHeading, worldUp);
			yawDegrees = _orbitalFollow.HorizontalAxis.ClampValue(yawDegrees + finalHeadingErrorDegrees);

			SetInitialValue(yawDegrees, orbitHeight01);
			camera.InternalUpdateCameraState(worldUp, -1f);
			_orbitalFollow.VerticalAxis.Recentering = verticalRecentering;
			_orbitalFollow.VerticalAxis.CancelRecentering();
		}

		private float FindOrbitHeight(CinemachineCamera camera, Vector3 worldUp, float yawDegrees, float targetElevation)
		{
			float minimumHeight01 = _orbitalFollow.VerticalAxis.Range.x;
			float maximumHeight01 = _orbitalFollow.VerticalAxis.Range.y;
			for (int step = 0; step < PITCH_SEARCH_STEPS; step++)
			{
				float height01 = (minimumHeight01 + maximumHeight01) * 0.5f;
				float elevation = Vector3.Dot(EvaluateForward(camera, worldUp, yawDegrees, height01), worldUp);
				if (elevation > targetElevation)
					minimumHeight01 = height01;
				else
					maximumHeight01 = height01;
			}
			return (minimumHeight01 + maximumHeight01) * 0.5f;
		}

		private Vector3 EvaluateForward(CinemachineCamera camera, Vector3 worldUp, float yawDegrees, float height01)
		{
			_orbitalFollow.HorizontalAxis.Value = yawDegrees;
			_orbitalFollow.VerticalAxis.Value = height01;
			camera.InternalUpdateCameraState(worldUp, -1f);
			return camera.State.GetFinalOrientation() * Vector3.forward;
		}

		private void CacheOrbitalFollow()
		{
			_orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
		}
	}
}

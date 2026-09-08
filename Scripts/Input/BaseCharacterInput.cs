using System;
using UnityEngine;

namespace PhysicsCharacterController
{
	/// <summary>
	/// Base class for all input controllers (human input, AI, etc.)
	/// Provides common interface for character movement input
	/// </summary>
	public abstract class BaseCharacterInput : MonoBehaviour
	{
		public event Action<Vector2> OnMoveStart;
		public event Action OnMoveStop;
		public event Action OnJumpPressed;
		public event Action<bool> OnSprint;
		public event Action<bool> OnCrouch;
		public event Action<bool> OnNormalActionsAvailabilityChanged;
		public event Action<bool> OnTerrestrialActionsAvailabilityChanged;
		public event Action<bool> OnDiveActionAvailabilityChanged;
		public event Action<bool> OnWaterSurfaceJumpsAvailabilityChanged;

		public bool AreNormalActionsEnabled { get; private set; } = true;
		public bool AreTerrestrialActionsEnabled { get; private set; } = true;
		public bool IsDiveActionEnabled { get; private set; }
		public bool AreWaterSurfaceJumpsEnabled { get; private set; }
		public bool IsSprintRequested { get; private set; }
		public bool IsDiveRequested { get; private set; }

		public abstract Vector2 GetMoveInput();
		public abstract float GetMoveAngle();
		public Vector3 HorizontalMoveDirection => Quaternion.Euler(0f, GetMoveAngle(), 0f) * Vector3.forward;

		#region Public Methods

		public void SetNormalActionsEnabled(bool areEnabled)
		{
			if (AreNormalActionsEnabled == areEnabled)
			{
				return;
			}

			AreNormalActionsEnabled = areEnabled;
			if (!areEnabled)
			{
				InvokeMoveStop();
				InvokeSprint(false);
				InvokeCrouch(false);
				InvokeDive(false);
			}

			OnNormalActionsAvailabilityChanged?.Invoke(areEnabled);
		}

		public void SetTerrestrialActionsEnabled(bool areEnabled)
		{
			if (AreTerrestrialActionsEnabled == areEnabled)
			{
				return;
			}

			AreTerrestrialActionsEnabled = areEnabled;
			if (!areEnabled)
			{
				InvokeCrouch(false);
			}

			OnTerrestrialActionsAvailabilityChanged?.Invoke(areEnabled);
		}

		public void SetDiveActionEnabled(bool isEnabled)
		{
			if (IsDiveActionEnabled == isEnabled)
			{
				return;
			}

			IsDiveActionEnabled = isEnabled;
			if (!isEnabled)
			{
				InvokeDive(false);
			}

			OnDiveActionAvailabilityChanged?.Invoke(isEnabled);
		}

		public void SetWaterSurfaceJumpsEnabled(bool areEnabled)
		{
			if (AreWaterSurfaceJumpsEnabled == areEnabled)
			{
				return;
			}

			AreWaterSurfaceJumpsEnabled = areEnabled;
			OnWaterSurfaceJumpsAvailabilityChanged?.Invoke(areEnabled);
		}

		#endregion

		#region Protected Methods

		protected void InvokeMoveStart(Vector2 value) => OnMoveStart?.Invoke(value);
		protected void InvokeMoveStop() => OnMoveStop?.Invoke();
		protected void InvokeJumpPressed()
		{
			if (AreTerrestrialActionsEnabled || AreWaterSurfaceJumpsEnabled)
			{
				OnJumpPressed?.Invoke();
			}
		}

		protected void InvokeSprint(bool active)
		{
			IsSprintRequested = active;
			OnSprint?.Invoke(active);
		}

		protected void InvokeCrouch(bool active)
		{
			if (AreTerrestrialActionsEnabled || !active)
			{
				OnCrouch?.Invoke(active);
			}
		}

		protected void InvokeDive(bool active)
		{
			if (active && (!AreNormalActionsEnabled || !IsDiveActionEnabled))
			{
				return;
			}

			IsDiveRequested = active;
		}

		#endregion
	}
}

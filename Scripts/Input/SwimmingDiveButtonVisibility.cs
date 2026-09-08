using UnityEngine;

namespace PhysicsCharacterController
{
    [DisallowMultipleComponent]
    public sealed class SwimmingDiveButtonVisibility : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private BaseCharacterInput _input;
        [SerializeField] private CanvasGroup _diveButtonCanvasGroup;

        #region Unity Lifecycle

        private void OnEnable()
        {
            _input.OnDiveActionAvailabilityChanged += SetDiveButtonVisibility;
            SetDiveButtonVisibility(_input.IsDiveActionEnabled);
        }

        private void OnDisable()
        {
            _input.OnDiveActionAvailabilityChanged -= SetDiveButtonVisibility;
            SetDiveButtonVisibility(false);
        }

        #endregion

        #region Private Methods

        private void SetDiveButtonVisibility(bool isVisible)
        {
            _diveButtonCanvasGroup.alpha = isVisible ? 1f : 0f;
            _diveButtonCanvasGroup.interactable = isVisible;
            _diveButtonCanvasGroup.blocksRaycasts = isVisible;
        }

        #endregion
    }
}

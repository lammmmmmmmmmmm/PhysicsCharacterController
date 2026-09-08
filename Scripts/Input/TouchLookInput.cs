using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace PhysicsCharacterController
{
    /// <summary>
    /// Tracks one touch that begins inside the camera-look area and exposes its raw movement in
    /// screen pixels. It does not create or write to a virtual Input System control, allowing camera
    /// controllers to consume touch movement directly while touches that begin over other UI remain
    /// available to those controls.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("UI/Touch Look Input")]
    public sealed class TouchLookInput : MonoBehaviour
    {
        private const int NO_TOUCH_ID = -1;

        private static readonly List<RaycastResult> RAYCAST_RESULTS = new();

        [SerializeField] private bool _blocksWhenPointerStartsOverUi = true;

        private int _activeTouchId = NO_TOUCH_ID;
        private RectTransform _lookArea;
        private Vector2 _previousScreenPositionPixels;

        public bool IsDragging { get; private set; }
        public Vector2 LookDeltaPixels { get; private set; }
        public Vector2 ScreenPositionPixels { get; private set; }
        public bool IsLookActive => IsDragging;

        public event Action OnPressed;
        public event Action OnReleased;

        #region Unity Lifecycle

        private void Awake()
        {
            _lookArea = GetComponent<RectTransform>();
            DisableLookAreaRaycastBlocking();
        }

        private void OnEnable()
        {
            ResetTouchStateWithoutRelease();
        }

        private void Update()
        {
            LookDeltaPixels = Vector2.zero;

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                CancelTouchWhenTouchscreenBecomesUnavailable();
                return;
            }

            UpdateTouchscreen(touchscreen);
        }

        private void OnDisable()
        {
            ReleaseTouch();
        }

        #endregion

        #region Public Methods

        public Vector2 ReadLookDeltaPixels()
        {
            return LookDeltaPixels;
        }

        #endregion

        #region Private Methods

        private void DisableLookAreaRaycastBlocking()
        {
            Graphic graphic = GetComponent<Graphic>();
            if (graphic != null && graphic.raycastTarget)
            {
                Debug.LogWarning(
                    "Touch Look Input must not block UI raycasts. Disabling Raycast Target.",
                    this);
                graphic.raycastTarget = false;
            }
        }

        private void CancelTouchWhenTouchscreenBecomesUnavailable()
        {
            if (!IsDragging)
            {
                return;
            }

            Debug.LogWarning(
                "Canceling camera look because the touchscreen became unavailable.",
                this);
            ReleaseTouch();
        }

        private void UpdateTouchscreen(Touchscreen touchscreen)
        {
            bool hasFoundActiveTouch = false;

            foreach (TouchControl touch in touchscreen.touches)
            {
                if (!touch.press.isPressed)
                {
                    continue;
                }

                int touchId = touch.touchId.ReadValue();
                Vector2 screenPositionPixels = touch.position.ReadValue();

                if (IsDragging && touchId == _activeTouchId)
                {
                    ContinueTouch(screenPositionPixels);
                    hasFoundActiveTouch = true;
                    continue;
                }

                if (!IsDragging &&
                    touch.press.wasPressedThisFrame &&
                    IsScreenPositionEligible(screenPositionPixels))
                {
                    BeginTouch(touchId, screenPositionPixels);
                    hasFoundActiveTouch = true;
                }
            }

            if (IsDragging && !hasFoundActiveTouch)
            {
                ReleaseTouch();
            }
        }

        private bool IsScreenPositionEligible(Vector2 screenPositionPixels)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(
                _lookArea,
                screenPositionPixels,
                GetEventCamera()))
            {
                return false;
            }

            return !_blocksWhenPointerStartsOverUi ||
                   !IsPointerOverUi(screenPositionPixels);
        }

        private void BeginTouch(int touchId, Vector2 screenPositionPixels)
        {
            _activeTouchId = touchId;
            _previousScreenPositionPixels = screenPositionPixels;
            ScreenPositionPixels = screenPositionPixels;
            IsDragging = true;
            OnPressed?.Invoke();
        }

        private void ContinueTouch(Vector2 screenPositionPixels)
        {
            LookDeltaPixels = screenPositionPixels - _previousScreenPositionPixels;
            _previousScreenPositionPixels = screenPositionPixels;
            ScreenPositionPixels = screenPositionPixels;
        }

        private void ReleaseTouch()
        {
            bool wasDragging = IsDragging;
            ResetTouchStateWithoutRelease();
            if (wasDragging)
            {
                OnReleased?.Invoke();
            }
        }

        private void ResetTouchStateWithoutRelease()
        {
            _activeTouchId = NO_TOUCH_ID;
            _previousScreenPositionPixels = Vector2.zero;
            ScreenPositionPixels = Vector2.zero;
            IsDragging = false;
            LookDeltaPixels = Vector2.zero;
        }

        private static bool IsPointerOverUi(Vector2 screenPositionPixels)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            RAYCAST_RESULTS.Clear();
            var pointerEvent = new PointerEventData(EventSystem.current)
            {
                position = screenPositionPixels
            };
            EventSystem.current.RaycastAll(pointerEvent, RAYCAST_RESULTS);
            return RAYCAST_RESULTS.Count > 0;
        }

        private Camera GetEventCamera()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
        }

        #endregion
    }
}

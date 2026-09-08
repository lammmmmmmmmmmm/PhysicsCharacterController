using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

[AddComponentMenu("UI/On-Screen Look")]
[DefaultExecutionOrder(-100)]
public class OnScreenLook : OnScreenControl
{
    private const int MOUSE_POINTER_ID = -999;
    private const int NO_TOUCH_ID = -1;

    [Header("Sensitivity")]
    [SerializeField, Range(0.01f, 5f)] private float sensitivity = 1f;
    [SerializeField] private bool invertX = false;
    [SerializeField] private bool invertY = false;

    [Header("UI Blocking")]
    [SerializeField] private bool blockWhenPointerStartsOverUI = true;

    [InputControl(layout = "Vector2")]
    [SerializeField] private string _controlPath;

    protected override string controlPathInternal
    {
        get => _controlPath;
        set => _controlPath = value;
    }

    private bool _isDragging;
    private int _lookTouchId = NO_TOUCH_ID;
    private Vector2 _previousPointerPosition;

    private static readonly List<RaycastResult> RaycastResults = new();

    public bool IsDragging => _isDragging;
    public bool IsTouchInputActive { get; private set; }
    public Vector2 ScreenPositionPixels { get; private set; }

    public event Action OnPressed;
    public event Action OnReleased;

    private void Awake()
    {
        // The fullscreen image must not block UI buttons.
        Graphic graphic = GetComponent<Graphic>();

        if (graphic != null && graphic.raycastTarget)
        {
            Debug.LogWarning(
                "[OnScreenLook] Fullscreen look image Raycast Target must be disabled. Disabling automatically.",
                this
            );

            graphic.raycastTarget = false;
        }
    }

    private void Update()
    {
        if (Touchscreen.current != null)
        {
            HandleTouches();
            return;
        }

        HandleMouse();
    }

    protected override void OnDisable()
    {
        StopDragging();
        base.OnDisable();
    }

    private void HandleTouches()
    {
        bool activeLookTouchStillExists = false;

        foreach (TouchControl touch in Touchscreen.current.touches)
        {
            if (!touch.press.isPressed)
                continue;

            int touchId = touch.touchId.ReadValue();
            Vector2 position = touch.position.ReadValue();

            if (_isDragging && touchId == _lookTouchId)
            {
                activeLookTouchStillExists = true;
                ContinuePointer(position);
                continue;
            }

            if (!_isDragging && touch.press.wasPressedThisFrame)
            {
                if (blockWhenPointerStartsOverUI && IsPointerOverAnyUI(position))
                    continue;

                BeginPointer(touchId, position, true);
                activeLookTouchStillExists = true;
            }
        }

        if (_isDragging && !activeLookTouchStillExists)
        {
            StopDragging();
        }
    }

    private void HandleMouse()
    {
        if (Mouse.current == null)
        {
            StopDragging();
            return;
        }

        Vector2 position = Mouse.current.position.ReadValue();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (blockWhenPointerStartsOverUI && IsPointerOverAnyUI(position))
            {
                StopDragging();
                return;
            }

            BeginPointer(MOUSE_POINTER_ID, position, false);
            return;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            StopDragging();
            return;
        }

        if (_isDragging && Mouse.current.leftButton.isPressed)
        {
            ContinuePointer(position);
        }
    }

    private void BeginPointer(int pointerId, Vector2 position, bool isTouchInput)
    {
        _lookTouchId = pointerId;
        _isDragging = true;
        IsTouchInputActive = isTouchInput;
        _previousPointerPosition = position;
        ScreenPositionPixels = position;
        SendValueToControl(Vector2.zero);
        OnPressed?.Invoke();
    }

    private void ContinuePointer(Vector2 position)
    {
        Vector2 delta = position - _previousPointerPosition;
        _previousPointerPosition = position;
        ScreenPositionPixels = position;

        delta.x *= sensitivity * (invertX ? -1f : 1f);
        delta.y *= sensitivity * (invertY ? -1f : 1f);

        SendValueToControl(delta);
    }

    private void StopDragging()
    {
        if (!_isDragging)
            return;

        _isDragging = false;
        _lookTouchId = NO_TOUCH_ID;
        ScreenPositionPixels = Vector2.zero;
        SendValueToControl(Vector2.zero);
        OnReleased?.Invoke();
        IsTouchInputActive = false;
    }

    private bool IsPointerOverAnyUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return false;

        RaycastResults.Clear();

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        EventSystem.current.RaycastAll(eventData, RaycastResults);

        return RaycastResults.Count > 0;
    }
}

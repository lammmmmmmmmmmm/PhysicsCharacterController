using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;

/// <summary>
/// Custom mobile on-screen joystick integrated with Unity's New Input System.
///
/// SETUP:
/// 1. Create a Canvas (Screen Space - Overlay, CanvasScaler = Scale With Screen Size).
///
/// FIXED MODE:
///   - Place a background Image in the Canvas.
///   - Place a handle Image inside the background.
///   - Attach this script to the background Image GameObject.
///   - Assign joystickBackground (self) and joystickHandle in Inspector.
///
/// DYNAMIC MODE:
///   - Create a full-screen transparent Image in the Canvas — this is the touchZone.
///   - Place the background Image and handle Image somewhere in the Canvas (they'll be
///     repositioned at runtime). Add a CanvasGroup to the background.
///   - Attach this script to ANY stable GameObject (e.g. the Canvas itself).
///   - Assign touchZone, joystickBackground, and joystickHandle in the Inspector.
///   - The touchZone catches the initial press anywhere on screen.
///
/// Set controlPath to e.g. "<Gamepad>/leftStick" to match your Input Actions asset.
/// </summary>
[AddComponentMenu("UI/On-Screen Joystick")]
public class OnScreenJoystick : OnScreenControl, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    // ── Inspector ──────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("DYNAMIC MODE ONLY: A full-screen transparent Image that catches the first touch anywhere on screen.")]
    [SerializeField] private RectTransform touchZone;

    [Tooltip("The outer ring / background visual. Must have a CanvasGroup component.")]
    [SerializeField] private RectTransform joystickBackground;

    [Tooltip("The movable inner handle.")]
    [SerializeField] private RectTransform joystickHandle;

    [Header("Behaviour")]
    [Tooltip("Dynamic: spawns at touch position. Fixed: stays put.")]
    [SerializeField] private JoystickMode mode = JoystickMode.Dynamic;

    [Tooltip("Maximum pixels the handle can travel from centre (canvas space).")]
    [SerializeField, Range(20f, 300f)] private float handleRange = 80f;

    [Tooltip("Normalised dead-zone radius (0–1). Input below this is zeroed.")]
    [SerializeField, Range(0f, 0.5f)] private float deadzone = 0.1f;

    [Tooltip("How quickly the handle returns to centre on release. 0 = instant.")]
    [SerializeField, Range(0f, 25f)] private float returnSpeed = 15f;

    [Tooltip("Automatically hide joystick visuals while idle in both Dynamic and Fixed modes. Disable to keep them visible.")]
    [SerializeField] private bool hideWhenIdle = true;

    // ── Input System ───────────────────────────────────────────────────────────

    [InputControl(layout = "Vector2")]
    [SerializeField] private string _controlPath;

    protected override string controlPathInternal
    {
        get => _controlPath;
        set => _controlPath = value;
    }

    // ── Runtime state ──────────────────────────────────────────────────────────

    private Vector2 _inputValue;
    private bool _isDragging;
    private Vector2 _fixedOrigin;   // cached for Fixed mode reset
    private CanvasGroup _bgCanvasGroup;
    private Canvas _rootCanvas;

    // ── Public ─────────────────────────────────────────────────────────────────

    /// <summary>Normalised joystick value [-1, 1] on both axes.</summary>
    public Vector2 InputValue => _inputValue;

    public bool IsDragging => _isDragging;

    // ──────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ──────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rootCanvas = GetComponentInParent<Canvas>();
        if (_rootCanvas != null && !_rootCanvas.isRootCanvas)
            _rootCanvas = _rootCanvas.rootCanvas;

        // Require a CanvasGroup on the background — never use SetActive.
        _bgCanvasGroup = joystickBackground.GetComponent<CanvasGroup>();
        if (_bgCanvasGroup == null)
            _bgCanvasGroup = joystickBackground.gameObject.AddComponent<CanvasGroup>();

        // CRITICAL: always block raycasts so Fixed-mode joystick is always pressable.
        // Visibility is controlled by alpha only — the collider stays live.
        _bgCanvasGroup.blocksRaycasts = true;

        _fixedOrigin = joystickBackground.anchoredPosition;

        if (mode == JoystickMode.Dynamic)
        {
            // In Dynamic mode the touchZone handles OnPointerDown, not the background.
            // Wire up the touchZone's events to this script.
            if (touchZone == null)
            {
                Debug.LogError("[OnScreenJoystick] Dynamic mode requires a touchZone RectTransform.", this);
            }
            else
            {
                TouchZoneProxy proxy = touchZone.gameObject.GetComponent<TouchZoneProxy>();
                if (proxy == null) proxy = touchZone.gameObject.AddComponent<TouchZoneProxy>();
                proxy.Init(this);
            }
        }

        SetVisuals(!hideWhenIdle);
    }

    private void Update()
    {
        if (_isDragging) return;

        if (joystickHandle.anchoredPosition == Vector2.zero) return;

        joystickHandle.anchoredPosition = returnSpeed > 0f
            ? Vector2.Lerp(joystickHandle.anchoredPosition, Vector2.zero, Time.unscaledDeltaTime * returnSpeed)
            : Vector2.zero;

        if (joystickHandle.anchoredPosition.sqrMagnitude < 0.01f)
            joystickHandle.anchoredPosition = Vector2.zero;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Pointer events (Fixed mode — received directly by the background)
    // ──────────────────────────────────────────────────────────────────────────

    public void OnPointerDown(PointerEventData eventData)
    {
        // In Dynamic mode this is routed through TouchZoneProxy instead.
        if (mode == JoystickMode.Dynamic) return;

        BeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;
        ProcessDrag(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_isDragging) return;
        EndDrag();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Internal drag logic (shared between Fixed and Dynamic)
    // ──────────────────────────────────────────────────────────────────────────

    internal void BeginDrag(PointerEventData eventData)
    {
        _isDragging = true;

        if (mode == JoystickMode.Dynamic)
        {
            // Relocate background to finger position
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                joystickBackground.parent as RectTransform,
                eventData.position,
                GetUICamera(),
                out Vector2 localPoint);

            joystickBackground.anchoredPosition = localPoint;
            joystickHandle.anchoredPosition = Vector2.zero;
        }

        SetVisuals(true);
        ProcessDrag(eventData);
    }

    internal void ProcessDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            joystickBackground,
            eventData.position,
            GetUICamera(),
            out Vector2 localPoint);

        Vector2 clamped = Vector2.ClampMagnitude(localPoint, handleRange);
        joystickHandle.anchoredPosition = clamped;

        Vector2 normalised = clamped / handleRange;
        float magnitude = normalised.magnitude;

        if (magnitude < deadzone)
        {
            normalised = Vector2.zero;
        }
        else
        {
            normalised = normalised.normalized * Mathf.InverseLerp(deadzone, 1f, magnitude);
        }

        SetInput(normalised);
    }

    internal void EndDrag()
    {
        _isDragging = false;
        SetInput(Vector2.zero);

        if (mode == JoystickMode.Dynamic)
        {
            joystickBackground.anchoredPosition = _fixedOrigin;
        }

        SetVisuals(!hideWhenIdle);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private void SetInput(Vector2 value)
    {
        _inputValue = value;
        SendValueToControl(value);
    }

    /// <summary>
    /// Controls ONLY alpha — never touches SetActive or blocksRaycasts.
    /// The collider stays live at all times so Fixed-mode presses always land.
    /// </summary>
    private void SetVisuals(bool visible)
    {
        if (_bgCanvasGroup == null) return;
        _bgCanvasGroup.alpha = visible ? 1f : 0f;
    }

    private Camera GetUICamera()
    {
        if (_rootCanvas == null) return null;
        return _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _rootCanvas.worldCamera;
    }

    // ──────────────────────────────────────────────────────────────────────────

    public enum JoystickMode { Dynamic, Fixed }
}

/// <summary>
/// Thin proxy placed on the Dynamic touchZone GameObject.
/// Forwards pointer events to the owning CustomJoystick.
/// Kept internal — you never need to interact with this directly.
/// </summary>
internal class TouchZoneProxy : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private OnScreenJoystick _owner;

    internal void Init(OnScreenJoystick owner) => _owner = owner;

    public void OnPointerDown(PointerEventData e) => _owner.BeginDrag(e);
    public void OnDrag(PointerEventData e) => _owner.ProcessDrag(e);
    public void OnPointerUp(PointerEventData e) => _owner.EndDrag();
}
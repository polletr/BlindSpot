using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;

public class VirtualAimCursor : MonoBehaviour
{
    public enum AimSource { Mouse, Stick }

    [Header("References")]
    public RectTransform crosshairUI;   // UI Image RectTransform
    [SerializeField, Tooltip("Optional override for where the aim ray originates. Defaults to this transform if unset.")]
    private Transform aimOriginOverride;
    [SerializeField, Tooltip("Optional override for which camera drives the cursor-to-world conversion.")]
    private Camera cameraOverride;

    [Header("Stick Cursor Movement")]
    public float stickDeadzone = 0.2f;
    public float stickCursorSpeed = 900f; // pixels/sec

    [Header("Visibility")]
    public float inactiveHideDelay = 2.5f;
    public float fadeInTime = 0.08f;
    public float fadeOutTime = 0.15f;

    [Header("Arbitration")]
    public float stickPriorityTime = 0.6f;
    public float mouseMovePixelsToActivate = 6f;

    public AimSource Source { get; private set; } = AimSource.Mouse;
    private Vector2 _cursorScreenPos;
    private Vector2 _stickLook;
    private float _lastLookInputTime;
    private float _lastStickTime = -999f;
    private Vector2 _lastMouseScreenPos;
    private bool _visible = true;
    private Tween _fadeTween;
    private Transform _aimOrigin;        // usually the player transform (aim origin)
    private Camera cam;                  // if null, uses Camera.main
    private CanvasGroup crosshairGroup;  // fading

    public Vector2 CursorScreenPos => _cursorScreenPos;

    void Awake()
    {
        cam = cameraOverride != null ? cameraOverride : Camera.main;
        _lastLookInputTime = Time.unscaledTime;
        _aimOrigin = aimOriginOverride != null ? aimOriginOverride : transform;
        if (crosshairUI != null)
        {
            crosshairGroup = crosshairUI.GetComponent<CanvasGroup>();
            if (crosshairGroup == null)
                crosshairGroup = crosshairUI.gameObject.AddComponent<CanvasGroup>();
        }
        // Optional: hide OS cursor if you want this crosshair to replace it
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }

    void Start()
    {
        // Initialize cursor near the aim origin if possible
        if (cam != null && _aimOrigin != null)
        {
            _cursorScreenPos = cam.WorldToScreenPoint(_aimOrigin.position + Vector3.right * 2f);
        }
        else
        {
            _cursorScreenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }
        ApplyUIPosition();
        SetVisible(true, immediate: true);
    }

    void Update()
    {
        // Stick-driven motion
        if (_stickLook.sqrMagnitude > 0.0001f)
        {
            _cursorScreenPos += _stickLook * stickCursorSpeed * Time.unscaledDeltaTime;
            _cursorScreenPos.x = Mathf.Clamp(_cursorScreenPos.x, 0f, Screen.width);
            _cursorScreenPos.y = Mathf.Clamp(_cursorScreenPos.y, 0f, Screen.height);
        }
        ApplyUIPosition();
        // Hide after inactivity
        bool shouldHide = (Time.unscaledTime - _lastLookInputTime) > inactiveHideDelay;
        if (shouldHide) SetVisible(false);
    }

    private void ApplyUIPosition()
    {
        if (crosshairUI != null)
            crosshairUI.position = _cursorScreenPos;
    }
    // Input System callback (bind your Look action to this)
    public void SetAimOrigin(Transform origin)
    {
        if (origin == null) return;
        _aimOrigin = origin;
    }

    public void SetCamera(Camera overrideCam)
    {
        if (overrideCam == null) return;
        cam = overrideCam;
    }

    public void OnLook(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed && !ctx.canceled) return;
        Vector2 v = ctx.ReadValue<Vector2>();
        var device = ctx.control?.device;
        _lastLookInputTime = Time.unscaledTime;
        SetVisible(true);
        // Mouse/Pointer: v is SCREEN POSITION
        if (device is Mouse || device is Pointer)
        {
            float mouseDelta = (v - _lastMouseScreenPos).magnitude;
            _lastMouseScreenPos = v;
            bool stickRecentlyUsed = (Time.unscaledTime - _lastStickTime) < stickPriorityTime;
            if (stickRecentlyUsed && mouseDelta < mouseMovePixelsToActivate)
                return;
            Source = AimSource.Mouse;
            _cursorScreenPos = v;
            _stickLook = Vector2.zero;
            return;
        }
        // Stick: v is DIRECTION
        if (v.sqrMagnitude >= stickDeadzone * stickDeadzone)
        {
            Source = AimSource.Stick;
            _lastStickTime = Time.unscaledTime;
            // keep magnitude for analog speed
            _stickLook = v;
        }
        else
        {
            _stickLook = Vector2.zero;
        }
    }
    /// <summary>
    /// Returns a normalized world-space aim direction from the aim origin -> cursor world point.
    /// </summary>
    public Vector2 GetAimDir()
    {
        Transform origin = _aimOrigin != null ? _aimOrigin : transform;
        Camera activeCam = cam != null ? cam : Camera.main;
        if (origin == null) return Vector2.right;
        Vector3 cursorWorld = GetCursorWorldPoint(activeCam, origin);
        Vector2 aim = (Vector2)cursorWorld - (Vector2)origin.position;
        if (aim.sqrMagnitude < 0.0001f)
        {
            Vector2 fallback = (Vector2)origin.right;
            if (fallback.sqrMagnitude < 0.0001f) fallback = Vector2.right;
            return fallback.normalized;
        }
        return aim.normalized;
    }

    private Vector3 GetCursorWorldPoint(Camera activeCam, Transform origin)
    {
        if (origin == null) return Vector3.right;
        if (activeCam == null)
            return origin.position + Vector3.right;
        float depth = origin.position.z - activeCam.transform.position.z;
        if (!activeCam.orthographic)
            depth = Mathf.Abs(depth);
        Vector3 screenPoint = new Vector3(_cursorScreenPos.x, _cursorScreenPos.y, depth);
        return activeCam.ScreenToWorldPoint(screenPoint);
    }

    private void SetVisible(bool visible, bool immediate = false)
    {
        if (_visible == visible && !immediate) return;
        _visible = visible;
        if (crosshairUI == null || crosshairGroup == null) return;
        crosshairUI.gameObject.SetActive(true);
        _fadeTween?.Kill();
        if (immediate)
        {
            crosshairGroup.alpha = visible ? 1f : 0f;
            if (!visible) crosshairUI.gameObject.SetActive(false);
            return;
        }
        float dur = visible ? fadeInTime : fadeOutTime;
        float targetAlpha = visible ? 1f : 0f;
        _fadeTween = crosshairGroup.DOFade(targetAlpha, dur)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (!visible)
                    crosshairUI.gameObject.SetActive(false);
            });
    }
}

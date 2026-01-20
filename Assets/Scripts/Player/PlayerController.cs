using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SonarPing sonar;
    [SerializeField] private VirtualAimCursor aimCursor;

    [Header("Movement Tuning")]
    public float moveSpeed = 6f;
    public float acceleration = 30f;
    public float deceleration = 40f;

    [Header("Dash")]
    public float dashSpeed = 16f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 0.7f;
    public bool allowDashWithoutInput = true;

    [Header("Dash Feel (Optional)")]
    public Transform visualRoot; // visual child (not the rigidbody transform)
    public float stretchAmount = 1.25f;
    public float squashAmount = 0.75f;
    public float stretchTime = 0.06f;
    public float settleTime = 0.10f;

    // Public accessors used by states
    public Rigidbody2D RB { get; private set; }
    public Vector2 MoveInput { get; private set; }
    public Vector2 CurrentVelocity { get; set; }

    // States
    private IPlayerState _currentState;
    public PlayerMoveState MoveState { get; private set; }
    public PlayerDashState DashState { get; private set; }
    public PlayerDeadState DeadState { get; private set; }

    // Status
    public bool IsDashing => _currentState == DashState;
    public bool IsDead => _currentState == DeadState;

    // Direction memory (for dash when no input)
    private Vector2 _lastMoveDir = Vector2.right;
    private Vector2 _lastAimDir = Vector2.right;

    // Cooldowns
    private float _dashCooldownRemaining;

    // Tweens
    private Tween _dashFeelTween;

    /// <summary>
    /// Single source of truth for aim direction. If no cursor, falls back to right.
    /// </summary>
    public Vector2 AimDir
    {
        get
        {
            if (aimCursor == null) return _lastAimDir;

            Vector2 dir = aimCursor.GetAimDir();
            if (dir.sqrMagnitude > 0.0001f)
                _lastAimDir = dir;

            return _lastAimDir;
        }
    }

    private void Awake()
    {
        RB = GetComponent<Rigidbody2D>();
        RB.gravityScale = 0f;
        RB.freezeRotation = true;

        if (sonar == null)
            sonar = GetComponent<SonarPing>();

        if (aimCursor == null)
            aimCursor = GetComponent<VirtualAimCursor>();
        if (aimCursor == null)
            aimCursor = FindFirstObjectByType<VirtualAimCursor>();
        if (aimCursor != null)
            aimCursor.SetAimOrigin(transform);

        if (sonar == null) sonar = GetComponent<SonarPing>();
        if (sonar != null) sonar.SetAimProvider(() => AimDir); // AimDir from your cursor/provider

        MoveState = new PlayerMoveState();
        DashState = new PlayerDashState();
        DeadState = new PlayerDeadState();

        ChangeState(MoveState);
    }

    private void Update()
    {
        if (_dashCooldownRemaining > 0f)
            _dashCooldownRemaining -= Time.deltaTime;

        _currentState?.Tick(this);
    }

    private void FixedUpdate()
    {
        _currentState?.FixedTick(this);
    }

    // ------------------------
    // Input
    // ------------------------

    public void OnMove(InputAction.CallbackContext ctx)
    {
        Vector2 v = ctx.ReadValue<Vector2>();
        if (v.sqrMagnitude > 1f) v.Normalize();

        MoveInput = v;

        if (v.sqrMagnitude > 0.001f)
            _lastMoveDir = v;
    }

    public void SetMoveInput(Vector2 v) => MoveInput = v;

    public void OnDash(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        if (IsDead || IsDashing) return;
        if (_dashCooldownRemaining > 0f) return;

        _dashCooldownRemaining = dashCooldown;
        ChangeState(DashState);
    }

    public void OnPing(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        if (sonar == null) return;

        // Rule: cannot START ping while dashing
        if (IsDashing) return;


        // Ping should follow player movement; aim is sampled live each scan tick inside SonarPing.
        sonar.DoPulse();
    }

    // ------------------------
    // State machine
    // ------------------------

    public void ChangeState(IPlayerState next)
    {
        if (next == null || next == _currentState) return;

        _currentState?.Exit(this);
        _currentState = next;
        _currentState.Enter(this);
    }

    // External hook for lethal contact
    public void KillPlayer()
    {
        if (IsDead) return;
        ChangeState(DeadState);
    }

    public void Respawn(Vector2 position)
    {
        transform.position = position;
        ChangeState(MoveState);
    }

    // ------------------------
    // Dash direction commitment
    // ------------------------

    public Vector2 GetCommittedDashDirection()
    {
        Vector2 dir = (MoveInput.sqrMagnitude > 0.001f) ? MoveInput : _lastMoveDir;

        if (dir.sqrMagnitude < 0.001f && !allowDashWithoutInput)
            return Vector2.zero;

        if (dir.sqrMagnitude < 0.001f)
            dir = Vector2.right;

        return dir;
    }

    // ------------------------
    // Dash feel (DOTween)
    // ------------------------

    public void PlayDashFeel(Vector2 dashDir)
    {
        if (visualRoot == null) return;

        KillDashFeel();

        Quaternion originalRot = visualRoot.localRotation;
        Vector3 originalScale = Vector3.one;

        float angle = Mathf.Atan2(dashDir.y, dashDir.x) * Mathf.Rad2Deg;
        visualRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
        visualRoot.localScale = originalScale;

        Vector3 stretchScale = new Vector3(stretchAmount, squashAmount, 1f);

        _dashFeelTween = DOTween.Sequence()
            .Append(visualRoot.DOScale(stretchScale, stretchTime).SetEase(Ease.OutQuad))
            .Append(visualRoot.DOScale(originalScale, settleTime).SetEase(Ease.OutBack, 1.2f))
            .OnComplete(() =>
            {
                // Keep player upright after deformation
                if (visualRoot != null)
                    visualRoot.localRotation = originalRot;
            });
    }

    public void KillDashFeel()
    {
        if (_dashFeelTween != null && _dashFeelTween.IsActive())
            _dashFeelTween.Kill();

        if (visualRoot != null)
            visualRoot.DOKill();
    }

    // UI helper
    public float DashCooldown01()
    {
        if (dashCooldown <= 0f) return 0f;
        return Mathf.Clamp01(_dashCooldownRemaining / dashCooldown);
    }
}

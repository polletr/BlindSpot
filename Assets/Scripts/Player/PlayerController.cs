using System;
using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using FMODUnity;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SonarPing sonar;
    [SerializeField] private PlayerFlashlight flashlight;
    [SerializeField] private VirtualAimCursor aimCursor;
    [SerializeField] private BlopShooter blopShooter;
    [SerializeField] private BlopWallet blopWallet;
    [SerializeField] private EventReference OnDashSound;
    [SerializeField] private EventReference OnDeathSound;
    [SerializeField] private EventReference OnFlashlight;

    [Header("Ping")]
    [SerializeField, Min(0)] private int pingBlopCost = 3;
    [SerializeField, Min(0f)] private float pingInputDebounceSeconds = 0.08f;

    [Header("Movement Tuning")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField, Range(0.1f, 1f)] private float aimingMoveSpeedMultiplier = 0.5f;
    public float acceleration = 30f;
    public float deceleration = 40f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 16f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 0.7f;
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
    public bool IsTemporarilyInvincible => _isTemporarilyInvincible;

    public event Action<PlayerController> PlayerDied;
    public event Action<PlayerController> PlayerRespawned;

    // Direction memory (for dash when no input)
    private Vector2 _lastMoveDir = Vector2.right;
    private Vector2 _lastAimDir = Vector2.right;

    // Cooldowns
    private float _dashCooldownRemaining;

    // Tweens
    private Tween _dashFeelTween;
    private Tween _aimShakeTween;
    private bool _movementInputLocked;
    private bool _flashlightEnabledBeforeKill = true;
    private Vector3 _visualRootRestLocalPos = Vector3.zero;
    private bool _isTemporarilyInvincible;
    private bool _pingInputHeld;
    private float _lastPingInputTime = -999f;

    private UpgradeManager _upgradeManager;
    private UpgradeManager UpgradeMgr
    {
        get
        {
            if (_upgradeManager == null)
                _upgradeManager = UpgradeManager.Instance;
            return _upgradeManager;
        }
    }

    private float VelocityMultiplier => UpgradeMgr != null ? UpgradeMgr.VelocityMultiplier : 1f;
    private float DashDistanceMultiplier => UpgradeMgr != null ? UpgradeMgr.DashDistanceMultiplier : 1f;
    private float DashCooldownMultiplier => UpgradeMgr != null ? UpgradeMgr.DashCooldownMultiplier : 1f;

    public float MovementSpeed
    {
        get
        {
            return moveSpeed * VelocityMultiplier;
        }
    }
    public float DashSpeed => dashSpeed * DashDistanceMultiplier;
    public float DashDuration => dashDuration;
    public float DashCooldown => dashCooldown * DashCooldownMultiplier;

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

        if (aimCursor == null)
            aimCursor = GetComponent<VirtualAimCursor>();
        if (aimCursor == null)
            aimCursor = FindFirstObjectByType<VirtualAimCursor>();
        if (aimCursor != null)
            aimCursor.SetAimOrigin(transform);

        if (blopWallet == null)
            blopWallet = GetComponent<BlopWallet>();
        if (blopWallet == null)
            blopWallet = GetComponentInChildren<BlopWallet>(true);
        if (blopWallet == null)
            blopWallet = GetComponentInParent<BlopWallet>();

        if (visualRoot != null)
            _visualRootRestLocalPos = visualRoot.localPosition;

        if (sonar == null) sonar = GetComponent<SonarPing>();
        bool createdFlashlight = false;
        if (flashlight == null) flashlight = GetComponent<PlayerFlashlight>();
        if (flashlight == null)
        {
            flashlight = gameObject.AddComponent<PlayerFlashlight>();
            createdFlashlight = true;
        }
        if (createdFlashlight && sonar != null)
        {
            flashlight.obstacleMask = sonar.obstacleMask;
            flashlight.revealableMask = sonar.revealableMask;
            flashlight.piercingUpgrade = sonar.piercingUpgrade;
            flashlight.autoResolvePoolsFromHub = sonar.autoResolvePoolsFromHub;
        }
        if (flashlight != null)
        {
            flashlight.SetAimProvider(() => AimDir);
            _flashlightEnabledBeforeKill = flashlight.flashlightEnabled;
        }

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

    private void OnDisable()
    {
        KillDashFeel();
    }

    // ------------------------
    // Input
    // ------------------------

    public void OnMove(InputAction.CallbackContext ctx)
    {
        Vector2 v = ctx.ReadValue<Vector2>();
        if (v.sqrMagnitude > 1f) v.Normalize();

        if (_movementInputLocked)
        {
            MoveInput = Vector2.zero;
            return;
        }

        MoveInput = v;

        if (v.sqrMagnitude > 0.001f)
            _lastMoveDir = v;
    }

    public void SetMoveInput(Vector2 v) => MoveInput = v;

    void SetMovementInputLocked(bool locked)
    {
        if (_movementInputLocked == locked) return;

        _movementInputLocked = locked;
        if (locked)
            SetMoveInput(Vector2.zero);
    }

    public void OnDash(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        if (IsDead || IsDashing) return;
        if (_dashCooldownRemaining > 0f) return;

        Vector2 dashDir = GetCommittedDashDirection();
        if (dashDir.sqrMagnitude < 0.001f) return;

        _dashCooldownRemaining = DashCooldown;
        ChangeState(DashState);
    }

    public void OnLook(InputAction.CallbackContext ctx)
    {
        if (aimCursor == null)
            aimCursor = GetComponent<VirtualAimCursor>();

        aimCursor?.OnLook(ctx);
    }

    public void OnPing(InputAction.CallbackContext ctx)
    {
        if (ctx.canceled)
        {
            _pingInputHeld = false;
            return;
        }

        bool pressedNow = false;
        try
        {
            pressedNow = ctx.ReadValueAsButton();
        }
        catch
        {
            pressedNow = ctx.performed;
        }

        if (!pressedNow) return;
        if (_pingInputHeld) return;
        _pingInputHeld = true;
        if (Time.unscaledTime - _lastPingInputTime < pingInputDebounceSeconds) return;
        _lastPingInputTime = Time.unscaledTime;
        if (IsDead) return;
        if (sonar == null || !sonar.CanLaunchClickPing) return;
        if (blopWallet == null)
        {
            blopWallet = GetComponent<BlopWallet>();
            if (blopWallet == null) blopWallet = GetComponentInChildren<BlopWallet>(true);
            if (blopWallet == null) blopWallet = GetComponentInParent<BlopWallet>();
        }
        if (blopWallet == null)
        {
            Debug.LogWarning("[PlayerController] Ping input received but no BlopWallet was found.", this);
            return;
        }
        if (!blopWallet.TrySpend(pingBlopCost)) return;

        sonar.TriggerClickPing();
    }

    public void OnToggleFlashlight(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        ToggleFlashlight();
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
    public void KillPlayer(Vector3 hitPointWorld, Transform attackerTip)
    {
        if (IsDead || _isTemporarilyInvincible) return;

        SetMovementInputLocked(true);

        if (flashlight != null)
        {
            _flashlightEnabledBeforeKill = flashlight.flashlightEnabled;
            flashlight.ForceFlashlightState(false);
        }
        ChangeState(DeadState);
        PlayerDied?.Invoke(this);

        Vector3 playerPos = visualRoot.transform.position;

        // Align the slice along the incoming attack: plane passes through the tip direction.
        Vector3 tipToPlayer = (playerPos - attackerTip.position).normalized;
        Vector3 cutNormal = Vector3.Cross(tipToPlayer, Vector3.forward).normalized;
        if (cutNormal.sqrMagnitude < 0.0001f)
        {
            cutNormal = Vector3.right;
        }
        var ctx = new DeathHitContext(playerPos, hitPointWorld, cutNormal, attackerTip);
        DeathDirector.Instance.PlayDeath(ctx, visualRoot.GetComponent<SpriteRenderer>());
        
        AudioManager.PlayAt(OnDeathSound, transform.position); 

    }

    public void Respawn(Vector2 position)
    {
        transform.position = position;
        _isTemporarilyInvincible = false;
        SetMovementInputLocked(false);
        blopShooter?.ReleaseCharge();

        if (flashlight != null)
            flashlight.ForceFlashlightState(_flashlightEnabledBeforeKill);

        ChangeState(MoveState);
        PlayerRespawned?.Invoke(this);
    }

    public void SetTemporaryInvincibility(bool invincible)
    {
        _isTemporarilyInvincible = invincible;
    }

    // ------------------------
    // Dash direction commitment
    // ------------------------

    public Vector2 GetCommittedDashDirection()
    {
        Vector2 dir;

        if (allowDashWithoutInput)
            dir = (MoveInput.sqrMagnitude > 0.001f) ? MoveInput : _lastMoveDir;
        else
            dir = MoveInput;

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

        AudioManager.PlayAttached(OnDashSound, this.gameObject);

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
        float cooldown = DashCooldown;
        if (cooldown <= 0f) return 0f;
        return Mathf.Clamp01(_dashCooldownRemaining / cooldown);
    }

    public void ToggleFlashlight()
    {
        if (flashlight == null)
            flashlight = GetComponent<PlayerFlashlight>();
        if (flashlight == null)
            return;

        bool nextState = !flashlight.flashlightEnabled;
        flashlight.ForceFlashlightState(nextState);
        AudioManager.PlayAttached(OnFlashlight, this.gameObject);
    }
}




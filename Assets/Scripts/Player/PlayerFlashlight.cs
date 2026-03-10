using System;
using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerFlashlight : MonoBehaviour
{
    [Header("Flashlight Cone")]
    public bool flashlightEnabled = true;

    [Tooltip("How often the flashlight cone updates aim/origin and reveal checks.")]
    public float flashlightUpdateInterval = 0.03f;

    public Color flashlightColor = new Color(0.85f, 0.92f, 1f, 1f);

    [Header("Flashlight Stabilization")]
    [Tooltip("Seconds to smooth aim direction. 0 = no smoothing.")]
    public float aimSmoothTime = 0.05f;
    [Tooltip("Seconds to smooth origin position (helps when rubbing walls). 0 = no smoothing.")]
    public float originSmoothTime = 0.03f;
    [Tooltip("If true, reads origin from Rigidbody2D.position (better with physics).")]
    public bool useRigidbodyOrigin = true;

    [Header("Flashlight Transition")]
    [Min(0.01f)] public float turnOnDuration = 0.2f;
    [SerializeField] private Ease turnOnEase = Ease.OutCubic;

    [Header("Reveal")]
    [Tooltip("If true the flashlight continuously reveals targets in its cone.")]
    public bool flashlightReveals = true;
    [Tooltip("How long each reveal tick keeps objects visible.")]
    public float flashlightRevealDuration = 0.2f;

    [Header("Tuning")]
    public float range = 7f;
    [Range(5f, 120f)] public float angleDeg = 45f;

    [Header("Line of sight")]
    public LayerMask obstacleMask;
    public LayerMask revealableMask;
    public bool piercingUpgrade = false;

    [Header("Ray fan quality")]
    [Range(12, 180)] public int rayCount = 60;

    [Header("Visual")]
    public SonarConeVisualPool conePool;

    [Header("Pool Resolution")]
    [Tooltip("Automatically subscribe to pool hub events so runtime-spawned players get valid pools.")]
    public bool autoResolvePoolsFromHub = true;

    Func<Vector2> _aimProvider;
    Vector2 _cachedAimDir = Vector2.right;

    UpgradeManager _upgradeManager;
    Rigidbody2D _rb;
    SonarConeVisual _flashlightVisual;
    Tween _turnOnTween;

    float _flashlightAccum;
    Vector2 _smoothedOrigin;
    Vector2 _originVel;
    Vector2 _smoothedDir = Vector2.right;
    Vector2 _dirVel;

    bool _aimModeActive;
    float _aimModeRange;
    float _aimModeAngle;
    Color _aimModeColor;
    float _aimBlend;
    float _aimColorBlend;
    float _aimReturnDuration = 0.2f;
    float _aimReturnDelayRemaining;

    bool _visualShownLastFrame;

    private UpgradeManager UpgradeMgr
    {
        get
        {
            if (_upgradeManager == null)
            {
                _upgradeManager = UpgradeManager.Instance;
                if (_upgradeManager != null)
                    _upgradeManager.UpgradesChanged += HandleUpgradeChanged;
            }

            return _upgradeManager;
        }
    }

    private float BaseFlashlightRange => range * (UpgradeMgr != null ? UpgradeMgr.FlashlightRangeMultiplier : 1f);
    private float BaseFlashlightAngle => angleDeg * (UpgradeMgr != null ? UpgradeMgr.FlashlightAngleMultiplier : 1f);
    private float EffectiveFlashlightRange => Mathf.Lerp(BaseFlashlightRange, Mathf.Max(0f, _aimModeRange), _aimBlend);
    private float EffectiveFlashlightAngle => Mathf.Lerp(BaseFlashlightAngle, Mathf.Max(1f, _aimModeAngle), _aimBlend);
    private Color EffectiveFlashlightColor => Color.Lerp(flashlightColor, _aimModeColor, _aimColorBlend);
    private bool ShouldFlashlightBeActive => flashlightEnabled || (UpgradeMgr != null && UpgradeMgr.RadarAlwaysOn);

    public bool IsAtBaseAimVisual =>
        !_aimModeActive &&
        _aimReturnDelayRemaining <= 0f &&
        _aimBlend <= 0.001f &&
        _aimColorBlend <= 0.001f;

    PlayerVisionField VisionField => PlayerVisionField.Instance;
    const string EnemyLayerName = "Enemies";
    const string RevealableLayerName = "Revealables";
    const string BlopsLayerName = "Blops";

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        EnsureRevealableMask();
    }

    void OnEnable()
    {
        EnsureRevealableMask();
        if (autoResolvePoolsFromHub)
        {
            SonarPoolHub.PoolsChanged += HandlePoolsChanged;
            TryResolvePoolsFromHub();
        }

        EnsureFlashlightState(forceRefresh: true);
    }

    void OnDisable()
    {
        if (autoResolvePoolsFromHub)
            SonarPoolHub.PoolsChanged -= HandlePoolsChanged;

        _turnOnTween?.Kill();
        _turnOnTween = null;
        HideFlashlightVisual();
        _visualShownLastFrame = false;

        if (_upgradeManager != null)
        {
            _upgradeManager.UpgradesChanged -= HandleUpgradeChanged;
            _upgradeManager = null;
        }
    }

    void OnValidate()
    {
        EnsureRevealableMask();
    }

    void LateUpdate()
    {
        if (!_aimModeActive && (_aimBlend > 0f || _aimColorBlend > 0f))
        {
            if (_aimReturnDelayRemaining > 0f)
            {
                _aimReturnDelayRemaining -= Time.deltaTime;
            }
            else
            {
                float returnDuration = Mathf.Max(0.01f, _aimReturnDuration);
                float step = Time.deltaTime / returnDuration;
                _aimBlend = Mathf.MoveTowards(_aimBlend, 0f, step);
                _aimColorBlend = Mathf.MoveTowards(_aimColorBlend, 0f, step);
            }
        }

        if (!ShouldFlashlightBeActive || _flashlightVisual == null)
            return;

        _flashlightAccum += Time.deltaTime;
        if (_flashlightAccum < flashlightUpdateInterval)
            return;

        _flashlightAccum = 0f;

        Vector2 rawOrigin = GetRawOrigin();
        Vector2 rawDir = GetAimDir();
        if (rawDir.sqrMagnitude <= 0.0001f) rawDir = _smoothedDir;
        rawDir.Normalize();

        if (originSmoothTime > 0f)
            _smoothedOrigin = Vector2.SmoothDamp(_smoothedOrigin, rawOrigin, ref _originVel, originSmoothTime);
        else
            _smoothedOrigin = rawOrigin;

        if (aimSmoothTime > 0f)
            _smoothedDir = Vector2.SmoothDamp(_smoothedDir, rawDir, ref _dirVel, aimSmoothTime);
        else
            _smoothedDir = rawDir;

        if (_smoothedDir.sqrMagnitude > 0.0001f)
            _smoothedDir.Normalize();

        UpdateFlashlightVisual(_smoothedOrigin, _smoothedDir);

        if (flashlightReveals)
            RevealInCone(_smoothedOrigin, _smoothedDir, flashlightRevealDuration);
    }

    void EnsureRevealableMask()
    {
        int enemyLayer = LayerMask.NameToLayer(EnemyLayerName);
        if (enemyLayer >= 0)
            revealableMask |= (1 << enemyLayer);

        int revealableLayer = LayerMask.NameToLayer(RevealableLayerName);
        if (revealableLayer >= 0)
            revealableMask |= (1 << revealableLayer);

        int blopsLayer = LayerMask.NameToLayer(BlopsLayerName);
        if (blopsLayer >= 0)
            revealableMask |= (1 << blopsLayer);
    }

    void TryResolvePoolsFromHub()
    {
        if (!autoResolvePoolsFromHub)
            return;

        if (conePool != null)
            return;

        if (SonarPoolHub.TryGet(out var cone, out _))
            conePool = cone;
    }

    void HandlePoolsChanged(SonarConeVisualPool cone, SonarImpactPool impact)
    {
        if (!autoResolvePoolsFromHub)
            return;

        conePool = cone;
    }

    void HandleUpgradeChanged(UpgradeSnapshot snapshot)
    {
        EnsureFlashlightState(forceRefresh: true);
    }

    public void SetAimProvider(Func<Vector2> aimProvider)
    {
        _aimProvider = aimProvider;
    }

    public void ForceFlashlightState(bool enabled)
    {
        flashlightEnabled = enabled;
        EnsureFlashlightState(forceRefresh: true);
    }

    public void SetAimModeOverride(bool active, float overrideRange, float overrideAngle)
    {
        _aimModeActive = active;
        _aimModeRange = Mathf.Max(0f, overrideRange);
        _aimModeAngle = Mathf.Max(1f, overrideAngle);
        if (active)
            _aimReturnDelayRemaining = 0f;
        EnsureFlashlightState(forceRefresh: true);
    }

    public void SetAimChargeVisual(float charge01, Color readyColor)
    {
        _aimBlend = Mathf.Clamp01(charge01);
        _aimModeColor = readyColor;
        _aimColorBlend = _aimBlend >= 0.999f ? 1f : 0f;
    }

    public void ClearAimModeOverride(float returnDuration, float returnDelay)
    {
        _aimModeActive = false;
        _aimReturnDuration = Mathf.Max(0.01f, returnDuration);
        _aimReturnDelayRemaining = Mathf.Max(0f, returnDelay);
    }

    void EnsureFlashlightState(bool forceRefresh = false)
    {
        bool shouldRun = ShouldFlashlightBeActive;

        if (!shouldRun)
        {
            _turnOnTween?.Kill();
            _turnOnTween = null;
            HideFlashlightVisual();
            _visualShownLastFrame = false;
            return;
        }

        EnsureFlashlightVisual();
        if (_flashlightVisual == null)
            return;

        ShowFlashlightVisual();

        Vector2 rawOrigin = GetRawOrigin();
        _smoothedOrigin = rawOrigin;

        Vector2 rawDir = GetAimDir();
        _smoothedDir = rawDir.sqrMagnitude > 0.0001f ? rawDir.normalized : Vector2.right;
        _flashlightAccum = 0f;

        bool shouldPlayTurnOn = !_visualShownLastFrame || forceRefresh;
        if (shouldPlayTurnOn)
        {
            _flashlightVisual.SetContinuousTarget(EffectiveFlashlightRange, EffectiveFlashlightAngle);
            _flashlightVisual.SetRestColor(EffectiveFlashlightColor);
            _flashlightVisual.AnimateFlashlightOff(0.01f, Ease.Linear);
            _turnOnTween = _flashlightVisual.AnimateFlashlightOn(turnOnDuration, turnOnEase);
        }

        _visualShownLastFrame = true;
    }

    void EnsureFlashlightVisual()
    {
        if (conePool == null) return;

        if (_flashlightVisual == null)
            _flashlightVisual = conePool.Get();

        _flashlightVisual.BeginContinuous(transform, 0f, EffectiveFlashlightAngle, obstacleMask, piercingUpgrade, rayCount, colorOverride: EffectiveFlashlightColor);
        _flashlightVisual.SetContinuousTarget(EffectiveFlashlightRange, EffectiveFlashlightAngle);
        _flashlightVisual.SetRestColor(EffectiveFlashlightColor);
    }

    void HideFlashlightVisual()
    {
        if (_flashlightVisual == null) return;
        _flashlightVisual.SetVisible(false);
    }

    void ShowFlashlightVisual()
    {
        if (_flashlightVisual == null) return;
        _flashlightVisual.SetVisible(true);
    }

    void UpdateFlashlightVisual(Vector2 origin, Vector2 dir)
    {
        if (_flashlightVisual == null) return;

        if (_turnOnTween != null && _turnOnTween.IsActive())
            _flashlightVisual.SetContinuousTarget(EffectiveFlashlightRange, EffectiveFlashlightAngle);
        else
            _flashlightVisual.SetContinuousSettings(EffectiveFlashlightRange, EffectiveFlashlightAngle);

        _flashlightVisual.SetRestColor(EffectiveFlashlightColor);
        _flashlightVisual.SetAim(origin, dir);
    }

    Vector2 GetRawOrigin()
    {
        if (useRigidbodyOrigin && _rb != null)
            return _rb.position;

        return (Vector2)transform.position;
    }

    Vector2 GetAimDir()
    {
        if (_aimProvider != null)
        {
            Vector2 dir = _aimProvider.Invoke();
            if (dir.sqrMagnitude > 0.0001f)
            {
                _cachedAimDir = dir.normalized;
                return _cachedAimDir;
            }
        }

        Vector2 tr = transform.right;
        if (tr.sqrMagnitude > 0.0001f)
            _cachedAimDir = tr.normalized;

        return _cachedAimDir;
    }

    void RevealInCone(Vector2 origin, Vector2 forward, float durationOverride)
    {
        Collider2D[] candidates = Physics2D.OverlapCircleAll(origin, EffectiveFlashlightRange, revealableMask);
        PlayerVisionField vision = VisionField;
        float half = EffectiveFlashlightAngle * 0.5f;

        for (int i = 0; i < candidates.Length; i++)
        {
            Collider2D col = candidates[i];

            Vector2 closest = col.ClosestPoint(origin);
            Vector2 toTarget = closest - origin;

            float dist = toTarget.magnitude;
            if (dist <= 0.001f) continue;

            Vector2 dir = toTarget / dist;

            float ang = Vector2.Angle(forward, dir);
            if (ang > half) continue;

            if (!piercingUpgrade)
            {
                RaycastHit2D block = Physics2D.Raycast(origin, dir, dist, obstacleMask);
                if (block.collider != null) continue;
            }

            var enemy = col.GetComponentInParent<EnemyBase>();
            if (enemy != null)
                enemy.NotifyFlashlightTouch(origin);

            if (vision != null && vision.ContainsPoint(closest))
                continue;

            var reveal = col.GetComponentInParent<Revealable>();
            if (reveal != null && reveal.CanBeRevealed)
                reveal.Reveal(durationOverride);
        }
    }
}

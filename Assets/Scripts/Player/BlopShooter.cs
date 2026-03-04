using FMODUnity;
using UnityEngine;

public class BlopShooter : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerController player;
    [SerializeField] private BlopWallet wallet;
    [SerializeField] private Transform firePoint;
    [SerializeField] private BlopProjectile projectilePrefab;

    [Header("Fire Settings")]
    [Tooltip("Fallback cooldown only used when SonarPing is missing.")]
    [SerializeField, Min(0f)] private float fireCooldown = 0.12f;
    [SerializeField] private float projectileSpeed = 16f;
    [SerializeField, Min(0.01f)] private float aimReadyTime = 0.35f;
    [SerializeField, Min(0f)] private float aimingFlashlightRange = 3.5f;
    [SerializeField, Range(1f, 120f)] private float aimingFlashlightAngle = 18f;
    [SerializeField] private Color aimReadyFlashlightColor = new Color(0.6f, 0.95f, 0.65f, 1f);

    [Header("AudioRefs")]
    [SerializeField] private EventReference OnChargeShot;
    [SerializeField] private EventReference OnShoot;

    private float _cooldownTimer;
    private bool _isCharging;
    private float _chargeTimer;
    private PlayerFlashlight _flashlight;
    private bool _aimOverrideApplied;
    private bool _waitingForFlashlightRecovery;

    private void Awake()
    {
        if (player == null)
            player = GetComponent<PlayerController>();

        if (wallet == null)
            wallet = GetComponent<BlopWallet>();

        if (firePoint == null)
            firePoint = transform;

        if (player != null)
            _flashlight = player.GetComponent<PlayerFlashlight>();
    }

    private void Update()
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;

        if (_waitingForFlashlightRecovery && _flashlight != null && _flashlight.IsAtBaseAimVisual)
            _waitingForFlashlightRecovery = false;

        if (_isCharging)
            _chargeTimer += Time.deltaTime;

        UpdateAimVisuals();
    }

    private void OnDisable()
    {
        _isCharging = false;
        _chargeTimer = 0f;
        _waitingForFlashlightRecovery = false;
        _cooldownTimer = 0f;
        if (_flashlight != null && _aimOverrideApplied)
            _flashlight.ClearAimModeOverride(aimReadyTime * 0.5f, 0.5f);
        _aimOverrideApplied = false;
    }

    public bool IsCharging => _isCharging;
    public bool IsAimReady => _isCharging && _chargeTimer >= aimReadyTime;
    public float ChargeProgress01 => Mathf.Clamp01(_chargeTimer / Mathf.Max(0.01f, aimReadyTime));

    public void BeginCharge()
    {
        if (_isCharging || !CanShoot()) return;

        _isCharging = true;
        _chargeTimer = 0f;
        UpdateAimVisuals();
        AudioManager.PlayAttached(OnChargeShot, this.gameObject);
    }

    public bool ReleaseCharge()
    {
        if (!_isCharging) return false;
        _isCharging = false;

        bool canFire = CanShoot();
        bool isReady = _chargeTimer >= aimReadyTime;
        _chargeTimer = 0f;
        UpdateAimVisuals();

        if (!canFire || !isReady) return false;
        if (!wallet.TrySpend(1)) return false;

        Vector2 aimDir = GetAimDirection();
        FireProjectile(aimDir);

        if (_flashlight != null)
            _waitingForFlashlightRecovery = true;
        else
            _cooldownTimer = fireCooldown;
        return true;
    }

    public bool CanShoot()
    {
        if (projectilePrefab == null || wallet == null)
            return false;

        if (_cooldownTimer > 0f)
            return false;

        if (_waitingForFlashlightRecovery)
            return false;

        return wallet.HasBlops;
    }

    private void UpdateAimVisuals()
    {
        if (_flashlight == null)
        {
            if (player == null) return;
            _flashlight = player.GetComponent<PlayerFlashlight>();
            if (_flashlight == null) return;
        }

        if (_isCharging)
        {
            if (!_aimOverrideApplied)
            {
                _flashlight.SetAimModeOverride(true, aimingFlashlightRange, aimingFlashlightAngle);
                _aimOverrideApplied = true;
            }

            _flashlight.SetAimChargeVisual(ChargeProgress01, aimReadyFlashlightColor);
            return;
        }

        if (_aimOverrideApplied)
        {
            _flashlight.ClearAimModeOverride(aimReadyTime * 0.5f, 0.5f);
            _aimOverrideApplied = false;
        }
    }

    private Vector2 GetAimDirection()
    {
        if (player == null)
            return Vector2.right;

        Vector2 dir = player.AimDir;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.right;

        return dir.normalized;
    }

    private void FireProjectile(Vector2 direction)
    {
        if (projectilePrefab == null) return;

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        var projectile = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        float maxDistance = aimingFlashlightRange;
        projectile.SetMaxTravelDistance(maxDistance);
        projectile.Launch(direction, projectileSpeed);
        AudioManager.PlayAt(OnShoot, transform.position);
    }

    private float ResolveProjectileVisionRange()
    {
        if (player == null)
            return 0f;

        var visionField = player.GetComponentInChildren<PlayerVisionField>();
        if (visionField == null)
            visionField = PlayerVisionField.Instance;

        if (visionField == null)
            return 0f;

        return visionField.CurrentOuterRadius;
    }
}

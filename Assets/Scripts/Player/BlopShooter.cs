using UnityEngine;

public class BlopShooter : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerController player;
    [SerializeField] private BlopWallet wallet;
    [SerializeField] private Transform firePoint;
    [SerializeField] private BlopProjectile projectilePrefab;

    [Header("Fire Settings")]
    [SerializeField, Min(0f)] private float fireCooldown = 0.12f;
    [SerializeField] private float projectileSpeed = 16f;

    private float _cooldownTimer;

    private void Awake()
    {
        if (player == null)
            player = GetComponent<PlayerController>();

        if (wallet == null)
            wallet = GetComponent<BlopWallet>();

        if (firePoint == null)
            firePoint = transform;
    }

    private void Update()
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;
    }

    public bool TryShoot()
    {
        if (!CanShoot()) return false;
        if (!wallet.TrySpend(1)) return false;

        Vector2 aimDir = GetAimDirection();
        FireProjectile(aimDir);

        _cooldownTimer = fireCooldown;
        return true;
    }

    public bool CanShoot()
    {
        if (projectilePrefab == null || wallet == null)
            return false;

        if (_cooldownTimer > 0f)
            return false;

        return wallet.HasBlops;
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
        float maxDistance = ResolveProjectileVisionRange();
        projectile.SetMaxTravelDistance(maxDistance);
        projectile.Launch(direction, projectileSpeed);
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

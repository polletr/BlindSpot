using DG.Tweening;
using MoreMountains.Feedbacks;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BlopProjectile : MonoBehaviour
{
    [Header("Motion")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private float maxLifetime = 4f;

    [Header("Damage")]
    [SerializeField] private int damage = 1;
    [SerializeField] private LayerMask damageLayers;

    [Header("Stick On Impact")]
    [SerializeField] private bool stickOnImpact = true;
    [SerializeField] private LayerMask stickLayers;
    [SerializeField] private float stickSurfaceOffset = 0.01f;
    [SerializeField] private float stickSquash = 0.7f;
    [SerializeField] private float stickStretch = 1.15f;
    [SerializeField] private float stickSquishTime = 0.06f;

    [Header("Squish Feel")]
    [SerializeField] private bool useSquish = true;
    [SerializeField] private float stretchAmount = 1.2f;
    [SerializeField] private float squashAmount = 0.85f;
    [SerializeField] private float stretchTime = 0.08f;
    [SerializeField] private float settleTime = 0.12f;

    private Rigidbody2D _rb;
    private float _lifeTimer;
    private Tween _squishTween;
    private Vector2 _launchOrigin;
    private float _maxDistanceFromOrigin = -1f;
    private bool _isStuck;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
    }

    private void OnEnable()
    {
        _lifeTimer = maxLifetime;
    }

    private void OnDisable()
    {
        _squishTween?.Kill();
        _squishTween = null;
    }

    private void Update()
    {
        if (_isStuck) return;

        if (maxLifetime <= 0f) return;
        _lifeTimer -= Time.deltaTime;
        if (_lifeTimer <= 0f)
            Destroy(gameObject);

        if (_maxDistanceFromOrigin > 0f)
        {
            float sqr = ((Vector2)transform.position - _launchOrigin).sqrMagnitude;
            if (sqr >= _maxDistanceFromOrigin * _maxDistanceFromOrigin)
                Destroy(gameObject);
        }
    }

    public void Launch(Vector2 direction, float overrideSpeed = -1f)
    {
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.right;

        Vector2 dir = direction.normalized;
        float finalSpeed = overrideSpeed > 0f ? overrideSpeed : speed;

        _rb.linearVelocity = dir * finalSpeed;
        _rb.angularVelocity = 0f;
        _rb.simulated = true;
        _isStuck = false;
        transform.SetParent(null, true);
        _launchOrigin = transform.position;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        PlaySquish(dir);
    }

    public void SetMaxTravelDistance(float maxDistance)
    {
        _maxDistanceFromOrigin = maxDistance > 0f ? maxDistance : -1f;
    }

    private void PlaySquish(Vector2 dir)
    {
        if (!useSquish) return;

        _squishTween?.Kill();
        Vector3 originalScale = transform.localScale;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        transform.localScale = originalScale;
        transform.localRotation = Quaternion.Euler(0f, 0f, angle);

        Vector3 stretchScale = new Vector3(
            originalScale.x * stretchAmount,
            originalScale.y * squashAmount,
            originalScale.z
        );

        _squishTween = DOTween.Sequence()
            .Append(transform.DOScale(stretchScale, stretchTime).SetEase(Ease.OutQuad))
            .Append(transform.DOScale(originalScale, settleTime).SetEase(Ease.OutBack, 1.1f));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isStuck) return;
        bool isDamageLayer = IsDamageLayer(other.gameObject.layer);

        var enemy = other.GetComponentInParent<EnemyBase>();
        if (enemy == null) enemy = other.GetComponent<EnemyBase>();

        if (enemy != null)
        {
            if (!isDamageLayer) return;
            enemy.TakeDamage(damage);
            if (enemy.TryGetComponent<MMF_Player>(out MMF_Player hitFeedback))
                hitFeedback.PlayFeedbacks();
            Destroy(gameObject);
        }
        else if (CanStickToLayer(other.gameObject.layer))
        {
            Vector2 fallbackNormal = -_rb.linearVelocity.normalized;
            Vector2 closest = other.ClosestPoint(transform.position);
            Vector2 computedNormal = ((Vector2)transform.position - closest).normalized;
            if (computedNormal.sqrMagnitude > 0.0001f)
                fallbackNormal = computedNormal;

            StickToSurface(other.transform, closest, fallbackNormal);
        }
        else
        {
            if (isDamageLayer)
                Destroy(gameObject);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_isStuck) return;
        if (!CanStickToLayer(collision.gameObject.layer)) return;
        if (!stickOnImpact) return;

        ContactPoint2D contact = collision.GetContact(0);
        Vector2 point = contact.point;
        Vector2 normal = contact.normal.sqrMagnitude > 0.0001f
            ? contact.normal.normalized
            : -_rb.linearVelocity.normalized;

        StickToSurface(collision.transform, point, normal);
    }

    private bool IsDamageLayer(int layer)
    {
        if (damageLayers.value == 0) return true;
        return ((1 << layer) & damageLayers.value) != 0;
    }

    private bool CanStickToLayer(int layer)
    {
        if (!stickOnImpact) return false;
        if (stickLayers.value == 0) return false;
        return ((1 << layer) & stickLayers.value) != 0;
    }

    private void StickToSurface(Transform surface, Vector2 hitPoint, Vector2 normal)
    {
        _isStuck = true;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        _rb.simulated = false;

        if (normal.sqrMagnitude < 0.0001f)
            normal = Vector2.up;
        normal.Normalize();

        transform.position = hitPoint + normal * Mathf.Max(0f, stickSurfaceOffset);
        transform.SetParent(surface, true);

        Vector2 intoSurface = -normal;
        float angle = Mathf.Atan2(intoSurface.y, intoSurface.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        _squishTween?.Kill();
        Vector3 baseScale = transform.localScale;
        Vector3 stuckScale = new Vector3(
            baseScale.x * stickSquash,
            baseScale.y * stickStretch,
            baseScale.z);

        _squishTween = transform.DOScale(stuckScale, Mathf.Max(0.01f, stickSquishTime))
            .SetEase(Ease.OutQuad);
    }
}

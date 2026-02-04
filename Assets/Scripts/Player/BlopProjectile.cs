using DG.Tweening;
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

    [Header("Squish Feel")]
    [SerializeField] private bool useSquish = true;
    [SerializeField] private float stretchAmount = 1.2f;
    [SerializeField] private float squashAmount = 0.85f;
    [SerializeField] private float stretchTime = 0.08f;
    [SerializeField] private float settleTime = 0.12f;

    private Rigidbody2D _rb;
    private float _lifeTimer;
    private Tween _squishTween;

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
        if (maxLifetime <= 0f) return;
        _lifeTimer -= Time.deltaTime;
        if (_lifeTimer <= 0f)
            Destroy(gameObject);
    }

    public void Launch(Vector2 direction, float overrideSpeed = -1f)
    {
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.right;

        Vector2 dir = direction.normalized;
        float finalSpeed = overrideSpeed > 0f ? overrideSpeed : speed;

        _rb.linearVelocity = dir * finalSpeed;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        PlaySquish(dir);
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
        if (!IsDamageLayer(other.gameObject.layer)) return;

        var enemy = other.GetComponentInParent<EnemyBase>();
        if (enemy == null) enemy = other.GetComponent<EnemyBase>();

        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            Destroy(gameObject);
        }
    }

    private bool IsDamageLayer(int layer)
    {
        if (damageLayers.value == 0) return true;
        return ((1 << layer) & damageLayers.value) != 0;
    }
}

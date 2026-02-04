using System.Collections;
using UnityEngine;
using DG.Tweening;

public class CurrencyDrop : MonoBehaviour
{
    [Header("Value")]
    [Min(1)] public int amount = 1;

    [Header("Filtering")]
    public LayerMask playerLayerMask;

    [Header("Delayed Magnet")]
    public float magnetDelay = 0.10f;
    public float magnetDelayRandom = 0.10f;

    [Header("Homing Movement (Smooth)")]
    [Tooltip("How quickly it converges. Lower = smoother/slower, higher = snappier.")]
    public float smoothTime = 0.08f;

    [Tooltip("Clamp max speed to avoid extreme acceleration if far away.")]
    public float maxSpeed = 25f;

    [Tooltip("Collect when within this distance of the target.")]
    public float arriveDistance = 0.08f;

    [Tooltip("Optional: speed ramp. 0 = no ramp; higher ramps in faster.")]
    public float rampUpTime = 0.10f;

    [Header("References (optional)")]
    public Collider2D magnetTriggerCollider;
    public Collider2D collectTriggerCollider;

    private CurrencyDropPool _pool;
    public string PoolKey { get; private set; }

    private Tween _delayTween;
    private Coroutine _magnetCoroutine;

    private bool _magneting;
    private bool _collected;

    private Transform _target;
    private BlopWallet _wallet;

    // SmoothDamp state
    private Vector3 _vel;
    private float _magnetStartTime;

    public void AssignPool(CurrencyDropPool pool, string poolKey)
    {
        _pool = pool;
        PoolKey = poolKey;
    }

    public void OnSpawned()
    {
        _delayTween?.Kill();
        _delayTween = null;
        StopMagnet(false);

        _magneting = false;
        _collected = false;

        _target = null;
        _wallet = null;

        _vel = Vector3.zero;
        _magnetStartTime = 0f;

        if (magnetTriggerCollider != null) magnetTriggerCollider.enabled = true;
        if (collectTriggerCollider != null) collectTriggerCollider.enabled = true;
    }

    public void OnDespawned()
    {
        _delayTween?.Kill();
        _delayTween = null;
        StopMagnet(false);

        _magneting = false;
        _collected = false;

        _target = null;
        _wallet = null;

        _vel = Vector3.zero;

        if (magnetTriggerCollider != null) magnetTriggerCollider.enabled = true;
        if (collectTriggerCollider != null) collectTriggerCollider.enabled = true;
    }

    public void OnMagnetTriggerEnter(Collider2D other)
    {
        if (_collected) return;
        if (!IsPlayerLayer(other.gameObject.layer)) return;

        var wallet = other.GetComponentInParent<BlopWallet>();
        if (wallet == null) return;

        if (!wallet.CanCollect) return;

        _target = wallet.transform;
        _wallet = wallet;

        if (_magneting) return;

        if (_delayTween == null)
        {
            float delay = magnetDelay + (magnetDelayRandom > 0f ? Random.Range(0f, magnetDelayRandom) : 0f);

            _delayTween = DOVirtual.DelayedCall(delay, () =>
            {
                _delayTween = null;
                if (_collected) return;
                if (_target == null || _wallet == null) return;

                if (!_wallet.CanCollect)
                {
                    _target = null;
                    _wallet = null;
                    return;
                }

                StartMagnet();
            });
        }
    }

    public void OnCollectTriggerEnter(Collider2D other)
    {
        if (_collected) return;
        if (!IsPlayerLayer(other.gameObject.layer)) return;

        if (_wallet == null || _target == null)
        {
            var wallet = other.GetComponentInParent<BlopWallet>();
            if (wallet == null) return;

            if (!wallet.CanCollect) return;

            _target = wallet.transform;
            _wallet = wallet;
        }

        if (_wallet == null || !_wallet.CanCollect) return;

        CollectNow();
    }

    private bool IsPlayerLayer(int layer)
    {
        if (playerLayerMask.value == 0) return true;
        return ((1 << layer) & playerLayerMask.value) != 0;
    }

    private void StartMagnet()
    {
        if (_collected || _magneting) return;
        if (_wallet == null || !_wallet.CanCollect) return;

        _magneting = true;
        _magnetStartTime = Time.time;
        _vel = Vector3.zero;

        if (magnetTriggerCollider != null) magnetTriggerCollider.enabled = false;
        if (collectTriggerCollider != null) collectTriggerCollider.enabled = false;

        _magnetCoroutine = StartCoroutine(MagnetRoutine());
    }

    private void StopMagnet(bool reactivateColliders = true)
    {
        if (_magnetCoroutine != null)
        {
            StopCoroutine(_magnetCoroutine);
        }

        FinalizeMagnet(reactivateColliders);
    }

    private void FinalizeMagnet(bool reactivateColliders)
    {
        _magneting = false;
        _magnetCoroutine = null;
        _vel = Vector3.zero;
        _magnetStartTime = 0f;
        _target = null;
        _wallet = null;

        if (reactivateColliders)
        {
            if (magnetTriggerCollider != null) magnetTriggerCollider.enabled = true;
            if (collectTriggerCollider != null) collectTriggerCollider.enabled = true;
        }
    }

    private IEnumerator MagnetRoutine()
    {
        while (_magneting && !_collected)
        {
            if (_target == null || _wallet == null)
            {
                FinalizeMagnet(true);
                yield break;
            }

            if (!_wallet.CanCollect)
            {
                FinalizeMagnet(true);
                yield break;
            }

            Vector3 targetPos = _target.position;
            float ramp = rampUpTime > 0f
                ? Mathf.Clamp01((Time.time - _magnetStartTime) / rampUpTime)
                : 1f;

            float effectiveMaxSpeed = maxSpeed * ramp;

            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPos,
                ref _vel,
                smoothTime,
                effectiveMaxSpeed,
                Time.deltaTime
            );

            if ((transform.position - targetPos).sqrMagnitude <= arriveDistance * arriveDistance)
            {
                CollectNow(false);
                yield break;
            }

            yield return null;
        }
    }

    private void CollectNow(bool stopMagnetCoroutine = true)
    {
        if (_collected) return;
        _collected = true;

        var wallet = _wallet;

        if (stopMagnetCoroutine) StopMagnet(false);
        else FinalizeMagnet(false);

        _delayTween?.Kill();
        _delayTween = null;

        if (wallet != null)
            wallet.Add(amount);

        if (_pool != null) _pool.Return(this);
        else gameObject.SetActive(false);
    }
}


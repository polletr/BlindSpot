using UnityEngine;
using DG.Tweening;

public class UIFlashlightSweep : MonoBehaviour
{
    [Header("Angle (degrees)")]
    public float maxAngle = 35f;        // sweep distance toward the negative side
    public float angleJitter = 6f;      // adds randomness to the negative sweep target

    [Header("Timing (seconds)")]
    public Vector2 rotateDuration = new Vector2(0.8f, 1.4f);
    public Vector2 pauseDuration = new Vector2(0.15f, 0.6f);

    [Header("Easing")]
    public Ease ease = Ease.InOutSine;

    RectTransform _rt;
    Tween _tween;
    float _positiveLimit;
    bool _goNegative = true;

    void Awake()
    {
        _rt = transform as RectTransform;
    }

    void OnEnable()
    {
        StartSweepLoop();
    }

    void OnDisable()
    {
        _tween?.Kill();
    }

    void StartSweepLoop()
    {
        _tween?.Kill();

        // Keep the current starting rotation as the positive limit.
        _positiveLimit = NormalizeAngle(_rt.localEulerAngles.z);
        _rt.localRotation = Quaternion.Euler(0, 0, _positiveLimit);
        _goNegative = true;

        ScheduleNext();
    }

    void ScheduleNext()
    {
        float target;
        if (_goNegative)
        {
            float jitter = Random.Range(-angleJitter, angleJitter);
            float sweepAmount = Mathf.Max(0f, maxAngle + jitter);
            target = _positiveLimit - sweepAmount;
        }
        else
        {
            target = _positiveLimit;
        }
        _goNegative = !_goNegative;

        float dur = Random.Range(rotateDuration.x, rotateDuration.y);
        float pause = Random.Range(pauseDuration.x, pauseDuration.y);

        _tween = DOTween.Sequence()
            .Append(_rt.DOLocalRotate(new Vector3(0, 0, target), dur, RotateMode.Fast))
            .Join(_rt.DOScale(1f, dur)) // optional placeholder if you want extra joins later
            .SetEase(ease)
            .AppendInterval(pause)
            .OnComplete(ScheduleNext);
    }

    static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}

using DG.Tweening;
using UnityEngine;
public class TriangleEnemy : EnemyBase
{
    [Header("Dash Attack")]
    public float chargeDistance = 2.5f;
    public float minChaseTimeBeforeDash = 1.5f;
    public float dashCooldown = 10f;
    public float chargeDuration = 0.35f;
    public float dashDuration = 0.25f;
    public float dashSpeedMultiplier = 4f;
    [Header("Dash Targeting")]
    public float directionCommitLeadTime = 0.08f;
    public ParticleSystem chargeVfx;
    [SerializeField] Transform shakeTarget;
    [SerializeField] float shakeStrength = 0.2f;
    [SerializeField] int shakeVibrato = 25;
    [Header("Dash Stretch")]
    public float dashStretchAmount = 1.15f;
    public float dashSquashAmount = 0.85f;
    public float dashStretchTime = 0.08f;
    public float dashSettleTime = 0.15f;
    [SerializeField] Transform dashVisualRoot;
    Vector2 dashDirection = Vector2.up;
    bool dashDirectionLocked;
    bool rotationLocked;
    float nextDashAllowedTime;
    Tween chargeShakeTween;
    Tween dashStretchTween;
    Vector3 shakeTargetRestLocalPos;
    Vector3 dashVisualRestScale = Vector3.one;
    // States
    public TriangleIdleState IdleState { get; private set; }
    public TriangleAlertState AlertState { get; private set; }
    public TriangleChaseState ChaseState { get; private set; }
    public TriangleDashState DashState { get; private set; }
    public TriangleRepositionState RepositionState { get; private set; }
    public bool IsPlayerDead => IsTargetPlayerDead;
    protected override bool ShouldRotateTowardPlayer => !rotationLocked;
    protected override void Awake()
    {
        base.Awake();
        if (shakeTarget == null)
            shakeTarget = transform;
        shakeTargetRestLocalPos = shakeTarget.localPosition;
        if (dashVisualRoot == null)
            dashVisualRoot = shakeTarget != null ? shakeTarget : transform;
        if (dashVisualRoot != null)
            dashVisualRestScale = dashVisualRoot.localScale;
        nextDashAllowedTime = Time.time;
        IdleState = new TriangleIdleState();
        AlertState = new TriangleAlertState();
        ChaseState = new TriangleChaseState();
        DashState = new TriangleDashState();
        RepositionState = new TriangleRepositionState();
        ChangeState(IdleState);
    }
    public bool ReadyToCharge()
    {
        if (!HasPlayer || IsPlayerDead) return false;
        return DistToPlayer <= chargeDistance;
    }
    public bool PlayerFarBeyondChargeRange()
    {
        if (!HasPlayer) return true;
        return DistToPlayer >= chargeDistance * 2f;
    }
    public bool DashOffCooldown => Time.time >= nextDashAllowedTime;
    public bool ShouldStartCharge(float chaseTime)
    {
        if (IsPlayerDead) return false;
        if (!DashOffCooldown) return false;
        if (chaseTime < minChaseTimeBeforeDash) return false;
        return ReadyToCharge();
    }
    public void BeginChargeWindup()
    {
        dashDirectionLocked = false;
        rotationLocked = false;
        dashDirection = ForwardDir;
        ResetDashStretchTween(true);
        FreezeTipSelection(chargeDuration + dashDuration);
        PlayChargeVfx();
        StartChargeShake(chargeDuration);
    }
    public void UpdateChargeWindup(float elapsedTime)
    {
        if (dashDirectionLocked) return;
        if (elapsedTime >= ChargeCommitTime())
            CommitDashDirection();
    }
    float ChargeCommitTime()
    {
        float lead = Mathf.Max(0f, directionCommitLeadTime);
        return Mathf.Clamp(chargeDuration - lead, 0f, chargeDuration);
    }
    void CommitDashDirection()
    {
        Vector2 dir = ForwardDir;
        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.up;
        dashDirection = dir.normalized;
        dashDirectionLocked = true;
        rotationLocked = true;
        AlignBodyToDash();
    }
    void AlignBodyToDash()
    {
        if (dashDirection.sqrMagnitude < 0.0001f) return;
        transform.up = dashDirection;
    }
    public void OnDashStarted()
    {
        CommitDashDirection();
        StopChargeShake(true);
        StopChargeVfx();
        PlayDashStretch();
    }
    public void CancelCharge()
    {
        dashDirectionLocked = false;
        rotationLocked = false;
        StopChargeShake(true);
        StopChargeVfx();
    }
    void PlayChargeVfx()
    {
        if (chargeVfx == null) return;
        chargeVfx.transform.position = ActiveTip != null ? ActiveTip.position : transform.position;
        chargeVfx.transform.up = ForwardDir;
        if (!chargeVfx.isPlaying)
            chargeVfx.Play(true);
        else
            chargeVfx.Simulate(0f, true, true);
    }
    public void StopChargeVfx()
    {
        if (chargeVfx == null) return;
        chargeVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    void StartChargeShake(float duration)
    {
        if (shakeTarget == null || duration <= 0f) return;
        shakeTargetRestLocalPos = shakeTarget.localPosition;
        StopChargeShake(false);
        chargeShakeTween = shakeTarget.DOShakePosition(duration, shakeStrength, shakeVibrato, 90f, false, true)
            .SetEase(Ease.Linear);
    }
    void StopChargeShake(bool resetPosition)
    {
        if (chargeShakeTween != null)
        {
            bool completeTween = resetPosition && shakeTarget == transform;
            chargeShakeTween.Kill(completeTween);
            chargeShakeTween = null;
        }
        if (resetPosition && shakeTarget != null && shakeTarget != transform)
            shakeTarget.localPosition = shakeTargetRestLocalPos;
    }
    void PlayDashStretch()
    {
        if (dashVisualRoot == null) return;
        ResetDashStretchTween(true);
        Vector3 stretchScale = new Vector3(dashSquashAmount, dashStretchAmount, 1f);
        dashStretchTween = DOTween.Sequence()
            .Append(dashVisualRoot.DOScale(stretchScale, dashStretchTime).SetEase(Ease.OutQuad))
            .Append(dashVisualRoot.DOScale(dashVisualRestScale, dashSettleTime).SetEase(Ease.OutBack, 1.2f));
    }
    void ResetDashStretchTween(bool resetScale)
    {
        if (dashStretchTween != null)
        {
            dashStretchTween.Kill(false);
            dashStretchTween = null;
        }
        if (resetScale && dashVisualRoot != null)
            dashVisualRoot.localScale = dashVisualRestScale;
    }
    public void PerformDashMove()
    {
        Vector2 dir = dashDirectionLocked ? dashDirection : ForwardDir;
        if (dir.sqrMagnitude < 0.0001f) dir = ForwardDir;
        RB.linearVelocity = dir.normalized * (CurrentMoveSpeed * dashSpeedMultiplier);
    }
    public void FinishDash()
    {
        dashDirectionLocked = false;
        rotationLocked = false;
        StopChargeShake(true);
        StopChargeVfx();
        nextDashAllowedTime = Time.time + dashCooldown;
    }
    void OnDisable()
    {
        ResetDashStretchTween(true);
        StopChargeShake(false);
    }
}
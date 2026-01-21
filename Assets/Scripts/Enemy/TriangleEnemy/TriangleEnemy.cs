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
    public ParticleSystem chargeVfx;
    [SerializeField] Transform shakeTarget;
    [SerializeField] float shakeStrength = 0.2f;
    [SerializeField] int shakeVibrato = 25;

    Vector2 dashDirection = Vector2.up;
    bool dashDirectionLocked;
    float nextDashAllowedTime;
    Tween chargeShakeTween;
    Vector3 shakeTargetRestLocalPos;
    PlayerController playerController;

    // States
    public TriangleIdleState IdleState { get; private set; }
    public TriangleAlertState AlertState { get; private set; }
    public TriangleChaseState ChaseState { get; private set; }
    public TriangleDashState DashState { get; private set; }
    public TriangleRepositionState RepositionState { get; private set; }

    public bool IsPlayerDead => playerController != null && playerController.IsDead;

    protected override void Awake()
    {
        base.Awake();

        playerController = player ? player.GetComponent<PlayerController>() : null;
        if (shakeTarget == null)
            shakeTarget = transform;
        shakeTargetRestLocalPos = shakeTarget.localPosition;
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
        dashDirection = DirToPlayer;
        if (dashDirection.sqrMagnitude < 0.0001f)
            dashDirection = ForwardDir;
        dashDirection = dashDirection.normalized;
        dashDirectionLocked = true;
        AlignBodyToDash();
        FreezeTipSelection(chargeDuration + dashDuration);
        PlayChargeVfx();
        StartChargeShake(chargeDuration);
    }

    void AlignBodyToDash()
    {
        if (dashDirection.sqrMagnitude < 0.0001f) return;
        transform.up = dashDirection;
    }

    public void OnDashStarted()
    {
        StopChargeShake(true);
        StopChargeVfx();
    }

    public void CancelCharge()
    {
        dashDirectionLocked = false;
        StopChargeShake(true);
        StopChargeVfx();
    }

    void PlayChargeVfx()
    {
        if (chargeVfx == null) return;
        chargeVfx.transform.position = ActiveTip != null ? ActiveTip.position : transform.position;
        chargeVfx.transform.up = dashDirection;
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
            chargeShakeTween.Kill(false);
            chargeShakeTween = null;
        }
        if (resetPosition && shakeTarget != null)
            shakeTarget.localPosition = shakeTargetRestLocalPos;
    }

    public void PerformDashMove()
    {
        Vector2 dir = dashDirectionLocked ? dashDirection : DirToPlayer;
        if (dir.sqrMagnitude < 0.0001f) dir = ForwardDir;
        dir = dir.normalized;
        RB.linearVelocity = dir * (moveSpeed * dashSpeedMultiplier);
    }

    public void FinishDash()
    {
        dashDirectionLocked = false;
        StopChargeShake(true);
        StopChargeVfx();
        nextDashAllowedTime = Time.time + dashCooldown;
    }
}

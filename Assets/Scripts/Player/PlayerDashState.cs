using UnityEngine;

public sealed class PlayerDashState : PlayerStateBase
{
    private const float PostDashSlowdownMultiplier = 3f;
    private float timeRemaining;
    private Vector2 dashDir;

    public override void Enter(PlayerController p)
    {
        float dashVisualDuration = p.stretchTime + p.settleTime;
        timeRemaining = (dashVisualDuration > 0.001f) ? dashVisualDuration : p.DashDuration;

        dashDir = p.GetCommittedDashDirection();
        if (dashDir.sqrMagnitude < 0.0001f)
        {
            p.ChangeState(p.MoveState);
            return;
        }

        dashDir.Normalize();

        // Immediate burst
        p.RB.linearVelocity = dashDir * p.DashSpeed;
        p.CurrentVelocity = p.RB.linearVelocity;

        // Presentation layer hook
        p.PlayDashFeel(dashDir);
    }

    public override void FixedTick(PlayerController p)
    {
        timeRemaining -= Time.fixedDeltaTime;

        // Enforce commitment (no steering)
        p.RB.linearVelocity = dashDir * p.DashSpeed;

        if (timeRemaining <= 0f)
            p.ChangeState(p.MoveState);
    }

    public override void Exit(PlayerController p)
    {
        // Keep a touch of momentum, then let move state quickly settle to normal speed.
        Vector2 postDashVelocity = p.MoveInput * p.MovementSpeed;
        Vector2 carryVelocity = Vector2.MoveTowards(
            p.RB.linearVelocity,
            postDashVelocity,
            p.deceleration * PostDashSlowdownMultiplier * Time.fixedDeltaTime
        );

        p.CurrentVelocity = carryVelocity;
        p.RB.linearVelocity = carryVelocity;
    }
}

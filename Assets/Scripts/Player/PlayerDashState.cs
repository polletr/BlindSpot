using UnityEngine;

public sealed class PlayerDashState : PlayerStateBase
{
    private float timeRemaining;
    private Vector2 dashDir;

    public override void Enter(PlayerController p)
    {
        timeRemaining = p.DashDuration;

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
        // Preserve momentum into move state
        p.CurrentVelocity = p.RB.linearVelocity;
    }
}

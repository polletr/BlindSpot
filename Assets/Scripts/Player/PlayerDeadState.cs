using UnityEngine;

public sealed class PlayerDeadState : PlayerStateBase
{
    public override void Enter(PlayerController p)
    {
        p.SetMoveInput(Vector2.zero);
        p.CurrentVelocity = Vector2.zero;
        p.RB.linearVelocity = Vector2.zero;

        p.KillDashFeel();
    }

    public override void FixedTick(PlayerController p)
    {
        // Stay inert
        p.RB.linearVelocity = Vector2.zero;
    }
}

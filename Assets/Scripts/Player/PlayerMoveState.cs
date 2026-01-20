using UnityEngine;

public sealed class PlayerMoveState : PlayerStateBase
{
    public override void FixedTick(PlayerController p)
    {
        Vector2 targetVelocity = p.MoveInput * p.moveSpeed;
        float rate = (p.MoveInput.sqrMagnitude > 0.001f) ? p.acceleration : p.deceleration;

        p.CurrentVelocity = Vector2.MoveTowards(
            p.CurrentVelocity,
            targetVelocity,
            rate * Time.fixedDeltaTime
        );

        p.RB.linearVelocity = p.CurrentVelocity;
    }
}
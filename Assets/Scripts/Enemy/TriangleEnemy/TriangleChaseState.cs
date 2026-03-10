using UnityEngine;

public sealed class TriangleChaseState : EnemyStateBase
{
    float _chaseTimer;

    public override void Enter(EnemyBase e)
    {
        _chaseTimer = 0f;
        var t = (TriangleEnemy)e;
        t.SetChaseRevealForced(true, instant: true);
        t.ResetChaseSteering();
        t.InvalidateNavPath();
    }

    public override void Exit(EnemyBase e)
    {
        var t = (TriangleEnemy)e;
        t.SetChaseRevealForced(false, instant: false);
    }

    public override void Tick(EnemyBase e)
    {
        var t = (TriangleEnemy)e;

        if ((t.PlayerBeyondLoseRadius() && !t.IsForcedAggroActive) || t.IsPlayerDead)
        {
            if (!t.IsPlayerDead && t.HasFlashlightStimulus)
                t.ChangeState(t.CuriousState);
            else
                t.ChangeState(t.IdleState);
            return;
        }

        _chaseTimer += Time.deltaTime;
        if (t.ShouldStartCharge(_chaseTimer))
        {
            t.ChangeState(t.AlertState);
        }
    }

    public override void FixedTick(EnemyBase e)
    {
        var t = (TriangleEnemy)e;

        float speedFraction = t.EvaluateChaseSpeedFraction();
        Vector2 moveDir = t.GetSmoothedChaseDirection();
        t.ApplyChaseMove(moveDir, speedFraction);
    }
}


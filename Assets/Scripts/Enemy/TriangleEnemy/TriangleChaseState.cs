using UnityEngine;

public sealed class TriangleChaseState : EnemyStateBase
{
    float _chaseTimer;

    public override void Enter(EnemyBase e)
    {
        _chaseTimer = 0f;
        var t = (TriangleEnemy)e;
        t.ResetChaseSteering();
    }

    public override void Tick(EnemyBase e)
    {
        var t = (TriangleEnemy)e;

        if (t.PlayerBeyondLoseRadius() || t.IsPlayerDead)
        {
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


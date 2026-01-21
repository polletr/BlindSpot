using UnityEngine;

public sealed class TriangleDashState : EnemyStateBase
{
    float _timer;

    public override void Enter(EnemyBase e)
    {
        _timer = 0f;
        var t = (TriangleEnemy)e;
        t.OnDashStarted();
    }

    public override void Tick(EnemyBase e)
    {
        var t = (TriangleEnemy)e;

        _timer += Time.deltaTime;

        if (t.IsPlayerDead)
        {
            t.ChangeState(t.RepositionState);
            return;
        }

        if (_timer >= t.dashDuration)
        {
            t.ChangeState(t.RepositionState);
        }
    }

    public override void FixedTick(EnemyBase e)
    {
        var t = (TriangleEnemy)e;
        t.PerformDashMove();
    }

    public override void Exit(EnemyBase e)
    {
        ((TriangleEnemy)e).FinishDash();
    }
}


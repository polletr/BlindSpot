using UnityEngine;

public sealed class TriangleAlertState : EnemyStateBase
{
    float _time;

    public override void Enter(EnemyBase e)
    {
        _time = 0f;
        var t = (TriangleEnemy)e;
        t.PickActiveTip(force: true);
        t.FreezeTipSelection(0.3f);
    }

    public override void Tick(EnemyBase e)
    {
        var t = (TriangleEnemy)e;

        if (t.PlayerBeyondLoseRadius())
        {
            t.ChangeState(t.IdleState);
            return;
        }

        _time += Time.deltaTime;
        if (_time >= t.alertTime)
            t.ChangeState(t.ChaseState);
    }

    public override void FixedTick(EnemyBase e)
    {
        e.StopMove(60f);
    }
}


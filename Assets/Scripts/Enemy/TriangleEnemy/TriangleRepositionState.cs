using UnityEngine;

public sealed class TriangleRepositionState : EnemyStateBase
{
    float _time;

    public override void Enter(EnemyBase e)
    {
        _time = 0f;
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
        if (_time >= t.repositionTime)
            t.ChangeState(t.AlertState);
    }

    public override void FixedTick(EnemyBase e)
    {
        var t = (TriangleEnemy)e;
        t.MoveForward(0.9f);
    }
}

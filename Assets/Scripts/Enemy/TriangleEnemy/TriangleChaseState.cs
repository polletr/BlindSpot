using UnityEngine;

public sealed class TriangleChaseState : EnemyStateBase
{
    float _time;

    public override void Enter(EnemyBase e)
    {
        _time = 0f;
        var t = (TriangleEnemy)e;

        // Lock tip for entire chase burst
        t.PickActiveTip(force: true);
        t.FreezeTipSelection(t.chaseCommitTime);
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
        if (_time >= t.chaseCommitTime)
            t.ChangeState(t.RepositionState);
    }

    public override void FixedTick(EnemyBase e)
    {
        var t = (TriangleEnemy)e;
        t.MoveForward(t.chaseSpeedMultiplier);
    }
}


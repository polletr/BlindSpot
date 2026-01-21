using UnityEngine;

public sealed class TriangleAlertState : EnemyStateBase
{
    float _timer;

    public override void Enter(EnemyBase e)
    {
        _timer = 0f;
        var t = (TriangleEnemy)e;
        t.BeginChargeWindup();
    }

    public override void Tick(EnemyBase e)
    {
        var t = (TriangleEnemy)e;

        if (t.PlayerBeyondLoseRadius())
        {
            t.CancelCharge();
            t.ChangeState(t.IdleState);
            return;
        }

        _timer += Time.deltaTime;
        if (_timer >= t.chargeDuration)
        {
            t.ChangeState(t.DashState);
        }
    }

    public override void FixedTick(EnemyBase e)
    {
        e.StopMove(80f);
    }
}




using UnityEngine;

public sealed class SquarePatrolState : EnemyStateBase
{
    float _waitTimer;
    bool _waiting;

    public override void Enter(EnemyBase e)
    {
        _waitTimer = 0f;
        _waiting = false;
        var square = (SquareEnemy)e;
        if (!square.EnsurePatrolRoute())
            square.ChangeState(square.IdleState);
    }

    public override void Tick(EnemyBase e)
    {
        var square = (SquareEnemy)e;

        if (square.PlayerInDetectRadius() && !square.IsPlayerDead)
        {
            square.ChangeState(square.ChaseState);
            return;
        }

        if (!square.HasPatrolRoute && !square.EnsurePatrolRoute())
        {
            square.ChangeState(square.IdleState);
        }
    }

    public override void FixedTick(EnemyBase e)
    {
        var square = (SquareEnemy)e;

        if (!square.HasPatrolRoute)
        {
            e.StopMove();
            return;
        }

        if (_waiting)
        {
            e.StopMove();
            _waitTimer -= Time.fixedDeltaTime;
            if (_waitTimer <= 0f)
                _waiting = false;
            return;
        }

        if (square.DistanceToPatrolPoint() <= square.PatrolArriveDistance)
        {
            _waiting = true;
            _waitTimer = square.PatrolWaitDuration;
            square.AdvanceToNextPatrolPoint();
            e.StopMove();
            return;
        }

        square.MoveTowardPatrolPoint();
    }
}
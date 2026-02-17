using UnityEngine;
public sealed class SquareChaseState : EnemyStateBase
{
    public override void Enter(EnemyBase e)
    {
        var square = (SquareEnemy)e;
        square.SetChaseRevealForced(true, instant: true);
        square.ResetChaseSteering();
        square.ClearMoveIntent();
        square.InvalidateNavPath();
    }

    public override void Exit(EnemyBase e)
    {
        var square = (SquareEnemy)e;
        square.SetChaseRevealForced(false, instant: false);
    }

    public override void Tick(EnemyBase e)
    {
        var square = (SquareEnemy)e;

        if (square.IsPlayerDead || (square.PlayerBeyondLoseRadius() && !square.IsForcedAggroActive))
        {
            if (square.EnsurePatrolRoute())
                square.ChangeState(square.PatrolState);
            else
                square.ChangeState(square.IdleState);
        }
    }

    public override void FixedTick(EnemyBase e)
    {
        var square = (SquareEnemy)e;
        float speedFraction = square.EvaluateChaseSpeedFraction();
        Vector2 moveDir = square.GetSmoothedChaseDirection();
        square.ApplyChaseMove(moveDir, speedFraction);
    }
}

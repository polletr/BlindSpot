using UnityEngine;

public sealed class SquareChaseState : EnemyStateBase
{
    public override void Enter(EnemyBase e)
    {
        var square = (SquareEnemy)e;
        square.ResetChaseSteering();
    }

    public override void Tick(EnemyBase e)
    {
        var square = (SquareEnemy)e;

        if (square.IsPlayerDead || square.PlayerBeyondLoseRadius())
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

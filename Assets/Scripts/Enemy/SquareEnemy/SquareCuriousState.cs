public sealed class SquareCuriousState : EnemyStateBase
{
    public override void Enter(EnemyBase e)
    {
        var square = (SquareEnemy)e;
        square.ClearMoveIntent();
        square.InvalidateNavPath();
    }

    public override void Tick(EnemyBase e)
    {
        var square = (SquareEnemy)e;

        if (square.PlayerInDetectRadius() && !square.IsPlayerDead)
        {
            square.ChangeState(square.ChaseState);
            return;
        }

        if (!square.HasFlashlightStimulus)
        {
            if (square.EnsurePatrolRoute())
                square.ChangeState(square.PatrolState);
            else
                square.ChangeState(square.IdleState);
            return;
        }

        if (!square.IsAtCuriosityTarget())
            return;

        square.ClearFlashlightStimulus();

        if (square.EnsurePatrolRoute())
            square.ChangeState(square.PatrolState);
        else
            square.ChangeState(square.IdleState);
    }

    public override void FixedTick(EnemyBase e)
    {
        var square = (SquareEnemy)e;
        square.MoveTowardCuriosityTarget();
    }
}

public sealed class SquareIdleState : EnemyStateBase
{
    public override void Enter(EnemyBase e)
    {
        e.StopMove(80f);
    }

    public override void Tick(EnemyBase e)
    {
        var square = (SquareEnemy)e;

        if (square.PlayerInDetectRadius() && !square.IsPlayerDead)
        {
            square.ChangeState(square.ChaseState);
            return;
        }

        if (square.EnsurePatrolRoute())
            square.ChangeState(square.PatrolState);
    }

    public override void FixedTick(EnemyBase e)
    {
        e.StopMove();
    }
}
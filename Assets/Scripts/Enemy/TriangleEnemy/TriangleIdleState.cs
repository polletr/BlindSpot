public sealed class TriangleIdleState : EnemyStateBase
{
    public override void Tick(EnemyBase e)
    {
        var t = (TriangleEnemy)e;

        if (t.PlayerInDetectRadius())
            t.ChangeState(t.AlertState);
    }

    public override void FixedTick(EnemyBase e)
    {
        e.StopMove(40f);
    }
}

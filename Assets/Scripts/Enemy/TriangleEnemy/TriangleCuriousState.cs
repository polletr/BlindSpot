public sealed class TriangleCuriousState : EnemyStateBase
{
    public override void Enter(EnemyBase e)
    {
        var t = (TriangleEnemy)e;
        t.InvalidateNavPath();
    }

    public override void Tick(EnemyBase e)
    {
        var t = (TriangleEnemy)e;

        if (t.PlayerInDetectRadius() && !t.IsPlayerDead)
        {
            t.ChangeState(t.ChaseState);
            return;
        }

        if (!t.HasFlashlightStimulus)
        {
            t.ChangeState(t.IdleState);
            return;
        }

        if (!t.IsAtCuriosityTarget())
            return;

        t.ClearFlashlightStimulus();
        t.ChangeState(t.IdleState);
    }

    public override void FixedTick(EnemyBase e)
    {
        var t = (TriangleEnemy)e;
        t.MoveTowardCuriosityTarget();
    }
}

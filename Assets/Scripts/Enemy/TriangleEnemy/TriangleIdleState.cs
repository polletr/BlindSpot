public sealed class TriangleIdleState : EnemyStateBase
{
    public override void Tick(EnemyBase e)
    {
        var t = (TriangleEnemy)e;

        if (t.PlayerInDetectRadius())
        {
            t.ChangeState(t.ChaseState);
            return;
        }

        if (t.HasFlashlightStimulus && !t.IsPlayerDead)
            t.ChangeState(t.CuriousState);
    }

    public override void FixedTick(EnemyBase e)
    {
        e.StopMove(40f);
    }
}

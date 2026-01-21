public abstract class EnemyStateBase : IEnemyState
{
    public virtual void Enter(EnemyBase e) { }
    public virtual void Exit(EnemyBase e) { }
    public virtual void Tick(EnemyBase e) { }
    public virtual void FixedTick(EnemyBase e) { }
}


public interface IEnemyState
{
    void Enter(EnemyBase e);
    void Exit(EnemyBase e);
    void Tick(EnemyBase e);       // Update
    void FixedTick(EnemyBase e);  // FixedUpdate
}

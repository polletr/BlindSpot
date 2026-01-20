public abstract class PlayerStateBase : IPlayerState
{
    public virtual void Enter(PlayerController p) { }
    public virtual void Exit(PlayerController p) { }
    public virtual void Tick(PlayerController p) { }
    public virtual void FixedTick(PlayerController p) { }
}

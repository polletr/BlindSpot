public interface IPlayerState
{
    void Enter(PlayerController p);
    void Exit(PlayerController p);
    void Tick(PlayerController p);
    void FixedTick(PlayerController p);
}

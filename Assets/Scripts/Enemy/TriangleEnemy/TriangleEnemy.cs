using UnityEngine;

public class TriangleEnemy : EnemyBase
{

    // States
    public TriangleIdleState IdleState { get; private set; }
    public TriangleAlertState AlertState { get; private set; }
    public TriangleChaseState ChaseState { get; private set; }
    public TriangleRepositionState RepositionState { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        IdleState = new TriangleIdleState();
        AlertState = new TriangleAlertState();
        ChaseState = new TriangleChaseState();
        RepositionState = new TriangleRepositionState();

        ChangeState(IdleState);

    }
}

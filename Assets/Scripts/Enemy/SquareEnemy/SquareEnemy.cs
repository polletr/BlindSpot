using UnityEngine;
using Random = UnityEngine.Random;

public class SquareEnemy : EnemyBase
{
    [Header("Patrol Settings")]
    [SerializeField, Min(0.25f)] float patrolMinHalfExtent = 1.25f;
    [SerializeField, Min(0.25f)] float patrolMaxHalfExtent = 2.75f;
    [SerializeField, Min(0.05f)] float patrolPointClearanceRadius = 0.35f;
    [SerializeField, Min(0.05f)] float patrolPointReachDistance = 0.25f;
    [SerializeField, Min(0.05f)] float waitAtPointDuration = 1.5f;
    [SerializeField, Range(0.1f, 1.5f)] float patrolSpeedFraction = 0.7f;
    [SerializeField, Min(1)] int patrolGenerationAttempts = 14;
    [SerializeField] float roomBoundsInset = 0.6f;
    [SerializeField] LayerMask patrolBlockMask;

    [Header("Chase Steering")]
    [SerializeField, Range(0f, 0.5f)] float chaseDirectionSmoothTime = 0.14f;
    [SerializeField, Range(1f, 4f)] float chaseSlowDistanceMultiplier = 1.8f;
    [SerializeField, Range(0f, 1f)] float minChaseSpeedFraction = 0.35f;
    [SerializeField, Range(0f, 0.3f)] float chaseVelocitySmoothTime = 0.08f;

    public SquareIdleState IdleState { get; private set; }
    public SquarePatrolState PatrolState { get; private set; }
    public SquareChaseState ChaseState { get; private set; }

    readonly Vector2[] _patrolPoints = new Vector2[4];
    int _patrolPointCount;
    int _currentPatrolIndex;

    Vector2 _smoothedChaseDir;
    Vector2 _chaseDirVelocity;
    Vector2 _chaseVelocityRef;

    public bool HasPatrolRoute => _patrolPointCount == _patrolPoints.Length;
    public Vector2 CurrentPatrolTarget => HasPatrolRoute ? _patrolPoints[_currentPatrolIndex] : (Vector2)transform.position;
    public float PatrolWaitDuration => waitAtPointDuration;
    public float PatrolArriveDistance => patrolPointReachDistance;
    public float PatrolSpeedMultiplier => Mathf.Max(0.05f, patrolSpeedFraction);
    public bool IsPlayerDead => IsTargetPlayerDead;

    protected override void Awake()
    {
        base.Awake();
        _smoothedChaseDir = ForwardDir;
        IdleState = new SquareIdleState();
        PatrolState = new SquarePatrolState();
        ChaseState = new SquareChaseState();
        ChangeState(IdleState);
    }

    void Start()
    {
        EnsurePatrolRoute();
    }

    public bool EnsurePatrolRoute()
    {
        if (HasPatrolRoute)
            return true;

        return GeneratePatrolRoute();
    }

    public void ResetPatrolRoute()
    {
        _patrolPointCount = 0;
        _currentPatrolIndex = 0;
    }

    bool GeneratePatrolRoute()
    {
        float minExtent = Mathf.Max(0.25f, patrolMinHalfExtent);
        float maxExtent = Mathf.Max(minExtent, patrolMaxHalfExtent);

        Vector2 center = transform.position;
        RoomGenerator generator = RoomGenerator.Instance;
        Bounds bounds = generator != null ? generator.RoomBounds : new Bounds(new Vector3(center.x, center.y, 0f), new Vector3(maxExtent * 4f, maxExtent * 4f, 4f));
        bool hasBounds = bounds.size.sqrMagnitude > 0.0001f;

        Vector2[] tempPoints = new Vector2[_patrolPoints.Length];
        int attempts = Mathf.Max(1, patrolGenerationAttempts);
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            float halfX = Random.Range(minExtent, maxExtent);
            float halfY = Random.Range(minExtent, maxExtent);
            float angle = Random.Range(0f, 360f);
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

            tempPoints[0] = new Vector2(-halfX, -halfY);
            tempPoints[1] = new Vector2(halfX, -halfY);
            tempPoints[2] = new Vector2(halfX, halfY);
            tempPoints[3] = new Vector2(-halfX, halfY);

            bool valid = true;
            for (int i = 0; i < tempPoints.Length; i++)
            {
                Vector2 world = center + (Vector2)(rotation * tempPoints[i]);
                if (hasBounds && !PointInsideBounds(bounds, world))
                {
                    valid = false;
                    break;
                }

                if (IsPointBlocked(world))
                {
                    valid = false;
                    break;
                }

                tempPoints[i] = world;
            }

            if (valid)
            {
                for (int i = 0; i < _patrolPoints.Length; i++)
                    _patrolPoints[i] = tempPoints[i];

                _patrolPointCount = _patrolPoints.Length;
                _currentPatrolIndex = 0;
                return true;
            }
        }

        return false;
    }

    bool PointInsideBounds(Bounds bounds, Vector2 point)
    {
        Vector3 min = bounds.min + new Vector3(roomBoundsInset, roomBoundsInset, 0f);
        Vector3 max = bounds.max - new Vector3(roomBoundsInset, roomBoundsInset, 0f);
        if (min.x > max.x || min.y > max.y)
            return true;

        return point.x >= min.x && point.x <= max.x && point.y >= min.y && point.y <= max.y;
    }

    bool IsPointBlocked(Vector2 point)
    {
        if (patrolPointClearanceRadius <= 0f)
            return false;

        if (patrolBlockMask.value == 0)
            return false;

        return Physics2D.OverlapCircle(point, patrolPointClearanceRadius, patrolBlockMask) != null;
    }

    public void AdvanceToNextPatrolPoint()
    {
        if (!HasPatrolRoute)
            return;

        _currentPatrolIndex = (_currentPatrolIndex + 1) % _patrolPointCount;
    }

    public float DistanceToPatrolPoint()
    {
        return Vector2.Distance(transform.position, CurrentPatrolTarget);
    }

    public void MoveTowardPatrolPoint()
    {
        if (!HasPatrolRoute)
            return;

        Vector2 dir = CurrentPatrolTarget - (Vector2)transform.position;
        MoveInDirection(dir, PatrolSpeedMultiplier);
    }

    public void ResetChaseSteering()
    {
        _smoothedChaseDir = ForwardDir;
        _chaseDirVelocity = Vector2.zero;
        _chaseVelocityRef = Vector2.zero;
    }

    public Vector2 GetSmoothedChaseDirection()
    {
        Vector2 desired = DirToPlayer;
        if (desired.sqrMagnitude < 0.0001f)
        {
            if (_smoothedChaseDir.sqrMagnitude < 0.0001f)
                _smoothedChaseDir = transform.up;
            return _smoothedChaseDir.normalized;
        }

        if (chaseDirectionSmoothTime <= 0f)
        {
            _smoothedChaseDir = desired.normalized;
        }
        else
        {
            _smoothedChaseDir = Vector2.SmoothDamp(
                _smoothedChaseDir,
                desired,
                ref _chaseDirVelocity,
                chaseDirectionSmoothTime,
                Mathf.Infinity,
                Time.fixedDeltaTime);
            if (_smoothedChaseDir.sqrMagnitude < 0.0001f)
                _smoothedChaseDir = desired;
        }
        return _smoothedChaseDir.normalized;
    }

    public float EvaluateChaseSpeedFraction()
    {
        if (!HasPlayer)
            return 0f;

        float stop = Mathf.Max(0.01f, stopDistance);
        float dist = DistToPlayer;
        if (dist <= stop * 0.95f)
            return 0f;

        float slowRadius = Mathf.Max(stop + 0.05f, stop * Mathf.Max(1f, chaseSlowDistanceMultiplier));
        if (dist >= slowRadius)
            return 1f;

        float t = Mathf.InverseLerp(stop, slowRadius, dist);
        return Mathf.Lerp(minChaseSpeedFraction, 1f, t);
    }

    public void ApplyChaseMove(Vector2 dir, float speedFraction)
    {
        float clampedFraction = Mathf.Clamp01(speedFraction);
        Vector2 desiredVelocity = Vector2.zero;
        if (dir.sqrMagnitude >= 0.0001f && clampedFraction > 0f)
        {
            desiredVelocity = dir.normalized * (CurrentMoveSpeed * chaseSpeedMultiplier * clampedFraction);
        }
        SmoothChaseVelocity(desiredVelocity);
    }

    void SmoothChaseVelocity(Vector2 desiredVelocity)
    {
        if (chaseVelocitySmoothTime <= 0f)
        {
            RB.linearVelocity = desiredVelocity;
            _chaseVelocityRef = Vector2.zero;
            return;
        }

        RB.linearVelocity = Vector2.SmoothDamp(
            RB.linearVelocity,
            desiredVelocity,
            ref _chaseVelocityRef,
            chaseVelocitySmoothTime,
            Mathf.Infinity,
            Time.fixedDeltaTime);
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        if (!HasPatrolRoute)
            return;

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.75f);
        for (int i = 0; i < _patrolPointCount; i++)
        {
            Vector2 point = _patrolPoints[i];
            Gizmos.DrawWireSphere(point, 0.15f);
            Vector2 next = _patrolPoints[(i + 1) % _patrolPointCount];
            Gizmos.DrawLine(point, next);
        }
    }
}

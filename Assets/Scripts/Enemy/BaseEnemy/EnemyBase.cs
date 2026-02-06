using System;
using System.Collections.Generic;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody2D))]
public abstract class EnemyBase : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] protected Transform player; // assign or auto-find
    public Rigidbody2D RB { get; private set; }
    protected PlayerController TargetPlayerController { get; private set; }

    UpgradeManager upgradeManager;
    private EnemyVisibility _enemyVisibility;
    protected UpgradeManager UpgradeMgr
    {
        get
        {
            if (upgradeManager == null)
                upgradeManager = UpgradeManager.Instance;
            return upgradeManager;
        }
    }

    protected float EnemySpeedMultiplier => UpgradeMgr != null ? UpgradeMgr.EnemySpeedMultiplier : 1f;
    protected float EnemyDetectionRadiusMultiplier => UpgradeMgr != null ? UpgradeMgr.EnemyDetectionRadiusMultiplier : 1f;
    protected float EnemyLoseSightRadiusMultiplier => UpgradeMgr != null ? UpgradeMgr.EnemyLoseSightRadiusMultiplier : 1f;
    protected float CurrentMoveSpeed => moveSpeed * EnemySpeedMultiplier;
    protected float CurrentDetectRadius => detectRadius * EnemyDetectionRadiusMultiplier;
    protected float CurrentLoseRadius => loseRadius * EnemyLoseSightRadiusMultiplier;

    [Header("Movement")]
    public float moveSpeed = 4f;
    public float acceleration = 25f;
    public float chaseSpeedMultiplier = 1.35f;
    public float alertTime = 0.15f;
    public float chaseCommitTime = 0.55f;
    public float repositionTime = 0.35f;
    public float stopDistance = 0.65f;
 
    [Header("Navigation")]
    [SerializeField, Min(0.05f)] float navMeshRepathInterval = 0.25f;
    [SerializeField, Min(0.01f)] float navMeshRepathDistance = 0.4f;
    [SerializeField, Min(0.01f)] float navMeshCornerTolerance = 0.1f;
    [SerializeField, Min(0.01f)] float navMeshSampleDistance = 0.6f;
    [SerializeField, Min(1f)] float navMeshSampleFallbackMultiplier = 3f;
    [SerializeField] int navMeshAreaMask = NavMesh.AllAreas;
 



    [Header("Tips")]
    [Tooltip("Assign all tip transforms (child objects).")]
    public List<Transform> tips = new List<Transform>();

    [Tooltip("How often we are allowed to switch the active tip (seconds).")]
    public float tipSwitchCooldown = 0.25f;

    [Tooltip("New tip must be at least this much closer (world units) to switch.")]
    public float tipSwitchBetterBy = 0.25f;

    [Tooltip("Rotation speed (deg/sec).")]
    public float rotateSpeed = 720f;

    protected float _nextTipSwitchTime;
    public Transform ActiveTip { get; protected set; }


    [Header("Perception")]
    public float detectRadius = 6f;
    public float loseRadius = 9f;

    [Header("Debug")]
    public bool drawGizmos = true;

    [Header("Combat")]
    [SerializeField, Min(1)] private int maxHealth = 3;
    [SerializeField, Tooltip("Delay before destroying corpses. Set negative to keep them around.")] private float deathCleanupDelay = 3f;
    [SerializeField] private Animator deathAnimatorOverride;
    [SerializeField] private string deathTriggerName = "Die";

    public int CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    public event Action<EnemyBase> EnemyDied;

    private Collider2D[] _cachedColliders;
    NavMeshPath _navMeshPath;
    Vector2 _navDestination;
    int _navCornerIndex;
    float _nextNavPathUpdateTime;
    bool _navPathValid;
    bool _hasNavDestination;
 

    protected IEnemyState currentState;
    protected bool IsTargetPlayerDead => TargetPlayerController != null && TargetPlayerController.IsDead;

    protected virtual void Awake()
    {
        RB = GetComponent<Rigidbody2D>();
        RB.gravityScale = 0f;
        RB.freezeRotation = true;

        if (player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }
        CachePlayerControllerReference();
        if (TryGetComponent(out EnemyVisibility enemyVis))
        {
            _enemyVisibility = enemyVis;
            _enemyVisibility.player = player;
        }

        if (tips == null || tips.Count == 0)
        {
            foreach (EnemyTipKill t in GetComponentsInChildren<EnemyTipKill>())
            {
                tips.Add(t.transform);
            }    

        }

        PickActiveTip(force: true);

        _cachedColliders = GetComponentsInChildren<Collider2D>(includeInactive: true);
        CurrentHealth = Mathf.Max(1, maxHealth);
        IsDead = false;
    }

    protected virtual void OnEnable()
    {
        ResetDeathState();
        SubscribeToPlayerSpawnEvents();
    }

    protected virtual void OnDisable()
    {
        UnsubscribeFromPlayerSpawnEvents();
    }

    protected virtual void ResetDeathState()
    {
        IsDead = false;
        CurrentHealth = Mathf.Clamp(CurrentHealth <= 0 ? maxHealth : CurrentHealth, 1, maxHealth);
        EnableAllColliders(true);
        InvalidateNavPath();
        if (RB != null)
        {
            RB.linearVelocity = Vector2.zero;
       }
    }

    protected virtual void Update()
    {
        if (IsDead) return;

        currentState?.Tick(this);

        if (!HasPlayer) return;

        UpdateActiveTip();

        if (ShouldRotateTowardPlayer)
            RotateSoActiveTipFacesPlayer();

    }

    protected virtual bool ShouldRotateTowardPlayer => true;

    protected virtual void FixedUpdate()
    {
        if (IsDead)
        {
            if (RB != null)
                RB.linearVelocity = Vector2.zero;
            return;
        }

        if (IsTargetPlayerDead)
        {
            StopMove();
            return;
        }

        currentState?.FixedTick(this);
    }

    public void ChangeState(IEnemyState next)
    {
        if (next == null || next == currentState) return;
        currentState?.Exit(this);
        currentState = next;
        currentState.Enter(this);
    }

    public bool HasPlayer => player != null;

    public float DistToPlayer
    {
        get
        {
            if (!HasPlayer) return float.PositiveInfinity;
            return Vector2.Distance(transform.position, player.position);
        }
    }

    public Vector2 DirToPlayer
    {
        get
        {
            if (!HasPlayer) return Vector2.right;
            Vector2 d = (Vector2)player.position - (Vector2)transform.position;
            if (d.sqrMagnitude < 0.0001f) return Vector2.right;
            return d.normalized;
        }
    }

    public bool PlayerInDetectRadius() => HasPlayer && DistToPlayer <= CurrentDetectRadius;
    public bool PlayerBeyondLoseRadius() => !HasPlayer || DistToPlayer >= CurrentLoseRadius;

    // Basic “accelerated velocity” steering (feels consistent with your player)
    public void MoveInDirection(Vector2 dir, float speedMultiplier = 1f)
    {
        if (IsDead) return;
        if (dir.sqrMagnitude < 0.0001f) return;

        Vector2 targetVel = dir.normalized * (CurrentMoveSpeed * speedMultiplier);
        Vector2 newVel = Vector2.MoveTowards(RB.linearVelocity, targetVel, acceleration * Time.fixedDeltaTime);
        RB.linearVelocity = newVel;
    }

    public void StopMove(float decel = 40f)
    {
        if (IsDead) return;
        RB.linearVelocity = Vector2.MoveTowards(RB.linearVelocity, Vector2.zero, decel * Time.fixedDeltaTime);
    }


    public virtual void TakeDamage(int amount)
    {
        if (IsDead) return;

        amount = Mathf.Max(1, amount);
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);

        if (CurrentHealth <= 0)
            HandleDeath();
    }

    protected virtual void HandleDeath()
    {
        if (IsDead) return;

        IsDead = true;
        CurrentHealth = 0;

        currentState?.Exit(this);
        currentState = null;

        if (RB != null)
        {
            RB.linearVelocity = Vector2.zero;
            RB.isKinematic = true;
        }

        EnableAllColliders(false);

        Animator animator = ResolveDeathAnimator();
        if (animator != null && !string.IsNullOrEmpty(deathTriggerName))
            animator.SetTrigger(deathTriggerName);

        EnemyDied?.Invoke(this);

        if (deathCleanupDelay >= 0f)
            Destroy(gameObject, deathCleanupDelay);
    }

    private Animator ResolveDeathAnimator()
    {
        if (deathAnimatorOverride != null)
            return deathAnimatorOverride;
        return GetComponentInChildren<Animator>();
    }

    private void EnableAllColliders(bool enabled)
    {
        if (_cachedColliders == null || _cachedColliders.Length == 0)
            _cachedColliders = GetComponentsInChildren<Collider2D>(includeInactive: true);

        foreach (var col in _cachedColliders)
        {
            if (col == null) continue;
            col.enabled = enabled;
        }
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        float detect = Application.isPlaying ? CurrentDetectRadius : detectRadius;
        float lose = Application.isPlaying ? CurrentLoseRadius : loseRadius;
        Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, detect);
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, lose);
    }

    // -----------------------------
    // Tip logic
    // -----------------------------

    protected virtual void UpdateActiveTip()
    {
        if (!HasPlayer)
            return;

        PickActiveTip(force: false);
    }

    public void PickActiveTip(bool force)
    {
        if (!HasPlayer || tips.Count == 0) return;

        Vector2 p = player.position;

        Transform best = ActiveTip;
        float bestDist = ActiveTip != null
            ? Vector2.Distance(ActiveTip.position, p)
            : float.PositiveInfinity;

        foreach (var t in tips)
        {
            if (t == null || t == transform) continue;
            if (!t.name.ToLower().Contains("tip")) continue;

            float d = Vector2.Distance(t.position, p);
            if (d < bestDist)
            {
                best = t;
                bestDist = d;
            }
        }

        if (best == null) return;

        if (force)
        {
            ActiveTip = best;
            _nextTipSwitchTime = Time.time + tipSwitchCooldown;
            return;
        }

        if (Time.time < _nextTipSwitchTime) return;
        if (ActiveTip == null) { ActiveTip = best; return; }

        float currentDist = Vector2.Distance(ActiveTip.position, p);
        if (best != ActiveTip && bestDist <= currentDist - tipSwitchBetterBy)
        {
            ActiveTip = best;
            _nextTipSwitchTime = Time.time + tipSwitchCooldown;
        }
    }

    void CachePlayerControllerReference()
    {
        if (player != null)
            TargetPlayerController = player.GetComponent<PlayerController>();
        else
            TargetPlayerController = null;
    }

    void AssignPlayerTarget(PlayerController controller)
    {
        player = controller != null ? controller.transform : null;
        CachePlayerControllerReference();
        if (_enemyVisibility != null)
            _enemyVisibility.player = player;
    }

    void SubscribeToPlayerSpawnEvents()
    {
        if (!GameFlowManager.HasInstance)
            return;

        GameFlowManager.Instance.PlayerSpawned += HandleGlobalPlayerSpawned;

        var current = GameFlowManager.Instance.CurrentPlayer;
        if (current == null)
        {
            var session = GameFlowManager.Instance.ActiveRunSession;
            if (session != null)
                current = session.ActivePlayer;
        }

        if (current != null)
            AssignPlayerTarget(current);
    }

    void UnsubscribeFromPlayerSpawnEvents()
    {
        if (!GameFlowManager.HasInstance)
            return;

        GameFlowManager.Instance.PlayerSpawned -= HandleGlobalPlayerSpawned;
    }

    void HandleGlobalPlayerSpawned(PlayerController controller)
    {
        AssignPlayerTarget(controller);
    }

    public void FreezeTipSelection(float duration)
    {
        _nextTipSwitchTime = Time.time + duration;
    }

    // -----------------------------
    // Navigation
    // -----------------------------

    public void InvalidateNavPath()
    {
        _navPathValid = false;
        _hasNavDestination = false;
        _navCornerIndex = 0;
        _nextNavPathUpdateTime = 0f;
    }

    protected Vector2 GetNavMeshDirection(Vector2 destination, bool forceRepath = false)
    {
        Vector2 fallback = destination - (Vector2)transform.position;
        if (!EnsureNavPath(destination, forceRepath))
            return fallback;

        Vector2 currentPosition = transform.position;
        if (!TryGetCurrentCorner(currentPosition, out Vector2 corner))
            return fallback;

        Vector2 toCorner = corner - currentPosition;
        if (toCorner.sqrMagnitude < 0.0001f)
            return fallback;

        return toCorner;
    }

    bool EnsureNavPath(Vector2 destination, bool forceRepath)
    {
        float repathDistance = Mathf.Max(0.01f, navMeshRepathDistance);
        bool needsRepath = forceRepath
            || !_navPathValid
            || !_hasNavDestination
            || (destination - _navDestination).sqrMagnitude >= repathDistance * repathDistance
            || Time.time >= _nextNavPathUpdateTime;

        if (!needsRepath)
            return true;

        if (_navMeshPath == null)
            _navMeshPath = new NavMeshPath();

        Vector3 from = transform.position;
        Vector3 to = new Vector3(destination.x, destination.y, from.z);

        if (!TrySampleNavPosition(from, out Vector3 sampledFrom) || !TrySampleNavPosition(to, out Vector3 sampledTo))
        {
            _navPathValid = false;
            _hasNavDestination = false;
            return false;
        }

        if (NavMesh.CalculatePath(sampledFrom, sampledTo, navMeshAreaMask, _navMeshPath)
            && _navMeshPath.status == NavMeshPathStatus.PathComplete
            && _navMeshPath.corners.Length > 0)
        {
            _navPathValid = true;
            _hasNavDestination = true;
            _navDestination = destination;
            _navCornerIndex = 0;
        }
        else
        {
            _navPathValid = false;
            _hasNavDestination = false;
        }

        float interval = Mathf.Max(0.01f, navMeshRepathInterval);
        _nextNavPathUpdateTime = Time.time + interval;
        return _navPathValid;
    }

    bool TryGetCurrentCorner(Vector2 currentPosition, out Vector2 corner)
    {
        corner = Vector2.zero;
        if (!_navPathValid || _navMeshPath == null || _navMeshPath.corners == null || _navMeshPath.corners.Length == 0)
            return false;

        float tolerance = Mathf.Max(0.005f, navMeshCornerTolerance);
        while (_navCornerIndex < _navMeshPath.corners.Length)
        {
            Vector3 rawCorner = _navMeshPath.corners[_navCornerIndex];
            corner = new Vector2(rawCorner.x, rawCorner.y);
            float dist = Vector2.Distance(currentPosition, corner);
            if (dist <= tolerance)
            {
                if (_navCornerIndex == _navMeshPath.corners.Length - 1)
                    return false;

                _navCornerIndex++;
                continue;
            }

            return true;
        }

        return false;
    }

    bool TrySampleNavPosition(Vector3 origin, out Vector3 sampled)
    {
        float maxDistance = Mathf.Max(0.01f, navMeshSampleDistance);
        if (NavMesh.SamplePosition(origin, out NavMeshHit hit, maxDistance, navMeshAreaMask))
        {
            sampled = hit.position;
            return true;
        }

        float fallbackDistance = Mathf.Max(maxDistance * navMeshSampleFallbackMultiplier, maxDistance);
        if (fallbackDistance > maxDistance
            && NavMesh.SamplePosition(origin, out hit, fallbackDistance, navMeshAreaMask))
        {
            sampled = hit.position;
            return true;
        }

        sampled = origin;
        return false;
    }

    // -----------------------------
    // Orientation
    // -----------------------------

    protected virtual bool TryGetDesiredFacingDirection(out Vector2 desiredDir)
    {
        desiredDir = Vector2.zero;
        if (ActiveTip == null || !HasPlayer)
            return false;

        Vector2 toPlayer = (Vector2)player.position - (Vector2)ActiveTip.position;
        if (toPlayer.sqrMagnitude < 0.0001f)
            return false;

        desiredDir = toPlayer.normalized;
        return true;
    }

    protected virtual Vector2 GetTipForward(Transform tip)
    {
        if (tip == null)
            return transform.up;
        return tip.up;
    }

    void RotateSoActiveTipFacesPlayer()
    {
        if (ActiveTip == null) return;

        if (!TryGetDesiredFacingDirection(out Vector2 desiredDir))
            return;

        // TIP FORWARD IS UP
        Vector2 tipForward = GetTipForward(ActiveTip);

        float angle = Vector2.SignedAngle(tipForward, desiredDir);
        float step = rotateSpeed * Time.deltaTime;

        transform.Rotate(0f, 0f, Mathf.Clamp(angle, -step, step));
    }

    // -----------------------------
    // Movement helpers
    // -----------------------------

    public Vector2 ForwardDir
    {
        get
        {
            if (ActiveTip == null) return transform.up;
            return GetTipForward(ActiveTip).normalized;
        }
    }

    public void MoveForward(float speedMultiplier)
    {
        MoveInDirection(ForwardDir, speedMultiplier);
    }
}







using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public abstract class EnemyBase : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] protected Transform player;
    public NavMeshAgent Agent { get; private set; }
    protected PlayerController TargetPlayerController { get; private set; }

    UpgradeManager upgradeManager;
    EnemyVisibility _enemyVisibility;
    Rigidbody2D _rb2D;

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
    [SerializeField, Min(0.05f)] float navMeshRepathInterval = 0.2f;
    [SerializeField, Min(0.05f)] float navMeshRepathDistance = 0.35f;
    [SerializeField, Min(0.01f)] float navMeshCornerTolerance = 0.1f;
    [SerializeField, Min(0.1f)] float navMeshSampleDistance = 1f;
    [SerializeField, Min(0.1f)] float navMeshSnapDistance = 2f;
    [SerializeField, Min(0.05f)] float navMeshRecoverInterval = 0.3f;
    [SerializeField] bool updateNavAgentEveryFrame = true;

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
    [SerializeField, Min(0f)] float forcedAggroDurationOnHit = 1.25f;

    [Header("Debug")]
    public bool drawGizmos = true;

    [Header("Combat")]
    [SerializeField, Min(1)] int maxHealth = 3;
    [SerializeField, Min(0f)] float hitRevealDuration = 0.9f;
    [SerializeField, Tooltip("Delay before destroying corpses. Set negative to keep them around.")] float deathCleanupDelay = 3f;
    [SerializeField] Animator deathAnimatorOverride;
    [SerializeField] string deathTriggerName = "Die";

    [Header("Death VFX")]
    [SerializeField] ParticleSystem deathVfx;
    [SerializeField] bool disableSpritesOnDeath = true;
    [SerializeField] List<SpriteRenderer> spritesToDisable = new();

    public int CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    public event Action<EnemyBase> EnemyDied;

    Collider2D[] _cachedColliders;
    Coroutine _deathCleanupRoutine;
    readonly List<Vector3> _navPathCorners = new();
    Vector2 _navDestination;
    int _navCornerIndex;
    float _nextNavPathUpdateTime;
    bool _navPathValid;
    bool _hasNavDestination;
    float _forcedAggroUntil;
    float _nextNavRecoverTime;
    bool _hasFlashlightStimulus;
    Vector2 _flashlightStimulusPosition;
    float _lastFlashlightStimulusTime;

    protected IEnemyState currentState;
    protected bool IsTargetPlayerDead => TargetPlayerController != null && TargetPlayerController.IsDead;

    protected virtual void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        ConfigureNavAgent();
        TrySnapAgentToNavMesh(force: true);

        if (TryGetComponent(out Rigidbody2D rb2D))
        {
            _rb2D = rb2D;
            _rb2D.linearVelocity = Vector2.zero;
            _rb2D.angularVelocity = 0f;
            _rb2D.bodyType = RigidbodyType2D.Kinematic;
            _rb2D.freezeRotation = true;
        }

        if (player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
                player = pc.transform;
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
                tips.Add(t.transform);
        }

        PickActiveTip(force: true);

        _cachedColliders = GetComponentsInChildren<Collider2D>(includeInactive: true);
        CurrentHealth = Mathf.Max(1, maxHealth);
        IsDead = false;

        if (disableSpritesOnDeath)
            SetSpritesVisible(true);
    }

    void OnValidate()
    {
        if (Agent == null)
            Agent = GetComponent<NavMeshAgent>();

        ConfigureNavAgent();
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

        if (disableSpritesOnDeath)
            SetSpritesVisible(true);

        CurrentHealth = Mathf.Clamp(CurrentHealth <= 0 ? maxHealth : CurrentHealth, 1, maxHealth);
        ClearFlashlightStimulus();

        EnableAllColliders(true);
        InvalidateNavPath();

        if (_rb2D != null)
        {
            _rb2D.linearVelocity = Vector2.zero;
            _rb2D.angularVelocity = 0f;
        }

        TrySnapAgentToNavMesh(force: true);
        if (CanUseNavAgent())
        {
            Agent.Warp(transform.position);
            Agent.isStopped = false;
        }
    }

    protected virtual void Update()
    {
        if (IsDead)
            return;

        if (!CanUseNavAgent() && Time.time >= _nextNavRecoverTime)
        {
            TrySnapAgentToNavMesh(force: true);
            _nextNavRecoverTime = Time.time + Mathf.Max(0.05f, navMeshRecoverInterval);
        }

        if (updateNavAgentEveryFrame)
            SyncNavAgentSettings();

        currentState?.Tick(this);

        if (!HasPlayer)
            return;

        UpdateActiveTip();

        if (ShouldRotateTowardPlayer)
            RotateSoActiveTipFacesPlayer();
    }

    protected virtual bool ShouldRotateTowardPlayer => true;

    protected virtual void FixedUpdate()
    {
        if (IsDead)
        {
            StopMove();
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
        if (next == null || next == currentState)
            return;

        currentState?.Exit(this);
        currentState = next;
        currentState.Enter(this);
    }

    public bool HasPlayer => player != null;

    public float DistToPlayer
    {
        get
        {
            if (!HasPlayer)
                return float.PositiveInfinity;
            return Vector2.Distance(transform.position, player.position);
        }
    }

    public Vector2 DirToPlayer
    {
        get
        {
            if (!HasPlayer)
                return Vector2.right;

            Vector2 d = (Vector2)player.position - (Vector2)transform.position;
            if (d.sqrMagnitude < 0.0001f)
                return Vector2.right;

            return d.normalized;
        }
    }

    public bool PlayerInDetectRadius() => HasPlayer && DistToPlayer <= CurrentDetectRadius;
    public bool PlayerBeyondLoseRadius() => !HasPlayer || DistToPlayer >= CurrentLoseRadius;
    public bool IsForcedAggroActive => Time.time < _forcedAggroUntil;
    public bool HasFlashlightStimulus => _hasFlashlightStimulus;
    public Vector2 FlashlightStimulusPosition => _flashlightStimulusPosition;
    public float LastFlashlightStimulusTime => _lastFlashlightStimulusTime;

    public bool CanUseNavAgent() => Agent != null && Agent.enabled && Agent.isOnNavMesh;

    public void NotifyFlashlightTouch(Vector2 sourcePosition)
    {
        if (IsDead)
            return;

        _flashlightStimulusPosition = sourcePosition;
        _lastFlashlightStimulusTime = Time.time;
        _hasFlashlightStimulus = true;
    }

    public void ClearFlashlightStimulus()
    {
        _hasFlashlightStimulus = false;
    }

    public bool ReachedPosition(Vector2 destination, float extraTolerance = 0f)
    {
        float tolerance = Mathf.Max(0.01f, stopDistance + Mathf.Max(0f, extraTolerance));
        return Vector2.Distance(transform.position, destination) <= tolerance;
    }

    public void MoveToPosition(Vector2 destination, float speedMultiplier = 1f, bool forceRepath = false)
    {
        if (IsDead)
            return;

        if (!TrySampleNavMeshPosition(destination, out Vector3 sampledDestination))
        {
            StopMove();
            return;
        }

        SetNavDestination(sampledDestination, speedMultiplier, forceRepath);
    }

    public void MoveToPlayer(float speedMultiplier = 1f, bool forceRepath = false)
    {
        if (!HasPlayer)
        {
            StopMove();
            return;
        }

        MoveToPosition(player.position, speedMultiplier, forceRepath);
    }

    public void MoveInDirection(Vector2 dir, float speedMultiplier = 1f)
    {
        if (dir.sqrMagnitude < 0.0001f)
        {
            StopMove();
            return;
        }

        Vector2 destination = (Vector2)transform.position + dir.normalized * Mathf.Max(1f, CurrentMoveSpeed * navMeshRepathInterval);
        MoveToPosition(destination, speedMultiplier, forceRepath: true);
    }

    public void StopMove(float decel = 0f)
    {
        if (!CanUseNavAgent())
        {
            if (_rb2D != null)
                _rb2D.linearVelocity = Vector2.zero;
            return;
        }

        Agent.isStopped = true;
        if (Agent.hasPath)
            Agent.ResetPath();
        Agent.velocity = Vector3.zero;
    }

    public virtual void TakeDamage(int amount)
    {
        if (IsDead)
            return;

        amount = Mathf.Max(1, amount);
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);

        HandleRevealAndAggroOnHit();

        if (CurrentHealth <= 0)
            HandleDeath();
        else
            OnDamaged();
    }

    protected virtual void OnDamaged() { }
    protected virtual void AggroOnHit() { }

    void HandleRevealAndAggroOnHit()
    {
        if (_enemyVisibility != null && !_enemyVisibility.IsVisible)
            _enemyVisibility.ForceReveal(hitRevealDuration, instant: true);

        TriggerForcedAggro();

        if (!IsTargetPlayerDead)
            AggroOnHit();
    }

    void TriggerForcedAggro()
    {
        float duration = Mathf.Max(0f, forcedAggroDurationOnHit);
        if (duration <= 0f)
            return;

        _forcedAggroUntil = Mathf.Max(_forcedAggroUntil, Time.time + duration);
    }

    public void SetChaseRevealForced(bool forced, bool instant = true)
    {
        if (_enemyVisibility == null)
            return;

        _enemyVisibility.SetForcedVisiblePersistent(forced, instant);
    }

    protected virtual void HandleDeath()
    {
        if (IsDead)
            return;

        IsDead = true;
        CurrentHealth = 0;
        ClearFlashlightStimulus();

        currentState?.Exit(this);
        currentState = null;

        StopMove();

        EnableAllColliders(false);

        Animator animator = ResolveDeathAnimator();
        if (animator != null && !string.IsNullOrEmpty(deathTriggerName))
            animator.SetTrigger(deathTriggerName);

        EnemyDied?.Invoke(this);

        if (disableSpritesOnDeath)
            SetSpritesVisible(false);

        if (deathVfx != null)
        {
            if (_deathCleanupRoutine != null)
                StopCoroutine(_deathCleanupRoutine);
            _deathCleanupRoutine = StartCoroutine(DestroyAfterDeathVfx());
        }
        else if (deathCleanupDelay >= 0f)
        {
            Destroy(gameObject, deathCleanupDelay);
        }
    }

    Animator ResolveDeathAnimator()
    {
        if (deathAnimatorOverride != null)
            return deathAnimatorOverride;
        return GetComponentInChildren<Animator>();
    }

    void EnableAllColliders(bool enabled)
    {
        if (_cachedColliders == null || _cachedColliders.Length == 0)
            _cachedColliders = GetComponentsInChildren<Collider2D>(includeInactive: true);

        foreach (var col in _cachedColliders)
        {
            if (col == null)
                continue;
            col.enabled = enabled;
        }
    }

    void CacheSpriteRenderers()
    {
        if (spritesToDisable == null)
            spritesToDisable = new List<SpriteRenderer>();
        if (spritesToDisable.Count == 0)
            spritesToDisable.AddRange(GetComponentsInChildren<SpriteRenderer>(includeInactive: true));
    }

    void SetSpritesVisible(bool visible)
    {
        if (spritesToDisable == null || spritesToDisable.Count == 0)
            CacheSpriteRenderers();

        foreach (var sr in spritesToDisable)
        {
            if (sr == null)
                continue;
            sr.enabled = visible;
        }
    }

    IEnumerator DestroyAfterDeathVfx()
    {
        if (deathVfx == null)
            yield break;

        if (!deathVfx.gameObject.activeInHierarchy)
            deathVfx.gameObject.SetActive(true);

        deathVfx.Play(true);

        while (deathVfx != null && deathVfx.IsAlive(true))
            yield return null;

        Destroy(gameObject);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        float detect = Application.isPlaying ? CurrentDetectRadius : detectRadius;
        float lose = Application.isPlaying ? CurrentLoseRadius : loseRadius;

        Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, detect);

        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, lose);
    }

    protected virtual void UpdateActiveTip()
    {
        if (!HasPlayer)
            return;

        PickActiveTip(force: false);
    }

    public void PickActiveTip(bool force)
    {
        if (!HasPlayer || tips.Count == 0)
            return;

        Vector2 p = player.position;
        Transform best = ActiveTip;
        float bestDist = ActiveTip != null
            ? Vector2.Distance(ActiveTip.position, p)
            : float.PositiveInfinity;

        foreach (var t in tips)
        {
            if (t == null || t == transform)
                continue;
            if (!t.name.ToLower().Contains("tip"))
                continue;

            float d = Vector2.Distance(t.position, p);
            if (d < bestDist)
            {
                best = t;
                bestDist = d;
            }
        }

        if (best == null)
            return;

        if (force)
        {
            ActiveTip = best;
            _nextTipSwitchTime = Time.time + tipSwitchCooldown;
            return;
        }

        if (Time.time < _nextTipSwitchTime)
            return;

        if (ActiveTip == null)
        {
            ActiveTip = best;
            return;
        }

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

        InvalidateNavPath();
    }

    public void SetPlayerTarget(PlayerController controller)
    {
        AssignPlayerTarget(controller);
    }

    void SubscribeToPlayerSpawnEvents()
    {
        if (!GameFlowManager.HasInstance)
            return;

        var flow = GameFlowManager.Instance;
        if (flow == null)
            return;

        flow.PlayerSpawned += HandleGlobalPlayerSpawned;

        var current = flow.CurrentPlayer;
        if (current == null)
        {
            var session = flow.ActiveRunSession;
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

        var flow = GameFlowManager.Instance;
        if (flow == null)
            return;

        flow.PlayerSpawned -= HandleGlobalPlayerSpawned;
    }

    void HandleGlobalPlayerSpawned(PlayerController controller)
    {
        AssignPlayerTarget(controller);
    }

    public void FreezeTipSelection(float duration)
    {
        _nextTipSwitchTime = Time.time + duration;
    }

    public void InvalidateNavPath()
    {
        _navPathValid = false;
        _hasNavDestination = false;
        _navCornerIndex = 0;
        _nextNavPathUpdateTime = 0f;
        _navPathCorners.Clear();
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

        return toCorner.normalized;
    }

    protected bool TrySampleNavMeshPosition(Vector2 desiredPosition, out Vector3 sampledPosition)
    {
        sampledPosition = new Vector3(desiredPosition.x, desiredPosition.y, transform.position.z);

        int areaMask = CanUseNavAgent() ? Agent.areaMask : NavMesh.AllAreas;
        float sampleDistance = Mathf.Max(0.1f, navMeshSampleDistance);

        if (NavMesh.SamplePosition(sampledPosition, out NavMeshHit hit, sampleDistance, areaMask))
        {
            sampledPosition = new Vector3(hit.position.x, hit.position.y, transform.position.z);
            return true;
        }

        return false;
    }

    public bool CanReachPosition(Vector2 destination)
    {
        if (!TrySampleNavMeshPosition(destination, out Vector3 sampledDestination))
            return false;

        return HasCompletePath(transform.position, sampledDestination);
    }

    protected bool HasCompletePath(Vector2 from, Vector2 to)
    {
        if (!TrySampleNavMeshPosition(from, out Vector3 sampledFrom))
            return false;
        if (!TrySampleNavMeshPosition(to, out Vector3 sampledTo))
            return false;

        return HasCompletePath(sampledFrom, sampledTo);
    }

    bool HasCompletePath(Vector3 from, Vector3 to)
    {
        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path))
            return false;

        return path.status == NavMeshPathStatus.PathComplete && path.corners != null && path.corners.Length >= 2;
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

        _navPathValid = TryBuildNavMeshPath(destination, _navPathCorners);
        _hasNavDestination = _navPathValid;
        _navDestination = destination;
        _navCornerIndex = 0;
        _nextNavPathUpdateTime = Time.time + Mathf.Max(0.01f, navMeshRepathInterval);

        if (!_navPathValid)
            _navPathCorners.Clear();

        return _navPathValid;
    }

    bool TryBuildNavMeshPath(Vector2 destination, List<Vector3> outCorners)
    {
        outCorners.Clear();

        if (!TrySampleNavMeshPosition(transform.position, out Vector3 sampledStart))
            return false;
        if (!TrySampleNavMeshPosition(destination, out Vector3 sampledDestination))
            return false;

        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(sampledStart, sampledDestination, NavMesh.AllAreas, path))
            return false;

        if (path.status != NavMeshPathStatus.PathComplete || path.corners == null || path.corners.Length < 2)
            return false;

        outCorners.AddRange(path.corners);
        return outCorners.Count > 1;
    }

    bool TryGetCurrentCorner(Vector2 currentPosition, out Vector2 corner)
    {
        corner = Vector2.zero;
        if (!_navPathValid || _navPathCorners.Count <= 1)
            return false;

        float tolerance = Mathf.Max(0.005f, navMeshCornerTolerance);
        while (_navCornerIndex < _navPathCorners.Count - 1)
        {
            Vector2 candidate = _navPathCorners[_navCornerIndex + 1];
            float dist = Vector2.Distance(currentPosition, candidate);
            if (dist <= tolerance)
            {
                _navCornerIndex++;
                continue;
            }

            corner = candidate;
            return true;
        }

        return false;
    }

    void SetNavDestination(Vector3 sampledDestination, float speedMultiplier, bool forceRepath)
    {
        if (!CanUseNavAgent() && !TrySnapAgentToNavMesh(force: true))
            return;

        SyncNavAgentSettings();

        float speed = Mathf.Max(0f, CurrentMoveSpeed * speedMultiplier);
        Agent.speed = speed;
        Agent.isStopped = speed <= 0.0001f;

        if (Agent.isStopped)
            return;

        float repathDistance = Mathf.Max(0.01f, navMeshRepathDistance);
        bool shouldSetDestination = forceRepath
            || !_hasNavDestination
            || ((Vector2)sampledDestination - _navDestination).sqrMagnitude >= repathDistance * repathDistance
            || Time.time >= _nextNavPathUpdateTime;

        if (!shouldSetDestination)
            return;

        if (Agent.SetDestination(sampledDestination))
        {
            _hasNavDestination = true;
            _navDestination = sampledDestination;
            _nextNavPathUpdateTime = Time.time + Mathf.Max(0.01f, navMeshRepathInterval);
        }
    }

    bool TrySnapAgentToNavMesh(bool force)
    {
        if (Agent == null || !Agent.enabled)
            return false;

        if (!force && Agent.isOnNavMesh)
            return true;

        Vector3 queryPosition = transform.position;
        float sampleDistance = Mathf.Max(0.1f, navMeshSnapDistance);
        int areaMask = Agent.areaMask != 0 ? Agent.areaMask : NavMesh.AllAreas;

        if (!NavMesh.SamplePosition(queryPosition, out NavMeshHit hit, sampleDistance, areaMask))
            return false;

        Vector3 snapped = new Vector3(hit.position.x, hit.position.y, transform.position.z);
        transform.position = snapped;

        if (_rb2D != null)
            _rb2D.position = new Vector2(snapped.x, snapped.y);

        if (!Agent.Warp(snapped))
            return false;

        return Agent.isOnNavMesh;
    }

    void SyncNavAgentSettings()
    {
        if (Agent == null)
            return;

        Agent.updateUpAxis = false;
        Agent.updateRotation = false;
        Agent.acceleration = Mathf.Max(0.01f, acceleration);
        Agent.stoppingDistance = Mathf.Max(0.01f, stopDistance);
        Agent.speed = Mathf.Max(0.01f, CurrentMoveSpeed);
    }

    void ConfigureNavAgent()
    {
        if (Agent == null)
            return;

        Agent.updateUpAxis = false;
        Agent.updateRotation = false;
        Agent.autoRepath = true;
        Agent.autoBraking = true;
    }

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
        if (ActiveTip == null)
            return;

        if (!TryGetDesiredFacingDirection(out Vector2 desiredDir))
            return;

        Vector2 tipForward = GetTipForward(ActiveTip);

        float angle = Vector2.SignedAngle(tipForward, desiredDir);
        float step = rotateSpeed * Time.deltaTime;

        transform.Rotate(0f, 0f, Mathf.Clamp(angle, -step, step));
    }

    public Vector2 ForwardDir
    {
        get
        {
            if (ActiveTip == null)
                return transform.up;
            return GetTipForward(ActiveTip).normalized;
        }
    }

    public void MoveForward(float speedMultiplier)
    {
        MoveInDirection(ForwardDir, speedMultiplier);
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public abstract class EnemyBase : MonoBehaviour
{
    const string WallsLayerName = "Walls";
    const string ObstaclesLayerName = "Obstacles";

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
    [SerializeField, Min(0.05f)] float navMeshRepathDistance = 0.4f;
    [SerializeField, Min(0.1f)] float gridCellSize = 0.5f;
    [SerializeField, Min(0.01f)] float navMeshCornerTolerance = 0.1f;
    [SerializeField, Min(0.5f)] float fallbackBoundsSize = 40f;
    [SerializeField] LayerMask pathBlockMask;
 



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
    [SerializeField, Min(0f)] private float hitRevealDuration = 0.9f;
    [SerializeField, Tooltip("Delay before destroying corpses. Set negative to keep them around.")] private float deathCleanupDelay = 3f;
    [SerializeField] private Animator deathAnimatorOverride;
    [SerializeField] private string deathTriggerName = "Die";

    [Header("Death VFX")]
    [SerializeField] private ParticleSystem deathVfx;
    [SerializeField] private bool disableSpritesOnDeath = true;
    [SerializeField] private List<SpriteRenderer> spritesToDisable = new();

    public int CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    public event Action<EnemyBase> EnemyDied;

    private Collider2D[] _cachedColliders;
    Coroutine _deathCleanupRoutine;
    readonly List<Vector2> _gridPathPoints = new();
    Vector2 _navDestination;
    int _navCornerIndex;
    float _nextNavPathUpdateTime;
    bool _navPathValid;
    bool _hasNavDestination;
 

    protected IEnemyState currentState;
    protected bool IsTargetPlayerDead => TargetPlayerController != null && TargetPlayerController.IsDead;

    protected virtual void Awake()
    {
        EnsurePathBlockMask();

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
        if (disableSpritesOnDeath)
            SetSpritesVisible(true);
    }

    void OnValidate()
    {
        EnsurePathBlockMask();
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

        HandleRevealAndAggroOnHit();

        if (CurrentHealth <= 0)
            HandleDeath();
        else
            OnDamaged();
    }

    protected virtual void OnDamaged()
    {
    }

    protected virtual void AggroOnHit()
    {
    }

    void HandleRevealAndAggroOnHit()
    {
        if (_enemyVisibility != null && !_enemyVisibility.IsVisible)
            _enemyVisibility.ForceReveal(hitRevealDuration, instant: true);

        if (!IsTargetPlayerDead)
            AggroOnHit();
    }

    public void SetChaseRevealForced(bool forced, bool instant = true)
    {
        if (_enemyVisibility == null)
            return;

        _enemyVisibility.SetForcedVisiblePersistent(forced, instant);
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
        }

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
            if (sr == null) continue;
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
        if (TryBuildGridPath((Vector2)transform.position, destination, _gridPathPoints))
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
            _gridPathPoints.Clear();
        }

        float interval = Mathf.Max(0.01f, navMeshRepathInterval);
        _nextNavPathUpdateTime = Time.time + interval;
        return _navPathValid;
    }

    bool TryGetCurrentCorner(Vector2 currentPosition, out Vector2 corner)
    {
        corner = Vector2.zero;
        if (!_navPathValid || _gridPathPoints.Count == 0)
            return false;

        float tolerance = Mathf.Max(0.005f, navMeshCornerTolerance);
        while (_navCornerIndex < _gridPathPoints.Count)
        {
            corner = _gridPathPoints[_navCornerIndex];
            float dist = Vector2.Distance(currentPosition, corner);
            if (dist <= tolerance)
            {
                if (_navCornerIndex == _gridPathPoints.Count - 1)
                    return false;

                _navCornerIndex++;
                continue;
            }

            int lookAhead = _navCornerIndex;
            for (int i = _navCornerIndex + 1; i < _gridPathPoints.Count; i++)
            {
                if (!HasPathLineOfSight(currentPosition, _gridPathPoints[i]))
                    break;

                lookAhead = i;
            }

            corner = _gridPathPoints[lookAhead];
            _navCornerIndex = lookAhead;
            return true;
        }

        return false;
    }

    bool HasPathLineOfSight(Vector2 from, Vector2 to)
    {
        if (pathBlockMask.value == 0)
            return true;

        RaycastHit2D hit = Physics2D.Linecast(from, to, pathBlockMask);
        return hit.collider == null;
    }

    bool TryBuildGridPath(Vector2 startWorld, Vector2 goalWorld, List<Vector2> outPath)
    {
        outPath.Clear();

        float cell = Mathf.Max(0.1f, gridCellSize);
        float halfCell = cell * 0.5f;
        float blockProbeRadius = Mathf.Max(0.05f, cell * 0.35f);

        Bounds bounds = ResolvePathBounds(startWorld, goalWorld, cell);
        Vector2 min = bounds.min;
        Vector2 max = bounds.max;

        int width = Mathf.Max(2, Mathf.CeilToInt((max.x - min.x) / cell));
        int height = Mathf.Max(2, Mathf.CeilToInt((max.y - min.y) / cell));
        int count = width * height;
        if (count <= 0 || count > 262144)
            return false;

        bool[] blocked = new bool[count];
        for (int y = 0; y < height; y++)
        {
            float wy = min.y + y * cell + halfCell;
            for (int x = 0; x < width; x++)
            {
                float wx = min.x + x * cell + halfCell;
                int idx = y * width + x;
                blocked[idx] = pathBlockMask.value != 0
                    && Physics2D.OverlapCircle(new Vector2(wx, wy), blockProbeRadius, pathBlockMask) != null;
            }
        }

        int sx = Mathf.Clamp(Mathf.FloorToInt((startWorld.x - min.x) / cell), 0, width - 1);
        int sy = Mathf.Clamp(Mathf.FloorToInt((startWorld.y - min.y) / cell), 0, height - 1);
        int gx = Mathf.Clamp(Mathf.FloorToInt((goalWorld.x - min.x) / cell), 0, width - 1);
        int gy = Mathf.Clamp(Mathf.FloorToInt((goalWorld.y - min.y) / cell), 0, height - 1);

        if (!TryFindNearestWalkableCell(sx, sy, width, height, blocked, out sx, out sy))
            return false;
        if (!TryFindNearestWalkableCell(gx, gy, width, height, blocked, out gx, out gy))
            return false;

        int start = sy * width + sx;
        int goal = gy * width + gx;
        if (start == goal)
            return false;

        float[] gScore = new float[count];
        float[] fScore = new float[count];
        int[] cameFrom = new int[count];
        bool[] closed = new bool[count];
        for (int i = 0; i < count; i++)
        {
            gScore[i] = float.PositiveInfinity;
            fScore[i] = float.PositiveInfinity;
            cameFrom[i] = -1;
        }

        List<int> open = new List<int>(128);
        gScore[start] = 0f;
        fScore[start] = Heuristic(start, goal, width);
        open.Add(start);

        int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };

        while (open.Count > 0)
        {
            int bestIdx = 0;
            int current = open[0];
            float bestF = fScore[current];
            for (int i = 1; i < open.Count; i++)
            {
                int node = open[i];
                float f = fScore[node];
                if (f < bestF)
                {
                    bestF = f;
                    bestIdx = i;
                    current = node;
                }
            }

            open.RemoveAt(bestIdx);
            if (current == goal)
            {
                ReconstructPath(current, cameFrom, width, min, cell, halfCell, outPath);
                return outPath.Count > 0;
            }

            closed[current] = true;
            int cx = current % width;
            int cy = current / width;

            for (int dir = 0; dir < 8; dir++)
            {
                int nx = cx + dx[dir];
                int ny = cy + dy[dir];
                if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                    continue;

                int neighbor = ny * width + nx;
                if (closed[neighbor] || blocked[neighbor])
                    continue;

                if (dx[dir] != 0 && dy[dir] != 0)
                {
                    int sideA = cy * width + nx;
                    int sideB = ny * width + cx;
                    if (blocked[sideA] || blocked[sideB])
                        continue;
                }

                float stepCost = (dx[dir] == 0 || dy[dir] == 0) ? 1f : 1.4142135f;
                float tentativeG = gScore[current] + stepCost;
                if (tentativeG >= gScore[neighbor])
                    continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeG;
                fScore[neighbor] = tentativeG + Heuristic(neighbor, goal, width);

                if (!open.Contains(neighbor))
                    open.Add(neighbor);
            }
        }

        return false;
    }

    static float Heuristic(int a, int b, int width)
    {
        int ax = a % width;
        int ay = a / width;
        int bx = b % width;
        int by = b / width;
        return Mathf.Abs(ax - bx) + Mathf.Abs(ay - by);
    }

    static void ReconstructPath(int goal, int[] cameFrom, int width, Vector2 min, float cell, float halfCell, List<Vector2> outPath)
    {
        outPath.Clear();
        int current = goal;
        while (current >= 0)
        {
            int x = current % width;
            int y = current / width;
            outPath.Add(new Vector2(min.x + x * cell + halfCell, min.y + y * cell + halfCell));
            current = cameFrom[current];
        }

        outPath.Reverse();
        if (outPath.Count > 1)
            outPath.RemoveAt(0);
    }

    static bool TryFindNearestWalkableCell(int x, int y, int width, int height, bool[] blocked, out int outX, out int outY)
    {
        int center = y * width + x;
        if (!blocked[center])
        {
            outX = x;
            outY = y;
            return true;
        }

        int maxRadius = Mathf.Max(width, height);
        for (int r = 1; r <= maxRadius; r++)
        {
            int minX = Mathf.Max(0, x - r);
            int maxX = Mathf.Min(width - 1, x + r);
            int minY = Mathf.Max(0, y - r);
            int maxY = Mathf.Min(height - 1, y + r);

            for (int yy = minY; yy <= maxY; yy++)
            {
                for (int xx = minX; xx <= maxX; xx++)
                {
                    if (xx != minX && xx != maxX && yy != minY && yy != maxY)
                        continue;

                    int idx = yy * width + xx;
                    if (!blocked[idx])
                    {
                        outX = xx;
                        outY = yy;
                        return true;
                    }
                }
            }
        }

        outX = x;
        outY = y;
        return false;
    }

    Bounds ResolvePathBounds(Vector2 start, Vector2 goal, float cell)
    {
        Bounds bounds;
        if (RoomGenerator.HasInstance && RoomGenerator.Instance.RoomBounds.size.sqrMagnitude > 0.0001f)
        {
            bounds = RoomGenerator.Instance.RoomBounds;
        }
        else
        {
            float size = Mathf.Max(2f, fallbackBoundsSize);
            bounds = new Bounds(transform.position, new Vector3(size, size, 0f));
        }

        bounds.Encapsulate(new Vector3(start.x, start.y, bounds.center.z));
        bounds.Encapsulate(new Vector3(goal.x, goal.y, bounds.center.z));

        float padding = Mathf.Max(cell * 2f, 0.5f);
        bounds.Expand(new Vector3(padding, padding, 0f));
        return bounds;
    }

    void EnsurePathBlockMask()
    {
        int wallsLayer = LayerMask.NameToLayer(WallsLayerName);
        if (wallsLayer >= 0)
            pathBlockMask |= (1 << wallsLayer);

        int obstaclesLayer = LayerMask.NameToLayer(ObstaclesLayerName);
        if (obstaclesLayer >= 0)
            pathBlockMask |= (1 << obstaclesLayer);
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










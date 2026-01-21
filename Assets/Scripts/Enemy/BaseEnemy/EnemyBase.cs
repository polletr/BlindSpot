using System.Collections.Generic;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public abstract class EnemyBase : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] protected Transform player; // assign or auto-find
    public Rigidbody2D RB { get; private set; }

    [Header("Movement")]
    public float moveSpeed = 4f;
    public float acceleration = 25f;
    public float chaseSpeedMultiplier = 1.35f;
    public float alertTime = 0.15f;
    public float chaseCommitTime = 0.55f;
    public float repositionTime = 0.35f;
    public float stopDistance = 0.65f;


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

    protected IEnemyState currentState;

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
        if (TryGetComponent(out EnemyVisibility enemyVis))
        {
            enemyVis.player = player;
        }

        if (tips == null || tips.Count == 0)
        {
            foreach (EnemyTipKill t in GetComponentsInChildren<EnemyTipKill>())
            {
                tips.Add(t.transform);
            }    

        }

        PickActiveTip(force: true);

    }

    protected virtual void Update()
    {
        currentState?.Tick(this);

        if (!HasPlayer) return;

        PickActiveTip(force: false);
        RotateSoActiveTipFacesPlayer();

    }

    protected virtual void FixedUpdate()
    {
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

    public bool PlayerInDetectRadius() => HasPlayer && DistToPlayer <= detectRadius;
    public bool PlayerBeyondLoseRadius() => !HasPlayer || DistToPlayer >= loseRadius;

    // Basic “accelerated velocity” steering (feels consistent with your player)
    public void MoveInDirection(Vector2 dir, float speedMultiplier = 1f)
    {
        if (dir.sqrMagnitude < 0.0001f) return;

        Vector2 targetVel = dir.normalized * (moveSpeed * speedMultiplier);
        Vector2 newVel = Vector2.MoveTowards(RB.linearVelocity, targetVel, acceleration * Time.fixedDeltaTime);
        RB.linearVelocity = newVel;
    }

    public void StopMove(float decel = 40f)
    {
        RB.linearVelocity = Vector2.MoveTowards(RB.linearVelocity, Vector2.zero, decel * Time.fixedDeltaTime);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, detectRadius);
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, loseRadius);
    }

    // -----------------------------
    // Tip logic
    // -----------------------------

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

    public void FreezeTipSelection(float duration)
    {
        _nextTipSwitchTime = Time.time + duration;
    }

    // -----------------------------
    // Orientation
    // -----------------------------

    void RotateSoActiveTipFacesPlayer()
    {
        if (ActiveTip == null) return;

        Vector2 toPlayer = (Vector2)player.position - (Vector2)ActiveTip.position;
        if (toPlayer.sqrMagnitude < 0.0001f) return;

        // TIP FORWARD IS UP
        Vector2 tipForward = ActiveTip.up;

        float angle = Vector2.SignedAngle(tipForward, toPlayer);
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
            return ActiveTip.up.normalized;
        }
    }

    public void MoveForward(float speedMultiplier)
    {
        MoveInDirection(ForwardDir, speedMultiplier);
    }
}

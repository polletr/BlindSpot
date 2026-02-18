using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Light2D))]
public class PlayerVisionField : MonoBehaviour
{
    public static PlayerVisionField Instance { get; private set; }

    [SerializeField] Light2D visionLight;
    [SerializeField] LayerMask detectionMask = ~0;
    [Header("Debug")]
    [SerializeField] bool debugLogs = false;
    [SerializeField] bool debugGizmos = true;
    [SerializeField] Color gizmoRadiusColor = new Color(1f, 0.9f, 0.1f, 0.15f);
    [SerializeField] Color gizmoContactColor = new Color(0.1f, 1f, 0.6f, 0.5f);

    readonly HashSet<EnemyVisibility> currentEnemies = new();
    readonly HashSet<Revealable> currentRevealables = new();
    readonly HashSet<EnemyVisibility> frameEnemies = new();
    readonly HashSet<Revealable> frameRevealables = new();
    readonly List<EnemyVisibility> exitEnemies = new();
    readonly List<Revealable> exitRevealables = new();

    PlayerController ownerController;
    float cachedOuterRadius;
    float baseOuterRadius;
    float baseLightIntensity = 1f;
    UpgradeManager upgradeManager;

    public float CurrentOuterRadius => cachedOuterRadius;

    Vector2 CenterWorld => (Vector2)transform.position;

    void Awake()
    {
        ownerController = GetComponentInParent<PlayerController>();
        if (visionLight == null)
            visionLight = GetComponent<Light2D>();

        if (Instance != null && Instance != this)
            Debug.LogWarning("Multiple PlayerVisionField instances detected; keeping the latest.");

        Instance = this;
        CacheBaseLightProperties();
        TryHookUpgradeManager();
        ApplyRoundLightSnapshot(upgradeManager != null ? upgradeManager.Snapshot : UpgradeSnapshot.Identity);
        SyncLightRadius(true);
    }

    void OnEnable()
    {
        if (Instance != this)
            Instance = this;
        TryHookUpgradeManager();
        ApplyRoundLightSnapshot(upgradeManager != null ? upgradeManager.Snapshot : UpgradeSnapshot.Identity);
        SyncLightRadius(true);
    }

    void OnDisable()
    {
        if (Instance == this)
            Instance = null;

        if (upgradeManager != null)
        {
            upgradeManager.UpgradesChanged -= HandleUpgradeChanged;
            upgradeManager = null;
        }

        ClearAllContacts();
    }

    void TryHookUpgradeManager()
    {
        if (upgradeManager != null)
            return;

        upgradeManager = UpgradeManager.Instance;
        if (upgradeManager != null)
            upgradeManager.UpgradesChanged += HandleUpgradeChanged;
    }

    void HandleUpgradeChanged(UpgradeSnapshot snapshot)
    {
        ApplyRoundLightSnapshot(snapshot);
    }

    void CacheBaseLightProperties()
    {
        if (visionLight == null)
            visionLight = GetComponent<Light2D>();
        if (visionLight == null)
            return;

        baseOuterRadius = GetLightOuterRadius();
        baseLightIntensity = visionLight.intensity;
    }

    void ApplyRoundLightSnapshot(UpgradeSnapshot snapshot)
    {
        if (visionLight == null)
            return;

        float radius = Mathf.Max(0f, baseOuterRadius * Mathf.Max(0f, snapshot.roundLightMultiplier));
        SetLightOuterRadius(radius);

        float intensity = Mathf.Max(0f, baseLightIntensity * Mathf.Max(0f, snapshot.roundLightIntensityMultiplier));
        visionLight.intensity = intensity;

        SyncLightRadius(true);
    }

    void SetLightOuterRadius(float radius)
    {
        if (visionLight == null)
            return;
        
        visionLight.pointLightOuterRadius = radius;

    }

    void Update()
    {
        SyncLightRadius();
        ScanContacts();
    }

    void SyncLightRadius(bool force = false)
    {
        if (visionLight == null)
            visionLight = GetComponent<Light2D>();
        if (visionLight == null) return;

        float desired = Mathf.Max(0f, GetLightOuterRadius());
        if (!force && Mathf.Approximately(desired, cachedOuterRadius)) return;
        cachedOuterRadius = desired;
    }

    float GetLightOuterRadius()
    {
        if (visionLight == null) return 0f;
        
        return visionLight.pointLightOuterRadius;

    }

    bool ShouldIgnore(Collider2D other)
    {
        if (other == null) return true;
        if (other.transform == transform) return true;
        if (other.transform.IsChildOf(transform)) return true;
        if (ownerController != null)
        {
            var otherPlayer = other.GetComponentInParent<PlayerController>();
            if (otherPlayer == ownerController)
                return true;
        }
        return false;
    }

    void ScanContacts()
    {
        frameEnemies.Clear();
        frameRevealables.Clear();

        Collider2D[] hits = Physics2D.OverlapCircleAll(CenterWorld, cachedOuterRadius, detectionMask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (ShouldIgnore(col)) continue;

            AddContact(col, frameEnemies);
            AddContact(col, frameRevealables);
        }

        UpdateEnemyContacts();
        UpdateRevealableContacts();
    }

    void ClearAllContacts()
    {
        foreach (var enemy in currentEnemies)
        {
            if (enemy != null)
                enemy.SetVisionContact(false);
        }
        currentEnemies.Clear();

        foreach (var reveal in currentRevealables)
        {
            if (reveal != null)
                reveal.SetVisionContact(false);
        }
        currentRevealables.Clear();
    }

    public bool ContainsPoint(Vector2 worldPosition)
    {
        if (cachedOuterRadius <= 0f) return false;
        float sqrRadius = cachedOuterRadius * cachedOuterRadius;
        return (worldPosition - CenterWorld).sqrMagnitude <= sqrRadius;
    }

    public void ForceExit(EnemyVisibility enemy)
    {
        if (enemy == null) return;
        if (currentEnemies.Remove(enemy))
            enemy.SetVisionContact(false);
    }

    public void ForceExit(Revealable revealable)
    {
        if (revealable == null) return;
        if (currentRevealables.Remove(revealable))
            revealable.SetVisionContact(false);
    }

    void AddContact<T>(Collider2D col, HashSet<T> set) where T : Component
    {
        if (col == null) return;
        var target = col.GetComponentInParent<T>();
        if (target != null)
            set.Add(target);
    }

    void UpdateEnemyContacts()
    {
        foreach (var enemy in frameEnemies)
        {
            if (enemy == null) continue;
            if (currentEnemies.Add(enemy))
            {
                enemy.SetVisionContact(true);
                LogContact("Enemy enter", enemy);
            }
        }

        exitEnemies.Clear();
        foreach (var enemy in currentEnemies)
        {
            if (!frameEnemies.Contains(enemy))
                exitEnemies.Add(enemy);
        }
        foreach (var enemy in exitEnemies)
        {
            currentEnemies.Remove(enemy);
            enemy.SetVisionContact(false);
            LogContact("Enemy exit", enemy);
        }
    }

    void UpdateRevealableContacts()
    {
        foreach (var reveal in frameRevealables)
        {
            if (reveal == null) continue;
            if (currentRevealables.Add(reveal))
            {
                reveal.SetVisionContact(true);
                LogContact("Reveal enter", reveal);
            }
        }

        exitRevealables.Clear();
        foreach (var reveal in currentRevealables)
        {
            if (!frameRevealables.Contains(reveal))
                exitRevealables.Add(reveal);
        }
        foreach (var reveal in exitRevealables)
        {
            currentRevealables.Remove(reveal);
            reveal.SetVisionContact(false);
            LogContact("Reveal exit", reveal);
        }
    }

    void LogContact(string label, Component target)
    {
        if (!debugLogs || target == null) return;
    }

    void OnDrawGizmosSelected()
    {
        if (!debugGizmos) return;

        if (visionLight == null)
            visionLight = GetComponent<Light2D>();

        float radius = cachedOuterRadius;
        if (radius <= 0f && visionLight != null)
            radius = visionLight.pointLightOuterRadius;

        Gizmos.color = gizmoRadiusColor;
        Gizmos.DrawWireSphere(transform.position, radius);

        Gizmos.color = gizmoContactColor;
        foreach (var enemy in currentEnemies)
        {
            if (enemy == null) continue;
            Gizmos.DrawLine(transform.position, enemy.transform.position);
            Gizmos.DrawSphere(enemy.transform.position, 0.05f);
        }
        foreach (var reveal in currentRevealables)
        {
            if (reveal == null) continue;
            Gizmos.DrawLine(transform.position, reveal.transform.position);
            Gizmos.DrawSphere(reveal.transform.position, 0.04f);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central authority that keeps track of both per-run and permanent upgrades,
/// exposes combined multipliers, and hands out upgrade offers.
/// </summary>
public class UpgradeManager : Singleton<UpgradeManager>
{
    [Header("Available Upgrades")] 
    [SerializeField] private List<RunUpgrade> runUpgradePool = new List<RunUpgrade>();
    [SerializeField] private List<PermaUpgrade> permaUpgradePool = new List<PermaUpgrade>();

    [Header("Starting Loadout")]
    [SerializeField] private List<RunUpgrade> startingRunUpgrades = new List<RunUpgrade>();
    [SerializeField] private List<PermaUpgrade> startingPermaUpgrades = new List<PermaUpgrade>();

    private readonly List<RunUpgrade> _activeRunUpgrades = new List<RunUpgrade>();
    private readonly List<PermaUpgrade> _activePermaUpgrades = new List<PermaUpgrade>();
    private UpgradeSnapshot _snapshot = UpgradeSnapshot.Identity;

    public event Action<UpgradeSnapshot> UpgradesChanged;

    public IReadOnlyList<RunUpgrade> ActiveRunUpgrades => _activeRunUpgrades;
    public IReadOnlyList<PermaUpgrade> ActivePermaUpgrades => _activePermaUpgrades;
    public UpgradeSnapshot Snapshot => _snapshot;
    public IReadOnlyList<PermaUpgrade> PermaUpgradePool => permaUpgradePool;

    public float VelocityMultiplier => _snapshot.velocityMultiplier;
    public float DashDistanceMultiplier => _snapshot.dashDistanceMultiplier;
    public float FlashlightAngleMultiplier => _snapshot.flashlightAngleMultiplier;
    public float FlashlightRangeMultiplier => _snapshot.flashlightRangeMultiplier;
    public float RoundLightMultiplier => _snapshot.roundLightMultiplier;
    public float RoundLightIntensityMultiplier => _snapshot.roundLightIntensityMultiplier;
    public float CurrencyCollectorMultiplier => _snapshot.currencyCollectorMultiplier;
    public float ScanCooldownMultiplier => _snapshot.scanCooldownMultiplier;
    public float DashCooldownMultiplier => _snapshot.dashCooldownMultiplier;
    public float EnemySpeedMultiplier => _snapshot.enemySpeedMultiplier;
    public float EnemyDetectionRadiusMultiplier => _snapshot.enemyDetectionRadiusMultiplier;
    public float EnemyLoseSightRadiusMultiplier => _snapshot.enemyLoseSightRadiusMultiplier;
    public float EnemyAmountMultiplier => _snapshot.enemyAmountMultiplier;
    public float BlopsAmountMultiplier => _snapshot.blopsAmountMultiplier;
    public bool RadarAlwaysOn => _snapshot.radarAlwaysOn;







    protected override void Awake()
    {
        base.Awake();
        if (Instance != this)
            return;

        ResetState();
    }

    /// <summary>
    /// Clears previously selected run upgrades and reapplies baseline data.
    /// Useful when starting a new run.
    /// </summary>
    public void ResetState()
    {
        _activeRunUpgrades.Clear();
        _activePermaUpgrades.Clear();

        if (startingRunUpgrades != null)
            _activeRunUpgrades.AddRange(startingRunUpgrades);

        if (startingPermaUpgrades != null)
            _activePermaUpgrades.AddRange(startingPermaUpgrades);

        RecalculateSnapshot();
    }

    public void ApplyUpgrade(RunUpgrade upgrade)
    {
        if (upgrade == null)
            return;

        _activeRunUpgrades.Add(upgrade);
        RecalculateSnapshot();
    }

    public void AddPermaUpgrade(PermaUpgrade upgrade)
    {
        if (upgrade == null || _activePermaUpgrades.Contains(upgrade))
            return;

        _activePermaUpgrades.Add(upgrade);
        RecalculateSnapshot();
    }

    /// <summary>
    /// Returns up to <paramref name="count"/> unique upgrades from the pool (or fewer if unavailable).
    /// </summary>
    public RunUpgrade[] GetRandomUpgrades(int count)
    {
        if (runUpgradePool == null || runUpgradePool.Count == 0 || count <= 0)
            return Array.Empty<RunUpgrade>();

        int finalCount = Mathf.Clamp(count, 0, runUpgradePool.Count);
        if (finalCount == 0)
            return Array.Empty<RunUpgrade>();

        var poolCopy = ListPool<RunUpgrade>.Get();
        poolCopy.AddRange(runUpgradePool);

        var selections = new RunUpgrade[finalCount];
        for (int i = 0; i < finalCount; i++)
        {
            int index = UnityEngine.Random.Range(0, poolCopy.Count);
            selections[i] = poolCopy[index];
            poolCopy.RemoveAt(index);
        }

        ListPool<RunUpgrade>.Release(poolCopy);
        return selections;
    }

    private void RecalculateSnapshot()
    {
        var snapshot = UpgradeSnapshot.Identity;

        foreach (var perma in _activePermaUpgrades)
            snapshot.Merge(perma);

        foreach (var run in _activeRunUpgrades)
            snapshot.Merge(run);

        _snapshot = snapshot;
        UpgradesChanged?.Invoke(_snapshot);
    }
}

/// <summary>
/// Helper struct that stores the combined multipliers from both run and permanent upgrades.
/// </summary>
[Serializable]
public struct UpgradeSnapshot
{
    public float velocityMultiplier;
    public float dashDistanceMultiplier;
    public float flashlightAngleMultiplier;
    public float flashlightRangeMultiplier;
    public float roundLightMultiplier;
    public float currencyCollectorMultiplier;
    public float scanCooldownMultiplier;
    public float roundLightIntensityMultiplier;
    public float dashCooldownMultiplier;
    public float enemySpeedMultiplier;
    public float enemyDetectionRadiusMultiplier;
    public float enemyLoseSightRadiusMultiplier;
    public float enemyAmountMultiplier;
    public float blopsAmountMultiplier;
    public bool radarAlwaysOn;


    public static UpgradeSnapshot Identity => new UpgradeSnapshot()
    {
        velocityMultiplier = 1f,
        dashDistanceMultiplier = 1f,
        flashlightAngleMultiplier = 1f,
        flashlightRangeMultiplier = 1f,
        roundLightMultiplier = 1f,
        currencyCollectorMultiplier = 1f,
        scanCooldownMultiplier = 1f,
        roundLightIntensityMultiplier = 1f,
        dashCooldownMultiplier = 1f,
        enemySpeedMultiplier = 1f,
        enemyDetectionRadiusMultiplier = 1f,
        enemyLoseSightRadiusMultiplier = 1f,
        enemyAmountMultiplier = 1f,
        blopsAmountMultiplier = 1f,
        radarAlwaysOn = false,

    };

    public void Merge(RunUpgrade upgrade)
    {
        if (upgrade == null)
            return;

        velocityMultiplier *= upgrade.velocityTempMultiplier;
        dashDistanceMultiplier *= upgrade.dashDistanceTempMultiplier;
        flashlightAngleMultiplier *= upgrade.flashlightAngleTempMultiplier;
        flashlightRangeMultiplier *= upgrade.flashlightRangeTempMultiplier;
        roundLightMultiplier *= upgrade.roundLightTempMultiplier;
        currencyCollectorMultiplier *= upgrade.currencyCollectorTempMultiplier;
        scanCooldownMultiplier *= upgrade.scanCooldownTempMultiplier;
        roundLightIntensityMultiplier *= upgrade.roundLightIntensityTempMultiplier;
        dashCooldownMultiplier *= upgrade.dashCooldownTempMultiplier;
        enemySpeedMultiplier *= upgrade.enemySpeedTempMultiplier;
        enemyDetectionRadiusMultiplier *= upgrade.enemyDetectionRadiusTempMultiplier;
        enemyLoseSightRadiusMultiplier *= upgrade.enemyLoseSightRadiusTempMultiplier;
        enemyAmountMultiplier *= upgrade.enemyAmountTempMultiplier;
        blopsAmountMultiplier *= upgrade.blopsAmountTempMultiplier;
        radarAlwaysOn |= upgrade.radarAlwaysOnTemp;


    }

    public void Merge(PermaUpgrade upgrade)
    {
        if (upgrade == null)
            return;

        velocityMultiplier *= upgrade.velocityPermMultiplier;
        dashDistanceMultiplier *= upgrade.dashDistancePermMultiplier;
        flashlightAngleMultiplier *= upgrade.flashlightAnglePermMultiplier;
        flashlightRangeMultiplier *= upgrade.flashlightRangePermMultiplier;
        roundLightMultiplier *= upgrade.roundLightPermMultiplier;
        currencyCollectorMultiplier *= upgrade.currencyCollectorPermMultiplier;
        scanCooldownMultiplier *= upgrade.scanCooldownPermMultiplier;
        roundLightIntensityMultiplier *= upgrade.roundLightIntensityPermMultiplier;
        dashCooldownMultiplier *= upgrade.dashCooldownPermMultiplier;
        if (upgrade.radarAlwaysOnPerm)
            radarAlwaysOn = true;
    }
}

/// <summary>
/// Trivial list pooling utility to avoid per-frame allocations when rolling upgrades.
/// </summary>
internal static class ListPool<T>
{
    private static readonly Stack<List<T>> Pool = new Stack<List<T>>();

    public static List<T> Get()
    {
        return Pool.Count > 0 ? Pool.Pop() : new List<T>();
    }

    public static void Release(List<T> list)
    {
        list.Clear();
        Pool.Push(list);
    }
}


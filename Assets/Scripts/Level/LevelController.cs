using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class LevelController : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private TriangleEnemy triangleEnemy;
    [SerializeField] private SquareEnemy squareEnemy;
    [SerializeField] private GameObject starEnemy;
    [SerializeField] private GameObject dropToSpawn;
    [SerializeField] private GameObject exitToSpawn;

    [Header("Spawn Points")]
    [SerializeField] private List<EnemySpawner> enemySpawners = new List<EnemySpawner>();
    [SerializeField] private List<DropSpawner> dropSpawners = new List<DropSpawner>();
    [SerializeField] private List<PlayerSpawner> playerSpawners = new List<PlayerSpawner>();
    [SerializeField] private List<ExitSpawner> exitSpawners = new List<ExitSpawner>();

    private readonly List<Spawner> spawnedSpawners = new List<Spawner>();
    private int starsSpawnedThisLevel;
    private Vector2 playerSpawnPoint;
    private bool hasPlayerSpawnPoint;

    public bool TryGetPlayerSpawnPoint(out Vector2 spawnPoint)
    {
        spawnPoint = playerSpawnPoint;
        return hasPlayerSpawnPoint;
    }

    private void Awake()
    {
        if (enemySpawners.Count == 0)
            enemySpawners.AddRange(GetComponentsInChildren<EnemySpawner>(true));

        if (dropSpawners.Count == 0)
            dropSpawners.AddRange(GetComponentsInChildren<DropSpawner>(true));

        if (playerSpawners.Count == 0)
            playerSpawners.AddRange(GetComponentsInChildren<PlayerSpawner>(true));

        if (exitSpawners.Count == 0)
            exitSpawners.AddRange(GetComponentsInChildren<ExitSpawner>(true));
    }

    public void Initialize(DungeonDifficultyConfig.DungeonRule rule)
    {
        DespawnAll();

        starsSpawnedThisLevel = 0;

        SelectPlayerSpawnPoint();
        SpawnExit();
        SpawnEnemies(rule);
        SpawnDrops(rule);
    }

    public void DespawnAll()
    {
        if (spawnedSpawners.Count <= 0)
            return;

        foreach (Spawner spawner in spawnedSpawners)
        {
            if (spawner == null)
                continue;

            spawner.DespawnObject();
        }

        spawnedSpawners.Clear();
    }

    private void SelectPlayerSpawnPoint()
    {
        hasPlayerSpawnPoint = false;

        if (playerSpawners.Count == 0)
            return;

        PlayerSpawner selectedSpawner = playerSpawners[Random.Range(0, playerSpawners.Count)];
        playerSpawnPoint = selectedSpawner.transform.position;
        hasPlayerSpawnPoint = true;
    }

    private void SpawnExit()
    {
        if (exitToSpawn == null || exitSpawners.Count == 0)
            return;

        ExitSpawner selectedSpawner = exitSpawners[Random.Range(0, exitSpawners.Count)];
        selectedSpawner.SpawnObject(exitToSpawn);
        spawnedSpawners.Add(selectedSpawner);
    }


    private void SpawnEnemies(DungeonDifficultyConfig.DungeonRule rule)
    {
        if (enemySpawners.Count == 0)
            return;

        int minEnemies = Mathf.Max(0, rule.minEnemies);
        int maxEnemies = Mathf.Max(minEnemies, rule.maxEnemies);
        int enemyCount = Random.Range(minEnemies, maxEnemies + 1);

        int spawnCount = Mathf.Min(enemyCount, enemySpawners.Count);
        List<EnemySpawner> selectedSpawners = PickRandomSpawners(enemySpawners, spawnCount);

        for (int i = 0; i < selectedSpawners.Count; i++)
        {
            GameObject enemyPrefab = PickEnemyPrefab(rule);
            if (enemyPrefab == null)
                continue;

            selectedSpawners[i].SpawnObject(enemyPrefab);
            spawnedSpawners.Add(selectedSpawners[i]);
        }
    }

    private void SpawnDrops(DungeonDifficultyConfig.DungeonRule rule)
    {
        if (dropToSpawn == null || dropSpawners.Count == 0)
            return;

        int minDrops = Mathf.Max(0, rule.minBlops);
        int maxDrops = Mathf.Max(minDrops, rule.maxBlops);
        int dropCount = Random.Range(minDrops, maxDrops + 1);

        int spawnCount = Mathf.Min(dropCount, dropSpawners.Count);
        List<DropSpawner> selectedSpawners = PickRandomSpawners(dropSpawners, spawnCount);

        for (int i = 0; i < selectedSpawners.Count; i++)
        {
            selectedSpawners[i].SpawnObject(dropToSpawn);
            spawnedSpawners.Add(selectedSpawners[i]);
        }
    }

    private GameObject PickEnemyPrefab(DungeonDifficultyConfig.DungeonRule rule)
    {
        GameObject squarePrefab = squareEnemy != null ? squareEnemy.gameObject : null;
        GameObject trianglePrefab = triangleEnemy != null ? triangleEnemy.gameObject : null;
        GameObject starPrefab = starEnemy;

        float squareWeight = squarePrefab != null ? Mathf.Max(0f, rule.squareWeight) : 0f;
        float triangleWeight = trianglePrefab != null ? Mathf.Max(0f, rule.triangleWeight) : 0f;
        float starWeight = (starPrefab != null && starsSpawnedThisLevel < rule.maxStars) ? Mathf.Max(0f, rule.starWeight) : 0f;

        float total = squareWeight + triangleWeight + starWeight;
        if (total <= 0f)
            return squarePrefab != null ? squarePrefab : trianglePrefab != null ? trianglePrefab : starPrefab;

        float roll = Random.value * total;

        if (roll < squareWeight)
            return squarePrefab;

        roll -= squareWeight;
        if (roll < triangleWeight)
            return trianglePrefab;

        starsSpawnedThisLevel++;
        return starPrefab;
    }

    private static List<TSpawner> PickRandomSpawners<TSpawner>(List<TSpawner> source, int count) where TSpawner : Spawner
    {
        List<TSpawner> pool = new List<TSpawner>(source);

        for (int i = 0; i < pool.Count; i++)
        {
            int randomIndex = Random.Range(i, pool.Count);
            (pool[i], pool[randomIndex]) = (pool[randomIndex], pool[i]);
        }

        if (count < pool.Count)
            pool.RemoveRange(count, pool.Count - count);

        return pool;
    }

}

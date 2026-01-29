using System.Collections.Generic;
using UnityEngine;

public class RoomGenerator : Singleton<RoomGenerator>
{
    [Header("Room Size (cells)")]
    public int minWidth = 18;
    public int maxWidth = 34;
    public int minHeight = 12;
    public int maxHeight = 26;

    [Header("Room Area Constraint (cells^2)")]
    public int minArea = 220;
    public int maxArea = 650;

    [Header("Cell Metrics")]
    public float cellSize = 1f;

    [Header("Wall Placement")]
    public GameObject wallPrefab;
    public float wallSegmentLength = 1f; // world units along its long axis
    public float wallThickness = 0.2f;   // used for internal offsets / spawn inset
    public bool autoDeriveWallMetrics = true;

    [Header("Wall Segment Constraints")]
    public bool constrainRoomToWallSegments = true;
    public int minWallSegmentsX = 8;
    public int maxWallSegmentsX = 12;
    public int minWallSegmentsY = 8;
    public int maxWallSegmentsY = 12;

    [Header("Exit / Spawn")]
    public GameObject exitPrefab;
    public Transform playerSpawnMarkerPrefab; // optional

    [Header("Difficulty")]
    public DungeonDifficultyConfig difficultyConfig;

    [Header("Enemies")]
    public GameObject squareEnemyPrefab;
    public GameObject triangleEnemyPrefab;
    public GameObject starEnemyPrefab;

    [Header("Blops")]
    public GameObject blopPrefab;

    [Header("Collision & Layers")]
    public LayerMask blockedMask;     // set to Walls|Obstacles (optionally Enemies)
    public LayerMask obstacleMask;    // set to Obstacles (for obstacle-obstacle checks)
    public LayerMask wallMask;        // set to Walls

    [Header("Interior Obstacles")]
    public bool spawnInteriorObstacles = true;
    public GameObject[] obstaclePrefabs;
    public int obstacleMin = 0;
    public int obstacleMax = 6;

    [Tooltip("Try this many samples per obstacle before giving up.")]
    public int obstaclePlacementTries = 40;

    [Header("Spacing / Safety")]
    public float wallInset = 0.6f;          // keeps things away from perimeter
    public float spawnSafeRadius = 3f;      // no spawns around player start
    public float exitSafeRadius = 3f;       // no spawns around exit

    [Header("Spawn collision radii")]
    public float enemySpawnRadius = 0.45f;  // OverlapCircle radius for enemies
    public float blopSpawnRadius = 0.20f;   // OverlapCircle radius for blops

    [Header("Optional: keep enemies apart")]
    public bool avoidEnemyOverlap = true;
    public LayerMask enemyMask;             // set to Enemies if avoidEnemyOverlap

    [Header("Hierarchy")]
    public Transform generatedRoot;

    public Vector2 PlayerSpawnPosition { get; private set; }
    public Vector2 ExitPosition { get; private set; }
    public Bounds RoomBounds { get; private set; }

    private readonly List<GameObject> _spawned = new();
    private readonly List<SpawnReservation> _spawnReservations = new();

    private bool _wallMetricsInitialized;
    private float _derivedWallSegmentLength;
    private float _derivedWallThickness;
    private Vector2Int _activeWallSegmentCounts = Vector2Int.zero;
    private Vector2 _roomSizeWorld = Vector2.zero;

    public void Generate(int dungeonIndex, int maxDungeon)
    {
        ClearGenerated();

        _activeWallSegmentCounts = Vector2Int.zero;
        _roomSizeWorld = Vector2.zero;
        EnsureWallMetrics();

        if (generatedRoot == null)
        {
            generatedRoot = transform;
        }

        // 1) room size
        int width, height;
        PickRoomSize(out width, out height);

        // 2) bounds in XY
        Vector2 center = transform.position;
        float w = ResolveRoomDimension(_roomSizeWorld.x, width);
        float h = ResolveRoomDimension(_roomSizeWorld.y, height);
        RoomBounds = new Bounds(new Vector3(center.x, center.y, 0f), new Vector3(w, h, 0f));

        // 3) perimeter walls
        BuildPerimeterWalls(width, height);

        // 4) spawn + exit opposite sides
        PickSpawnAndExit(width, height);

        // 5) instantiate exit + spawn marker
        SpawnExitAndMarker();

        // 6) obstacles (collision safe)
        if (spawnInteriorObstacles && obstaclePrefabs != null && obstaclePrefabs.Length > 0)
            SpawnObstacles(width, height);

        // 7) enemies + blops
        SpawnEnemies(dungeonIndex);
        SpawnBlops(dungeonIndex);
    }

    private void PickRoomSize(out int width, out int height)
    {
        _roomSizeWorld = Vector2.zero;
        _activeWallSegmentCounts = Vector2Int.zero;

        if (constrainRoomToWallSegments && TryPickRoomSizeFromSegments(out width, out height))
        {
            return;
        }

        for (int i = 0; i < 50; i++)
        {
            int w = Random.Range(minWidth, maxWidth + 1);
            int h = Random.Range(minHeight, maxHeight + 1);
            int area = w * h;
            if (area >= minArea && area <= maxArea)
            {
                width = w;
                height = h;
                _roomSizeWorld = new Vector2(width * cellSize, height * cellSize);
                return;
            }
        }

        width = Mathf.Clamp(minWidth, 1, maxWidth);
        height = Mathf.Clamp(Mathf.RoundToInt((float)minArea / width), minHeight, maxHeight);
        _roomSizeWorld = new Vector2(width * cellSize, height * cellSize);
    }

    private bool TryPickRoomSizeFromSegments(out int width, out int height)
    {
        float segmentLength = GetEffectiveWallSegmentLength();
        width = 0;
        height = 0;

        if (segmentLength <= 0.0001f)
        {
            return false;
        }

        int minSegX = Mathf.Max(1, Mathf.Min(minWallSegmentsX, maxWallSegmentsX));
        int maxSegX = Mathf.Max(minSegX, Mathf.Max(minWallSegmentsX, maxWallSegmentsX));
        int minSegY = Mathf.Max(1, Mathf.Min(minWallSegmentsY, maxWallSegmentsY));
        int maxSegY = Mathf.Max(minSegY, Mathf.Max(minWallSegmentsY, maxWallSegmentsY));

        Vector2Int bestSegments = new Vector2Int(minSegX, minSegY);
        Vector2 bestWorldSize = new Vector2(bestSegments.x * segmentLength, bestSegments.y * segmentLength);
        int bestWidth = Mathf.Max(1, Mathf.RoundToInt(bestWorldSize.x / cellSize));
        int bestHeight = Mathf.Max(1, Mathf.RoundToInt(bestWorldSize.y / cellSize));
        int bestPenalty = int.MaxValue;

        for (int i = 0; i < 60; i++)
        {
            int segX = Random.Range(minSegX, maxSegX + 1);
            int segY = Random.Range(minSegY, maxSegY + 1);

            Vector2 worldSize = new Vector2(segX * segmentLength, segY * segmentLength);
            int widthCells = Mathf.Max(1, Mathf.RoundToInt(worldSize.x / cellSize));
            int heightCells = Mathf.Max(1, Mathf.RoundToInt(worldSize.y / cellSize));
            int area = widthCells * heightCells;
            int penalty = GetAreaPenalty(area);

            if (penalty < bestPenalty)
            {
                bestPenalty = penalty;
                bestSegments = new Vector2Int(segX, segY);
                bestWorldSize = worldSize;
                bestWidth = widthCells;
                bestHeight = heightCells;
            }

            if (penalty == 0)
            {
                break;
            }
        }

        width = bestWidth;
        height = bestHeight;
        _activeWallSegmentCounts = bestSegments;
        _roomSizeWorld = bestWorldSize;
        return true;
    }

    private int GetAreaPenalty(int area)
    {
        if (area < minArea) return minArea - area;
        if (area > maxArea) return area - maxArea;
        return 0;
    }

    private void BuildPerimeterWalls(int width, int height)
    {
        float widthWorld = ResolveRoomDimension(_roomSizeWorld.x, width);
        float heightWorld = ResolveRoomDimension(_roomSizeWorld.y, height);
        float halfW = widthWorld * 0.5f;
        float halfH = heightWorld * 0.5f;

        float segmentLength = Mathf.Max(GetEffectiveWallSegmentLength(), 0.0001f);
        int countX = (_activeWallSegmentCounts.x > 0) ? _activeWallSegmentCounts.x : Mathf.Max(1, Mathf.RoundToInt(widthWorld / segmentLength));
        int countY = (_activeWallSegmentCounts.y > 0) ? _activeWallSegmentCounts.y : Mathf.Max(1, Mathf.RoundToInt(heightWorld / segmentLength));

        // Top/bottom (horizontal)
        for (int i = 0; i < countX; i++)
        {
            float x = -halfW + (i + 0.5f) * segmentLength;
            SpawnWall(new Vector2(x, +halfH), Quaternion.identity);
            SpawnWall(new Vector2(x, -halfH), Quaternion.identity);
        }

        // Left/right (vertical) rotate 90 around Z
        Quaternion rot = Quaternion.Euler(0f, 0f, 90f);
        for (int i = 0; i < countY; i++)
        {
            float y = -halfH + (i + 0.5f) * segmentLength;
            SpawnWall(new Vector2(+halfW, y), rot);
            SpawnWall(new Vector2(-halfW, y), rot);
        }
    }

    private void PickSpawnAndExit(int width, int height)
    {
        float halfW = ResolveRoomDimension(_roomSizeWorld.x, width) * 0.5f;
        float halfH = ResolveRoomDimension(_roomSizeWorld.y, height) * 0.5f;

        bool horizontalOpposites = Random.value < 0.5f;

        if (horizontalOpposites)
        {
            bool spawnLeft = Random.value < 0.5f;

            float ySpawn = Random.Range(-halfH + wallInset, +halfH - wallInset);
            float yExit = Random.Range(-halfH + wallInset, +halfH - wallInset);

            float xSpawn = spawnLeft ? (-halfW + wallInset) : (+halfW - wallInset);
            float xExit = spawnLeft ? (+halfW - wallInset) : (-halfW + wallInset);

            PlayerSpawnPosition = (Vector2)transform.position + new Vector2(xSpawn, ySpawn);
            ExitPosition = (Vector2)transform.position + new Vector2(xExit, yExit);
        }
        else
        {
            bool spawnBottom = Random.value < 0.5f;

            float xSpawn = Random.Range(-halfW + wallInset, +halfW - wallInset);
            float xExit = Random.Range(-halfW + wallInset, +halfW - wallInset);

            float ySpawn = spawnBottom ? (-halfH + wallInset) : (+halfH - wallInset);
            float yExit = spawnBottom ? (+halfH - wallInset) : (-halfH + wallInset);

            PlayerSpawnPosition = (Vector2)transform.position + new Vector2(xSpawn, ySpawn);
            ExitPosition = (Vector2)transform.position + new Vector2(xExit, yExit);
        }
    }

    private void SpawnExitAndMarker()
    {
        if (exitPrefab != null)
        {
            var exit = Instantiate(exitPrefab, ExitPosition, Quaternion.identity, generatedRoot);
            _spawned.Add(exit);
        }

        if (playerSpawnMarkerPrefab != null)
        {
            var marker = Instantiate(playerSpawnMarkerPrefab, PlayerSpawnPosition, Quaternion.identity, generatedRoot);
            _spawned.Add(marker.gameObject);
        }
    }

    private void SpawnObstacles(int width, int height)
    {
        int count = Random.Range(obstacleMin, obstacleMax + 1);

        for (int i = 0; i < count; i++)
        {
            var prefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)];

            // We’ll use the prefab collider bounds as placement size (best practice)
            if (!TryGetPrefabColliderAABB(prefab, out Vector2 halfExtents))
                halfExtents = Vector2.one * 0.5f;

            for (int t = 0; t < obstaclePlacementTries; t++)
            {
                Vector2 pos = SamplePointInsideBounds(halfExtents, wallInset);

                // avoid start/exit
                if (Vector2.Distance(pos, PlayerSpawnPosition) < spawnSafeRadius) continue;
                if (Vector2.Distance(pos, ExitPosition) < exitSafeRadius) continue;

                // collision safe: no overlap with walls/other obstacles
                if (Physics2D.OverlapBox(pos, halfExtents * 2f, 0f, blockedMask) != null) continue;

                var rot = Quaternion.Euler(0f, 0f, 90f * Random.Range(0, 4));
                var ob = Instantiate(prefab, pos, rot, generatedRoot);
                _spawned.Add(ob);
                RegisterSpawnReservation(pos, Mathf.Max(halfExtents.x, halfExtents.y));
                break;
            }
        }
    }

    private void SpawnEnemies(int dungeonIndex)
    {
        var rule = difficultyConfig.GetRule(dungeonIndex);

        int enemyCount = Random.Range(rule.minEnemies, rule.maxEnemies + 1);
        int starsSpawned = 0;

        for (int i = 0; i < enemyCount; i++)
        {
            var prefab = PickEnemyPrefab(rule, ref starsSpawned);
            if (prefab == null) continue;

            if (TryFindSpawnPointCircle(enemySpawnRadius, out Vector2 pos))
            {
                var enemy = Instantiate(prefab, pos, Quaternion.identity, generatedRoot);
                _spawned.Add(enemy);
                RegisterSpawnReservation(pos, enemySpawnRadius);
            }
        }
    }

    private void SpawnBlops(int dungeonIndex)
    {
        if (blopPrefab == null) return;

        var rule = difficultyConfig.GetRule(dungeonIndex);
        int blopCount = Random.Range(rule.minBlops, rule.maxBlops + 1);

        for (int i = 0; i < blopCount; i++)
        {
            if (TryFindSpawnPointCircle(blopSpawnRadius, out Vector2 pos))
            {
                var blop = Instantiate(blopPrefab, pos, Quaternion.identity, generatedRoot);
                _spawned.Add(blop);
                RegisterSpawnReservation(pos, blopSpawnRadius);
            }
        }
    }

    private GameObject PickEnemyPrefab(DungeonDifficultyConfig.DungeonRule rule, ref int starsSpawned)
    {
        float sW = rule.squareWeight;
        float tW = rule.triangleWeight;
        float starW = (starsSpawned >= rule.maxStars) ? 0f : rule.starWeight;

        float total = sW + tW + starW;
        if (total <= 0.0001f) return squareEnemyPrefab;

        float roll = Random.value * total;

        if (roll < sW) return squareEnemyPrefab;
        roll -= sW;

        if (roll < tW) return triangleEnemyPrefab;

        starsSpawned++;
        return starEnemyPrefab;
    }

    // --- Spawn sampling / collision checks ---

    private bool TryFindSpawnPointCircle(float radius, out Vector2 pos)
    {
        LayerMask staticBlockMask = blockedMask | obstacleMask | wallMask;

        // Rejection sampling
        for (int tries = 0; tries < 80; tries++)
        {
            Vector2 candidate = SamplePointInsideBounds(Vector2.one * radius, wallInset);

            if (Vector2.Distance(candidate, PlayerSpawnPosition) < spawnSafeRadius) continue;
            if (Vector2.Distance(candidate, ExitPosition) < exitSafeRadius) continue;

            // blocked by walls/obstacles?
            if (Physics2D.OverlapCircle(candidate, radius, staticBlockMask) != null) continue;
            if (IsSpawnReserved(candidate, radius)) continue;

            // optionally avoid enemies overlapping each other
            if (avoidEnemyOverlap && enemyMask.value != 0)
            {
                if (Physics2D.OverlapCircle(candidate, radius, enemyMask) != null) continue;
            }

            pos = candidate;
            return true;
        }

        pos = default;
        return false;
    }

    private Vector2 SamplePointInsideBounds(Vector2 halfExtents, float inset)
    {
        float minX = RoomBounds.min.x + inset + halfExtents.x;
        float maxX = RoomBounds.max.x - inset - halfExtents.x;
        float minY = RoomBounds.min.y + inset + halfExtents.y;
        float maxY = RoomBounds.max.y - inset - halfExtents.y;

        float x = Random.Range(minX, maxX);
        float y = Random.Range(minY, maxY);
        return new Vector2(x, y);
    }

    private bool TryGetPrefabColliderAABB(GameObject prefab, out Vector2 halfExtents)
    {
        halfExtents = default;

        Bounds bounds = default;
        bool found = false;

        var col = prefab.GetComponent<Collider2D>();
        if (col != null)
        {
            bounds = col.bounds;
            found = bounds.size.x > 0.0001f && bounds.size.y > 0.0001f;
        }

        if (!found)
        {
            var renderer = prefab.GetComponent<Renderer>();
            if (renderer != null)
            {
                bounds = renderer.bounds;
                found = bounds.size.x > 0.0001f && bounds.size.y > 0.0001f;
            }
        }

        if (!found) return false;

        halfExtents = new Vector2(bounds.extents.x, bounds.extents.y);
        return true;
    }

    private void SpawnWall(Vector2 localOffset, Quaternion rotation)
    {
        if (wallPrefab == null) return;

        Vector2 pos = (Vector2)transform.position + localOffset;
        var wall = Instantiate(wallPrefab, pos, rotation, generatedRoot);
        _spawned.Add(wall);
    }


    private float ResolveRoomDimension(float storedValue, int fallbackCellCount)
    {
        if (storedValue > 0.0001f) return storedValue;
        return Mathf.Max(1, fallbackCellCount) * cellSize;
    }

    private void RegisterSpawnReservation(Vector2 position, float radius)
    {
        if (radius <= 0f) return;
        _spawnReservations.Add(new SpawnReservation { position = position, radius = radius });
    }

    private bool IsSpawnReserved(Vector2 position, float radius)
    {
        float minDistance = Mathf.Max(0.0001f, radius);
        for (int i = 0; i < _spawnReservations.Count; i++)
        {
            var reservation = _spawnReservations[i];
            float required = reservation.radius + minDistance;
            if (Vector2.Distance(position, reservation.position) < required)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureWallMetrics()
    {
        if (_wallMetricsInitialized) return;
        _wallMetricsInitialized = true;

        if (!autoDeriveWallMetrics || wallPrefab == null) return;

        if (TryGetPrefabColliderAABB(wallPrefab, out Vector2 halfExtents))
        {
            float length = Mathf.Max(halfExtents.x, halfExtents.y) * 2f;
            float thickness = Mathf.Min(halfExtents.x, halfExtents.y) * 2f;

            if (length > 0.0001f)
            {
                _derivedWallSegmentLength = length;
                wallSegmentLength = length;
            }

            if (thickness > 0.0001f)
            {
                _derivedWallThickness = thickness;
                wallThickness = thickness;
            }
        }
    }

    private float GetEffectiveWallSegmentLength()
    {
        if (_derivedWallSegmentLength > 0.0001f) return _derivedWallSegmentLength;
        if (wallSegmentLength > 0.0001f) return wallSegmentLength;
        return Mathf.Max(0.1f, cellSize);
    }

    private struct SpawnReservation
    {
        public Vector2 position;
        public float radius;
    }

    private void ClearGenerated()
    {
        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i] != null) Destroy(_spawned[i]);

        _spawned.Clear();
        _spawnReservations.Clear();

        if (generatedRoot != null)
        {
            for (int i = generatedRoot.childCount - 1; i >= 0; i--)
                Destroy(generatedRoot.GetChild(i).gameObject);
        }
    }

    // Optional: helpful debug gizmos
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(RoomBounds.center, RoomBounds.size);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(PlayerSpawnPosition, spawnSafeRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(ExitPosition, exitSafeRadius);
    }
}

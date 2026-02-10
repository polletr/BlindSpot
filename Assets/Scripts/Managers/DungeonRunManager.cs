using System.Collections.Generic;
using UnityEngine;

public class DungeonRunManager : Singleton<DungeonRunManager>
{
    [Header("Progression")]
    [Min(1)] public int dungeonIndex = 1;
    [Min(1)] public int maxDungeon = 10;

    [Header("Difficulty")]
    public DungeonDifficultyConfig difficultyConfig;

    [Header("Level List")]
    public List<LevelController> levelPrefabs = new List<LevelController>();

    private LevelController currentLevel;

    public LevelController CurrentLevel => currentLevel;

    public void GenerateDungeon()
    {
        if (difficultyConfig == null || levelPrefabs.Count == 0)
        {
            Debug.LogWarning("[DungeonRunManager] Missing difficulty config or level prefabs.");
            return;
        }

        if (currentLevel != null)
        {
            Destroy(currentLevel.gameObject);
            currentLevel = null;
        }

        DungeonDifficultyConfig.DungeonRule rule = difficultyConfig.GetRule(dungeonIndex);

        //Improve this logic later, for now just a rondomizer
        int selectLevel = Random.Range(0, levelPrefabs.Count);

        currentLevel = Instantiate(levelPrefabs[selectLevel], transform);
        currentLevel.Initialize(rule);
    }

    public void GoToNextDungeon()
    {
        dungeonIndex = Mathf.Min(dungeonIndex + 1, maxDungeon);
        GenerateDungeon();
    }

    public bool TryGetCurrentPlayerSpawnPosition(out Vector2 spawnPosition)
    {
        spawnPosition = default;

        if (currentLevel == null)
            return false;

        return currentLevel.TryGetPlayerSpawnPoint(out spawnPosition);
    }
}


using UnityEngine;

public class DungeonRunManager : Singleton<DungeonRunManager>
{
    [Header("Progression")]
    [Min(1)] public int dungeonIndex = 1;
    [Min(1)] public int maxDungeon = 10;

    [Header("Seed (optional)")]
    public bool useFixedSeed = false;
    public int fixedSeed = 12345;

    private void Start()
    {
        GenerateCurrentDungeon();
    }

    public void GenerateCurrentDungeon()
    {
        if (useFixedSeed) Random.InitState(fixedSeed + dungeonIndex);
        else Random.InitState(System.Environment.TickCount + dungeonIndex);

        RoomGenerator.Instance.Generate(dungeonIndex, maxDungeon);
    }

    public void GoToNextDungeon()
    {
        dungeonIndex = Mathf.Min(dungeonIndex + 1, maxDungeon);
        GenerateCurrentDungeon();
    }
}

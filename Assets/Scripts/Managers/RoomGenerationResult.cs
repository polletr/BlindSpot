using UnityEngine;

/// <summary>
/// Lightweight data package produced once the room generator has finished building the arena.
/// </summary>
public readonly struct RoomGenerationResult
{
    public RoomGenerationResult(Vector2 playerSpawnPosition, Vector2 exitPosition, Bounds roomBounds, int dungeonIndex, int maxDungeon)
    {
        PlayerSpawnPosition = playerSpawnPosition;
        ExitPosition = exitPosition;
        RoomBounds = roomBounds;
        DungeonIndex = dungeonIndex;
        MaxDungeon = maxDungeon;
    }

    public Vector2 PlayerSpawnPosition { get; }
    public Vector2 ExitPosition { get; }
    public Bounds RoomBounds { get; }
    public int DungeonIndex { get; }
    public int MaxDungeon { get; }
}

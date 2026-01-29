using UnityEngine;

[CreateAssetMenu(menuName = "SignalLost/Dungeon Difficulty Config")]
public class DungeonDifficultyConfig : ScriptableObject
{
    [System.Serializable]
    public struct DungeonRule
    {
        public int dungeonIndex;

        [Header("Enemies")]
        public int minEnemies;
        public int maxEnemies;

        [Range(0, 1)] public float squareWeight;
        [Range(0, 1)] public float triangleWeight;
        [Range(0, 1)] public float starWeight;
        public int maxStars;

        [Header("Blops (currency)")]
        public int minBlops;
        public int maxBlops;
    }

    public DungeonRule[] rules;

    public DungeonRule GetRule(int dungeonIndex)
    {
        DungeonRule best = rules[0];
        int bestIdx = -1;

        foreach (var r in rules)
        {
            if (r.dungeonIndex <= dungeonIndex && r.dungeonIndex > bestIdx)
            {
                best = r;
                bestIdx = r.dungeonIndex;
            }
        }
        return best;
    }
}

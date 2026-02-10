using UnityEngine;

public class DropSpawner : Spawner
{
    [SerializeField][Range(0f, 1f)] private float spawnChance = 1f;

    public override void SpawnObject(GameObject objToSpawn)
    {
        if (Random.Range(0f, 1f) > spawnChance) return;

        base.SpawnObject(objToSpawn);
    }

}

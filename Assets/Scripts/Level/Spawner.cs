using UnityEngine;

public abstract class Spawner : MonoBehaviour
{
    [SerializeField] protected GameObject objInstance;

    public virtual void SpawnObject(GameObject objToSpawn)
    {
        if (objToSpawn == null)
            return;

        objInstance = Instantiate(objToSpawn, transform.position, transform.rotation, transform);
    }

    public virtual void DespawnObject()
    {
        if (objInstance != null)
            Destroy(objInstance);

        objInstance = null;
    }

}

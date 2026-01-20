using System.Collections.Generic;
using UnityEngine;

public class CurrencyDropPool : MonoBehaviour
{
    [System.Serializable]
    public class PoolEntry
    {
        public string key;
        public CurrencyDrop prefab;
        public int prewarmCount = 64;
    }

    [SerializeField] private PoolEntry[] entries;

    // One queue per key
    private readonly Dictionary<string, Queue<CurrencyDrop>> _pools = new();
    private readonly Dictionary<string, CurrencyDrop> _prefabs = new();

    private void Awake()
    {
        BuildPools();
    }

    private void BuildPools()
    {
        _pools.Clear();
        _prefabs.Clear();

        foreach (var e in entries)
        {
            if (string.IsNullOrWhiteSpace(e.key) || e.prefab == null)
                continue;

            _prefabs[e.key] = e.prefab;
            var q = new Queue<CurrencyDrop>(Mathf.Max(1, e.prewarmCount));
            _pools[e.key] = q;

            for (int i = 0; i < e.prewarmCount; i++)
            {
                var inst = CreateInstance(e.key, e.prefab);
                ReturnInternal(e.key, inst);
            }
        }
    }

    private CurrencyDrop CreateInstance(string key, CurrencyDrop prefab)
    {
        var inst = Instantiate(prefab, transform);
        inst.gameObject.SetActive(false);
        inst.AssignPool(this, key);
        return inst;
    }

    /// <summary>Spawn a drop by key, at a position/rotation, optionally setting amount.</summary>
    public CurrencyDrop Spawn(string key, Vector3 position, Quaternion rotation, int amountOverride = -1)
    {
        if (!_pools.TryGetValue(key, out var q))
        {
            Debug.LogError($"DropsPool: Unknown key '{key}'.");
            return null;
        }

        CurrencyDrop drop = (q.Count > 0) ? q.Dequeue() : CreateInstance(key, _prefabs[key]);

        var t = drop.transform;
        t.SetPositionAndRotation(position, rotation);

        if (amountOverride > 0)
            drop.amount = amountOverride;

        drop.gameObject.SetActive(true);
        drop.OnSpawned(); // reset internal state
        return drop;
    }

    /// <summary>Return a drop to the pool (safe to call multiple times).</summary>
    public void Return(CurrencyDrop drop)
    {
        if (drop == null) return;
        ReturnInternal(drop.PoolKey, drop);
    }

    private void ReturnInternal(string key, CurrencyDrop drop)
    {
        drop.OnDespawned();
        drop.gameObject.SetActive(false);
        drop.transform.SetParent(transform, false);

        if (_pools.TryGetValue(key, out var q))
            q.Enqueue(drop);
        else
            Destroy(drop.gameObject); // fallback if misconfigured
    }
}

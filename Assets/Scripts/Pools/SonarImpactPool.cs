using System.Collections.Generic;
using UnityEngine;

public class SonarImpactPool : MonoBehaviour
{
    public SonarImpactStreak prefab;
    public int preload = 40;

    readonly Queue<SonarImpactStreak> pool = new();

    void Awake()
    {
        for (int i = 0; i < preload; i++)
            pool.Enqueue(Create());
    }

    SonarImpactStreak Create()
    {
        var s = Instantiate(prefab, transform);
        s.gameObject.SetActive(false);
        s.OnFinished += Return;
        return s;
    }

    public SonarImpactStreak Get()
    {
        if (pool.Count == 0) pool.Enqueue(Create());
        var s = pool.Dequeue();
        s.gameObject.SetActive(true);
        return s;
    }

    void Return(SonarImpactStreak s)
    {
        s.gameObject.SetActive(false);
        pool.Enqueue(s);
    }
}

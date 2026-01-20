using System.Collections.Generic;
using UnityEngine;

public class SonarConeVisualPool : MonoBehaviour
{
    public SonarConeVisual prefab;
    public int preload = 1;

    readonly Queue<SonarConeVisual> pool = new();

    void Awake()
    {
        for (int i = 0; i < preload; i++)
            pool.Enqueue(Create());
    }

    SonarConeVisual Create()
    {
        var v = Instantiate(prefab, transform);
        v.gameObject.SetActive(false);
        v.OnFinished += Return;
        return v;
    }

    public SonarConeVisual Get()
    {
        if (pool.Count == 0) pool.Enqueue(Create());
        var v = pool.Dequeue();
        v.gameObject.SetActive(true);
        return v;
    }

    void Return(SonarConeVisual v)
    {
        v.gameObject.SetActive(false);
        pool.Enqueue(v);
    }
}

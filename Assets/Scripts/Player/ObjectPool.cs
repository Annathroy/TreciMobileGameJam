using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : MonoBehaviour
{
    [SerializeField] GameObject prefab;
    [SerializeField] int prewarm = 10;
    [SerializeField] Transform container;

    readonly Queue<GameObject> pool = new Queue<GameObject>();

    void Awake()
    {
        if (!container)
        {
            var go = new GameObject($"[Pool] {prefab?.name ?? "Prefab"}");
            go.transform.SetParent(transform, false);
            container = go.transform;
        }

        for (int i = 0; i < prewarm; i++)
        {
            var inst = Instantiate(prefab, container);
            inst.SetActive(false);
            pool.Enqueue(inst);
        }
    }

    public GameObject GetObject()
    {
        while (pool.Count > 0)
        {
            var go = pool.Dequeue();
            if (go == null) continue;
            go.SetActive(true);
            return go;
        }

        var fresh = Instantiate(prefab, container);
        fresh.SetActive(true);
        return fresh;
    }

    public void Return(GameObject go)
    {
        if (!go) return;
        go.SetActive(false);
        go.transform.SetParent(container, false);
        pool.Enqueue(go);
    }
}

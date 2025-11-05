using System.Collections.Generic;
using UnityEngine;

public class SharedFishPool : MonoBehaviour
{
    public static SharedFishPool Instance { get; private set; }

    private class Bucket
    {
        public readonly Queue<GameObject> q = new Queue<GameObject>();
        public readonly Transform parent;
        public Bucket(string name, Transform root)
        {
            parent = new GameObject(name).transform;
            parent.SetParent(root, false);
        }
    }

    // prefab => bucket
    private readonly Dictionary<GameObject, Bucket> buckets = new Dictionary<GameObject, Bucket>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Prewarm(GameObject prefab, int count)
    {
        var b = GetOrCreateBucket(prefab);
        for (int i = 0; i < count; i++)
        {
            var go = CreateInstance(prefab, b.parent);
            go.SetActive(false);
            b.q.Enqueue(go);
        }
    }

    public GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        var b = GetOrCreateBucket(prefab);
        GameObject go = b.q.Count > 0 ? b.q.Dequeue() : CreateInstance(prefab, b.parent);
        go.transform.SetPositionAndRotation(pos, rot);
        go.SetActive(true);
        return go;
    }

    public void Despawn(GameObject instance, GameObject prefabHint = null)
    {
        // Find PooledObject to know its source prefab
        var po = instance.GetComponent<PooledObject>();
        if (po == null || po.SourcePrefab == null)
        {
            // last resort: use hint
            if (prefabHint == null) { instance.SetActive(false); Destroy(instance); return; }
            EnsureInBucket(prefabHint, instance);
            return;
        }
        EnsureInBucket(po.SourcePrefab, instance);
    }

    private void EnsureInBucket(GameObject prefab, GameObject instance)
    {
        var b = GetOrCreateBucket(prefab);
        instance.SetActive(false);
        instance.transform.SetParent(b.parent, false);
        b.q.Enqueue(instance);
    }

    private Bucket GetOrCreateBucket(GameObject prefab)
    {
        if (!buckets.TryGetValue(prefab, out var b))
        {
            b = new Bucket($"[Pool] {prefab.name}", transform);
            buckets[prefab] = b;
        }
        return b;
    }

    private GameObject CreateInstance(GameObject prefab, Transform parent)
    {
        var go = Instantiate(prefab, parent);
        var po = go.GetComponent<PooledObject>();
        if (po == null) po = go.AddComponent<PooledObject>();
        po.SourcePrefab = prefab;
        return go;
    }
}

// Attached automatically to pooled instances
public class PooledObject : MonoBehaviour
{
    public GameObject SourcePrefab; // set by pool
}

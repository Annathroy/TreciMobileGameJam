using System.Collections.Generic;
using UnityEngine;

public class SimplePool : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private int prewarm = 32;
    [SerializeField] private bool parentUnderThis = false; // keep false to avoid inactive-parent masking

    private readonly Queue<GameObject> pool = new Queue<GameObject>();
    private Transform activeParent; // optional active container

    void Awake()
    {
        // Create an always-active parent (optional, for hierarchy tidiness)
        if (!parentUnderThis)
        {
            var root = GameObject.Find("__PoolRuntimeRoot") ?? new GameObject("__PoolRuntimeRoot");
            activeParent = root.transform;
        }
        else
        {
            // WARN if our own GameObject is inactive (will mask children)
            if (!gameObject.activeInHierarchy)
                Debug.LogWarning($"{name}: Pool GameObject is inactive; children would be inactiveInHierarchy.");
        }

        for (int i = 0; i < prewarm; i++)
        {
            var go = Instantiate(prefab, ParentForInstantiate());
            go.SetActive(false);
            pool.Enqueue(go);
        }
    }

    public GameObject Get()
    {
        GameObject go = pool.Count > 0 ? pool.Dequeue() : Instantiate(prefab, ParentForInstantiate());
        // Reparent to active root if needed
        if (!parentUnderThis && go.transform.parent != activeParent)
            go.transform.SetParent(activeParent, false);

        go.SetActive(true); // activeSelf = true
        return go;
    }

    public void Return(GameObject go)
    {
        go.SetActive(false);
        if (!parentUnderThis && go.transform.parent != activeParent)
            go.transform.SetParent(activeParent, false);
        pool.Enqueue(go);
    }

    private Transform ParentForInstantiate()
    {
        if (parentUnderThis) return transform;
        if (activeParent == null)
        {
            var root = GameObject.Find("__PoolRuntimeRoot") ?? new GameObject("__PoolRuntimeRoot");
            activeParent = root.transform;
        }
        return activeParent;
    }
}

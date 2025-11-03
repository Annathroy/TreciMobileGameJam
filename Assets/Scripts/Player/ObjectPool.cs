using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private int poolSize = 10;

    private Queue<GameObject> pool = new Queue<GameObject>();

    private void Awake()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(prefab);
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
    }

    public GameObject GetObject()
    {
        // Always reuse the oldest object in the pool
        GameObject obj = pool.Dequeue();
        obj.SetActive(true);
        pool.Enqueue(obj); // Immediately re-add it to the queue
        return obj;
    }

    public void ReturnObject(GameObject obj)
    {
        // Ensure the object is deactivated but remains in the queue
        obj.SetActive(false);
    }
}

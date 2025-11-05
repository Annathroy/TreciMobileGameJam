using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class SideSpawnerBase : MonoBehaviour
{
    public enum Side { Left, Right }

    [Header("Side (fixed by child class)")]
    [SerializeField, Tooltip("Don’t edit in children; it’s set in Awake()")]
    protected Side side = Side.Left;

    [Header("Fish Prefabs (shared pool)")]
    public List<GameObject> fishPrefabs = new List<GameObject>();

    [Header("Spawn")]
    public float spawnInterval = 0.25f;
    public int spawnPerTick = 1;
    public int prewarmEach = 5;
    public bool autoStart = true;

    [Header("Speed")]
    public float speedMin = 4f;
    public float speedMax = 7f;

    protected Coroutine loop;

    protected virtual void Awake()
    {
        // children must set 'side' in their Awake() BEFORE base.Awake() is called
    }

    protected virtual void Start()
    {
        // Ensure pool exists
        if (SharedFishPool.Instance == null)
        {
            var go = new GameObject("SharedFishPool");
            go.AddComponent<SharedFishPool>();
        }

        foreach (var p in fishPrefabs)
            if (p) SharedFishPool.Instance.Prewarm(p, Mathf.Max(0, prewarmEach));
    }
    private void OnEnable()
    {
        if (autoStart)
            StartSpawning();
    }

    private void OnDisable()
    {
        StopSpawning();
    }
    public void StartSpawning()
    {
        if (loop == null) loop = StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        if (loop != null) { StopCoroutine(loop); loop = null; }
    }

    private IEnumerator SpawnLoop()
    {
        var wait = new WaitForSeconds(spawnInterval);
        while (true)
        {
            for (int i = 0; i < spawnPerTick; i++) SpawnOne();
            yield return wait;
        }
    }

    void SpawnOne()
    {
        if (fishPrefabs.Count == 0) return;
        var prefab = fishPrefabs[Random.Range(0, fishPrefabs.Count)];
        if (!prefab) return;

        // Base direction: from side toward the other side (world +X or -X)
        Vector3 dir = (side == Side.Left) ? Vector3.right : Vector3.left;

        var go = SharedFishPool.Instance.Spawn(prefab, transform.position, Quaternion.identity);
        var mover = CreateAndConfigureMover(go);
        if (mover == null) return;

        float speed = Random.Range(speedMin, speedMax);
        mover.OnSpawned(transform.position, dir, speed);
    }

    /// <summary>Create + return a configured mover (pattern-specific).</summary>
    protected abstract BaseFishMover CreateAndConfigureMover(GameObject go);
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FishSpawner : MonoBehaviour
{
    [Header("Fish Prefabs (shared across spawners)")]
    [Tooltip("All spawners can reference the same list; pool is shared globally.")]
    public List<GameObject> fishPrefabs = new List<GameObject>();

    [Header("Spawning")]
    public float spawnInterval = 0.25f;
    public int spawnPerTick = 1;
    public bool autoStart = true;

    [Header("Prewarm")]
    public int prewarmEach = 5;

    [Header("Movement Defaults")]
    public float speedMin = 4f;
    public float speedMax = 7f;
    public float sineAmpMin = 0.3f;
    public float sineAmpMax = 1.2f;
    public float sineFreqMin = 1.5f;
    public float sineFreqMax = 3.5f;

    public enum Plane { XY, XZ }
    [Header("Space")]
    public Plane movementPlane = Plane.XY;

    Coroutine loop;

    private void Start()
    {
        // Ensure pool exists in scene
        if (SharedFishPool.Instance == null)
        {
            var go = new GameObject("SharedFishPool");
            go.AddComponent<SharedFishPool>();
        }

        // Prewarm
        foreach (var p in fishPrefabs)
            if (p) SharedFishPool.Instance.Prewarm(p, Mathf.Max(0, prewarmEach));

        if (autoStart) loop = StartCoroutine(SpawnLoop());
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

    private void SpawnOne()
    {
        if (fishPrefabs.Count == 0) return;

        var prefab = fishPrefabs[Random.Range(0, fishPrefabs.Count)];
        if (!prefab) return;

        // Pick a random outward direction on the selected plane
        Vector3 dir;
        Vector3 perp;
        if (movementPlane == Plane.XY)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f).normalized;
            perp = new Vector3(-dir.y, dir.x, 0f); // 90° CCW
        }
        else
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)).normalized;
            perp = new Vector3(-dir.z, 0f, dir.x);
        }

        var go = SharedFishPool.Instance.Spawn(prefab, transform.position, Quaternion.identity);

        var mover = go.GetComponent<FishMover>();
        if (!mover) mover = go.AddComponent<FishMover>();

        mover.OnSpawned(
            origin: transform.position,
            direction: dir,
            lateral: perp,
            speed: Random.Range(speedMin, speedMax),
            amp: Random.Range(sineAmpMin, sineAmpMax),
            freq: Random.Range(sineFreqMin, sineFreqMax),
            planeXY: movementPlane == Plane.XY
        );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.25f);
        // Draw a few spokes to visualize radial emission
        for (int i = 0; i < 12; i++)
        {
            float a = (Mathf.PI * 2f) * i / 12f;
            Vector3 r = movementPlane == Plane.XY
                ? new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f)
                : new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Gizmos.DrawLine(transform.position, transform.position + r * 1.5f);
        }
    }
}

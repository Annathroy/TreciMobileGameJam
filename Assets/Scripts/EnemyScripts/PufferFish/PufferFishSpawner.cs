using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Periodically spawns PufferFish with a cap on concurrent instances.
/// Supports SimplePool if provided; otherwise instantiates normally.
/// </summary>
[DisallowMultipleComponent]
public class PufferFishSpawner : MonoBehaviour
{
    [Header("Spawn Source (pick ONE)")]
    [SerializeField] private SimplePool pool;       // Optional: assign if you use pooling
    [SerializeField] private PufferFish prefab;     // Optional: assign if you don't use pooling

    [Header("Timing")]
    [SerializeField] private Vector2 spawnIntervalRange = new Vector2(3f, 7f); // seconds (min..max)
    [SerializeField] private float startDelay = 1f;
    [SerializeField] private bool playOnStart = true;

    [Header("Limits")]
    [SerializeField] private int maxConcurrent = 4; // max puffer fish alive at once
    [SerializeField] private int warmStartSpawns = 0; // spawn this many immediately on start (clamped by maxConcurrent)

    [Header("Difficulty Ramp (optional)")]
    [Tooltip("Every 'rampPeriod' seconds: reduce intervals by this factor (>= 0.1) and optionally raise the cap.")]
    [SerializeField] private bool enableRamp = false;
    [SerializeField] private float rampPeriod = 20f;
    [SerializeField, Range(0.1f, 1f)] private float intervalMultiplierPerRamp = 0.9f; // 0.9 => 10% faster
    [SerializeField] private int maxConcurrentIncrementPerRamp = 1;
    [SerializeField] private int maxConcurrentHardCap = 12;

    // ---- internals ----
    private readonly HashSet<SpawnHook> _alive = new HashSet<SpawnHook>();
    private Coroutine _loop;
    private Coroutine _ramp;

    void Start()
    {
        if (!pool && !prefab)
        {
            Debug.LogError("[PufferFishSpawner] Assign either a SimplePool OR a PufferFish prefab.");
            enabled = false;
            return;
        }

        if (playOnStart)
            StartSpawning();

        // optional warm start
        int initial = Mathf.Clamp(warmStartSpawns, 0, maxConcurrent);
        for (int i = 0; i < initial; i++) ForceSpawn();
    }

    public void StartSpawning()
    {
        if (_loop == null) _loop = StartCoroutine(SpawnLoop());
        if (enableRamp && _ramp == null) _ramp = StartCoroutine(RampLoop());
    }

    public void StopSpawning()
    {
        if (_loop != null) { StopCoroutine(_loop); _loop = null; }
        if (_ramp != null) { StopCoroutine(_ramp); _ramp = null; }
    }

    public int AliveCount => _alive.Count;

    /// <summary>Spawn one immediately if under cap.</summary>
    public void ForceSpawn()
    {
        if (_alive.Count >= maxConcurrent) return;

        GameObject go = null;
        if (pool)
        {
            go = pool.Get();
            if (!go)
            {
                Debug.LogWarning("[PufferFishSpawner] Pool returned null.");
                return;
            }
        }
        else
        {
            if (!prefab)
            {
                Debug.LogError("[PufferFishSpawner] No prefab assigned.");
                return;
            }
            go = Instantiate(prefab.gameObject);
        }

        // Make sure it has a hook to notify us when it goes away
        var hook = go.GetComponent<SpawnHook>();
        if (!hook) hook = go.AddComponent<SpawnHook>();
        hook.onGone -= OnChildGone; // avoid double-subscribe
        hook.onGone += OnChildGone;

        // Ensure active so PufferFish.OnEnable() runs and teleports off-screen
        if (!go.activeSelf) go.SetActive(true);

        _alive.Add(hook);
    }

    private IEnumerator SpawnLoop()
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        while (true)
        {
            // If we have room, spawn; otherwise wait a short tick
            if (_alive.Count < maxConcurrent)
                ForceSpawn();

            // Wait a random interval
            float wait = Random.Range(spawnIntervalRange.x, spawnIntervalRange.y);
            if (wait < 0.05f) wait = 0.05f;
            yield return new WaitForSeconds(wait);
        }
    }

    private IEnumerator RampLoop()
    {
        var wait = new WaitForSeconds(rampPeriod > 0f ? rampPeriod : 10f);
        while (true)
        {
            yield return wait;

            // Tighten intervals (speed up spawns)
            spawnIntervalRange.x = Mathf.Max(0.1f, spawnIntervalRange.x * intervalMultiplierPerRamp);
            spawnIntervalRange.y = Mathf.Max(spawnIntervalRange.x + 0.1f, spawnIntervalRange.y * intervalMultiplierPerRamp);

            // Raise cap
            if (maxConcurrent < maxConcurrentHardCap)
                maxConcurrent = Mathf.Min(maxConcurrent + maxConcurrentIncrementPerRamp, maxConcurrentHardCap);
        }
    }

    private void OnChildGone(SpawnHook hook)
    {
        // Called when a spawned fish disables/destroys itself
        if (hook) hook.onGone -= OnChildGone;
        _alive.Remove(hook);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Just a tiny icon to find the spawner in scene
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.25f);
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, $"PufferFishSpawner\nAlive: {(Application.isPlaying ? _alive.Count : 0)}");
    }
#endif

    /// <summary>
    /// Small helper that notifies the spawner when the spawned object goes away.
    /// Lives on each spawned PufferFish GameObject.
    /// </summary>
    private sealed class SpawnHook : MonoBehaviour
    {
        public System.Action<SpawnHook> onGone;
        void OnDisable() => onGone?.Invoke(this);
        void OnDestroy() => onGone?.Invoke(this);
    }
}

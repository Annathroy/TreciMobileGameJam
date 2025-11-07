using Unity.VisualScripting;
using UnityEngine;

public class EnemyDrop : MonoBehaviour
{
    [Header("Drop Settings")]
    [SerializeField] private GameObject[] dropPrefabs;
    [SerializeField] private float[] dropChances;        // 0..1 per prefab
    [SerializeField] private bool guaranteedDrop = false;

    [Header("Random Selection")]
    [SerializeField] private bool useWeightedRandomSelection = true;
    [SerializeField, Range(0f, 1f)] private float overallDropChance = 0.1f;

    [Header("Drop Limiting")]
    [SerializeField] private bool useGlobalDropLimiting = true;
    [SerializeField] private float globalDropCooldown = 2f;
    [SerializeField] private int maxDropsPerWave = 3;
    [SerializeField, Range(0f, 1f)] private float dropChanceReduction = 0.5f;
    [SerializeField] private int enemyThresholdForReduction = 5;

    [Header("Drop Physics")]
    [SerializeField] private float dropForce = 5f;
    [SerializeField] private float dropRadius = 2f;
    [SerializeField] private float dropHeight = 1f;

    [Header("Auto Drop on Death")]
    [SerializeField] private bool dropOnDestroy = true;
    [SerializeField] private bool dropOnHealthDeath = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    private bool hasDropped = false;

    // Global state
    private static float lastGlobalDropTime = -1f;
    private static int currentWaveDropCount = 0;
    private static float waveStartTime = -1f;
    private const float WAVE_RESET_TIME = 10f;

    // --- Lifecycle ---

    private void Awake() { Log("Awake"); }
    private void OnEnable() { Log("OnEnable"); }

    private void Start()
    {
        Log("Start");
        if (waveStartTime < 0f) waveStartTime = Time.time;

        if (dropOnHealthDeath)
        {
            var kh = GetComponent<KrakenHealth>();
            if (kh != null) { kh.onDeath.AddListener(TriggerDrop); Log("Hooked KrakenHealth.onDeath"); }

            var eh = GetComponent<EnemyHealth>();
            if (eh != null) { eh.onDeath.AddListener(TriggerDrop); Log("Hooked EnemyHealth.onDeath"); }
        }
    }

    private void OnDisable()
    {
        Log($"OnDisable (dropOnDestroy={dropOnDestroy}, hasDropped={hasDropped})");
        if (dropOnDestroy && !hasDropped)
        {
            TriggerDrop();
        }
    }

    // --- Main ---

    public void TriggerDrop()
    {
        Log("TriggerDrop() ENTER");

        if (hasDropped)
        {
            Log("Already dropped once, abort.");
            return;
        }
        if (dropPrefabs == null || dropPrefabs.Length == 0)
        {
            Log("No dropPrefabs assigned, abort.");
            return;
        }

        // If globally blocked, no roll occurs -> no +5
        if (useGlobalDropLimiting && !CanDropGlobally())
        {
            Log("Global limit blocked roll; no score.");
            return;
        }

        hasDropped = true;

        float adjusted = GetAdjustedChanceMultiplier();
        float finalDropChance = Mathf.Clamp01(overallDropChance * adjusted);
        Log($"AdjustedChanceMult={adjusted:F3}, finalDropChance={finalDropChance:F3}, guaranteed={guaranteedDrop}");

        // Non-guaranteed path: a real roll happens -> +5 on attempt
        if (!guaranteedDrop)
        {
            Score.Add(5);
            Log($"+5 for roll attempt. Current={Score.Current}, High={Score.High}");

            bool rollPasses = Random.Range(0f, 1f) <= finalDropChance;
            Log($"Roll result: {(rollPasses ? "PASS" : "FAIL")}");

            if (!rollPasses)
            {
                // No drop; only +5 stays
                return;
            }
            // else continue to do the drop (+10 below)
        }

        // Guaranteed or passed roll -> perform the drop (+10)
        var prefab = SelectRandomPrefab();
        if (prefab != null)
        {
            SpawnDrop(prefab);
            RecordGlobalDrop();
            Score.Add(10);
            Log($"+10 for actual drop. Current={Score.Current}, High={Score.High}");
        }
        else
        {
            // Edge case: said yes to drop but nothing to spawn; still count as drop
            RecordGlobalDrop();
            Score.Add(10);
            LogWarning("Drop approved but no prefab available. Counted +10 anyway.");
        }
    }

    // Forced drop: no roll (+5). Always +10 for actual drop.
    public void ForceDrop(int dropIndex = -1)
    {
        Log("ForceDrop()");

        if (dropPrefabs == null || dropPrefabs.Length == 0)
        {
            Log("No dropPrefabs assigned, abort force.");
            return;
        }

        GameObject chosen = null;
        if (dropIndex >= 0 && dropIndex < dropPrefabs.Length) chosen = dropPrefabs[dropIndex];
        if (!chosen) chosen = SelectRandomPrefab();
        if (!chosen)
        {
            Log("No valid prefab to force-drop.");
            return;
        }

        SpawnDrop(chosen);
        RecordGlobalDrop();
        Score.Add(10);
        Log($"+10 (forced drop). Current={Score.Current}, High={Score.High}");
    }

    // --- Helpers ---

    private bool CanDropGlobally()
    {
        if (Time.time - waveStartTime > WAVE_RESET_TIME)
        {
            currentWaveDropCount = 0;
            waveStartTime = Time.time;
            Log("Wave window reset.");
        }

        if (Time.time - lastGlobalDropTime < globalDropCooldown)
        {
            Log($"Global cooldown active ({Time.time - lastGlobalDropTime:F2}s < {globalDropCooldown}s)");
            return false;
        }

        if (currentWaveDropCount >= maxDropsPerWave)
        {
            Log($"Wave cap reached ({currentWaveDropCount}/{maxDropsPerWave}).");
            return false;
        }

        return true;
    }

    private void RecordGlobalDrop()
    {
        lastGlobalDropTime = Time.time;
        currentWaveDropCount++;
        Log($"RecordGlobalDrop: waveCount={currentWaveDropCount}");
    }

    private float GetAdjustedChanceMultiplier()
    {
        if (!useGlobalDropLimiting) return 1f;

        var all = Object.FindObjectsByType<EnemyDrop>(FindObjectsSortMode.None);
        int activeCount = 0;
        foreach (var d in all)
            if (d.gameObject.activeInHierarchy && !d.hasDropped) activeCount++;

        if (activeCount > enemyThresholdForReduction)
        {
            Log($"High enemy count={activeCount} > threshold={enemyThresholdForReduction}, reducing chances x{dropChanceReduction}");
            return dropChanceReduction;
        }
        return 1f;
    }

    private GameObject SelectRandomPrefab()
    {
        if (dropPrefabs == null || dropPrefabs.Length == 0) return null;

        if (useWeightedRandomSelection && dropChances != null && dropChances.Length > 0)
            return SelectWeightedRandom();

        return SelectSimpleRandom();
    }

    private GameObject SelectWeightedRandom()
    {
        float total = 0f;
        int max = Mathf.Min(dropPrefabs.Length, dropChances.Length);
        for (int i = 0; i < max; i++) if (dropPrefabs[i]) total += dropChances[i];
        if (total <= 0f) return SelectSimpleRandom();

        float r = Random.Range(0f, total);
        float acc = 0f;
        for (int i = 0; i < max; i++)
        {
            if (!dropPrefabs[i]) continue;
            acc += dropChances[i];
            if (r <= acc) return dropPrefabs[i];
        }
        for (int i = dropPrefabs.Length - 1; i >= 0; i--) if (dropPrefabs[i]) return dropPrefabs[i];
        return null;
    }

    private GameObject SelectSimpleRandom()
    {
        var list = new System.Collections.Generic.List<GameObject>();
        foreach (var p in dropPrefabs) if (p) list.Add(p);
        if (list.Count == 0) return null;
        return list[Random.Range(0, list.Count)];
    }

    private void SpawnDrop(GameObject prefab)
    {
        Vector3 pos = transform.position + Vector3.up * dropHeight;
        Vector2 circle = Random.insideUnitCircle * dropRadius;
        pos += new Vector3(circle.x, 0f, circle.y);

        var drop = Instantiate(prefab, pos, Quaternion.identity);

        var rb = drop.GetComponent<Rigidbody>();
        if (rb)
        {
            Vector3 dir = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(0.5f, 1f),
                Random.Range(-1f, 1f)
            ).normalized;
            rb.AddForce(dir * dropForce, ForceMode.Impulse);
        }
        Log($"Spawned drop: {prefab.name} @ {pos}");
    }

    // --- Logging ---

    private void Log(string msg)
    {
        if (verboseLogs) Debug.Log($"[EnemyDrop] {name}: {msg}", this);
    }
    private void LogWarning(string msg)
    {
        if (verboseLogs) Debug.LogWarning($"[EnemyDrop] {name}: {msg}", this);
    }
}

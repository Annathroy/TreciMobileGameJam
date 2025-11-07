using Unity.VisualScripting;
using UnityEngine;

public class EnemyDrop : MonoBehaviour
{
    [Header("Drop Settings")]
    [SerializeField] private GameObject[] dropPrefabs;
    [SerializeField] private float[] dropChances; // 0.0 to 1.0 for each prefab
    [SerializeField] private bool guaranteedDrop = false;

    [Header("Random Selection")]
    [SerializeField] private bool useWeightedRandomSelection = true;
    [SerializeField] private float overallDropChance = 0.1f; // Overall chance that ANY drop will occur

    [Header("Drop Limiting")]
    [SerializeField] private bool useGlobalDropLimiting = true;
    [SerializeField] private float globalDropCooldown = 2f; // Seconds between any drops globally
    [SerializeField] private int maxDropsPerWave = 3; // Maximum drops allowed per enemy wave
    [SerializeField] private float dropChanceReduction = 0.5f; // Multiply drop chances by this when many enemies present
    [SerializeField] private int enemyThresholdForReduction = 5; // Reduce chances when more than this many enemies

    [Header("Drop Physics")]
    [SerializeField] private float dropForce = 5f;
    [SerializeField] private float dropRadius = 2f;
    [SerializeField] private float dropHeight = 1f;

    [Header("Auto Drop on Death")]
    [SerializeField] private bool dropOnDestroy = true;
    [SerializeField] private bool dropOnHealthDeath = true;

    private bool hasDropped = false; // Prevent multiple drops

    // Static variables for global drop management
    private static float lastGlobalDropTime = -1f;
    private static int currentWaveDropCount = 0;
    private static float waveStartTime = -1f;
    private const float WAVE_RESET_TIME = 10f; // Reset wave counter every 10 seconds

    private void Start()
    {
        if (waveStartTime < 0f) waveStartTime = Time.time;

        if (dropOnHealthDeath)
        {
            var krakenHealth = GetComponent<KrakenHealth>();
            if (krakenHealth != null) krakenHealth.onDeath.AddListener(TriggerDrop);

            var enemyHealth = GetComponent<EnemyHealth>();
            if (enemyHealth != null) enemyHealth.onDeath.AddListener(TriggerDrop);
        }
    }

    private void OnDisable()
    {
        if (dropOnDestroy && !hasDropped)
        {
            TriggerDrop();
        }
    }

    public void TriggerDrop()
    {
        if (hasDropped || dropPrefabs == null || dropPrefabs.Length == 0) return;

        if (useGlobalDropLimiting && !CanDropGlobally()) return;

        hasDropped = true;

        float adjustedChanceMultiplier = GetAdjustedChanceMultiplier();
        float finalDropChance = overallDropChance * adjustedChanceMultiplier;

        if (!guaranteedDrop && Random.Range(0f, 1f) > finalDropChance)
        {
            return; // No drop this time
        }

        GameObject selectedPrefab = SelectRandomPrefab();

        if (selectedPrefab != null)
        {
            SpawnDrop(selectedPrefab);
            RecordGlobalDrop();
        }
    }

    private GameObject SelectRandomPrefab()
    {
        if (dropPrefabs == null || dropPrefabs.Length == 0) return null;

        if (useWeightedRandomSelection && dropChances != null && dropChances.Length > 0)
            return SelectWeightedRandom();
        else
            return SelectSimpleRandom();
    }

    private GameObject SelectWeightedRandom()
    {
        float totalWeight = 0f;
        for (int i = 0; i < Mathf.Min(dropPrefabs.Length, dropChances.Length); i++)
        {
            if (dropPrefabs[i] != null)
            {
                totalWeight += dropChances[i];
            }
        }

        if (totalWeight <= 0f) return SelectSimpleRandom();

        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        for (int i = 0; i < Mathf.Min(dropPrefabs.Length, dropChances.Length); i++)
        {
            if (dropPrefabs[i] == null) continue;

            currentWeight += dropChances[i];
            if (randomValue <= currentWeight)
            {
                return dropPrefabs[i];
            }
        }

        for (int i = dropPrefabs.Length - 1; i >= 0; i--)
        {
            if (dropPrefabs[i] != null) return dropPrefabs[i];
        }

        return null;
    }

    private GameObject SelectSimpleRandom()
    {
        var validPrefabs = new System.Collections.Generic.List<GameObject>();
        foreach (var prefab in dropPrefabs)
        {
            if (prefab != null) validPrefabs.Add(prefab);
        }

        if (validPrefabs.Count == 0) return null;

        int randomIndex = Random.Range(0, validPrefabs.Count);
        return validPrefabs[randomIndex];
    }

    private bool CanDropGlobally()
    {
        if (Time.time - waveStartTime > WAVE_RESET_TIME)
        {
            currentWaveDropCount = 0;
            waveStartTime = Time.time;
        }

        if (Time.time - lastGlobalDropTime < globalDropCooldown) return false;
        if (currentWaveDropCount >= maxDropsPerWave) return false;

        return true;
    }

    private void RecordGlobalDrop()
    {
        lastGlobalDropTime = Time.time;
        currentWaveDropCount++;
    }

    private float GetAdjustedChanceMultiplier()
    {
        if (!useGlobalDropLimiting) return 1f;

        EnemyDrop[] allEnemyDrops = Object.FindObjectsByType<EnemyDrop>(FindObjectsSortMode.None);
        int activeEnemyCount = 0;

        foreach (var enemyDrop in allEnemyDrops)
        {
            if (enemyDrop.gameObject.activeInHierarchy && !enemyDrop.hasDropped)
            {
                activeEnemyCount++;
            }
        }

        if (activeEnemyCount > enemyThresholdForReduction)
        {
            return dropChanceReduction;
        }

        return 1f;
    }

    private void SpawnDrop(GameObject prefab)
    {
        Vector3 dropPosition = transform.position + Vector3.up * dropHeight;
        Vector2 randomCircle = Random.insideUnitCircle * dropRadius;
        dropPosition += new Vector3(randomCircle.x, 0, randomCircle.y);

        GameObject drop = Instantiate(prefab, dropPosition, Quaternion.identity);

        Rigidbody rb = drop.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 randomDirection = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(0.5f, 1f),
                Random.Range(-1f, 1f)
            ).normalized;

            rb.AddForce(randomDirection * dropForce, ForceMode.Impulse);
        }
    }

    public void ForceDrop(int dropIndex = -1)
    {
        if (dropPrefabs == null || dropPrefabs.Length == 0) return;

        if (dropIndex >= 0 && dropIndex < dropPrefabs.Length && dropPrefabs[dropIndex] != null)
        {
            SpawnDrop(dropPrefabs[dropIndex]);
        }
        else
        {
            GameObject selectedPrefab = SelectRandomPrefab();
            if (selectedPrefab != null) SpawnDrop(selectedPrefab);
        }
    }

    public void SetDropChance(int index, float chance)
    {
        if (dropChances == null || index < 0 || index >= dropChances.Length) return;
        dropChances[index] = Mathf.Clamp01(chance);
    }

    public float GetTotalDropChance()
    {
        float multiplier = GetAdjustedChanceMultiplier();
        return overallDropChance * multiplier;
    }

    public float GetDropTypeChance(int index)
    {
        if (!useWeightedRandomSelection || dropChances == null || index < 0 || index >= dropChances.Length)
        {
            int validPrefabCount = 0;
            foreach (var prefab in dropPrefabs)
            {
                if (prefab != null) validPrefabCount++;
            }
            return validPrefabCount > 0 ? GetTotalDropChance() / validPrefabCount : 0f;
        }

        float totalWeight = 0f;
        for (int i = 0; i < Mathf.Min(dropPrefabs.Length, dropChances.Length); i++)
        {
            if (dropPrefabs[i] != null) totalWeight += dropChances[i];
        }

        if (totalWeight <= 0f) return 0f;

        return GetTotalDropChance() * (dropChances[index] / totalWeight);
    }

    public void ResetDropState()
    {
        hasDropped = false;
    }

    public static void ResetGlobalDropCounters()
    {
        lastGlobalDropTime = -1f;
        currentWaveDropCount = 0;
        waveStartTime = Time.time;
    }

    void Update() { }
}

using Unity.VisualScripting;
using UnityEngine;

public class EnemyDrop : MonoBehaviour
{
    [Header("Drop Settings")]
    [SerializeField] private GameObject[] dropPrefabs;
    [SerializeField] private float[] dropChances; // 0.0 to 1.0 for each prefab
    [SerializeField] private bool guaranteedDrop = false;
    [SerializeField] private int maxDrops = 1;

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
        // Initialize wave timing if this is the first enemy
        if (waveStartTime < 0f)
        {
            waveStartTime = Time.time;
        }

        // Ensure arrays match
        if (dropPrefabs != null && dropChances != null)
        {
            if (dropPrefabs.Length != dropChances.Length)
            {
                Debug.LogWarning($"[EnemyDrop] Drop prefabs and chances arrays don't match on {gameObject.name}");
            }
        }

        // Try to hook into enemy death if there's a health component
        if (dropOnHealthDeath)
        {
            var krakenHealth = GetComponent<KrakenHealth>();
            if (krakenHealth != null)
            {
                krakenHealth.onDeath.AddListener(TriggerDrop);
            }

            var enemyHealth = GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.onDeath.AddListener(TriggerDrop);
            }
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
        // Prevent multiple drops from the same enemy
        if (hasDropped || dropPrefabs == null || dropPrefabs.Length == 0) return;

        // Global drop limiting
        if (useGlobalDropLimiting && !CanDropGlobally()) return;

        hasDropped = true;

        float adjustedChanceMultiplier = GetAdjustedChanceMultiplier();
        float finalDropChance = overallDropChance * adjustedChanceMultiplier;

        // First check if we should drop anything at all
        if (!guaranteedDrop && Random.Range(0f, 1f) > finalDropChance)
        {
            return; // No drop this time
        }

        // Select which prefab to drop
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
        {
            // Weighted random selection based on drop chances
            return SelectWeightedRandom();
        }
        else
        {
            // Simple random selection (equal chances)
            return SelectSimpleRandom();
        }
    }

    private GameObject SelectWeightedRandom()
    {
        // Calculate total weight
        float totalWeight = 0f;
        for (int i = 0; i < Mathf.Min(dropPrefabs.Length, dropChances.Length); i++)
        {
            if (dropPrefabs[i] != null)
            {
                totalWeight += dropChances[i];
            }
        }

        if (totalWeight <= 0f) return SelectSimpleRandom();

        // Random selection based on weights
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

        // Fallback to last valid prefab
        for (int i = dropPrefabs.Length - 1; i >= 0; i--)
        {
            if (dropPrefabs[i] != null) return dropPrefabs[i];
        }

        return null;
    }

    private GameObject SelectSimpleRandom()
    {
        // Filter out null prefabs
        var validPrefabs = new System.Collections.Generic.List<GameObject>();
        foreach (var prefab in dropPrefabs)
        {
            if (prefab != null) validPrefabs.Add(prefab);
        }

        if (validPrefabs.Count == 0) return null;

        // Random selection from valid prefabs
        int randomIndex = Random.Range(0, validPrefabs.Count);
        return validPrefabs[randomIndex];
    }

    private bool CanDropGlobally()
    {
        // Reset wave counter if enough time has passed
        if (Time.time - waveStartTime > WAVE_RESET_TIME)
        {
            currentWaveDropCount = 0;
            waveStartTime = Time.time;
        }

        // Check global cooldown
        if (Time.time - lastGlobalDropTime < globalDropCooldown)
        {
            return false;
        }

        // Check wave drop limit
        if (currentWaveDropCount >= maxDropsPerWave)
        {
            return false;
        }

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

        // Count active enemies with EnemyDrop components
        EnemyDrop[] allEnemyDrops = Object.FindObjectsByType<EnemyDrop>(FindObjectsSortMode.None);
        int activeEnemyCount = 0;

        foreach (var enemyDrop in allEnemyDrops)
        {
            if (enemyDrop.gameObject.activeInHierarchy && !enemyDrop.hasDropped)
            {
                activeEnemyCount++;
            }
        }

        // Reduce drop chances when there are many enemies
        if (activeEnemyCount > enemyThresholdForReduction)
        {
            return dropChanceReduction;
        }

        return 1f;
    }

    private void SpawnDrop(GameObject prefab)
    {
        Vector3 dropPosition = transform.position + Vector3.up * dropHeight;

        // Add some random spread
        Vector2 randomCircle = Random.insideUnitCircle * dropRadius;
        dropPosition += new Vector3(randomCircle.x, 0, randomCircle.y);

        GameObject drop = Instantiate(prefab, dropPosition, Quaternion.identity);

        // Apply physics if the drop has a Rigidbody
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

    // Public method to manually trigger drops (for external scripts)
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

    // Helper method to set drop chances at runtime
    public void SetDropChance(int index, float chance)
    {
        if (dropChances == null || index < 0 || index >= dropChances.Length) return;
        dropChances[index] = Mathf.Clamp01(chance);
    }

    // Utility method to calculate total drop chance for UI/debugging
    public float GetTotalDropChance()
    {
        float multiplier = GetAdjustedChanceMultiplier();
        return overallDropChance * multiplier;
    }

    // Get the effective chance for a specific drop type
    public float GetDropTypeChance(int index)
    {
        if (!useWeightedRandomSelection || dropChances == null || index < 0 || index >= dropChances.Length)
        {
            // Equal chances when not using weighted selection
            int validPrefabCount = 0;
            foreach (var prefab in dropPrefabs)
            {
                if (prefab != null) validPrefabCount++;
            }
            return validPrefabCount > 0 ? GetTotalDropChance() / validPrefabCount : 0f;
        }

        // Calculate total weight
        float totalWeight = 0f;
        for (int i = 0; i < Mathf.Min(dropPrefabs.Length, dropChances.Length); i++)
        {
            if (dropPrefabs[i] != null) totalWeight += dropChances[i];
        }

        if (totalWeight <= 0f) return 0f;

        // Return this type's share of the total drop chance
        return GetTotalDropChance() * (dropChances[index] / totalWeight);
    }

    // Reset the drop state (useful if you want to reuse enemies from pools)
    public void ResetDropState()
    {
        hasDropped = false;
    }

    // Static method to reset global drop counters (useful for new levels/waves)
    public static void ResetGlobalDropCounters()
    {
        lastGlobalDropTime = -1f;
        currentWaveDropCount = 0;
        waveStartTime = Time.time;
    }

    void Update()
    {
        // This can be used for any per-frame drop logic if needed
        // Currently empty as drops are event-driven
    }
}
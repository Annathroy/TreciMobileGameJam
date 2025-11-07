using System.Collections;
using UnityEngine;

public class Spawners : MonoBehaviour
{
    [Header("Spawner References")]
    [SerializeField] private GameObject[] spawners = new GameObject[4];
    
    [Header("Timing Configuration")]
    [SerializeField] private float delayBetweenSpawners = 5f; // Time between each spawner activation
    [SerializeField] private float initialDelay = 2f; // Delay before starting the sequence
    [SerializeField] private bool autoStartOnEnable = true;
    
    [Header("Debug Info")]
    [SerializeField] private bool showDebugLogs = true;
    
    private int currentSpawnerIndex = 0;
    private bool isSequenceRunning = false;

    void Start()
    {
        // Disable all spawners at start
        DisableAllSpawners();
        
        if (autoStartOnEnable)
        {
            StartSpawnerSequence();
        }
    }

    /// <summary>
    /// Starts the sequential spawner activation
    /// </summary>
    public void StartSpawnerSequence()
    {
        if (isSequenceRunning)
        {
            if (showDebugLogs) Debug.LogWarning("[Spawners] Sequence is already running!");
            return;
        }

        if (ValidateSpawners())
        {
            StartCoroutine(SpawnerSequenceCoroutine());
        }
    }

    /// <summary>
    /// Stops the spawner sequence and disables all spawners
    /// </summary>
    public void StopSpawnerSequence()
    {
        StopAllCoroutines();
        isSequenceRunning = false;
        currentSpawnerIndex = 0;
        DisableAllSpawners();
        
        if (showDebugLogs) Debug.Log("[Spawners] Sequence stopped and all spawners disabled.");
    }

    /// <summary>
    /// Resets and restarts the spawner sequence
    /// </summary>
    public void RestartSpawnerSequence()
    {
        StopSpawnerSequence();
        StartSpawnerSequence();
    }

    private IEnumerator SpawnerSequenceCoroutine()
    {
        isSequenceRunning = true;
        currentSpawnerIndex = 0;

        if (showDebugLogs) Debug.Log($"[Spawners] Starting spawner sequence with {initialDelay}s initial delay...");
        
        // Initial delay before starting
        if (initialDelay > 0)
        {
            yield return new WaitForSeconds(initialDelay);
        }

        // Activate each spawner sequentially
        for (int i = 0; i < spawners.Length; i++)
        {
            if (spawners[i] != null)
            {
                EnableSpawner(i);
                currentSpawnerIndex = i;
                
                // Wait before activating next spawner (except for the last one)
                if (i < spawners.Length - 1)
                {
                    yield return new WaitForSeconds(delayBetweenSpawners);
                }
            }
        }

        isSequenceRunning = false;
        if (showDebugLogs) Debug.Log("[Spawners] Spawner sequence completed. All spawners are now active.");
    }

    private void EnableSpawner(int index)
    {
        if (index >= 0 && index < spawners.Length && spawners[index] != null)
        {
            spawners[index].SetActive(true);
            
            // Try to call StartSpawning method if the spawner has one
            var spawnerComponent = spawners[index].GetComponent<MonoBehaviour>();
            if (spawnerComponent != null)
            {
                var startMethod = spawnerComponent.GetType().GetMethod("StartSpawning");
                if (startMethod != null)
                {
                    startMethod.Invoke(spawnerComponent, null);
                }
            }
            
            if (showDebugLogs) Debug.Log($"[Spawners] Spawner {index + 1} ({spawners[index].name}) enabled!");
        }
    }

    private void DisableAllSpawners()
    {
        for (int i = 0; i < spawners.Length; i++)
        {
            if (spawners[i] != null)
            {
                spawners[i].SetActive(false);
            }
        }
        
        if (showDebugLogs) Debug.Log("[Spawners] All spawners disabled.");
    }

    private bool ValidateSpawners()
    {
        int nullCount = 0;
        for (int i = 0; i < spawners.Length; i++)
        {
            if (spawners[i] == null)
            {
                nullCount++;
                Debug.LogWarning($"[Spawners] Spawner slot {i + 1} is not assigned!");
            }
        }

        if (nullCount == spawners.Length)
        {
            Debug.LogError("[Spawners] No spawners assigned! Please assign spawner GameObjects in the inspector.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Manually enable a specific spawner by index (0-3)
    /// </summary>
    public void EnableSpecificSpawner(int index)
    {
        if (index >= 0 && index < spawners.Length)
        {
            EnableSpawner(index);
        }
        else
        {
            Debug.LogWarning($"[Spawners] Invalid spawner index: {index}. Valid range is 0-{spawners.Length - 1}");
        }
    }

    /// <summary>
    /// Get the current progress of the spawner sequence (0-1)
    /// </summary>
    public float GetSequenceProgress()
    {
        if (!isSequenceRunning) return currentSpawnerIndex >= spawners.Length - 1 ? 1f : 0f;
        return (float)(currentSpawnerIndex + 1) / spawners.Length;
    }

    // Properties for external access
    public bool IsSequenceRunning => isSequenceRunning;
    public int CurrentSpawnerIndex => currentSpawnerIndex;
    public int TotalSpawners => spawners.Length;

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Draw connections to assigned spawners in Scene view
        if (spawners == null) return;
        
        Gizmos.color = Color.yellow;
        for (int i = 0; i < spawners.Length; i++)
        {
            if (spawners[i] != null)
            {
                Gizmos.DrawLine(transform.position, spawners[i].transform.position);
                Gizmos.color = spawners[i].activeInHierarchy ? Color.green : Color.red;
                Gizmos.DrawWireSphere(spawners[i].transform.position, 0.5f);
                
                UnityEditor.Handles.Label(
                    spawners[i].transform.position + Vector3.up, 
                    $"Spawner {i + 1}\n{(spawners[i].activeInHierarchy ? "Active" : "Inactive")}"
                );
            }
        }
    }
#endif
}

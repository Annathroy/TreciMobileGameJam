using UnityEngine;

public class FishRandomizer : MonoBehaviour
{
    [Header("Fish Game Objects")]
    [SerializeField] private GameObject[] fishObjects = new GameObject[6];

    [Header("Randomization Settings")]
    [SerializeField] private float intervalMin = 1f;
    [SerializeField] private float intervalMax = 3f;
    [SerializeField] private int minActiveCount = 1;
    [SerializeField] private int maxActiveCount = 4;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private float nextRandomizeTime;

    void Start()
    {
        // Validate fish objects array
        for (int i = 0; i < fishObjects.Length; i++)
        {
            if (fishObjects[i] == null)
            {
                Debug.LogWarning($"FishRandomizer: Fish object at index {i} is not assigned!", this);
            }
        }

        // Initial randomization
        RandomizeFishStates();
        ScheduleNextRandomization();
    }

    void Update()
    {
        if (Time.time >= nextRandomizeTime)
        {
            RandomizeFishStates();
            ScheduleNextRandomization();
        }
    }

    private void RandomizeFishStates()
    {
        // Determine how many fish should be active
        int targetActiveCount = Random.Range(minActiveCount, maxActiveCount + 1);
        targetActiveCount = Mathf.Clamp(targetActiveCount, 0, fishObjects.Length);

        if (enableDebugLogs)
        {
            Debug.Log($"FishRandomizer: Setting {targetActiveCount} fish to active");
        }

        // First, disable all fish
        for (int i = 0; i < fishObjects.Length; i++)
        {
            if (fishObjects[i] != null)
            {
                fishObjects[i].SetActive(false);
            }
        }

        // Then randomly enable the target number of fish
        if (targetActiveCount > 0)
        {
            // Create a list of available indices
            var availableIndices = new System.Collections.Generic.List<int>();
            for (int i = 0; i < fishObjects.Length; i++)
            {
                if (fishObjects[i] != null)
                {
                    availableIndices.Add(i);
                }
            }

            // Randomly select and activate fish
            for (int i = 0; i < targetActiveCount && availableIndices.Count > 0; i++)
            {
                int randomIndex = Random.Range(0, availableIndices.Count);
                int fishIndex = availableIndices[randomIndex];

                fishObjects[fishIndex].SetActive(true);
                availableIndices.RemoveAt(randomIndex);

                if (enableDebugLogs)
                {
                    Debug.Log($"FishRandomizer: Activated fish at index {fishIndex}");
                }
            }
        }
    }

    private void ScheduleNextRandomization()
    {
        float randomInterval = Random.Range(intervalMin, intervalMax);
        nextRandomizeTime = Time.time + randomInterval;

        if (enableDebugLogs)
        {
            Debug.Log($"FishRandomizer: Next randomization in {randomInterval:F1} seconds");
        }
    }
}
    ///

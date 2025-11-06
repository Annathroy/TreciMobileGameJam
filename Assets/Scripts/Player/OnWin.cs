using UnityEngine;
using UnityEngine.Events;
using System.Collections;

[DisallowMultipleComponent]
public class OnWin : MonoBehaviour
{
    [Header("Player Speed Settings")]
    [SerializeField] private float victorySpeed = 15f;
    [SerializeField] private float speedDuration = 2f;
    [SerializeField] private bool disablePlayerControls = true;

    [Header("Victory Panel")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private float panelDelay = 0.5f;

    [Header("Audio")]
    [SerializeField] private AudioClip victorySound;
    [SerializeField] private float victorySoundVolume = 1f;

    [Header("Components to Disable")]
    [SerializeField] private Behaviour[] componentsToDisable;

    [Header("Events")]
    public UnityEvent onVictoryStart;
    public UnityEvent onVictoryComplete;

    private GameObject player;
    private Rigidbody playerRb;
    private bool victoryTriggered;
    private Vector3 originalVelocity;

    private void Awake()
    {
        // Find player GameObject
        player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[OnWin] Player GameObject with 'Player' tag not found!");
            return;
        }

        playerRb = player.GetComponent<Rigidbody>();
        if (playerRb == null)
        {
            Debug.LogWarning("[OnWin] Player doesn't have a Rigidbody. Will use Transform movement instead.");
        }

        // Auto-find victory panel if not assigned
        if (victoryPanel == null)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                Transform foundPanel = canvas.transform.Find("VictoryPanel");
                if (foundPanel != null)
                {
                    victoryPanel = foundPanel.gameObject;
                }
            }
        }

        // Ensure victory panel is initially hidden
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Triggers the victory sequence - call this when win condition is met
    /// </summary>
    public void TriggerVictory()
    {
        if (victoryTriggered || player == null) return;

        victoryTriggered = true;

        Debug.Log("[OnWin] Victory triggered!");

        // Play victory sound
        if (victorySound != null)
        {
            AudioSource.PlayClipAtPoint(victorySound, player.transform.position, victorySoundVolume);
        }

        // Trigger victory start event
        onVictoryStart?.Invoke();

        // Start victory sequence
        StartCoroutine(VictorySequence());
    }

    private IEnumerator VictorySequence()
    {
        // Store original velocity if using Rigidbody
        if (playerRb != null)
        {
            originalVelocity = playerRb.linearVelocity;
        }

        // Disable player controls
        if (disablePlayerControls)
        {
            DisablePlayerComponents();
        }

        // Speed player forward
        yield return StartCoroutine(SpeedPlayerForward());

        // Show victory panel after delay
        yield return new WaitForSeconds(panelDelay);
        ShowVictoryPanel();

        // Trigger victory complete event
        onVictoryComplete?.Invoke();
    }

    private IEnumerator SpeedPlayerForward()
    {
        float elapsed = 0f;
        Vector3 forwardDirection = Vector3.forward; // Assuming forward is positive Z

        while (elapsed < speedDuration)
        {
            if (player == null) yield break;

            if (playerRb != null)
            {
                // Use Rigidbody for movement
                playerRb.linearVelocity = forwardDirection * victorySpeed;
            }
            else
            {
                // Use Transform for movement
                player.transform.position += forwardDirection * victorySpeed * Time.deltaTime;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Stop player movement
        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector3.zero;
        }
    }

    private void DisablePlayerComponents()
    {
        if (componentsToDisable != null)
        {
            foreach (var component in componentsToDisable)
            {
                if (component != null)
                {
                    component.enabled = false;
                }
            }
        }

        // Auto-disable common player components
        var playerAttack = player.GetComponent<MonoBehaviour>();
        if (playerAttack != null && playerAttack.GetType().Name.Contains("Attack"))
        {
            playerAttack.enabled = false;
        }

        // Disable player input components
        var inputComponents = player.GetComponents<MonoBehaviour>();
        foreach (var comp in inputComponents)
        {
            if (comp.GetType().Name.Contains("Input") || comp.GetType().Name.Contains("Control"))
            {
                comp.enabled = false;
            }
        }
    }

    private void ShowVictoryPanel()
    {
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
            Debug.Log("[OnWin] Victory panel displayed!");
        }
        else
        {
            Debug.LogWarning("[OnWin] Victory panel not assigned or found!");
        }
    }

    /// <summary>
    /// Resets the victory state - useful for testing or restarting
    /// </summary>
    public void ResetVictory()
    {
        victoryTriggered = false;

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }

        // Re-enable components
        if (componentsToDisable != null)
        {
            foreach (var component in componentsToDisable)
            {
                if (component != null)
                {
                    component.enabled = true;
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Optional: Auto-trigger victory when player enters this trigger
        if (other.CompareTag("Player"))
        {
            TriggerVictory();
        }
    }
}
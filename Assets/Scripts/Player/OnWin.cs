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
    [SerializeField] private bool overrideRigidbodyConstraints = true;

    [Header("Player Disappearance")]
    [Tooltip("How the player should disappear after speeding away.")]
    [SerializeField] private DisappearanceMethod disappearanceMethod = DisappearanceMethod.SetInactive;
    [SerializeField] private float fadeOutDuration = 1f;

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

    public enum DisappearanceMethod
    {
        SetInactive,    // Disable the GameObject
        Destroy,        // Destroy the GameObject
        FadeOut,        // Fade out renderers
        MakeInvisible   // Disable all renderers
    }

    private GameObject player;
    private Rigidbody playerRb;
    private bool victoryTriggered;
    private Vector3 originalVelocity;
    private Renderer[] playerRenderers;
    private Material[] originalMaterials;
    private RigidbodyConstraints originalConstraints;
    private bool originalKinematic;
    private float originalDrag;
    private MonoBehaviour[] disabledComponents;

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

        // Cache player renderers for disappearance effects
        playerRenderers = player.GetComponentsInChildren<Renderer>();

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
        // Store original Rigidbody settings
        if (playerRb != null)
        {
            originalVelocity = playerRb.linearVelocity;
            originalConstraints = playerRb.constraints;
            originalKinematic = playerRb.isKinematic;
            originalDrag = playerRb.linearDamping;

            Debug.Log($"[OnWin] Original Rigidbody settings - Constraints: {originalConstraints}, Kinematic: {originalKinematic}, Drag: {originalDrag}");
        }

        // Disable player controls and other scripts
        if (disablePlayerControls)
        {
            DisablePlayerComponents();
        }

        // Prepare Rigidbody for victory movement
        if (playerRb != null && overrideRigidbodyConstraints)
        {
            playerRb.constraints = RigidbodyConstraints.FreezeRotation; // Allow movement but freeze rotation
            playerRb.isKinematic = false; // Ensure it's not kinematic
            playerRb.linearDamping = 0f; // Remove drag for smooth movement
            playerRb.linearVelocity = Vector3.zero; // Clear any existing velocity
            Debug.Log("[OnWin] Rigidbody configured for victory movement");
        }

        // Speed player forward in Z-axis
        yield return StartCoroutine(SpeedPlayerForward());

        // Make player disappear
        yield return StartCoroutine(DisappearPlayer());

        // Show victory panel after delay
        yield return new WaitForSeconds(panelDelay);
        ShowVictoryPanel();

        // Trigger victory complete event
        onVictoryComplete?.Invoke();
    }

    private IEnumerator SpeedPlayerForward()
    {
        float elapsed = 0f;
        Vector3 forwardDirection = Vector3.forward; // Z-axis direction

        Debug.Log("[OnWin] Player speeding away in Z-axis...");

        while (elapsed < speedDuration)
        {
            if (player == null) yield break;

            if (playerRb != null)
            {
                // Force velocity every frame to ensure movement
                Vector3 targetVelocity = forwardDirection * victorySpeed;
                playerRb.linearVelocity = targetVelocity;

                Debug.Log($"[OnWin] Setting velocity to: {targetVelocity}, Current position: {player.transform.position}");
            }
            else
            {
                // Use Transform for movement as fallback
                Vector3 movement = forwardDirection * victorySpeed * Time.deltaTime;
                player.transform.position += movement;
                Debug.Log($"[OnWin] Moving by: {movement}, Current position: {player.transform.position}");
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Stop player movement
        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector3.zero;
        }

        Debug.Log($"[OnWin] Player finished speeding away. Final position: {player.transform.position}");
    }

    private IEnumerator DisappearPlayer()
    {
        if (player == null) yield break;

        Debug.Log($"[OnWin] Making player disappear using method: {disappearanceMethod}");

        switch (disappearanceMethod)
        {
            case DisappearanceMethod.SetInactive:
                player.SetActive(false);
                break;

            case DisappearanceMethod.Destroy:
                Destroy(player);
                player = null;
                break;

            case DisappearanceMethod.FadeOut:
                yield return StartCoroutine(FadeOutPlayer());
                break;

            case DisappearanceMethod.MakeInvisible:
                foreach (var renderer in playerRenderers)
                {
                    if (renderer != null)
                        renderer.enabled = false;
                }
                break;
        }
    }

    private IEnumerator FadeOutPlayer()
    {
        if (playerRenderers == null || playerRenderers.Length == 0) yield break;

        // Store original materials and create fade materials
        originalMaterials = new Material[playerRenderers.Length];
        Material[] fadeMaterials = new Material[playerRenderers.Length];

        for (int i = 0; i < playerRenderers.Length; i++)
        {
            if (playerRenderers[i] != null)
            {
                originalMaterials[i] = playerRenderers[i].material;
                fadeMaterials[i] = new Material(originalMaterials[i]);

                // Enable transparency if the shader supports it
                if (fadeMaterials[i].HasProperty("_Mode"))
                {
                    fadeMaterials[i].SetFloat("_Mode", 3); // Transparent mode
                    fadeMaterials[i].SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    fadeMaterials[i].SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    fadeMaterials[i].SetInt("_ZWrite", 0);
                    fadeMaterials[i].DisableKeyword("_ALPHATEST_ON");
                    fadeMaterials[i].EnableKeyword("_ALPHABLEND_ON");
                    fadeMaterials[i].DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    fadeMaterials[i].renderQueue = 3000;
                }

                playerRenderers[i].material = fadeMaterials[i];
            }
        }

        float elapsed = 0f;
        Color originalColor, fadeColor;

        while (elapsed < fadeOutDuration)
        {
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);

            for (int i = 0; i < fadeMaterials.Length; i++)
            {
                if (fadeMaterials[i] != null)
                {
                    if (fadeMaterials[i].HasProperty("_Color"))
                    {
                        originalColor = fadeMaterials[i].GetColor("_Color");
                        fadeColor = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                        fadeMaterials[i].SetColor("_Color", fadeColor);
                    }
                    else if (fadeMaterials[i].HasProperty("_BaseColor"))
                    {
                        originalColor = fadeMaterials[i].GetColor("_BaseColor");
                        fadeColor = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                        fadeMaterials[i].SetColor("_BaseColor", fadeColor);
                    }
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Make completely invisible
        foreach (var renderer in playerRenderers)
        {
            if (renderer != null)
                renderer.enabled = false;
        }
    }

    private void DisablePlayerComponents()
    {
        var componentsToDisableList = new System.Collections.Generic.List<MonoBehaviour>();

        // Disable specified components
        if (componentsToDisable != null)
        {
            foreach (var component in componentsToDisable)
            {
                if (component != null)
                {
                    component.enabled = false;
                    componentsToDisableList.Add(component as MonoBehaviour);
                    Debug.Log($"[OnWin] Disabled component: {component.GetType().Name}");
                }
            }
        }

        // Auto-disable all player movement and control scripts
        var allComponents = player.GetComponents<MonoBehaviour>();
        foreach (var comp in allComponents)
        {
            if (comp == this) continue; // Don't disable this script

            string typeName = comp.GetType().Name;
            if (typeName.Contains("Input") || typeName.Contains("Control") ||
                typeName.Contains("Movement") || typeName.Contains("Attack") ||
                typeName.Contains("Player") && !typeName.Contains("Health"))
            {
                if (comp.enabled)
                {
                    comp.enabled = false;
                    componentsToDisableList.Add(comp);
                    Debug.Log($"[OnWin] Auto-disabled component: {typeName}");
                }
            }
        }

        disabledComponents = componentsToDisableList.ToArray();
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

        // Restore Rigidbody settings
        if (playerRb != null)
        {
            playerRb.constraints = originalConstraints;
            playerRb.isKinematic = originalKinematic;
            playerRb.linearDamping = originalDrag;
            playerRb.linearVelocity = Vector3.zero;
        }

        // Re-enable components
        if (disabledComponents != null)
        {
            foreach (var component in disabledComponents)
            {
                if (component != null)
                {
                    component.enabled = true;
                }
            }
        }

        // Restore player if it was made invisible
        if (player != null && disappearanceMethod == DisappearanceMethod.MakeInvisible)
        {
            foreach (var renderer in playerRenderers)
            {
                if (renderer != null)
                    renderer.enabled = true;
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
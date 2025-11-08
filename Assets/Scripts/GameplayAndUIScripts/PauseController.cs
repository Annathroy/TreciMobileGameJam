using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class PauseController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pausePanel; // Assign your Pause Panel (disabled by default)

    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth; // Assign PlayerHealth in Inspector

    private bool isPaused = false;
    private float prePauseTimeScale = 1f;

    private void Awake()
    {
        Time.timeScale = 1f;
        if (pausePanel) pausePanel.SetActive(false);
        isPaused = false;

        // Auto-find PlayerHealth if not assigned
        if (!playerHealth)
            playerHealth = FindObjectOfType<PlayerHealth>();
    }

    private void OnDisable()
    {
        if (isPaused) ForceResume();
    }

    // --- UI BUTTONS ---

    public void OnPauseButton()
    {
        if (isPaused) return;
        isPaused = true;

        prePauseTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (pausePanel) pausePanel.SetActive(true);
    }

    public void OnUnpauseButton()
    {
        if (!isPaused) return;
        isPaused = false;

        Time.timeScale = Mathf.Approximately(prePauseTimeScale, 0f) ? 1f : prePauseTimeScale;
        if (pausePanel) pausePanel.SetActive(false);
    }

    public void OnRestartButton()
    {
        ForceResume();
        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.name);
    }

    public void OnBackToMainMenuButton()
    {
        ForceResume();
        SceneManager.LoadScene("StartMenuScene");
    }

    // --- GOD MODE BUTTON ---
    public void OnToggleGodModeButton()
    {
        if (!playerHealth)
        {
            Debug.LogWarning("[PauseController] No PlayerHealth reference assigned or found.");
            return;
        }

        playerHealth.ToggleGodMode();

        string status = playerHealth.IsGodModeActive ? "ENABLED" : "DISABLED";
        Debug.Log($"[PauseController] God Mode {status}");
    }

    // --- Internals ---
    private void ForceResume()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (pausePanel) pausePanel.SetActive(false);
    }
}

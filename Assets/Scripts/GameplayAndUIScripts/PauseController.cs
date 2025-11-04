using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pausePanel;     // Assign your Pause Panel (disabled by default)

    [Header("Scenes")]

    private bool isPaused = false;
    private float prePauseTimeScale = 1f;

    private void Awake()
    {
        // Ensure clean state if entering this scene from anywhere
        Time.timeScale = 1f;
        if (pausePanel) pausePanel.SetActive(false);
        isPaused = false;
    }

    private void OnDisable()
    {
        // Safety: never leave the game frozen if this object gets disabled
        if (isPaused) ForceResume();
    }

    // --- Wired from UI buttons ---

    // Hook this to your in-game "Pause" button (on the HUD Canvas)
    public void OnPauseButton()
    {
        if (isPaused) return;
        isPaused = true;

        prePauseTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (pausePanel) pausePanel.SetActive(true);

        // Optional SFX
        // AudioManager.Instance?.PlaySFX("pause");
    }

    // Hook this to the "Unpause" button in the Pause Panel
    public void OnUnpauseButton()
    {
        if (!isPaused) return;
        isPaused = false;

        Time.timeScale = Mathf.Approximately(prePauseTimeScale, 0f) ? 1f : prePauseTimeScale;

        if (pausePanel) pausePanel.SetActive(false);

        // Optional SFX
        // AudioManager.Instance?.PlaySFX("resume");
    }

    // Hook this to the "Back to Main Menu" button in the Pause Panel
    public void OnBackToMainMenuButton()
    {
        // Always restore time before scene change
        ForceResume();

        if (string.IsNullOrEmpty("StartMenuScene"))
        {
            Debug.LogError("[PauseController] mainMenuSceneName not set.");
            return;
        }

        SceneManager.LoadScene("StartMenuScene");
    }

    // --- Optional helper if you also want a single toggle button somewhere ---


    // --- Internals ---
    private void ForceResume()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (pausePanel) pausePanel.SetActive(false);
    }
}

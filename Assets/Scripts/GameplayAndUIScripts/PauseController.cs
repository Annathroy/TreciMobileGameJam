using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class PauseController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pausePanel; // Assign your Pause Panel (disabled by default)

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
        // Ensure clean resume first
        ForceResume();

        // Reload current scene
        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.name);
    }

    public void OnBackToMainMenuButton()
    {
        ForceResume();
        SceneManager.LoadScene("StartMenuScene");
    }

    // --- Internals ---
    private void ForceResume()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (pausePanel) pausePanel.SetActive(false);
    }
}

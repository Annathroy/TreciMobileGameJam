using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuManager : MonoBehaviour
{


    [Header("Scenes")]
    [SerializeField] private string gameplaySceneName = "GamePlayScene";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Panels")]
    [SerializeField] private GameObject startMenuPanel;
    [SerializeField] private GameObject optionsPanel;

    [Header("Audio (via AudioManager)")]
    [SerializeField] private Slider volumeSlider;   // UI slider that controls music volume (0..1)

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI highScoreText;

    private const string HIGH_SCORE_KEY = "HighScore";

    private void Awake()
    {
        // Panels
        if (startMenuPanel) startMenuPanel.SetActive(true);
        if (optionsPanel) optionsPanel.SetActive(false);

        // High score
        int savedScore = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
        UpdateHighScoreText(savedScore);

        // Audio: ensure global music is playing and init slider from AudioManager
        var am = AudioManager.Instance;

        // --- Force correct slider wiring & range ---
        if (volumeSlider)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.wholeNumbers = false;

            // Nuke anything wired in the Inspector that might also write to it
            volumeSlider.onValueChanged.RemoveAllListeners();
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        if (am != null)
        {
            am.EnsureMusicPlaying();

            // If AM volume is "bad" (0 because of old prefs), bump to a sane default
            if (am.musicVolume <= 0.001f) am.SetMusicVolume(0.8f);

            if (volumeSlider)
                volumeSlider.SetValueWithoutNotify(am.musicVolume);
        }
        else
        {
            if (volumeSlider) volumeSlider.SetValueWithoutNotify(0.8f);
        }
    }

    // --- UI Button Hooks ---

    public void OnStartGame()
    {
        if (string.IsNullOrEmpty(gameplaySceneName))
        {
            Debug.LogError("[MainMenuManager] gameplaySceneName is empty.");
            return;
        }
        SceneManager.LoadScene(1);
    }

    public void OnOpenOptions()
    {
        if (startMenuPanel) startMenuPanel.SetActive(false);
        if (optionsPanel) optionsPanel.SetActive(true);
    }

    public void OnBackToStartMenu()
    {
        if (optionsPanel) optionsPanel.SetActive(false);
        if (startMenuPanel) startMenuPanel.SetActive(true);

        int score = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
        UpdateHighScoreText(score);
    }

    public void OnResetHighScore()
    {
        PlayerPrefs.DeleteKey(HIGH_SCORE_KEY);
        PlayerPrefs.Save();
        UpdateHighScoreText(0);
        Debug.Log("High score reset.");
        // Optional: AudioManager.Instance?.PlaySFX("click");
    }

    public void OnVolumeChanged(float value)
    {
        if (Mathf.Approximately(value, 0f))
            Debug.LogWarning("Slider value became 0 — check other listeners/animators.");

        var am = AudioManager.Instance;
        if (am == null) return;
        am.SetMusicVolume(value);
        am.EnsureMusicPlaying();
    }




    public void OnBackToMainMenu()
    {
        if (string.IsNullOrEmpty(mainMenuSceneName))
        {
            Debug.LogError("[MainMenuManager] mainMenuSceneName is empty.");
            return;
        }
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // --- Helpers ---
    private void UpdateHighScoreText(int score)
    {
        if (highScoreText)
            highScoreText.text = $"High Score: {score}";
    }
}

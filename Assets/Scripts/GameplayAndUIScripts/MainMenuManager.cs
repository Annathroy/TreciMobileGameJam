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
        if (am != null)
        {
            am.EnsureMusicPlaying();

            // Initialize slider without triggering OnValueChanged
            if (volumeSlider)
                volumeSlider.SetValueWithoutNotify(am != null ? am.musicVolume : 1f);
        }
        else
        {
            Debug.LogWarning("[MainMenuManager] AudioManager missing in the scene. " +
                             "Place it in the first loaded scene (DontDestroyOnLoad).");
            if (volumeSlider) volumeSlider.SetValueWithoutNotify(1f);
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
        SceneManager.LoadScene(gameplaySceneName);
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
        var am = AudioManager.Instance;
        if (am != null)
        {
            am.SetMusicVolume(value);           // persists internally (PlayerPrefs)
        }
        else
        {
            Debug.LogWarning("[MainMenuManager] AudioManager not found; volume change ignored.");
        }
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

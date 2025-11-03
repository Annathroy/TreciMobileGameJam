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

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource; // assign main AudioSource
    [SerializeField] private Slider volumeSlider;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI highScoreText;

    private const string HIGH_SCORE_KEY = "HighScore";
    private const string VOLUME_KEY = "Volume";

    private void Awake()
    {
        // Panel defaults
        if (startMenuPanel) startMenuPanel.SetActive(true);
        if (optionsPanel) optionsPanel.SetActive(false);

        // --- Load saved volume ---
        float savedVol = PlayerPrefs.GetFloat(VOLUME_KEY, 1f);
        if (audioSource) audioSource.volume = savedVol;
        if (volumeSlider) volumeSlider.SetValueWithoutNotify(savedVol);

        // --- Load and display high score ---
        int savedScore = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
        UpdateHighScoreText(savedScore);
    }

    // --- UI Button Hooks ---

    public void OnStartGame()
    {
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

        // Refresh text in case it changed while in options
        int score = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
        UpdateHighScoreText(score);
    }

    public void OnResetHighScore()
    {
        PlayerPrefs.DeleteKey(HIGH_SCORE_KEY);
        PlayerPrefs.Save();
        UpdateHighScoreText(0);
        Debug.Log("High score reset.");
    }

    public void OnVolumeChanged(float value)
    {
        if (audioSource) audioSource.volume = value;
        PlayerPrefs.SetFloat(VOLUME_KEY, value);
        PlayerPrefs.Save();
    }

    public void OnBackToMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // --- Helpers ---
    private void UpdateHighScoreText(int score)
    {
        if (highScoreText)
            highScoreText.text = $"High Score: {score}";
    }
}

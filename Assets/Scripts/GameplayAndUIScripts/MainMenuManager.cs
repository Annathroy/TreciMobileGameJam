using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Panels")]
    [SerializeField] private GameObject startMenuPanel;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private GameObject howToPlayPanel;

    [Header("How To Play UI")]
    [SerializeField] private Button howToPlayBackButton; // assign the Back button here

    [Header("Audio (via AudioManager)")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Toggle musicToggle;
    [SerializeField] private Toggle sfxToggle;

    private float lastMusicVolume = 0.8f;
    private float lastSfxVolume = 0.8f;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI highScoreText;

    private const string HIGH_SCORE_KEY = "HighScore";

    private void Awake()
    {
        ShowPanel(startMenuPanel);
        UpdateHighScoreText(PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0));

        // Auto-wire the Back button so Inspector miswiring can’t break it
        if (howToPlayBackButton != null)
        {
            howToPlayBackButton.onClick.RemoveAllListeners();
            howToPlayBackButton.onClick.AddListener(OnBackToStartMenuPanel);
        }

        var am = AudioManager.Instance;
        if (am != null)
        {
            am.EnsureMusicPlaying();
            if (am.musicVolume <= 0.001f) am.SetMusicVolume(0.8f);
            if (am.sfxVolume <= 0.001f) am.SetSfxVolume(0.8f);

            volumeSlider?.SetValueWithoutNotify(am.musicVolume);
            sfxVolumeSlider?.SetValueWithoutNotify(am.sfxVolume);

            musicToggle?.SetIsOnWithoutNotify(am.musicVolume > 0f);
            sfxToggle?.SetIsOnWithoutNotify(am.sfxVolume > 0f);

            lastMusicVolume = am.musicVolume > 0f ? am.musicVolume : 0.8f;
            lastSfxVolume = am.sfxVolume > 0f ? am.sfxVolume : 0.8f;
        }
        else
        {
            volumeSlider?.SetValueWithoutNotify(0.8f);
            sfxVolumeSlider?.SetValueWithoutNotify(0.8f);
            Debug.LogWarning("[MainMenuManager] AudioManager not found in scene.");
        }

        // Slider/toggle wiring
        if (volumeSlider)
        {
            volumeSlider.minValue = 0f; volumeSlider.maxValue = 1f; volumeSlider.wholeNumbers = false;
            volumeSlider.onValueChanged.RemoveAllListeners();
            volumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }
        if (sfxVolumeSlider)
        {
            sfxVolumeSlider.minValue = 0f; sfxVolumeSlider.maxValue = 1f; sfxVolumeSlider.wholeNumbers = false;
            sfxVolumeSlider.onValueChanged.RemoveAllListeners();
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }
        if (musicToggle)
        {
            musicToggle.onValueChanged.RemoveAllListeners();
            musicToggle.onValueChanged.AddListener(OnMusicToggleChanged);
        }
        if (sfxToggle)
        {
            sfxToggle.onValueChanged.RemoveAllListeners();
            sfxToggle.onValueChanged.AddListener(OnSfxToggleChanged);
        }
    }

    // Centralized panel switching
    private void ShowPanel(GameObject target)
    {
        if (startMenuPanel) startMenuPanel.SetActive(target == startMenuPanel);
        if (optionsPanel) optionsPanel.SetActive(target == optionsPanel);
        if (howToPlayPanel) howToPlayPanel.SetActive(target == howToPlayPanel);
    }

    // --- UI Hooks ---
    public void OnOpenOptions() => ShowPanel(optionsPanel);
    public void OnOpenHowToPlay() => ShowPanel(howToPlayPanel);

    // Keep both names alive so old buttons still work
    public void OnBackToStartMenuPanel() => BackToStartMenuPanel();
    public void OnBackToStartMenu() => BackToStartMenuPanel();

    private void BackToStartMenuPanel()
    {
        ShowPanel(startMenuPanel);
        UpdateHighScoreText(PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0));
    }

    public void OnStartGame()
    {
        Score.ResetRun();
        SceneManager.LoadScene("MislavTestScene");
    }

    public void OnResetHighScore()
    {
        PlayerPrefs.DeleteKey(HIGH_SCORE_KEY);
        PlayerPrefs.Save();
        UpdateHighScoreText(0);
    }

    public void OnMusicVolumeChanged(float v)
    {
        var am = AudioManager.Instance; if (am == null) return;
        am.SetMusicVolume(v);
        musicToggle?.SetIsOnWithoutNotify(v > 0f);
        if (v > 0f) lastMusicVolume = v;
        am.EnsureMusicPlaying();
    }

    public void OnSfxVolumeChanged(float v)
    {
        var am = AudioManager.Instance; if (am == null) return;
        am.SetSfxVolume(v);
        sfxToggle?.SetIsOnWithoutNotify(v > 0f);
        if (v > 0f) lastSfxVolume = v;
    }

    public void OnMusicToggleChanged(bool on)
    {
        var am = AudioManager.Instance; if (am == null) return;
        if (on) { am.SetMusicVolume(lastMusicVolume); volumeSlider?.SetValueWithoutNotify(lastMusicVolume); am.EnsureMusicPlaying(); }
        else { lastMusicVolume = am.musicVolume > 0f ? am.musicVolume : lastMusicVolume; am.SetMusicVolume(0f); volumeSlider?.SetValueWithoutNotify(0f); }
    }

    public void OnSfxToggleChanged(bool on)
    {
        var am = AudioManager.Instance; if (am == null) return;
        if (on) { am.SetSfxVolume(lastSfxVolume); sfxVolumeSlider?.SetValueWithoutNotify(lastSfxVolume); }
        else { lastSfxVolume = am.sfxVolume > 0f ? am.sfxVolume : lastSfxVolume; am.SetSfxVolume(0f); sfxVolumeSlider?.SetValueWithoutNotify(0f); }
    }

    public void LoadMainMenuScene()
    {
        if (string.IsNullOrEmpty(mainMenuSceneName))
        {
            Debug.LogError("[MainMenuManager] mainMenuSceneName is empty.");
            return;
        }
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void UpdateHighScoreText(int score)
    {
        if (highScoreText) highScoreText.text = $"{score}";
    }
}

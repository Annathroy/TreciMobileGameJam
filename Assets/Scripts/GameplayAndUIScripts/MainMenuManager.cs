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

    [Header("Audio (via AudioManager)")]
    [SerializeField] private Slider volumeSlider;     // Music volume (0..1)
    [SerializeField] private Slider sfxVolumeSlider;  // SFX volume (0..1)
    [SerializeField] private Toggle musicToggle;      // Toggle for music on/off
    [SerializeField] private Toggle sfxToggle;        // Toggle for SFX on/off

    private float lastMusicVolume = 0.8f;  // Store last non-zero volume
    private float lastSfxVolume = 0.8f;    // Store last non-zero volume

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI highScoreText;

    private const string HIGH_SCORE_KEY = "HighScore";

    private void Awake()
    {
        // Panels
        if (startMenuPanel) startMenuPanel.SetActive(true);
        if (optionsPanel) optionsPanel.SetActive(false);

        // High score (from PlayerPrefs; Score updates this key)
        UpdateHighScoreText(PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0));

        // Sliders
        if (volumeSlider)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.wholeNumbers = false;
            volumeSlider.onValueChanged.RemoveAllListeners();
            volumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }
        if (sfxVolumeSlider)
        {
            sfxVolumeSlider.minValue = 0f;
            sfxVolumeSlider.maxValue = 1f;
            sfxVolumeSlider.wholeNumbers = false;
            sfxVolumeSlider.onValueChanged.RemoveAllListeners();
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }

        // Toggles
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

        // Init from AudioManager
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
    }

    // --- UI Button Hooks ---

    public void OnStartGame()
    {
        if (string.IsNullOrEmpty("MislavTestScene"))
        {
            Debug.LogError("[MainMenuManager] gameplaySceneName is empty.");
            return;
        }

        // Start a fresh session score (does not affect HighScore)
        Score.ResetRun();

        SceneManager.LoadScene("MislavTestScene");
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

        UpdateHighScoreText(PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0));
    }

    public void OnResetHighScore()
    {
        PlayerPrefs.DeleteKey(HIGH_SCORE_KEY);
        PlayerPrefs.Save();
        UpdateHighScoreText(0);
        Debug.Log("High score reset.");
        // Example: AudioManager.Instance?.PlaySFX("click");
    }

    // Slider handlers
    public void OnMusicVolumeChanged(float value)
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        am.SetMusicVolume(value);
        musicToggle?.SetIsOnWithoutNotify(value > 0f);
        if (value > 0f) lastMusicVolume = value;
        am.EnsureMusicPlaying(); // if something stopped it, resume
    }

    public void OnSfxVolumeChanged(float value)
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        am.SetSfxVolume(value);
        sfxToggle?.SetIsOnWithoutNotify(value > 0f);
        if (value > 0f) lastSfxVolume = value; // persists; affects next SFX plays
    }

    public void OnMusicToggleChanged(bool isOn)
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        if (isOn)
        {
            am.SetMusicVolume(lastMusicVolume);
            volumeSlider?.SetValueWithoutNotify(lastMusicVolume);
            am.EnsureMusicPlaying();
        }
        else
        {
            lastMusicVolume = am.musicVolume > 0f ? am.musicVolume : lastMusicVolume;
            am.SetMusicVolume(0f);
            volumeSlider?.SetValueWithoutNotify(0f);
        }
    }

    public void OnSfxToggleChanged(bool isOn)
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        if (isOn)
        {
            am.SetSfxVolume(lastSfxVolume);
            sfxVolumeSlider?.SetValueWithoutNotify(lastSfxVolume);
        }
        else
        {
            lastSfxVolume = am.sfxVolume > 0f ? am.sfxVolume : lastSfxVolume;
            am.SetSfxVolume(0f);
            sfxVolumeSlider?.SetValueWithoutNotify(0f);
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
        if (highScoreText) highScoreText.text = $"{score}";
    }
}

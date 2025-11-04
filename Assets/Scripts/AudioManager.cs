using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    // ===== Singleton =====
    public static AudioManager Instance { get; private set; }

    [Header("Mixer (optional)")]
    public AudioMixerGroup musicMixer;
    public AudioMixerGroup sfxMixer;

    [Header("Single Music Track")]
    [Tooltip("This one looped track will play in ALL scenes (menu + game).")]
    public AudioClip musicClip;

    [Header("Music Settings")]
    [Range(0f, 1f)] public float musicVolume = 0.8f;
    [Tooltip("Autoplay the music clip when the first scene loads.")]
    public bool autoPlayOnStart = true;

    [Header("SFX Settings")]
    [Range(0f, 1f)] public float sfxVolume = 1.0f;
    [Min(1)] public int sfxPoolSize = 10;

    [Serializable]
    public class SfxDef
    {
        public string id;
        public AudioClip clip;
        [Range(0f, 1f)] public float baseVolume = 1f;
        [Range(0.5f, 2f)] public float pitchMin = 1f;
        [Range(0.5f, 2f)] public float pitchMax = 1f;
    }

    [Header("Registered SFX")]
    public List<SfxDef> sfx = new();

    // ===== Internals =====
    private readonly Dictionary<string, SfxDef> _sfxMap = new();
    private AudioSource _music;
    private readonly List<AudioSource> _sfxPool = new();

    private const string PP_MusicVol = "Audio.MusicVol";
    private const string PP_SfxVol = "Audio.SfxVol";

    void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Build SFX map
        _sfxMap.Clear();
        foreach (var def in sfx)
        {
            if (def?.clip == null || string.IsNullOrWhiteSpace(def.id)) continue;
            _sfxMap[def.id.ToLowerInvariant()] = def;
        }

        // Music source (single)
        _music = gameObject.AddComponent<AudioSource>();
        _music.playOnAwake = false;
        _music.loop = true;          // single looping track
        _music.spatialBlend = 0f;
        _music.outputAudioMixerGroup = musicMixer;

        // SFX pool
        for (int i = 0; i < sfxPoolSize; i++)
            _sfxPool.Add(CreateSfxSource());

        // Load saved vols
        if (PlayerPrefs.HasKey(PP_MusicVol)) musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PP_MusicVol));
        if (PlayerPrefs.HasKey(PP_SfxVol)) sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PP_SfxVol));

        // Apply and optionally start music
        SetMusicVolume(musicVolume);
        if (autoPlayOnStart && musicClip != null && !_music.isPlaying)
        {
            _music.clip = musicClip;
            _music.volume = musicVolume;
            _music.Play();
        }
    }

    private AudioSource CreateSfxSource()
    {
        var go = new GameObject("SFX_Source", typeof(AudioSource));
        go.transform.SetParent(transform, false);
        var src = go.GetComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 0f;
        src.outputAudioMixerGroup = sfxMixer;
        return src;
    }

    // ===== Public API =====

    /// <summary>Ensure the single music track is playing (loops).</summary>
    public void EnsureMusicPlaying()
    {
        if (_music.clip == null && musicClip != null)
            _music.clip = musicClip;

        if (_music.clip != null && !_music.isPlaying)
        {
            _music.volume = musicVolume;
            _music.Play();
        }
    }

    /// <summary>Stops the single music track.</summary>
    public void StopMusic()
    {
        if (_music.isPlaying) _music.Stop();
    }

    /// <summary>Replace the music clip at runtime (still only one track plays).</summary>
    public void SetMusicClip(AudioClip newClip, bool playImmediately = true, float startTimeSeconds = 0f)
    {
        musicClip = newClip;
        _music.Stop();
        _music.clip = newClip;

        if (playImmediately && newClip != null)
        {
            _music.time = Mathf.Clamp(startTimeSeconds, 0f, newClip.length - 0.001f);
            _music.volume = musicVolume;
            _music.Play();
        }
    }

    /// <summary>Set music volume [0..1] and persist.</summary>
    public void SetMusicVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);

        // Persist
        PlayerPrefs.SetFloat("Audio.MusicVol", musicVolume);
        PlayerPrefs.Save();

        if (_music != null)
        {
            _music.volume = musicVolume;

            // If something stopped playback, auto-recover when volume > 0
            if (_music.clip != null && !_music.isPlaying && musicVolume > 0f)
                _music.Play();
        }
    }


    /// <summary>Set SFX volume [0..1] and persist.</summary>
    public void SetSfxVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(PP_SfxVol, sfxVolume);
        PlayerPrefs.Save();
        // Applied per play
    }

    /// <summary>Play registered SFX by id (2D one-shot with pitch variance).</summary>
    public void PlaySFX(string id, float volumeMul = 1f)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        if (!_sfxMap.TryGetValue(id.ToLowerInvariant(), out var def) || def.clip == null) return;

        var src = RentSfxSource();
        src.pitch = UnityEngine.Random.Range(def.pitchMin, def.pitchMax);
        src.volume = sfxVolume * def.baseVolume * Mathf.Clamp01(volumeMul);
        src.clip = def.clip;
        src.Play();
        StartCoroutine(ReturnWhenFinished(src));
    }

    /// <summary>Play arbitrary SFX clip.</summary>
    public void PlaySFX(AudioClip clip, float volumeMul = 1f, float pitch = 1f)
    {
        if (clip == null) return;
        var src = RentSfxSource();
        src.pitch = pitch;
        src.volume = sfxVolume * Mathf.Clamp01(volumeMul);
        src.clip = clip;
        src.Play();
        StartCoroutine(ReturnWhenFinished(src));
    }

    // ===== Helpers =====
    private AudioSource RentSfxSource()
    {
        for (int i = 0; i < _sfxPool.Count; i++)
            if (!_sfxPool[i].isPlaying) return _sfxPool[i];

        var extra = CreateSfxSource();
        _sfxPool.Add(extra);
        return extra;
    }

    private System.Collections.IEnumerator ReturnWhenFinished(AudioSource src)
    {
        while (src != null && src.isPlaying) yield return null;
        if (src == null) yield break;
        src.clip = null;
        src.pitch = 1f;
        src.volume = sfxVolume;
    }
}



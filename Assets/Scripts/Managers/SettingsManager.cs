using UnityEngine;
using UnityEngine.Audio;

namespace FreeWorld.Managers
{
    /// <summary>
    /// Persists across scenes. Stores and applies player preferences:
    /// mouse sensitivity, master/sfx/music volume, graphics quality.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        // ── PlayerPrefs keys ──────────────────────────────────────────────────
        private const string K_SENSITIVITY = "MouseSensitivity";
        private const string K_MASTER_VOL  = "VolMaster";
        private const string K_SFX_VOL     = "VolSFX";
        private const string K_MUSIC_VOL   = "VolMusic";
        private const string K_QUALITY     = "Quality";
        private const string K_FULLSCREEN  = "Fullscreen";

        [Header("Audio Mixer (optional)")]
        [SerializeField] private AudioMixer audioMixer;

        // ── Current values ────────────────────────────────────────────────────
        public float MouseSensitivity { get; private set; } = 200f;
        public float MasterVolume     { get; private set; } = 1f;
        public float SFXVolume        { get; private set; } = 1f;
        public float MusicVolume      { get; private set; } = 0.6f;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            Load();
        }

        // ── Save / Load ───────────────────────────────────────────────────────
        public void Load()
        {
            MouseSensitivity = PlayerPrefs.GetFloat(K_SENSITIVITY, 200f);
            MasterVolume     = PlayerPrefs.GetFloat(K_MASTER_VOL,  1f);
            SFXVolume        = PlayerPrefs.GetFloat(K_SFX_VOL,     1f);
            MusicVolume      = PlayerPrefs.GetFloat(K_MUSIC_VOL,   0.6f);

            int quality    = PlayerPrefs.GetInt(K_QUALITY, QualitySettings.GetQualityLevel());
            bool fullscreen = PlayerPrefs.GetInt(K_FULLSCREEN, 1) == 1;

            QualitySettings.SetQualityLevel(quality);
            Screen.fullScreen = fullscreen;

            ApplyAudio();
        }

        public void Save()
        {
            PlayerPrefs.SetFloat(K_SENSITIVITY, MouseSensitivity);
            PlayerPrefs.SetFloat(K_MASTER_VOL,  MasterVolume);
            PlayerPrefs.SetFloat(K_SFX_VOL,     SFXVolume);
            PlayerPrefs.SetFloat(K_MUSIC_VOL,   MusicVolume);
            PlayerPrefs.SetInt(K_QUALITY,        QualitySettings.GetQualityLevel());
            PlayerPrefs.SetInt(K_FULLSCREEN,     Screen.fullScreen ? 1 : 0);
            PlayerPrefs.Save();
        }

        // ── Setters (called by UI sliders) ────────────────────────────────────
        public void SetSensitivity(float value)
        {
            MouseSensitivity = Mathf.Clamp(value, 10f, 600f);
            // Live-apply to any active PlayerCamera
            var cam = FindObjectOfType<Player.PlayerCamera>();
            cam?.SetSensitivity(MouseSensitivity);
        }

        public void SetMasterVolume(float value)
        {
            MasterVolume = Mathf.Clamp01(value);
            ApplyAudio();
        }

        public void SetSFXVolume(float value)
        {
            SFXVolume = Mathf.Clamp01(value);
            ApplyAudio();
        }

        public void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            ApplyAudio();
        }

        public void SetQuality(int index)
        {
            QualitySettings.SetQualityLevel(index);
        }

        public void SetFullscreen(bool value)
        {
            Screen.fullScreen = value;
        }

        // ── Audio Mixer helper ────────────────────────────────────────────────
        private void ApplyAudio()
        {
            if (audioMixer == null) return;
            // AudioMixer volumes are in dB: 0 = silence, 0dB = full
            audioMixer.SetFloat("MasterVol", LinearToDb(MasterVolume));
            audioMixer.SetFloat("SFXVol",    LinearToDb(SFXVolume));
            audioMixer.SetFloat("MusicVol",  LinearToDb(MusicVolume));
        }

        private static float LinearToDb(float linear)
            => linear > 0.0001f ? 20f * Mathf.Log10(linear) : -80f;
    }
}

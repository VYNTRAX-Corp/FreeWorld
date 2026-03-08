using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

namespace FreeWorld.Managers
{
    /// <summary>
    /// Drives the main menu scene.
    /// Attach to a MainMenu Canvas. All UI references are wired in
    /// Inspector (or auto-built by FreeWorldSetup).
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject creditsPanel;
        [SerializeField] private GameObject loadingPanel;

        [Header("Loading Screen")]
        [SerializeField] private TextMeshProUGUI loadingPercentText;
        [SerializeField] private TextMeshProUGUI loadingStatusText;
        [SerializeField] private Slider          loadingBar;

        // ── Settings controls ─────────────────────────────────────────────────
        [Header("Settings Widgets")]
        [SerializeField] private Slider            sensitivitySlider;
        [SerializeField] private Slider            masterVolSlider;
        [SerializeField] private Slider            sfxVolSlider;
        [SerializeField] private Slider            musicVolSlider;
        [SerializeField] private TMPro.TMP_Dropdown qualityDropdown;
        [SerializeField] private Toggle            fullscreenToggle;

        [Header("Game Scene")]
        [SerializeField] private string gameSceneName = "MainLevel";

        // ─────────────────────────────────────────────────────────────────────
        private void Start()
        {
            ShowMain();
            PopulateSettings();
            Time.timeScale = 1f;   // ensure time runs (could be paused from prev session)
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }

        // ── Button callbacks ──────────────────────────────────────────────────
        public void OnPlay()
        {
            StartCoroutine(LoadGameAsync());
        }

        private IEnumerator LoadGameAsync()
        {
            // Show loading panel, hide main
            mainPanel?.SetActive(false);
            if (loadingPanel != null) loadingPanel.SetActive(true);

            var statusMessages = new[]
            {
                "INITIALIZING NEURAL LINK...",
                "LOADING COMBAT PROTOCOLS...",
                "SYNCING WEAPON DATABASE...",
                "CALIBRATING TARGET SYSTEMS...",
                "DECRYPTING SECTOR MAPS...",
                "DEPLOYMENT READY"
            };
            int msgIndex = 0;

            var op = SceneManager.LoadSceneAsync(gameSceneName);
            if (op == null)
            {
                Debug.LogError($"[MainMenu] Scene '{gameSceneName}' not found in Build Settings!");
                mainPanel?.SetActive(true);
                loadingPanel?.SetActive(false);
                yield break;
            }
            op.allowSceneActivation = false;

            float displayProgress = 0f;
            while (!op.isDone)
            {
                // LoadSceneAsync goes 0→0.9 loading, then 0.9→1.0 on activation
                float targetProgress = Mathf.Clamp01(op.progress / 0.9f) * 100f;
                displayProgress = Mathf.MoveTowards(displayProgress, targetProgress, Time.unscaledDeltaTime * 60f);

                if (loadingBar          != null) loadingBar.value      = displayProgress / 100f;
                if (loadingPercentText  != null) loadingPercentText.text = $"{displayProgress:F1}%";

                int newMsg = Mathf.FloorToInt((displayProgress / 100f) * (statusMessages.Length - 1));
                if (newMsg != msgIndex)
                {
                    msgIndex = newMsg;
                    if (loadingStatusText != null) loadingStatusText.text = statusMessages[msgIndex];
                }

                if (op.progress >= 0.9f)
                {
                    displayProgress = 100f;
                    if (loadingBar         != null) loadingBar.value       = 1f;
                    if (loadingPercentText != null) loadingPercentText.text = "100.0%";
                    if (loadingStatusText  != null) loadingStatusText.text  = statusMessages[^1];
                    yield return new WaitForSecondsRealtime(0.6f);
                    op.allowSceneActivation = true;
                }

                yield return null;
            }
        }

        public void OnSettings()
        {
            mainPanel?.SetActive(false);
            settingsPanel?.SetActive(true);
        }

        public void OnCredits()
        {
            mainPanel?.SetActive(false);
            creditsPanel?.SetActive(true);
        }

        public void OnBack()
        {
            SaveSettings();
            settingsPanel?.SetActive(false);
            creditsPanel?.SetActive(false);
            ShowMain();
        }

        public void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ── Settings ──────────────────────────────────────────────────────────
        private void PopulateSettings()
        {
            var s = SettingsManager.Instance;
            if (s == null) return;

            if (sensitivitySlider != null) sensitivitySlider.value = s.MouseSensitivity;
            if (masterVolSlider   != null) masterVolSlider.value   = s.MasterVolume;
            if (sfxVolSlider      != null) sfxVolSlider.value      = s.SFXVolume;
            if (musicVolSlider    != null) musicVolSlider.value     = s.MusicVolume;
            if (fullscreenToggle  != null) fullscreenToggle.isOn    = Screen.fullScreen;

            if (qualityDropdown != null)
            {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(
                    QualitySettings.names));
                qualityDropdown.value = QualitySettings.GetQualityLevel();
            }

            // Wire live callbacks
            sensitivitySlider?.onValueChanged.AddListener(v => s.SetSensitivity(v));
            masterVolSlider?.onValueChanged.AddListener(v   => s.SetMasterVolume(v));
            sfxVolSlider?.onValueChanged.AddListener(v      => s.SetSFXVolume(v));
            musicVolSlider?.onValueChanged.AddListener(v    => s.SetMusicVolume(v));
            qualityDropdown?.onValueChanged.AddListener(v   => s.SetQuality(v));
            fullscreenToggle?.onValueChanged.AddListener(v  => s.SetFullscreen(v));
        }

        private void SaveSettings() => SettingsManager.Instance?.Save();

        private void ShowMain()
        {
            mainPanel?.SetActive(true);
            settingsPanel?.SetActive(false);
            creditsPanel?.SetActive(false);
        }
    }
}

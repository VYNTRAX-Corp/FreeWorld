using System.Collections;
using System.Collections.Generic;
using FreeWorld.Utilities;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FreeWorld.Managers
{
    /// <summary>
    /// Drives all HUD elements: health bar, armor, ammo counter,
    /// crosshair, score, round info, kill feed, hit marker.
    ///
    /// Wire up the serialized fields by dragging Canvas children in Inspector.
    /// Requires TextMeshPro (install via Package Manager → TMP Essentials).
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        // ── Health / Armor ────────────────────────────────────────────────────
        [Header("Health & Armor")]
        [SerializeField] private Slider      healthBar;
        [SerializeField] private Slider      armorBar;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI armorText;

        // ── Ammo ──────────────────────────────────────────────────────────────
        [Header("Ammo HUD")]
        [SerializeField] private TextMeshProUGUI ammoText;       // "30 / 180"
        [SerializeField] private TextMeshProUGUI weaponNameText;
        [SerializeField] private GameObject      reloadIndicator;

        // ── Crosshair ─────────────────────────────────────────────────────────
        [Header("Crosshair")]
        [SerializeField] private RectTransform   crosshairTop;
        [SerializeField] private RectTransform   crosshairBottom;
        [SerializeField] private RectTransform   crosshairLeft;
        [SerializeField] private RectTransform   crosshairRight;
        [SerializeField] private Image           hitMarker;
        [SerializeField] private float           hitMarkerDuration = 0.15f;
        [SerializeField] private float           crosshairBaseSpread = 9f;
        [SerializeField] private float           crosshairSpreadSpeed = 8f;

        // ── Score / Round ─────────────────────────────────────────────────────
        [Header("Score & Round")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI roundText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI killCountText;
        [SerializeField] private TextMeshProUGUI enemiesText;

        // ── Grenade counter ───────────────────────────────────────────────
        [Header("Grenades")]
        [SerializeField] private TextMeshProUGUI grenadeCountText;
        // ── Kill Feed ─────────────────────────────────────────────────────────
        [Header("Kill Feed")]
        [SerializeField] private Transform       killFeedContainer;
        [SerializeField] private GameObject      killFeedEntryPrefab;
        [SerializeField] private int             maxKillFeedEntries = 5;
        [SerializeField] private float           killFeedEntryDuration = 4f;

        // ── Screens ───────────────────────────────────────────────────────────
        [Header("Overlay Screens")]
        [SerializeField] private GameObject pauseScreen;
        [SerializeField] private GameObject gameOverScreen;
        [SerializeField] private GameObject roundEndScreen;
        [SerializeField] private TextMeshProUGUI gameOverScoreText;

        // ── Damage Flash ──────────────────────────────────────────────────────
        [Header("Damage Flash")]
        [SerializeField] private Image  damageFlash;
        [SerializeField] private float  damageFlashDuration = 0.3f;

        // ── Internal ──────────────────────────────────────────────────────────
        private float _hitMarkerTimer;
        private float _flashTimer;
        private float _currentSpread;
        private GameState _lastKnownState = (GameState)(-1); // invalid sentinel
        private int   _lastAmmo = -1;

        // ── Vitals bars (runtime-built, survival HUD) ─────────────────────────
        private struct VitalsBarUI
        {
            public RectTransform    fill;
            public TextMeshProUGUI  abbrevLabel;
            public TextMeshProUGUI  valueLabel;
            public float            innerWidth;   // usable fill width in pixels
        }
        private VitalsBarUI _staminaBarUI;
        private VitalsBarUI _hungerBarUI;
        private VitalsBarUI _thirstBarUI;

        // ── Damage Direction Indicators ───────────────────────────────────────
        private const int   _maxArrows     = 4;
        private const float _arrowLifetime = 1.5f;
        private const float _arrowRadius   = 160f;   // px from screen center

        private class DamageArrow
        {
            public Image          img;
            public RectTransform  rt;
            public float          timer;
        }

        private readonly List<DamageArrow> _damageArrows = new List<DamageArrow>();
        private Camera _playerCamera;

        // ── Weapon Carousel ────────────────────────────────────────────────────
        private class WeaponSlotUI
        {
            public RectTransform        root;
            public Image                bg;
            public Image                border;
            public List<Image>          iconParts = new List<Image>();
            public TextMeshProUGUI      label;
            public Weapons.WeaponBase   weapon;   // live reference — always current
        }
        private readonly List<WeaponSlotUI> _weaponSlots = new List<WeaponSlotUI>();

        // ── Singleton ─────────────────────────────────────────────────────────
        public static UIManager Instance { get; private set; }

        // ── Scoreboard ─────────────────────────────────────────────────────
        public Scoreboard Scoreboard { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            Instance   = this;
            Scoreboard = GetComponentInChildren<Scoreboard>(true);

            // Auto-resolve screen references by name if not wired in Inspector
            if (pauseScreen    == null) pauseScreen    = FindChildByName("PauseScreen");
            if (gameOverScreen == null) gameOverScreen = FindChildByName("GameOverScreen");
            if (roundEndScreen == null) roundEndScreen = FindChildByName("RoundEndScreen");
            if (gameOverScoreText == null)
            {
                var go = gameOverScreen != null
                    ? gameOverScreen.transform.Find("GameOverScore")
                    : null;
                if (go != null) gameOverScoreText = go.GetComponent<TextMeshProUGUI>();
            }

            // Build kill feed container at runtime if not wired in inspector
            if (killFeedContainer == null)
            {
                var kfGO = new GameObject("KillFeed");
                kfGO.transform.SetParent(transform, false);
                var kfRT         = kfGO.AddComponent<RectTransform>();
                kfRT.anchorMin   = new Vector2(1f, 0.5f);
                kfRT.anchorMax   = new Vector2(1f, 0.5f);
                kfRT.pivot       = new Vector2(1f, 0.5f);
                kfRT.anchoredPosition = new Vector2(-16f, 0f);
                kfRT.sizeDelta   = new Vector2(330f, 320f);
                var vlg = kfGO.AddComponent<VerticalLayoutGroup>();
                vlg.childAlignment    = TextAnchor.LowerRight;
                vlg.spacing           = 4f;
                vlg.childControlWidth = vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true;
                kfGO.AddComponent<ContentSizeFitter>().verticalFit =
                    ContentSizeFitter.FitMode.PreferredSize;
                killFeedContainer = kfGO.transform;
            }

            // Ensure an EventSystem exists — required for UI button clicks
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Last resort: build panels dynamically if still missing
            // (happens when scene was built before the setup script added them)
            if (pauseScreen    == null) pauseScreen    = BuildRuntimeScreen("PauseScreen",
                new Color(0f,0f,0f,0.85f), BuildPauseScreenContents);
            if (gameOverScreen == null) gameOverScreen = BuildRuntimeScreen("GameOverScreen",
                new Color(0f,0f,0f,0.90f), BuildGameOverScreenContents);
            if (roundEndScreen == null) roundEndScreen = BuildRuntimeScreen("RoundEndScreen",
                new Color(0f,0f,0.05f,0.80f), BuildRoundEndScreenContents);

            HideAllScreens();
        }

        // ── Runtime screen builders ───────────────────────────────────────────
        private GameObject BuildRuntimeScreen(string screenName, Color bg,
            System.Action<Transform> populate)
        {
            var go  = new GameObject(screenName);
            go.transform.SetParent(transform, false);
            var img  = go.AddComponent<Image>();
            img.color = bg;
            img.raycastTarget = true;
            var rt   = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero;

            // Centered card for visual depth
            var card    = new GameObject("Card");
            card.transform.SetParent(go.transform, false);
            var cardImg = card.AddComponent<Image>();
            cardImg.color         = new Color(0.04f, 0.04f, 0.10f, 0.78f);
            cardImg.raycastTarget = false;
            var cardRT  = card.GetComponent<RectTransform>();
            cardRT.anchorMin        = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax        = new Vector2(0.5f, 0.5f);
            cardRT.pivot            = new Vector2(0.5f, 0.5f);
            cardRT.anchoredPosition = Vector2.zero;
            cardRT.sizeDelta        = new Vector2(500f, 560f);

            populate(go.transform);
            go.SetActive(false);
            return go;
        }

        private void BuildPauseScreenContents(Transform parent)
        {
            var title = AddScreenTitle(parent, "PAUSED", new Color(0f, 0.95f, 1f), 80f, 230f);
            title.fontStyle      = FontStyles.Bold;
            title.characterSpacing = 12f;
            title.gameObject.AddComponent<CyberpunkTitle>();
            AddScreenTitle(parent, "— MISSION SUSPENDED —", new Color(0.55f, 0.55f, 0.65f), 18f, 162f);

            var pmc = gameObject.GetComponent<PauseMenu>()
                   ?? gameObject.AddComponent<PauseMenu>();

            AddScreenButton(parent, "RESUME",    new Color(0.05f,0.40f,0.08f),  60f, pmc.OnResume);
            AddScreenButton(parent, "RESTART",   new Color(0.10f,0.14f,0.28f), -20f, pmc.OnRestart);
            AddScreenButton(parent, "MAIN MENU", new Color(0.10f,0.14f,0.28f),-100f, pmc.OnMainMenu);
            AddScreenButton(parent, "QUIT",      new Color(0.40f,0.06f,0.06f),-180f, pmc.OnQuit);
        }

        private void BuildGameOverScreenContents(Transform parent)
        {
            AddScreenTitle(parent, "GAME OVER", new Color(1f,0.18f,0.18f), 72f, 220f);
            AddScreenTitle(parent, "NEURAL LINK SEVERED", new Color(0.6f,0.6f,0.6f), 22f, 152f);

            var scoreTMP = AddScreenTitle(parent, "FINAL SCORE: 0",
                new Color(0.95f,0.85f,0.1f), 30f, 80f);
            gameOverScoreText = scoreTMP;

            var pmc = gameObject.GetComponent<PauseMenu>()
                   ?? gameObject.AddComponent<PauseMenu>();

            AddScreenButton(parent, "RESTART MISSION", new Color(0.05f,0.40f,0.08f),  -20f, pmc.OnRestart);
            AddScreenButton(parent, "MAIN MENU",       new Color(0.10f,0.14f,0.28f), -100f, pmc.OnMainMenu);
        }

        private void BuildRoundEndScreenContents(Transform parent)
        {
            AddScreenTitle(parent, "WAVE CLEARED", new Color(0.9f,0.75f,0.1f), 72f, 40f);
            AddScreenTitle(parent, "PREPARING NEXT WAVE...", new Color(0f,0.95f,1f,0.85f), 22f, -30f);
        }

        private TextMeshProUGUI AddScreenTitle(Transform parent, string text,
            Color color, float fontSize, float yOffset)
        {
            var go  = new GameObject("Title_" + text.Replace(' ','_').Substring(0, Mathf.Min(text.Length,10)));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text           = text;
            tmp.fontSize       = fontSize;
            tmp.fontStyle      = FontStyles.Bold;
            tmp.alignment      = TextAlignmentOptions.Center;
            tmp.color          = color;
            var rt          = go.GetComponent<RectTransform>();
            rt.anchorMin    = new Vector2(0.5f,0.5f);
            rt.anchorMax    = new Vector2(0.5f,0.5f);
            rt.pivot        = new Vector2(0.5f,0.5f);
            rt.anchoredPosition = new Vector2(0f, yOffset);
            rt.sizeDelta        = new Vector2(800f, fontSize + 20f);
            return tmp;
        }

        private void AddScreenButton(Transform parent, string label,
            Color bg, float yOffset, UnityEngine.Events.UnityAction callback)
        {
            var go   = new GameObject($"Btn_{label}");
            go.transform.SetParent(parent, false);
            var img  = go.AddComponent<Image>();
            img.color = bg;
            var btn  = go.AddComponent<Button>();
            btn.onClick.AddListener(callback);
            var rt   = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f,0.5f);
            rt.anchorMax = new Vector2(0.5f,0.5f);
            rt.pivot     = new Vector2(0.5f,0.5f);
            rt.anchoredPosition = new Vector2(0f, yOffset);
            rt.sizeDelta        = new Vector2(380f, 60f);

            var lblGO  = new GameObject("Label");
            lblGO.transform.SetParent(go.transform, false);
            var tmp    = lblGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 22f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;
            var lrt         = lblGO.GetComponent<RectTransform>();
            lrt.anchorMin   = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.sizeDelta   = Vector2.zero;
        }

        // Searches all children (including inactive) for a GO with the given name
        private GameObject FindChildByName(string goName)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name == goName) return t.gameObject;
            return null;
        }

        private void Start()
        {
            SetupHUDStyle();

            // Subscribe in Start so GameManager.Instance is guaranteed to be set
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnScoreChanged   += UpdateScore;
                GameManager.Instance.OnRoundChanged   += UpdateRound;
                GameManager.Instance.OnTimerTick      += UpdateTimer;
                GameManager.Instance.OnStateChanged   += HandleStateChange;
                GameManager.Instance.OnEnemyKilled    += ShowKillFeedEntry;
                GameManager.Instance.OnEnemiesChanged += UpdateEnemiesRemaining;
            }

            // Subscribe to player health events
            Player.PlayerHealth ph = FindObjectOfType<Player.PlayerHealth>();
            if (ph != null)
            {
                ph.OnHealthChanged += UpdateHealth;
                ph.OnArmorChanged  += UpdateArmor;
                ph.OnDamaged       += ShowDamageFlash;
                ph.OnDamagedFrom   += ShowDamageIndicator;
                UpdateHealth(ph.CurrentHealth, 100f);
            }

            // Subscribe to player vitals events
            Player.PlayerVitals pv = FindObjectOfType<Player.PlayerVitals>();
            if (pv != null)
            {
                pv.OnStaminaChanged += UpdateStamina;
                pv.OnHungerChanged  += UpdateHunger;
                pv.OnThirstChanged  += UpdateThirst;
                UpdateStamina(pv.Stamina, 100f);
                UpdateHunger(pv.Hunger,   100f);
                UpdateThirst(pv.Thirst,   100f);
            }

            HideAllScreens();
        }

        private void Update()
        {
            // Handle Escape directly — UIManager owns cursor so it works even without a Player
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (pauseScreen != null && pauseScreen.activeSelf)
                {
                    // Resume
                    pauseScreen.SetActive(false);
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible   = false;
                    GameManager.Instance?.TogglePause();
                }
                else if (GameManager.Instance == null ||
                         GameManager.Instance.CurrentState == GameState.Playing)
                {
                    // Pause
                    HideAllScreens();
                    if (pauseScreen != null) pauseScreen.SetActive(true);
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible   = true;
                    GameManager.Instance?.TogglePause();
                }
            }

            // Sync other state-driven screens (game over, round end) via polling
            if (GameManager.Instance != null)
            {
                var state = GameManager.Instance.CurrentState;
                if (state != _lastKnownState)
                {
                    _lastKnownState = state;
                    if (state != GameState.Paused) // pause handled above
                        HandleStateChange(state);
                }
            }

            // ── Poll timer and kill count every frame — works regardless of event order ──────
            var gm = GameManager.Instance;
            if (gm != null)
            {
                // Always poll — no state guard, so timer updates regardless of event ordering
                if (timerText != null)
                    UpdateTimer(gm.RoundTimer);

                if (killCountText != null)
                    killCountText.text = $"{gm.KillCount} KILLS";
            }

            TickHitMarker();
            TickDamageFlash();
            TickDamageIndicators();
            TickCrosshair();
            TickVitals();
        }

        // Poll vitals every frame — works even if events were missed on startup
        private Player.PlayerVitals _cachedVitals;
        private void TickVitals()
        {
            if (_cachedVitals == null)
                _cachedVitals = FindObjectOfType<Player.PlayerVitals>();

            // Auto-add if the component is absent — fixes existing scenes without re-running Setup
            if (_cachedVitals == null)
            {
                var playerGO = GameObject.FindGameObjectWithTag("Player");
                if (playerGO != null)
                    _cachedVitals = playerGO.AddComponent<Player.PlayerVitals>();
            }

            if (_cachedVitals == null) return;

            SetVitalsBar(_staminaBarUI, _cachedVitals.Stamina / 100f);
            UpdateHunger(_cachedVitals.Hunger,  100f);
            UpdateThirst(_cachedVitals.Thirst,  100f);
        }

        // ── Health / Armor ────────────────────────────────────────────────────
        public void UpdateHealth(float current, float max)
        {
            if (healthBar  != null) healthBar.value  = current / max;
            if (healthText != null) healthText.text  = Mathf.CeilToInt(current).ToString();
        }

        public void UpdateArmor(float current, float max)
        {
            if (armorBar  != null) armorBar.value  = current / max;
            if (armorText != null) armorText.text  = Mathf.CeilToInt(current).ToString();
        }

        // ── Vitals ────────────────────────────────────────────────────────────
        public void UpdateStamina(float current, float max)
        {
            SetVitalsBar(_staminaBarUI, current / max);
        }

        public void UpdateHunger(float current, float max)
        {
            Color c = current > 30f ? new Color(0.95f, 0.65f, 0.15f)
                    : current > 10f ? new Color(1f,    0.35f, 0.10f)
                    :                 new Color(1f,    0.10f, 0.10f);
            SetVitalsBar(_hungerBarUI, current / max, c);
        }

        public void UpdateThirst(float current, float max)
        {
            Color c = current > 30f ? new Color(0.25f, 0.65f, 1.00f)
                    : current > 10f ? new Color(0.90f, 0.40f, 0.10f)
                    :                 new Color(1f,    0.10f, 0.10f);
            SetVitalsBar(_thirstBarUI, current / max, c);
        }

        private static void SetVitalsBar(VitalsBarUI bar, float t, Color? color = null)
        {
            if (bar.fill == null) return;
            float clamped = Mathf.Clamp01(t);
            bar.fill.sizeDelta = new Vector2(bar.innerWidth * clamped, 0f);
            if (color.HasValue)
            {
                var img = bar.fill.GetComponent<UnityEngine.UI.Image>();
                if (img != null) img.color = color.Value;
            }
            if (bar.valueLabel != null)
                bar.valueLabel.text = $"{clamped * 100f:F1}%";
        }

        // ── Ammo ──────────────────────────────────────────────────────────────
        public void UpdateAmmo(int current, int reserve)
        {
            if (ammoText != null)
            {
                Color ammoColor = current > 10 ? Color.white
                                : current >  5 ? new Color(1f, 0.60f, 0.10f)
                                :                new Color(1f, 0.18f, 0.18f);
                ammoText.color = ammoColor;
                ammoText.text  = $"{current}  <size=60%><color=#AAAAAA>/ {reserve}</color></size>";
                if (current != _lastAmmo)
                {
                    _lastAmmo = current;
                    StartCoroutine(PopScale(ammoText.rectTransform));
                }
            }
            if (reloadIndicator != null) reloadIndicator.SetActive(current == 0);
        }

        public void UpdateWeaponName(string name)
        {
            if (weaponNameText != null) weaponNameText.text = name.ToUpper();
        }

        // ── Grenades ──────────────────────────────────────────────────────
        public void ShowGrenadeCount(int count)
        {
            // Auto-create the label if it wasn’t wired in the Inspector
            if (grenadeCountText == null)
            {
                var go = new GameObject("GrenadeCountText");
                go.transform.SetParent(transform, false);
                grenadeCountText = go.AddComponent<TextMeshProUGUI>();
                ApplyHUDStyle(grenadeCountText, 26f, new Color(0.55f, 1f, 0.40f));
                var rt = grenadeCountText.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(180f, 124f);
                rt.sizeDelta        = new Vector2(140f, 36f);
                grenadeCountText.alignment = TextAlignmentOptions.Left;
            }

            grenadeCountText.text  = count > 0
                ? $"[G]  {count}"
                : "[G]  <color=#555>0</color>";
            grenadeCountText.color = count > 0
                ? new Color(0.55f, 1f, 0.40f)
                : new Color(0.45f, 0.45f, 0.45f);
        }

        // ── Score / Round ─────────────────────────────────────────────────────
        public void UpdateScore(int score)
        {
            if (scoreText == null) return;
            scoreText.text = score.ToString("N0");
            StartCoroutine(PopScale(scoreText.rectTransform, 1.40f, 0.22f));
        }

        public void UpdateRound(int current, int total)
        {
            if (roundText != null) roundText.text = $"ROUND {current} / {total}";
        }

        public void UpdateTimer(float seconds)
        {
            if (timerText == null) return;
            float clamped = Mathf.Max(0f, seconds);
            int m = Mathf.FloorToInt(clamped / 60f);
            int s = Mathf.FloorToInt(clamped % 60f);
            timerText.text = $"{m}:{s:00}";
            if (clamped < 30f && clamped > 0f)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 3f);
                timerText.color = new Color(1f, pulse * 0.2f, pulse * 0.2f);
                float sp = 1f + 0.08f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * 2.5f));
                timerText.rectTransform.localScale = new Vector3(sp, sp, 1f);
            }
            else
            {
                timerText.color = Color.white;
                timerText.rectTransform.localScale = Vector3.one;
            }
        }

        public void UpdateKillCount(int kills, int target)
        {
            if (killCountText != null) killCountText.text = $"{kills} / {target}";
        }

        // ── Hit Marker ────────────────────────────────────────────────────────
        public void ShowHitMarker()
        {
            if (hitMarker == null) return;
            hitMarker.enabled = true;
            _hitMarkerTimer   = hitMarkerDuration;

            // Auditory hit confirmation
            ProceduralAudioLibrary.PlayAt(
                ProceduralAudioLibrary.ClipHitMarker,
                Camera.main != null ? Camera.main.transform.position : Vector3.zero, 0.4f);
        }

        private void TickHitMarker()
        {
            if (hitMarker == null || !hitMarker.enabled) return;
            _hitMarkerTimer -= Time.deltaTime;
            if (_hitMarkerTimer <= 0f)
                hitMarker.enabled = false;
        }

        // ── Directional Damage Indicator ──────────────────────────────────────
        public void ShowDamageIndicator(Vector3 hitWorldDir)
        {
            // Lazily cache the player camera
            if (_playerCamera == null)
                _playerCamera = Camera.main;
            if (_playerCamera == null) return;

            // Project both vectors onto XZ plane to get a 2-D compass angle
            Vector3 camFwd   = _playerCamera.transform.forward;
            camFwd.y         = 0f;
            Vector3 fromDir  = hitWorldDir;
            fromDir.y        = 0f;

            // Defend against zero-length vectors (overhead / underfoot shots)
            if (camFwd.sqrMagnitude < 0.001f || fromDir.sqrMagnitude < 0.001f) return;

            camFwd.Normalize();
            fromDir.Normalize();

            // Angle (degrees, clockwise from top of screen = 0)
            float dot   = Vector3.Dot(camFwd, fromDir);
            float cross = camFwd.x * fromDir.z - camFwd.z * fromDir.x;
            float angle = Mathf.Atan2(-cross, -dot) * Mathf.Rad2Deg;  // negate: indicator points AT source

            // Reuse oldest or grab a free slot
            DamageArrow arrow = null;
            foreach (var a in _damageArrows)
                if (a.timer <= 0f) { arrow = a; break; }

            if (arrow == null)
            {
                if (_damageArrows.Count < _maxArrows)
                {
                    arrow = CreateArrow();
                    _damageArrows.Add(arrow);
                }
                else
                {
                    // Reuse the one with least time remaining
                    arrow = _damageArrows[0];
                    for (int i = 1; i < _damageArrows.Count; i++)
                        if (_damageArrows[i].timer < arrow.timer)
                            arrow = _damageArrows[i];
                }
            }

            // Position arrow on a circle around screen center, rotated to angle
            float rad  = angle * Mathf.Deg2Rad;
            float px   = Mathf.Sin(rad) * _arrowRadius;
            float py   = Mathf.Cos(rad) * _arrowRadius;
            arrow.rt.anchoredPosition = new Vector2(px, py);
            arrow.rt.localEulerAngles = new Vector3(0f, 0f, -angle);
            arrow.timer               = _arrowLifetime;
            arrow.img.enabled         = true;
        }

        private DamageArrow CreateArrow()
        {
            // Find or create a Canvas parent
            Canvas canvas = GetComponent<Canvas>() ?? FindObjectOfType<Canvas>();
            if (canvas == null) return null;

            var go = new GameObject("DmgArrow");
            go.transform.SetParent(canvas.transform, false);

            // AddComponent<Image> implicitly creates the RectTransform; fetch it after.
            var img         = go.AddComponent<Image>();
            img.color       = new Color(1f, 0.15f, 0.10f, 0.85f);
            img.raycastTarget = false;

            var rt          = go.GetComponent<RectTransform>();
            rt.anchorMin    = new Vector2(0.5f, 0.5f);
            rt.anchorMax    = new Vector2(0.5f, 0.5f);
            rt.pivot        = new Vector2(0.5f, 0f);   // pivot at bottom = points inward
            rt.sizeDelta    = new Vector2(18f, 42f);   // narrow rectangle

            return new DamageArrow { img = img, rt = rt, timer = 0f };
        }

        private void TickDamageIndicators()
        {
            foreach (var arrow in _damageArrows)
            {
                if (arrow.timer <= 0f) continue;
                arrow.timer -= Time.deltaTime;
                float alpha = Mathf.Clamp01(arrow.timer / _arrowLifetime) * 0.85f;
                arrow.img.color = new Color(1f, 0.15f, 0.10f, alpha);
                if (arrow.timer <= 0f) arrow.img.enabled = false;
            }
        }

        // ── Damage Flash ──────────────────────────────────────────────────────
        public void ShowDamageFlash()
        {
            _flashTimer = damageFlashDuration;
            if (damageFlash != null) damageFlash.enabled = true;
        }

        /// <summary>Brief camera shake triggered by nearby explosion.</summary>
        public void TriggerExplosionShake()
        {
            // Camera shake is handled by PlayerCamera; forward the request if available
            var cam = FindObjectOfType<Player.PlayerCamera>();
            cam?.StartExplosionShake();
        }

        private void TickDamageFlash()
        {
            if (_flashTimer <= 0f || damageFlash == null) return;
            _flashTimer -= Time.deltaTime;
            float alpha = (_flashTimer / damageFlashDuration) * 0.45f;
            damageFlash.color = new Color(1f, 0f, 0f, alpha);
            if (_flashTimer <= 0f) damageFlash.enabled = false;
        }

        // ── Crosshair spread ────────────────────────────────────────────────
        /// <summary>Expand the crosshair by 'amount' pixels (call on each shot).</summary>
        public void AddCrosshairSpread(float amount)
        {
            _currentSpread = Mathf.Min(_currentSpread + amount, crosshairBaseSpread * 4f);
        }

        private void TickCrosshair()
        {
            // Recover toward base spread
            _currentSpread = Mathf.Lerp(_currentSpread, crosshairBaseSpread,
                                         crosshairSpreadSpeed * Time.deltaTime);

            // Expand extra when ADS or moving
            var wm = FindObjectOfType<Weapons.WeaponManager>();
            float spread = _currentSpread;
            if (wm != null && wm.CurrentWeapon != null && wm.CurrentWeapon.IsADS)
                spread = crosshairBaseSpread * 0.3f;   // tighter when aiming

            if (crosshairTop    != null) crosshairTop.anchoredPosition    = new Vector2(0f,  spread);
            if (crosshairBottom != null) crosshairBottom.anchoredPosition = new Vector2(0f, -spread);
            if (crosshairLeft   != null) crosshairLeft.anchoredPosition   = new Vector2(-spread, 0f);
            if (crosshairRight  != null) crosshairRight.anchoredPosition  = new Vector2( spread, 0f);
        }

        // ── Kill Feed ─────────────────────────────────────────────────────────
        public void ShowKillFeedEntry(string enemyType, int score)
        {
            if (killFeedContainer == null) return;

            // Prune oldest entry when at capacity
            while (killFeedContainer.childCount >= maxKillFeedEntries)
                Destroy(killFeedContainer.GetChild(0).gameObject);

            // Container row
            var row    = new GameObject("KF_Row");
            row.transform.SetParent(killFeedContainer, false);
            var rowImg = row.AddComponent<Image>();
            rowImg.color         = new Color(0f, 0f, 0f, 0.45f);
            rowImg.raycastTarget = false;
            var le               = row.AddComponent<LayoutElement>();
            le.preferredHeight   = 28f;

            // Text child
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(row.transform, false);
            var tmp    = textGO.AddComponent<TextMeshProUGUI>();

            Color typeColor = enemyType == "HEAVY" ? new Color(0.40f, 0.60f, 1.00f)
                            : enemyType == "SCOUT" ? new Color(1.00f, 0.90f, 0.15f)
                            : new Color(0.85f, 0.85f, 0.85f);

            tmp.text      = $"<color=#66EE66>YOU</color>  <b>x</b>  " +
                            $"<color=#{ColorToHex(typeColor)}>{enemyType}</color>  " +
                            $"<color=#FFD700>+{score}</color>";
            tmp.fontSize  = 13f;
            tmp.alignment = TextAlignmentOptions.Right;
            tmp.raycastTarget = false;
            var tRT       = textGO.GetComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
            tRT.offsetMin = new Vector2(8f, 0f);
            tRT.offsetMax = new Vector2(-8f, 0f);

            StartCoroutine(FadeKillEntry(row, tmp, rowImg));
        }

        private IEnumerator FadeKillEntry(GameObject row, TextMeshProUGUI tmp, Image bg)
        {
            yield return new WaitForSecondsRealtime(killFeedEntryDuration - 0.6f);
            float t = 0f;
            while (t < 0.6f && row != null)
            {
                t += Time.unscaledDeltaTime;
                float a = 1f - (t / 0.6f);
                if (bg  != null) bg.color  = new Color(0f, 0f, 0f, 0.45f * a);
                if (tmp != null) tmp.alpha = a;
                yield return null;
            }
            if (row != null) Destroy(row);
        }

        private static string ColorToHex(Color c)
        {
            return $"{ToByte(c.r):X2}{ToByte(c.g):X2}{ToByte(c.b):X2}";
        }
        private static int ToByte(float f) => Mathf.Clamp(Mathf.RoundToInt(f * 255f), 0, 255);

        public void UpdateEnemiesRemaining(int count)
        {
            if (enemiesText == null) return;
            enemiesText.text = $"ENEMIES  <color=#FF6666>{count}</color>";
            StartCoroutine(PopScale(enemiesText.rectTransform, 1.25f, 0.14f));
        }

        // Legacy overload kept for compatibility
        public void AddKillFeedEntry(string killerName, string victimName, string weaponName)
            => ShowKillFeedEntry(victimName, 0);

        // ── Weapon Carousel ────────────────────────────────────────────────────
        public void BuildWeaponCarousel(Weapons.WeaponBase[] weapons)
        {
            foreach (var s in _weaponSlots)
                if (s.root != null) Destroy(s.root.gameObject);
            _weaponSlots.Clear();

            Canvas canvas = GetComponent<Canvas>() ?? FindObjectOfType<Canvas>();
            if (canvas == null) return;

            const float slotW   = 72f;
            const float slotH   = 75f;
            const float spacing = 80f;
            float startX = -((weapons.Length - 1) * spacing) / 2f;

            for (int i = 0; i < weapons.Length; i++)
            {
                var wb = weapons[i];

                // ─ Slot root ─
                var slotGO = new GameObject("WSlot_" + i, typeof(RectTransform));
                slotGO.transform.SetParent(canvas.transform, false);
                var rt             = (RectTransform)slotGO.transform;
                rt.anchorMin       = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot           = new Vector2(0.5f, 0f);
                rt.sizeDelta       = new Vector2(slotW, slotH);
                rt.anchoredPosition = new Vector2(startX + i * spacing, 10f);

                var bg             = slotGO.AddComponent<Image>();
                bg.color           = new Color(0f, 0f, 0f, 0.5f);
                bg.raycastTarget   = false;

                // ─ Border ─
                var borderGO = new GameObject("Border", typeof(RectTransform));
                borderGO.transform.SetParent(slotGO.transform, false);
                var brt      = (RectTransform)borderGO.transform;
                brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
                brt.offsetMin = new Vector2(2f, 2f); brt.offsetMax = new Vector2(-2f, -2f);
                var border   = borderGO.AddComponent<Image>();
                border.color = Color.clear;
                border.raycastTarget = false;

                // ─ Gun icon ─
                var iconContainer = new GameObject("GunIcon", typeof(RectTransform));
                iconContainer.transform.SetParent(slotGO.transform, false);
                var icRT         = (RectTransform)iconContainer.transform;
                icRT.anchorMin   = icRT.anchorMax = new Vector2(0.5f, 0.72f);
                icRT.sizeDelta   = new Vector2(52f, 24f);
                icRT.anchoredPosition = Vector2.zero;

                var iconParts = new List<Image>();
                var wt        = wb != null ? wb.weaponType : Weapons.WeaponType.Rifle;
                BuildGunIconParts(iconContainer.transform, wt, Color.white, iconParts);

                // ─ Key number ─
                var numGO    = new GameObject("Num", typeof(RectTransform));
                numGO.transform.SetParent(slotGO.transform, false);
                var nrt      = (RectTransform)numGO.transform;
                nrt.anchorMin = new Vector2(0f, 0f); nrt.anchorMax = new Vector2(0.4f, 0.28f);
                nrt.offsetMin = nrt.offsetMax = Vector2.zero;
                var numTMP   = numGO.AddComponent<TextMeshProUGUI>();
                numTMP.text  = (i + 1).ToString();
                numTMP.fontSize   = 11f;
                numTMP.alignment  = TextAlignmentOptions.Center;
                numTMP.color      = new Color(1f, 1f, 1f, 0.45f);
                numTMP.raycastTarget = false;

                // ─ Weapon name ─
                var nameGO   = new GameObject("WName", typeof(RectTransform));
                nameGO.transform.SetParent(slotGO.transform, false);
                var lrt      = (RectTransform)nameGO.transform;
                lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 0.28f);
                lrt.offsetMin = lrt.offsetMax = Vector2.zero;
                var label    = nameGO.AddComponent<TextMeshProUGUI>();
                label.text   = wb != null ? wb.weaponName : "---";
                label.fontSize    = 9f;
                label.alignment   = TextAlignmentOptions.Right;
                label.color       = new Color(1f, 1f, 1f, 0.45f);
                label.margin      = new Vector4(0f, 0f, 4f, 0f);
                label.raycastTarget = false;

                _weaponSlots.Add(new WeaponSlotUI
                {
                    root = rt, bg = bg, border = border,
                    iconParts = iconParts, label = label, weapon = wb
                });
            }

            UpdateWeaponCarousel(0);
        }

        private void BuildGunIconParts(Transform parent, Weapons.WeaponType wt,
                                        Color color, List<Image> parts)
        {
            // Each part: (name, cx, cy, w, h) — all in pixels relative to icon container centre
            (string n, float x, float y, float w, float h)[] pieces;

            switch (wt)
            {
                case Weapons.WeaponType.Pistol:
                    pieces = new[]
                    {
                        ("slide",  8f,   4f,  24f,  8f),   // slide / barrel top
                        ("frame", -2f,  -2f,  16f, 10f),   // frame
                        ("grip",  -8f, -10f,   7f, 12f),   // grip
                    };
                    break;
                case Weapons.WeaponType.Shotgun:
                    pieces = new[]
                    {
                        ("barrel",  14f,  6f, 26f, 8f),   // fat barrel
                        ("recv",   -4f,   0f, 22f, 14f),  // thick receiver
                        ("pump",    8f,  -4f, 14f,  5f),  // pump under barrel
                        ("stock", -18f,   0f, 12f,  8f),  // stock
                    };
                    break;
                default: // Rifle / Sniper
                    pieces = new[]
                    {
                        ("barrel",  16f,  7f, 28f,  4f),  // long thin barrel
                        ("recv",   -2f,   1f, 22f, 12f),  // receiver
                        ("mag",     0f,  -8f,  6f, 11f),  // magazine
                        ("stock", -18f,   1f, 12f,  8f),  // stock
                    };
                    break;
            }

            foreach (var (n, cx, cy, w, h) in pieces)
            {
                var go    = new GameObject(n, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var prt   = (RectTransform)go.transform;
                prt.anchorMin  = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
                prt.anchoredPosition = new Vector2(cx, cy);
                prt.sizeDelta  = new Vector2(w, h);
                var img   = go.AddComponent<Image>();
                img.color = color;
                img.raycastTarget = false;
                parts.Add(img);
            }
        }

        public void UpdateWeaponCarousel(int activeIndex)
        {
            for (int i = 0; i < _weaponSlots.Count; i++)
            {
                var s      = _weaponSlots[i];
                bool active = i == activeIndex;

                // Re-derive color and name from the live weapon reference every time
                var wt  = s.weapon != null ? s.weapon.weaponType : Weapons.WeaponType.Rifle;
                var wc  = WeaponTypeColor(wt);
                var wn  = s.weapon != null ? s.weapon.weaponName : "---";

                if (s.bg     != null) s.bg.color     = active
                    ? new Color(0.10f, 0.10f, 0.10f, 0.90f)
                    : new Color(0f,    0f,    0f,    0.40f);

                if (s.border != null) s.border.color = active
                    ? new Color(wc.r, wc.g, wc.b, 0.90f)
                    : Color.clear;

                float partAlpha  = active ? 1.00f : 0.22f;
                Color partColor  = new Color(wc.r, wc.g, wc.b, partAlpha);
                foreach (var img in s.iconParts)
                    if (img != null) img.color = partColor;

                if (s.label != null)
                {
                    s.label.text  = wn;
                    s.label.color = new Color(1f, 1f, 1f, active ? 0.92f : 0.30f);
                }
            }
        }

        private static Color WeaponTypeColor(Weapons.WeaponType wt)
        {
            switch (wt)
            {
                case Weapons.WeaponType.Pistol:  return new Color(0.33f, 0.60f, 1.00f);
                case Weapons.WeaponType.Shotgun: return new Color(1.00f, 0.58f, 0.18f);
                case Weapons.WeaponType.Sniper:  return new Color(0.60f, 0.30f, 1.00f);
                default:                         return new Color(0.28f, 0.85f, 0.42f);
            }
        }

        // ── Screen management ─────────────────────────────────────────────────
        /// <summary>Called by Resume button. Hides the pause screen and restores play state.</summary>
        public void Resume()
        {
            if (pauseScreen != null) pauseScreen.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
            GameManager.Instance?.TogglePause();
        }

        // ── HUD Styling & Animation ───────────────────────────────────────────
        private void SetupHUDStyle()
        {
            // ── Ammo (bottom-right, very large) ──────────────────────────────
            ApplyHUDStyle(ammoText,       56f, Color.white);
            if (ammoText != null)
            {
                var rt = ammoText.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 88f);
                rt.sizeDelta        = new Vector2(270f, 64f);
                ammoText.alignment  = TextAlignmentOptions.Center;
            }
            // ── Grenade counter (just above ammo) ──────────────────────────
            ApplyHUDStyle(grenadeCountText, 26f, new Color(0.55f, 1f, 0.40f));
            if (grenadeCountText != null)
            {
                var rt = grenadeCountText.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(180f, 124f);
                rt.sizeDelta        = new Vector2(140f, 36f);
                grenadeCountText.alignment = TextAlignmentOptions.Left;
            }
            ApplyHUDStyle(weaponNameText, 16f, new Color(0.55f, 0.85f, 1f));
            if (weaponNameText != null)
            {
                var rt = weaponNameText.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 56f);
                rt.sizeDelta        = new Vector2(270f, 28f);
                weaponNameText.alignment = TextAlignmentOptions.Center;
            }

            // ── Timer (top-center, large) ─────────────────────────────────────
            ApplyHUDStyle(timerText, 42f, Color.white);
            if (timerText != null)
            {
                var rt = timerText.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -16f);
                rt.sizeDelta        = new Vector2(220f, 58f);
                timerText.alignment = TextAlignmentOptions.Center;
            }

            // ── Round info (top-center, below timer) ──────────────────────────
            ApplyHUDStyle(roundText, 20f, new Color(0f, 0.88f, 1f));
            if (roundText != null)
            {
                var rt = roundText.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -74f);
                rt.sizeDelta        = new Vector2(320f, 30f);
                roundText.alignment = TextAlignmentOptions.Center;
            }

            // ── Score / kills (top-right) ─────────────────────────────────────
            ApplyHUDStyle(scoreText, 30f, new Color(1f, 0.85f, 0.10f));
            if (scoreText != null)
            {
                var rt = scoreText.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-24f, -20f);
                rt.sizeDelta        = new Vector2(240f, 40f);
                scoreText.alignment = TextAlignmentOptions.Right;
            }
            ApplyHUDStyle(killCountText, 18f, new Color(0.78f, 0.78f, 0.78f));
            if (killCountText != null)
            {
                var rt = killCountText.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-24f, -62f);
                rt.sizeDelta        = new Vector2(240f, 28f);
                killCountText.alignment = TextAlignmentOptions.Right;
            }
            ApplyHUDStyle(enemiesText, 18f, new Color(0.78f, 0.78f, 0.78f));
            if (enemiesText != null)
            {
                var rt = enemiesText.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-24f, -94f);
                rt.sizeDelta        = new Vector2(280f, 28f);
                enemiesText.alignment = TextAlignmentOptions.Right;
            }

            // ── Health / armor (bottom-left) ──────────────────────────────────
            ApplyHUDStyle(healthText, 28f, new Color(0.20f, 1f, 0.20f));
            if (healthText != null)
            {
                var rt = healthText.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(20f, 90f);
                rt.sizeDelta        = new Vector2(150f, 36f);
                healthText.alignment = TextAlignmentOptions.Left;
            }
            ApplyHUDStyle(armorText, 22f, new Color(0.45f, 0.55f, 1f));
            if (armorText != null)
            {
                var rt = armorText.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(20f, 10f);
                rt.sizeDelta        = new Vector2(150f, 30f);
                armorText.alignment = TextAlignmentOptions.Left;
            }
            // Reposition bars to match taller text
            if (healthBar != null)
            {
                var rt = healthBar.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(20f, 74f);
                rt.sizeDelta        = new Vector2(260f, 14f);
            }
            if (armorBar != null)
            {
                var rt = armorBar.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(20f, 38f);
                rt.sizeDelta        = new Vector2(260f, 14f);
            }

            // ── Survival vitals (stamina / hunger / thirst) ───────────────────
            _staminaBarUI = BuildVitalsBar("STA", new Color(0.20f, 0.90f, 0.85f), 178f);
            _hungerBarUI  = BuildVitalsBar("HNG", new Color(0.95f, 0.65f, 0.15f), 158f);
            _thirstBarUI  = BuildVitalsBar("THR", new Color(0.25f, 0.65f, 1.00f), 138f);
        }

        // Builds a thin labelled bar anchored to the bottom-left of the canvas.
        // Layout: [abbrev] [background/fill] [value%]
        private VitalsBarUI BuildVitalsBar(string abbreviation, Color fillColor, float yOffset)
        {
            Canvas canvas = GetComponent<Canvas>() ?? FindObjectOfType<Canvas>();
            if (canvas == null) return default;

            const float barW   = 140f;
            const float barH   = 10f;
            const float labelW = 36f;
            const float valW   = 58f;
            const float startX = 20f;
            const float pad    = 4f;
            // inner fill width = barW minus 2px border on each side
            float innerW = barW - 2f;

            // ── Abbreviation label (left) ─────────────────────────────────────
            var labelGO  = new GameObject($"VBar_{abbreviation}_Abbrev");
            labelGO.transform.SetParent(canvas.transform, false);
            var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
            labelTMP.text          = abbreviation;
            labelTMP.fontSize      = 9f;
            labelTMP.fontStyle     = FontStyles.Bold;
            labelTMP.color         = new Color(0.85f, 0.85f, 0.85f, 0.75f);
            labelTMP.alignment     = TextAlignmentOptions.Right;
            labelTMP.raycastTarget = false;
            var labelRT  = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = labelRT.anchorMax = labelRT.pivot = new Vector2(0f, 0f);
            labelRT.anchoredPosition = new Vector2(startX, yOffset - 1f);
            labelRT.sizeDelta        = new Vector2(labelW, barH + 2f);

            // ── Background ────────────────────────────────────────────────────
            var bgGO  = new GameObject($"VBar_{abbreviation}_BG");
            bgGO.transform.SetParent(canvas.transform, false);
            var bgImg  = bgGO.AddComponent<UnityEngine.UI.Image>();
            bgImg.color         = new Color(0f, 0f, 0f, 0.55f);
            bgImg.raycastTarget = false;
            var bgRT   = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = bgRT.anchorMax = bgRT.pivot = new Vector2(0f, 0f);
            bgRT.anchoredPosition = new Vector2(startX + labelW + pad, yOffset);
            bgRT.sizeDelta        = new Vector2(barW, barH);

            // ── Fill — left-anchored, width driven by sizeDelta.x ─────────────
            var fillGO  = new GameObject($"VBar_{abbreviation}_Fill");
            fillGO.transform.SetParent(bgGO.transform, false);
            var fillImg = fillGO.AddComponent<UnityEngine.UI.Image>();
            fillImg.color         = fillColor;
            fillImg.raycastTarget = false;
            var fillRT  = fillGO.GetComponent<RectTransform>();
            fillRT.pivot          = new Vector2(0f, 0.5f);
            fillRT.anchorMin      = new Vector2(0f, 0f);
            fillRT.anchorMax      = new Vector2(0f, 1f);   // anchored to left edge, height stretches
            fillRT.offsetMin      = new Vector2(1f, 1f);
            fillRT.offsetMax      = new Vector2(0f, -1f);  // height shrunk 2px by offsets
            fillRT.sizeDelta      = new Vector2(innerW, 0f);  // full at start

            // ── Value label (right of bar) ────────────────────────────────────
            var valGO  = new GameObject($"VBar_{abbreviation}_Value");
            valGO.transform.SetParent(canvas.transform, false);
            var valTMP = valGO.AddComponent<TextMeshProUGUI>();
            valTMP.text          = "100.0%";
            valTMP.fontSize      = 9f;
            valTMP.fontStyle     = FontStyles.Bold;
            valTMP.color         = new Color(0.90f, 0.90f, 0.90f, 0.90f);
            valTMP.alignment     = TextAlignmentOptions.Left;
            valTMP.raycastTarget = false;
            var valRT  = valGO.GetComponent<RectTransform>();
            valRT.anchorMin = valRT.anchorMax = valRT.pivot = new Vector2(0f, 0f);
            valRT.anchoredPosition = new Vector2(startX + labelW + pad + barW + pad, yOffset - 1f);
            valRT.sizeDelta        = new Vector2(valW, barH + 2f);

            return new VitalsBarUI
            {
                fill        = fillRT,
                abbrevLabel = labelTMP,
                valueLabel  = valTMP,
                innerWidth  = innerW
            };
        }

        private void ApplyHUDStyle(TextMeshProUGUI t, float fontSize, Color color)
        {
            if (t == null) return;
            t.fontSize     = fontSize;
            t.color        = color;
            t.fontStyle    = FontStyles.Bold;
            t.outlineWidth = 0.22f;
            t.outlineColor = new Color32(0, 0, 0, 220);
        }

        private IEnumerator PopScale(RectTransform rt, float peak = 1.30f, float duration = 0.16f)
        {
            if (rt == null) yield break;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = t / duration;
                float s = Mathf.Lerp(peak, 1f, Mathf.SmoothStep(0f, 1f, p));
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        private void HideAllScreens()
        {
            if (pauseScreen    != null) pauseScreen.SetActive(false);
            if (gameOverScreen != null) gameOverScreen.SetActive(false);
            if (roundEndScreen != null) roundEndScreen.SetActive(false);
        }

        private void HandleStateChange(GameState state)
        {
            HideAllScreens();
            switch (state)
            {
                case GameState.Paused:
                    pauseScreen?.SetActive(true);
                    break;
                case GameState.GameOver:
                    gameOverScreen?.SetActive(true);
                    if (gameOverScoreText != null)
                        gameOverScoreText.text = $"SCORE: {GameManager.Instance.Score:N0}";
                    break;
                case GameState.RoundEnd:
                    roundEndScreen?.SetActive(true);
                    break;
            }
        }
    }
}

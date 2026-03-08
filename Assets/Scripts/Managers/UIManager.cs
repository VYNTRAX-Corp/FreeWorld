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
            // Subscribe in Start so GameManager.Instance is guaranteed to be set
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnScoreChanged  += UpdateScore;
                GameManager.Instance.OnRoundChanged  += UpdateRound;
                GameManager.Instance.OnTimerTick     += UpdateTimer;
                GameManager.Instance.OnStateChanged  += HandleStateChange;
            }

            // Subscribe to player health events
            Player.PlayerHealth ph = FindObjectOfType<Player.PlayerHealth>();
            if (ph != null)
            {
                ph.OnHealthChanged += UpdateHealth;
                ph.OnArmorChanged  += UpdateArmor;
                ph.OnDamaged       += ShowDamageFlash;
                UpdateHealth(ph.CurrentHealth, 100f);
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

            TickHitMarker();
            TickDamageFlash();
            TickCrosshair();
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

        // ── Ammo ──────────────────────────────────────────────────────────────
        public void UpdateAmmo(int current, int reserve)
        {
            if (ammoText != null) ammoText.text = $"{current}  <size=70%>/ {reserve}</size>";
            if (reloadIndicator != null) reloadIndicator.SetActive(current == 0);
        }

        public void UpdateWeaponName(string name)
        {
            if (weaponNameText != null) weaponNameText.text = name.ToUpper();
        }

        // ── Score / Round ─────────────────────────────────────────────────────
        public void UpdateScore(int score)
        {
            if (scoreText != null) scoreText.text = score.ToString("N0");
        }

        public void UpdateRound(int current, int total)
        {
            if (roundText != null) roundText.text = $"ROUND {current} / {total}";
        }

        public void UpdateTimer(float seconds)
        {
            if (timerText == null) return;
            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            timerText.text = $"{m:00}:{s:00}";
            timerText.color = seconds < 15f ? Color.red : Color.white;
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
        }

        private void TickHitMarker()
        {
            if (hitMarker == null || !hitMarker.enabled) return;
            _hitMarkerTimer -= Time.deltaTime;
            if (_hitMarkerTimer <= 0f)
                hitMarker.enabled = false;
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
        public void AddKillFeedEntry(string killerName, string victimName, string weaponName)
        {
            if (killFeedContainer == null || killFeedEntryPrefab == null) return;

            // Trim old entries
            while (killFeedContainer.childCount >= maxKillFeedEntries)
                Destroy(killFeedContainer.GetChild(0).gameObject);

            GameObject entry = Instantiate(killFeedEntryPrefab, killFeedContainer);
            TextMeshProUGUI txt = entry.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
                txt.text = $"<b>{killerName}</b>  [{weaponName}]  <color=red>{victimName}</color>";

            Destroy(entry, killFeedEntryDuration);
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

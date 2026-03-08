using System.Collections;
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
        [SerializeField] private TextMeshProUGUI killCountText;        [SerializeField] private TextMeshProUGUI enemiesText;
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
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
                rt.anchoredPosition = new Vector2(-24f, 78f);
                rt.sizeDelta        = new Vector2(320f, 68f);
                ammoText.alignment  = TextAlignmentOptions.Right;
            }
            ApplyHUDStyle(weaponNameText, 16f, new Color(0.55f, 0.85f, 1f));
            if (weaponNameText != null)
            {
                var rt = weaponNameText.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
                rt.anchoredPosition = new Vector2(-24f, 44f);
                rt.sizeDelta        = new Vector2(320f, 28f);
                weaponNameText.alignment = TextAlignmentOptions.Right;
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

using UnityEngine;
using UnityEngine.UI;

namespace FreeWorld.Enemy
{
    /// <summary>
    /// Floating world-space health bar displayed above each enemy.
    ///
    /// Self-contained — attach to an enemy and call Init(EnemyHealth).
    /// The bar fades in instantly when the enemy takes damage and fades
    /// back out after a short delay if the enemy is at full health.
    /// Color transitions green → yellow → red based on remaining HP %.
    ///
    /// Usage (from EnemyAI.Awake or FreeWorldSetup):
    ///   GetComponent&lt;EnemyHealthBar&gt;()?.Init(GetComponent&lt;EnemyHealth&gt;());
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyHealthBar : MonoBehaviour
    {
        // ── Tuning ────────────────────────────────────────────────────────────
        [Header("Display")]
        [SerializeField] private float yOffset      = 2.6f;    // height above enemy pivot
        [SerializeField] private float barWidth     = 1.0f;
        [SerializeField] private float barHeight    = 0.10f;
        [SerializeField] private float fadeOutDelay = 2.5f;    // seconds at full health before hiding
        [SerializeField] private float fadeSpeed    = 4.0f;

        // ── Runtime refs ──────────────────────────────────────────────────────
        private EnemyHealth   _health;
        private Canvas        _canvas;
        private Image         _bgImage;
        private Image         _fgImage;

        private float  _targetAlpha  = 0f;
        private float  _currentAlpha = 0f;
        private float  _hideTimer;
        private Camera _cam;

        // ─────────────────────────────────────────────────────────────────────
        public void Init(EnemyHealth health)
        {
            _health = health;
            health.OnDamaged += OnDamaged;
            BuildUI();
            SetAlpha(0f);   // hidden at start
        }

        private void Awake()
        {
            // Auto-init when the component is present on the same GameObject
            var h = GetComponent<EnemyHealth>();
            if (h != null) Init(h);
        }

        private void OnDestroy()
        {
            if (_health != null) _health.OnDamaged -= OnDamaged;
        }

        private void Update()
        {
            if (_canvas == null) return;

            // Always face camera
            if (_cam == null) _cam = Camera.main;
            if (_cam != null)
                _canvas.transform.rotation = Quaternion.LookRotation(
                    _canvas.transform.position - _cam.transform.position);

            // Update bar fill and color
            if (_health != null)
            {
                float hp   = _health.CurrentHealth;
                float max  = _health.MaxHealth;
                float pct  = max > 0f ? Mathf.Clamp01(hp / max) : 0f;

                // Resize fill image via rect
                if (_fgImage != null)
                {
                    var rt = _fgImage.rectTransform;
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(pct, 1f);
                    rt.offsetMin = rt.offsetMax = Vector2.zero;

                    // Color: green (1) → yellow (0.5) → red (0)
                    _fgImage.color = Color.Lerp(
                        Color.Lerp(Color.red, Color.yellow, pct * 2f),
                        Color.Lerp(Color.yellow, new Color(0.2f, 1f, 0.2f), (pct - 0.5f) * 2f),
                        pct > 0.5f ? 1f : 0f
                    );
                }
            }

            // Fade timer — hide after delay when at full health
            if (_targetAlpha > 0f)
            {
                _hideTimer -= Time.deltaTime;
                if (_hideTimer <= 0f)
                    _targetAlpha = 0f;
            }

            // Smooth alpha transition
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, _targetAlpha, fadeSpeed * Time.deltaTime);
            SetAlpha(_currentAlpha);
        }

        // ── Events ────────────────────────────────────────────────────────────
        private void OnDamaged()
        {
            _targetAlpha = 1f;
            _hideTimer   = fadeOutDelay;
        }

        // ── UI Construction ───────────────────────────────────────────────────
        private void BuildUI()
        {
            // World-space canvas parented to the enemy
            var cvGO = new GameObject("HealthBarCanvas");
            cvGO.transform.SetParent(transform, false);
            cvGO.transform.localPosition = new Vector3(0f, yOffset, 0f);
            cvGO.transform.localScale    = Vector3.one * 0.01f;  // world-units → small pixels

            _canvas = cvGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            var rt = _canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(barWidth * 100f, barHeight * 100f);  // in "pixel" units

            // Background — dark bar
            var bgGO = new GameObject("BG");
            bgGO.transform.SetParent(cvGO.transform, false);
            _bgImage = bgGO.AddComponent<Image>();
            _bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
            var bgRT = _bgImage.rectTransform;
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;

            // Foreground — coloured fill
            var fgGO = new GameObject("Fill");
            fgGO.transform.SetParent(cvGO.transform, false);
            _fgImage = fgGO.AddComponent<Image>();
            _fgImage.color = new Color(0.2f, 1f, 0.2f);

            // Variant name label — tiny text above the bar (uses Unity built-in font)
            if (_health != null)
            {
                var labelGO = new GameObject("Label");
                labelGO.transform.SetParent(cvGO.transform, false);
                var txt = labelGO.AddComponent<UnityEngine.UI.Text>();
                txt.text      = _health.EnemyTypeName;
                txt.fontSize  = 11;
                txt.alignment = TextAnchor.LowerCenter;
                txt.color     = Color.white;
                txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var lrt = txt.rectTransform;
                lrt.anchorMin = new Vector2(0f, 1f);
                lrt.anchorMax = new Vector2(1f, 1f);
                lrt.pivot     = new Vector2(0.5f, 0f);
                lrt.offsetMin = new Vector2(0f, 0f);
                lrt.offsetMax = new Vector2(0f, 14f);
            }
        }

        private void SetAlpha(float a)
        {
            if (_canvas == null) return;
            a = Mathf.Clamp01(a);
            _canvas.GetComponentsInChildren<Graphic>(true, _graphicsBuffer);
            foreach (var g in _graphicsBuffer)
            {
                var c = g.color;
                c.a   = a;
                g.color = c;
            }
        }

        private readonly System.Collections.Generic.List<Graphic> _graphicsBuffer
            = new System.Collections.Generic.List<Graphic>(8);
    }
}

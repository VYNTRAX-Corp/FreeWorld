using TMPro;
using UnityEngine;

namespace FreeWorld.Utilities
{
    /// <summary>
    /// World-space floating damage number. 
    /// Call DamageNumber.Show() from EnemyHealth.TakeDamage — no prefab needed.
    /// </summary>
    public class DamageNumber : MonoBehaviour
    {
        private static readonly Color ColNormal   = new Color(1f,   0.95f, 0.55f);
        private static readonly Color ColHigh     = new Color(1f,   0.52f, 0.08f);
        private static readonly Color ColCritical = new Color(1f,   0.18f, 0.18f);

        private const float LifeTime  = 1.1f;
        private const float RiseSpeed = 2.2f;

        private TextMeshPro _tmp;
        private float       _timer;
        private Camera      _cam;

        // ── Factory ───────────────────────────────────────────────────────────
        public static void Show(float amount, Vector3 worldPos)
        {
            var go        = new GameObject("DmgNum");
            go.transform.position = worldPos + Vector3.up * 1.4f;

            var tmp           = go.AddComponent<TextMeshPro>();
            int rounded       = Mathf.Max(1, Mathf.RoundToInt(amount));
            tmp.text          = rounded.ToString();
            tmp.fontSize      = amount >= 50f ? 7f : 5f;
            tmp.fontStyle     = FontStyles.Bold;
            tmp.alignment     = TextAlignmentOptions.Center;
            tmp.color         = amount >= 75f ? ColCritical
                              : amount >= 35f ? ColHigh
                              : ColNormal;

            go.AddComponent<DamageNumber>();
        }

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _tmp = GetComponent<TextMeshPro>();
            _cam = Camera.main;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= LifeTime) { Destroy(gameObject); return; }

            // Float upward
            transform.position += Vector3.up * RiseSpeed * Time.deltaTime;

            // Always face the camera
            if (_cam != null)
                transform.LookAt(transform.position + _cam.transform.rotation * Vector3.forward,
                                 _cam.transform.rotation * Vector3.up);

            // Fade out in the last third of lifetime
            float alpha = Mathf.Clamp01(1f - (_timer / LifeTime));
            if (_tmp != null)
            {
                var c  = _tmp.color;
                _tmp.color = new Color(c.r, c.g, c.b, alpha);
            }
        }
    }
}

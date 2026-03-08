using UnityEngine;
using TMPro;

namespace FreeWorld.Managers
{
    /// <summary>
    /// Attach to a TextMeshProUGUI to give it a pulsing cyberpunk colour-cycle effect.
    /// Uses unscaled time so the animation runs while the game is paused.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class CyberpunkTitle : MonoBehaviour
    {
        [SerializeField] private Color colorA       = new Color(0f,   0.95f, 1f,   1f); // cyan
        [SerializeField] private Color colorB       = new Color(0.85f,0f,    1f,   1f); // purple
        [SerializeField] private float cycleSpeed   = 0.9f;   // hue cycle speed
        [SerializeField] private float glowMin      = 0.65f;  // minimum brightness factor
        [SerializeField] private float glowMax      = 1.00f;  // maximum brightness factor
        [SerializeField] private float glowSpeed    = 2.4f;   // glow pulse speed
        [SerializeField] private float scaleAmp     = 0.018f; // subtle size breathe
        [SerializeField] private float scaleSpeed   = 1.8f;

        private TextMeshProUGUI _tmp;
        private Vector3         _baseScale;

        private void Awake()
        {
            _tmp       = GetComponent<TextMeshProUGUI>();
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            float t    = (Mathf.Sin(Time.unscaledTime * cycleSpeed) + 1f) * 0.5f;
            Color base_= Color.Lerp(colorA, colorB, t);

            float glow = Mathf.Lerp(glowMin, glowMax,
                (Mathf.Sin(Time.unscaledTime * glowSpeed) + 1f) * 0.5f);
            _tmp.color = new Color(base_.r * glow, base_.g * glow, base_.b * glow, 1f);

            float s = 1f + Mathf.Sin(Time.unscaledTime * scaleSpeed) * scaleAmp;
            transform.localScale = _baseScale * s;
        }
    }
}

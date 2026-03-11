using UnityEngine;
using UnityEngine.Rendering;

namespace FreeWorld.World
{
    /// <summary>
    /// Day / night cycle.
    ///
    /// Attach to any GameObject (e.g. a "WorldManager" child under Managers).
    /// Assign the scene's Directional Light in the Inspector, or it will
    /// auto-find one named "Sun" on Start.
    ///
    /// Time runs in real-seconds by default:
    ///   dayDurationSeconds = 600  →  a full day every 10 minutes.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        // ── Public accessors ──────────────────────────────────────────────────
        /// <summary>0 = midnight, 0.25 = dawn, 0.5 = noon, 0.75 = dusk</summary>
        public float TimeOfDay { get; private set; } = 0.25f;

        /// <summary>Current hour in 0-24 range, for display.</summary>
        public float Hour => TimeOfDay * 24f;

        public bool IsDay  => TimeOfDay >= 0.24f && TimeOfDay <= 0.76f;
        public bool IsNight => !IsDay;

        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Time")]
        [Tooltip("Real seconds for one full in-game day.")]
        [SerializeField] private float dayDurationSeconds = 300f;
        [SerializeField] [Range(0f, 1f)] private float startTimeOfDay = 0.26f; // ~6 AM

        [Header("References")]
        [SerializeField] private Light sunLight;
        [SerializeField] private Light moonLight;

        [Header("Sun")]
        [SerializeField] private Gradient sunColor = DefaultSunGradient();
        [SerializeField] private AnimationCurve sunIntensity = DefaultSunCurve();

        [Header("Moon")]
        [SerializeField] private Color moonColor     = new Color(0.55f, 0.65f, 0.85f);
        [SerializeField] private float moonIntensity = 0.15f;

        [Header("Ambient Light")]
        [SerializeField] private Gradient ambientColor = DefaultAmbientGradient();
        [SerializeField] private AnimationCurve ambientIntensity = DefaultAmbientCurve();

        [Header("Fog")]
        [SerializeField] private bool  controlFog       = true;
        [SerializeField] private Color dayFogColor      = new Color(0.75f, 0.82f, 0.90f);
        [SerializeField] private Color nightFogColor    = new Color(0.04f, 0.05f, 0.10f);
        [SerializeField] private float dayFogDensity    = 0.002f;
        [SerializeField] private float nightFogDensity  = 0.006f;

        // ── Singleton ────────────────────────────────────────────────────────
        public static DayNightCycle Instance { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            Instance   = this;
            TimeOfDay  = startTimeOfDay;
        }

        private void Start()
        {
            // Auto-find Sun if not assigned
            if (sunLight == null)
            {
                var sun = GameObject.Find("Sun");
                if (sun != null) sunLight = sun.GetComponent<Light>();
            }

            // Create moon light if missing
            if (moonLight == null)
            {
                var moonGO = new GameObject("Moon");
                moonGO.transform.SetParent(transform);
                moonLight = moonGO.AddComponent<Light>();
                moonLight.type    = LightType.Directional;
                moonLight.shadows = LightShadows.None;
            }

            if (controlFog) RenderSettings.fog = true;

            ApplyTime();
        }

        private void Update()
        {
            TimeOfDay = (TimeOfDay + Time.deltaTime / dayDurationSeconds) % 1f;
            ApplyTime();
        }

        // ── Core logic ────────────────────────────────────────────────────────
        private void ApplyTime()
        {
            float t = TimeOfDay;

            // ── Sun rotation: rises in east (y=−90), sets in west (y=90)
            // Full 360° per day on the X axis
            float sunAngle = (t * 360f) - 90f;
            if (sunLight != null)
            {
                sunLight.transform.eulerAngles = new Vector3(sunAngle, -30f, 0f);
                sunLight.color     = sunColor.Evaluate(t);
                sunLight.intensity = sunIntensity.Evaluate(t);

                // Disable sun if below horizon to avoid underground illumination
                bool sunAbove = sunLight.transform.forward.y < 0f;
                sunLight.enabled = sunAbove;
            }

            // ── Moon (opposite the sun)
            if (moonLight != null)
            {
                moonLight.transform.eulerAngles = new Vector3(sunAngle + 180f, -30f, 0f);
                moonLight.color     = moonColor;
                moonLight.intensity = sunLight != null && sunLight.enabled ? 0f : moonIntensity;
                moonLight.enabled   = !sunLight.enabled;
            }

            // ── Ambient
            RenderSettings.ambientLight     = ambientColor.Evaluate(t);
            RenderSettings.ambientIntensity = ambientIntensity.Evaluate(t);

            // ── Fog
            if (controlFog)
            {
                // Blend day/night fog around sunrise (0.25) and sunset (0.75)
                float dayBlend = Mathf.Clamp01(sunIntensity.Evaluate(t) / 1.2f);
                RenderSettings.fogColor   = Color.Lerp(nightFogColor, dayFogColor, dayBlend);
                RenderSettings.fogDensity = Mathf.Lerp(nightFogDensity, dayFogDensity, dayBlend);
            }
        }

        // ── Helpers to set time from outside ─────────────────────────────────
        /// <param name="hour">0-24</param>
        public void SetHour(float hour) => TimeOfDay = Mathf.Repeat(hour / 24f, 1f);

        // ── Default gradient / curve factories ────────────────────────────────
        private static Gradient DefaultSunGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.05f, 0.05f, 0.10f), 0.00f), // midnight
                    new GradientColorKey(new Color(0.90f, 0.45f, 0.15f), 0.23f), // pre-dawn
                    new GradientColorKey(new Color(1.00f, 0.80f, 0.55f), 0.26f), // sunrise
                    new GradientColorKey(new Color(1.00f, 0.97f, 0.88f), 0.50f), // noon
                    new GradientColorKey(new Color(1.00f, 0.75f, 0.40f), 0.74f), // sunset
                    new GradientColorKey(new Color(0.70f, 0.30f, 0.10f), 0.77f), // dusk
                    new GradientColorKey(new Color(0.05f, 0.05f, 0.10f), 1.00f), // midnight
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f),
                });
            return g;
        }

        private static AnimationCurve DefaultSunCurve()
        {
            return new AnimationCurve(
                new Keyframe(0.00f, 0.00f),
                new Keyframe(0.23f, 0.00f), // still dark before dawn
                new Keyframe(0.27f, 0.60f), // sunrise
                new Keyframe(0.50f, 1.20f), // noon peak
                new Keyframe(0.73f, 0.60f), // sunset
                new Keyframe(0.77f, 0.00f), // gone after dusk
                new Keyframe(1.00f, 0.00f)
            );
        }

        private static Gradient DefaultAmbientGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.04f, 0.04f, 0.10f), 0.00f), // night
                    new GradientColorKey(new Color(0.40f, 0.35f, 0.50f), 0.24f), // pre-dawn
                    new GradientColorKey(new Color(0.65f, 0.72f, 0.85f), 0.28f), // morning
                    new GradientColorKey(new Color(0.75f, 0.80f, 0.90f), 0.50f), // midday
                    new GradientColorKey(new Color(0.60f, 0.55f, 0.65f), 0.73f), // evening
                    new GradientColorKey(new Color(0.04f, 0.04f, 0.10f), 0.78f), // night
                    new GradientColorKey(new Color(0.04f, 0.04f, 0.10f), 1.00f),
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f),
                });
            return g;
        }

        private static AnimationCurve DefaultAmbientCurve()
        {
            return new AnimationCurve(
                new Keyframe(0.00f, 0.15f),
                new Keyframe(0.24f, 0.18f),
                new Keyframe(0.50f, 1.00f),
                new Keyframe(0.76f, 0.18f),
                new Keyframe(1.00f, 0.15f)
            );
        }
    }
}

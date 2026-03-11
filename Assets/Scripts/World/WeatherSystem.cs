using System.Collections;
using UnityEngine;

namespace FreeWorld.World
{
    public enum WeatherState
    {
        Clear,
        Overcast,
        Rain,
        Storm,
        Snow,
        Fog
    }

    /// <summary>
    /// Drives weather transitions, particle FX, and ambient adjustments.
    ///
    /// Ticks a new weather decision every <see cref="weatherCheckInterval"/> seconds.
    /// Actual transitions are smoothed over <see cref="transitionDuration"/> seconds.
    ///
    /// Requires <see cref="DayNightCycle"/> and <see cref="BiomeSystem"/> in the scene
    /// (auto-found on Start if not assigned).
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────
        public static WeatherSystem Instance { get; private set; }

        // ── Public state ─────────────────────────────────────────────────────
        public WeatherState Current  { get; private set; } = WeatherState.Clear;
        public float        Intensity { get; private set; } = 0f; // 0-1

        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Timing")]
        [SerializeField] private float weatherCheckInterval = 120f;  // every 2 min decide new weather
        [SerializeField] private float transitionDuration   = 20f;   // seconds to blend

        [Header("Particles")]
        [SerializeField] private ParticleSystem rainParticles;
        [SerializeField] private ParticleSystem snowParticles;

        [Header("Audio")]
        [SerializeField] private AudioSource weatherAudio;
        [SerializeField] private AudioClip   rainClip;
        [SerializeField] private AudioClip   stormClip;
        [SerializeField] private AudioClip   windClip;

        [Header("Fog override")]
        [SerializeField] private float foggyDensity = 0.030f;
        [SerializeField] private Color stormFogColor = new Color(0.35f, 0.37f, 0.40f);

        // ── Private ───────────────────────────────────────────────────────────
        private DayNightCycle _dnc;
        private BiomeSystem   _biome;
        private float         _checkTimer;
        private WeatherState  _target = WeatherState.Clear;
        private float         _transitionTime;
        private float         _startIntensity;
        private float         _targetIntensity;
        private Color         _baseFogColor;
        private float         _baseFogDensity;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            _dnc   = DayNightCycle.Instance  ?? FindObjectOfType<DayNightCycle>();
            _biome = BiomeSystem.Instance     ?? FindObjectOfType<BiomeSystem>();

            _baseFogColor   = RenderSettings.fogColor;
            _baseFogDensity = RenderSettings.fogDensity;

            SetParticles(WeatherState.Clear, 0f);
        }

        private void Update()
        {
            // ── Tick weather decision ─────────────────────────────────────────
            _checkTimer += Time.deltaTime;
            if (_checkTimer >= weatherCheckInterval)
            {
                _checkTimer = 0f;
                DecideWeather();
            }

            // ── Smooth transition ─────────────────────────────────────────────
            if (_transitionTime < transitionDuration)
            {
                _transitionTime += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, _transitionTime / transitionDuration);
                Intensity = Mathf.Lerp(_startIntensity, _targetIntensity, t);

                if (t >= 1f) Current = _target;
            }

            // ── Apply per-frame effects ───────────────────────────────────────
            ApplyEffects();
        }

        // ── Decision logic ────────────────────────────────────────────────────
        private void DecideWeather()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            float bx = player ? player.transform.position.x : 0f;
            float bz = player ? player.transform.position.z : 0f;

            float humidity    = _biome != null ? _biome.GetHumidity(bx, bz)    : 0.5f;
            float temperature = _biome != null ? _biome.GetTemperature(bx, bz) : 0.5f;
            bool  isNight     = _dnc != null && _dnc.IsNight;

            // Roll weather weighted by biome
            float roll = Random.value;

            WeatherState newState;
            float        newIntensity;

            // Temperature (0-1) below 0.3 → snow possible
            bool   cold    = temperature < 0.35f;
            float  rainP   = humidity * 0.6f;        // 0-0.54
            float  stormP  = humidity * 0.25f;       // 0-0.225
            float  snowP   = cold ? humidity * 0.4f : 0f;
            float  fogP    = (isNight ? 0.20f : 0.06f) + humidity * 0.10f;

            // Normalise
            float total = rainP + stormP + snowP + fogP + 0.25f; // 0.25 = clear weight
            rainP  /= total; stormP /= total; snowP /= total; fogP /= total;

            if      (roll < stormP)                  { newState = WeatherState.Storm;    newIntensity = Random.Range(0.6f, 1.0f); }
            else if (roll < stormP + rainP)          { newState = WeatherState.Rain;     newIntensity = Random.Range(0.2f, 0.8f); }
            else if (roll < stormP + rainP + snowP)  { newState = WeatherState.Snow;     newIntensity = Random.Range(0.2f, 0.9f); }
            else if (roll < stormP + rainP + snowP + fogP) { newState = WeatherState.Fog; newIntensity = Random.Range(0.3f, 1.0f); }
            else                                     { newState = WeatherState.Clear;    newIntensity = 0f; }

            TransitionTo(newState, newIntensity);
        }

        // ── Transition ────────────────────────────────────────────────────────
        public void TransitionTo(WeatherState state, float intensity)
        {
            _target          = state;
            _startIntensity  = Intensity;
            _targetIntensity = intensity;
            _transitionTime  = 0f;

            // Immediately swap particles to target (they fade with intensity)
            SetParticles(state, 0f);
        }

        // ── Apply per-frame ───────────────────────────────────────────────────
        private void ApplyEffects()
        {
            // Particles emission rate driven by Intensity
            UpdateParticle(rainParticles,  Current == WeatherState.Rain  || Current == WeatherState.Storm, Intensity, 600f);
            UpdateParticle(snowParticles,  Current == WeatherState.Snow,  Intensity, 300f);

            // Audio
            if (weatherAudio != null)
            {
                AudioClip target = null;
                float vol = Intensity;

                if (Current == WeatherState.Storm)       { target = stormClip ?? rainClip; }
                else if (Current == WeatherState.Rain)   { target = rainClip;  }
                else if (Current == WeatherState.Snow || Current == WeatherState.Overcast)
                                                         { target = windClip;  vol *= 0.4f; }

                if (target != null && weatherAudio.clip != target)
                {
                    weatherAudio.clip = target;
                    weatherAudio.loop = true;
                    weatherAudio.Play();
                }
                else if (target == null && weatherAudio.isPlaying)
                {
                    weatherAudio.Stop();
                }

                weatherAudio.volume = vol;
            }

            // Fog
            if (RenderSettings.fog)
            {
                if (Current == WeatherState.Fog)
                {
                    RenderSettings.fogDensity = Mathf.Lerp(_baseFogDensity, foggyDensity, Intensity);
                }
                else if (Current == WeatherState.Storm)
                {
                    RenderSettings.fogDensity = Mathf.Lerp(_baseFogDensity, foggyDensity * 0.6f, Intensity);
                    RenderSettings.fogColor   = Color.Lerp(_baseFogColor, stormFogColor, Intensity);
                }
            }

            // Overcast / storm dims sun  
            if (Current == WeatherState.Storm || Current == WeatherState.Overcast)
            {
                var sun = DayNightCycle.Instance?.GetComponent<Light>() ??
                          GameObject.Find("Sun")?.GetComponent<Light>();
                if (sun != null)
                    sun.intensity = Mathf.Lerp(sun.intensity, sun.intensity * (1f - Intensity * 0.6f), Time.deltaTime * 0.5f);
            }
        }

        // ── Particle helpers ──────────────────────────────────────────────────
        private static void UpdateParticle(ParticleSystem ps, bool active, float intensity, float maxRate)
        {
            if (ps == null) return;
            if (active && intensity > 0.01f)
            {
                if (!ps.isPlaying) ps.Play();
                var em = ps.emission;
                em.rateOverTime = maxRate * intensity;
            }
            else
            {
                if (ps.isPlaying) ps.Stop();
            }
        }

        private void SetParticles(WeatherState state, float intensity)
        {
            UpdateParticle(rainParticles, state == WeatherState.Rain || state == WeatherState.Storm, intensity, 600f);
            UpdateParticle(snowParticles, state == WeatherState.Snow, intensity, 300f);
        }
    }
}

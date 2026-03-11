using UnityEngine;

namespace FreeWorld.World
{
    public enum BiomeType
    {
        Grassland,
        Forest,
        Desert,
        Tundra,
        Mountains,
        Swamp,
        Ocean
    }

    [System.Serializable]
    public struct BiomeData
    {
        public BiomeType type;
        public Color     ambientTint;    // sky/ambient color override for this biome
        public float     fogDensityMult; // multiplied on top of day/night fog
        public float     temperature;    // °C, used by weather
        public float     humidity;       // 0-1, used by weather
    }

    /// <summary>
    /// Biome map built from two Perlin noise layers: temperature and humidity.
    /// The biome for any world position is read with <see cref="GetBiome(float, float)"/>.
    ///
    /// No MonoBehaviour required — attaches as a singleton spawned from Awake.
    /// </summary>
    public class BiomeSystem : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────
        public static BiomeSystem Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Noise")]
        [SerializeField] private float temperatureScale = 600f;
        [SerializeField] private float humidityScale    = 800f;
        [SerializeField] private int   seed             = 42;

        [Header("Biome Definitions")]
        [SerializeField] private BiomeData[] biomeTable = DefaultBiomes();

        // ── Private offsets ───────────────────────────────────────────────────
        private float _tOx, _tOz, _hOx, _hOz;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            Instance = this;
            // Generate deterministic offsets from seed
            var rng = new System.Random(seed);
            _tOx = (float)(rng.NextDouble() * 10000);
            _tOz = (float)(rng.NextDouble() * 10000);
            _hOx = (float)(rng.NextDouble() * 10000);
            _hOz = (float)(rng.NextDouble() * 10000);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Returns the temperature [0,1] at a world (x,z) position.</summary>
        public float GetTemperature(float worldX, float worldZ)
            => Mathf.PerlinNoise(worldX / temperatureScale + _tOx, worldZ / temperatureScale + _tOz);

        /// <summary>Returns the humidity [0,1] at a world (x,z) position.</summary>
        public float GetHumidity(float worldX, float worldZ)
            => Mathf.PerlinNoise(worldX / humidityScale + _hOx, worldZ / humidityScale + _hOz);

        /// <summary>Returns the <see cref="BiomeType"/> at a world (x,z) position.</summary>
        public BiomeType GetBiome(float worldX, float worldZ)
        {
            float t = GetTemperature(worldX, worldZ);
            float h = GetHumidity(worldX, worldZ);

            // Whittaker-inspired classification
            if (t < 0.20f)             return BiomeType.Tundra;
            if (t < 0.40f && h < 0.3f) return BiomeType.Tundra;
            if (t > 0.75f && h < 0.3f) return BiomeType.Desert;
            if (t > 0.60f && h > 0.7f) return BiomeType.Swamp;
            if (h < 0.25f)             return BiomeType.Desert;
            if (h > 0.65f)             return BiomeType.Forest;
            if (t < 0.50f)             return BiomeType.Mountains;
            return BiomeType.Grassland;
        }

        /// <summary>Returns the full <see cref="BiomeData"/> record for a world position.</summary>
        public BiomeData GetBiomeData(float worldX, float worldZ)
        {
            var type = GetBiome(worldX, worldZ);
            foreach (var b in biomeTable)
                if (b.type == type) return b;
            // Fallback
            return new BiomeData { type = type, fogDensityMult = 1f };
        }

        // ── Defaults ──────────────────────────────────────────────────────────
        private static BiomeData[] DefaultBiomes() => new[]
        {
            new BiomeData { type = BiomeType.Grassland, ambientTint = new Color(0.72f, 0.84f, 0.68f), fogDensityMult = 0.9f,  temperature = 18f,  humidity = 0.55f },
            new BiomeData { type = BiomeType.Forest,    ambientTint = new Color(0.50f, 0.68f, 0.48f), fogDensityMult = 1.4f,  temperature = 14f,  humidity = 0.75f },
            new BiomeData { type = BiomeType.Desert,    ambientTint = new Color(0.92f, 0.82f, 0.58f), fogDensityMult = 0.4f,  temperature = 36f,  humidity = 0.10f },
            new BiomeData { type = BiomeType.Tundra,    ambientTint = new Color(0.78f, 0.88f, 0.96f), fogDensityMult = 0.8f,  temperature = -8f,  humidity = 0.30f },
            new BiomeData { type = BiomeType.Mountains, ambientTint = new Color(0.70f, 0.74f, 0.80f), fogDensityMult = 1.1f,  temperature = 2f,   humidity = 0.45f },
            new BiomeData { type = BiomeType.Swamp,     ambientTint = new Color(0.42f, 0.55f, 0.38f), fogDensityMult = 2.2f,  temperature = 24f,  humidity = 0.90f },
            new BiomeData { type = BiomeType.Ocean,     ambientTint = new Color(0.52f, 0.72f, 0.88f), fogDensityMult = 0.6f,  temperature = 16f,  humidity = 0.85f },
        };
    }
}

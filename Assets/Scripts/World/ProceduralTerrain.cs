using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace FreeWorld.World
{
    /// <summary>
    /// Generates a Unity Terrain at runtime using layered Perlin noise.
    ///
    /// Attach to any GameObject before scene play begins — or let
    /// FreeWorldSetup create a "Terrain" GameObject with this component.
    ///
    /// Texture splatmap layers are auto-assigned if you create and reference
    /// TerrainLayer assets; if none are supplied the terrain still generates
    /// with the default Unity checker texture.
    /// </summary>
    [RequireComponent(typeof(Terrain))]
    [DefaultExecutionOrder(-200)]  // Run before Player, enemies, everything
    public class ProceduralTerrain : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────
        public static ProceduralTerrain Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Dimensions")]
        [SerializeField] private int   worldSize  = 1000;
        [SerializeField] private int   heightmapResolution = 513; // must be 2^n + 1
        [SerializeField] private float maxHeight  = 40f;

        [Header("Noise — Base Height")]
        [SerializeField] private float baseScale      = 400f;
        [SerializeField] private float baseAmplitude  = 1.0f;

        [Header("Noise — Octaves")]
        [SerializeField] private int   octaves        = 5;
        [SerializeField] private float lacunarity     = 2.0f;   // freq multiplier per octave
        [SerializeField] private float persistence    = 0.45f;  // amplitude multiplier per octave

        [Header("Noise — Seed")]
        [SerializeField] private int   seed            = 0;     // 0 = random on play
        [SerializeField] private bool  randomizeSeed   = true;

        [Header("Splatmap / Texturing (optional)")]
        [Tooltip("Layer 0: flat grass, Layer 1: slope/dirt, Layer 2: high rock, Layer 3: snow peaks")]
        [SerializeField] private TerrainLayer[] terrainLayers;
        [SerializeField] [Range(0f, 1f)] private float dirtSlopeThreshold  = 0.3f;
        [SerializeField] [Range(0f, 1f)] private float rockSlopeThreshold  = 0.6f;
        [SerializeField] [Range(0f, 1f)] private float snowHeightThreshold = 0.80f; // fraction of maxHeight

        [Header("Trees")]
        [SerializeField] private bool  spawnTrees      = false;
        [SerializeField] private GameObject treePrefab;
        [SerializeField] private int   treeCount       = 500;
        [SerializeField] [Range(0f, 1f)] private float treeMinHeight = 0.05f;
        [SerializeField] [Range(0f, 1f)] private float treeMaxHeight = 0.60f;

        [Header("Flatten player spawn area")]
        [SerializeField] private bool  flattenSpawn    = true;
        [SerializeField] private float flattenRadius   = 200f;  // world units — large enough to run around freely
        [SerializeField] private float flattenHeight   = 0f;    // 0 = terrain base = Y=0, matching the physics cube surface

        // ── Internal ──────────────────────────────────────────────────────────
        private Terrain        _terrain;
        private TerrainData    _data;
        private int            _activeSeed;

        // ── Public accessors ─────────────────────────────────────────────────
        /// <summary>Returns world-space Y position at the given (x,z) point on the terrain.</summary>
        public float GetHeight(float worldX, float worldZ)
            => _terrain != null ? _terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) : 0f;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            Instance = this;
            _terrain = GetComponent<Terrain>();
            GenerateTerrain();
        }

        // ── Main generation entry ─────────────────────────────────────────────
        public void GenerateTerrain()
        {
            _activeSeed = (randomizeSeed || seed == 0) ? Random.Range(1, 999999) : seed;

            // Reuse the existing TerrainData in-place so TerrainCollider stays in sync.
            // Creating a new TerrainData and assigning it only to Terrain leaves the
            // TerrainCollider with stale flat data, causing the player to fall through.
            _data = _terrain.terrainData;
            if (_data == null)
            {
                _data = new TerrainData();
                _terrain.terrainData = _data;
            }

            _data.heightmapResolution = Mathf.Max(heightmapResolution, 33);
            _data.size                = new Vector3(worldSize, maxHeight, worldSize);

            float[,] heights = GenerateHeightmap();

            if (flattenSpawn) FlattenArea(heights, Vector3.zero, flattenRadius, flattenHeight);

            _data.SetHeights(0, 0, heights);

            // Guarantee at least default colour layers so terrain isn't white
            if (terrainLayers == null || terrainLayers.Length == 0)
                terrainLayers = CreateDefaultTerrainLayers();

            ForceMatteTerrainLayers();

            ApplySplatmap(heights);

            // Keep terrain data assigned for rendering first.
            _terrain.terrainData = _data;

            // Terrain collider support is in the optional Terrain Physics package.
            // When missing, we keep the legacy flat ground collider enabled as fallback.
            bool terrainPhysicsAvailable = System.Type.GetType(
                "UnityEngine.TerrainCollider, UnityEngine.TerrainPhysicsModule") != null;

            bool terrainColliderReady = false;
            if (terrainPhysicsAvailable)
            {
                // Refresh terrain data assignment so TerrainCollider picks up latest heights.
                _terrain.terrainData = null;
                _terrain.terrainData = _data;

                var terrainCol = EnsureTerrainColliderComponent();
                terrainColliderReady = terrainCol != null && terrainCol.enabled;

                // If we previously created a fallback mesh collider, remove it now
                // to avoid dual-surface collision artifacts.
                RemoveFallbackColliderMesh();
            }
            else
            {
                // Build a collider mesh from the generated heightmap so gameplay physics
                // still follows hills even without Terrain Physics package.
                BuildFallbackColliderMesh(heights);
                var meshCol = transform.Find("TerrainPhysicsMesh")?.GetComponent<MeshCollider>();
                terrainColliderReady = meshCol != null && meshCol.enabled;

                Debug.LogWarning("[ProceduralTerrain] Terrain Physics package is not installed. " +
                                 "Using fallback TerrainPhysicsMesh collider.");
            }

            // The original setup scene creates a flat "Ground" cube for legacy physics.
            // Only disable it once a terrain collider is confirmed active.
            DisableLegacyGroundPlane(terrainColliderReady);

            // Centre the terrain so world (0,0,0) is mid-map
            transform.position = new Vector3(-worldSize * 0.5f, 0f, -worldSize * 0.5f);

            // Bake NavMesh at runtime so bots can navigate the procedural hills.
            BakeNavMesh();

            if (spawnTrees && treePrefab != null) PlaceTrees(heights);
        }

        private void DisableLegacyGroundPlane(bool terrainColliderReady)
        {
            if (!terrainColliderReady)
            {
                Debug.LogWarning("[ProceduralTerrain] TerrainCollider not ready, keeping legacy Ground collider enabled this run.");
                return;
            }

            var legacyGround = GameObject.Find("Ground");
            if (legacyGround == null) return;

            // Disable rendering so the old flat floor cannot visually fight with terrain textures.
            var renderer = legacyGround.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;

            // Disable flat collision so movement uses TerrainCollider heights.
            var col = legacyGround.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // Prevent baking/using stale navmesh from the legacy flat floor.
            var surface = legacyGround.GetComponent<NavMeshSurface>();
            if (surface != null) surface.enabled = false;
        }

        private Collider EnsureTerrainColliderComponent()
        {
            var terrainColliderType = System.Type.GetType(
                "UnityEngine.TerrainCollider, UnityEngine.TerrainPhysicsModule");
            if (terrainColliderType == null) return null;

            var comp = GetComponent(terrainColliderType) ?? gameObject.AddComponent(terrainColliderType);
            if (comp == null) return null;

            var prop = terrainColliderType.GetProperty("terrainData");
            prop?.SetValue(comp, _data, null);

            if (comp is Collider col)
            {
                col.enabled = true;
                return col;
            }

            return null;
        }

        private void RemoveFallbackColliderMesh()
        {
            var old = transform.Find("TerrainPhysicsMesh");
            if (old != null) DestroyImmediate(old.gameObject);
        }

        private void BuildFallbackColliderMesh(float[,] heights)
        {
            RemoveFallbackColliderMesh();

            const int colRes = 256;
            float stepX = (float)worldSize / colRes;
            float stepZ = (float)worldSize / colRes;
            int hRes = heights.GetLength(0);

            var vertices = new Vector3[(colRes + 1) * (colRes + 1)];
            var triangles = new int[colRes * colRes * 6];

            for (int z = 0; z <= colRes; z++)
            {
                for (int x = 0; x <= colRes; x++)
                {
                    int hx = Mathf.RoundToInt((float)x / colRes * (hRes - 1));
                    int hz = Mathf.RoundToInt((float)z / colRes * (hRes - 1));
                    float y = heights[hz, hx] * maxHeight;
                    vertices[z * (colRes + 1) + x] = new Vector3(x * stepX, y, z * stepZ);
                }
            }

            int t = 0;
            for (int z = 0; z < colRes; z++)
            {
                for (int x = 0; x < colRes; x++)
                {
                    int v = z * (colRes + 1) + x;
                    triangles[t++] = v;
                    triangles[t++] = v + colRes + 1;
                    triangles[t++] = v + 1;

                    triangles[t++] = v + 1;
                    triangles[t++] = v + colRes + 1;
                    triangles[t++] = v + colRes + 2;
                }
            }

            var mesh = new Mesh { name = "TerrainColliderMeshFallback" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();

            var go = new GameObject("TerrainPhysicsMesh");
            go.transform.SetParent(transform, false);
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
        }

        // ── Runtime NavMesh bake ──────────────────────────────────────────────
        /// <summary>
        /// Bakes a NavMesh at runtime over the full terrain so bots can
        /// navigate the procedurally generated hills.
        /// Replaces any NavMesh previously baked on this GameObject.
        /// </summary>
        private void BakeNavMesh()
        {
            // Reuse an existing surface component if present (e.g. re-generation),
            // otherwise add one now.
            var surface = GetComponent<NavMeshSurface>()
                       ?? gameObject.AddComponent<NavMeshSurface>();

            // Collect ALL scene objects so navmesh builds from render meshes and colliders,
            // including fallback TerrainPhysicsMesh when Terrain Physics is absent.
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry    = NavMeshCollectGeometry.RenderMeshes;

            // A voxel size of 0.4 m is a good balance: accurate enough on gentle
            // terrain slopes while keeping bake time short at game start.
            surface.overrideVoxelSize = true;
            surface.voxelSize         = 0.4f;

            surface.BuildNavMesh();
            Debug.Log("[ProceduralTerrain] NavMesh baked over runtime terrain.");
        }

        // ── Height map ────────────────────────────────────────────────────────
        private float[,] GenerateHeightmap()
        {
            int res = _data.heightmapResolution;
            float[,] h = new float[res, res];

            // Seeded offset so each seed produces a unique map
            float ox = _activeSeed * 7.3f;
            float oz = _activeSeed * 3.1f;

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float nx = (float)x / (res - 1);
                    float nz = (float)y / (res - 1);

                    float val        = 0f;
                    float amplitude  = baseAmplitude;
                    float frequency  = 1f;
                    float totalAmp   = 0f;

                    for (int o = 0; o < octaves; o++)
                    {
                        float sx = (nx + ox) * frequency / baseScale * worldSize;
                        float sz = (nz + oz) * frequency / baseScale * worldSize;
                        val      += Mathf.PerlinNoise(sx, sz) * amplitude;
                        totalAmp += amplitude;
                        amplitude *= persistence;
                        frequency *= lacunarity;
                    }

                    // Normalise to [0,1]
                    h[y, x] = val / totalAmp;
                }
            }

            return h;
        }

        // ── Flatten spawn area ───────────────────────────────────────────────
        private void FlattenArea(float[,] heights, Vector3 worldCenter, float radius, float targetHeight)
        {
            int   res          = _data.heightmapResolution;
            float normTarget   = targetHeight / maxHeight;
            float normRadius   = radius / worldSize;

            // worldCenter (0,0,0) → normalised terrain space (0.5, 0.5) because terrain is centred
            float cx = 0.5f;
            float cz = 0.5f;

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float nx   = (float)x / (res - 1);
                    float nz   = (float)y / (res - 1);
                    float dist = Mathf.Sqrt((nx - cx) * (nx - cx) + (nz - cz) * (nz - cz));

                    if (dist < normRadius)
                    {
                        // Hard-flatten within inner half, smooth blend in outer half
                        float blend = Mathf.Clamp01((dist - normRadius * 0.5f) / (normRadius * 0.5f));
                        heights[y, x] = Mathf.Lerp(normTarget, heights[y, x], blend * blend);
                    }
                }
            }
        }

        // ── Default terrain layers (grass / dirt / rock / snow) ──────────────
        private TerrainLayer[] CreateDefaultTerrainLayers()
        {
            return new TerrainLayer[]
            {
                CreateProceduralLayer(new Color(0.30f, 0.50f, 0.18f),
                                      new Color(0.22f, 0.38f, 0.12f), 512, 12f, 0.22f, 20f), // 0 grass
                CreateProceduralLayer(new Color(0.49f, 0.36f, 0.22f),
                                      new Color(0.35f, 0.24f, 0.14f), 512, 10f, 0.28f, 14f), // 1 dirt
                CreateProceduralLayer(new Color(0.50f, 0.50f, 0.48f),
                                      new Color(0.34f, 0.34f, 0.34f), 512, 8f,  0.30f, 10f), // 2 rock
                CreateProceduralLayer(new Color(0.92f, 0.93f, 0.95f),
                                      new Color(0.80f, 0.83f, 0.88f), 512, 15f, 0.16f, 18f), // 3 snow
            };
        }

        private static TerrainLayer CreateProceduralLayer(
            Color baseColor,
            Color altColor,
            int resolution,
            float tileSize,
            float noiseStrength,
            float noiseScale)
        {
            // RGB24 avoids alpha-driven smoothness artifacts in terrain shading.
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGB24, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                name = $"TerrainLayerTex_{baseColor.r:F2}_{baseColor.g:F2}_{baseColor.b:F2}"
            };

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float nx = (float)x / resolution;
                    float ny = (float)y / resolution;

                    // Two octave blend gives visual breakup so terrain no longer looks flat.
                    float n1 = Mathf.PerlinNoise(nx * noiseScale, ny * noiseScale);
                    float n2 = Mathf.PerlinNoise((nx + 11.3f) * (noiseScale * 0.45f),
                                                 (ny + 7.1f)  * (noiseScale * 0.45f));
                    float n = Mathf.Lerp(n1, n2, 0.45f);

                    float t = Mathf.Clamp01((n - 0.5f) * 2f * noiseStrength + 0.5f);
                    Color c = Color.Lerp(baseColor, altColor, t);
                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply(updateMipmaps: true, makeNoLongerReadable: false);

            var layer = new TerrainLayer
            {
                diffuseTexture = tex,
                tileSize = new Vector2(tileSize, tileSize),
                metallic = 0f,
                smoothness = 0.02f
            };
            return layer;
        }

        private void ForceMatteTerrainLayers()
        {
            if (terrainLayers == null) return;
            for (int i = 0; i < terrainLayers.Length; i++)
            {
                var layer = terrainLayers[i];
                if (layer == null) continue;

                layer.metallic = 0f;
                layer.smoothness = 0.02f;
                layer.normalMapTexture = null;
            }
        }

        // ── Splatmap ─────────────────────────────────────────────────────────
        private void ApplySplatmap(float[,] heights)
        {
            _data.terrainLayers = terrainLayers;

            int w      = _data.alphamapWidth;
            int h      = _data.alphamapHeight;
            int layers = terrainLayers.Length;
            float[,,] maps = new float[h, w, layers];

            // Normalised flatten radius (0-1 in terrain UV space).
            // The flatten centre maps to UV (0.5, 0.5) because the terrain is centred.
            float normFlatR = flattenSpawn ? (flattenRadius / worldSize) : 0f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (float)x / (w - 1);
                    float nz = (float)y / (h - 1);

                    // Sample steepness from terrain
                    float steepness = _data.GetSteepness(nx, nz) / 90f; // 0-1
                    float normH     = heights[
                        Mathf.RoundToInt(nz * (_data.heightmapResolution - 1)),
                        Mathf.RoundToInt(nx * (_data.heightmapResolution - 1))];

                    // Layer weights
                    float grass = 0f, dirt = 0f, rock = 0f, snow = 0f;

                    // Distance from terrain centre (UV space); used to lock the arena
                    // floor to pure grass so layers don't bleed/overlap there.
                    float distFromCentre = Mathf.Sqrt((nx - 0.5f) * (nx - 0.5f) +
                                                      (nz - 0.5f) * (nz - 0.5f));

                    // Inside the flatten zone force pure grass — no texture mixing.
                    bool inFlatZone = flattenSpawn && (distFromCentre < normFlatR * 0.85f);

                    if (inFlatZone)
                    {
                        grass = 1f;
                    }
                    else if (layers >= 3)
                    {
                        if (normH >= snowHeightThreshold)
                        {
                            snow = 1f;
                        }
                        else if (steepness >= rockSlopeThreshold)
                        {
                            rock = 1f;
                        }
                        else if (steepness >= dirtSlopeThreshold)
                        {
                            float t2 = (steepness - dirtSlopeThreshold) / (rockSlopeThreshold - dirtSlopeThreshold);
                            rock = t2;
                            dirt = 1f - t2;
                        }
                        else
                        {
                            float t2 = steepness / dirtSlopeThreshold;
                            dirt  = t2;
                            grass = 1f - t2;
                        }

                        maps[y, x, 0] = grass;
                        maps[y, x, 1] = dirt;
                        maps[y, x, 2] = rock;
                        if (layers >= 4) maps[y, x, 3] = snow;
                        continue;
                    }
                    else if (layers == 2)
                    {
                        maps[y, x, 0] = 1f - steepness;
                        maps[y, x, 1] = steepness;
                        continue;
                    }
                    else
                    {
                        maps[y, x, 0] = 1f;
                        continue;
                    }

                    maps[y, x, 0] = grass;
                    maps[y, x, 1] = dirt;
                    maps[y, x, 2] = rock;
                    if (layers >= 4) maps[y, x, 3] = snow;
                }
            }

            _data.SetAlphamaps(0, 0, maps);
        }

        // ── Trees ────────────────────────────────────────────────────────────
        private void PlaceTrees(float[,] heights)
        {
            int res = _data.heightmapResolution;
            Random.InitState(_activeSeed + 1);

            var protoList = new TreePrototype[1];
            protoList[0] = new TreePrototype { prefab = treePrefab };
            _data.treePrototypes = protoList;

            var instances = new System.Collections.Generic.List<TreeInstance>();
            int attempts  = treeCount * 10;

            for (int i = 0; i < attempts && instances.Count < treeCount; i++)
            {
                float nx = Random.value;
                float nz = Random.value;

                float normH = heights[
                    Mathf.RoundToInt(nz * (res - 1)),
                    Mathf.RoundToInt(nx * (res - 1))];

                if (normH < treeMinHeight || normH > treeMaxHeight) continue;

                // Avoid steep slopes for trees
                float steep = _data.GetSteepness(nx, nz) / 90f;
                if (steep > 0.4f) continue;

                var inst = new TreeInstance
                {
                    position         = new Vector3(nx, normH, nz),
                    prototypeIndex   = 0,
                    widthScale       = Random.Range(0.8f, 1.2f),
                    heightScale      = Random.Range(0.8f, 1.3f),
                    color            = Color.white,
                    lightmapColor    = Color.white
                };
                instances.Add(inst);
            }

            _data.treeInstances = instances.ToArray();
        }
    }
}

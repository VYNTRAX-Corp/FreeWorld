// ============================================================
//  FreeWorld — Procedural Texture Generator
//  Generates tiling textures for ground, walls, and crates.
//  Saves PNGs + Materials to Assets/Textures/ and Assets/Materials/
//  Menu: FreeWorld → Setup → 3 - Generate Textures
// ============================================================
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FreeWorld.Editor
{
    public static class TextureGenerator
    {
        private const string TexFolder = "Assets/Textures";
        private const string MatFolder = "Assets/Materials";

        [MenuItem("FreeWorld/Setup/3 - Generate Textures")]
        public static void GenerateAll()
        {
            EnsureFolder(TexFolder);
            EnsureFolder(MatFolder);

            GenerateConcreteTex();
            GenerateBrickTex();
            GenerateWoodTex();
            GenerateMetalTex();

            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            Debug.Log("[FreeWorld] Textures generated in Assets/Textures/");
        }

        // ── Public API so FreeWorldSetup can call these ───────────────────────
        public static Material GetOrCreateMaterial(string matName, string texName,
            Color tint, Vector2 tiling)
        {
            string matPath = $"{MatFolder}/{matName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null) return existing;

            EnsureFolder(TexFolder);
            EnsureFolder(MatFolder);

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolder}/{texName}.png");
            if (tex == null) { GenerateAll(); tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolder}/{texName}.png"); }

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = tint;
            if (tex != null)
            {
                mat.mainTexture      = tex;
                mat.mainTextureScale = tiling;
            }
            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        // ── Texture generators ────────────────────────────────────────────────

        // Concrete tiles: subtle grid lines + noise
        private static void GenerateConcreteTex()
        {
            int size = 256;
            var tex  = new Texture2D(size, size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float noise = Mathf.PerlinNoise(x * 0.04f, y * 0.04f) * 0.12f;
                float base_ = 0.48f + noise;

                // 2-unit tile grid lines
                bool lineX = (x % 64) < 2 || (x % 64) > 62;
                bool lineY = (y % 64) < 2 || (y % 64) > 62;
                float g    = (lineX || lineY) ? base_ * 0.65f : base_;

                tex.SetPixel(x, y, new Color(g, g * 0.97f, g * 0.95f));
            }
            SaveTexture(tex, "T_Concrete");
        }

        // Brick wall: rows of bricks with mortar lines
        private static void GenerateBrickTex()
        {
            int size      = 256;
            int brickW    = 64;
            int brickH    = 28;
            int mortarSize = 4;
            var tex        = new Texture2D(size, size);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int row    = y / (brickH + mortarSize);
                int offset = (row % 2 == 0) ? 0 : brickW / 2;
                int localX = (x + offset) % (brickW + mortarSize);
                int localY = y % (brickH + mortarSize);

                bool mortar = localX > brickW || localY > brickH;

                if (mortar)
                {
                    float g = 0.62f;
                    tex.SetPixel(x, y, new Color(g, g * 0.98f, g * 0.96f));
                }
                else
                {
                    // Brick colour varies per brick for variety
                    float seed  = Mathf.Floor((float)(x + offset) / (brickW + mortarSize))
                                + row * 31f;
                    float vary  = Mathf.Abs(Mathf.Sin(seed * 127.1f)) * 0.12f;
                    float noise = Mathf.PerlinNoise(x * 0.06f, y * 0.06f) * 0.08f;
                    float r     = 0.60f + vary + noise;
                    float gv    = 0.28f + vary * 0.5f + noise;
                    float b     = 0.20f + vary * 0.3f + noise;
                    tex.SetPixel(x, y, new Color(r, gv, b));
                }
            }
            SaveTexture(tex, "T_Brick");
        }

        // Wood planks: horizontal planks with grain
        private static void GenerateWoodTex()
        {
            int size      = 256;
            int plankH    = 40;
            int gapH      = 3;
            var tex        = new Texture2D(size, size);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int localY = y % (plankH + gapH);
                bool gap   = localY >= plankH;

                if (gap)
                {
                    tex.SetPixel(x, y, new Color(0.22f, 0.15f, 0.08f));
                }
                else
                {
                    // Grain: long horizontal noise streaks
                    float grain = Mathf.PerlinNoise(x * 0.008f, y * 0.5f) * 0.18f;
                    float knot  = Mathf.Clamp01(1f - Vector2.Distance(
                        new Vector2(x, localY),
                        new Vector2(size * 0.4f, plankH * 0.5f)) / 18f) * 0.12f;
                    float r = 0.62f + grain + knot;
                    float g = 0.38f + grain * 0.6f + knot * 0.5f;
                    float b = 0.18f + grain * 0.2f;
                    tex.SetPixel(x, y, new Color(r, g, b));
                }
            }
            SaveTexture(tex, "T_Wood");
        }

        // Metal panel: dark panels with rivets and slight sheen
        private static void GenerateMetalTex()
        {
            int size   = 256;
            int panelS = 80;
            var tex    = new Texture2D(size, size);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int lx = x % panelS;
                int ly = y % panelS;

                bool edge = lx < 3 || lx > panelS - 4 || ly < 3 || ly > panelS - 4;

                // Rivets at corners
                bool rivet = Vector2.Distance(new Vector2(lx, ly), new Vector2(6, 6))     < 4f
                          || Vector2.Distance(new Vector2(lx, ly), new Vector2(panelS-6, 6)) < 4f
                          || Vector2.Distance(new Vector2(lx, ly), new Vector2(6, panelS-6)) < 4f
                          || Vector2.Distance(new Vector2(lx, ly), new Vector2(panelS-6, panelS-6)) < 4f;

                float noise = Mathf.PerlinNoise(x * 0.03f, y * 0.03f) * 0.06f;
                float base_ = edge ? 0.28f : (rivet ? 0.55f : 0.40f + noise);
                tex.SetPixel(x, y, new Color(base_, base_ * 1.02f, base_ * 1.05f));
            }
            SaveTexture(tex, "T_Metal");
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static void SaveTexture(Texture2D tex, string name)
        {
            tex.Apply();
            byte[] png  = tex.EncodeToPNG();
            string path = $"{TexFolder}/{name}.png";
            File.WriteAllBytes(path, png);
            Object.DestroyImmediate(tex);
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace('\\', '/');
                string folder = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}

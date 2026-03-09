// ============================================================
//  FreeWorld — Real Texture Downloader
//  Downloads CC0-licensed (public domain) PBR texture sets
//  from ambientCG.com and applies them to scene materials.
//
//  License of downloaded assets: CC0
//  https://ambientcg.com/info#license
//
//  Menu: FreeWorld → Setup → 4 - Download Real Textures (CC0)
// ============================================================
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using UnityEditor;
using UnityEngine;

namespace FreeWorld.Editor
{
    public static class TextureDownloader
    {
        const string TexFolder = "Assets/Textures";
        const string MatFolder = "Assets/Materials";

        // ── ambientCG asset IDs ───────────────────────────────────────────────
        // Browse alternatives at https://ambientcg.com
        // Each row: (ambientCG ID, material file to update, UV tiling, smoothness, metallic, normal strength)
        static readonly (string id, string matName, Vector2 tiling,
                          float smoothness, float metallic, float normalStr)[] Sets =
        {
            ("Concrete034",    "GroundMaterial", new Vector2(20f,  20f), 0.12f, 0.00f, 1.0f),
            ("Bricks057",      "WallMaterial",   new Vector2( 8f,   2f), 0.08f, 0.00f, 1.3f),
            ("WoodFloor040",   "CrateMaterial",  new Vector2( 2f,   2f), 0.22f, 0.00f, 0.8f),
            ("MetalPlates002", "MetalMaterial",  new Vector2( 3f,   3f), 0.65f, 0.75f, 1.0f),
        };

        // ─────────────────────────────────────────────────────────────────────
        [MenuItem("FreeWorld/Setup/4 - Download Real Textures (CC0)")]
        public static void DownloadAll()
        {
            if (!EditorUtility.DisplayDialog(
                "Download CC0 Textures",
                "Downloads ~20 MB of public-domain PBR textures from ambientCG.com " +
                "and applies them to your scene materials.\n\nRequires an internet connection.",
                "Download", "Cancel"))
                return;

            EnsureFolder(TexFolder);
            EnsureFolder(MatFolder);

            int ok = 0;
            for (int i = 0; i < Sets.Length; i++)
            {
                var s = Sets[i];
                EditorUtility.DisplayProgressBar(
                    "FreeWorld — Downloading Textures",
                    $"Downloading {s.id}  ({i + 1} / {Sets.Length})…",
                    (float)i / Sets.Length);

                if (TryDownloadAndApply(s.id, s.matName, s.tiling,
                                        s.smoothness, s.metallic, s.normalStr))
                    ok++;
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();

            string msg = ok == Sets.Length
                ? $"All {ok} texture sets downloaded and applied!\n\nRun Setup Scene to rebuild the arena with real textures."
                : $"{ok} / {Sets.Length} downloads succeeded.\n" +
                  "Failed textures fall back to procedural. Check the Console for details.\n" +
                  "The asset IDs at the top of TextureDownloader.cs can be changed " +
                  "to any ID listed on ambientcg.com.";

            EditorUtility.DisplayDialog("FreeWorld Textures", msg, "OK");
        }

        // ── Per-set download + apply ──────────────────────────────────────────
        static bool TryDownloadAndApply(string acgId, string matName, Vector2 tiling,
                                         float smoothness, float metallic, float normalStr)
        {
            try
            {
                string colorPath  = $"{TexFolder}/RT_{acgId}_Color.png";
                string normalPath = $"{TexFolder}/RT_{acgId}_Normal.png";

                // ambientCG documented download URL — 1K PNG pack
                string url = $"https://ambientcg.com/get?file={acgId}_1K-PNG.zip";
                byte[] zipBytes;
                using (var wc = new WebClient())
                    zipBytes = wc.DownloadData(url);

                bool gotColor = false, gotNormal = false;

                using (var ms  = new MemoryStream(zipBytes))
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Read))
                {
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        string lower = entry.Name.ToLowerInvariant();

                        if (!gotColor && (lower.Contains("color") || lower.Contains("colour")))
                        {
                            ExtractEntry(entry, colorPath);
                            gotColor = true;
                        }
                        else if (!gotNormal && lower.Contains("normalgl"))
                        {
                            ExtractEntry(entry, normalPath);
                            gotNormal = true;
                        }

                        if (gotColor && gotNormal) break;
                    }
                }

                if (!gotColor)
                {
                    Debug.LogWarning($"[FreeWorld] No Color map found in {acgId} ZIP.");
                    return false;
                }

                // Import color texture
                AssetDatabase.ImportAsset(colorPath, ImportAssetOptions.ForceUpdate);

                // Import and mark normal map
                if (gotNormal)
                {
                    AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceUpdate);
                    var imp = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                    if (imp != null)
                    {
                        imp.textureType = TextureImporterType.NormalMap;
                        imp.SaveAndReimport();
                    }
                }

                // Load or create the target material
                string matPath = $"{MatFolder}/{matName}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                    ?? Shader.Find("Standard"))
                    {
                        name = matName
                    };
                    AssetDatabase.CreateAsset(mat, matPath);
                }

                // Apply textures + PBR values
                var colorTex  = AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath);
                var normalTex = gotNormal
                    ? AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath)
                    : null;

                mat.SetTexture("_BaseMap", colorTex);
                mat.SetTextureScale("_BaseMap", tiling);
                mat.mainTextureScale = tiling;    // fallback for Standard shader
                mat.SetFloat("_Smoothness", smoothness);
                mat.SetFloat("_Metallic",   metallic);

                if (normalTex != null)
                {
                    mat.EnableKeyword("_NORMALMAP");
                    mat.SetTexture("_BumpMap",  normalTex);
                    mat.SetFloat("_BumpScale",  normalStr);
                }

                EditorUtility.SetDirty(mat);
                Debug.Log($"[FreeWorld] Applied real PBR texture: {acgId} → {matName}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FreeWorld] Could not download {acgId}: {ex.Message}");
                return false;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        static void ExtractEntry(ZipArchiveEntry entry, string destPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            using var src  = entry.Open();
            using var dest = File.Create(destPath);
            src.CopyTo(dest);
        }

        static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
                string folder = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FreeWorld.Audio;
using UnityEditor;
using UnityEngine;

namespace FreeWorld.Editor
{
    /// <summary>
    /// Editor window: FreeWorld → Setup → 6 - Set Up Gun Sounds (CC0)
    ///
    /// Workflow:
    ///   1. Download CC0 gun sounds from the links listed in this window.
    ///   2. Drop .wav / .ogg / .mp3 files into Assets/Audio/Weapons/ and Assets/Audio/Player/
    ///      following the naming convention listed below.
    ///   3. Click "Auto-Assign Clips" — the tool will:
    ///        • Set optimal import settings (Vorbis compression, normalize, etc.)
    ///        • Create or update Assets/Audio/WeaponAudioBank.asset
    ///        • Assign all discovered clips to the correct bank fields
    ///
    /// ── Naming convention ─────────────────────────────────────────────────────
    ///   Assets/Audio/Weapons/
    ///     rifle_shoot_*.wav          → RifleShoot[]
    ///     rifle_reload*.wav          → RifleReload[]
    ///     pistol_shoot_*.wav         → PistolShoot[]
    ///     pistol_reload*.wav         → PistolReload[]
    ///     shotgun_shoot_*.wav        → ShotgunShoot[]
    ///     shotgun_reload*.wav        → ShotgunReload[]
    ///     empty*.wav  / dry_fire*.wav → DryFire[]
    ///     draw*.wav                  → WeaponDraw[]
    ///     enemy_shoot_*.wav          → EnemyShoot[]
    ///     enemy_alert*.wav           → EnemyAlert[]
    ///     enemy_death*.wav           → EnemyDeath[]
    ///     enemy_hurt*.wav            → EnemyHurt[]
    ///   Assets/Audio/Player/
    ///     footstep_*.wav             → Footstep[]
    ///     player_hurt_*.wav          → PlayerHurt[]
    ///     bullet_impact_*.wav        → BulletImpact[]
    ///     flesh_hit_*.wav            → FleshHit[]
    /// </summary>
    public class AudioImportSetup : EditorWindow
    {
        internal const string WeaponsDir = "Assets/Audio/Weapons";
        internal const string PlayerDir  = "Assets/Audio/Player";
        // Must live in a Resources folder so Resources.Load<WeaponAudioBank>("WeaponAudioBank") works at runtime
        internal const string BankPath   = "Assets/Resources/WeaponAudioBank.asset";

        private Vector2 _scroll;

        [MenuItem("FreeWorld/Setup/6 - Set Up Gun Sounds (CC0)")]
        public static void ShowWindow()
        {
            var w = GetWindow<AudioImportSetup>("FW: Gun Sounds");
            w.minSize = new Vector2(480, 640);
            w.Show();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Space(8);

            // ── Header ────────────────────────────────────────────────────────
            GUIStyle title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            GUILayout.Label("Step 1 — Download Real Gun Sounds (Free)", title);
            GUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "All sources below contain REAL recorded gunshots from actual firearms.\n" +
                "Licenses allow free commercial use — no royalties required.",
                MessageType.Info);

            GUILayout.Space(6);
            GUILayout.Label("Real firearm recordings — free for commercial use:", EditorStyles.boldLabel);

            DrawSource(
                "Freesound — CC0 Real Gunshots (best source)",
                "Thousands of real recorded shots: AR-15, AK-47, pistols, shotguns, suppressors, " +
                "bolt actions. Filter by CC0 to get only royalty-free clips. " +
                "Search terms: 'gunshot', 'rifle shot', 'pistol shot', 'AK-47', 'AR15 shot'.",
                "https://freesound.org/search/?q=gunshot&f=license%3A%22Creative+Commons+0%22");

            DrawSource(
                "Sonniss GDC Game Audio Bundle (free yearly release)",
                "Professional game audio bundles released free every GDC. Past bundles include " +
                "real weapon foley: assault rifles, handguns, reloads, impacts. Royalty-free for games.",
                "https://sonniss.com/gameaudiogdc");

            DrawSource(
                "99Sounds — Weapons SFX Pack (free)",
                "Dedicated free weapons pack with real recorded pistol, rifle, shotgun, " +
                "mechanical reload, bullet casings, suppressed shots. Free for commercial projects.",
                "https://99sounds.org/free-sound-effects");

            DrawSource(
                "Pixabay — Real Gun Sound Effects (royalty-free)",
                "Royalty-free real gunshot recordings. No attribution needed. Covers " +
                "pistol, rifle, shotgun, suppressed shots, distant echoes.",
                "https://pixabay.com/sound-effects/search/gunshot");

            DrawSource(
                "ZapSplat — Real Firearms SFX (free account)",
                "Professional library with real recorded guns: handguns, SMGs, rifles, shotguns, " +
                "reloads, dry-fire clicks, shell casings. Free account gets full downloads.",
                "https://www.zapsplat.com/sound-effect-categories/guns-and-weapons");

            DrawSource(
                "Freesound — CC0 Footsteps (concrete / gravel / metal)",
                "Search 'footstep concrete CC0', 'boots footstep CC0'. Use 6-8 variants " +
                "in the Footstep[] array for natural-sounding enemy movement.",
                "https://freesound.org/search/?q=footstep+boots&f=license%3A%22Creative+Commons+0%22");

            GUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "PRO TIP — Layering for realism:\n" +
                "Real guns sound best when layered. Download 2-3 recordings of the same weapon " +
                "from different distances (close mic, mid, room) and put ALL of them in the same " +
                "RifleShoot[] array. Unity will randomly play one per shot, giving natural variation.\n\n" +
                "For a semi-auto pistol: search 'Glock shot CC0' or 'pistol 9mm shot CC0'.\n" +
                "For a rifle:            search 'AR-15 shot CC0' or 'AK47 shot CC0'.\n" +
                "For a shotgun:          search 'pump shotgun blast CC0'.",
                MessageType.None);

            // ── Step 2 ────────────────────────────────────────────────────────
            GUILayout.Space(10);
            GUILayout.Label("Step 2 — Drop Files Into Project", title);
            GUILayout.Space(4);
            EditorGUILayout.HelpBox(
                $"Weapons → {WeaponsDir}/\n" +
                $"Player  → {PlayerDir}/\n\n" +
                "Follow the naming convention — the tool matches filenames by prefix:\n" +
                "  rifle_shoot_01.wav, rifle_shoot_02.wav …\n" +
                "  pistol_shoot_01.wav\n" +
                "  shotgun_shoot_01.wav, shotgun_reload.wav\n" +
                "  empty.wav  (dry-fire click)\n" +
                "  enemy_shoot_01.wav, enemy_alert.wav, enemy_death.wav\n" +
                "  footstep_01.wav … footstep_08.wav\n" +
                "  player_hurt_01.wav, bullet_impact_01.wav, flesh_hit_01.wav",
                MessageType.None);

            // ── Step 3 ────────────────────────────────────────────────────────
            GUILayout.Space(10);
            GUILayout.Label("Step 3 — Auto-Assign", title);
            GUILayout.Space(4);

            EditorGUILayout.HelpBox(
                "Clicking the button below will:\n" +
                "  • Configure import settings on all audio files (Vorbis quality 80, normalised)\n" +
                "  • Create or update Assets/Audio/WeaponAudioBank.asset\n" +
                "  • Assign every matched clip to the correct bank field\n" +
                "  • Weapons and enemies will auto-use bank clips at runtime (procedural fallback still active)",
                MessageType.Info);

            GUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Auto-Assign Clips", GUILayout.Height(38)))
                    RunAutoAssign();

                if (GUILayout.Button("Open WeaponAudioBank", GUILayout.Height(38)))
                    PingOrCreateBank();
            }

            GUILayout.Space(6);
            if (GUILayout.Button("Show Audio Folders in Project"))
            {
                EnsureFolders();
                AssetDatabase.Refresh();
                var obj = AssetDatabase.LoadAssetAtPath<Object>(WeaponsDir);
                if (obj != null) EditorGUIUtility.PingObject(obj);
            }

            EditorGUILayout.EndScrollView();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Core logic
        // ─────────────────────────────────────────────────────────────────────

        private static void RunAutoAssign() => RunAutoAssignPublic();

        public static void RunAutoAssignPublic()
        {
            EnsureFolders();
            AssetDatabase.Refresh();

            // Apply import settings to every audio file in our dirs
            int configured = ConfigureAudioImports(WeaponsDir) + ConfigureAudioImports(PlayerDir);

            AssetDatabase.Refresh();

            // Load or create bank
            var bank = AssetDatabase.LoadAssetAtPath<WeaponAudioBank>(BankPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<WeaponAudioBank>();
                AssetDatabase.CreateAsset(bank, BankPath);
            }

            var so = new SerializedObject(bank);

            // Weapon clips
            AssignByPrefix(so, WeaponsDir, "RifleShoot",    "rifle_shoot");
            AssignByPrefix(so, WeaponsDir, "RifleReload",   "rifle_reload");
            AssignByPrefix(so, WeaponsDir, "PistolShoot",   "pistol_shoot");
            AssignByPrefix(so, WeaponsDir, "PistolReload",  "pistol_reload");
            AssignByPrefix(so, WeaponsDir, "ShotgunShoot",  "shotgun_shoot");
            AssignByPrefix(so, WeaponsDir, "ShotgunReload", "shotgun_reload");
            AssignByPrefix(so, WeaponsDir, "DryFire",       "empty", "dry_fire");
            AssignByPrefix(so, WeaponsDir, "WeaponDraw",    "draw");

            // Enemy clips
            AssignByPrefix(so, WeaponsDir, "EnemyShoot", "enemy_shoot");
            AssignByPrefix(so, WeaponsDir, "EnemyAlert", "enemy_alert");
            AssignByPrefix(so, WeaponsDir, "EnemyDeath", "enemy_death");
            AssignByPrefix(so, WeaponsDir, "EnemyHurt",  "enemy_hurt");

            // Player/environment clips
            AssignByPrefix(so, PlayerDir, "Footstep",     "footstep");
            AssignByPrefix(so, PlayerDir, "PlayerHurt",   "player_hurt");
            AssignByPrefix(so, PlayerDir, "BulletImpact", "bullet_impact");
            AssignByPrefix(so, PlayerDir, "FleshHit",     "flesh_hit");

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();

            int total = CountBankClips(bank);
            Debug.Log($"[AudioImportSetup] Done — configured {configured} audio files, " +
                      $"assigned {total} clips to WeaponAudioBank.");
            EditorUtility.DisplayDialog("FreeWorld Audio Setup",
                $"Configured {configured} audio file(s).\n" +
                $"Assigned {total} clip(s) to WeaponAudioBank.\n\n" +
                "Weapons and enemies will now prefer real audio clips over procedural synthesis.",
                "OK");

            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(bank);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static void AssignByPrefix(SerializedObject so, string dir,
                                           string propertyName, params string[] prefixes)
        {
            if (!AssetDatabase.IsValidFolder(dir)) return;

            var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { dir });
            var found = new List<AudioClip>();

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (prefixes.Any(p => file.StartsWith(p)))
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip != null) found.Add(clip);
                }
            }

            if (found.Count == 0) return;

            found.Sort((a, b) => string.Compare(a.name, b.name,
                System.StringComparison.OrdinalIgnoreCase));

            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[AudioImportSetup] Property '{propertyName}' not found on bank.");
                return;
            }

            prop.ClearArray();
            prop.arraySize = found.Count;
            for (int i = 0; i < found.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = found[i];

            Debug.Log($"[AudioImportSetup] {propertyName}: {found.Count} clip(s) assigned.");
        }

        private static int ConfigureAudioImports(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return 0;

            var guids  = AssetDatabase.FindAssets("t:AudioClip", new[] { folder });
            int count  = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) continue;

                bool changed = false;

                // Default sample settings: Vorbis, quality 80 %, normalise off
                var def = importer.defaultSampleSettings;
                if (def.loadType != AudioClipLoadType.CompressedInMemory)
                { def.loadType = AudioClipLoadType.CompressedInMemory; changed = true; }
                if (def.compressionFormat != AudioCompressionFormat.Vorbis)
                { def.compressionFormat = AudioCompressionFormat.Vorbis; changed = true; }
                if (!Mathf.Approximately(def.quality, 0.8f))
                { def.quality = 0.8f; changed = true; }

                if (changed)
                {
                    importer.defaultSampleSettings = def;
                    importer.SaveAndReimport();
                    count++;
                }
            }
            return count;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Audio"))
                AssetDatabase.CreateFolder("Assets", "Audio");
            if (!AssetDatabase.IsValidFolder(WeaponsDir))
                AssetDatabase.CreateFolder("Assets/Audio", "Weapons");
            if (!AssetDatabase.IsValidFolder(PlayerDir))
                AssetDatabase.CreateFolder("Assets/Audio", "Player");
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
        }

        private static void PingOrCreateBank()
        {
            EnsureFolders();
            var bank = AssetDatabase.LoadAssetAtPath<WeaponAudioBank>(BankPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<WeaponAudioBank>();
                AssetDatabase.CreateAsset(bank, BankPath);
                AssetDatabase.SaveAssets();
            }
            EditorGUIUtility.PingObject(bank);
            Selection.activeObject = bank;
        }

        private static int CountBankClips(WeaponAudioBank b)
        {
            int t = 0;
            t += b.RifleShoot?.Length ?? 0;
            t += b.RifleReload?.Length ?? 0;
            t += b.PistolShoot?.Length ?? 0;
            t += b.PistolReload?.Length ?? 0;
            t += b.ShotgunShoot?.Length ?? 0;
            t += b.ShotgunReload?.Length ?? 0;
            t += b.DryFire?.Length ?? 0;
            t += b.WeaponDraw?.Length ?? 0;
            t += b.EnemyShoot?.Length ?? 0;
            t += b.EnemyAlert?.Length ?? 0;
            t += b.EnemyDeath?.Length ?? 0;
            t += b.EnemyHurt?.Length ?? 0;
            t += b.PlayerHurt?.Length ?? 0;
            t += b.Footstep?.Length ?? 0;
            t += b.BulletImpact?.Length ?? 0;
            t += b.FleshHit?.Length ?? 0;
            return t;
        }

        // ─────────────────────────────────────────────────────────────────────

        private static void DrawSource(string label, string desc, string url)
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(desc, EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(url, EditorStyles.miniTextField,
                    GUILayout.Height(16));
                if (GUILayout.Button("Copy", GUILayout.Width(46), GUILayout.Height(16)))
                {
                    EditorGUIUtility.systemCopyBuffer = url;
                    Debug.Log($"Copied: {url}");
                }
            }
            GUILayout.EndVertical();
            GUILayout.Space(3);
        }
    }

    /// <summary>
    /// Auto-runs RunAutoAssign() whenever audio files are imported into
    /// Assets/Audio/Weapons/ or Assets/Audio/Player/ so clips are wired
    /// to the bank immediately — no manual button press needed.
    /// </summary>
    public class AudioBankPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets,
            string[] movedAssets,    string[] movedFromAssetPaths)
        {
            bool relevant = false;
            foreach (var path in importedAssets)
            {
                if ((path.StartsWith(AudioImportSetup.WeaponsDir) ||
                     path.StartsWith(AudioImportSetup.PlayerDir)) &&
                    (path.EndsWith(".wav") || path.EndsWith(".mp3") || path.EndsWith(".ogg")))
                {
                    relevant = true;
                    break;
                }
            }
            if (!relevant) return;

            // Defer one frame so all assets in this batch are fully imported first
            EditorApplication.delayCall += () =>
            {
                Debug.Log("[FreeWorld] New audio detected — auto-updating WeaponAudioBank...");
                AudioImportSetup.RunAutoAssignPublic();
            };
        }
    }
}
#endif

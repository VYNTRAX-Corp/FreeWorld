#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using FreeWorld.Enemy;

namespace FreeWorld.Editor
{
    /// <summary>
    /// Editor window: FreeWorld → Setup → 5 - Set Up Character Models (CC0)
    ///
    /// Two-step workflow:
    ///   1. Download a CC0 humanoid FBX pack (links provided in the window).
    ///   2. Drop the FBX(s) into Assets/Characters/ and click "Configure Models".
    ///      The tool will:
    ///        • Set the rig to Humanoid and bake a generic Avatar.
    ///        • Create a shared AnimatorController at Assets/Characters/EnemyAnimator.controller
    ///          with the parameters EnemyModelAnimator expects.
    ///        • Log instructions for wiring animation clips to states.
    ///
    /// Recommended CC0 character packs:
    ///   • Quaternius Ultimate Characters (CC0)  — https://quaternius.com/packs/ultimatecharacters.html
    ///   • Kenney Toon Characters 1 (CC0)        — https://kenney.nl/assets/toon-characters-1
    ///   • OpenGameArt "LowPoly Human" (CC0)     — https://opengameart.org/content/simple-characters
    /// </summary>
    public class CharacterModelImporter : EditorWindow
    {
        private const string CharDir        = "Assets/Characters";
        private const string AnimDir         = "Assets/Characters/Animations";
        private const string ControllerPath  = "Assets/Characters/EnemyAnimator.controller";
        private const string ControllerResources = "Assets/Resources/EnemyAnimator.controller"; // runtime load
        private const string PrefabPath     = "Assets/Characters/Swat_Enemy.prefab";
        private const string CharacterFbx   = "Assets/Characters/Swat_Character.fbx";
        private const string UpperBodyMaskPath = "Assets/Characters/UpperBodyMask.mask";

        // Maps blend tree child index → animation FBX filename
        // Order matches AddChild calls in CreateAnimatorController:
        // 0=Idle, 1=WalkFwd, 2=RunFwd, 3=WalkBack, 4=StrafeLeft, 5=StrafeRight
        private static readonly string[] BlendTreeClips =
        {
            "Anim_Idle.fbx",        // child 0 — (0, 0)
            "Anim_Walk.fbx",        // child 1 — (0, 0.5)
            "Anim_Run.fbx",         // child 2 — (0, 1)
            "Anim_WalkBack.fbx",    // child 3 — (0, -0.5)
            "Anim_StrafeLeft.fbx",  // child 4 — (-1, 0)
            "Anim_StrafeRight.fbx", // child 5 — (1, 0)
        };

        // Maps AnimatorController state name → animation FBX (for non-locomotion states)
        // Aim uses a 1D blend tree wired separately (standing vs walking fire)
        private static readonly Dictionary<string, string> StateToAnim = new Dictionary<string, string>
        {
            { "Reload", "Anim_Idle.fbx"  },
            { "Death",  "Anim_Death.fbx" },
        };

        private Vector2 _scroll;
        private GUIStyle _linkStyle;

        // ── Menu entry ────────────────────────────────────────────────────────
        [MenuItem("FreeWorld/Setup/5 - Set Up Character Models (CC0)")]
        public static void Open() =>
            GetWindow<CharacterModelImporter>("Character Model Setup", true).minSize = new Vector2(480, 520);

        // ── GUI ───────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            EnsureLinkStyle();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.Space(6);
            GUILayout.Label("Character Model Setup — CC0 Humanoids", EditorStyles.boldLabel);

            // ── Quick Setup ───────────────────────────────────────────────────
            bool hasFbx  = File.Exists(CharacterFbx);
            bool hasAnims = Directory.Exists(AnimDir) && Directory.GetFiles(AnimDir, "*.fbx").Length > 0;
            bool hasPrefab = File.Exists(PrefabPath);

            using (new EditorGUI.DisabledScope(!hasFbx || !hasAnims))
            {
                var color = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
                if (GUILayout.Button("▶  Auto-Wire Animations + Create Prefab", GUILayout.Height(42)))
                    RunFullSetup();
                GUI.backgroundColor = color;
            }
            if (!hasFbx)
                EditorGUILayout.HelpBox("Swat_Character.fbx not found in Assets/Characters/", MessageType.Warning);
            else if (!hasAnims)
                EditorGUILayout.HelpBox("No animation FBX files found in Assets/Characters/Animations/", MessageType.Warning);
            else if (hasPrefab)
                EditorGUILayout.HelpBox("Prefab ready: " + PrefabPath, MessageType.Info);

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Replace the procedural primitive enemy body with a real CC0 humanoid character.\n\n" +
                "Steps:\n" +
                "  1. Download a CC0 model pack from the links below.\n" +
                "  2. Copy the FBX file(s) into  Assets/Characters/  in your project.\n" +
                "  3. Click  Configure Models  — the tool sets Humanoid rig and creates\n" +
                "     an AnimatorController with the correct parameter names.\n" +
                "  4. Drag animation clips onto the controller states (Window → Animator).\n" +
                "  5. Assign the model's  Prefab  to  EnemyAI._characterModelPrefab  in\n" +
                "     the Inspector (or in FreeWorldSetup).",
                MessageType.Info);

            EditorGUILayout.Space(8);
            GUILayout.Label("CC0 Character Sources", EditorStyles.boldLabel);
            DrawLink("Quaternius — Character packs (CC0, low-poly, includes anims) — itch.io",
                     "https://quaternius.itch.io/");
            DrawLink("Kenney — Toon Characters 1 (CC0, stylised, FBX)",
                     "https://kenney.nl/assets/toon-characters-1");
            DrawLink("OpenGameArt — Low Poly Human Base Mesh (CC0)",
                     "https://opengameart.org/content/simple-characters");
            DrawLink("Mixamo — Free rigged characters (free, requires Adobe login)",
                     "https://www.mixamo.com/#/?type=Character");

            EditorGUILayout.Space(10);
            GUILayout.Label("Animator Parameters Expected by EnemyModelAnimator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Speed       (Float)  — NavMeshAgent velocity magnitude\n" +
                "IsAttacking (Bool)   — enemy is in Attack state\n" +
                "IsReloading (Bool)   — shooting module is reloading\n" +
                "IsDead      (Bool)   — triggers death animation",
                MessageType.None);

            EditorGUILayout.Space(3);
            EditorGUILayout.HelpBox(
                "Suggested states: Idle  →  Walk (Speed > 0.1)  →  Run (Speed > 3.5)\n" +
                "                  Aim (IsAttacking)  →  Reload (IsReloading)  →  Death (IsDead)",
                MessageType.None);

            EditorGUILayout.Space(10);
            GUILayout.Label("Configuration", EditorStyles.boldLabel);

            bool hasCharDir = Directory.Exists(CharDir);
            if (!hasCharDir)
                EditorGUILayout.HelpBox($"Folder  {CharDir}  not found. Create it or drop an FBX there first.", MessageType.Warning);

            using (new EditorGUI.DisabledScope(!hasCharDir))
            {
                if (GUILayout.Button("Configure Models in Assets/Characters/", GUILayout.Height(36)))
                    RunConfigure();
            }

            if (GUILayout.Button("Create Assets/Characters/ Folder", GUILayout.Height(28)))
            {
                Directory.CreateDirectory(CharDir);
                AssetDatabase.Refresh();
                Debug.Log("[CharacterModelImporter] Created folder: " + CharDir);
            }

            EditorGUILayout.Space(10);
            GUILayout.Label("AnimatorController", EditorStyles.boldLabel);
            bool hasController = File.Exists(ControllerPath);
            if (hasController)
                EditorGUILayout.HelpBox("EnemyAnimator.controller already exists at " + ControllerPath, MessageType.None);

            using (new EditorGUI.DisabledScope(hasController))
            {
                if (GUILayout.Button("Create EnemyAnimator.controller Only", GUILayout.Height(28)))
                {
                    EnsureCharactersDir();
                    CreateAnimatorController();
                    AssetDatabase.Refresh();
                    Debug.Log("[CharacterModelImporter] AnimatorController created at " + ControllerPath);
                }
            }

            if (hasController)
            {
                if (GUILayout.Button("Select EnemyAnimator.controller", GUILayout.Height(28)))
                    Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(ControllerPath);
            }

            EditorGUILayout.EndScrollView();
        }

        // ── Configure all FBX models found in Assets/Characters/ ─────────────
        private static void RunConfigure()
        {
            EnsureCharactersDir();
            string[] guids   = AssetDatabase.FindAssets("t:Model", new[] { CharDir });
            int      changed = 0;

            foreach (string guid in guids)
            {
                string path     = AssetDatabase.GUIDToAssetPath(guid);
                var    importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                bool dirty = false;
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    // Let Unity auto-map the avatar
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    dirty = true;
                }
                if (importer.importAnimation)
                {
                    // Keep animation but make sure humanoid clip maps are enabled
                    dirty = true;
                }
                if (dirty)
                {
                    importer.SaveAndReimport();
                    changed++;
                    Debug.Log($"[CharacterModelImporter] Configured humanoid rig: {path}");
                }
            }

            // Always ensure the AnimatorController exists
            if (!File.Exists(ControllerPath))
                CreateAnimatorController();

            // Mirror to Resources so EnemyModelAnimator can self-heal at runtime
            EnsureControllerInResources();

            AssetDatabase.Refresh();

            string summary = changed > 0
                ? $"Configured {changed} model(s) in {CharDir}.\n" +
                  "AnimatorController: " + ControllerPath + "\n\n" +
                  "Next steps:\n" +
                  "  • Open Window → Animation → Animator and assign clips to each state.\n" +
                  "  • Assign the configured model as a Prefab to EnemyAI._characterModelPrefab."
                : $"No new models needed reconfiguring in {CharDir}.\n" +
                  "AnimatorController is at: " + ControllerPath;

            EditorUtility.DisplayDialog("Character Model Setup", summary, "OK");
        }

        // ── Create/refresh Resources copy so runtime can load without AssetDatabase ──
        private static void EnsureControllerInResources()
        {
            if (!File.Exists(ControllerPath)) return;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            // Copy only when missing or source is newer
            if (!File.Exists(ControllerResources) ||
                File.GetLastWriteTimeUtc(ControllerPath) > File.GetLastWriteTimeUtc(ControllerResources))
            {
                // AssetDatabase copy keeps GUID links intact
                AssetDatabase.CopyAsset(ControllerPath, ControllerResources);
                AssetDatabase.Refresh();
                Debug.Log("[CharacterModelImporter] Mirrored controller to Resources: " + ControllerResources);
            }
        }

        // ── Build AnimatorController — Layer 0: Locomotion, Layer 1: Upper Body ─
        private static AnimatorController CreateAnimatorController()
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            // also mirror immediately
            EnsureControllerInResources();

            // Parameters
            controller.AddParameter("MoveX",       AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveZ",       AnimatorControllerParameterType.Float);
            controller.AddParameter("Speed",       AnimatorControllerParameterType.Float);
            controller.AddParameter("IsAttacking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsReloading", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsDead",      AnimatorControllerParameterType.Bool);

            // ── Layer 0: Full-body Locomotion ─────────────────────────────────
            var locoLayer    = controller.layers[0];
            locoLayer.name   = "Locomotion";
            var locoSM       = locoLayer.stateMachine;

            // 2D blend tree — handles Idle / Walk / Run / Back / Strafe
            var locoTree = new BlendTree();
            AssetDatabase.AddObjectToAsset(locoTree, ControllerPath);
            locoTree.name            = "LocoBlend";
            locoTree.blendType       = BlendTreeType.FreeformCartesian2D;
            locoTree.blendParameter  = "MoveX";
            locoTree.blendParameterY = "MoveZ";
            locoTree.useAutomaticThresholds = false;
            locoTree.AddChild(null, new Vector2( 0f,  0f));    // 0 Idle
            locoTree.AddChild(null, new Vector2( 0f,  0.5f)); // 1 Walk fwd
            locoTree.AddChild(null, new Vector2( 0f,  1f));   // 2 Run fwd
            locoTree.AddChild(null, new Vector2( 0f, -0.5f)); // 3 Walk back
            locoTree.AddChild(null, new Vector2(-1f,  0f));   // 4 Strafe left
            locoTree.AddChild(null, new Vector2( 1f,  0f));   // 5 Strafe right

            var locoState = locoSM.AddState("Locomotion");
            locoState.motion = locoTree;

            var deathState = locoSM.AddState("Death");
            locoSM.defaultState = locoState;

            // Death transition (full body — interrupts everything)
            var t = locoSM.AddAnyStateTransition(deathState);
            t.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");
            t.canTransitionToSelf = false;
            t.duration = 0f;

            // Write layer 0 back
            var layers    = controller.layers;
            layers[0]     = locoLayer;

            // ── Layer 1: Upper Body (Aim / Reload) ────────────────────────────
            controller.AddLayer("UpperBody");
            layers = controller.layers;          // re-fetch after AddLayer
            var ubLayer          = layers[1];
            ubLayer.defaultWeight = 0f;          // starts at 0; code sets to 1 when needed
            ubLayer.blendingMode  = AnimatorLayerBlendingMode.Override;

            // Create and save the upper-body AvatarMask
            var mask = CreateUpperBodyMask();
            ubLayer.avatarMask = mask;

            var ubSM = ubLayer.stateMachine;

            var emptyState  = ubSM.AddState("Empty");   // weight=0 → locomotion shows through
            var aimState    = ubSM.AddState("Aim");
            var reloadState = ubSM.AddState("Reload");
            ubSM.defaultState = emptyState;

            // Empty → Aim
            t = emptyState.AddTransition(aimState);
            t.AddCondition(AnimatorConditionMode.If, 0f, "IsAttacking");
            t.duration = 0.15f;

            // Aim → Empty
            t = aimState.AddTransition(emptyState);
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAttacking");
            t.duration = 0.15f;

            // Aim → Reload
            t = aimState.AddTransition(reloadState);
            t.AddCondition(AnimatorConditionMode.If, 0f, "IsReloading");
            t.duration = 0.1f;

            // Reload → Aim
            t = reloadState.AddTransition(aimState);
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsReloading");
            t.duration = 0.15f;

            layers[1] = ubLayer;
            controller.layers = layers;

            AssetDatabase.SaveAssets();
            return controller;
        }

        // ── Extract + upgrade Mixamo materials to URP Lit ─────────────────
        private static void UpgradeCharacterMaterials()
        {
            if (!File.Exists(CharacterFbx)) return;
            var charImporter = AssetImporter.GetAtPath(CharacterFbx) as ModelImporter;
            if (charImporter == null) return;

            // Extract embedded materials to disk so we can edit their shaders
            if (charImporter.materialLocation != ModelImporterMaterialLocation.External)
            {
                charImporter.materialLocation = ModelImporterMaterialLocation.External;
                charImporter.SaveAndReimport();
                AssetDatabase.Refresh();
            }

            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogWarning("[CharacterModelImporter] URP/Lit shader not found — is URP installed?");
                return;
            }

            int upgraded = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { CharDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat  = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == urpLit) continue;
                if (!mat.shader.name.Contains("Standard") && !mat.shader.name.Contains("Legacy")) continue;

                // Capture textures before switching shader (property names change)
                var mainTex  = mat.GetTexture("_MainTex");
                var bumpMap  = mat.GetTexture("_BumpMap");
                var metalMap = mat.GetTexture("_MetallicGlossMap");
                var occMap   = mat.GetTexture("_OcclusionMap");
                var baseCol  = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

                mat.shader = urpLit;
                if (mainTex  != null) mat.SetTexture("_BaseMap", mainTex);
                if (bumpMap  != null) { mat.SetTexture("_BumpMap", bumpMap); mat.EnableKeyword("_NORMALMAP"); }
                if (metalMap != null) mat.SetTexture("_MetallicGlossMap", metalMap);
                if (occMap   != null) mat.SetTexture("_OcclusionMap", occMap);
                mat.SetColor("_BaseColor", Color.white); // white = no tint, textures show through

                EditorUtility.SetDirty(mat);
                upgraded++;
                Debug.Log("[CharacterModelImporter] Upgraded to URP Lit: " + path);
            }
            if (upgraded > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[CharacterModelImporter] Upgraded {upgraded} material(s) to URP Lit.");
            }
        }

        // ── Upper-body AvatarMask (Spine upward) ─────────────────────────────
        private static AvatarMask CreateUpperBodyMask()
        {
            var mask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);

            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body,          true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm,       true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm,      true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers,   true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers,  true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head,          true);

            if (File.Exists(UpperBodyMaskPath))
                AssetDatabase.DeleteAsset(UpperBodyMaskPath);
            AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
            return mask;
        }

        // ── Full one-click setup ───────────────────────────────────────────
        private static void RunFullSetup()
        {
            // 0. Extract + upgrade materials to URP (Mixamo FBX uses Standard shader by default)
            UpgradeCharacterMaterials();

            // 1. Configure humanoid rig on character + animation FBX files
            EnsureCharactersDir();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { CharDir }))
            {
                string path     = AssetDatabase.GUIDToAssetPath(guid);
                var    importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                bool isAnim = path.Contains("/Animations/");
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup   = isAnim
                        ? ModelImporterAvatarSetup.CopyFromOther
                        : ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.SaveAndReimport();
                }
            }

            // Set anim files to copy avatar from character + configure clips
            string charPath     = CharacterFbx;
            var    charImporter = AssetImporter.GetAtPath(charPath) as ModelImporter;

            // charImporter.sourceAvatar is null for CreateFromThisModel rigs.
            // Load the baked Avatar sub-asset from the character FBX directly.
            Avatar charAvatar = AssetDatabase.LoadAllAssetsAtPath(charPath)
                .OfType<Avatar>()
                .FirstOrDefault();

            if (charAvatar == null)
            {
                // Not imported as humanoid yet — force it now
                if (charImporter != null)
                {
                    charImporter.animationType = ModelImporterAnimationType.Human;
                    charImporter.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
                    charImporter.SaveAndReimport();
                    charAvatar = AssetDatabase.LoadAllAssetsAtPath(charPath)
                        .OfType<Avatar>()
                        .FirstOrDefault();
                }
            }

            if (charAvatar == null)
                Debug.LogError("[CharacterModelImporter] Could not load Avatar from " + charPath + ". Animations will T-pose.");
            else
                Debug.Log("[CharacterModelImporter] Avatar loaded: " + charAvatar.name);

            if (charImporter != null)
            {
                // Clips that should loop (everything except Death)
                var loopClips = new System.Collections.Generic.HashSet<string>
                    { "Anim_Idle", "Anim_Walk", "Anim_Run", "Anim_Aim", "Anim_AimWalk",
                      "Anim_StrafeLeft", "Anim_StrafeRight", "Anim_WalkBack", "Anim_RunBack" };

                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { AnimDir }))
                {
                    string ap = AssetDatabase.GUIDToAssetPath(guid);
                    var    ai = AssetImporter.GetAtPath(ap) as ModelImporter;
                    if (ai == null) continue;

                    ai.animationType = ModelImporterAnimationType.Human;
                    ai.avatarSetup   = ModelImporterAvatarSetup.CopyFromOther;
                    if (charAvatar != null) ai.sourceAvatar = charAvatar;   // ← actual Avatar asset

                    ai.importAnimation = true;

                    string baseName   = System.IO.Path.GetFileNameWithoutExtension(ap);
                    bool   shouldLoop = loopClips.Contains(baseName);

                    var clips = ai.clipAnimations.Length > 0
                        ? ai.clipAnimations
                        : ai.defaultClipAnimations;
                    foreach (var clip in clips)
                    {
                        clip.loopTime           = shouldLoop;
                        clip.lockRootPositionXZ = true;
                        clip.lockRootHeightY    = true;
                    }
                    ai.clipAnimations = clips;
                    ai.SaveAndReimport();
                    Debug.Log($"[CharacterModelImporter] Configured anim: {baseName}  loop={shouldLoop}  avatar={(charAvatar != null ? charAvatar.name : "NULL")}");
                }
            }

            // 2. Rebuild AnimatorController with clips wired
            if (File.Exists(ControllerPath))
                AssetDatabase.DeleteAsset(ControllerPath);
            var controller   = CreateAnimatorController();
            var stateMachine = controller.layers[0].stateMachine;

            // Wire blend tree children (Locomotion state)
            var locoState = System.Array.Find(stateMachine.states, s => s.state.name == "Locomotion").state;
            var blendTree = locoState?.motion as BlendTree;
            if (blendTree != null)
            {
                var children = blendTree.children;
                for (int i = 0; i < BlendTreeClips.Length && i < children.Length; i++)
                {
                    string fbxPath = AnimDir + "/" + BlendTreeClips[i];
                    if (!File.Exists(fbxPath)) { Debug.LogWarning("[CharacterModelImporter] Missing: " + fbxPath); continue; }
                    var clip = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                        .OfType<AnimationClip>()
                        .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                    if (clip != null)
                    {
                        children[i].motion = clip;
                        Debug.Log($"[CharacterModelImporter] BlendTree[{i}] → {clip.name}");
                    }
                    else Debug.LogWarning($"[CharacterModelImporter] No clip found in {fbxPath}");
                }
                blendTree.children = children;
                EditorUtility.SetDirty(blendTree);
            }

            // Wire non-locomotion states (Reload, Death)
            foreach (var pair in StateToAnim)
            {
                string fbxPath = AnimDir + "/" + pair.Value;
                if (!File.Exists(fbxPath)) { Debug.LogWarning("[CharacterModelImporter] Missing anim: " + fbxPath); continue; }
                var clip = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip == null) { Debug.LogWarning("[CharacterModelImporter] No clip in: " + fbxPath); continue; }
                foreach (var cs in stateMachine.states)
                    if (cs.state.name == pair.Key) { cs.state.motion = clip; break; }
                Debug.Log($"[CharacterModelImporter] Wired {pair.Key} → {clip.name}");
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();  // flush all writes to disk first

            // Reload controller from disk so prefab gets a valid persistent reference
            var savedController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            // Wire Aim state in upper-body layer as a 1D Speed blend tree
            // (standing fire at Speed=0, walking fire at Speed=4)
            var ubSM2 = savedController.layers[1].stateMachine;
            var aimStateObj = System.Array.Find(ubSM2.states, s => s.state.name == "Aim").state;
            if (aimStateObj != null)
            {
                var aimTree = new BlendTree();
                AssetDatabase.AddObjectToAsset(aimTree, ControllerPath);
                aimTree.name           = "AimBlend";
                aimTree.blendType      = BlendTreeType.Simple1D;
                aimTree.blendParameter = "Speed";
                aimTree.useAutomaticThresholds = false;

                var aimStandClip = AssetDatabase.LoadAllAssetsAtPath(AnimDir + "/Anim_Aim.fbx")
                    .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                var aimWalkClip  = AssetDatabase.LoadAllAssetsAtPath(AnimDir + "/Anim_AimWalk.fbx")
                    .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));

                aimTree.AddChild(aimStandClip, 0f);   // Speed=0 → standing fire
                aimTree.AddChild(aimWalkClip,  4f);   // Speed=4 → walking fire

                aimStateObj.motion = aimTree;
                EditorUtility.SetDirty(savedController);
                AssetDatabase.SaveAssets();
                Debug.Log("[CharacterModelImporter] Aim blend tree wired (stand + walk fire)");
            }

            // 3. Create prefab
            string prefab = CreateEnemyPrefab(savedController);

            // 4. Auto-assign prefab to all EnemyAI instances in open scenes
            if (prefab != null)
                AutoAssignPrefabInScenes(prefab);

            string msg = prefab != null
                ? $"Setup complete!\n\nPrefab: {prefab}\nController: {ControllerPath}\n\nPrefab auto-assigned to all EnemyAI in open scenes."
                : "Animations wired. Could not create prefab — check Console.";
            EditorUtility.DisplayDialog("Character Setup", msg, "OK");
            if (prefab != null)
                Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(prefab);
        }

        // ── Create prefab from Swat_Character.fbx ────────────────────────────
        private static string CreateEnemyPrefab(AnimatorController controller)
        {
            if (!File.Exists(CharacterFbx))
            {
                Debug.LogWarning("[CharacterModelImporter] " + CharacterFbx + " not found.");
                return null;
            }

            // Step 1: if no prefab exists yet, create one from the FBX first
            if (!File.Exists(PrefabPath))
            {
                var modelGO  = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFbx);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelGO);
                instance.name = "Swat_Enemy";
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
                Object.DestroyImmediate(instance);
                AssetDatabase.ImportAsset(PrefabPath);
            }

            // Step 2: open the prefab in an isolated editing context so the
            //         controller assignment is guaranteed to survive to disk.
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            var animator = contents.GetComponent<Animator>()
                        ?? contents.GetComponentInChildren<Animator>();
            if (animator == null) animator = contents.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);

            Debug.Log("[CharacterModelImporter] Prefab saved with controller: " + PrefabPath);
            EnsureControllerInResources();
            return PrefabPath;
        }

        // ── Auto-assign prefab to all EnemyAI instances in open scenes ────────
        private static void AutoAssignPrefabInScenes(string prefabPath)
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null) return;

            var enemies = Object.FindObjectsOfType<EnemyAI>(true);
            int count = 0;
            foreach (var ai in enemies)
            {
                var so   = new SerializedObject(ai);
                var prop = so.FindProperty("_characterModelPrefab");
                if (prop != null)
                {
                    prop.objectReferenceValue = prefabAsset;
                    so.ApplyModifiedProperties();
                    count++;
                }
            }
            if (count > 0)
            {
                EditorSceneManager.MarkAllScenesDirty();
                Debug.Log($"[CharacterModelImporter] Auto-assigned prefab to {count} EnemyAI instance(s). Save the scene to persist.");
            }
        }

        private static void EnsureCharactersDir()
        {
            if (!Directory.Exists(CharDir))
            {
                Directory.CreateDirectory(CharDir);
                AssetDatabase.Refresh();
            }
        }

        // ── Clickable URL helper ──────────────────────────────────────────────
        private void DrawLink(string label, string url)
        {
            if (GUILayout.Button(label, _linkStyle))
                Application.OpenURL(url);
        }

        private void EnsureLinkStyle()
        {
            if (_linkStyle != null) return;
            _linkStyle = new GUIStyle(EditorStyles.linkLabel)
            {
                wordWrap = true,
                fontSize = 11,
                margin   = new RectOffset(4, 4, 2, 2)
            };
        }
    }
}
#endif

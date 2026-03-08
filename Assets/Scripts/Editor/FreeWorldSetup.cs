// ============================================================
//  FreeWorld — Automated Scene & Project Setup
//  Menu:  FreeWorld → Setup → Build FPS Scene
//         FreeWorld → Setup → Configure URP
//         FreeWorld → Setup → Create Layer Tags
// ============================================================
// EDITOR-ONLY: this file is inside Assets/Scripts/Editor/
// It is NOT included in your game build.

using System.IO;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.AI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using FreeWorld.Enemy;
using FreeWorld.Managers;
using FreeWorld.Player;
using FreeWorld.Utilities;
using FreeWorld.Weapons;

namespace FreeWorld.Editor
{
    public static class FreeWorldSetup
    {
        // ── Menu items ────────────────────────────────────────────────────────
        [MenuItem("FreeWorld/Setup/1 - Configure Layers & Tags")]
        public static void SetupLayersAndTags()
        {
            // Fix Input System activeInputHandler setting if invalid
            var playerSettings = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var inputHandler = playerSettings.FindProperty("activeInputHandler");
            if (inputHandler != null && inputHandler.intValue < 0)
            {
                inputHandler.intValue = 0; // 0 = old Input Manager, 1 = new, 2 = both
                playerSettings.ApplyModifiedProperties();
            }

            EnsureTag("Player");
            EnsureTag("Enemy");
            EnsureTag("Head");
            EnsureTag("Ground");
            EnsureTag("Pickup");

            EnsureLayer("Player",   6);
            EnsureLayer("Enemy",    7);
            EnsureLayer("Ground",   8);
            EnsureLayer("Pickup",   9);

            Debug.Log("[FreeWorld Setup] Tags and Layers configured!");
            AssetDatabase.SaveAssets();
        }

        [MenuItem("FreeWorld/Setup/2 - Build FPS Scene (Main)")]
        public static void BuildFPSScene()
        {
            // Cannot create scenes during play mode
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Stop Play Mode First",
                    "Please press the Stop (▶) button to exit Play Mode, then run this setup again.", "OK");
                return;
            }

            // ── Auto-assign URP pipeline asset to Graphics Settings ────────────
            AssignURPAsset();

            // ── Pre-create all required asset folders ─────────────────────────
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Materials");
            Directory.CreateDirectory("Assets/Settings");
            AssetDatabase.Refresh();

            // ── Generate textures before building the scene ───────────────────
            TextureGenerator.GenerateAll();

            // Save / create a new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // ── Lighting ──────────────────────────────────────────────────────
            SetupLighting();

            // ── Ground plane ──────────────────────────────────────────────────
            GameObject ground = CreateGround();

            // ── Player ────────────────────────────────────────────────────────
            GameObject player = CreatePlayer();

            // ── Enemy example (also saved as prefab for waves) ────────────────
            GameObject enemy = CreateEnemy();
            enemy.transform.position = new Vector3(10f, 1f, 10f);
            // Face toward player so it's immediately in FOV and starts chasing
            enemy.transform.LookAt(new Vector3(0f, 1f, 0f));

            // Save enemy as prefab so EnemySpawner can instantiate waves from it
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            string prefabPath = "Assets/Prefabs/Enemy_Grunt.prefab";
            var enemyPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
                enemy, prefabPath, InteractionMode.AutomatedAction);

            // ── Spawn points ──────────────────────────────────────────────────
            GameObject spawns = CreateSpawnPoints();

            // ── Arena cover / walls ───────────────────────────────────────────
            CreateArena();

            // ── Managers ──────────────────────────────────────────────────────
            GameObject managers = CreateManagers();

            // ── Camera post-process volume ────────────────────────────────────
            CreatePostProcessVolume();

            // ── Re-bake NavMesh after ALL geometry is placed ──────────────────
            var navSurface = ground.GetComponent<NavMeshSurface>();
            if (navSurface != null) navSurface.BuildNavMesh();

            // ── Wire EnemySpawner after prefab and spawn points exist ─────────
            var spawner = managers.GetComponentInChildren<EnemySpawner>();
            if (spawner != null && enemyPrefab != null)
            {
                var spawnerSO = new SerializedObject(spawner);
                spawnerSO.FindProperty("enemyPrefab").objectReferenceValue = enemyPrefab;
                // Collect spawn point transforms
                var spawnTransforms = new System.Collections.Generic.List<Transform>();
                foreach (Transform t in spawns.transform) spawnTransforms.Add(t);
                var spProp = spawnerSO.FindProperty("spawnPoints");
                spProp.arraySize = spawnTransforms.Count;
                for (int i2 = 0; i2 < spawnTransforms.Count; i2++)
                    spProp.GetArrayElementAtIndex(i2).objectReferenceValue = spawnTransforms[i2];
                spawnerSO.ApplyModifiedPropertiesWithoutUndo();
            }

            // ── Wire GameManager spawn points ─────────────────────────────────
            var gm = managers.GetComponentInChildren<GameManager>();
            if (gm != null)
            {
                var gmSO = new SerializedObject(gm);
                var spawnTransforms = new System.Collections.Generic.List<Transform>();
                foreach (Transform t in spawns.transform) spawnTransforms.Add(t);
                var gmSpProp = gmSO.FindProperty("spawnPoints");
                gmSpProp.arraySize = spawnTransforms.Count;
                for (int i2 = 0; i2 < spawnTransforms.Count; i2++)
                    gmSpProp.GetArrayElementAtIndex(i2).objectReferenceValue = spawnTransforms[i2];
                gmSO.ApplyModifiedPropertiesWithoutUndo();
            }

            // Save
            string scenePath = "Assets/Scenes/MainLevel.unity";
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[FreeWorld Setup] Scene saved to {scenePath}. Press Play to test!");
            EditorUtility.DisplayDialog(
                "FreeWorld Scene Built!",
                "Scene saved to Assets/Scenes/MainLevel.unity\n\n" +
                "NEXT STEPS:\n" +
                "1. Assign AudioClips to weapon/player components in Inspector\n" +
                "2. Add weapon model child objects under Camera/WeaponHolder\n" +
                "3. Bake NavMesh: Window → AI → Navigation → Bake\n" +
                "4. Import your weapon/environment assets from Asset Store\n" +
                "5. Build & run!",
                "Got it!");
        }

        [MenuItem("FreeWorld/Setup/3 - Build Main Menu Scene")]
        public static void BuildMainMenuScene()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Stop Play Mode First",
                    "Exit Play Mode before running setup.", "OK");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Configure the auto-created Main Camera
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.clearFlags      = CameraClearFlags.SolidColor;
                mainCam.backgroundColor = new Color(0.04f, 0.04f, 0.10f);
                mainCam.orthographic    = true;
            }

            // Canvas
            GameObject cv      = new GameObject("MainMenuCanvas");
            var canvas         = cv.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            var scaler         = cv.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode            = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution    = new Vector2(1920f, 1080f);
            cv.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Background (dark full-screen image)
            var bg = new GameObject("Background");
            bg.transform.SetParent(cv.transform, false);
            var bgImg      = bg.AddComponent<UnityEngine.UI.Image>();
            bgImg.color    = new Color(0.06f, 0.06f, 0.12f, 1f);
            var bgRT       = (RectTransform)bg.transform;
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;

            // Title label
            var title    = new GameObject("Title");
            title.transform.SetParent(cv.transform, false);
            var titleTMP = title.AddComponent<TextMeshProUGUI>();
            titleTMP.text      = "FREEWORLD";
            titleTMP.fontSize  = 72f;
            titleTMP.alignment = TextAlignmentOptions.Center;
            titleTMP.color     = new Color(0.9f, 0.7f, 0.1f);
            var titleRT          = (RectTransform)title.transform;
            titleRT.anchorMin    = new Vector2(0.5f, 1f);
            titleRT.anchorMax    = new Vector2(0.5f, 1f);
            titleRT.pivot        = new Vector2(0.5f, 1f);
            titleRT.anchoredPosition = new Vector2(0f, -80f);
            titleRT.sizeDelta        = new Vector2(600f, 100f);

            // Main panel with buttons
            var mainPanel = new GameObject("MainPanel");
            mainPanel.transform.SetParent(cv.transform, false);
            var vlg = mainPanel.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            vlg.spacing           = 20f;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            mainPanel.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit =
                UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var mpRT             = (RectTransform)mainPanel.transform;
            mpRT.anchorMin       = new Vector2(0.5f, 0.5f);
            mpRT.anchorMax       = new Vector2(0.5f, 0.5f);
            mpRT.pivot           = new Vector2(0.5f, 0.5f);
            mpRT.anchoredPosition = new Vector2(0f, -40f);
            mpRT.sizeDelta        = new Vector2(320f, 0f);

            // Create buttons and keep references for onClick wiring
            var btnLabels = new (string label, System.Action action)[0]; // filled below after menuComp
            var playBtn     = BuildMenuButton(mainPanel.transform, "PLAY",     new Color(0.15f, 0.15f, 0.22f));
            var settingsBtn = BuildMenuButton(mainPanel.transform, "SETTINGS", new Color(0.15f, 0.15f, 0.22f));
            var creditsBtn  = BuildMenuButton(mainPanel.transform, "CREDITS",  new Color(0.15f, 0.15f, 0.22f));
            var quitBtn     = BuildMenuButton(mainPanel.transform, "QUIT",     new Color(0.15f, 0.15f, 0.22f));

            // Settings panel (hidden by default)
            var settingsPanel = BuildSettingsPanel(cv.transform);
            settingsPanel.SetActive(false);

            // Credits panel
            var creditsPanel = BuildCreditsPanel(cv.transform);
            creditsPanel.SetActive(false);

            // Cyberpunk loading panel (hidden by default)
            var loadingPanel = BuildLoadingPanel(cv.transform);
            loadingPanel.SetActive(false);

            // Wire MainMenu component
            var menuComp = cv.AddComponent<MainMenu>();
            var menuSO   = new SerializedObject(menuComp);
            menuSO.FindProperty("mainPanel").objectReferenceValue        = mainPanel;
            menuSO.FindProperty("settingsPanel").objectReferenceValue    = settingsPanel;
            menuSO.FindProperty("creditsPanel").objectReferenceValue     = creditsPanel;
            menuSO.FindProperty("loadingPanel").objectReferenceValue     = loadingPanel;
            menuSO.FindProperty("loadingPercentText").objectReferenceValue =
                loadingPanel.transform.Find("PercentText")?.GetComponent<TextMeshProUGUI>();
            menuSO.FindProperty("loadingStatusText").objectReferenceValue  =
                loadingPanel.transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
            menuSO.FindProperty("loadingBar").objectReferenceValue        =
                loadingPanel.transform.Find("BarContainer/LoadingBar")?.GetComponent<UnityEngine.UI.Slider>();
            menuSO.ApplyModifiedPropertiesWithoutUndo();

            // Wire onClick persistent listeners so buttons call MainMenu methods at runtime
            UnityEditor.Events.UnityEventTools.AddPersistentListener(playBtn.onClick,     menuComp.OnPlay);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(settingsBtn.onClick, menuComp.OnSettings);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(creditsBtn.onClick,  menuComp.OnCredits);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(quitBtn.onClick,     menuComp.OnQuit);

            // SettingsManager on a separate GO (persists to game scene)
            var smGO = new GameObject("SettingsManager");
            smGO.AddComponent<SettingsManager>();

            // EventSystem
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            string menuScenePath = "Assets/Scenes/MainMenu.unity";
            string gameScenePath = "Assets/Scenes/MainLevel.unity";
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, menuScenePath);
            AssetDatabase.Refresh();

            // ── Auto-register scenes in Build Settings ────────────────────────
            var buildScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            if (System.IO.File.Exists(menuScenePath))
                buildScenes.Add(new EditorBuildSettingsScene(menuScenePath, true));
            if (System.IO.File.Exists(gameScenePath))
                buildScenes.Add(new EditorBuildSettingsScene(gameScenePath, true));
            // Preserve any other existing scenes
            foreach (var s2 in EditorBuildSettings.scenes)
                if (s2.path != menuScenePath && s2.path != gameScenePath)
                    buildScenes.Add(s2);
            EditorBuildSettings.scenes = buildScenes.ToArray();

            Debug.Log("[FreeWorld] Main menu scene saved and added to Build Settings.");
            EditorUtility.DisplayDialog("Done!",
                "Main menu scene saved to Assets/Scenes/MainMenu.unity\nand added to Build Settings.", "OK");
        }

        private static UnityEngine.UI.Button BuildMenuButton(Transform parent, string label, Color bgColor)
        {
            var go    = new GameObject($"Btn_{label}");
            go.transform.SetParent(parent, false);
            var img   = go.AddComponent<UnityEngine.UI.Image>();
            img.color = bgColor;
            var btn   = go.AddComponent<UnityEngine.UI.Button>();
            var nav   = btn.navigation;
            nav.mode  = UnityEngine.UI.Navigation.Mode.Vertical;
            btn.navigation = nav;

            var txtGO  = new GameObject("Label");
            txtGO.transform.SetParent(go.transform, false);
            var tmp    = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text   = label;
            tmp.fontSize = 26f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color  = Color.white;
            var lrt      = (RectTransform)txtGO.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.sizeDelta = Vector2.zero;

            var le    = go.AddComponent<UnityEngine.UI.LayoutElement>();
            le.preferredHeight = 60f;
            return btn;
        }

        private static GameObject BuildSettingsPanel(Transform parent)
        {
            var panel    = new GameObject("SettingsPanel");
            panel.transform.SetParent(parent, false);
            var bg       = panel.AddComponent<UnityEngine.UI.Image>();
            bg.color     = new Color(0.1f, 0.1f, 0.18f, 0.97f);
            var rt       = (RectTransform)panel.transform;
            rt.anchorMin = new Vector2(0.25f, 0.1f);
            rt.anchorMax = new Vector2(0.75f, 0.9f);
            rt.sizeDelta = Vector2.zero;

            AddSettingsLabel(panel.transform, "SETTINGS", 36f, new Vector2(0f, -30f));
            AddSettingsLabel(panel.transform, "Mouse Sensitivity",  20f, new Vector2(-80f, -110f));
            AddSettingsLabel(panel.transform, "Master Volume",      20f, new Vector2(-80f, -160f));
            AddSettingsLabel(panel.transform, "SFX Volume",         20f, new Vector2(-80f, -210f));
            AddSettingsLabel(panel.transform, "Music Volume",       20f, new Vector2(-80f, -260f));

            AddSettingsLabel(panel.transform, "← Back", 22f, new Vector2(0f, 30f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

            return panel;
        }

        private static void AddSettingsLabel(Transform parent, string text, float size,
            Vector2 pos, Vector2? anchorMin = null, Vector2? anchorMax = null)
        {
            var go  = new GameObject($"Lbl_{text.Replace(' ', '_')}");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;
            var rt          = (RectTransform)go.transform;
            rt.anchorMin    = anchorMin ?? new Vector2(0.5f, 1f);
            rt.anchorMax    = anchorMax ?? new Vector2(0.5f, 1f);
            rt.pivot        = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta        = new Vector2(300f, 36f);
        }

        private static GameObject BuildCreditsPanel(Transform parent)
        {
            var panel    = new GameObject("CreditsPanel");
            panel.transform.SetParent(parent, false);
            var bg       = panel.AddComponent<UnityEngine.UI.Image>();
            bg.color     = new Color(0.1f, 0.1f, 0.18f, 0.97f);
            var rt       = (RectTransform)panel.transform;
            rt.anchorMin = new Vector2(0.25f, 0.1f);
            rt.anchorMax = new Vector2(0.75f, 0.9f);
            rt.sizeDelta = Vector2.zero;

            var go  = new GameObject("Text");
            go.transform.SetParent(panel.transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = "<b>FREEWORLD</b>\n\nDeveloped with Unity 2022\n\nBuilt with GitHub Copilot\n\n\n← Back";
            tmp.fontSize  = 22f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;
            var trt         = (RectTransform)go.transform;
            trt.anchorMin   = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.sizeDelta   = Vector2.zero;
            return panel;
        }

        // ── Cyberpunk loading screen ──────────────────────────────────────────
        private static GameObject BuildLoadingPanel(Transform parent)
        {
            // Full-screen dark overlay
            var panel      = new GameObject("LoadingPanel");
            panel.transform.SetParent(parent, false);
            var bg         = panel.AddComponent<UnityEngine.UI.Image>();
            bg.color       = new Color(0.02f, 0.04f, 0.08f, 1f);
            var rt         = (RectTransform)panel.transform;
            rt.anchorMin   = Vector2.zero;
            rt.anchorMax   = Vector2.one;
            rt.sizeDelta   = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            // "FREEWORLD" logo at top
            var logo    = new GameObject("LogoText");
            logo.transform.SetParent(panel.transform, false);
            var logoTMP = logo.AddComponent<TextMeshProUGUI>();
            logoTMP.text      = "FREEWORLD";
            logoTMP.fontSize  = 64f;
            logoTMP.alignment = TextAlignmentOptions.Center;
            logoTMP.color     = new Color(0f, 0.95f, 1f);         // neon cyan
            var logoRT          = (RectTransform)logo.transform;
            logoRT.anchorMin    = new Vector2(0.5f, 0.5f);
            logoRT.anchorMax    = new Vector2(0.5f, 0.5f);
            logoRT.pivot        = new Vector2(0.5f, 0.5f);
            logoRT.anchoredPosition = new Vector2(0f, 120f);
            logoRT.sizeDelta        = new Vector2(700f, 80f);

            // Decorative divider line
            var line  = new GameObject("Divider");
            line.transform.SetParent(panel.transform, false);
            var lineImg   = line.AddComponent<UnityEngine.UI.Image>();
            lineImg.color = new Color(0f, 0.95f, 1f, 0.5f);
            var lineRT    = (RectTransform)line.transform;
            lineRT.anchorMin    = new Vector2(0.5f, 0.5f);
            lineRT.anchorMax    = new Vector2(0.5f, 0.5f);
            lineRT.pivot        = new Vector2(0.5f, 0.5f);
            lineRT.anchoredPosition = new Vector2(0f, 70f);
            lineRT.sizeDelta        = new Vector2(600f, 2f);

            // Status text  ("INITIALIZING NEURAL LINK...")
            var statusGO  = new GameObject("StatusText");
            statusGO.transform.SetParent(panel.transform, false);
            var statusTMP = statusGO.AddComponent<TextMeshProUGUI>();
            statusTMP.text      = "INITIALIZING NEURAL LINK...";
            statusTMP.fontSize  = 18f;
            statusTMP.alignment = TextAlignmentOptions.Center;
            statusTMP.color     = new Color(0f, 0.95f, 1f, 0.75f);
            var statusRT          = (RectTransform)statusGO.transform;
            statusRT.anchorMin    = new Vector2(0.5f, 0.5f);
            statusRT.anchorMax    = new Vector2(0.5f, 0.5f);
            statusRT.pivot        = new Vector2(0.5f, 0.5f);
            statusRT.anchoredPosition = new Vector2(0f, 20f);
            statusRT.sizeDelta        = new Vector2(700f, 36f);

            // Progress bar container (track)
            var barContainer = new GameObject("BarContainer");
            barContainer.transform.SetParent(panel.transform, false);
            var trackImg   = barContainer.AddComponent<UnityEngine.UI.Image>();
            trackImg.color = new Color(0.05f, 0.08f, 0.16f, 1f);
            var bcRT       = (RectTransform)barContainer.transform;
            bcRT.anchorMin       = new Vector2(0.5f, 0.5f);
            bcRT.anchorMax       = new Vector2(0.5f, 0.5f);
            bcRT.pivot           = new Vector2(0.5f, 0.5f);
            bcRT.anchoredPosition = new Vector2(0f, -40f);
            bcRT.sizeDelta        = new Vector2(600f, 20f);

            // Slider (re-use the track image as background, fill as cyan bar)
            var sliderGO  = new GameObject("LoadingBar");
            sliderGO.transform.SetParent(barContainer.transform, false);
            var slider    = sliderGO.AddComponent<UnityEngine.UI.Slider>();
            slider.minValue    = 0f;
            slider.maxValue    = 1f;
            slider.value       = 0f;
            slider.interactable = false;
            slider.direction   = UnityEngine.UI.Slider.Direction.LeftToRight;
            var sliderRT       = (RectTransform)sliderGO.transform;
            sliderRT.anchorMin = Vector2.zero; sliderRT.anchorMax = Vector2.one;
            sliderRT.sizeDelta = Vector2.zero;

            // Fill area — must be created with RectTransform (no UI component to auto-add it)
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGO.transform, false);
            var faRT      = (RectTransform)fillArea.transform;
            faRT.anchorMin = Vector2.zero; faRT.anchorMax = Vector2.one;
            faRT.sizeDelta = Vector2.zero;

            var fill    = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(fillArea.transform, false);
            var fillImg = fill.AddComponent<UnityEngine.UI.Image>();
            fillImg.color = new Color(0f, 0.95f, 1f);          // neon cyan fill
            var fillRT    = (RectTransform)fill.transform;
            fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
            fillRT.sizeDelta = Vector2.zero;

            slider.fillRect = (RectTransform)fill.transform;

            // Percent text  ("0.0%")
            var pctGO  = new GameObject("PercentText");
            pctGO.transform.SetParent(panel.transform, false);
            var pctTMP = pctGO.AddComponent<TextMeshProUGUI>();
            pctTMP.text      = "0.0%";
            pctTMP.fontSize  = 42f;
            pctTMP.alignment = TextAlignmentOptions.Center;
            pctTMP.color     = new Color(0.85f, 1f, 0.3f);     // neon yellow-green
            var pctRT          = (RectTransform)pctGO.transform;
            pctRT.anchorMin    = new Vector2(0.5f, 0.5f);
            pctRT.anchorMax    = new Vector2(0.5f, 0.5f);
            pctRT.pivot        = new Vector2(0.5f, 0.5f);
            pctRT.anchoredPosition = new Vector2(0f, -90f);
            pctRT.sizeDelta        = new Vector2(400f, 60f);

            // Corner decoration text
            var corner  = new GameObject("CornerText");
            corner.transform.SetParent(panel.transform, false);
            var cTMP    = corner.AddComponent<TextMeshProUGUI>();
            cTMP.text      = "SYS://BOOT v2.0.77";
            cTMP.fontSize  = 12f;
            cTMP.alignment = TextAlignmentOptions.Right;
            cTMP.color     = new Color(0f, 0.95f, 1f, 0.35f);
            var cRT        = (RectTransform)corner.transform;
            cRT.anchorMin  = new Vector2(1f, 0f);
            cRT.anchorMax  = new Vector2(1f, 0f);
            cRT.pivot      = new Vector2(1f, 0f);
            cRT.anchoredPosition = new Vector2(-20f, 20f);
            cRT.sizeDelta        = new Vector2(300f, 24f);

            return panel;
        }

        [MenuItem("FreeWorld/Setup/4 - Create URP Pipeline Assets")]
        public static void CreateURPAssets()
        {
            // Check if URP is available
            var urpType = System.Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime");

            if (urpType == null)
            {
                EditorUtility.DisplayDialog("URP Not Found",
                    "Universal Render Pipeline package not found.\n\n" +
                    "Please wait for Unity to finish importing packages (bottom-right progress bar), then try again.",
                    "OK");
                return;
            }

            Debug.Log("[FreeWorld Setup] URP is installed. Go to:\n" +
                      "Edit → Project Settings → Graphics → Scriptable Render Pipeline Settings\n" +
                      "and drag in a URP Asset (or create one with Assets → Create → Rendering → URP Asset)");

            EditorUtility.DisplayDialog("URP Setup",
                "URP package is installed!\n\n" +
                "To activate it:\n" +
                "1. Edit → Project Settings → Graphics\n" +
                "2. Drag a URP Pipeline Asset into 'Scriptable Render Pipeline Settings'\n" +
                "   (Create one: Assets → Create → Rendering → Universal Render Pipeline → Pipeline Asset)\n\n" +
                "For high quality:\n" +
                "• Enable SSAO, Bloom, Depth of Field in the URP Renderer\n" +
                "• Add a Global Volume to your scene with Post-processing overrides",
                "OK");
        }

        [MenuItem("FreeWorld/Setup/4 - Open Package Manager")]
        public static void OpenPackageManager()
        {
            UnityEditor.PackageManager.UI.Window.Open("");
        }

        [MenuItem("FreeWorld/Help/Controls Reference")]
        public static void ShowControls()
        {
            EditorUtility.DisplayDialog("FreeWorld — Controls",
                "WASD          Move\n" +
                "Left Shift    Sprint\n" +
                "Left Ctrl     Crouch (toggle)\n" +
                "Space         Jump\n" +
                "LMB           Shoot\n" +
                "RMB           Aim Down Sights\n" +
                "R             Reload\n" +
                "Scroll / 1-5  Switch Weapon\n" +
                "Escape        Pause",
                "Got it!");
        }

        // ── Scene builders ────────────────────────────────────────────────────

        // ── URP auto-assign ───────────────────────────────────────────────────
        private static void AssignURPAsset()
        {
            // Find any URP pipeline asset already in the project
            var guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");

            if (guids.Length == 0)
            {
                // None found — create one via reflection to avoid hard assembly dependency
                Debug.LogWarning("[FreeWorld Setup] No URP asset found. Creating one via reflection...");
                var urpType = System.Type.GetType(
                    "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, " +
                    "Unity.RenderPipelines.Universal.Runtime");
                if (urpType != null)
                {
                    var createMethod = urpType.GetMethod("Create",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
                        null, new System.Type[0], null);
                    if (createMethod != null)
                    {
                        var asset = createMethod.Invoke(null, null) as UnityEngine.Rendering.RenderPipelineAsset;
                        if (asset != null)
                        {
                            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                                AssetDatabase.CreateFolder("Assets", "Settings");
                            AssetDatabase.CreateAsset(asset, "Assets/Settings/FreeWorld-URP-Auto.asset");
                            AssetDatabase.SaveAssets();
                        }
                    }
                }
                guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
            }

            if (guids.Length > 0)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                var urpAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(assetPath);
                if (urpAsset != null)
                {
                    UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = urpAsset;
                    QualitySettings.renderPipeline = urpAsset;
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[FreeWorld Setup] URP asset assigned: {assetPath}");
                }
            }
            else
            {
                Debug.LogWarning("[FreeWorld Setup] Could not find or create a URP asset. " +
                    "Manually assign one via Edit → Project Settings → Graphics.");
            }
        }

        private static void SetupLighting()
        {
            // Directional light already created by NewScene, just tune it
            var light = Object.FindObjectOfType<Light>();
            if (light != null)
            {
                light.type      = LightType.Directional;
                light.intensity = 1.2f;
                light.color     = new Color(1f, 0.95f, 0.84f);
                light.shadows   = LightShadows.Soft;
                light.transform.eulerAngles = new Vector3(50f, -30f, 0f);
                light.gameObject.name = "Sun";
            }

            // Ambient
            RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog          = true;
            RenderSettings.fogColor     = new Color(0.5f, 0.5f, 0.5f);
            RenderSettings.fogMode      = FogMode.ExponentialSquared;
            RenderSettings.fogDensity   = 0.008f;
        }

        private static GameObject CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.tag  = "Ground";
            ground.transform.localScale    = new Vector3(100f, 1f, 100f);
            ground.transform.position      = new Vector3(0f, -0.5f, 0f);

            // Static flags for lighting & occlusion (NavigationStatic is obsolete in 2022+)
            GameObjectUtility.SetStaticEditorFlags(ground,
                StaticEditorFlags.ContributeGI |
                StaticEditorFlags.OccluderStatic);

            // Add a NavMeshSurface so enemies can pathfind — bake immediately
            var navSurface = ground.AddComponent<NavMeshSurface>();
            navSurface.BuildNavMesh();

            // Give it a concrete texture
            Renderer r = ground.GetComponent<Renderer>();
            if (r != null)
            {
                TextureGenerator.GenerateAll();
                Material mat = TextureGenerator.GetOrCreateMaterial(
                    "GroundMaterial", "T_Concrete",
                    new Color(0.80f, 0.80f, 0.78f), new Vector2(20f, 20f));
                r.material = mat;
            }

            return ground;
        }

        private static GameObject CreatePlayer()
        {
            // ── Root capsule ──────────────────────────────────────────────────
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name  = "Player";
            player.tag   = "Player";
            player.layer = LayerMask.NameToLayer("Player") >= 0
                         ? LayerMask.NameToLayer("Player") : 0;
            player.transform.position = new Vector3(0f, 1f, 0f);

            // Replace CapsuleCollider with CharacterController
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());

            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            // ── Ground check ──────────────────────────────────────────────────
            GameObject groundCheck = new GameObject("GroundCheck");
            groundCheck.transform.SetParent(player.transform, false);
            groundCheck.transform.localPosition = new Vector3(0f, 0.05f, 0f);

            // ── FPS Camera setup ──────────────────────────────────────────────
            GameObject camRoot = new GameObject("CameraRoot");
            camRoot.transform.SetParent(player.transform, false);
            camRoot.transform.localPosition = new Vector3(0f, 0.75f, 0f);

            var camObj = new GameObject("FPSCamera");
            camObj.transform.SetParent(camRoot.transform, false);
            var cam = camObj.AddComponent<Camera>();
            cam.fieldOfView   = 75f;
            cam.nearClipPlane = 0.05f;
            camObj.tag = "MainCamera";

            // Only one AudioListener — remove the one on Main Camera if it exists
            var existingListener = Object.FindObjectOfType<AudioListener>();
            if (existingListener != null) Object.DestroyImmediate(existingListener);
            camObj.AddComponent<AudioListener>();

            // ── Add & wire PlayerController ───────────────────────────────────
            var pc = player.AddComponent<PlayerController>();
            var pcSO = new SerializedObject(pc);
            pcSO.FindProperty("groundCheck").objectReferenceValue = groundCheck.transform;
            // Use Everything except the Player/Enemy layers so jump detects ground, walls, etc.
            pcSO.FindProperty("groundMask").intValue = ~LayerMask.GetMask("Player", "Enemy");
            pcSO.ApplyModifiedPropertiesWithoutUndo();

            // ── Add & wire PlayerCamera ───────────────────────────────────────
            var playerCam = camObj.AddComponent<PlayerCamera>();
            var camSO = new SerializedObject(playerCam);
            camSO.FindProperty("playerBody").objectReferenceValue = player.transform;
            camSO.ApplyModifiedPropertiesWithoutUndo();

            player.AddComponent<PlayerHealth>();
            player.AddComponent<WeaponManager>();

            // Grenade throwing
            var gt   = player.AddComponent<GrenadeThrow>();
            var gtSO = new SerializedObject(gt);
            gtSO.FindProperty("throwOrigin").objectReferenceValue = camObj.transform;
            gtSO.ApplyModifiedPropertiesWithoutUndo();

            // ── Weapon holder ─────────────────────────────────────────────────
            GameObject weaponHolder = new GameObject("WeaponHolder");
            weaponHolder.transform.SetParent(camObj.transform, false);
            weaponHolder.transform.localPosition = new Vector3(0.2f, -0.15f, 0.4f);

            // Placeholder weapon (Cube until real model imported)
            GameObject weaponMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            weaponMesh.name = "Weapon_AK47_Placeholder";
            weaponMesh.transform.SetParent(weaponHolder.transform, false);
            weaponMesh.transform.localScale    = new Vector3(0.08f, 0.08f, 0.4f);
            weaponMesh.transform.localPosition = Vector3.zero;
            Object.DestroyImmediate(weaponMesh.GetComponent<BoxCollider>());

            var weapon = weaponMesh.AddComponent<AssaultRifle>();
            weaponMesh.AddComponent<AudioSource>();  // required for weapon sounds

            // Wire weapon's camera reference
            var weaponSO = new SerializedObject(weapon);
            weaponSO.FindProperty("fpsCam").objectReferenceValue = cam;
            weaponSO.ApplyModifiedPropertiesWithoutUndo();

            // Muzzle point
            GameObject muzzle = new GameObject("MuzzlePoint");
            muzzle.transform.SetParent(weaponMesh.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 0.6f);

            // ── Pistol (secondary weapon — slot 2) ────────────────────────────
            GameObject pistolMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pistolMesh.name = "Weapon_Pistol_Placeholder";
            pistolMesh.transform.SetParent(weaponHolder.transform, false);
            pistolMesh.transform.localScale    = new Vector3(0.06f, 0.06f, 0.24f);
            pistolMesh.transform.localPosition = Vector3.zero;
            Object.DestroyImmediate(pistolMesh.GetComponent<BoxCollider>());

            var pistol = pistolMesh.AddComponent<Pistol>();
            pistolMesh.AddComponent<AudioSource>();

            var pistolSO = new SerializedObject(pistol);
            pistolSO.FindProperty("fpsCam").objectReferenceValue = cam;
            pistolSO.ApplyModifiedPropertiesWithoutUndo();

            pistolMesh.SetActive(false);   // holstered by default

            // ── Wire WeaponManager weapon slots ──────────────────────────────
            var wm   = player.GetComponent<WeaponManager>();
            var wmSO = new SerializedObject(wm);
            var wArr = wmSO.FindProperty("weapons");
            wArr.arraySize = 2;
            wArr.GetArrayElementAtIndex(0).objectReferenceValue = weapon;
            wArr.GetArrayElementAtIndex(1).objectReferenceValue = pistol;
            wmSO.ApplyModifiedPropertiesWithoutUndo();

            return player;
        }

        private static GameObject CreateEnemy()
        {
            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name  = "Enemy_Grunt";
            enemy.tag   = "Enemy";
            enemy.layer = LayerMask.NameToLayer("Enemy") >= 0
                        ? LayerMask.NameToLayer("Enemy") : 0;

            // Red material to distinguish from player
            Renderer r = enemy.GetComponent<Renderer>();
            if (r != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ??
                                            Shader.Find("Standard"));
                mat.color    = new Color(0.8f, 0.1f, 0.1f);
                mat.name     = "EnemyMaterial";
                r.material   = mat;
                if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                    AssetDatabase.CreateFolder("Assets", "Materials");
                AssetDatabase.CreateAsset(mat, "Assets/Materials/EnemyMaterial.asset");
            }

            // NavMeshAgent (requires AI Navigation package)
            var agentType = System.Type.GetType(
                "UnityEngine.AI.NavMeshAgent, UnityEngine.AIModule");
            if (agentType != null)
                enemy.AddComponent(agentType);

            enemy.AddComponent<EnemyHealth>();
            enemy.AddComponent<EnemyAI>();
            enemy.AddComponent<EnemyProceduralAnimator>();
            enemy.AddComponent<EnemyShootingModule>();
            enemy.AddComponent<AudioSource>();

            return enemy;
        }

        // Builds a small CS-style arena: perimeter walls + crates for cover
        private static void CreateArena()
        {
            GameObject arena = new GameObject("Arena");
            Material wallMat = TextureGenerator.GetOrCreateMaterial(
                "WallMaterial", "T_Brick",
                Color.white, new Vector2(8f, 2f));

            // ── Perimeter walls (4 sides, 40×3 units) ─────────────────────────
            (Vector3 pos, Vector3 scale, string name)[] walls =
            {
                (new Vector3( 0f, 1.5f,  25f), new Vector3(50f, 3f, 1f), "Wall_North"),
                (new Vector3( 0f, 1.5f, -25f), new Vector3(50f, 3f, 1f), "Wall_South"),
                (new Vector3( 25f, 1.5f,  0f), new Vector3(1f, 3f, 50f), "Wall_East"),
                (new Vector3(-25f, 1.5f,  0f), new Vector3(1f, 3f, 50f), "Wall_West"),
            };

            foreach (var w in walls)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = w.name;
                wall.transform.SetParent(arena.transform);
                wall.transform.position   = w.pos;
                wall.transform.localScale = w.scale;
                wall.GetComponent<Renderer>().sharedMaterial = wallMat;
                GameObjectUtility.SetStaticEditorFlags(wall,
                    StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic);
            }

            // ── Cover crates (scattered around mid-map) ────────────────────────
            Material crateMat = TextureGenerator.GetOrCreateMaterial(
                "CrateMaterial", "T_Wood",
                Color.white, new Vector2(2f, 2f));

            (Vector3 pos, Vector3 scale)[] crates =
            {
                // Centre cluster
                (new Vector3(  0f, 0.6f,   0f), new Vector3(1.2f, 1.2f, 1.2f)),
                (new Vector3(  1.3f, 0.6f, 0.2f), new Vector3(1.2f, 1.2f, 1.2f)),
                // Mid-left cover
                (new Vector3(-8f, 0.6f,  3f), new Vector3(3f, 1.2f, 1.2f)),
                (new Vector3(-8f, 1.8f,  3f), new Vector3(3f, 1.2f, 0.6f)),    // stacked
                // Mid-right cover
                (new Vector3( 8f, 0.6f, -3f), new Vector3(3f, 1.2f, 1.2f)),
                // Far-left corner cover
                (new Vector3(-14f, 0.6f, 12f), new Vector3(1.2f, 1.2f, 3f)),
                (new Vector3(-14f, 0.6f,  9f), new Vector3(1.2f, 1.2f, 1.2f)),
                // Far-right corner cover
                (new Vector3( 14f, 0.6f,-12f), new Vector3(1.2f, 1.2f, 3f)),
                // Diagonal wall
                (new Vector3(  3f, 0.6f, 10f), new Vector3(6f, 1.8f, 0.5f)),
                (new Vector3( -4f, 0.6f,-10f), new Vector3(6f, 1.8f, 0.5f)),
            };

            for (int i = 0; i < crates.Length; i++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = $"Crate_{i + 1}";
                c.transform.SetParent(arena.transform);
                c.transform.position   = crates[i].pos;
                c.transform.localScale = crates[i].scale;
                c.GetComponent<Renderer>().sharedMaterial = crateMat;
            }
        }

        private static GameObject CreateSpawnPoints()
        {
            GameObject root = new GameObject("SpawnPoints");
            Vector3[] positions =
            {
                new Vector3( 0f, 0f,  0f),
                new Vector3(15f, 0f, 15f),
                new Vector3(-15f, 0f, 15f),
                new Vector3(15f, 0f, -15f),
                new Vector3(-15f, 0f, -15f),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                var sp = new GameObject($"SpawnPoint_{i + 1}");
                sp.transform.SetParent(root.transform);
                sp.transform.position = positions[i];
            }
            return root;
        }

        private static GameObject CreateManagers()
        {
            GameObject managers = new GameObject("--MANAGERS--");

            // ── GameManager ───────────────────────────────────────────────────
            GameObject gmObj = new GameObject("GameManager");
            gmObj.transform.SetParent(managers.transform);
            gmObj.AddComponent<GameManager>();

            // SettingsManager on its OWN child so its DontDestroyOnLoad never kills GameManager
            GameObject smObj = new GameObject("SettingsManager");
            smObj.transform.SetParent(managers.transform);
            smObj.AddComponent<SettingsManager>();
            // ── UICanvas ──────────────────────────────────────────────────────
            GameObject uiCanvas = new GameObject("UICanvas");
            uiCanvas.transform.SetParent(managers.transform);
            var canvas = uiCanvas.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = uiCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;
            uiCanvas.AddComponent<GraphicRaycaster>();

            var uiMgr = uiCanvas.AddComponent<UIManager>();
            var uiSO  = new SerializedObject(uiMgr);
            Transform cv = uiCanvas.transform;

            // Damage flash — full-screen red overlay, hidden by default
            var flashImg = UI_Image(cv, "DamageFlash", new Color(1f, 0f, 0f, 0f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            flashImg.raycastTarget = false;
            flashImg.enabled       = false;
            uiSO.FindProperty("damageFlash").objectReferenceValue = flashImg;

            // Health bar — bottom-left
            var healthBar = UI_Slider(cv, "HealthBar", new Color(0.15f, 0.85f, 0.15f),
                new Vector2(0f, 0f), new Vector2(20f, 68f), new Vector2(260f, 18f));
            uiSO.FindProperty("healthBar").objectReferenceValue = healthBar;

            var healthText = UI_TMP(cv, "HealthText", "100", 26f, TextAlignmentOptions.Left,
                new Vector2(0f, 0f), new Vector2(20f, 42f), new Vector2(120f, 32f));
            healthText.color     = new Color(0.20f, 1f, 0.20f);
            healthText.fontStyle = TMPro.FontStyles.Bold;
            uiSO.FindProperty("healthText").objectReferenceValue = healthText;

            // Armor bar — bottom-left
            var armorBar = UI_Slider(cv, "ArmorBar", new Color(0.22f, 0.55f, 0.90f),
                new Vector2(0f, 0f), new Vector2(20f, 36f), new Vector2(260f, 18f));
            uiSO.FindProperty("armorBar").objectReferenceValue = armorBar;

            var armorText = UI_TMP(cv, "ArmorText", "0", 26f, TextAlignmentOptions.Left,
                new Vector2(0f, 0f), new Vector2(20f, 10f), new Vector2(120f, 32f));
            armorText.color     = new Color(0.45f, 0.55f, 1f);
            armorText.fontStyle = TMPro.FontStyles.Bold;
            uiSO.FindProperty("armorText").objectReferenceValue = armorText;

            // Ammo counter — bottom-right, large
            var ammoText = UI_TMP(cv, "AmmoText", "30 / 180", 56f, TextAlignmentOptions.Right,
                new Vector2(1f, 0f), new Vector2(-24f, 74f), new Vector2(320f, 68f));
            ammoText.fontStyle = TMPro.FontStyles.Bold;
            uiSO.FindProperty("ammoText").objectReferenceValue = ammoText;

            var weaponNameText = UI_TMP(cv, "WeaponNameText", "ASSAULT RIFLE", 17f, TextAlignmentOptions.Right,
                new Vector2(1f, 0f), new Vector2(-24f, 40f), new Vector2(320f, 26f));
            weaponNameText.color     = new Color(0.55f, 0.85f, 1f);
            weaponNameText.fontStyle = TMPro.FontStyles.Bold;
            uiSO.FindProperty("weaponNameText").objectReferenceValue = weaponNameText;

            // Timer — top-center, large
            var timerText = UI_TMP(cv, "TimerText", "2:00", 42f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(220f, 58f));
            timerText.fontStyle = TMPro.FontStyles.Bold;
            uiSO.FindProperty("timerText").objectReferenceValue = timerText;

            // Round info — top-center below timer
            var roundText = UI_TMP(cv, "RoundText", "ROUND 1 / 15", 22f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(320f, 32f));
            roundText.color     = new Color(0f, 0.88f, 1f);
            roundText.fontStyle = TMPro.FontStyles.Bold;
            uiSO.FindProperty("roundText").objectReferenceValue = roundText;

            // Score + kills — top-right
            var scoreText = UI_TMP(cv, "ScoreText", "0", 30f, TextAlignmentOptions.Right,
                new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(240f, 40f));
            scoreText.color     = new Color(1f, 0.85f, 0.10f);
            scoreText.fontStyle = TMPro.FontStyles.Bold;
            uiSO.FindProperty("scoreText").objectReferenceValue = scoreText;

            var killCountText = UI_TMP(cv, "KillCountText", "0 KILLS", 19f, TextAlignmentOptions.Right,
                new Vector2(1f, 1f), new Vector2(-24f, -62f), new Vector2(240f, 28f));
            killCountText.color     = new Color(0.82f, 0.82f, 0.82f);
            killCountText.fontStyle = TMPro.FontStyles.Bold;
            uiSO.FindProperty("killCountText").objectReferenceValue = killCountText;

            var enemiesText = UI_TMP(cv, "EnemiesText", "ENEMIES  <color=#FF6666>0</color>", 19f, TextAlignmentOptions.Right,
                new Vector2(1f, 1f), new Vector2(-24f, -94f), new Vector2(280f, 28f));
            enemiesText.color     = new Color(0.82f, 0.82f, 0.82f);
            enemiesText.fontStyle = TMPro.FontStyles.Bold;
            uiSO.FindProperty("enemiesText").objectReferenceValue = enemiesText;

            // Kill feed — right side, vertically centered
            var kfGO = new GameObject("KillFeed");
            kfGO.transform.SetParent(cv, false);
            var kfRT         = kfGO.AddComponent<RectTransform>();
            kfRT.anchorMin   = new Vector2(1f, 0.5f);
            kfRT.anchorMax   = new Vector2(1f, 0.5f);
            kfRT.pivot       = new Vector2(1f, 0.5f);
            kfRT.anchoredPosition = new Vector2(-16f, 0f);
            kfRT.sizeDelta   = new Vector2(330f, 320f);
            var kfVlg = kfGO.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            kfVlg.childAlignment      = TextAnchor.LowerRight;
            kfVlg.spacing             = 4f;
            kfVlg.childControlWidth   = kfVlg.childControlHeight = true;
            kfVlg.childForceExpandWidth = true;
            kfGO.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit =
                UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            uiSO.FindProperty("killFeedContainer").objectReferenceValue = kfGO.transform;

            // Crosshair lines — screen center
            var crossColor = new Color(0f, 1f, 0f, 0.9f);
            var chTop    = UI_CrosshairLine(cv, "CH_Top",     crossColor,  0f,  9f, 2f, 8f);
            var chBottom = UI_CrosshairLine(cv, "CH_Bottom",  crossColor,  0f, -9f, 2f, 8f);
            var chLeft   = UI_CrosshairLine(cv, "CH_Left",    crossColor, -9f,  0f, 8f, 2f);
            var chRight  = UI_CrosshairLine(cv, "CH_Right",   crossColor,  9f,  0f, 8f, 2f);
            uiSO.FindProperty("crosshairTop").objectReferenceValue    = chTop;
            uiSO.FindProperty("crosshairBottom").objectReferenceValue = chBottom;
            uiSO.FindProperty("crosshairLeft").objectReferenceValue   = chLeft;
            uiSO.FindProperty("crosshairRight").objectReferenceValue  = chRight;

            // Hit marker — small red flash at screen center when hitting an enemy
            var hitImg = UI_Image(cv, "HitMarker", new Color(1f, 0.2f, 0.2f, 0.8f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(20f, 20f));
            hitImg.enabled       = false;
            hitImg.raycastTarget = false;
            uiSO.FindProperty("hitMarker").objectReferenceValue = hitImg;

            // ── Overlay screens ───────────────────────────────────────────────
            // Shared helper: full-screen dark panel
            GameObject pauseScreen   = BuildOverlayPanel(cv, "PauseScreen",   new Color(0f, 0f, 0f, 0.82f));
            GameObject gameOverScreen = BuildOverlayPanel(cv, "GameOverScreen", new Color(0f, 0f, 0f, 0.90f));
            GameObject roundEndScreen = BuildOverlayPanel(cv, "RoundEndScreen", new Color(0f, 0f, 0.05f, 0.78f));

            // ── Pause screen contents ─────────────────────────────────────────
            var pauseTitle = UI_OverlayTitle(pauseScreen.transform, "PAUSED",
                new Color(0f, 0.95f, 1f), 80f, 230f);
            pauseTitle.characterSpacing = 12f;
            pauseTitle.gameObject.AddComponent<FreeWorld.Managers.CyberpunkTitle>();
            UI_OverlayTitle(pauseScreen.transform, "— MISSION SUSPENDED —",
                new Color(0.55f, 0.55f, 0.65f), 18f, 162f);

            var pauseBtnPanel = UI_ButtonColumn(pauseScreen.transform, new Vector2(0f, -30f), 380f);
            var btnResume   = BuildOverlayButton(pauseBtnPanel.transform, "RESUME",    new Color(0.05f, 0.40f, 0.08f));
            var btnRestart  = BuildOverlayButton(pauseBtnPanel.transform, "RESTART",   new Color(0.10f, 0.14f, 0.28f));
            var btnMainMenu = BuildOverlayButton(pauseBtnPanel.transform, "MAIN MENU", new Color(0.10f, 0.14f, 0.28f));
            var btnQuit     = BuildOverlayButton(pauseBtnPanel.transform, "QUIT",      new Color(0.40f, 0.06f, 0.06f));

            // ── Game Over screen contents ─────────────────────────────────────
            UI_OverlayTitle(gameOverScreen.transform, "GAME OVER",      new Color(1f, 0.18f, 0.18f), 80f,  230f);
            UI_OverlayTitle(gameOverScreen.transform, "NEURAL LINK SEVERED", new Color(0.6f, 0.6f, 0.6f), 22f, 155f);

            // Score text (wired via UIManager.gameOverScoreText SerializedField)
            var goScoreTMP = UI_TMP(cv, "GameOverScore", "FINAL SCORE: 0", 32f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(500f, 50f));
            goScoreTMP.transform.SetParent(gameOverScreen.transform, false);
            goScoreTMP.color = new Color(0.95f, 0.85f, 0.1f);

            var goBtnPanel = UI_ButtonColumn(gameOverScreen.transform, new Vector2(0f, -60f), 380f);
            var btnGoRestart  = BuildOverlayButton(goBtnPanel.transform, "RESTART MISSION", new Color(0.05f, 0.35f, 0.05f));
            var btnGoMainMenu = BuildOverlayButton(goBtnPanel.transform, "MAIN MENU",       new Color(0.12f, 0.12f, 0.20f));

            // ── Round End screen contents ─────────────────────────────────────
            UI_OverlayTitle(roundEndScreen.transform, "WAVE CLEARED",     new Color(0.9f, 0.75f, 0.1f), 72f, 60f);
            UI_OverlayTitle(roundEndScreen.transform, "PREPARING NEXT WAVE...", new Color(0f, 0.95f, 1f, 0.8f), 22f, -10f);

            // Attach PauseMenu component and wire all buttons
            var pauseMenuComp = uiCanvas.AddComponent<PauseMenu>();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btnResume.onClick,   pauseMenuComp.OnResume);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btnRestart.onClick,  pauseMenuComp.OnRestart);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btnMainMenu.onClick, pauseMenuComp.OnMainMenu);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btnQuit.onClick,     pauseMenuComp.OnQuit);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btnGoRestart.onClick,  pauseMenuComp.OnRestart);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btnGoMainMenu.onClick, pauseMenuComp.OnMainMenu);

            // Wire UIManager fields for overlay screens
            uiSO.FindProperty("pauseScreen").objectReferenceValue    = pauseScreen;
            uiSO.FindProperty("gameOverScreen").objectReferenceValue = gameOverScreen;
            uiSO.FindProperty("roundEndScreen").objectReferenceValue = roundEndScreen;
            uiSO.FindProperty("gameOverScoreText").objectReferenceValue = goScoreTMP;

            // Hide all screens by default
            pauseScreen.SetActive(false);
            gameOverScreen.SetActive(false);
            roundEndScreen.SetActive(false);

            uiSO.ApplyModifiedPropertiesWithoutUndo();

            // ── Scoreboard (Tab key) ───────────────────────────────────────────
            GameObject sbPanel = new GameObject("ScoreboardPanel");
            sbPanel.transform.SetParent(cv, false);
            var sbRT = sbPanel.AddComponent<RectTransform>();
            sbRT.anchorMin        = new Vector2(0.2f, 0.15f);
            sbRT.anchorMax        = new Vector2(0.8f, 0.85f);
            sbRT.offsetMin        = Vector2.zero;
            sbRT.offsetMax        = Vector2.zero;
            var sbBg = sbPanel.AddComponent<Image>();
            sbBg.color = new Color(0f, 0f, 0f, 0.85f);

            // Header row
            var sbHeader = new GameObject("Header");
            sbHeader.transform.SetParent(sbPanel.transform, false);
            var sbHeaderTMP = sbHeader.AddComponent<TextMeshProUGUI>();
            sbHeaderTMP.text      = "<b>#    NAME                KILLS  DEATHS  SCORE</b>";
            sbHeaderTMP.fontSize  = 18f;
            sbHeaderTMP.color     = new Color(1f, 0.85f, 0.2f);
            var sbHRT = (RectTransform)sbHeader.transform;
            sbHRT.anchorMin = new Vector2(0f, 1f); sbHRT.anchorMax = new Vector2(1f, 1f);
            sbHRT.pivot     = new Vector2(0.5f, 1f);
            sbHRT.anchoredPosition = new Vector2(0f, -10f);
            sbHRT.sizeDelta        = new Vector2(0f, 30f);

            // Row container
            var rowRoot = new GameObject("Rows");
            rowRoot.transform.SetParent(sbPanel.transform, false);
            var rowRT = rowRoot.AddComponent<RectTransform>();
            rowRT.anchorMin = new Vector2(0f, 0f); rowRT.anchorMax = new Vector2(1f, 1f);
            rowRT.offsetMin = new Vector2(10f, 10f); rowRT.offsetMax = new Vector2(-10f, -45f);
            var vlg = rowRoot.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            vlg.spacing           = 4f;
            vlg.childControlHeight = true;
            vlg.childControlWidth  = true;

            var sb  = uiCanvas.AddComponent<Scoreboard>();
            var sbSO = new SerializedObject(sb);
            sbSO.FindProperty("panel").objectReferenceValue        = sbPanel;
            sbSO.FindProperty("rowContainer").objectReferenceValue = rowRoot.transform;
            sbSO.ApplyModifiedPropertiesWithoutUndo();
            sbPanel.SetActive(false);

            // ── EnemySpawner ──────────────────────────────────────────────────
            GameObject spawnerObj = new GameObject("EnemySpawner");
            spawnerObj.transform.SetParent(managers.transform);
            spawnerObj.AddComponent<EnemySpawner>();

            // ── EventSystem (required for UI button clicks) ───────────────────
            GameObject evtSys = new GameObject("EventSystem");
            evtSys.AddComponent<UnityEngine.EventSystems.EventSystem>();
            evtSys.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            return managers;
        }

        // ── Overlay screen helpers ─────────────────────────────────────────────

        private static GameObject BuildOverlayPanel(Transform parent, string name, Color bg)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.color = bg;
            var rt  = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            // Centered card for visual depth
            var card    = new GameObject("Card");
            card.transform.SetParent(go.transform, false);
            var cardImg = card.AddComponent<UnityEngine.UI.Image>();
            cardImg.color         = new Color(0.04f, 0.04f, 0.10f, 0.78f);
            cardImg.raycastTarget = false;
            var cardRT  = (RectTransform)card.transform;
            cardRT.anchorMin        = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax        = new Vector2(0.5f, 0.5f);
            cardRT.pivot            = new Vector2(0.5f, 0.5f);
            cardRT.anchoredPosition = Vector2.zero;
            cardRT.sizeDelta        = new Vector2(500f, 560f);

            return go;
        }

        private static TextMeshProUGUI UI_OverlayTitle(Transform parent, string text, Color color,
            float fontSize, float yOffset)
        {
            var go  = new GameObject($"Title_{text.Replace(' ', '_').Substring(0, Mathf.Min(text.Length, 12))}");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text           = text;
            tmp.fontSize       = fontSize;
            tmp.fontStyle      = TMPro.FontStyles.Bold;
            tmp.alignment      = TextAlignmentOptions.Center;
            tmp.color          = color;
            var rt          = (RectTransform)go.transform;
            rt.anchorMin    = new Vector2(0.5f, 0.5f);
            rt.anchorMax    = new Vector2(0.5f, 0.5f);
            rt.pivot        = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, yOffset);
            rt.sizeDelta        = new Vector2(800f, fontSize + 20f);
            return tmp;
        }

        private static GameObject UI_ButtonColumn(Transform parent, Vector2 anchorPos, float width)
        {
            var go  = new GameObject("ButtonColumn");
            go.transform.SetParent(parent, false);
            var vlg = go.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            vlg.spacing           = 14f;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            go.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit =
                UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var rt             = (RectTransform)go.transform;
            rt.anchorMin       = new Vector2(0.5f, 0.5f);
            rt.anchorMax       = new Vector2(0.5f, 0.5f);
            rt.pivot           = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchorPos;
            rt.sizeDelta        = new Vector2(width, 0f);
            return go;
        }

        private static UnityEngine.UI.Button BuildOverlayButton(Transform parent, string label, Color bg)
        {
            var go    = new GameObject($"Btn_{label.Replace(' ', '_')}");
            go.transform.SetParent(parent, false);
            var img   = go.AddComponent<UnityEngine.UI.Image>();
            img.color = bg;
            var btn   = go.AddComponent<UnityEngine.UI.Button>();

            var le    = go.AddComponent<UnityEngine.UI.LayoutElement>();
            le.preferredHeight = 60f;

            var lblGO  = new GameObject("Label");
            lblGO.transform.SetParent(go.transform, false);
            var tmp    = lblGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 22f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;
            var lrt         = (RectTransform)lblGO.transform;
            lrt.anchorMin   = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.sizeDelta   = Vector2.zero;
            return btn;
        }

        // ── UI builder helpers ─────────────────────────────────────────────────

        private static Image UI_Image(Transform parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color         = color;
            img.raycastTarget = false;
            var rt            = (RectTransform)go.transform;
            rt.anchorMin        = anchorMin;
            rt.anchorMax        = anchorMax;
            rt.pivot            = new Vector2((anchorMin.x + anchorMax.x) * 0.5f,
                                              (anchorMin.y + anchorMax.y) * 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta        = size;
            return img;
        }

        private static Slider UI_Slider(Transform parent, string name, Color fillColor,
            Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var root   = new GameObject(name);
            root.transform.SetParent(parent, false);
            var rootRT = root.AddComponent<RectTransform>();
            rootRT.anchorMin        = anchor;
            rootRT.anchorMax        = anchor;
            rootRT.pivot            = anchor;
            rootRT.anchoredPosition = pos;
            rootRT.sizeDelta        = size;

            var slider          = root.AddComponent<Slider>();
            slider.minValue     = 0f;
            slider.maxValue     = 1f;
            slider.value        = 1f;
            slider.direction    = Slider.Direction.LeftToRight;
            slider.interactable = false;

            // Background
            var bgGO  = new GameObject("Background");
            bgGO.transform.SetParent(root.transform, false);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.6f);
            var bgRT    = (RectTransform)bgGO.transform;
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;

            // Fill Area
            var fillArea   = new GameObject("Fill Area");
            fillArea.transform.SetParent(root.transform, false);
            var fillAreaRT = fillArea.AddComponent<RectTransform>();
            fillAreaRT.anchorMin = Vector2.zero;
            fillAreaRT.anchorMax = Vector2.one;
            fillAreaRT.offsetMin = Vector2.zero;
            fillAreaRT.offsetMax = Vector2.zero;

            // Fill
            var fillGO  = new GameObject("Fill");
            fillGO.transform.SetParent(fillArea.transform, false);
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.color = fillColor;
            var fillRT    = (RectTransform)fillGO.transform;
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.sizeDelta = Vector2.zero;

            slider.fillRect      = fillRT;
            slider.targetGraphic = bgImg;
            return slider;
        }

        private static TextMeshProUGUI UI_TMP(Transform parent, string name, string text,
            float fontSize, TextAlignmentOptions align,
            Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.alignment = align;
            tmp.color     = Color.white;
            var rt          = (RectTransform)go.transform;
            rt.anchorMin        = anchor;
            rt.anchorMax        = anchor;
            rt.pivot            = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta        = size;
            return tmp;
        }

        private static RectTransform UI_CrosshairLine(Transform parent, string name, Color color,
            float x, float y, float w, float h)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color         = color;
            img.raycastTarget = false;
            var rt              = (RectTransform)go.transform;
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta        = new Vector2(w, h);
            return rt;
        }

        private static void CreatePostProcessVolume()
        {
            GameObject vol = new GameObject("PostProcessVolume");
            vol.transform.position = Vector3.zero;
            // Volume component is added if URP is present; skip gracefully otherwise
            var volumeType = System.Type.GetType(
                "UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime");
            if (volumeType != null)
            {
                var v = vol.AddComponent(volumeType);
                var isGlobalProp = volumeType.GetProperty("isGlobal");
                if (isGlobalProp != null) isGlobalProp.SetValue(v, true);
            }
        }

        // ── Utility helpers ───────────────────────────────────────────────────

        private static void EnsureTag(string tag)
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            SerializedProperty tags = tagManager.FindProperty("tags");
            for (int i = 0; i < tags.arraySize; i++)
                if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;

            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedProperties();
        }

        private static void EnsureLayer(string layerName, int index)
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            SerializedProperty layers = tagManager.FindProperty("layers");
            if (index >= layers.arraySize) return;

            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
            }
        }
    }
}

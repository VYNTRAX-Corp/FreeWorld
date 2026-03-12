using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FreeWorld.Editor
{
    public static class CharacterCustomizationMenuAutoUI
    {
        [MenuItem("FreeWorld/Setup/Add Character Customization Panel to Menu")]
        public static void AddCustomizationPanelToMenu()
        {
            var canvasGO = GameObject.Find("MainMenuCanvas");
            if (canvasGO == null)
            {
                Debug.LogError("MainMenuCanvas not found in scene. Open your menu scene and try again.");
                return;
            }

            // Find or create MainPanel (main menu buttons)
            var mainPanel = canvasGO.transform.Find("MainPanel");
            if (mainPanel == null)
            {
                mainPanel = new GameObject("MainPanel").transform;
                mainPanel.SetParent(canvasGO.transform, false);
            }

            // Add CUSTOMIZE button as sibling (so vertical layout group doesn't move it)
            float panelHeight = mainPanel.GetComponent<RectTransform>()?.sizeDelta.y ?? 0f;
            float customizeY = -(panelHeight / 2f) - 60f; // below main panel
            Button customizeBtn = CreateButton(canvasGO.transform, "CUSTOMIZE", new Vector2(0, customizeY));

            // Remove any existing customization panels anywhere in canvas
            foreach (Transform child in canvasGO.transform)
            {
                if (child.name == "CharacterCustomizationPanel")
                    Object.DestroyImmediate(child.gameObject);
            }
            // also check deeper layers just in case
            var toRemove = canvasGO.GetComponentsInChildren<Transform>(true);
            foreach (var t in toRemove)
            {
                if (t.name == "CharacterCustomizationPanel")
                    Object.DestroyImmediate(t.gameObject);
            }

            // Create CharacterCustomizationPanel (initially hidden) anchored to right edge
            var panelGO = new GameObject("CharacterCustomizationPanel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.12f, 0.18f, 0.97f);
            var panelRT = panelGO.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(1f, 0.5f); // right edge
            panelRT.anchorMax = new Vector2(1f, 0.5f);
            panelRT.pivot = new Vector2(1f, 0.5f);
            panelRT.anchoredPosition = new Vector2(-250f, 0f); // 250px left of right edge
            panelRT.sizeDelta = new Vector2(400f, 220f);
            panelGO.SetActive(false);

            // Create buttons for customization
            Button skinBtn = CreateButton(panelGO.transform, "Skin", new Vector2(-100, 40));
            Button clothingBtn = CreateButton(panelGO.transform, "Clothing", new Vector2(0, 40));
            Button faceBtn = CreateButton(panelGO.transform, "Face", new Vector2(100, 40));

            // Add BACK button
            Button backBtn = CreateButton(panelGO.transform, "BACK", new Vector2(0, -60));

            // Add CharacterCustomizationMenuUI
            var ui = panelGO.AddComponent<CharacterCustomizationMenuUI>();
            ui.skinButton = skinBtn;
            ui.clothingButton = clothingBtn;
            ui.faceButton = faceBtn;

            // Add controller to manage show/hide
            var controller = canvasGO.AddComponent<CharacterCustomizationMenuController>();
            controller.mainMenuPanel = mainPanel.gameObject;
            controller.customizationPanel = panelGO;
            controller.customizeButton = customizeBtn;
            controller.backButton = backBtn;

            Debug.Log("Character customization panel, CUSTOMIZE button, and show/hide logic added to MainMenuCanvas.");
        }

        private static Button CreateButton(Transform parent, string label, Vector2 anchoredPos)
        {
            GameObject btnGO = new GameObject(label + "Button");
            btnGO.transform.SetParent(parent);
            var btn = btnGO.AddComponent<Button>();
            var img = btnGO.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.22f); // match FreeWorld menu button color
            var rect = btnGO.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(120, 40);
            rect.anchoredPosition = anchoredPos;
            // Add text
            GameObject txtGO = new GameObject("Text");
            txtGO.transform.SetParent(btnGO.transform);
            var txt = txtGO.AddComponent<Text>();
            txt.text = label;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.black;
            var txtRect = txtGO.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;
            return btn;
        }
    }
}

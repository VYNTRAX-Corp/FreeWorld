// Integrates character customization UI into the existing FreeWorld MainMenuCanvas
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FreeWorld.Editor
{
    public static class CharacterCustomizationMenuIntegration
    {
        [MenuItem("FreeWorld/Setup/Integrate Character Customization Panel")]
        public static void IntegrateCustomizationPanel()
        {
            // forward to the improved auto-UI setup so the two commands stay in sync
            CharacterCustomizationMenuAutoUI.AddCustomizationPanelToMenu();
            Debug.Log("[Integration] forwarded to improved customization setup.");
        }

        private static Button CreateButton(Transform parent, string label, Vector2 anchoredPos)
        {
            GameObject btnGO = new GameObject(label + "Button");
            btnGO.transform.SetParent(parent);
            var btn = btnGO.AddComponent<Button>();
            var img = btnGO.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.22f); // match existing menu buttons
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

using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class CharacterCustomizationAutoSetup : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Setup Character Customization")]
    public static void SetupCustomization()
    {
        // Find player in scene
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogError("No GameObject with tag 'Player' found in scene.");
            return;
        }

        // Add CharacterCustomization if missing
        var customization = player.GetComponent<CharacterCustomization>();
        if (customization == null)
            customization = player.AddComponent<CharacterCustomization>();

        // Try to auto-assign renderers
        var renderers = player.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
            customization.skinRenderer = renderers[0];
        if (renderers.Length > 1)
            customization.clothingRenderer = renderers[1];

        // Assign example colors
        customization.skinTones = new Color[] { Color.white, new Color(1f,0.8f,0.6f), new Color(0.6f,0.4f,0.2f), Color.black };
        customization.clothingColors = new Color[] { Color.blue, Color.red, Color.green, Color.gray };

        // Try to find face meshes (children named "Face*")
        var faces = new System.Collections.Generic.List<GameObject>();
        foreach (Transform t in player.GetComponentsInChildren<Transform>())
            if (t.name.ToLower().StartsWith("face")) faces.Add(t.gameObject);
        customization.faceMeshes = faces.ToArray();
        for (int i = 0; i < faces.Count; i++) faces[i].SetActive(i == 0);

        // Create Canvas if not present
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("CustomizationCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // Create buttons
        Button skinBtn = CreateButton(canvas.transform, "Skin", new Vector2(-100, 100));
        Button clothingBtn = CreateButton(canvas.transform, "Clothing", new Vector2(0, 100));
        Button faceBtn = CreateButton(canvas.transform, "Face", new Vector2(100, 100));

        // Create UI controller
        GameObject uiGO = new GameObject("CharacterCustomizationUI");
        var ui = uiGO.AddComponent<CharacterCustomizationUI>();
        ui.customization = customization;
        ui.skinButton = skinBtn;
        ui.clothingButton = clothingBtn;
        ui.faceButton = faceBtn;

        Debug.Log("Character customization setup complete!");
    }

    private static Button CreateButton(Transform parent, string label, Vector2 anchoredPos)
    {
        GameObject btnGO = new GameObject(label + "Button");
        btnGO.transform.SetParent(parent);
        var btn = btnGO.AddComponent<Button>();
        var img = btnGO.AddComponent<Image>();
        img.color = Color.white;
        var rect = btnGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(120, 40);
        rect.anchoredPosition = anchoredPos;
        // Add text
        GameObject txtGO = new GameObject("Text");
        txtGO.transform.SetParent(btnGO.transform);
        var txt = txtGO.AddComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.black;
        var txtRect = txtGO.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;
        return btn;
    }
#endif
}

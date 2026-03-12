using UnityEngine;

public class CharacterCustomization : MonoBehaviour
{
    [Header("Skin Settings")]
    public Renderer skinRenderer; // optional single renderer
    public Color[] skinTones;
    private int currentSkinIndex = 0;
    private Renderer[] skinRenderers;

    [Header("Clothing Settings")]
    public Renderer clothingRenderer; // optional single renderer
    public Color[] clothingColors;
    private int currentClothingIndex = 0;
    private Renderer[] clothingRenderers;

    // procedural body data (if the built-in primitive builder was used)
    public FreeWorld.Utilities.EnemyBodyParts proceduralBody;

    [Header("Face Settings")]
    public GameObject[] faceMeshes;
    private int currentFaceIndex = 0;

    // Change skin tone
    public void NextSkinTone()
    {
        if (skinTones.Length == 0) return;
        currentSkinIndex = (currentSkinIndex + 1) % skinTones.Length;
        ApplySkinColor(skinTones[currentSkinIndex]);
    }

    // Change clothing color
    public void NextClothingColor()
    {
        if (clothingColors.Length == 0) return;
        currentClothingIndex = (currentClothingIndex + 1) % clothingColors.Length;
        ApplyClothColor(clothingColors[currentClothingIndex]);
    }

    // Change face mesh
    public void NextFace()
    {
        if (faceMeshes.Length == 0) return;
        faceMeshes[currentFaceIndex].SetActive(false);
        currentFaceIndex = (currentFaceIndex + 1) % faceMeshes.Length;
        faceMeshes[currentFaceIndex].SetActive(true);
    }

    void Awake()
    {
        // collect child renderers by tag
        skinRenderers = GetComponentsInChildren<Renderer>(); // will filter later
        var list = new System.Collections.Generic.List<Renderer>();
        foreach(var r in GetComponentsInChildren<Renderer>()){
            if (r.gameObject.CompareTag("Clothing")) continue;
            if (r.gameObject.CompareTag("Face")) continue;
            list.Add(r);
        }
        skinRenderers = list.ToArray();
        list.Clear();
        foreach(var r in GetComponentsInChildren<Renderer>()){
            if (r.gameObject.CompareTag("Clothing")) list.Add(r);
        }
        clothingRenderers = list.ToArray();
    }

    // Optionally, call these from UI buttons
    public void SetSkinTone(int index)
    {
        if (skinTones.Length == 0) return;
        currentSkinIndex = Mathf.Clamp(index, 0, skinTones.Length - 1);
        ApplySkinColor(skinTones[currentSkinIndex]);
    }

    public void SetClothingColor(int index)
    {
        if (clothingColors.Length == 0) return;
        currentClothingIndex = Mathf.Clamp(index, 0, clothingColors.Length - 1);
        ApplyClothColor(clothingColors[currentClothingIndex]);
    }

    public void SetFace(int index)
    {
        if (faceMeshes.Length == 0) return;
        faceMeshes[currentFaceIndex].SetActive(false);
        currentFaceIndex = Mathf.Clamp(index, 0, faceMeshes.Length - 1);
        faceMeshes[currentFaceIndex].SetActive(true);
    }

    private void ApplySkinColor(Color c)
    {
        if (skinRenderers != null && skinRenderers.Length > 0)
            foreach (var r in skinRenderers) if (r!=null) r.material.color = c;
        else if (skinRenderer != null)
            skinRenderer.material.color = c;
    }

    private void ApplyClothColor(Color c)
    {
        if (clothingRenderers != null && clothingRenderers.Length > 0)
            foreach (var r in clothingRenderers) if (r!=null) r.material.color = c;
        else if (clothingRenderer != null)
            clothingRenderer.material.color = c;
    }
}

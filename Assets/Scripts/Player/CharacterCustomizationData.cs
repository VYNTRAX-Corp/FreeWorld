using UnityEngine;

public static class CharacterCustomizationData
{
    // Save customization choices to PlayerPrefs
    public static void Save(int skinIndex, int clothingIndex, int faceIndex)
    {
        PlayerPrefs.SetInt("CC_Skin", skinIndex);
        PlayerPrefs.SetInt("CC_Clothing", clothingIndex);
        PlayerPrefs.SetInt("CC_Face", faceIndex);
        PlayerPrefs.Save();
    }

    // Load customization choices from PlayerPrefs
    public static void Load(out int skinIndex, out int clothingIndex, out int faceIndex)
    {
        skinIndex = PlayerPrefs.GetInt("CC_Skin", 0);
        clothingIndex = PlayerPrefs.GetInt("CC_Clothing", 0);
        faceIndex = PlayerPrefs.GetInt("CC_Face", 0);
    }
}

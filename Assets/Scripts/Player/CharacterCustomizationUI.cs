using UnityEngine;
using UnityEngine.UI;

public class CharacterCustomizationUI : MonoBehaviour
{
    public CharacterCustomization customization;
    public Button skinButton;
    public Button clothingButton;
    public Button faceButton;

    void Start()
    {
        if (skinButton != null)
            skinButton.onClick.AddListener(customization.NextSkinTone);
        if (clothingButton != null)
            clothingButton.onClick.AddListener(customization.NextClothingColor);
        if (faceButton != null)
            faceButton.onClick.AddListener(customization.NextFace);
    }
}

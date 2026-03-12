using UnityEngine;
using UnityEngine.UI;

public class CharacterCustomizationMenuController : MonoBehaviour
{
    public GameObject mainMenuPanel;
    public GameObject customizationPanel;
    public Button customizeButton;
    public Button backButton;

    void Start()
    {
        if (customizeButton != null)
            customizeButton.onClick.AddListener(ShowCustomization);
        if (backButton != null)
            backButton.onClick.AddListener(ShowMainMenu);
        ShowMainMenu();
    }

    public void ShowCustomization()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (customizeButton != null) customizeButton.gameObject.SetActive(false);
        if (customizationPanel != null) customizationPanel.SetActive(true);
    }

    public void ShowMainMenu()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (customizeButton != null) customizeButton.gameObject.SetActive(true);
        if (customizationPanel != null) customizationPanel.SetActive(false);
    }
}

using UnityEngine;
using UnityEngine.UI;

public class CharacterCustomizationMenuUI : MonoBehaviour
{
    public Button skinButton;
    public Button clothingButton;
    public Button faceButton;
    public UnityEngine.UI.Image panelBackground; // image of the panel for color preview
    public UnityEngine.UI.RawImage previewImage; // render texture display

    [Header("3D Preview")]
    public GameObject previewRoot; // instantiated model for showing skin/clothing/face
    private Renderer[] skinRenderers;
    private Renderer[] clothRenderers;
    private Renderer[] faceRenderers;

    // preview colours for each category (simple placeholders)
    public Color[] skinPreviewColors = { Color.white, new Color(1f,0.8f,0.6f), new Color(0.6f,0.4f,0.2f), Color.black };
    public Color[] clothingPreviewColors = { Color.gray, Color.red, Color.green, Color.blue };
    public Color[] facePreviewColors = { Color.white, Color.yellow, Color.cyan, Color.magenta };

    private int skinIndex = 0;
    private int clothingIndex = 0;
    private int faceIndex = 0;

    private void Start()
    {
        skinButton.onClick.AddListener(() => {
            skinIndex = (skinIndex + 1) % skinPreviewColors.Length;
            UpdateButtonLabel(skinButton, "SKIN", skinIndex);
            UpdatePreview();
            Save();
        });
        clothingButton.onClick.AddListener(() => {
            clothingIndex = (clothingIndex + 1) % clothingPreviewColors.Length;
            UpdateButtonLabel(clothingButton, "CLOTHING", clothingIndex);
            UpdatePreview();
            Save();
        });
        faceButton.onClick.AddListener(() => {
            faceIndex = (faceIndex + 1) % facePreviewColors.Length;
            UpdateButtonLabel(faceButton, "FACE", faceIndex);
            UpdatePreview();
            Save();
        });
        Load();
        // set initial labels
        UpdateButtonLabel(skinButton, "SKIN", skinIndex);
        UpdateButtonLabel(clothingButton, "CLOTHING", clothingIndex);
        UpdateButtonLabel(faceButton, "FACE", faceIndex);
        // prepare renderer lists if previewRoot assigned
        if (previewRoot != null)
        {
            var renders = previewRoot.GetComponentsInChildren<Renderer>();
            var skinList  = new System.Collections.Generic.List<Renderer>();
            var clothList = new System.Collections.Generic.List<Renderer>();
            var faceList  = new System.Collections.Generic.List<Renderer>();
            foreach (var r in renders)
            {
                if (r.gameObject.CompareTag("Clothing"))
                    clothList.Add(r);
                else if (r.gameObject.CompareTag("Face"))
                    faceList.Add(r);
                else
                    skinList.Add(r); // everything else counts as skin
            }
            skinRenderers = skinList.ToArray();
            clothRenderers = clothList.ToArray();
            faceRenderers = faceList.ToArray();
        }
        // build preview camera and texture at runtime
        if (previewRoot != null && previewImage != null)
        {
            // enlarge model for clearer view and lift higher
            previewRoot.transform.localScale = Vector3.one * 10f;
            previewRoot.transform.position += new Vector3(0f,5f,0f);
            // give it a slight yaw so head/arms poke out
            previewRoot.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            // create camera that renders preview layer
            var camGO = new GameObject("PreviewCam");
            camGO.transform.position = previewRoot.transform.position + new Vector3(0f,8f,-15f);
            camGO.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
            camGO.transform.LookAt(previewRoot.transform.position + new Vector3(0f,3f,0f));
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f,0.06f,0.12f,1f);
            int prevLayer = previewRoot.layer;
            cam.cullingMask = 1 << prevLayer;
            cam.fieldOfView = 40f;
            RenderTexture rt = new RenderTexture(256,256,16);
            cam.targetTexture = rt;
            previewImage.texture = rt;
            // spin model slowly for visibility
            previewRoot.AddComponent<SimpleRotator>().speed = 20f;
        }
        UpdatePreview();
    }

    private void Save()
    {
        CharacterCustomizationData.Save(skinIndex, clothingIndex, faceIndex);
        Debug.Log($"Customization saved: skin={skinIndex}, clothing={clothingIndex}, face={faceIndex}");
    }

    private void Load()
    {
        CharacterCustomizationData.Load(out skinIndex, out clothingIndex, out faceIndex);
    }

    public void UpdatePreview()
    {
        if (panelBackground != null)
        {
            // show skin color mainly
            Color skinCol = skinPreviewColors[Mathf.Clamp(skinIndex,0,skinPreviewColors.Length-1)];
            panelBackground.color = skinCol;
        }
        // update 3D preview renderers
        if (skinRenderers != null)
        {
            Color sk = skinPreviewColors[Mathf.Clamp(skinIndex,0,skinPreviewColors.Length-1)];
            foreach (var r in skinRenderers)
                if (r != null && r.sharedMaterial != null)
                    r.sharedMaterial.color = sk;
        }
        if (clothRenderers != null)
        {
            Color cl = clothingPreviewColors[Mathf.Clamp(clothingIndex,0,clothingPreviewColors.Length-1)];
            foreach (var r in clothRenderers)
                if (r != null && r.sharedMaterial != null)
                    r.sharedMaterial.color = cl;
        }
        if (faceRenderers != null)
        {
            Color fc = facePreviewColors[Mathf.Clamp(faceIndex,0,facePreviewColors.Length-1)];
            foreach (var r in faceRenderers)
                if (r != null && r.sharedMaterial != null)
                    r.sharedMaterial.color = fc;
        }
        // also tint head (if present) when face changes
        var headRend = previewRoot != null ? previewRoot.GetComponentInChildren<Renderer>() : null;
        if (headRend != null && headRend.gameObject.name.ToLower().Contains("head") && headRend.sharedMaterial != null)
        {
            Color fc = facePreviewColors[Mathf.Clamp(faceIndex,0,facePreviewColors.Length-1)];
            headRend.sharedMaterial.color = fc;
        }
    }

    private void UpdateButtonLabel(Button btn, string baseText, int index)
    {
        var tmp = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (tmp != null)
            tmp.text = $"{baseText} ({index})";
    }

    private bool ApproximatelyEqual(Color a, Color b)
    {
        return Vector4.Distance(a, b) < 0.01f;
    }
}

using UnityEngine;
using FreeWorld.Player;

public class CharacterCustomizationGameLoader : MonoBehaviour
{
    public CharacterCustomization customization;

    private Camera debugCam;
    private Camera fpCam;
    private GameObject weaponHolder;
    private Transform originalWeaponParent;
    private Vector3 originalWeaponLocalPos;
    private Quaternion originalWeaponLocalRot;

    // debug helper for movement speed calculation
    private Vector3 _prevPosition;

    void Start()
    {
        if (customization == null) customization = GetComponent<CharacterCustomization>();
        if (customization == null) return;
        int skin, clothing, face;
        CharacterCustomizationData.Load(out skin, out clothing, out face);
        Debug.Log($"Loaded customization indices skin={skin} clothing={clothing} face={face}");
        customization.SetSkinTone(skin);
        customization.SetClothingColor(clothing);
        customization.SetFace(face);

        // debug log presence of body parts
        if (customization.proceduralBody == null)
            Debug.LogWarning("CharacterCustomizationGameLoader: proceduralBody was null in Start");
        else
            Debug.Log("proceduralBody available, head=" + customization.proceduralBody.Head + " leftLeg=" + customization.proceduralBody.LeftUpperLeg);

        _prevPosition = transform.position;

        // if the renderer lists were not yet populated earlier (unlikely), call again to be safe
        customization.SetSkinTone(skin);
        customization.SetClothingColor(clothing);

        // do not override colors here; SetSkinTone/SetClothingColor already painted
        // the procedural body via the arrays populated in CharacterCustomization.Awake.

        // store first-person camera (child tagged MainCamera)
        fpCam = null;
        foreach (var cam in GetComponentsInChildren<Camera>(true))
        {
            if (cam.gameObject.CompareTag("MainCamera"))
            {
                fpCam = cam;
                break;
            }
        }
        // cache weapon holder so we can hide it during third-person
        var weaponHolderTransform = transform.Find("CameraRoot/WeaponHolder");
        weaponHolder = weaponHolderTransform != null ? weaponHolderTransform.gameObject : null;
        if (weaponHolder != null)
        {
            originalWeaponParent = weaponHolder.transform.parent;
            originalWeaponLocalPos = weaponHolder.transform.localPosition;
            originalWeaponLocalRot = weaponHolder.transform.localRotation;
        }
        if (fpCam != null)
            Debug.Log("FP camera found: " + fpCam.name);
        else
            Debug.LogWarning("No FP camera found on player");

        // spawn a debug third-person camera behind the player
        var camGO = new GameObject("DebugThirdPersonCam");
        camGO.transform.SetParent(transform, false);
        camGO.transform.localPosition = new Vector3(0f, 2f, -6f);
        camGO.transform.LookAt(transform.position + Vector3.up * 1.2f);
        debugCam = camGO.AddComponent<Camera>();
        // use skybox to match scene lighting instead of solid black
        debugCam.clearFlags = CameraClearFlags.Skybox;
        // if there is a global skybox material set in RenderSettings, copy it
        if (RenderSettings.skybox != null)
        {
            debugCam.gameObject.AddComponent<Skybox>().material = RenderSettings.skybox;
        }
        debugCam.enabled = false; // start disabled by default
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.V))
        {
            Debug.Log("V pressed, toggling camera");
            bool useDebug = debugCam != null && !debugCam.enabled;
            if (debugCam != null)
            {
                debugCam.enabled = useDebug;
                debugCam.depth = 100;
                // keep debug camera rotation in sync with fpCam
                if (fpCam != null)
                    debugCam.transform.rotation = fpCam.transform.rotation;
                Debug.Log("debugCam enabled=" + debugCam.enabled);
            }
            // keep the fpCam active so PlayerCamera continues updating crosshair/controls
            if (fpCam != null)
            {
                fpCam.depth = 0;
                Debug.Log("fpCam (still) enabled=" + fpCam.enabled);
            }
            if (weaponHolder != null)
            {
                if (useDebug && customization != null && customization.proceduralBody != null)
                {
                    // try attach to right hand first, fallback to forearm
                    var hand = customization.proceduralBody.RightHand ?? customization.proceduralBody.RightForeArm;
                    if (hand != null)
                    {
                        weaponHolder.transform.SetParent(hand, false);
                        weaponHolder.transform.localPosition = Vector3.zero;
                        weaponHolder.transform.localRotation = Quaternion.identity;
                        // move the holder slightly forward in world space so it sits where a real gun would
                        weaponHolder.transform.position = hand.position + hand.forward * 0.25f + hand.up * -0.1f;
                        weaponHolder.transform.rotation = hand.rotation;
                    }
                }
                else if (!useDebug && originalWeaponParent != null)
                {
                    weaponHolder.transform.SetParent(originalWeaponParent, false);
                    weaponHolder.transform.localPosition = originalWeaponLocalPos;
                    weaponHolder.transform.localRotation = originalWeaponLocalRot;
                }
            }
        }
        // if the debug cam is active, keep it pointed in same direction as the first-person camera
        if (debugCam != null && debugCam.enabled && fpCam != null)
        {
            debugCam.transform.rotation = fpCam.transform.rotation;
            // orient right upper arm + forearm toward camera forward for natural pose
            if (customization != null && customization.proceduralBody != null)
            {
                var body = customization.proceduralBody;
                if (body.RightUpperArm != null)
                    body.RightUpperArm.rotation = Quaternion.LookRotation(fpCam.transform.forward, body.RightUpperArm.up);
                if (body.RightForeArm != null)
                    body.RightForeArm.rotation = Quaternion.LookRotation(fpCam.transform.forward, body.RightForeArm.up);
                if (body.RightHand != null)
                    body.RightHand.rotation = Quaternion.LookRotation(fpCam.transform.forward, body.RightHand.up);
            }
        }

        // always animate procedural limbs; compute speed from position delta (works regardless of cc state)
        if (customization != null && customization.proceduralBody != null)
        {
            float speed = (transform.position - _prevPosition).magnitude / Time.deltaTime;
            _prevPosition = transform.position;
            if (speed > 0.01f)
                Debug.Log("positional animation speed=" + speed);

            float phase = Time.time * 6f; // constant swing frequency
            float amp   = Mathf.Clamp01(speed / 10f); // full amplitude at 10 units/sec
            var b = customization.proceduralBody;
            if (b == null)
            {
                Debug.LogWarning("proceduralBody was null when animating");
            }
            else
            {
                if (b.LeftUpperLeg != null)
                    b.LeftUpperLeg.localRotation = Quaternion.Euler(Mathf.Sin(phase) * 30f * amp, 0f, 0f);
                else Debug.LogWarning("LeftUpperLeg missing");
                if (b.RightUpperLeg != null)
                    b.RightUpperLeg.localRotation = Quaternion.Euler(Mathf.Sin(phase + Mathf.PI) * 30f * amp, 0f, 0f);
                else Debug.LogWarning("RightUpperLeg missing");
                if (b.LeftUpperArm != null)
                    b.LeftUpperArm.localRotation = Quaternion.Euler(Mathf.Sin(phase + Mathf.PI) * 20f * amp, 0f, 0f);
                if (b.RightUpperArm != null)
                    b.RightUpperArm.localRotation = Quaternion.Euler(Mathf.Sin(phase) * 20f * amp, 0f, 0f);
            }
        }

        if (debugCam != null && Input.GetKeyDown(KeyCode.C))
        {
            debugCam.enabled = !debugCam.enabled;
            Debug.Log("C pressed, debugCam now " + debugCam.enabled);
        }
    }
}

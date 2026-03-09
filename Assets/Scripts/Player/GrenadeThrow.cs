using UnityEngine;
using System.Collections;

namespace FreeWorld.Player
{
    /// <summary>
    /// Grenade throwing with first-person viewmodel, cook animation and HUD counter.
    ///
    /// Controls:
    ///   Hold G  — equip and cook (viewmodel raises grenade, pin-pull animation plays)
    ///   Release G — throw (viewmodel animates arm swing, grenade launches)
    ///
    /// No prefabs or assets required — everything is built procedurally at runtime.
    /// </summary>
    public class GrenadeThrow : MonoBehaviour
    {
        [Header("Throw Settings")]
        [SerializeField] private GameObject grenadePrefab;
        [SerializeField] private float      minThrowForce  = 8f;
        [SerializeField] private float      maxThrowForce  = 22f;
        [SerializeField] private float      maxCookTime    = 2f;
        [SerializeField] private int        maxGrenades    = 2;

        [Header("Throw Origin")]
        [SerializeField] private Transform  throwOrigin;   // auto-resolved to Camera if null

        // ── Runtime state ──────────────────────────────────────────────────────
        private int   _grenadeCount;
        private bool  _cooking;
        private float _cookTimer;

        // ── Viewmodel ─────────────────────────────────────────────────────────
        // Root that follows the camera; contains arm + grenade meshes
        private Transform _viewRoot;
        private Transform _handPivot;      // parent that animates (raise/lower)
        private Transform _grenadeMesh;    // the visible grenade in hand
        private Vector3   _handRestPos;    // idle position of the hand pivot
        private Vector3   _handRaisePos;   // raised (cooking) position
        private bool      _viewmodelVisible;

        // Idle bob
        private float _bobTime;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _grenadeCount = maxGrenades;
            BuildViewmodel();
            SetViewmodelVisible(false);
        }

        private void Start()
        {
            // Deferred to Start so UIManager.Instance is guaranteed to exist
            UpdateHUD();
        }

        private void Update()
        {
            if (Managers.GameManager.Instance != null &&
                Managers.GameManager.Instance.CurrentState != Managers.GameState.Playing) return;

            AnimateViewmodel();

            if (_grenadeCount <= 0) return;

            if (Input.GetKeyDown(KeyCode.G))
            {
                _cooking   = true;
                _cookTimer = 0f;
                SetViewmodelVisible(true);
            }

            if (_cooking)
            {
                _cookTimer = Mathf.Min(_cookTimer + Time.deltaTime, maxCookTime);

                if (Input.GetKeyUp(KeyCode.G))
                    StartCoroutine(ThrowRoutine());
            }
        }

        // ── Throw routine with throw animation ───────────────────────────────
        private IEnumerator ThrowRoutine()
        {
            _cooking = false;
            _grenadeCount--;
            UpdateHUD();

            // Animate arm swinging forward
            float t = 0f;
            Vector3 throwPos = _handRaisePos + new Vector3(0f, 0.05f, 0.15f);
            while (t < 0.12f)
            {
                t += Time.deltaTime;
                if (_handPivot != null)
                    _handPivot.localPosition = Vector3.Lerp(
                        _handRaisePos, throwPos, t / 0.12f);
                yield return null;
            }

            // Spawn and launch the actual grenade
            LaunchGrenade();

            // Hide grenade mesh immediately after release
            if (_grenadeMesh != null) _grenadeMesh.gameObject.SetActive(false);

            // Retract arm
            t = 0f;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                if (_handPivot != null)
                    _handPivot.localPosition = Vector3.Lerp(throwPos, _handRestPos, t / 0.2f);
                yield return null;
            }

            SetViewmodelVisible(false);
            // Restore grenade mesh for next throw
            if (_grenadeMesh != null) _grenadeMesh.gameObject.SetActive(true);
        }

        private void LaunchGrenade()
        {
            Transform origin = throwOrigin != null ? throwOrigin : Camera.main?.transform;
            if (origin == null) return;

            Vector3 spawnPos = origin.position + origin.forward * 0.5f;

            GameObject go;
            if (grenadePrefab != null)
                go = Instantiate(grenadePrefab, spawnPos, origin.rotation);
            else
            {
                go = Utilities.Grenade.SpawnProcedural(spawnPos).gameObject;
            }

            go.GetComponent<Utilities.Grenade>()?.ReduceFuse(_cookTimer);

            float force = Mathf.Lerp(minThrowForce, maxThrowForce, _cookTimer / maxCookTime);
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.AddForce(origin.forward * force, ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * 4f, ForceMode.VelocityChange);
            }
        }

        // ── Viewmodel animation ───────────────────────────────────────────────
        private void AnimateViewmodel()
        {
            if (!_viewmodelVisible || _handPivot == null) return;

            // Idle bob
            _bobTime += Time.deltaTime * 1.8f;
            float bob = Mathf.Sin(_bobTime) * 0.004f;

            // Smoothly raise when cooking
            Vector3 target = _cooking ? _handRaisePos : _handRestPos;
            target.y += bob;
            _handPivot.localPosition = Vector3.Lerp(
                _handPivot.localPosition, target, 12f * Time.deltaTime);

            // Slight tilt when cooking (pin-pull feel)
            float targetTilt = _cooking ? -15f : 0f;
            _handPivot.localRotation = Quaternion.Lerp(
                _handPivot.localRotation,
                Quaternion.Euler(targetTilt, 0f, 0f),
                10f * Time.deltaTime);
        }

        // ── Viewmodel construction ────────────────────────────────────────────
        private void BuildViewmodel()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            // Root: child of camera, layer = whatever the camera renders
            _viewRoot = new GameObject("GrenadeViewModel").transform;
            _viewRoot.SetParent(cam.transform, false);
            _viewRoot.localPosition = Vector3.zero;
            _viewRoot.localRotation = Quaternion.identity;

            // Hand pivot — controls raise/lower animation
            var pivotGO = new GameObject("HandPivot");
            _handPivot = pivotGO.transform;
            _handPivot.SetParent(_viewRoot, false);

            _handRestPos  = new Vector3( 0.22f, -0.28f, 0.36f);
            _handRaisePos = new Vector3( 0.18f, -0.14f, 0.32f);
            _handPivot.localPosition = _handRestPos;

            // --- Forearm (cylinder) ---
            var arm = MakeViewPart("Arm", PrimitiveType.Cylinder, _handPivot,
                new Vector3(0f, -0.04f, 0.01f),
                new Vector3(0.06f, 0.12f, 0.06f),
                new Color(0.76f, 0.61f, 0.50f));   // skin tone
            // Tilt cylinder to lie along local Z
            arm.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // --- Hand (box) ---
            MakeViewPart("Hand", PrimitiveType.Cube, _handPivot,
                new Vector3(0f, -0.065f, 0.065f),
                new Vector3(0.075f, 0.055f, 0.08f),
                new Color(0.72f, 0.57f, 0.46f));

            // --- Grenade body (sphere) ---
            _grenadeMesh = MakeViewPart("GrenadeMesh", PrimitiveType.Sphere, _handPivot,
                new Vector3(0f, 0.02f, 0.055f),
                Vector3.one * 0.068f,
                new Color(0.20f, 0.27f, 0.12f));   // dark olive

            // --- Pin ring (thin torus approximated with a cylinder ring) ---
            var pin = MakeViewPart("Pin", PrimitiveType.Cylinder, _grenadeMesh,
                new Vector3(0.04f, 0.025f, 0f),
                new Vector3(0.008f, 0.018f, 0.008f),
                new Color(0.75f, 0.75f, 0.75f));   // silver
            pin.localRotation = Quaternion.Euler(0f, 0f, 90f);

            // Disable shadow casting on all view parts to avoid self-shadowing
            foreach (var r in _viewRoot.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows    = false;
            }
        }

        private Transform MakeViewPart(string partName, PrimitiveType shape,
            Transform parent, Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = partName;
            // Remove collider — viewmodel must not interact with physics
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = localScale;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.25f);
            go.GetComponent<Renderer>().material = mat;

            return go.transform;
        }

        private void SetViewmodelVisible(bool visible)
        {
            _viewmodelVisible = visible;
            if (_viewRoot != null) _viewRoot.gameObject.SetActive(visible);
        }

        // ── Public API ────────────────────────────────────────────────────────
        public void RefillGrenades(int amount = 1)
        {
            _grenadeCount = Mathf.Min(_grenadeCount + amount, maxGrenades);
            UpdateHUD();
        }

        private void UpdateHUD()
        {
            Managers.UIManager.Instance?.ShowGrenadeCount(_grenadeCount);
        }
    }
}

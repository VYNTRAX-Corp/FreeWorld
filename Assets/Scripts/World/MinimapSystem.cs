using UnityEngine;
using UnityEngine.UI;

namespace FreeWorld.World
{
    /// <summary>
    /// Runtime minimap rendered by a dedicated overhead camera onto a RenderTexture.
    ///
    /// Creates all objects it needs on Awake:
    ///   • Minimap Camera   — orthographic, high up, follows player
    ///   • RenderTexture    — 256×256 (or configurable)
    ///   • UI RawImage      — circular mask, bottom-right of screen
    ///   • Player dot       — white circle in the center of the minimap
    ///   • North indicator  — tiny arrow above the circle
    ///
    /// Drop this component on any persistent GameObject (e.g. WorldManager).
    /// </summary>
    public class MinimapSystem : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Camera")]
        [SerializeField] private float cameraHeight  = 200f;  // world units above player
        [SerializeField] private float orthoSize     = 150f;  // half-width shown (world units)

        [Header("Render Texture")]
        [SerializeField] private int   rtSize        = 256;

        [Header("UI")]
        [SerializeField] private Vector2 mapPosition  = new Vector2(-20f, 20f); // from bottom-right
        [SerializeField] private float   mapDiameter  = 120f;
        [SerializeField] private Color   borderColor  = new Color(0.12f, 0.12f, 0.14f, 0.85f);

        // ── Private ───────────────────────────────────────────────────────────
        private Camera       _minimapCam;
        private Transform    _player;
        private RectTransform _playerDot;

        // ── Singleton ────────────────────────────────────────────────────────
        public static MinimapSystem Instance { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            Instance = this;
            BuildCamera();
            BuildUI();
        }

        private void LateUpdate()
        {
            if (_player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) _player = p.transform;
            }

            if (_player != null && _minimapCam != null)
            {
                // Camera follows player position, fixed height
                var cp  = _player.position;
                cp.y    = _player.position.y + cameraHeight;
                _minimapCam.transform.position = cp;

                // Keep the player marker aligned with player facing direction.
                if (_playerDot != null)
                    _playerDot.localEulerAngles = new Vector3(0f, 0f, -_player.eulerAngles.y);
            }
        }

        // ── Camera creation ───────────────────────────────────────────────────
        private void BuildCamera()
        {
            GameObject camGO = new GameObject("MinimapCamera");
            camGO.transform.SetParent(transform);
            camGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            _minimapCam = camGO.AddComponent<Camera>();
            _minimapCam.orthographic       = true;
            _minimapCam.orthographicSize   = orthoSize;
            _minimapCam.clearFlags         = CameraClearFlags.SolidColor;
            _minimapCam.backgroundColor    = new Color(0.10f, 0.12f, 0.14f, 1f);
            _minimapCam.cullingMask        = ~(1 << LayerMask.NameToLayer("UI")); // skip UI layer
            _minimapCam.nearClipPlane      = 1f;
            _minimapCam.farClipPlane       = cameraHeight + 50f;
            _minimapCam.depth              = -2;

            // Assign a culling layer for the minimap camera only (optional)
            var rt = new RenderTexture(rtSize, rtSize, 16, RenderTextureFormat.ARGB32);
            rt.name = "MinimapRT";
            rt.Create();
            _minimapCam.targetTexture = rt;
        }

        // ── UI creation ───────────────────────────────────────────────────────
        private void BuildUI()
        {
            Canvas canvas = FindOrCreateMinimapCanvas();
            Transform cv  = canvas.transform;

            // ── Border (slightly larger circle behind the map) ────────────────
            var borderGO = new GameObject("MinimapBorder");
            borderGO.transform.SetParent(cv, false);
            var borderRT = borderGO.AddComponent<RectTransform>();
            SetAnchorBottomRight(borderRT, mapPosition, new Vector2(mapDiameter + 8f, mapDiameter + 8f));
            var borderImg = borderGO.AddComponent<Image>();
            borderImg.color        = borderColor;
            borderImg.sprite       = CreateCircleSprite(64);
            borderImg.raycastTarget = false;

            // ── Mask root (circle Image that clips its children) ──────────────
            var maskGO = new GameObject("MinimapMask");
            maskGO.transform.SetParent(cv, false);
            var maskRT = maskGO.AddComponent<RectTransform>();
            SetAnchorBottomRight(maskRT, mapPosition, new Vector2(mapDiameter, mapDiameter));
            var maskImg = maskGO.AddComponent<Image>();
            maskImg.sprite         = CreateCircleSprite(64);
            maskImg.color          = Color.white;
            maskImg.raycastTarget  = false;
            var mask = maskGO.AddComponent<Mask>();
            mask.showMaskGraphic   = false; // hide the mask shape itself

            // ── RawImage (render texture) as child of mask ────────────────────
            var mapGO = new GameObject("MinimapImage");
            mapGO.transform.SetParent(maskGO.transform, false);
            var mapRT = mapGO.AddComponent<RectTransform>();
            mapRT.anchorMin        = Vector2.zero;
            mapRT.anchorMax        = Vector2.one;
            mapRT.offsetMin        = mapRT.offsetMax = Vector2.zero;
            var rawImg = mapGO.AddComponent<RawImage>();
            rawImg.texture         = _minimapCam.targetTexture;
            rawImg.raycastTarget   = false;

            // ── Player dot (green arrow, centred in mask) ─────────────────────
            var dotGO  = new GameObject("PlayerDot");
            dotGO.transform.SetParent(maskGO.transform, false);
            _playerDot = dotGO.AddComponent<RectTransform>();
            _playerDot.anchorMin        = _playerDot.anchorMax = new Vector2(0.5f, 0.5f);
            _playerDot.sizeDelta        = new Vector2(10f, 14f);
            _playerDot.anchoredPosition = Vector2.zero;
            var dotImg = dotGO.AddComponent<Image>();
            dotImg.color        = new Color(0.15f, 0.85f, 0.25f);
            dotImg.sprite       = CreateArrowSprite();
            dotImg.raycastTarget = false;

            // ── North label (above the circle) ────────────────────────────────
            var northGO = new GameObject("NorthLabel");
            northGO.transform.SetParent(cv, false);
            var northRT  = northGO.AddComponent<RectTransform>();
            SetAnchorBottomRight(northRT,
                mapPosition + new Vector2(0f, mapDiameter * 0.5f + 14f),
                new Vector2(30f, 20f));
            var northText = northGO.AddComponent<Text>();
            northText.text          = "N";
            northText.color         = new Color(0.85f, 0.85f, 0.85f);
            northText.fontSize      = 14;
            northText.alignment     = TextAnchor.MiddleCenter;
            northText.raycastTarget = false;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static Canvas FindOrCreateMinimapCanvas()
        {
            // Reuse dedicated minimap canvas if it already exists.
            var existing = GameObject.Find("MinimapCanvas");
            if (existing != null)
            {
                var existingCanvas = existing.GetComponent<Canvas>();
                if (existingCanvas != null) return existingCanvas;
            }

            // Use a dedicated minimap canvas — never share with game HUD.
            var go     = new GameObject("MinimapCanvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void SetAnchorBottomRight(RectTransform rt, Vector2 offset, Vector2 size)
        {
            rt.anchorMin        = new Vector2(1f, 0f);
            rt.anchorMax        = new Vector2(1f, 0f);
            rt.pivot            = new Vector2(1f, 0f);
            rt.anchoredPosition = offset;
            rt.sizeDelta        = size;
        }

        // ── Procedural sprites ────────────────────────────────────────────────
        private static Sprite CreateCircleSprite(int resolution)
        {
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            float half = resolution * 0.5f;
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dx = x - half; float dy = y - half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float a    = Mathf.Clamp01((half - dist) / 1.5f);
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, resolution, resolution),
                new Vector2(0.5f, 0.5f), resolution);
        }

        private static Sprite CreateArrowSprite()
        {
            // Small 16×20 upward arrow
            int w = 16, h = 20;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(new Color[w * h]); // all transparent

            // Draw rough arrow pixels
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (float)x / w;
                    float ny = (float)y / h;
                    // Arrow shape
                    bool inArrow = ny > 0.45f
                        ? Mathf.Abs(nx - 0.5f) < (1f - ny)   // triangle top
                        : Mathf.Abs(nx - 0.5f) < 0.18f;      // shaft bottom
                    if (inArrow) tex.SetPixel(x, y, Color.white);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
        }
    }
}

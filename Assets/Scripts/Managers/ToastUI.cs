using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FreeWorld.Managers
{
    /// <summary>
    /// Displays brief on-screen notification toasts.
    ///
    /// Usage from anywhere:
    ///   ToastUI.Show("GAME SAVED");
    ///   ToastUI.Show("SPEED → Level 3", ToastUI.Level);
    ///
    /// Auto-creates itself on first use; no scene setup needed.
    /// </summary>
    public class ToastUI : MonoBehaviour
    {
        // ── Toast presets ──────────────────────────────────────────────────────
        public static readonly ToastStyle Save      = new ToastStyle(new Color(0.20f, 0.85f, 0.35f), 1.8f);
        public static readonly ToastStyle Load      = new ToastStyle(new Color(0.25f, 0.65f, 1.00f), 1.8f);
        public static readonly ToastStyle Level     = new ToastStyle(new Color(1.00f, 0.85f, 0.20f), 2.5f);
        public static readonly ToastStyle Warning   = new ToastStyle(new Color(1.00f, 0.45f, 0.15f), 2.0f);
        public static readonly ToastStyle Info      = new ToastStyle(new Color(0.90f, 0.90f, 0.90f), 1.5f);

        public class ToastStyle
        {
            public Color color;
            public float duration;
            public ToastStyle(Color c, float d) { color = c; duration = d; }
        }

        // ── Singleton ─────────────────────────────────────────────────────────
        private static ToastUI _instance;
        private static ToastUI Instance
        {
            get
            {
                if (_instance == null) _instance = CreateInstance();
                return _instance;
            }
        }

        // ── Queue ─────────────────────────────────────────────────────────────
        private class ToastEntry
        {
            public string     message;
            public ToastStyle style;
        }
        private readonly Queue<ToastEntry> _queue = new Queue<ToastEntry>();
        private bool _showing;

        // ── UI refs ───────────────────────────────────────────────────────────
        private CanvasGroup _group;
        private Text        _label;
        private RectTransform _bg;

        // ─────────────────────────────────────────────────────────────────────
        public static void Show(string message, ToastStyle style = null)
        {
            style ??= Info;
            Instance._queue.Enqueue(new ToastEntry { message = message, style = style });
            if (!Instance._showing)
                Instance.StartCoroutine(Instance.ShowNext());
        }

        private IEnumerator ShowNext()
        {
            while (_queue.Count > 0)
            {
                _showing = true;
                var entry = _queue.Dequeue();

                _label.text  = entry.message;
                _label.color = entry.style.color;

                // Resize bg to fit text
                _bg.sizeDelta = new Vector2(
                    Mathf.Clamp(_label.preferredWidth + 36f, 200f, 600f),
                    44f);

                // Fade in
                yield return StartCoroutine(Fade(0f, 1f, 0.18f));

                // Hold
                yield return new WaitForSecondsRealtime(entry.style.duration);

                // Fade out
                yield return StartCoroutine(Fade(1f, 0f, 0.30f));

                // Small gap between queued toasts
                yield return new WaitForSecondsRealtime(0.1f);
            }
            _showing = false;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            _group.alpha = to;
        }

        // ── Construction ──────────────────────────────────────────────────────
        private static ToastUI CreateInstance()
        {
            var root = new GameObject("ToastUI");
            DontDestroyOnLoad(root);
            var ui = root.AddComponent<ToastUI>();

            // Canvas
            var c  = root.AddComponent<Canvas>();
            c.renderMode   = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 100; // above everything
            root.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;

            // Background pill
            var bgGO = new GameObject("ToastBG", typeof(RectTransform));
            bgGO.transform.SetParent(root.transform, false);
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin        = new Vector2(0.5f, 1f);
            bgRT.anchorMax        = new Vector2(0.5f, 1f);
            bgRT.pivot            = new Vector2(0.5f, 1f);
            bgRT.anchoredPosition = new Vector2(0f, -24f);
            bgRT.sizeDelta        = new Vector2(280f, 44f);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.06f, 0.06f, 0.06f, 0.88f);
            ui._bg = bgRT;

            // CanvasGroup for fading
            var cg = bgGO.AddComponent<CanvasGroup>();
            cg.alpha          = 0f;
            cg.blocksRaycasts = false;
            ui._group = cg;

            // Label
            var lblGO = new GameObject("Label", typeof(RectTransform));
            lblGO.transform.SetParent(bgGO.transform, false);
            var lblRT = lblGO.GetComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero;
            lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = new Vector2(12f, 0f);
            lblRT.offsetMax = new Vector2(-12f, 0f);
            var txt = lblGO.AddComponent<Text>();
            txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize  = 16;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color     = Color.white;
            ui._label = txt;

            return ui;
        }
    }
}

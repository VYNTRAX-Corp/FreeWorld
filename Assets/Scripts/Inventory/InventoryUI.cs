using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PlayerInventory = FreeWorld.Inventory.Inventory;

namespace FreeWorld.Managers
{
    /// <summary>
    /// Combined Player Panel — Inventory + Scoreboard in two tabs.
    /// I   → toggle, opens on Inventory tab.
    /// Tab → toggle, opens on Scoreboard tab.
    /// Clicking a tab header switches without closing.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        public enum Tab { Inventory, Scoreboard, Stats }

        // ── Colours ───────────────────────────────────────────────────────────
        private static readonly Color SlotNormal  = new Color(0.12f, 0.12f, 0.12f, 0.90f);
        private static readonly Color SlotHover   = new Color(0.22f, 0.22f, 0.22f, 0.95f);
        private static readonly Color PanelBg     = new Color(0.05f, 0.05f, 0.05f, 0.88f);
        private static readonly Color FrameColor  = new Color(0.35f, 0.35f, 0.35f, 1.00f);
        private static readonly Color TextColor   = new Color(0.90f, 0.90f, 0.90f, 1.00f);
        private static readonly Color EmptyIcon   = new Color(1f,    1f,    1f,    0.12f);
        private static readonly Color TabActive   = new Color(0.18f, 0.45f, 0.75f, 1.00f);
        private static readonly Color TabInactive = new Color(0.10f, 0.10f, 0.10f, 1.00f);
        private static readonly Color HeaderGold  = new Color(1.00f, 0.85f, 0.20f, 1.00f);

        // ── Layout ────────────────────────────────────────────────────────────
        private const float SlotSize = 64f;
        private const float SlotPad  = 6f;
        private const float PadX     = 16f;
        private const float TabBarH  = 30f;
        private const float FooterH  = 12f;

        // ── Debug ─────────────────────────────────────────────────────────────
        [Header("Debug")]
        [Tooltip("Assign any ItemDefinition. Press F9 in Play mode to add 5.")]
        [SerializeField] private FreeWorld.Inventory.ItemDefinition debugTestItem;

        // ── Runtime refs ─────────────────────────────────────────────────────
        private PlayerInventory _inventory;
        private Canvas          _canvas;
        private GameObject      _panel;
        private bool            _open;
        private Tab             _activeTab = Tab.Inventory;

        // Inventory tab
        private GameObject   _inventoryContent;
        private SlotWidget[] _slots;
        private Image        _btnInventoryBg;

        // Scoreboard tab
        private GameObject            _scoreboardContent;
        private Image                 _btnScoreboardBg;
        private Transform             _scoreRowContainer;
        private readonly List<GameObject> _scoreRows = new List<GameObject>();

        // Stats tab
        private GameObject  _statsContent;
        private Image       _btnStatsBg;
        private StatBarWidget[] _statBars;

        private struct SlotWidget
        {
            public Image bg, icon;
            public Text  countLabel;
        }

        private struct StatBarWidget
        {
            public Text  levelLabel;
            public RectTransform fill;
            public Text  xpLabel;
            public float innerWidth;
        }

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            if (_inventory == null) _inventory = gameObject.AddComponent<PlayerInventory>();
            _inventory.OnChanged += RefreshInventory;
        }

        private void Start()
        {
            BuildUI();
            _panel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_inventory != null) _inventory.OnChanged -= RefreshInventory;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;

            if (Input.GetKeyDown(KeyCode.I))   ToggleOrSwitch(Tab.Inventory);
            if (Input.GetKeyDown(KeyCode.Tab)) ToggleOrSwitch(Tab.Scoreboard);

#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.F9) && debugTestItem != null)
            {
                _inventory.Add(debugTestItem, 5);
                Debug.Log($"[PlayerPanel] Added 5x `{debugTestItem.itemName}` via F9.");
            }
#endif
        }

        // ── Open / Close / Switch ─────────────────────────────────────────────
        private void ToggleOrSwitch(Tab requested)
        {
            if (!_open)                       Open(requested);
            else if (_activeTab == requested) Close();
            else                              SwitchTab(requested);
        }

        private void Open(Tab tab)
        {
            _open = true;
            _panel.SetActive(true);
            SwitchTab(tab);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }

        private void Close()
        {
            _open = false;
            _panel.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        public void SwitchTab(Tab tab)
        {
            _activeTab = tab;
            _inventoryContent.SetActive(tab == Tab.Inventory);
            _scoreboardContent.SetActive(tab == Tab.Scoreboard);
            _statsContent.SetActive(tab == Tab.Stats);
            _btnInventoryBg.color  = tab == Tab.Inventory  ? TabActive : TabInactive;
            _btnScoreboardBg.color = tab == Tab.Scoreboard ? TabActive : TabInactive;
            _btnStatsBg.color      = tab == Tab.Stats       ? TabActive : TabInactive;
            if (tab == Tab.Stats) RefreshStats();
        }

        // ── Called by Scoreboard.cs ───────────────────────────────────────────
        public void ShowScoreboard(bool show)
        {
            if (show && !_open)
                Open(Tab.Scoreboard);
            else if (!show && _open && _activeTab == Tab.Scoreboard)
                Close();
        }

        public void RefreshScoreboard(IList<Scoreboard.PlayerEntry> entries)
        {
            if (_scoreRowContainer == null) return;

            foreach (var r in _scoreRows) Destroy(r);
            _scoreRows.Clear();

            var h = BuildScoreRow("#", "NAME", "KILLS", "DEATHS", "SCORE", HeaderGold);
            h.transform.SetParent(_scoreRowContainer, false);
            _scoreRows.Add(h);
            AddSeparator();

            var sorted = new List<Scoreboard.PlayerEntry>(entries);
            sorted.Sort((a, b) => b.Score.CompareTo(a.Score));

            for (int i = 0; i < sorted.Count; i++)
            {
                var e   = sorted[i];
                Color c = i == 0 ? HeaderGold : TextColor;
                var row = BuildScoreRow((i + 1).ToString(), e.Name,
                                        e.Kills.ToString(), e.Deaths.ToString(),
                                        e.Score.ToString("N0"), c);
                row.transform.SetParent(_scoreRowContainer, false);
                _scoreRows.Add(row);
            }
        }

        // ── Inventory refresh ─────────────────────────────────────────────────
        public void RefreshInventory()
        {
            if (_slots == null) return;
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _inventory.GetSlot(i);
                if (slot.IsEmpty)
                {
                    _slots[i].icon.sprite     = null;
                    _slots[i].icon.color      = EmptyIcon;
                    _slots[i].countLabel.text = "";
                }
                else
                {
                    _slots[i].icon.sprite     = slot.item.icon;
                    _slots[i].icon.color      = slot.item.icon != null
                        ? Color.white : new Color(0.6f, 0.6f, 0.6f, 0.5f);
                    _slots[i].countLabel.text = slot.item.stackable && slot.count > 1
                        ? slot.count.ToString() : "";
                }
            }
        }

        // ── Slot interactions ─────────────────────────────────────────────────
        internal void OnSlotLeftClick(int i)  => _inventory.UseSlot(i);
        internal void OnSlotRightClick(int i) => _inventory.DropSlot(i);
        internal void SetHover(int i, bool on)
        {
            if (_slots != null && i >= 0 && i < _slots.Length)
                _slots[i].bg.color = on ? SlotHover : SlotNormal;
        }

        // ── Build ─────────────────────────────────────────────────────────────
        private void BuildUI()
        {
            _canvas = BuildCanvas();

            int   cols   = _inventory.Columns;
            int   rows   = _inventory.Rows;
            float gridW  = cols * (SlotSize + SlotPad) - SlotPad;
            float gridH  = rows * (SlotSize + SlotPad) - SlotPad;
            float panelW = gridW + PadX * 2;
            float panelH = gridH + TabBarH + FooterH + PadX;

            _panel = MakeRect("PlayerPanel", _canvas.transform);
            var pRT = _panel.GetComponent<RectTransform>();
            pRT.anchorMin = pRT.anchorMax = new Vector2(0.5f, 0.5f);
            pRT.pivot     = new Vector2(0.5f, 0.5f);
            pRT.sizeDelta = new Vector2(panelW, panelH);
            _panel.AddComponent<Image>().color = PanelBg;

            // Tab bar
            var tabBar   = MakeRect("TabBar", _panel.transform);
            var tabBarRT = tabBar.GetComponent<RectTransform>();
            tabBarRT.anchorMin = new Vector2(0, 1); tabBarRT.anchorMax = new Vector2(1, 1);
            tabBarRT.pivot     = new Vector2(0.5f, 1);
            tabBarRT.offsetMin = new Vector2(0, -TabBarH);
            tabBarRT.offsetMax = Vector2.zero;

            _btnInventoryBg  = BuildTabButton(tabBar, "INVENTORY",  0.00f, 0.335f, Tab.Inventory);
            _btnScoreboardBg = BuildTabButton(tabBar, "SCOREBOARD", 0.335f, 0.670f, Tab.Scoreboard);
            _btnStatsBg      = BuildTabButton(tabBar, "STATS",      0.670f, 1.000f, Tab.Stats);

            float contentTop = -TabBarH;

            // Inventory content
            _inventoryContent = MakeRect("InventoryContent", _panel.transform);
            var invRT = _inventoryContent.GetComponent<RectTransform>();
            invRT.anchorMin = Vector2.zero; invRT.anchorMax = Vector2.one;
            invRT.offsetMin = new Vector2(PadX, FooterH);
            invRT.offsetMax = new Vector2(-PadX, contentTop);
            BuildInventoryGrid(_inventoryContent.transform, cols, rows);

            // Scoreboard content
            _scoreboardContent = MakeRect("ScoreboardContent", _panel.transform);
            var sbRT = _scoreboardContent.GetComponent<RectTransform>();
            sbRT.anchorMin = Vector2.zero; sbRT.anchorMax = Vector2.one;
            sbRT.offsetMin = new Vector2(PadX, FooterH);
            sbRT.offsetMax = new Vector2(-PadX, contentTop);

            var vlg = _scoreboardContent.AddComponent<VerticalLayoutGroup>();
            vlg.spacing              = 3f;
            vlg.childControlWidth    = true;
            vlg.childControlHeight   = false;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(0, 0, 4, 4);
            _scoreRowContainer = _scoreboardContent.transform;

            // Stats content
            _statsContent = MakeRect("StatsContent", _panel.transform);
            var stRT = _statsContent.GetComponent<RectTransform>();
            stRT.anchorMin = Vector2.zero; stRT.anchorMax = Vector2.one;
            stRT.offsetMin = new Vector2(PadX, FooterH);
            stRT.offsetMax = new Vector2(-PadX, contentTop);
            BuildStatsContent(_statsContent.transform);

            SwitchTab(Tab.Inventory);
            RefreshInventory();
        }

        private void BuildStatsContent(Transform parent)
        {
            var vlg = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing              = 14f;
            vlg.childControlWidth    = true;
            vlg.childControlHeight   = false;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(4, 4, 12, 4);

            string[] names = { "Strength", "Speed", "Crafting", "Combat" };
            Color[]  cols  = {
                new Color(0.9f, 0.4f, 0.2f), // orange
                new Color(0.2f, 0.8f, 0.4f), // green
                new Color(0.5f, 0.6f, 1.0f), // blue
                new Color(1.0f, 0.3f, 0.3f), // red
            };

            _statBars = new StatBarWidget[4];
            for (int i = 0; i < 4; i++)
            {
                _statBars[i] = BuildStatRow(parent, names[i], cols[i]);
            }
        }

        private StatBarWidget BuildStatRow(Transform parent, string statName, Color barColor)
        {
            var row   = MakeRect(statName + "Row", parent);
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 38f;

            // Name label
            var nameGO  = MakeRect("Name", row.transform);
            var nameRT  = nameGO.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 0.5f); nameRT.anchorMax = new Vector2(0.22f, 1f);
            nameRT.offsetMin = nameRT.offsetMax = Vector2.zero;
            var nameTxt = nameGO.AddComponent<Text>();
            nameTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameTxt.text      = statName.ToUpper();
            nameTxt.fontSize  = 13;
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.alignment = TextAnchor.MiddleLeft;
            nameTxt.color     = TextColor;

            // Level label
            var lvlGO  = MakeRect("Level", row.transform);
            var lvlRT  = lvlGO.GetComponent<RectTransform>();
            lvlRT.anchorMin = new Vector2(0.22f, 0.5f); lvlRT.anchorMax = new Vector2(0.38f, 1f);
            lvlRT.offsetMin = lvlRT.offsetMax = Vector2.zero;
            var lvlTxt = lvlGO.AddComponent<Text>();
            lvlTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lvlTxt.text      = "Lv.1";
            lvlTxt.fontSize  = 13;
            lvlTxt.alignment = TextAnchor.MiddleLeft;
            lvlTxt.color     = HeaderGold;

            // Bar BG
            var bgGO   = MakeRect("BarBG", row.transform);
            var bgRT   = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0.38f, 0f); bgRT.anchorMax = new Vector2(0.88f, 0.45f);
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
            bgGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 1f);

            // Bar fill
            var fillGO = MakeRect("Fill", bgGO.transform);
            var fillRT = fillGO.GetComponent<RectTransform>();
            fillRT.anchorMin = new Vector2(0, 0); fillRT.anchorMax = new Vector2(0, 1);
            fillRT.pivot     = new Vector2(0, 0.5f);
            fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;
            fillGO.AddComponent<Image>().color = barColor;

            // XP label
            var xpGO   = MakeRect("XP", row.transform);
            var xpRT   = xpGO.GetComponent<RectTransform>();
            xpRT.anchorMin = new Vector2(0.88f, 0f); xpRT.anchorMax = new Vector2(1f, 1f);
            xpRT.offsetMin = xpRT.offsetMax = Vector2.zero;
            var xpTxt  = xpGO.AddComponent<Text>();
            xpTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            xpTxt.text      = "0 / 100";
            xpTxt.fontSize  = 11;
            xpTxt.alignment = TextAnchor.MiddleRight;
            xpTxt.color     = TextColor;

            return new StatBarWidget
            {
                levelLabel = lvlTxt,
                fill       = fillRT,
                xpLabel    = xpTxt,
                innerWidth = 0f // resolved on first Refresh via layout
            };
        }

        public void RefreshStats()
        {
            var ps = Player.PlayerStats.Instance;
            if (ps == null || _statBars == null) return;

            Player.PlayerStats.Stat[] stats = {
                ps.Strength, ps.Speed, ps.Crafting, ps.Combat
            };

            for (int i = 0; i < stats.Length; i++)
            {
                var s  = stats[i];
                var w  = _statBars[i];

                w.levelLabel.text = $"Lv.{s.level}";
                w.xpLabel.text    = $"{Mathf.FloorToInt(s.xp)} / {Mathf.FloorToInt(s.xpToNext)}";

                // Resolve bar width lazily from parent rect
                var bgRT = w.fill.parent.GetComponent<RectTransform>();
                float inner = bgRT.rect.width;
                if (inner < 1f) inner = bgRT.sizeDelta.x;
                w.fill.sizeDelta = new Vector2(inner * s.NormalisedXP, 0f);
            }
        }

        private Image BuildTabButton(GameObject bar, string lbl,
                                      float x0, float x1, Tab tab)
        {
            var go  = MakeRect($"Btn_{lbl}", bar.transform);
            var rt  = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(x0, 0); rt.anchorMax = new Vector2(x1, 1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = TabInactive;
            var txt = MakeText(lbl, go.transform, lbl, 12, FontStyle.Bold);
            var tRT = txt.GetComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
            tRT.offsetMin = tRT.offsetMax = Vector2.zero;
            var click = go.AddComponent<TabClickHandler>();
            click.Init(tab, this);
            return img;
        }

        private void BuildInventoryGrid(Transform parent, int cols, int rows)
        {
            _slots = new SlotWidget[_inventory.TotalSlots];
            for (int i = 0; i < _inventory.TotalSlots; i++)
            {
                int   col = i % cols;
                int   row = i / cols;
                float x   = col * (SlotSize + SlotPad);
                float y   = -(row * (SlotSize + SlotPad));

                var go  = MakeRect($"Slot_{i}", parent);
                var rt  = go.GetComponent<RectTransform>();
                rt.anchorMin        = new Vector2(0, 1);
                rt.anchorMax        = new Vector2(0, 1);
                rt.pivot            = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(x, y);
                rt.sizeDelta        = new Vector2(SlotSize, SlotSize);

                var bg   = go.AddComponent<Image>();
                bg.color = SlotNormal;

                var fGO = MakeRect("Frame", go.transform);
                var fRT = fGO.GetComponent<RectTransform>();
                fRT.anchorMin = Vector2.zero; fRT.anchorMax = Vector2.one;
                fRT.offsetMin = new Vector2(-1, -1); fRT.offsetMax = new Vector2(1, 1);
                fGO.AddComponent<Image>().color = FrameColor;
                fGO.transform.SetAsFirstSibling();

                var iGO  = MakeRect("Icon", go.transform);
                var iRT  = iGO.GetComponent<RectTransform>();
                iRT.anchorMin = new Vector2(0.1f, 0.1f);
                iRT.anchorMax = new Vector2(0.9f, 0.9f);
                iRT.offsetMin = iRT.offsetMax = Vector2.zero;
                var icon = iGO.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.color = EmptyIcon;

                var cGO  = MakeRect("Count", go.transform);
                var cRT  = cGO.GetComponent<RectTransform>();
                cRT.anchorMin = new Vector2(0, 0); cRT.anchorMax = new Vector2(1, 0);
                cRT.pivot     = new Vector2(1, 0);
                cRT.offsetMin = new Vector2(2, 2); cRT.offsetMax = new Vector2(-2, 18);
                var cnt  = cGO.AddComponent<Text>();
                cnt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                cnt.fontSize  = 11;
                cnt.alignment = TextAnchor.LowerRight;
                cnt.color     = TextColor;

                _slots[i] = new SlotWidget { bg = bg, icon = icon, countLabel = cnt };
                int idx = i;
                var h = go.AddComponent<SlotClickHandler>();
                h.Init(idx, this);
            }
        }

        private GameObject BuildScoreRow(string rank, string name,
                                          string kills, string deaths,
                                          string score, Color col)
        {
            var go = MakeRect("ScoreRow", _scoreRowContainer);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 22f;

            void Col(string txt, float a0, float a1)
            {
                var c  = MakeRect("C", go.transform);
                var rt = c.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(a0, 0); rt.anchorMax = new Vector2(a1, 1);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                var t  = c.AddComponent<Text>();
                t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                t.text      = txt;
                t.color     = col;
                t.fontSize  = 14;
                t.alignment = TextAnchor.MiddleLeft;
            }

            Col(rank,   0.00f, 0.07f);
            Col(name,   0.07f, 0.45f);
            Col(kills,  0.45f, 0.62f);
            Col(deaths, 0.62f, 0.79f);
            Col(score,  0.79f, 1.00f);
            return go;
        }

        private void AddSeparator()
        {
            var go  = MakeRect("Sep", _scoreRowContainer);
            var le  = go.AddComponent<LayoutElement>();
            le.preferredHeight = 1f;
            go.AddComponent<Image>().color = FrameColor;
            _scoreRows.Add(go);
        }

        private Canvas BuildCanvas()
        {
            var go = new GameObject("Canvas_PlayerPanel");
            var c  = go.AddComponent<Canvas>();
            c.renderMode   = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 50;
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();

            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
            return c;
        }

        private static GameObject MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Text MakeText(string name, Transform parent, string content,
                                      int size, FontStyle style = FontStyle.Normal)
        {
            var go  = MakeRect(name, parent);
            var txt = go.AddComponent<Text>();
            txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text      = content;
            txt.fontSize  = size;
            txt.fontStyle = style;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color     = TextColor;
            return txt;
        }
    }

    // ── Tab click handler ─────────────────────────────────────────────────────
    internal class TabClickHandler : MonoBehaviour,
        UnityEngine.EventSystems.IPointerClickHandler
    {
        private InventoryUI.Tab _tab;
        private InventoryUI     _ui;
        public void Init(InventoryUI.Tab tab, InventoryUI ui) { _tab = tab; _ui = ui; }
        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData _)
            => _ui?.SwitchTab(_tab);
    }

    // ── Slot click / hover handler ────────────────────────────────────────────
    internal class SlotClickHandler : MonoBehaviour,
        UnityEngine.EventSystems.IPointerEnterHandler,
        UnityEngine.EventSystems.IPointerExitHandler,
        UnityEngine.EventSystems.IPointerClickHandler
    {
        private int         _index;
        private InventoryUI _ui;
        public void Init(int i, InventoryUI ui) { _index = i; _ui = ui; }
        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData _) => _ui?.SetHover(_index, true);
        public void OnPointerExit (UnityEngine.EventSystems.PointerEventData _) => _ui?.SetHover(_index, false);
        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e)
        {
            if (e.button == UnityEngine.EventSystems.PointerEventData.InputButton.Left)
                _ui?.OnSlotLeftClick(_index);
            else if (e.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
                _ui?.OnSlotRightClick(_index);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace FreeWorld.Managers
{
    /// <summary>
    /// In-game scoreboard. Press Tab to show.
    /// Displays each "player" entry (name, kills, deaths, score).
    /// Extend for multiplayer by adding real player data.
    /// </summary>
    public class Scoreboard : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject     panel;
        [SerializeField] private Transform      rowContainer;
        [SerializeField] private GameObject     rowPrefab;     // auto-created if null

        [Header("Key Binding")]
        [SerializeField] private KeyCode        toggleKey = KeyCode.Tab;

        // ── Row data ──────────────────────────────────────────────────────────
        public class PlayerEntry
        {
            public string Name;
            public int    Kills;
            public int    Deaths;
            public int    Score;
        }

        private readonly List<PlayerEntry> _entries = new List<PlayerEntry>();
        private readonly List<GameObject>  _rows    = new List<GameObject>();
        private bool _visible;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            panel?.SetActive(false);

            // Add the local player entry
            AddEntry("You", 0, 0, 0);
        }

        private void OnEnable()
        {
            // Subscribe to GameManager events
            if (GameManager.Instance != null)
                GameManager.Instance.OnScoreChanged += OnLocalScoreChanged;
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnScoreChanged -= OnLocalScoreChanged;
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) Show(true);
            if (Input.GetKeyUp(toggleKey))   Show(false);
        }

        // ── Public API ────────────────────────────────────────────────────────
        public void AddEntry(string name, int kills, int deaths, int score)
        {
            _entries.Add(new PlayerEntry { Name = name, Kills = kills,
                                           Deaths = deaths, Score = score });
            RebuildRows();
        }

        public void RecordKill(string playerName)
        {
            var e = _entries.Find(x => x.Name == playerName);
            if (e == null) return;
            e.Kills++;
            e.Score += 100;
            RebuildRows();
        }

        public void RecordDeath(string playerName)
        {
            var e = _entries.Find(x => x.Name == playerName);
            if (e == null) return;
            e.Deaths++;
            RebuildRows();
        }

        // ── Internal ──────────────────────────────────────────────────────────
        private void OnLocalScoreChanged(int score)
        {
            if (_entries.Count > 0)
            {
                _entries[0].Score  = score;
                _entries[0].Kills  = GameManager.Instance != null
                                     ? GameManager.Instance.KillCount : 0;
            }
            if (_visible) RebuildRows();
        }

        private void Show(bool show)
        {
            _visible = show;
            panel?.SetActive(show);
            if (show)
            {
                RebuildRows();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible   = true;
            }
            else
            {
                bool playing = GameManager.Instance?.CurrentState == GameState.Playing;
                if (playing)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible   = false;
                }
            }
        }

        private void RebuildRows()
        {
            if (rowContainer == null) return;

            // Clear existing rows
            foreach (var r in _rows) Destroy(r);
            _rows.Clear();

            // Sort by score descending
            _entries.Sort((a, b) => b.Score.CompareTo(a.Score));

            int rank = 1;
            foreach (var entry in _entries)
            {
                GameObject row = CreateRow(rank, entry);
                row.transform.SetParent(rowContainer, false);
                _rows.Add(row);
                rank++;
            }
        }

        private GameObject CreateRow(int rank, PlayerEntry entry)
        {
            // Use provided prefab or build a simple row on the fly
            if (rowPrefab != null)
            {
                var go  = Instantiate(rowPrefab);
                var txts = go.GetComponentsInChildren<TextMeshProUGUI>();
                if (txts.Length >= 5)
                {
                    txts[0].text = rank.ToString();
                    txts[1].text = entry.Name;
                    txts[2].text = entry.Kills.ToString();
                    txts[3].text = entry.Deaths.ToString();
                    txts[4].text = entry.Score.ToString("N0");
                }
                return go;
            }

            // Fallback: one TMP showing everything on one line
            var fallback = new GameObject($"Row_{rank}");
            var tmp      = fallback.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 18f;
            tmp.text     = $"<mspace=22>{rank,2}.  {entry.Name,-16} " +
                           $"K:{entry.Kills,3}  D:{entry.Deaths,3}  Score:{entry.Score,6:N0}</mspace>";
            tmp.color    = rank == 1 ? new Color(1f, 0.85f, 0.2f) : Color.white;
            return fallback;
        }
    }
}

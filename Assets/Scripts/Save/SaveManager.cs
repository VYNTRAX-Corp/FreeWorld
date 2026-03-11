using System;
using System.IO;
using UnityEngine;
using FreeWorld.Player;
using FreeWorld.Inventory;

namespace FreeWorld.Save
{
    /// <summary>
    /// Handles writing and reading SaveData to/from JSON on disk.
    ///
    ///  F5           — manual save
    ///  F8           — manual load
    ///  Autosave     — every autosaveInterval seconds (default 60 s)
    ///
    /// Save file: Application.persistentDataPath/save.json
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        [Header("Autosave")]
        [SerializeField] private float autosaveInterval = 60f;

        public static SaveManager Instance { get; private set; }

        private static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

        private float _autosaveTimer;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // DontDestroyOnLoad only works on root GameObjects
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;

            _autosaveTimer += Time.deltaTime;
            if (_autosaveTimer >= autosaveInterval)
            {
                _autosaveTimer = 0f;
                Save();
            }

            if (Input.GetKeyDown(KeyCode.F5)) Save();
            if (Input.GetKeyDown(KeyCode.F8)) Load();
        }

        // ── Save ──────────────────────────────────────────────────────────────
        public void Save()
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) { Debug.LogWarning("[SaveManager] No Player tag found."); return; }

            var data = new SaveData
            {
                savedAt = DateTime.UtcNow.ToString("o"),
                posX    = player.transform.position.x,
                posY    = player.transform.position.y,
                posZ    = player.transform.position.z,
                rotY    = player.transform.eulerAngles.y,
            };

            // Health
            var ph = player.GetComponent<PlayerHealth>();
            if (ph != null) { data.health = ph.CurrentHealth; data.armor = ph.CurrentArmor; }

            // Vitals
            var pv = player.GetComponent<PlayerVitals>();
            if (pv != null) { data.stamina = pv.Stamina; data.hunger = pv.Hunger; data.thirst = pv.Thirst; }

            // Stats
            var ps = player.GetComponent<PlayerStats>();
            if (ps != null)
            {
                data.strengthStat = new SaveData.StatSave { level = ps.Strength.level, xp = ps.Strength.xp };
                data.speedStat    = new SaveData.StatSave { level = ps.Speed.level,    xp = ps.Speed.xp    };
                data.craftingStat = new SaveData.StatSave { level = ps.Crafting.level, xp = ps.Crafting.xp };
                data.combatStat   = new SaveData.StatSave { level = ps.Combat.level,   xp = ps.Combat.xp   };
            }

            // Inventory
            var inv = player.GetComponent<Inventory.Inventory>();
            if (inv != null)
            {
                data.inventorySlots.Clear();
                for (int i = 0; i < inv.TotalSlots; i++)
                {
                    var slot = inv.GetSlot(i);
                    data.inventorySlots.Add(new SaveData.SlotSave
                    {
                        itemAssetName = slot.IsEmpty ? "" : slot.item.name,
                        count         = slot.IsEmpty ? 0  : slot.count
                    });
                }
            }

            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"[SaveManager] Saved → {SavePath}");            Managers.ToastUI.Show("\u2713  GAME SAVED", Managers.ToastUI.Save);        }

        // ── Load ──────────────────────────────────────────────────────────────
        public void Load()
        {
            if (!File.Exists(SavePath))
            {
                Debug.Log("[SaveManager] No save file found.");
                Managers.ToastUI.Show("No save file found", Managers.ToastUI.Warning);
                return;
            }

            string   json = File.ReadAllText(SavePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            var player = GameObject.FindWithTag("Player");
            if (player == null) { Debug.LogWarning("[SaveManager] No Player tag found."); return; }

            // Position
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(
                new Vector3(data.posX, data.posY, data.posZ),
                Quaternion.Euler(0f, data.rotY, 0f));
            if (cc != null) cc.enabled = true;

            // Health
            var ph = player.GetComponent<PlayerHealth>();
            ph?.LoadState(data.health, data.armor);

            // Vitals
            var pv = player.GetComponent<PlayerVitals>();
            pv?.LoadState(data.stamina, data.hunger, data.thirst);

            // Stats
            var ps = player.GetComponent<PlayerStats>();
            if (ps != null)
            {
                ps.LoadStat(ps.Strength, data.strengthStat);
                ps.LoadStat(ps.Speed,    data.speedStat);
                ps.LoadStat(ps.Crafting, data.craftingStat);
                ps.LoadStat(ps.Combat,   data.combatStat);
            }

            // Inventory
            var inv = player.GetComponent<Inventory.Inventory>();
            if (inv != null && data.inventorySlots != null)
                inv.LoadState(data.inventorySlots);

            Debug.Log($"[SaveManager] Loaded ← {data.savedAt}");            Managers.ToastUI.Show("\u21BA  GAME LOADED", Managers.ToastUI.Load);        }

        public bool HasSaveFile() => File.Exists(SavePath);
        public void DeleteSave()  { if (File.Exists(SavePath)) File.Delete(SavePath); }
    }
}

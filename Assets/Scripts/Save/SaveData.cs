using System;
using System.Collections.Generic;

namespace FreeWorld.Save
{
    /// <summary>
    /// Pure data class — no Unity dependencies.
    /// Serialised to JSON by SaveManager.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int    version    = 1;
        public string savedAt;          // ISO-8601 timestamp

        // ── Position ──────────────────────────────────────────────────────────
        public float posX, posY, posZ;
        public float rotY;

        // ── Health ────────────────────────────────────────────────────────────
        public float health;
        public float armor;

        // ── Vitals ────────────────────────────────────────────────────────────
        public float stamina;
        public float hunger;
        public float thirst;

        // ── Stats ─────────────────────────────────────────────────────────────
        public StatSave strengthStat = new StatSave();
        public StatSave speedStat    = new StatSave();
        public StatSave craftingStat = new StatSave();
        public StatSave combatStat   = new StatSave();

        [Serializable]
        public class StatSave
        {
            public int   level = 1;
            public float xp    = 0f;
        }

        // ── Inventory ─────────────────────────────────────────────────────────
        public List<SlotSave> inventorySlots = new List<SlotSave>();

        [Serializable]
        public class SlotSave
        {
            public string itemAssetName;   // ScriptableObject asset name (Resources path)
            public int    count;
        }
    }
}

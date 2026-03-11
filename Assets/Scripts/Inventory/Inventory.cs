using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeWorld.Inventory
{
    /// <summary>
    /// Player inventory: fixed-size grid of slots.
    /// Other systems call Add/Remove/Use; UI subscribes to OnChanged.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private int columns   = 6;
        [SerializeField] private int rows      = 4;

        // ── Events ────────────────────────────────────────────────────────────
        /// <summary>Fires whenever any slot changes. UI subscribes to this.</summary>
        public event Action OnChanged;

        // ── Slot ──────────────────────────────────────────────────────────────
        [Serializable]
        public class Slot
        {
            public ItemDefinition item;
            public int            count;
            public bool           IsEmpty => item == null || count <= 0;
        }

        // ── State ─────────────────────────────────────────────────────────────
        public int   Columns    => columns;
        public int   Rows       => rows;
        public int   TotalSlots => columns * rows;

        private Slot[] _slots;

        // ── Singleton-style accessor (player only has one inventory) ──────────
        public static Inventory Instance { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            Instance = this;
            _slots   = new Slot[TotalSlots];
            for (int i = 0; i < _slots.Length; i++)
                _slots[i] = new Slot();
        }

        // ── Public API ────────────────────────────────────────────────────────
        public Slot GetSlot(int index) => _slots[index];

        /// <summary>
        /// Add 'count' of item. Returns how many could NOT be added (overflow).
        /// </summary>
        public int Add(ItemDefinition item, int count = 1)
        {
            if (item == null || count <= 0) return count;

            int remaining = count;

            // Fill existing stacks first
            if (item.stackable)
            {
                for (int i = 0; i < _slots.Length && remaining > 0; i++)
                {
                    if (_slots[i].item == item && _slots[i].count < item.maxStack)
                    {
                        int space = item.maxStack - _slots[i].count;
                        int add   = Mathf.Min(space, remaining);
                        _slots[i].count += add;
                        remaining       -= add;
                    }
                }
            }

            // Open new slots
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    int add         = item.stackable ? Mathf.Min(item.maxStack, remaining) : 1;
                    _slots[i].item  = item;
                    _slots[i].count = add;
                    remaining      -= add;
                }
            }

            if (remaining < count)
                OnChanged?.Invoke();

            return remaining; // >0 means inventory was full
        }

        /// <summary>Remove 'count' of item. Returns true if successful.</summary>
        public bool Remove(ItemDefinition item, int count = 1)
        {
            if (!Has(item, count)) return false;

            int remaining = count;
            for (int i = _slots.Length - 1; i >= 0 && remaining > 0; i--)
            {
                if (_slots[i].item != item) continue;
                int take = Mathf.Min(_slots[i].count, remaining);
                _slots[i].count -= take;
                remaining       -= take;
                if (_slots[i].count <= 0) _slots[i].item = null;
            }

            OnChanged?.Invoke();
            return true;
        }

        /// <summary>Returns total count of an item across all slots.</summary>
        public int Count(ItemDefinition item)
        {
            int total = 0;
            foreach (var s in _slots)
                if (s.item == item) total += s.count;
            return total;
        }

        public bool Has(ItemDefinition item, int count = 1) => Count(item) >= count;

        /// <summary>
        /// Use item in slot. Applies heal/food/water/stamina effects and removes one.
        /// </summary>
        public bool UseSlot(int index)
        {
            var slot = _slots[index];
            if (slot.IsEmpty) return false;

            var item = slot.item;
            var ph   = GetComponent<Player.PlayerHealth>();
            var pv   = GetComponent<Player.PlayerVitals>();

            if (item.healAmount    > 0f) ph?.Heal(item.healAmount);
            if (item.foodAmount    > 0f) pv?.Feed(item.foodAmount);
            if (item.waterAmount   > 0f) pv?.Drink(item.waterAmount);
            if (item.staminaAmount > 0f) pv?.RestoreStamina(item.staminaAmount);

            slot.count--;
            if (slot.count <= 0) slot.item = null;

            OnChanged?.Invoke();
            return true;
        }

        /// <summary>Drop item from slot to ground in front of player.</summary>
        public void DropSlot(int index)
        {
            var slot = _slots[index];
            if (slot.IsEmpty) return;

            if (slot.item.dropPrefab != null)
            {
                Vector3 forward = transform.forward;
                Vector3 pos     = transform.position + forward * 1.2f + Vector3.up * 0.5f;
                Instantiate(slot.item.dropPrefab, pos, Quaternion.identity);
            }

            slot.count--;
            if (slot.count <= 0) slot.item = null;

            OnChanged?.Invoke();
        }

        /// <summary>Move item between two slots (handles stacking and swap).</summary>
        public void MoveSlot(int from, int to)
        {
            if (from == to) return;
            var a = _slots[from];
            var b = _slots[to];

            // Same item + stackable → merge
            if (!a.IsEmpty && !b.IsEmpty && a.item == b.item && a.item.stackable)
            {
                int space = a.item.maxStack - b.count;
                int move  = Mathf.Min(a.count, space);
                b.count  += move;
                a.count  -= move;
                if (a.count <= 0) a.item = null;
            }
            else
            {
                // Swap
                (a.item,  b.item)  = (b.item,  a.item);
                (a.count, b.count) = (b.count, a.count);
            }

            OnChanged?.Invoke();
        }

        // ── Serialization helpers (for save/load later) ───────────────────────
        public List<(string itemName, int count)> Serialize()
        {
            var list = new List<(string, int)>();
            foreach (var s in _slots)
                list.Add((s.item != null ? s.item.name : "", s.count));
            return list;
        }

        /// <summary>
        /// Restore inventory from save data.
        /// ItemDefinition assets must live in any Resources folder so they can
        /// be found by name via Resources.Load.
        /// </summary>
        public void LoadState(List<Save.SaveData.SlotSave> slotSaves)
        {
            // Clear all slots
            foreach (var s in _slots) { s.item = null; s.count = 0; }

            for (int i = 0; i < slotSaves.Count && i < _slots.Length; i++)
            {
                var saved = slotSaves[i];
                if (string.IsNullOrEmpty(saved.itemAssetName) || saved.count <= 0) continue;

                var def = Resources.Load<ItemDefinition>($"Items/{saved.itemAssetName}");
                if (def == null)
                    def = Resources.Load<ItemDefinition>(saved.itemAssetName);

                if (def != null)
                {
                    _slots[i].item  = def;
                    _slots[i].count = saved.count;
                }
                else
                {
                    Debug.LogWarning($"[Inventory] Could not find ItemDefinition '{saved.itemAssetName}' in Resources.");
                }
            }

            OnChanged?.Invoke();
        }
    }
}


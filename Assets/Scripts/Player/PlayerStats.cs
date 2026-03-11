using System;
using UnityEngine;

namespace FreeWorld.Player
{
    /// <summary>
    /// Four player skills that level up through gameplay actions.
    ///
    ///  Strength   — melee damage, carry weight cap
    ///  Speed      — walk / sprint speed bonus
    ///  Crafting   — unlocks recipes, reduces craft time
    ///  Combat     — weapon accuracy, reload speed, damage bonus
    ///
    /// Call the Grant*XP methods from relevant systems.
    /// Listen to OnLevelUp for UI notifications.
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        // ── XP curve ──────────────────────────────────────────────────────────
        // XP required to reach level N = BaseXP * N^Exponent
        [Header("XP Curve")]
        [SerializeField] private float baseXP    = 100f;
        [SerializeField] private float exponent  = 1.35f;
        [SerializeField] private int   maxLevel  = 50;

        // ── Stat data ─────────────────────────────────────────────────────────
        [Serializable]
        public class Stat
        {
            public string name;
            public int    level = 1;
            public float  xp;

            [NonSerialized] public float xpToNext; // filled at runtime

            public float NormalisedXP => xpToNext > 0f ? Mathf.Clamp01(xp / xpToNext) : 1f;
        }

        public Stat Strength { get; } = new Stat { name = "Strength" };
        public Stat Speed    { get; } = new Stat { name = "Speed"    };
        public Stat Crafting { get; } = new Stat { name = "Crafting" };
        public Stat Combat   { get; } = new Stat { name = "Combat"   };

        private Stat[] _all;

        // ── Events ────────────────────────────────────────────────────────────
        /// <summary>stat, newLevel</summary>
        public event Action<Stat, int> OnLevelUp;

        // ── Singleton ────────────────────────────────────────────────────────
        public static PlayerStats Instance { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            Instance = this;
            _all = new[] { Strength, Speed, Crafting, Combat };
            foreach (var s in _all)
                s.xpToNext = XPForLevel(s.level);
        }

        // ── Public XP grant API ───────────────────────────────────────────────

        /// <summary>Grant XP for landing a melee hit or carrying heavy objects.</summary>
        public void GrantStrengthXP(float amount) => GiveXP(Strength, amount);

        /// <summary>Grant XP whenever the player runs a certain distance.</summary>
        public void GrantSpeedXP(float amount)    => GiveXP(Speed,    amount);

        /// <summary>Grant XP on successful crafting.</summary>
        public void GrantCraftingXP(float amount) => GiveXP(Crafting, amount);

        /// <summary>Grant XP on dealing damage or killing enemies.</summary>
        public void GrantCombatXP(float amount)   => GiveXP(Combat,   amount);

        // ── Stat bonuses (queried by other systems) ───────────────────────────

        /// <summary>Multiplier on walk + sprint speed. 1% per level above 1.</summary>
        public float SpeedMultiplier => 1f + (Speed.level - 1) * 0.01f;

        /// <summary>Flat damage bonus. 0.5 per Combat level above 1.</summary>
        public float CombatDamageBonus => (Combat.level - 1) * 0.5f;

        /// <summary>Melee damage multiplier. 2% per Strength level above 1.</summary>
        public float MeleeDamageMultiplier => 1f + (Strength.level - 1) * 0.02f;

        /// <summary>Carry weight capacity in kg. Base 20 kg + 2 kg per Strength level.</summary>
        public float CarryCapacityKg => 20f + (Strength.level - 1) * 2f;

        // ── Internal ──────────────────────────────────────────────────────────
        private void GiveXP(Stat stat, float amount)
        {
            if (stat.level >= maxLevel) return;

            stat.xp += amount;
            while (stat.xp >= stat.xpToNext && stat.level < maxLevel)
            {
                stat.xp      -= stat.xpToNext;
                stat.level++;
                stat.xpToNext = XPForLevel(stat.level);
                OnLevelUp?.Invoke(stat, stat.level);
                Debug.Log($"[PlayerStats] {stat.name} → Level {stat.level}");                Managers.ToastUI.Show($"{stat.name.ToUpper()} \u2192 Level {stat.level}",
                                      Managers.ToastUI.Level);            }
        }

        private float XPForLevel(int level)
            => Mathf.Round(baseXP * Mathf.Pow(level, exponent));

        /// <summary>Restore a stat to saved level + XP (used by SaveManager).</summary>
        public void LoadStat(Stat stat, Save.SaveData.StatSave saved)
        {
            stat.level    = Mathf.Clamp(saved.level, 1, maxLevel);
            stat.xpToNext = XPForLevel(stat.level);
            stat.xp       = Mathf.Clamp(saved.xp, 0f, stat.xpToNext - 1f);
        }

        // ── Speed XP passively from PlayerController ──────────────────────────
        private float _distanceAccumulator;

        /// <summary>Call every frame with movement delta to accumulate speed XP.</summary>
        public void TrackMovement(float deltaDistance)
        {
            _distanceAccumulator += deltaDistance;
            if (_distanceAccumulator >= 10f) // 1 XP per 10 m travelled
            {
                GrantSpeedXP(1f);
                _distanceAccumulator -= 10f;
            }
        }
    }
}

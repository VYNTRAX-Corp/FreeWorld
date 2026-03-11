using System;
using FreeWorld.Utilities;
using UnityEngine;

namespace FreeWorld.Player
{
    /// <summary>
    /// Tracks player health, handles damage, death and respawn.
    /// Implements IDamageable so enemies/bullets interact with a shared interface.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth    = 100f;
        [SerializeField] private float maxArmor     = 100f;
        [SerializeField] private float armorAbsorption = 0.5f;  // 50% damage blocked by armor

        [Header("Regeneration")]
        [SerializeField] private bool  regenEnabled      = false;
        [SerializeField] private float regenDelay        = 5f;
        [SerializeField] private float regenRate         = 5f;   // HP per second

        // ── Events ────────────────────────────────────────────────────────────
        public event Action<float, float> OnHealthChanged;  // (current, max)
        public event Action<float, float> OnArmorChanged;
        public event Action               OnDeath;
        public event Action               OnRespawn;
        public event Action               OnDamaged;        // fires any time damage is taken
        public event Action<Vector3>      OnDamagedFrom;   // fires with world-space hit direction

        // ── Properties ────────────────────────────────────────────────────────
        public float CurrentHealth { get; private set; }
        public float CurrentArmor  { get; private set; }
        public bool  IsAlive       { get; private set; } = true;

        private float _regenTimer;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            CurrentHealth = maxHealth;
            CurrentArmor  = 0f;   // armor picked up via collectibles
        }

        private void Update()
        {
            if (!IsAlive || !regenEnabled) return;

            _regenTimer += Time.deltaTime;
            if (_regenTimer >= regenDelay)
                Heal(regenRate * Time.deltaTime);
        }

        // ── IDamageable ───────────────────────────────────────────────────────
        public void TakeDamage(float amount, Vector3 hitPoint = default, Vector3 hitDirection = default)
        {
            if (!IsAlive) return;

            // Armor absorbs part of the damage
            float armorDamage = 0f;
            if (CurrentArmor > 0f)
            {
                armorDamage   = Mathf.Min(amount * armorAbsorption, CurrentArmor);
                CurrentArmor -= armorDamage;
                amount       -= armorDamage;
                OnArmorChanged?.Invoke(CurrentArmor, maxArmor);
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            _regenTimer   = 0f;   // reset regen on damage

            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
            OnDamaged?.Invoke();
            OnDamagedFrom?.Invoke(hitDirection);

            // Procedural hurt sound
            ProceduralAudioLibrary.PlayAt(
                ProceduralAudioLibrary.ClipPlayerHurt, transform.position, 0.85f);

            if (CurrentHealth <= 0f)
                Die();
        }

        // ── Public Helpers ────────────────────────────────────────────────────
        public void Heal(float amount)
        {
            if (!IsAlive) return;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        public void AddArmor(float amount)
        {
            CurrentArmor = Mathf.Min(maxArmor, CurrentArmor + amount);
            OnArmorChanged?.Invoke(CurrentArmor, maxArmor);
        }

        /// <summary>Restore exact health + armor values (used by SaveManager).</summary>
        public void LoadState(float health, float armor)
        {
            IsAlive       = true;
            CurrentHealth = Mathf.Clamp(health, 0f, maxHealth);
            CurrentArmor  = Mathf.Clamp(armor,  0f, maxArmor);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
            OnArmorChanged?.Invoke(CurrentArmor,   maxArmor);
        }

        public void Respawn(Vector3 spawnPoint)
        {
            IsAlive       = true;
            CurrentHealth = maxHealth;
            CurrentArmor  = 0f;
            transform.position = spawnPoint;

            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
            OnRespawn?.Invoke();

            // Re-enable player components
            GetComponent<CharacterController>().enabled = true;
            GetComponent<PlayerController>().enabled    = true;
        }

        // ── Private ───────────────────────────────────────────────────────────
        private void Die()
        {
            IsAlive = false;

            // Disable control while dead
            GetComponent<PlayerController>().enabled    = false;
            GetComponent<CharacterController>().enabled = false;

            ProceduralAudioLibrary.PlayAt(
                ProceduralAudioLibrary.ClipEnemyDeath, transform.position, 0.9f);

            OnDeath?.Invoke();
        }
    }
}

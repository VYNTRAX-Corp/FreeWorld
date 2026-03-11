using System;
using UnityEngine;

namespace FreeWorld.Player
{
    /// <summary>
    /// Survival vitals: Stamina, Hunger, Thirst.
    ///
    /// Stamina — drains while sprinting, regens after a short delay when still.
    ///           PlayerController reads CanSprint to gate the sprint key.
    ///
    /// Hunger  — drains passively over time, faster while sprinting.
    ///           At zero: player takes 1 HP/s starvation damage.
    ///
    /// Thirst  — drains slightly faster than hunger.
    ///           At zero: player takes 2 HP/s dehydration damage.
    ///
    /// Feed()/Drink() called by the Pickup / Inventory system when
    /// the player eats food or drinks water.
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    [RequireComponent(typeof(PlayerController))]
    public class PlayerVitals : MonoBehaviour
    {
        // ── Stamina ───────────────────────────────────────────────────────────
        [Header("Stamina")]
        [SerializeField] private float maxStamina                = 100f;
        [SerializeField] private float staminaWalkDrainPerSecond = 2.5f;  // drains while walking
        [SerializeField] private float staminaRunDrainPerSecond  = 10f;   // drains while sprinting (Shift)
        [SerializeField] private float staminaRegenPerSecond     = 12f;
        [SerializeField] private float staminaExhaustThreshold   = 10f;   // must recover to this % before sprint re-enables

        // ── Hunger ────────────────────────────────────────────────────────────
        [Header("Hunger")]
        [SerializeField] private float maxHunger                  = 100f;
        [SerializeField] private float hungerDrainPerSecond       = 0.00833f;  // ~100 in 12 minutes
        [SerializeField] private float hungerSprintMultiplier     = 2.5f;
        [SerializeField] private float hungerStarveDamagePerSec   = 1f;

        // ── Thirst ────────────────────────────────────────────────────────────
        [Header("Thirst")]
        [SerializeField] private float maxThirst                  = 100f;
        [SerializeField] private float thirstDrainPerSecond       = 0.01389f;  // ~100 in 7 minutes
        [SerializeField] private float thirstSprintMultiplier     = 3f;
        [SerializeField] private float thirstStarveDamagePerSec   = 2f;

        // ── Events ────────────────────────────────────────────────────────────
        public event Action<float, float> OnStaminaChanged;  // (current, max)
        public event Action<float, float> OnHungerChanged;
        public event Action<float, float> OnThirstChanged;

        // ── Properties ────────────────────────────────────────────────────────
        public float Stamina { get; private set; }
        public float Hunger  { get; private set; }
        public float Thirst  { get; private set; }

        /// <summary>False when exhausted (stamina == 0) until it recovers to the exhaustion threshold.</summary>
        public bool CanSprint => !_exhausted;

        // ── Internal ──────────────────────────────────────────────────────────
        private PlayerController _controller;
        private PlayerHealth     _health;
        private bool             _exhausted;   // true from 0 until threshold is reached

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _health     = GetComponent<PlayerHealth>();

            Stamina = maxStamina;
            Hunger  = maxHunger;
            Thirst  = maxThirst;
        }

        private void Update()
        {
            if (_health != null && !_health.IsAlive) return;

            bool moving    = _controller != null && _controller.CurrentSpeed > 0.1f;
            bool sprinting = _controller != null && _controller.IsSprinting;

            TickStamina(moving, sprinting);
            TickHunger(sprinting);
            TickThirst(sprinting);
        }

        // ── Stamina tick ──────────────────────────────────────────────────────
        private void TickStamina(bool moving, bool sprinting)
        {
            if (moving && Stamina > 0f)
            {
                float drain = sprinting ? staminaRunDrainPerSecond : staminaWalkDrainPerSecond;
                Stamina = Mathf.Max(0f, Stamina - drain * Time.deltaTime);

                // Enter exhaustion the moment stamina bottoms out
                if (Stamina <= 0f)
                    _exhausted = true;

                OnStaminaChanged?.Invoke(Stamina, maxStamina);
            }
            else if (!moving && Stamina < maxStamina)
            {
                Stamina = Mathf.Min(maxStamina, Stamina + staminaRegenPerSecond * Time.deltaTime);

                // Clear exhaustion once stamina reaches the threshold
                if (_exhausted && Stamina >= staminaExhaustThreshold)
                    _exhausted = false;

                OnStaminaChanged?.Invoke(Stamina, maxStamina);
            }
        }

        // ── Hunger tick ───────────────────────────────────────────────────────
        private void TickHunger(bool sprinting)
        {
            float drain = hungerDrainPerSecond * (sprinting ? hungerSprintMultiplier : 1f);
            float prev  = Hunger;
            Hunger = Mathf.Max(0f, Hunger - drain * Time.deltaTime);
            if (!Mathf.Approximately(Hunger, prev))
                OnHungerChanged?.Invoke(Hunger, maxHunger);

            if (Hunger <= 0f && _health != null)
                _health.TakeDamage(hungerStarveDamagePerSec * Time.deltaTime);
        }

        // ── Thirst tick ───────────────────────────────────────────────────────
        private void TickThirst(bool sprinting)
        {
            float drain = thirstDrainPerSecond * (sprinting ? thirstSprintMultiplier : 1f);
            float prev  = Thirst;
            Thirst = Mathf.Max(0f, Thirst - drain * Time.deltaTime);
            if (!Mathf.Approximately(Thirst, prev))
                OnThirstChanged?.Invoke(Thirst, maxThirst);

            if (Thirst <= 0f && _health != null)
                _health.TakeDamage(thirstStarveDamagePerSec * Time.deltaTime);
        }

        // ── Public API ────────────────────────────────────────────────────────
        /// <summary>Eat food. Restores hunger by amount.</summary>
        public void Feed(float amount)
        {
            Hunger = Mathf.Min(maxHunger, Hunger + amount);
            OnHungerChanged?.Invoke(Hunger, maxHunger);
        }

        /// <summary>Drink water. Restores thirst by amount.</summary>
        public void Drink(float amount)
        {
            Thirst = Mathf.Min(maxThirst, Thirst + amount);
            OnThirstChanged?.Invoke(Thirst, maxThirst);
        }

        /// <summary>Restore stamina directly (e.g. from a stim item).</summary>
        public void RestoreStamina(float amount)
        {
            Stamina = Mathf.Min(maxStamina, Stamina + amount);
            OnStaminaChanged?.Invoke(Stamina, maxStamina);
        }

        /// <summary>Full restore on respawn.</summary>
        public void ResetOnRespawn()
        {
            Stamina = maxStamina;
            Hunger  = maxHunger;
            Thirst  = maxThirst;
            OnStaminaChanged?.Invoke(Stamina, maxStamina);
            OnHungerChanged?.Invoke(Hunger, maxHunger);
            OnThirstChanged?.Invoke(Thirst, maxThirst);
        }

        /// <summary>Restore exact values (used by SaveManager).</summary>
        public void LoadState(float stamina, float hunger, float thirst)
        {
            Stamina = Mathf.Clamp(stamina, 0f, maxStamina);
            Hunger  = Mathf.Clamp(hunger,  0f, maxHunger);
            Thirst  = Mathf.Clamp(thirst,  0f, maxThirst);
            OnStaminaChanged?.Invoke(Stamina, maxStamina);
            OnHungerChanged?.Invoke(Hunger,   maxHunger);
            OnThirstChanged?.Invoke(Thirst,   maxThirst);
        }
    }
}

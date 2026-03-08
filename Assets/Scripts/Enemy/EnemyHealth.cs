using System;
using FreeWorld.Utilities;
using UnityEngine;

namespace FreeWorld.Enemy
{
    /// <summary>
    /// Enemy health component. Implements IDamageable.
    /// Notifies EnemyAI on death and triggers a score event for GameManager.
    /// </summary>
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField] private float maxHealth    = 100f;

        [Header("Death")]
        [SerializeField] private float despawnDelay = 5f;
        [SerializeField] private int   scoreValue   = 100;
        [SerializeField] private string enemyTypeName = "GRUNT";

        [Header("Loot Drop")]
        [SerializeField] private GameObject[] lootPrefabs;
        [SerializeField] [Range(0f, 1f)] private float lootDropChance = 0.5f;

        [Header("Hit Feedback")]
        [SerializeField] private GameObject bloodSplatterPrefab;
        public event Action OnDeathEvent;

        // ── IDamageable ───────────────────────────────────────────────────────
        public float CurrentHealth { get; private set; }
        public bool  IsAlive       { get; private set; } = true;

        private EnemyAI _ai;

        // ── Variant configuration (called by EnemyAI.ApplyVariant) ─────────────────────────
        public void Configure(float hp, int score, string typeName)
        {
            maxHealth     = hp;
            CurrentHealth = hp;
            scoreValue    = score;
            enemyTypeName = typeName;
        }

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            CurrentHealth = maxHealth;
            _ai = GetComponent<EnemyAI>();
        }

        public void TakeDamage(float amount, Vector3 hitPoint = default, Vector3 hitDirection = default)
        {
            if (!IsAlive) return;

            CurrentHealth -= amount;

            // Floating damage number at the hit point
            DamageNumber.Show(amount, hitPoint != default ? hitPoint : transform.position + Vector3.up);

            // Blood hit effect
            if (bloodSplatterPrefab != null)
            {
                GameObject fx = Instantiate(bloodSplatterPrefab, hitPoint,
                                            Quaternion.LookRotation(-hitDirection));
                Destroy(fx, 2f);
            }

            if (CurrentHealth <= 0f)
                Die();
        }

        // ── Death ─────────────────────────────────────────────────────────────
        private void Die()
        {
            IsAlive = false;
            _ai?.OnDeath();

            // Award score via GameManager
            Managers.GameManager.Instance?.EnemyKilled(enemyTypeName, scoreValue);

            OnDeathEvent?.Invoke();

            // Drop random loot
            if (lootPrefabs != null && lootPrefabs.Length > 0
                && UnityEngine.Random.value <= lootDropChance)
            {
                GameObject loot = lootPrefabs[UnityEngine.Random.Range(0, lootPrefabs.Length)];
                if (loot != null)
                    Instantiate(loot, transform.position + Vector3.up * 0.5f, Quaternion.identity);
            }

            // Despawn after delay (replace with object pool or ragdoll later)
            Destroy(gameObject, despawnDelay);
        }
    }
}

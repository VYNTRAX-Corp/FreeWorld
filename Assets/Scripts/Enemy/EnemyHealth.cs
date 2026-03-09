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
        public event Action OnDamaged;    // fires on every successful hit (used by EnemyAI for cover reaction)

        // ── IDamageable ───────────────────────────────────────────────────────
        public float CurrentHealth { get; private set; }
        public float MaxHealth     => maxHealth;
        public string EnemyTypeName => enemyTypeName;
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

            OnDamaged?.Invoke();
            CurrentHealth -= amount;

            // Floating damage number at the hit point
            Vector3 dmgPos = hitPoint != default ? hitPoint : transform.position + Vector3.up;
            DamageNumber.Show(amount, dmgPos);

            // VFX blood hit (procedural — no prefab needed)
            if (hitPoint != default)
                VFXManager.BloodHit(hitPoint, hitDirection != default ? -hitDirection : Vector3.up);

            // Procedural hurt sound
            var audio = GetComponent<AudioSource>();
            if (audio != null)
                ProceduralAudioLibrary.Play(audio, ProceduralAudioLibrary.ClipEnemyHurt, 0.7f);

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

            // Procedural loot drop — no prefab required
            SpawnProceduralLoot();

            // Play death sound
            ProceduralAudioLibrary.PlayAt(
                ProceduralAudioLibrary.ClipEnemyDeath, transform.position, 0.8f);

            // Despawn after delay (replace with object pool or ragdoll later)
            Destroy(gameObject, despawnDelay);
        }

        // ── Procedural loot ───────────────────────────────────────────────────
        private void SpawnProceduralLoot()
        {
            if (UnityEngine.Random.value > lootDropChance) return;

            // 60 % health pickup, 40 % ammo pickup
            bool isHealth = UnityEngine.Random.value < 0.6f;

            Vector3 spawnPos = transform.position + Vector3.up * 0.3f
                               + new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), 0f,
                                             UnityEngine.Random.Range(-0.6f, 0.6f));

            // Primitive shape: sphere for health, cube for ammo
            PrimitiveType prim = isHealth ? PrimitiveType.Sphere : PrimitiveType.Cube;
            GameObject go = GameObject.CreatePrimitive(prim);
            go.name = isHealth ? "Pickup_Health" : "Pickup_Ammo";
            go.transform.position   = spawnPos;
            go.transform.localScale = Vector3.one * 0.35f;
            go.tag = "Pickup";

            // Emissive material
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor",    isHealth ? new Color(0.1f, 0.9f, 0.2f) : new Color(0.95f, 0.8f, 0.1f));
            mat.SetColor("_EmissionColor", isHealth ? new Color(0f, 0.4f, 0.05f)  : new Color(0.4f, 0.3f, 0f));
            mat.EnableKeyword("_EMISSION");
            go.GetComponent<Renderer>().material = mat;

            // Replace auto-added collider with trigger
            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            // Attach Pickup behaviour
            var pickup = go.AddComponent<Pickup>();
            if (isHealth)
                pickup.Configure(Pickup.PickupType.Health, 25f);
            else
                pickup.Configure(Pickup.PickupType.Ammo,   30f);

            // Play pickup chime so the player hears the drop
            ProceduralAudioLibrary.PlayAt(
                ProceduralAudioLibrary.ClipPickup, spawnPos, 0.5f);

            // Auto-destroy if not collected within 20 s
            UnityEngine.Object.Destroy(go, 20f);
        }
    }
}

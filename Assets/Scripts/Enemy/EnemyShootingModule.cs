using UnityEngine;
using System.Collections;
using FreeWorld.Utilities;

namespace FreeWorld.Enemy
{
    /// <summary>
    /// Full shooting mechanics module for enemy AI.
    /// Handles fire-rate timing, burst fire, magazine management, reload,
    /// per-shot accuracy spread, headshot multiplier, muzzle flash, and impact sparks.
    ///
    /// Wire-up:
    ///   1. AddComponent to enemy in FreeWorldSetup or prefab.
    ///   2. Call Init(muzzleTransform) from EnemyAI.Awake() after building the gun.
    ///   3. Call Configure(...) per variant in EnemyAI.ApplyVariant().
    ///   4. Call TryShoot(playerTransform) every frame while in Attack state.
    /// </summary>
    public class EnemyShootingModule : MonoBehaviour
    {
        // ── Tunable parameters ────────────────────────────────────────────────
        [Header("Accuracy")]
        [Tooltip("Cone half-angle in degrees. 0 = perfect aim.")]
        public float SpreadDegrees = 4f;

        [Header("Fire")]
        [Tooltip("Shots per second (trigger pulls per second for burst/auto).")]
        public float FireRate   = 1.2f;
        [Tooltip("Rounds per trigger pull. 1 = semi-auto, 3 = 3-round burst, 30 = full auto.")]
        public int   BurstCount = 1;
        [Tooltip("Seconds between rounds inside a burst.")]
        public float BurstDelay = 0.10f;

        [Header("Damage")]
        public float DamagePerShot = 10f;
        [Tooltip("Multiplier applied to damage when a 'Head'-tagged collider is hit.")]
        public float HeadshotMult  = 2.0f;

        [Header("Magazine")]
        public int   MagSize    = 20;
        public float ReloadTime = 2.0f;

        [Header("Range")]
        public float MaxRange = 15f;

        [Header("Audio")]
        [Tooltip("Optional — wire an AudioClip here or leave blank. EnemyAI forwards its shootSound automatically.")]
        public AudioClip ShootAudioClip;

        // ── Public state (readable by animator/HUD) ───────────────────────────
        public bool IsFiring    { get; private set; }
        public bool IsReloading { get; private set; }
        public int  AmmoInMag   { get; private set; }

        // ── Internal ─────────────────────────────────────────────────────────
        private Transform   _muzzle;
        private AudioSource _audio;
        private float       _fireTimer;
        private bool        _burstRunning;

        // ── Initialise ────────────────────────────────────────────────────────
        /// <summary>Called once from EnemyAI.Awake() after the gun is built.</summary>
        public void Init(Transform muzzlePoint)
        {
            _muzzle   = muzzlePoint;
            _audio    = GetComponent<AudioSource>();
            AmmoInMag = MagSize;
        }

        /// <summary>Bulk-configure all parameters at once (called by EnemyAI.ApplyVariant).</summary>
        public void Configure(float damage, float fireRate, float spreadDeg,
                              int burstCount, int magSize, float reloadTime, float maxRange)
        {
            DamagePerShot = damage;
            FireRate      = fireRate;
            SpreadDegrees = spreadDeg;
            BurstCount    = burstCount;
            MagSize       = magSize;
            ReloadTime    = reloadTime;
            MaxRange      = maxRange;
            AmmoInMag     = magSize;
        }

        // ── Per-frame entry point ─────────────────────────────────────────────
        /// <summary>
        /// Call every frame while the enemy is in Attack state.
        /// Internally tracks the fire-rate timer; no need to manage timing outside.
        /// </summary>
        public void TryShoot(Transform target)
        {
            if (IsReloading || target == null || _burstRunning) return;

            _fireTimer += Time.deltaTime;
            if (_fireTimer < 1f / FireRate) return;
            _fireTimer = 0f;

            if (AmmoInMag <= 0)
            {
                StartCoroutine(DoReload());
                return;
            }

            if (BurstCount <= 1)
            {
                FireShot(target, firstShot: true);
                if (AmmoInMag <= 0) StartCoroutine(DoReload());
            }
            else
            {
                StartCoroutine(FireBurst(target));
            }
        }

        // ── Shooting ──────────────────────────────────────────────────────────
        private IEnumerator FireBurst(Transform target)
        {
            _burstRunning = true;
            IsFiring      = true;

            for (int i = 0; i < BurstCount && AmmoInMag > 0; i++)
            {
                FireShot(target, firstShot: i == 0);
                if (i < BurstCount - 1)
                    yield return new WaitForSeconds(BurstDelay);
            }

            IsFiring      = false;
            _burstRunning = false;
            if (AmmoInMag <= 0) StartCoroutine(DoReload());
        }

        private void FireShot(Transform target, bool firstShot)
        {
            if (AmmoInMag <= 0 || IsReloading) return;
            AmmoInMag--;
            IsFiring = true;

            Vector3 origin  = _muzzle != null
                ? _muzzle.position
                : transform.position + Vector3.up * 1.5f;
            Vector3 aimPt   = target.position + Vector3.up * 1.0f;
            Vector3 baseDir = (aimPt - origin).normalized;

            // First shot of a trigger pull gets a tighter cone (calm first shot)
            float spread = SpreadDegrees * (firstShot ? 0.45f : 1f);
            Vector3 dir  = Quaternion.Euler(
                Random.Range(-spread, spread),
                Random.Range(-spread, spread),
                0f) * baseDir;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, MaxRange))
            {
                bool  headshot   = hit.collider.CompareTag("Head");
                float dmg        = DamagePerShot * (headshot ? HeadshotMult : 1f);
                var   damageable = hit.collider.GetComponentInParent<IDamageable>();
                damageable?.TakeDamage(dmg, hit.point, dir);

                bool isFlesh = hit.collider.CompareTag("Player") || headshot;
                if (isFlesh)
                    VFXManager.BloodHit(hit.point, hit.normal);
                else
                {
                    VFXManager.BulletSpark(hit.point, hit.normal);
                    VFXManager.BulletHole(hit.point, hit.normal, hit.transform);
                }
                VFXManager.BulletTracer(origin, hit.point);
            }

            VFXManager.MuzzleFlashLight(origin);
            PlayGunshot();

            if (BurstCount <= 1) IsFiring = false;
        }

        private IEnumerator DoReload()
        {
            IsReloading = true;
            IsFiring    = false;
            yield return new WaitForSeconds(ReloadTime);
            AmmoInMag   = MagSize;
            IsReloading = false;
        }

        private void PlayGunshot()
        {
            if (_audio != null && ShootAudioClip != null)
                _audio.PlayOneShot(ShootAudioClip);
        }
    }
}

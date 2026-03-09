using UnityEngine;

namespace FreeWorld.Audio
{
    /// <summary>
    /// ScriptableObject that holds all weapon and enemy AudioClip arrays.
    ///
    /// Multiple clips per event are supported — a random one is chosen on
    /// each play call to avoid the "machine-gun" effect of hearing the exact
    /// same sound repeatedly.
    ///
    /// Create via: Assets → Create → FreeWorld → Weapon Audio Bank
    ///
    /// Naming convention for the AudioImportSetup editor auto-assign:
    ///   Assets/Audio/Weapons/rifle_shoot_*.wav   → RifleShoot[]
    ///   Assets/Audio/Weapons/rifle_reload*.wav   → RifleReload[]
    ///   Assets/Audio/Weapons/pistol_shoot_*.wav  → PistolShoot[]
    ///   Assets/Audio/Weapons/pistol_reload*.wav  → PistolReload[]
    ///   Assets/Audio/Weapons/shotgun_shoot_*.wav → ShotgunShoot[]
    ///   Assets/Audio/Weapons/shotgun_reload*.wav → ShotgunReload[]
    ///   Assets/Audio/Weapons/empty*.wav          → DryFire[]
    ///   Assets/Audio/Weapons/draw*.wav           → WeaponDraw[]
    ///   Assets/Audio/Weapons/enemy_shoot_*.wav   → EnemyShoot[]
    ///   Assets/Audio/Weapons/enemy_alert*.wav    → EnemyAlert[]
    ///   Assets/Audio/Weapons/enemy_death*.wav    → EnemyDeath[]
    ///   Assets/Audio/Player/footstep_*.wav       → Footstep[]
    ///   Assets/Audio/Player/player_hurt_*.wav    → PlayerHurt[]
    ///   Assets/Audio/Player/bullet_impact_*.wav  → BulletImpact[]
    ///   Assets/Audio/Player/flesh_hit_*.wav      → FleshHit[]
    /// </summary>
    [CreateAssetMenu(menuName = "FreeWorld/Weapon Audio Bank", fileName = "WeaponAudioBank")]
    public class WeaponAudioBank : ScriptableObject
    {
        // ── Player weapons ────────────────────────────────────────────────────
        [Header("Rifle")]
        public AudioClip[] RifleShoot;
        public AudioClip[] RifleReload;

        [Header("Pistol")]
        public AudioClip[] PistolShoot;
        public AudioClip[] PistolReload;

        [Header("Shotgun")]
        public AudioClip[] ShotgunShoot;
        public AudioClip[] ShotgunReload;

        [Header("Shared Weapon Events")]
        public AudioClip[] DryFire;       // empty-magazine click
        public AudioClip[] WeaponDraw;    // holster → ready

        // ── Enemy ─────────────────────────────────────────────────────────────
        [Header("Enemy")]
        public AudioClip[] EnemyShoot;
        public AudioClip[] EnemyAlert;
        public AudioClip[] EnemyDeath;
        public AudioClip[] EnemyHurt;

        // ── Player ────────────────────────────────────────────────────────────
        [Header("Player")]
        public AudioClip[] PlayerHurt;
        public AudioClip[] Footstep;
        public AudioClip[] BulletImpact; // hitting environment
        public AudioClip[] FleshHit;     // hitting enemy

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Pick a random non-null clip from an array. Returns null if empty/null.</summary>
        public static AudioClip Pick(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            // Compact — ignore null entries
            int start = Random.Range(0, clips.Length);
            for (int i = 0; i < clips.Length; i++)
            {
                var c = clips[(start + i) % clips.Length];
                if (c != null) return c;
            }
            return null;
        }

        /// <summary>Returns the clip arrays for a given WeaponType shoot event.</summary>
        public AudioClip[] ShootClipsFor(Weapons.WeaponType wt)
        {
            switch (wt)
            {
                case Weapons.WeaponType.Pistol:  return PistolShoot;
                case Weapons.WeaponType.Shotgun: return ShotgunShoot;
                default:                          return RifleShoot;  // Rifle + Sniper
            }
        }

        /// <summary>Returns the clip arrays for a given WeaponType reload event.</summary>
        public AudioClip[] ReloadClipsFor(Weapons.WeaponType wt)
        {
            switch (wt)
            {
                case Weapons.WeaponType.Pistol:  return PistolReload;
                case Weapons.WeaponType.Shotgun: return ShotgunReload;
                default:                          return RifleReload;
            }
        }
    }
}

using System;
using FreeWorld.Managers;
using UnityEngine;

namespace FreeWorld.Weapons
{
    public enum WeaponType { Rifle, Pistol, Shotgun, Sniper }
    public enum FireMode  { Auto, SemiAuto, Burst }

    /// <summary>
    /// Base class for all weapons. Handles shooting, reloading, recoil, ADS.
    /// Subclass this to add weapon-specific behaviour (e.g. shotgun spread).
    /// </summary>
    public class WeaponBase : MonoBehaviour
    {
        // ── Identity ──────────────────────────────────────────────────────────
        [Header("Identity")]
        public string    weaponName = "Rifle";
        public WeaponType weaponType;
        public FireMode   fireMode;

        // ── Damage ────────────────────────────────────────────────────────────
        [Header("Damage")]
        [SerializeField] protected float damage          = 25f;
        [SerializeField] protected float headshotMult    = 2f;
        [SerializeField] protected float range           = 200f;

        // ── Fire rate ─────────────────────────────────────────────────────────
        [Header("Fire Rate")]
        [SerializeField] protected float fireRate        = 600f;  // rounds per minute
        [SerializeField] protected int   burstCount      = 3;

        // ── Ammo ──────────────────────────────────────────────────────────────
        [Header("Ammo")]
        [SerializeField] protected int   magazineSize    = 30;
        [SerializeField] protected int   maxReserveAmmo  = 180;
        [SerializeField] protected float reloadTime      = 2.2f;

        // ── Recoil ────────────────────────────────────────────────────────────
        [Header("Recoil")]
        [SerializeField] protected float recoilVertical  = 0.5f;
        [SerializeField] protected float recoilHorizontal= 0.15f;

        // ── ADS (Aim Down Sights) ─────────────────────────────────────────────
        [Header("ADS")]
        [SerializeField] protected Vector3 adsPosition   = new Vector3(0f, -0.1f, 0.2f);
        [SerializeField] protected float   adsSpeed      = 10f;
        [SerializeField] protected float   adsFovMultiplier = 0.7f;  // 0–1 of base FOV

        // ── Audio ─────────────────────────────────────────────────────────────
        [Header("Audio")]
        [SerializeField] protected AudioClip shootSound;
        [SerializeField] protected AudioClip reloadSound;
        [SerializeField] protected AudioClip emptySound;
        [SerializeField] protected AudioClip drawSound;

        // ── VFX ───────────────────────────────────────────────────────────────
        [Header("VFX")]
        [SerializeField] protected ParticleSystem muzzleFlash;
        [SerializeField] protected GameObject     bulletImpactPrefab;

        // ── References ────────────────────────────────────────────────────────
        [Header("References")]
        [SerializeField] protected Transform    muzzlePoint;
        [SerializeField] protected Camera       fpsCam;

        // ── Events ────────────────────────────────────────────────────────────
        public event Action<int, int> OnAmmoChanged;    // (current mag, reserve)
        public event Action           OnReloadStart;
        public event Action           OnReloadEnd;

        // ── State ─────────────────────────────────────────────────────────────
        public int  CurrentAmmo    { get; protected set; }
        public int  ReserveAmmo    { get; protected set; }
        public bool IsReloading    { get; protected set; }
        public bool IsADS          { get; protected set; }

        protected float  _nextFireTime;
        protected int    _burstFired;
        protected float  _fireInterval;
        protected Vector3 _hipPosition;

        private AudioSource _audio;

        // ─────────────────────────────────────────────────────────────────────
        protected virtual void Awake()
        {
            // Auto-add AudioSource if missing so weapon always has one
            _audio = GetComponent<AudioSource>();
            if (_audio == null)
                _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 0f;   // 2D sound for FPS feel
            _audio.playOnAwake  = false;

            _fireInterval = 60f / fireRate;
            CurrentAmmo   = magazineSize;
            ReserveAmmo   = maxReserveAmmo;
            _hipPosition  = transform.localPosition;

            if (fpsCam == null)
                fpsCam = Camera.main;
        }

        protected virtual void Update()
        {
            // Block all weapon input while not playing
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameState.Playing) return;

            HandleADS();
            HandleShootInput();
            HandleReloadInput();
        }

        // ── Input ─────────────────────────────────────────────────────────────
        private void HandleShootInput()
        {
            if (IsReloading) return;

            bool trigger = fireMode == FireMode.Auto
                         ? Input.GetButton("Fire1")
                         : Input.GetButtonDown("Fire1");

            if (trigger && Time.time >= _nextFireTime)
            {
                if (CurrentAmmo > 0)
                    TryShoot();
                else
                    PlaySound(emptySound);
            }
        }

        private void HandleReloadInput()
        {
            if (Input.GetKeyDown(KeyCode.R) && !IsReloading
                && CurrentAmmo < magazineSize && ReserveAmmo > 0)
            {
                StartCoroutine(ReloadRoutine());
            }
        }

        private void HandleADS()
        {
            IsADS = Input.GetButton("Fire2");
            Vector3 target = IsADS ? adsPosition : _hipPosition;
            transform.localPosition = Vector3.Lerp(
                transform.localPosition, target, adsSpeed * Time.deltaTime);
        }

        // ── Shooting ──────────────────────────────────────────────────────────
        protected virtual void TryShoot()
        {
            _nextFireTime = Time.time + _fireInterval;
            CurrentAmmo--;
            OnAmmoChanged?.Invoke(CurrentAmmo, ReserveAmmo);

            PerformRaycast();
            PlayMuzzleFlash();
            PlaySound(shootSound);

            // Spread crosshair on each shot
            UIManager.Instance?.AddCrosshairSpread(recoilVertical * 6f);

            // Camera recoil
            Player.PlayerCamera cam = fpsCam.GetComponent<Player.PlayerCamera>();
            cam?.ApplyRecoil(recoilVertical, recoilHorizontal);

            // Burst mode tracking
            if (fireMode == FireMode.Burst)
            {
                _burstFired++;
                if (_burstFired >= burstCount)
                {
                    _burstFired = 0;
                    _nextFireTime = Time.time + _fireInterval * 3f;   // gap between bursts
                }
            }
        }

        protected virtual void PerformRaycast()
        {
            Ray ray = new Ray(fpsCam.transform.position, fpsCam.transform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, range))
            {
                SpawnImpactEffect(hit);

                IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null && target.IsAlive)
                {
                    bool headshot = hit.collider.CompareTag("Head");
                    float dmg = headshot ? damage * headshotMult : damage;
                    target.TakeDamage(dmg, hit.point, ray.direction);
                    UIManager.Instance?.ShowHitMarker();
                }
            }
        }

        // ── Reload ────────────────────────────────────────────────────────────
        protected virtual System.Collections.IEnumerator ReloadRoutine()
        {
            IsReloading = true;
            OnReloadStart?.Invoke();
            PlaySound(reloadSound);

            yield return new WaitForSeconds(reloadTime);

            int needed = magazineSize - CurrentAmmo;
            int take   = Mathf.Min(needed, ReserveAmmo);
            CurrentAmmo  += take;
            ReserveAmmo  -= take;

            IsReloading = false;
            OnReloadStart?.Invoke();
            OnAmmoChanged?.Invoke(CurrentAmmo, ReserveAmmo);
            OnReloadEnd?.Invoke();
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void PlayMuzzleFlash()
        {
            if (muzzleFlash != null) muzzleFlash.Play();
        }

        private void SpawnImpactEffect(RaycastHit hit)
        {
            if (bulletImpactPrefab == null) return;
            GameObject impact = Instantiate(bulletImpactPrefab, hit.point,
                                            Quaternion.LookRotation(hit.normal));
            Destroy(impact, 2f);
        }

        protected void PlaySound(AudioClip clip)
        {
            if (_audio == null) return;
            if (clip != null)
                _audio.PlayOneShot(clip);
            else
                PlayProceduralShot();  // fallback click so shooting feels responsive
        }

        // Generates a quick synthetic gunshot when no AudioClip is assigned
        private void PlayProceduralShot()
        {
            int sampleRate = AudioSettings.outputSampleRate;
            int samples    = sampleRate / 10;   // 100ms burst
            float[] data   = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t    = (float)i / sampleRate;
                float env  = Mathf.Exp(-t * 40f);           // fast decay
                data[i]    = env * (UnityEngine.Random.value * 2f - 1f); // white noise
            }
            AudioClip tmp = AudioClip.Create("shot", samples, 1, sampleRate, false);
            tmp.SetData(data, 0);
            _audio.PlayOneShot(tmp, 0.6f);
        }

        /// <summary>Adds ammo to reserve (called by ammo pickups).</summary>
        public void RefillReserve(int amount)
        {
            ReserveAmmo = Mathf.Min(maxReserveAmmo, ReserveAmmo + amount);
            OnAmmoChanged?.Invoke(CurrentAmmo, ReserveAmmo);
        }

        /// <summary>Called by WeaponManager when the weapon is drawn/holstered.</summary>
        public virtual void OnEquip()
        {
            gameObject.SetActive(true);
            PlaySound(drawSound);
        }

        public virtual void OnHolster()
        {
            if (IsReloading)
                StopAllCoroutines();
            gameObject.SetActive(false);
        }
    }
}

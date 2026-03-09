using System;
using FreeWorld.Audio;
using FreeWorld.Managers;
using FreeWorld.Utilities;
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
        [Tooltip("Optional — leave blank to auto-assign from the WeaponAudioBank.")]
        [SerializeField] protected AudioClip shootSound;
        [SerializeField] protected AudioClip reloadSound;
        [SerializeField] protected AudioClip emptySound;
        [SerializeField] protected AudioClip drawSound;

        [Header("Audio Bank (optional — auto-found in Resources)")]
        [Tooltip("Shared WeaponAudioBank asset. Leave blank — the field is auto-populated from Resources/WeaponAudioBank.")]
        [SerializeField] private WeaponAudioBank audioBank;

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

        // ── Viewmodel ─────────────────────────────────────────────────────────
        private Transform _viewmodelRoot;
        private bool      _viewmodelBuilt;
        private float     _viewBobTimer;
        private float     _kickZ;

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

            // Fill in any missing AudioClips with synthesised fallbacks
            AssignFallbackClips();
        }

        protected virtual void Update()
        {
            // Block all weapon input while not playing
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameState.Playing) return;

            HandleADS();
            HandleShootInput();
            HandleReloadInput();
            TickViewmodel();
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
            PlayShootSound();

            // Spread crosshair on each shot
            UIManager.Instance?.AddCrosshairSpread(recoilVertical * 6f);

            // Camera recoil
            Player.PlayerCamera cam = fpsCam.GetComponent<Player.PlayerCamera>();
            cam?.ApplyRecoil(recoilVertical, recoilHorizontal);
            _kickZ = 0.05f;

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
            // Tracer starts slightly in front of the camera to avoid spawning inside walls
            Vector3 tracerOrigin = fpsCam.transform.position + fpsCam.transform.forward * 0.8f;

            if (Physics.Raycast(ray, out RaycastHit hit, range))
            {
                SpawnImpactEffect(hit);
                VFXManager.BulletTracer(tracerOrigin, hit.point);

                IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null && target.IsAlive)
                {
                    bool headshot = hit.collider.CompareTag("Head");
                    float dmg = headshot ? damage * headshotMult : damage;
                    target.TakeDamage(dmg, hit.point, ray.direction);
                    UIManager.Instance?.ShowHitMarker();
                }
            }
            else
            {
                // Missed shot — tracer extends to max range
                VFXManager.BulletTracer(tracerOrigin,
                    tracerOrigin + fpsCam.transform.forward * (range - 0.8f));
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
            Vector3 muzzlePos = muzzleFlash != null
                ? muzzleFlash.transform.position
                : fpsCam.transform.position + fpsCam.transform.forward * 0.6f;

            if (muzzleFlash != null) muzzleFlash.Play();
            VFXManager.MuzzleFlashLight(muzzlePos);
        }

        private void SpawnImpactEffect(RaycastHit hit)
        {
            bool isFlesh = hit.collider.CompareTag("Enemy") || hit.collider.CompareTag("Head");
            if (isFlesh)
                VFXManager.BloodHit(hit.point, hit.normal);
            else
            {
                VFXManager.BulletSpark(hit.point, hit.normal);
                VFXManager.BulletHole(hit.point, hit.normal, hit.transform);
            }

            // Legacy prefab support (still works if assigned in Inspector)
            if (bulletImpactPrefab != null)
            {
                GameObject impact = Instantiate(bulletImpactPrefab, hit.point,
                                                Quaternion.LookRotation(hit.normal));
                Destroy(impact, 2f);
            }
        }

        protected void PlaySound(AudioClip clip)
        {
            if (_audio == null) return;
            if (clip != null)
                _audio.PlayOneShot(clip);
            else
                PlayProceduralShot();  // fallback click so shooting feels responsive
        }

        // Picks a fresh random clip from the bank each shot to avoid repetition.
        private void PlayShootSound()
        {
            if (audioBank != null)
            {
                var pick = WeaponAudioBank.Pick(audioBank.ShootClipsFor(weaponType));
                if (pick != null) { _audio?.PlayOneShot(pick); return; }
            }
            PlaySound(shootSound);  // single cached clip or procedural
        }

        // Use the cached ProceduralAudioLibrary instead of allocating a new clip each shot
        private void PlayProceduralShot()
        {
            ProceduralAudioLibrary.Play(_audio,
                weaponType == WeaponType.Shotgun
                    ? ProceduralAudioLibrary.ClipShotgunBlast
                    : ProceduralAudioLibrary.ClipGunshot, 0.9f);
        }

        // Suppress warnings — kept for potential subclass override
        [System.Obsolete("Use PlayProceduralShot() — kept for API compatibility")]
        private void _unused_OldSynthShot()
        {
            int sampleRate = AudioSettings.outputSampleRate;
            int samples    = sampleRate / 10;
            float[] data   = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t    = (float)i / sampleRate;
                float env  = Mathf.Exp(-t * 40f);
                data[i]    = env * (UnityEngine.Random.value * 2f - 1f);
            }
            AudioClip tmp = AudioClip.Create("shot", samples, 1, sampleRate, false);
            tmp.SetData(data, 0);
            _audio.PlayOneShot(tmp, 0.6f);
        }

        // ── Procedural fallbacks in Awake ─────────────────────────────────────
        // Assign clips: SerializedField → AudioBank → ProceduralAudioLibrary
        private void AssignFallbackClips()
        {
            // Try to auto-find bank if not wired in Inspector
            if (audioBank == null)
                audioBank = Resources.Load<WeaponAudioBank>("WeaponAudioBank");

            if (shootSound  == null && audioBank != null)
                shootSound  = WeaponAudioBank.Pick(audioBank.ShootClipsFor(weaponType));
            if (reloadSound == null && audioBank != null)
                reloadSound = WeaponAudioBank.Pick(audioBank.ReloadClipsFor(weaponType));
            if (emptySound  == null && audioBank != null)
                emptySound  = WeaponAudioBank.Pick(audioBank.DryFire);

            // Final fallback: synthesised procedural audio
            if (shootSound  == null) shootSound  = ProceduralAudioLibrary.ClipGunshot;
            if (reloadSound == null) reloadSound = ProceduralAudioLibrary.ClipReload;
            if (emptySound  == null) emptySound  = ProceduralAudioLibrary.ClipEmpty;
        }

        /// <summary>Adds ammo to reserve (called by ammo pickups).</summary>
        public void RefillReserve(int amount)
        {
            ReserveAmmo = Mathf.Min(maxReserveAmmo, ReserveAmmo + amount);
            OnAmmoChanged?.Invoke(CurrentAmmo, ReserveAmmo);
        }

        /// <summary>Set weapon stats at runtime (used by WeaponManager auto-loadout).</summary>
        public void ApplyRuntimeStats(WeaponType wt, FireMode fm, float dmg, float fireRt,
                                       int mag, int res, float reload, Camera cam)
        {
            weaponType     = wt;
            fireMode       = fm;
            damage         = dmg;
            fireRate       = fireRt;
            magazineSize   = mag;
            maxReserveAmmo = res;
            reloadTime     = reload;
            fpsCam         = cam;
            _fireInterval  = 60f / fireRate;
            CurrentAmmo    = magazineSize;
            ReserveAmmo    = maxReserveAmmo;
            AssignFallbackClips();
        }

        /// <summary>Called by WeaponManager when the weapon is drawn/holstered.</summary>
        public virtual void OnEquip()
        {
            gameObject.SetActive(true);
            if (!_viewmodelBuilt)
            {
                BuildViewmodel();
                _viewmodelBuilt = true;
            }
            if (_viewmodelRoot != null) _viewmodelRoot.gameObject.SetActive(true);
            PlaySound(drawSound);
        }

        public virtual void OnHolster()
        {
            if (IsReloading)
                StopAllCoroutines();
            if (_viewmodelRoot != null) _viewmodelRoot.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        // ── Procedural first-person viewmodel ─────────────────────────────────
        private void BuildViewmodel()
        {
            if (fpsCam == null) return;

            // Hide any placeholder mesh on the weapon GO itself — all visuals live in the viewmodel
            foreach (var mr in GetComponentsInChildren<MeshRenderer>())
                mr.enabled = false;

            var root = new GameObject("VM_" + weaponName).transform;
            root.SetParent(fpsCam.transform, false);
            root.localPosition = new Vector3(0.22f, -0.22f, 0.45f);
            root.localRotation = Quaternion.identity;
            _viewmodelRoot = root;

            Color skin = new Color(0.78f, 0.64f, 0.54f);

            // Arm cylinder
            MakeVMPart("VM_Arm", PrimitiveType.Cylinder, root,
                new Vector3(0f, -0.22f, 0.04f),
                Quaternion.Euler(15f, 0f, 0f),
                new Vector3(0.065f, 0.19f, 0.065f), skin);

            // Hand
            MakeVMPart("VM_Hand", PrimitiveType.Cube, root,
                new Vector3(0f, -0.04f, 0.02f),
                Quaternion.identity,
                new Vector3(0.09f, 0.07f, 0.13f), skin);

            switch (weaponType)
            {
                case WeaponType.Pistol:  BuildPistolShape(root);  break;
                case WeaponType.Shotgun: BuildShotgunShape(root); break;
                default:                 BuildRifleShape(root);   break;
            }

            root.gameObject.SetActive(false);
        }

        private void BuildRifleShape(Transform root)
        {
            Color metal = new Color(0.19f, 0.19f, 0.21f);
            Color wood  = new Color(0.36f, 0.22f, 0.11f);

            MakeVMPart("VM_Receiver", PrimitiveType.Cube, root,
                new Vector3(0f, 0.01f, 0.13f), Quaternion.identity,
                new Vector3(0.058f, 0.068f, 0.30f), metal);

            MakeVMPart("VM_Barrel", PrimitiveType.Cylinder, root,
                new Vector3(0f, 0.024f, 0.34f), Quaternion.Euler(90f, 0f, 0f),
                new Vector3(0.021f, 0.16f, 0.021f), metal);

            MakeVMPart("VM_Magazine", PrimitiveType.Cube, root,
                new Vector3(0f, -0.065f, 0.10f), Quaternion.Euler(-14f, 0f, 0f),
                new Vector3(0.040f, 0.11f, 0.036f), metal);

            MakeVMPart("VM_Stock", PrimitiveType.Cube, root,
                new Vector3(0f, -0.01f, -0.05f), Quaternion.Euler(5f, 0f, 0f),
                new Vector3(0.05f, 0.06f, 0.13f), wood);
        }

        private void BuildPistolShape(Transform root)
        {
            Color metal = new Color(0.18f, 0.18f, 0.20f);
            Color grip  = new Color(0.11f, 0.09f, 0.09f);

            MakeVMPart("VM_Slide", PrimitiveType.Cube, root,
                new Vector3(0f, 0.022f, 0.09f), Quaternion.identity,
                new Vector3(0.048f, 0.058f, 0.18f), metal);

            MakeVMPart("VM_Barrel", PrimitiveType.Cylinder, root,
                new Vector3(0f, 0.030f, 0.21f), Quaternion.Euler(90f, 0f, 0f),
                new Vector3(0.018f, 0.074f, 0.018f), metal);

            MakeVMPart("VM_Grip", PrimitiveType.Cube, root,
                new Vector3(0f, -0.08f, 0.04f), Quaternion.Euler(-10f, 0f, 0f),
                new Vector3(0.044f, 0.10f, 0.05f), grip);
        }

        private void BuildShotgunShape(Transform root)
        {
            Color metal = new Color(0.22f, 0.22f, 0.24f);
            Color wood  = new Color(0.30f, 0.18f, 0.08f);

            MakeVMPart("VM_Receiver", PrimitiveType.Cube, root,
                new Vector3(0f, 0.01f, 0.11f), Quaternion.identity,
                new Vector3(0.072f, 0.074f, 0.28f), metal);

            MakeVMPart("VM_Barrel", PrimitiveType.Cylinder, root,
                new Vector3(0f, 0.036f, 0.32f), Quaternion.Euler(90f, 0f, 0f),
                new Vector3(0.030f, 0.18f, 0.030f), metal);

            MakeVMPart("VM_Pump", PrimitiveType.Cube, root,
                new Vector3(0f, 0.018f, 0.22f), Quaternion.identity,
                new Vector3(0.066f, 0.052f, 0.082f), wood);

            MakeVMPart("VM_Stock", PrimitiveType.Cube, root,
                new Vector3(0f, -0.022f, -0.06f), Quaternion.Euler(4f, 0f, 0f),
                new Vector3(0.062f, 0.065f, 0.18f), wood);
        }

        private void MakeVMPart(string partName, PrimitiveType prim, Transform parent,
                                 Vector3 localPos, Quaternion localRot, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(prim);
            go.name = partName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale    = scale;

            Destroy(go.GetComponent<Collider>());

            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows    = false;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mr.sharedMaterial = mat;
        }

        private void TickViewmodel()
        {
            if (_viewmodelRoot == null) return;

            _viewBobTimer += Time.deltaTime;

            // Scale bob by movement input for a natural feel
            float move   = new Vector2(Input.GetAxisRaw("Horizontal"),
                                       Input.GetAxisRaw("Vertical")).magnitude;
            float bobAmp = move * 0.005f + 0.0015f;
            float bobY   = Mathf.Sin(_viewBobTimer * 7.0f) * bobAmp;
            float bobX   = Mathf.Sin(_viewBobTimer * 3.5f) * bobAmp * 0.5f;

            // Kick: spring back to rest
            _kickZ = Mathf.Lerp(_kickZ, 0f, 14f * Time.deltaTime);

            // ADS: bring weapon to centre and slightly closer
            Vector3 hipPos = new Vector3(0.22f, -0.22f, 0.45f);
            Vector3 adsPos = new Vector3(0.00f, -0.16f, 0.38f);
            Vector3 target = Vector3.Lerp(hipPos, adsPos, IsADS ? 1f : 0f);
            target = Vector3.Lerp(_viewmodelRoot.localPosition, target, adsSpeed * Time.deltaTime);

            _viewmodelRoot.localPosition = target + new Vector3(bobX, bobY, -_kickZ);
        }
    }
}

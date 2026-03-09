using UnityEngine;

namespace FreeWorld.Weapons
{
    /// <summary>
    /// Manages the player's weapon inventory and switching.
    /// Attach to the Player root. Drag weapon child GameObjects into slots.
    /// </summary>
    public class WeaponManager : MonoBehaviour
    {
        [Header("Weapon Slots (assign in Inspector)")]
        [SerializeField] private WeaponBase[] weapons;     // max 5 like CS2

        [Header("Switch Speed")]
        [SerializeField] private float switchDelay = 0.3f;

        public WeaponBase CurrentWeapon { get; private set; }
        private int   _currentIndex = 0;
        private float _nextSwitchTime;

        // ── Managers/UI reference ─────────────────────────────────────────────
        private Managers.UIManager _ui;

        // ─────────────────────────────────────────────────────────────────────
        private void Start()
        {
            _ui = FindObjectOfType<Managers.UIManager>();

            EnsureDefaultLoadout();

            // Holster all, then equip slot 0
            foreach (var w in weapons)
                if (w != null) w.gameObject.SetActive(false);

            EquipWeapon(0);
        }

        /// <summary>
        /// Guarantees the player always has Rifle / Pistol / Shotgun even when
        /// the scene pre-dates a setup-script change. Creates any missing weapon
        /// as a procedural child GO with the correct component and camera ref.
        /// </summary>
        private void EnsureDefaultLoadout()
        {
            Camera cam = Camera.main;

            (System.Type type, string wname, WeaponType wt, FireMode fm,
             float dmg, float rate, int mag, int res, float reload)[] defaults =
            {
                (typeof(AssaultRifle), "AK-47",   WeaponType.Rifle,   FireMode.Auto,     22f, 650f, 30, 180, 2.5f),
                (typeof(Pistol),       "Pistol",   WeaponType.Pistol,  FireMode.SemiAuto, 35f, 400f, 15,  60, 1.8f),
                (typeof(Shotgun),      "Shotgun",  WeaponType.Shotgun, FireMode.SemiAuto, 18f, 120f,  6,  30, 2.0f),
            };

            int needed = defaults.Length;
            if (weapons == null)           weapons = new WeaponBase[needed];
            if (weapons.Length < needed)   System.Array.Resize(ref weapons, needed);

            for (int i = 0; i < needed; i++)
            {
                var (type, wname, wt, fm, dmg, rate, mag, res, reload) = defaults[i];

                if (weapons[i] == null)
                {
                    // Create new GO + component for this slot
                    var go = new GameObject("Weapon_" + wname + "_Auto");
                    go.transform.SetParent(transform, false);
                    go.AddComponent<AudioSource>();
                    var wb2 = (WeaponBase)go.AddComponent(type);
                    wb2.weaponName = wname;
                    wb2.ApplyRuntimeStats(wt, fm, dmg, rate, mag, res, reload, cam);
                    weapons[i] = wb2;
                    continue;
                }

                // Slot pre-populated: correct any wrong defaults (e.g. Shotgun placeholder
                // that has weaponType=Rifle because FreeWorldSetup didn't set it).
                var existing = weapons[i];
                if (existing.weaponType != wt || existing.weaponName == "Rifle" || string.IsNullOrEmpty(existing.weaponName))
                {
                    existing.weaponName = wname;
                    existing.ApplyRuntimeStats(wt, fm, dmg, rate, mag, res, reload, cam);
                }
            }

            _ui?.BuildWeaponCarousel(weapons);
        }

        private void Update()
        {
            HandleScrollSwitch();
            HandleNumberKeySwitch();
        }

        // ── Input ─────────────────────────────────────────────────────────────
        private void HandleScrollSwitch()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0f)  SwitchWeapon(_currentIndex - 1);
            if (scroll < 0f)  SwitchWeapon(_currentIndex + 1);
        }

        private void HandleNumberKeySwitch()
        {
            for (int i = 0; i < Mathf.Min(weapons.Length, 9); i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    SwitchWeapon(i);
        }

        // ── Switch logic ──────────────────────────────────────────────────────
        public void SwitchWeapon(int index)
        {
            if (Time.time < _nextSwitchTime) return;
            if (index == _currentIndex)      return;
            if (index < 0 || index >= weapons.Length) return;
            if (weapons[index] == null)       return;

            _nextSwitchTime = Time.time + switchDelay;

            CurrentWeapon?.OnHolster();
            EquipWeapon(index);
        }

        private void EquipWeapon(int index)
        {
            if (index < 0 || index >= weapons.Length || weapons[index] == null)
                return;

            _currentIndex = index;
            CurrentWeapon = weapons[index];
            CurrentWeapon.OnEquip();

            // Subscribe ammo changes to UI
            if (_ui != null)
            {
                CurrentWeapon.OnAmmoChanged -= _ui.UpdateAmmo;
                CurrentWeapon.OnAmmoChanged += _ui.UpdateAmmo;
            }

            _ui?.UpdateAmmo(CurrentWeapon.CurrentAmmo, CurrentWeapon.ReserveAmmo);
            _ui?.UpdateWeaponName(CurrentWeapon.weaponName);
            _ui?.UpdateWeaponCarousel(_currentIndex);
        }

        /// <summary>
        /// Add weapon by prefab (e.g. picked up from ground).
        /// Finds first empty slot and assigns it.
        /// </summary>
        public bool PickupWeapon(WeaponBase weapon)
        {
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] == null)
                {
                    weapons[i] = weapon;
                    SwitchWeapon(i);
                    return true;
                }
            }
            return false;   // inventory full
        }

        /// <summary>Add ammo to current weapon's reserve (called by ammo pickups).</summary>
        public void RefillCurrentWeaponAmmo(int amount)
        {
            if (CurrentWeapon == null) return;
            CurrentWeapon.RefillReserve(amount);
        }
    }
}

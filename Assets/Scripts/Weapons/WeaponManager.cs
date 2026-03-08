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

            // Holster all, then equip slot 0
            foreach (var w in weapons)
                if (w != null) w.gameObject.SetActive(false);

            EquipWeapon(0);
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

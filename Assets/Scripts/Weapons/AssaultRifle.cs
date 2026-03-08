using UnityEngine;

namespace FreeWorld.Weapons
{
    /// <summary>
    /// Full-auto Assault Rifle. All core behaviour is in WeaponBase.
    /// Reset() sets Inspector defaults so a new component looks right immediately.
    /// </summary>
    public class AssaultRifle : WeaponBase
    {
#if UNITY_EDITOR
        // Called by Unity when the component is first added in the Inspector.
        protected void Reset()
        {
            weaponName       = "AK-47";
            weaponType       = WeaponType.Rifle;
            fireMode         = FireMode.Auto;
            damage           = 22f;
            fireRate         = 650f;
            magazineSize     = 30;
            maxReserveAmmo   = 180;
            reloadTime       = 2.5f;
            recoilVertical   = 0.6f;
            recoilHorizontal = 0.18f;
            range            = 250f;
        }
#endif
    }
}

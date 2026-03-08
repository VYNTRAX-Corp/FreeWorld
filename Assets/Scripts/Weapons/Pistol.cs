using UnityEngine;

namespace FreeWorld.Weapons
{
    /// <summary>
    /// Semi-auto Pistol — lower capacity, higher per-shot damage, short range.
    /// Inherits all behaviour from WeaponBase.
    /// </summary>
    public class Pistol : WeaponBase
    {
#if UNITY_EDITOR
        protected void Reset()
        {
            weaponName       = "Pistol";
            weaponType       = WeaponType.Pistol;
            fireMode         = FireMode.SemiAuto;
            damage           = 35f;
            fireRate         = 400f;
            magazineSize     = 15;
            maxReserveAmmo   = 60;
            reloadTime       = 1.8f;
            recoilVertical   = 0.8f;
            recoilHorizontal = 0.3f;
            range            = 100f;
        }
#endif
    }
}

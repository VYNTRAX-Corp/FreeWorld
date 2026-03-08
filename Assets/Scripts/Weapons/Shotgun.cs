using UnityEngine;

namespace FreeWorld.Weapons
{
    /// <summary>
    /// Shotgun: fires multiple pellets per shot using spread.
    /// Inherits all base behaviour; just overrides PerformRaycast().
    /// </summary>
    public class Shotgun : WeaponBase
    {
        [Header("Shotgun Settings")]
        [SerializeField] private int   pelletCount    = 8;
        [SerializeField] private float spreadAngle    = 5f;   // degrees

        protected override void PerformRaycast()
        {
            for (int i = 0; i < pelletCount; i++)
            {
                Vector3 spread = new Vector3(
                    Random.Range(-spreadAngle, spreadAngle),
                    Random.Range(-spreadAngle, spreadAngle),
                    0f);

                Vector3 dir = Quaternion.Euler(spread) * fpsCam.transform.forward;
                Ray ray = new Ray(fpsCam.transform.position, dir);

                if (Physics.Raycast(ray, out RaycastHit hit, range))
                {
                    IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                    target?.TakeDamage(damage, hit.point, dir);
                }
            }
        }
    }
}

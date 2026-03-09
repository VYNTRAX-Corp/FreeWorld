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
            Vector3 tracerOrigin = fpsCam.transform.position + fpsCam.transform.forward * 0.8f;

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
                    // Damage
                    IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                    if (target != null && target.IsAlive)
                    {
                        bool headshot = hit.collider.CompareTag("Head");
                        target.TakeDamage(headshot ? damage * 2f : damage, hit.point, dir);
                        Managers.UIManager.Instance?.ShowHitMarker();
                    }

                    // Surface effects — same as WeaponBase
                    bool isFlesh = hit.collider.CompareTag("Enemy") || hit.collider.CompareTag("Head");
                    if (isFlesh)
                        Utilities.VFXManager.BloodHit(hit.point, hit.normal);
                    else
                    {
                        Utilities.VFXManager.BulletSpark(hit.point, hit.normal);
                        Utilities.VFXManager.BulletHole(hit.point, hit.normal, hit.transform);
                    }

                    Utilities.VFXManager.BulletTracer(tracerOrigin, hit.point);
                }
                else
                {
                    Utilities.VFXManager.BulletTracer(tracerOrigin,
                        tracerOrigin + dir * (range - 0.8f));
                }
            }
        }
    }
}

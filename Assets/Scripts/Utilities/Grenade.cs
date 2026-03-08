using System.Collections;
using UnityEngine;

namespace FreeWorld.Utilities
{
    /// <summary>
    /// Physics-based frag grenade.
    /// Throw via GrenadeThrow.cs on the player. Deals radius damage with falloff.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Grenade : MonoBehaviour
    {
        [Header("Explosion")]
        [SerializeField] private float fuseTime       = 3f;
        [SerializeField] private float blastRadius    = 8f;
        [SerializeField] private float maxDamage      = 100f;  // at epicentre
        [SerializeField] private float minDamage      = 10f;   // at blast edge
        [SerializeField] private float blastForce     = 500f;
        [SerializeField] private LayerMask damageMask = ~0;    // hit everything

        [Header("VFX / Audio")]
        [SerializeField] private GameObject explosionPrefab;  // particle effect (optional)
        [SerializeField] private AudioClip  explosionSound;
        [SerializeField] private AudioClip  bounceSound;

        private bool _exploded;

        // Called by GrenadeThrow to subtract already-cooked time from the fuse
        private void ReduceFuse(float cookedSeconds)
        {
            fuseTime = Mathf.Max(0.1f, fuseTime - cookedSeconds);
        }

        // ─────────────────────────────────────────────────────────────────────
        private void Start()
        {
            StartCoroutine(FuseRoutine());
        }

        private IEnumerator FuseRoutine()
        {
            yield return new WaitForSeconds(fuseTime);
            Explode();
        }

        // ── Bounce sound ──────────────────────────────────────────────────────
        private void OnCollisionEnter(Collision col)
        {
            if (bounceSound != null && col.relativeVelocity.magnitude > 1f)
                AudioSource.PlayClipAtPoint(bounceSound, transform.position, 0.5f);
        }

        // ── Explosion ─────────────────────────────────────────────────────────
        private void Explode()
        {
            if (_exploded) return;
            _exploded = true;

            Vector3 pos = transform.position;

            // Spawn VFX
            if (explosionPrefab != null)
                Destroy(Instantiate(explosionPrefab, pos, Quaternion.identity), 4f);

            if (explosionSound != null)
                AudioSource.PlayClipAtPoint(explosionSound, pos);

            // Damage all colliders in radius
            Collider[] hits = Physics.OverlapSphere(pos, blastRadius, damageMask);
            foreach (Collider col in hits)
            {
                float dist   = Vector3.Distance(pos, col.transform.position);
                float t      = 1f - Mathf.Clamp01(dist / blastRadius);
                float damage = Mathf.Lerp(minDamage, maxDamage, t);

                // Apply damage if damageable
                IDamageable dmg = col.GetComponentInParent<IDamageable>();
                Vector3 dir = (col.transform.position - pos).normalized;
                dmg?.TakeDamage(damage, col.ClosestPoint(pos), dir);

                // Apply physics force to rigidbodies
                Rigidbody rb = col.attachedRigidbody;
                if (rb != null)
                    rb.AddExplosionForce(blastForce, pos, blastRadius, 0.5f, ForceMode.Impulse);
            }

            // Screen shake / camera feedback for local player
            Managers.UIManager.Instance?.TriggerExplosionShake();

            Destroy(gameObject);
        }

        // ── Gizmo so you can see blast radius in Scene view ───────────────────
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.3f);
            Gizmos.DrawSphere(transform.position, blastRadius);
        }
    }
}

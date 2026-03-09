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

        /// <summary>Subtract already-cooked time from the fuse. Called by GrenadeThrow.</summary>
        public void ReduceFuse(float cookedSeconds)
        {
            fuseTime = Mathf.Max(0.1f, fuseTime - cookedSeconds);
        }

        /// <summary>Build a grenade at runtime with no prefab — sphere + Rigidbody + collider.</summary>
        public static Grenade SpawnProcedural(Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Grenade";
            go.transform.position   = position;
            go.transform.localScale = Vector3.one * 0.18f;

            // Dark olive look
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", new Color(0.22f, 0.28f, 0.12f));
            go.GetComponent<Renderer>().material = mat;

            // Physics
            var rb                           = go.AddComponent<Rigidbody>();
            rb.mass                          = 0.4f;
            rb.collisionDetectionMode        = CollisionDetectionMode.Continuous;
            go.GetComponent<SphereCollider>().radius = 0.5f;  // matches unit-sphere

            return go.AddComponent<Grenade>();
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
            if (col.relativeVelocity.magnitude <= 1f) return;

            if (bounceSound != null)
                AudioSource.PlayClipAtPoint(bounceSound, transform.position, 0.5f);
            else
                ProceduralAudioLibrary.PlayAt(
                    ProceduralAudioLibrary.ClipBulletImpact, transform.position, 0.5f);
        }

        // ── Explosion ─────────────────────────────────────────────────────────
        private void Explode()
        {
            if (_exploded) return;
            _exploded = true;

            Vector3 pos = transform.position;

            // Spawn VFX — procedural if no prefab assigned
            if (explosionPrefab != null)
                Destroy(Instantiate(explosionPrefab, pos, Quaternion.identity), 4f);
            else
                SpawnProceduralExplosion(pos);

            // Audio — procedural if no clip assigned
            if (explosionSound != null)
                AudioSource.PlayClipAtPoint(explosionSound, pos);
            else
                ProceduralAudioLibrary.PlayAt(
                    ProceduralAudioLibrary.ClipShotgunBlast, pos, 1.0f);

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

        // ── Procedural explosion VFX ──────────────────────────────────────────
        private static void SpawnProceduralExplosion(Vector3 pos)
        {
            // Burst of orange/yellow debris spheres flying outward
            for (int i = 0; i < 18; i++)
            {
                var go   = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                float sz = Random.Range(0.06f, 0.22f);
                go.transform.position   = pos;
                go.transform.localScale = Vector3.one * sz;

                // Destroy collider so debris doesn't block bullets
                Object.Destroy(go.GetComponent<Collider>());

                float heat  = Random.value;
                var mat     = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                Color col   = Color.Lerp(new Color(1f, 0.3f, 0f), new Color(1f, 0.9f, 0f), heat);
                mat.SetColor("_BaseColor",     col);
                mat.SetColor("_EmissionColor", col * 2.5f);
                mat.EnableKeyword("_EMISSION");
                go.GetComponent<Renderer>().material = mat;

                var rb       = go.AddComponent<Rigidbody>();
                rb.useGravity = true;
                rb.velocity   = Random.insideUnitSphere.normalized * Random.Range(3f, 14f);

                Object.Destroy(go, Random.Range(0.3f, 0.9f));
            }

            // Central flash sphere — white, no gravity, very brief
            var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.transform.position   = pos;
            flash.transform.localScale = Vector3.one * 1.2f;
            Object.Destroy(flash.GetComponent<Collider>());
            var fm  = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            fm.SetColor("_BaseColor",     Color.white);
            fm.SetColor("_EmissionColor", Color.white * 5f);
            fm.EnableKeyword("_EMISSION");
            flash.GetComponent<Renderer>().material = fm;
            Object.Destroy(flash, 0.07f);
        }

        // ── Gizmo so you can see blast radius in Scene view ───────────────────
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.3f);
            Gizmos.DrawSphere(transform.position, blastRadius);
        }
    }
}

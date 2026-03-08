using UnityEngine;
using System.Collections;

namespace FreeWorld.Utilities
{
    /// <summary>
    /// Central VFX factory. All methods are static; the MonoBehaviour singleton
    /// is created automatically on first use and persists across scene loads.
    ///
    /// Available effects:
    ///   VFXManager.BloodHit(pos, normal)          — red sphere splatter
    ///   VFXManager.BulletSpark(pos, normal)        — orange spark cubes
    ///   VFXManager.BulletHole(pos, normal, parent) — flat black decal on surface
    ///   VFXManager.BulletTracer(from, to)          — emissive tracer cylinder
    ///   VFXManager.MuzzleFlashLight(pos)           — point-light burst
    /// </summary>
    public class VFXManager : MonoBehaviour
    {
        private static VFXManager _instance;

        private static VFXManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("VFXManager");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<VFXManager>();
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ── Public static API ─────────────────────────────────────────────────

        /// <summary>Red sphere particles — use on flesh / enemy hits.</summary>
        public static void BloodHit(Vector3 pos, Vector3 normal)
            => Instance.DoBloodHit(pos, normal);

        /// <summary>Orange spark cubes — use on metal / concrete hits.</summary>
        public static void BulletSpark(Vector3 pos, Vector3 normal)
            => Instance.DoBulletSpark(pos, normal);

        /// <summary>Flat black decal parented to the hit surface.</summary>
        public static void BulletHole(Vector3 pos, Vector3 normal, Transform parent = null)
            => Instance.DoBulletHole(pos, normal, parent);

        /// <summary>Brief emissive cylinder from shooter to impact point.</summary>
        public static void BulletTracer(Vector3 from, Vector3 to)
            => Instance.DoBulletTracer(from, to);

        /// <summary>Short-lived orange point light at the gun muzzle.</summary>
        public static void MuzzleFlashLight(Vector3 pos)
            => Instance.DoMuzzleFlashLight(pos);

        // ── Effect implementations ────────────────────────────────────────────

        private void DoBloodHit(Vector3 pos, Vector3 normal)
        {
            for (int i = 0; i < 5; i++)
            {
                float size = 0.025f + Random.value * 0.02f;
                var go = MakePrimitive("Blood", PrimitiveType.Sphere, pos,
                                       Vector3.one * size, new Color(0.65f, 0.02f, 0.02f));
                var rb = go.AddComponent<Rigidbody>();
                rb.velocity = (normal + Random.insideUnitSphere * 0.7f).normalized
                              * Random.Range(1.5f, 4.0f);
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                Destroy(go, 0.4f);
            }
        }

        private void DoBulletSpark(Vector3 pos, Vector3 normal)
        {
            for (int i = 0; i < 4; i++)
            {
                float size = 0.012f + Random.value * 0.010f;
                var go = MakePrimitive("Spark", PrimitiveType.Cube, pos,
                                       Vector3.one * size, new Color(1f, 0.6f, 0.08f));
                var mat = go.GetComponent<Renderer>().material;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(3f, 1.2f, 0.1f));
                var rb = go.AddComponent<Rigidbody>();
                rb.velocity = (normal + Random.insideUnitSphere * 0.5f).normalized
                              * Random.Range(3f, 8f);
                Destroy(go, 0.15f);
            }
        }

        private void DoBulletHole(Vector3 pos, Vector3 normal, Transform parent)
        {
            var go = MakePrimitive("BulletHole", PrimitiveType.Cube,
                                   pos + normal * 0.002f,
                                   new Vector3(0.06f, 0.002f, 0.06f),
                                   new Color(0.05f, 0.05f, 0.05f));
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
            if (parent != null) go.transform.SetParent(parent, true);
            Destroy(go, 12f);
        }

        private void DoBulletTracer(Vector3 from, Vector3 to)
        {
            float len = Vector3.Distance(from, to);
            if (len < 0.2f) return;

            var go = MakePrimitive("Tracer", PrimitiveType.Cylinder,
                                   (from + to) * 0.5f,
                                   new Vector3(0.007f, len * 0.5f, 0.007f),
                                   new Color(1f, 0.92f, 0.35f));
            go.transform.up = (to - from).normalized;
            var mat = go.GetComponent<Renderer>().material;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(4f, 2.2f, 0.4f));
            Destroy(go, 0.06f);
        }

        private void DoMuzzleFlashLight(Vector3 pos)
        {
            var go = new GameObject("MuzzleLight");
            go.transform.position = pos;
            var lt       = go.AddComponent<Light>();
            lt.type      = LightType.Point;
            lt.color     = new Color(1f, 0.65f, 0.2f);
            lt.intensity = 8f;
            lt.range     = 5f;
            lt.shadows   = LightShadows.None;
            StartCoroutine(FadeLight(go, lt, 0.07f));
        }

        private IEnumerator FadeLight(GameObject go, Light lt, float duration)
        {
            float start = lt.intensity;
            float t     = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                if (lt  != null) lt.intensity = Mathf.Lerp(start, 0f, t / duration);
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static GameObject MakePrimitive(string name, PrimitiveType type,
                                                 Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position   = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().material = MakeMat(color);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            return go;
        }

        private static Material MakeMat(Color color)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ??
                                   Shader.Find("Standard"));
            mat.color = color;
            return mat;
        }
    }
}

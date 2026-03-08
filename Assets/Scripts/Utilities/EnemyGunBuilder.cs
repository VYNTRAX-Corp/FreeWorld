using UnityEngine;

namespace FreeWorld.Utilities
{
    /// <summary>
    /// Holds the runtime references returned after building a gun on an enemy.
    /// </summary>
    public class GunVisual
    {
        public Transform GunRoot;
        /// <summary>World-positioned fire origin at the barrel tip.</summary>
        public Transform MuzzlePoint;
    }

    /// <summary>
    /// Builds a procedural rifle model and parents it to the enemy's right forearm.
    /// The barrel runs along the GunRoot's local +Y axis.
    /// With the gun-aim arm pose (shoulder 60° + elbow 30°) the barrel points
    /// world-forward (+Z) so shots travel toward the player.
    /// </summary>
    public static class EnemyGunBuilder
    {
        public static GunVisual Build(Transform rightForeArm)
        {
            if (rightForeArm == null) return null;

            // ── Gun root ────────────────────────────────────────────────────
            // Positioned at hand level; barrel extends along local +Y (= forward when arm is in aim pose)
            var gunRoot = new GameObject("GunRoot");
            gunRoot.transform.SetParent(rightForeArm, false);
            gunRoot.transform.localPosition = new Vector3(0f, -0.22f, 0f);
            gunRoot.transform.localRotation = Quaternion.identity;

            Material darkMetal  = MakeMat(new Color(0.17f, 0.17f, 0.19f));
            Material lightMetal = MakeMat(new Color(0.30f, 0.30f, 0.32f));
            Material stockMat   = MakeMat(new Color(0.22f, 0.13f, 0.07f));

            // Receiver / grip block (centre of the gun)
            MakePart("Receiver", gunRoot.transform, PrimitiveType.Cube,
                Vector3.zero,
                new Vector3(0.055f, 0.24f, 0.055f), darkMetal);

            // Barrel – thin cylinder, extends in +Y from receiver top
            MakePart("Barrel", gunRoot.transform, PrimitiveType.Cylinder,
                new Vector3(0f, 0.26f, 0f),
                new Vector3(0.020f, 0.10f, 0.020f), lightMetal);

            // Handguard / front grip around barrel
            MakePart("Handguard", gunRoot.transform, PrimitiveType.Cube,
                new Vector3(0f, 0.14f, 0f),
                new Vector3(0.062f, 0.065f, 0.062f), darkMetal);

            // Stock – extends in -Y (toward enemy's back when aiming)
            MakePart("Stock", gunRoot.transform, PrimitiveType.Cube,
                new Vector3(0f, -0.15f, -0.010f),
                new Vector3(0.040f, 0.10f, 0.040f), stockMat);

            // Magazine – hangs perpendicular to barrel, slightly forward
            MakePart("Magazine", gunRoot.transform, PrimitiveType.Cube,
                new Vector3(0f, 0.01f, 0.042f),
                new Vector3(0.030f, 0.09f, 0.022f), darkMetal);

            // Sight – small cube on top
            MakePart("Sight", gunRoot.transform, PrimitiveType.Cube,
                new Vector3(0f, 0.16f, -0.027f),
                new Vector3(0.018f, 0.06f, 0.018f), lightMetal);

            // Muzzle point at barrel tip
            var muzzleGO = new GameObject("MuzzlePoint");
            muzzleGO.transform.SetParent(gunRoot.transform, false);
            muzzleGO.transform.localPosition = new Vector3(0f, 0.38f, 0f);

            return new GunVisual
            {
                GunRoot    = gunRoot.transform,
                MuzzlePoint = muzzleGO.transform,
            };
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static void MakePart(string name, Transform parent,
            PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale    = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }

        private static Material MakeMat(Color c)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ??
                                   Shader.Find("Standard"));
            mat.color = c;
            return mat;
        }
    }
}

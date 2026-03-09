using UnityEngine;

namespace FreeWorld.Utilities
{
    /// <summary>
    /// Builds a multi-part procedural humanoid body from Unity primitives.
    /// Called at runtime from EnemyAI.Awake() so it works on every spawned enemy.
    /// Swap for a real Mixamo/Asset-Store FBX by replacing the body child GO.
    /// </summary>
    public static class EnemyHumanoidBuilder
    {
        // Base skin tone — warm medium
        private static readonly Color SkinColor     = new Color(0.87f, 0.68f, 0.54f);
        private static readonly Color BootColor     = new Color(0.14f, 0.10f, 0.08f);
        private static readonly Color BeltColor     = new Color(0.18f, 0.14f, 0.10f);

        // ── Public entry point ────────────────────────────────────────────────
        /// <summary>
        /// Replaces the plain capsule look with a jointed humanoid.
        /// Returns the body-parts struct so the animator can drive them.
        /// clothingColor tints the uniform: green=Grunt, blue=Heavy, grey=Scout.
        /// </summary>
        public static EnemyBodyParts Build(Transform root, Color clothingColor)
        {
            // Disable root capsule renderer (keep capsule collider + NavMesh on root)
            var rootRend = root.GetComponent<Renderer>();
            if (rootRend != null) rootRend.enabled = false;

            Material skinMat    = MakeMat(SkinColor);
            Material clothMat   = MakeMat(clothingColor);
            Material bootMat    = MakeMat(BootColor);
            Material beltMat    = MakeMat(BeltColor);

            var parts = new EnemyBodyParts();

            // ── Body root (offset so feet touch y=0) ─────────────────────────
            var body = new GameObject("HumanoidBody");
            body.transform.SetParent(root, false);
            body.transform.localPosition = new Vector3(0f, -1f, 0f); // capsule origin is centre
            parts.Body = body.transform;

            // ── Head ─────────────────────────────────────────────────────────
            parts.Head = MakePart("Head", body.transform, PrimitiveType.Sphere,
                new Vector3(0f, 1.72f, 0f), new Vector3(0.36f, 0.40f, 0.36f), skinMat,
                keepCollider: true, tag: "Head");

            // Face marker (darker sphere overlay for rough face direction)
            MakePart("FaceMarker", parts.Head, PrimitiveType.Sphere,
                new Vector3(0f, 0f, 0.14f), new Vector3(0.50f, 0.55f, 0.25f),
                MakeMat(new Color(SkinColor.r - 0.12f, SkinColor.g - 0.1f, SkinColor.b - 0.1f)));

            // ── Neck ─────────────────────────────────────────────────────────
            MakePart("Neck", body.transform, PrimitiveType.Cylinder,
                new Vector3(0f, 1.54f, 0f), new Vector3(0.12f, 0.09f, 0.12f), skinMat);

            // ── Torso ─────────────────────────────────────────────────────────
            parts.Torso = MakePart("Torso", body.transform, PrimitiveType.Cube,
                new Vector3(0f, 1.18f, 0f), new Vector3(0.54f, 0.52f, 0.26f), clothMat);

            // Belt
            MakePart("Belt", body.transform, PrimitiveType.Cube,
                new Vector3(0f, 0.86f, 0f), new Vector3(0.50f, 0.10f, 0.24f), beltMat);

            // ── Hips ──────────────────────────────────────────────────────────
            MakePart("Hips", body.transform, PrimitiveType.Cube,
                new Vector3(0f, 0.78f, 0f), new Vector3(0.48f, 0.18f, 0.24f), clothMat);

            // ── Left Arm ──────────────────────────────────────────────────────
            var lShoulder = new GameObject("LeftShoulder");
            lShoulder.transform.SetParent(body.transform, false);
            lShoulder.transform.localPosition = new Vector3(-0.34f, 1.34f, 0f);
            parts.LeftUpperArm = lShoulder.transform;
            MakePart("UpperArm_L", lShoulder.transform, PrimitiveType.Cylinder,
                new Vector3(0f, -0.14f, 0f), new Vector3(0.12f, 0.16f, 0.12f), clothMat);

            var lElbow = new GameObject("LeftElbow");
            lElbow.transform.SetParent(lShoulder.transform, false);
            lElbow.transform.localPosition = new Vector3(0f, -0.31f, 0f);
            parts.LeftForeArm = lElbow.transform;
            MakePart("ForeArm_L", lElbow.transform, PrimitiveType.Cylinder,
                new Vector3(0f, -0.13f, 0f), new Vector3(0.10f, 0.14f, 0.10f), skinMat);
            MakePart("Hand_L", lElbow.transform, PrimitiveType.Sphere,
                new Vector3(0f, -0.29f, 0f), new Vector3(0.11f, 0.11f, 0.11f), skinMat);

            // ── Right Arm ─────────────────────────────────────────────────────
            var rShoulder = new GameObject("RightShoulder");
            rShoulder.transform.SetParent(body.transform, false);
            rShoulder.transform.localPosition = new Vector3(0.34f, 1.34f, 0f);
            parts.RightUpperArm = rShoulder.transform;
            MakePart("UpperArm_R", rShoulder.transform, PrimitiveType.Cylinder,
                new Vector3(0f, -0.14f, 0f), new Vector3(0.12f, 0.16f, 0.12f), clothMat);

            var rElbow = new GameObject("RightElbow");
            rElbow.transform.SetParent(rShoulder.transform, false);
            rElbow.transform.localPosition = new Vector3(0f, -0.31f, 0f);
            parts.RightForeArm = rElbow.transform;
            MakePart("ForeArm_R", rElbow.transform, PrimitiveType.Cylinder,
                new Vector3(0f, -0.13f, 0f), new Vector3(0.10f, 0.14f, 0.10f), skinMat);
            MakePart("Hand_R", rElbow.transform, PrimitiveType.Sphere,
                new Vector3(0f, -0.29f, 0f), new Vector3(0.11f, 0.11f, 0.11f), skinMat);

            // ── Left Leg ──────────────────────────────────────────────────────
            var lHip = new GameObject("LeftHip");
            lHip.transform.SetParent(body.transform, false);
            lHip.transform.localPosition = new Vector3(-0.16f, 0.68f, 0f);
            parts.LeftUpperLeg = lHip.transform;
            MakePart("UpperLeg_L", lHip.transform, PrimitiveType.Cylinder,
                new Vector3(0f, -0.19f, 0f), new Vector3(0.15f, 0.22f, 0.15f), clothMat);

            var lKnee = new GameObject("LeftKnee");
            lKnee.transform.SetParent(lHip.transform, false);
            lKnee.transform.localPosition = new Vector3(0f, -0.41f, 0f);
            parts.LeftLowerLeg = lKnee.transform;
            MakePart("LowerLeg_L", lKnee.transform, PrimitiveType.Cylinder,
                new Vector3(0f, -0.17f, 0f), new Vector3(0.12f, 0.20f, 0.12f), clothMat);
            MakePart("Foot_L", lKnee.transform, PrimitiveType.Cube,
                new Vector3(0f, -0.38f, 0.06f), new Vector3(0.14f, 0.08f, 0.26f), bootMat);

            // ── Right Leg ─────────────────────────────────────────────────────
            var rHip = new GameObject("RightHip");
            rHip.transform.SetParent(body.transform, false);
            rHip.transform.localPosition = new Vector3(0.16f, 0.68f, 0f);
            parts.RightUpperLeg = rHip.transform;
            MakePart("UpperLeg_R", rHip.transform, PrimitiveType.Cylinder,
                new Vector3(0f, -0.19f, 0f), new Vector3(0.15f, 0.22f, 0.15f), clothMat);

            var rKnee = new GameObject("RightKnee");
            rKnee.transform.SetParent(rHip.transform, false);
            rKnee.transform.localPosition = new Vector3(0f, -0.41f, 0f);
            parts.RightLowerLeg = rKnee.transform;
            MakePart("LowerLeg_R", rKnee.transform, PrimitiveType.Cylinder,
                new Vector3(0f, -0.17f, 0f), new Vector3(0.12f, 0.20f, 0.12f), clothMat);
            MakePart("Foot_R", rKnee.transform, PrimitiveType.Cube,
                new Vector3(0f, -0.38f, 0.06f), new Vector3(0.14f, 0.08f, 0.26f), bootMat);

            // Gather all clothing renderers for variant recoloring
            parts.ClothingMaterial = clothMat;
            parts.ClothingRenderers = GatherClothingRenderers(body.transform, clothMat);

            return parts;
        }

        // ── Build from imported humanoid Animator (CC0 / real model path) ────
        /// <summary>
        /// Populates EnemyBodyParts from a Unity humanoid Animator using
        /// GetBoneTransform(HumanBodyBones.*) so no bone names need hardcoding.
        /// Call this instead of Build() when a real CC0 model is in use.
        /// clothingColor is applied as a MaterialPropertyBlock tint so the
        /// original materials are not replaced.
        /// </summary>
        public static EnemyBodyParts BuildFromAnimator(Transform root, Animator animator, Color clothingColor)
        {
            // Suppress root capsule mesh (keep collider / NavMesh)
            var rootRend = root.GetComponent<Renderer>();
            if (rootRend != null) rootRend.enabled = false;

            var parts = new EnemyBodyParts();

            // Map Unity's standard humanoid bones → our transform slots.
            // GetBoneTransform returns null for bones that don't exist in the rig —
            // the animator / procedural code handles null transforms gracefully.
            parts.Head         = animator.GetBoneTransform(HumanBodyBones.Head);
            parts.Body         = animator.GetBoneTransform(HumanBodyBones.Hips);
            parts.Torso        = animator.GetBoneTransform(HumanBodyBones.UpperChest)
                              ?? animator.GetBoneTransform(HumanBodyBones.Chest)
                              ?? animator.GetBoneTransform(HumanBodyBones.Spine);

            parts.LeftUpperArm  = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            parts.LeftForeArm   = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            parts.RightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            parts.RightForeArm  = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);

            parts.LeftUpperLeg  = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            parts.LeftLowerLeg  = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            parts.RightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            parts.RightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);

            // For imported models keep original textures intact — no tint applied.
            // ClothingRenderers is populated so SetClothingColor can optionally tint later.
            var renderers = root.GetComponentsInChildren<Renderer>();

            parts.ClothingRenderers = renderers;
            // ClothingMaterial stays null — use SetClothingColor overload below.
            return parts;
        }

        // ── Set uniform color (called by ApplyVariant) ────────────────────────
        public static void SetClothingColor(EnemyBodyParts parts, Color color)
        {
            if (parts?.ClothingRenderers == null) return;

            if (parts.ClothingMaterial != null)
            {
                // Procedural path — direct material tint
                foreach (var r in parts.ClothingRenderers)
                    if (r != null) r.material.color = color;
            }
            else
            {
                // Imported-model path — tint via property block (no material duplication)
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color",     color);
                foreach (var r in parts.ClothingRenderers)
                    if (r != null) r.SetPropertyBlock(block);
            }
        }

        // ── Clear tint — restore original textures (OriginalSkin variant) ─────
        public static void ClearClothingTint(EnemyBodyParts parts)
        {
            if (parts?.ClothingRenderers == null) return;
            // Reset _BaseColor to white = no tint; original material textures show through.
            // Cannot use SetPropertyBlock(null) — unreliable in URP.
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", Color.white);
            block.SetColor("_Color",     Color.white);
            foreach (var r in parts.ClothingRenderers)
                if (r != null) r.SetPropertyBlock(block);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static Transform MakePart(string name, Transform parent,
            PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat,
            bool keepCollider = false, string tag = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (tag != null) go.tag = tag;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
            }
            return go.transform;
        }

        private static Material MakeMat(Color color)
        {
            var mat   = new Material(Shader.Find("Universal Render Pipeline/Lit") ??
                                     Shader.Find("Standard"));
            mat.color = color;
            return mat;
        }

        private static Renderer[] GatherClothingRenderers(Transform body, Material clothMat)
        {
            var list = new System.Collections.Generic.List<Renderer>();
            foreach (var r in body.GetComponentsInChildren<Renderer>())
                if (r.sharedMaterial == clothMat) list.Add(r);
            return list.ToArray();
        }
    }

    // ── Data container ────────────────────────────────────────────────────────
    public class EnemyBodyParts
    {
        public Transform Body;
        public Transform Head;
        public Transform Torso;
        public Transform LeftUpperArm,  LeftForeArm;
        public Transform RightUpperArm, RightForeArm;
        public Transform LeftUpperLeg,  LeftLowerLeg;
        public Transform RightUpperLeg, RightLowerLeg;
        public Material  ClothingMaterial;
        public Renderer[] ClothingRenderers;
    }
}

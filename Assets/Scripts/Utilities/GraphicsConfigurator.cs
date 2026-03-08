using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FreeWorld.Utilities
{
    /// <summary>
    /// Configures URP post-processing at runtime:
    ///   • Bloom        — makes lights and emissive tracers glow
    ///   • Color Grading — slightly cinematic contrast + warm tint
    ///   • Vignette      — darkens screen edges for immersion
    ///   • Chromatic Aberration — subtle lens imperfection
    ///
    /// Place on any scene GameObject (FreeWorldSetup adds it to the PostProcessVolume GO).
    /// Enables post-processing on every Camera it finds so no manual Inspector step needed.
    /// </summary>
    [RequireComponent(typeof(Volume))]
    public class GraphicsConfigurator : MonoBehaviour
    {
        private void Start()
        {
            ConfigureVolume();
            EnableCameraPostProcessing();
        }

        private void ConfigureVolume()
        {
            var vol     = GetComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 100;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.profile = profile;

            // ── Bloom ─────────────────────────────────────────────────────────
            // Makes muzzle flashes, emissive tracers, sparks and UI elements glow
            var bloom = profile.Add<Bloom>(false);
            bloom.active = true;
            bloom.intensity.Override(1.8f);
            bloom.threshold.Override(0.75f);
            bloom.scatter.Override(0.65f);
            bloom.tint.Override(new Color(1f, 0.96f, 0.88f));   // slight warm bloom

            // ── Color Adjustments ─────────────────────────────────────────────
            // High-contrast, slightly desaturated, warm exposure — cinematic look
            var color = profile.Add<ColorAdjustments>(false);
            color.active = true;
            color.postExposure.Override(0.12f);
            color.contrast.Override(22f);
            color.saturation.Override(-12f);
            color.colorFilter.Override(new Color(1f, 0.97f, 0.94f));

            // ── Vignette ──────────────────────────────────────────────────────
            // Subtle edge darkening — immersive without looking like binoculars
            var vignette = profile.Add<Vignette>(false);
            vignette.active = true;
            vignette.intensity.Override(0.20f);
            vignette.smoothness.Override(0.30f);
            vignette.rounded.Override(true);

            // ── Chromatic Aberration ──────────────────────────────────────────
            // Subtle lens fringing — especially visible on bright muzzle flashes
            var ca = profile.Add<ChromaticAberration>(false);
            ca.active = true;
            ca.intensity.Override(0.10f);
        }

        private void EnableCameraPostProcessing()
        {
            foreach (var cam in FindObjectsOfType<Camera>())
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null)
                    data.renderPostProcessing = true;
            }
        }
    }
}

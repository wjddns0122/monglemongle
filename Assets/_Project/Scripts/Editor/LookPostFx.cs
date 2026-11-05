using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Mongle.EditorTools
{
    /// <summary>Per-scene post-processing moods (warm bloom, soft vignette, gentle grade). URP stays as is.</summary>
    public static class LookPostFx
    {
        const string Dir = "Assets/_Project/Art/Look/";

        public struct Mood
        {
            public float exposure, contrast, saturation, bloomThreshold, bloomIntensity, bloomScatter, vignette, vignetteSmooth;
            public Color filter, bloomTint, vignetteColor;
        }

        public static readonly Mood Shop = new Mood { exposure = 0.0f, contrast = 8, saturation = 0, bloomThreshold = 0.85f, bloomIntensity = 0.45f, bloomScatter = 0.72f, vignette = 0.30f, vignetteSmooth = 0.55f, filter = new Color(1f, 0.99f, 0.96f), bloomTint = new Color(1f, 0.88f, 0.72f), vignetteColor = new Color(0.30f, 0.16f, 0.1f) };
        public static readonly Mood Studio = new Mood { exposure = 0.1f, contrast = 6, saturation = 8, bloomThreshold = 0.8f, bloomIntensity = 0.55f, bloomScatter = 0.75f, vignette = 0.26f, vignetteSmooth = 0.55f, filter = new Color(1f, 0.97f, 0.95f), bloomTint = new Color(1f, 0.85f, 0.78f), vignetteColor = new Color(0.35f, 0.16f, 0.14f) };
        // dreamy pastel daytime (D-004): softer contrast, a touch of pink, lavender vignette, gentle glow on lamps and stars
        public static readonly Mood Village = new Mood { exposure = -0.05f, contrast = 9, saturation = 10, bloomThreshold = 0.9f, bloomIntensity = 0.45f, bloomScatter = 0.72f, vignette = 0.18f, vignetteSmooth = 0.6f, filter = new Color(1f, 0.97f, 0.99f), bloomTint = new Color(1f, 0.9f, 0.92f), vignetteColor = new Color(0.36f, 0.28f, 0.46f) };

        public static VolumeProfile Profile(string name, Mood m)
        {
            Directory.CreateDirectory(Dir);
            string path = Dir + name + ".asset";
            var vp = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (vp == null) { vp = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(vp, path); }
            // rebuild from scratch so tuning here is the single source of truth
            foreach (var c in vp.components.ToArray()) { vp.Remove(c.GetType()); Object.DestroyImmediate(c, true); }

            var tm = vp.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.Neutral);
            var ca = vp.Add<ColorAdjustments>(true);
            ca.postExposure.Override(m.exposure); ca.contrast.Override(m.contrast); ca.saturation.Override(m.saturation); ca.colorFilter.Override(m.filter);
            var bl = vp.Add<Bloom>(true);
            bl.threshold.Override(m.bloomThreshold); bl.intensity.Override(m.bloomIntensity); bl.scatter.Override(m.bloomScatter); bl.tint.Override(m.bloomTint); bl.highQualityFiltering.Override(true);
            var vg = vp.Add<Vignette>(true);
            vg.color.Override(m.vignetteColor); vg.intensity.Override(m.vignette); vg.smoothness.Override(m.vignetteSmooth); vg.rounded.Override(false);
            EditorUtility.SetDirty(vp);
            foreach (var c in vp.components) { c.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector; AssetDatabase.AddObjectToAsset(c, vp); }
            return vp;
        }

        /// <summary>Adds a global Volume to the active scene and enables post-processing + SMAA on its camera.</summary>
        public static void AddTo(Camera cam, string profileName, Mood mood)
        {
            var go = new GameObject("Look"); var vol = go.AddComponent<Volume>(); vol.isGlobal = true; vol.priority = 10;
            vol.sharedProfile = Profile(profileName, mood);
            var data = cam.gameObject.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
        }
    }
}

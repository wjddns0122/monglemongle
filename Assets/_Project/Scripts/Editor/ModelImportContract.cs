using UnityEditor;
using UnityEngine;

namespace Mongle.EditorTools
{
    /// <summary>
    /// Blender->Unity import contract (docs/20_decisions/C-001_Blender_Unity_연결계약.md).
    /// Applies to FBX under Assets/_Project/Art. Strips "Armature|" clip prefixes, sets loops by name.
    /// </summary>
    public class ModelImportContract : AssetPostprocessor
    {
        const string Root = "Assets/_Project/Art/";

        // Clips that must loop (Blender action names).
        static readonly string[] LoopNames = { "Idle", "Walk", "SitIdle", "Nervous", "Talk", "Wave", "PIP_Idle" };

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root) || !assetPath.EndsWith(".fbx")) return;
            var imp = (ModelImporter)assetImporter;
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.importBlendShapes = true;
            imp.importCameras = false;
            imp.importLights = false;
            imp.optimizeGameObjects = false; // keep AP/DP/CA nodes under bones
            imp.isReadable = true;           // BakeMesh (thorn proxy, surface probe) returns empty meshes in a player build when the source mesh is not readable
        }

        // Blender bakes constant shape-key tracks into every clip; they would overwrite gameplay-driven blend shapes
        // (Deflated/Smile/Worried/Closed) every frame, so they are stripped on import (C-001).
        void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            if (!assetPath.StartsWith(Root) || !assetPath.EndsWith(".fbx")) return;
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
                if (b.propertyName.StartsWith("blendShape.")) AnimationUtility.SetEditorCurve(clip, b, null);
        }

        // D-009 patchwork textures: RGB = fabric colour (sRGB), A = stitch height (not transparency)
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root + "Toys/Patch/") || !assetPath.EndsWith(".png")) return;
            ConfigurePatchTexture((TextureImporter)assetImporter);
        }

        /// <summary>Patch texture import settings; true when something had to change (caller reimports).</summary>
        public static bool ConfigurePatchTexture(TextureImporter ti)
        {
            bool ok = ti.textureType == TextureImporterType.Default && ti.sRGBTexture && ti.alphaSource == TextureImporterAlphaSource.FromInput
                && !ti.alphaIsTransparency && ti.mipmapEnabled && ti.maxTextureSize >= 2048 && ti.textureCompression == TextureImporterCompression.CompressedHQ
                && ti.wrapMode == TextureWrapMode.Repeat && ti.anisoLevel >= 4;
            if (ok) return false;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = false; // alpha is the stitch height: no colour dilation into "transparent" texels
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.anisoLevel = 4;
            return true;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Root) || !assetPath.EndsWith(".fbx")) return;
            var imp = (ModelImporter)assetImporter;
            var defaults = imp.defaultClipAnimations;
            if (defaults == null || defaults.Length == 0) return;
            var list = new System.Collections.Generic.List<ModelImporterClipAnimation>();
            foreach (var c in defaults)
            {
                if (c.name.StartsWith("__preview__")) continue;
                int bar = c.name.IndexOf('|');
                string clean = bar >= 0 ? c.name.Substring(bar + 1) : c.name;
                c.name = clean;
                c.loopTime = System.Array.IndexOf(LoopNames, clean) >= 0;
                c.loopPose = c.loopTime;
                list.Add(c);
            }
            imp.clipAnimations = list.ToArray();
        }
    }
}

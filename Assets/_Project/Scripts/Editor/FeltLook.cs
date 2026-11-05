using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mongle.EditorTools
{
    /// <summary>
    /// Felt shader presets by material name. ContentBuilder/SceneBuilder call TryApply after creating a material,
    /// so rebuilding content never reverts the look back to plain Lit.
    /// </summary>
    public static class FeltLook
    {
        public const string ShaderName = "Mongle/Felt";
        const string MatDir = "Assets/_Project/Art/Materials/";

        struct Preset
        {
            public float scale, normal, tone, speck, rim, sub, smooth; public bool world;
            public Color rimColor, subColor;
        }

        static readonly Color WarmRim = new Color(1f, 0.9f, 0.78f);
        static readonly Color Blush = new Color(1f, 0.7f, 0.6f);

        static Preset Toy(float rim = 0.26f, float sub = 0.22f) => new Preset { scale = 150, normal = 0.26f, tone = 0.10f, speck = 0.05f, rim = rim, sub = sub, smooth = 0.05f, rimColor = WarmRim, subColor = Blush };
        static Preset Prop(float scale = 90, float rim = 0.2f) => new Preset { scale = scale, normal = 0.26f, tone = 0.08f, speck = 0.04f, rim = rim, sub = 0.2f, smooth = 0.05f, rimColor = WarmRim, subColor = Blush };
        static Preset World(float scale = 30, float normal = 0.4f, float tone = 0.07f, float rim = 0.12f) => new Preset { scale = scale, normal = normal, tone = tone, speck = 0.04f, rim = rim, sub = 0.18f, smooth = 0.04f, rimColor = WarmRim, subColor = Blush, world = true };

        static readonly Dictionary<string, Preset> Presets = new Dictionary<string, Preset>
        {
            // Mongi + accessories (object space so the fuzz travels with the toy)
            { "M_Mongi_Brown", Toy() }, { "M_Mongi_Cream", Toy(0.3f, 0.2f) }, { "M_Mongi_Dark", Toy(0.14f, 0.15f) },
            { "M_TearInside", Toy(0.2f, 0.2f) },
            { "M_Ribbon_Red", Prop(160) }, { "M_Hat_Blue", Prop(160) },
            // diorama surfaces (world space, soft)
            { "M_Grass", World(26, 0.36f, 0.07f, 0.1f) }, { "M_Path", World(34) }, { "M_WorkMat", World(60, 0.45f, 0.06f, 0.1f) },
            { "M_Wall", World(22, 0.25f, 0.04f, 0.05f) }, { "M_WallAccent", World(22, 0.25f, 0.04f, 0.05f) },
            { "M_StudioFloor", World(40, 0.35f, 0.05f, 0.08f) }, { "M_BackdropPeach", World(26, 0.22f, 0.04f, 0.04f) }, { "M_BackdropMint", World(26, 0.22f, 0.04f, 0.04f) },
            { "M_TreeLeaf", World(40, 0.5f, 0.1f, 0.2f) }, { "M_TreeLeaf2", World(40, 0.5f, 0.1f, 0.2f) }, { "M_Leaf", World(60, 0.45f, 0.08f, 0.18f) },
            { "M_Block0", World(70) }, { "M_Block1", World(70) }, { "M_Block2", World(70) }, { "M_Block3", World(70) }, { "M_Block4", World(70) },
            { "M_Bloom0", World(90, 0.45f, 0.08f, 0.2f) }, { "M_Bloom1", World(90, 0.45f, 0.08f, 0.2f) }, { "M_Bloom2", World(90, 0.45f, 0.08f, 0.2f) }, { "M_Bloom3", World(90, 0.45f, 0.08f, 0.2f) },
            { "M_GrassOuter", World(22, 0.36f, 0.08f, 0.08f) }, { "M_GrassPatchA", World(26, 0.4f, 0.07f, 0.1f) }, { "M_GrassPatchB", World(26, 0.4f, 0.07f, 0.1f) },
            { "M_PathB", World(34) }, { "M_TreePine", World(40, 0.5f, 0.1f, 0.2f) }, { "M_Shrub", World(50, 0.5f, 0.1f, 0.2f) }, { "M_Tuft", World(80, 0.4f, 0.08f, 0.1f) }, { "M_Rock", World(40, 0.35f, 0.06f, 0.1f) },
            { "M_Rug", World(60, 0.4f, 0.06f, 0.1f) }, { "M_RugRing", World(60, 0.4f, 0.06f, 0.1f) }, { "M_Curtain", World(50, 0.35f, 0.06f, 0.12f) }, { "M_PlantLeaf", World(70, 0.4f, 0.08f, 0.2f) },
            { "M_FeltPink", World(80) }, { "M_FeltBlue", World(80) }, { "M_FeltYellow", World(80) }, { "M_FeltMint", World(80) }, { "M_Cream", World(40, 0.25f, 0.04f, 0.08f) }, { "M_Wainscot", World(30, 0.25f, 0.04f, 0.06f) },
            { "M_Wild0", World(90) }, { "M_Wild1", World(90) }, { "M_Wild2", World(90) },
        };

        // hard toys (D-009): matte pastel clay, no fuzz
        static Preset Clay(float smooth = 0.18f) => new Preset { scale = 150, normal = 0.05f, tone = 0.03f, speck = 0f, rim = 0.22f, sub = 0.25f, smooth = smooth, rimColor = WarmRim, subColor = Blush };

        public static bool TryApply(Material m)
        {
            if (m == null) return false;
            Preset p;
            if (!Presets.TryGetValue(m.name, out p))
            {
                int us = m.name.IndexOf('_');
                if (m.name.StartsWith("TOY_")) p = Clay(m.name.Contains("Glass") ? 0.55f : 0.18f); // robot, cars, plane, pawn (D-009)
                else if (m.name.StartsWith("MF") && us > 2 && int.TryParse(m.name.Substring(2, us - 2), out int fz)) p = Prop(fz, 0.22f); // imported model: fuzz scale chosen for its import scale
                else if (m.name.StartsWith("MP_")) p = Prop(110, 0.22f);        // movable prop: fuzz sticks to the object
                else if (m.name.StartsWith("MC_")) p = World(40, 0.5f, 0.1f, 0.2f); // candy scenery (cotton-candy trees, clouds): big soft fuzz
                else if (m.name.StartsWith("MS_")) p = World(60, 0.35f, 0.06f, 0.1f); // static scene part: world-space fuzz
                else return false;
            }
            var sh = Shader.Find(ShaderName);
            if (sh == null) return false;
            var color = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
            m.shader = sh;
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", p.smooth);
            m.SetFloat("_FuzzScale", p.scale); m.SetFloat("_FuzzNormal", p.normal); m.SetFloat("_FuzzTone", p.tone); m.SetFloat("_FuzzSpeck", p.speck);
            m.SetFloat("_WorldSpace", p.world ? 1f : 0f);
            m.SetColor("_RimColor", p.rimColor); m.SetFloat("_RimPower", 2.4f); m.SetFloat("_RimStrength", p.rim);
            m.SetFloat("_Subsurface", p.sub); m.SetColor("_SubsurfaceColor", p.subColor);
            if (m.name.StartsWith("TOY_") && m.name.Contains("Metal")) m.SetFloat("_Metallic", 0f); // pastel matte, not chrome
            if (!m.name.Contains("Glow") && m.name.StartsWith("TOY_")) m.SetColor("_EmissionColor", Color.black); // only lamps glow
            if (!ApplyPatch(m)) { m.SetFloat("_UseBaseMap", 0f); m.SetFloat("_StitchBump", 0f); }
            EditorUtility.SetDirty(m);
            return true;
        }

        // ---------------- patchwork textures (D-009) ----------------
        public const string PatchDir = "Assets/_Project/Art/Toys/Patch/";
        public const float StitchBump = 0.6f;
        [System.Serializable] class PatchList { public string[] materials; }
        static Dictionary<string, string> patchOf; // material name -> texture path

        /// <summary>Material name -> T_Patch_&lt;Kind&gt;.png for every T_Patch_&lt;Kind&gt;.json whose texture exists.</summary>
        static Dictionary<string, string> Patches
        {
            get
            {
                if (patchOf != null) return patchOf;
                patchOf = new Dictionary<string, string>();
                if (!System.IO.Directory.Exists(PatchDir)) return patchOf;
                foreach (var json in System.IO.Directory.GetFiles(PatchDir, "T_Patch_*.json"))
                {
                    var png = json.Replace('\\', '/').Replace(".json", ".png");
                    if (!System.IO.File.Exists(png)) continue;
                    PatchList list = null;
                    try { list = JsonUtility.FromJson<PatchList>(System.IO.File.ReadAllText(json)); } catch (System.Exception e) { Debug.LogWarning("[FeltLook] bad " + json + ": " + e.Message); }
                    if (list?.materials == null) continue;
                    foreach (var n in list.materials) if (!string.IsNullOrEmpty(n)) patchOf[n] = png;
                }
                return patchOf;
            }
        }

        /// <summary>True when the material is drawn with a patchwork texture (its colour then comes from the texture).</summary>
        public static bool IsPatched(string materialName) => Patches.ContainsKey(materialName);

        static bool ApplyPatch(Material m)
        {
            if (!Patches.TryGetValue(m.name, out var png)) return false;
            var tex = PatchTexture(png);
            if (tex == null) return false;
            m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", Vector2.one); m.SetTextureOffset("_BaseMap", Vector2.zero);
            m.SetFloat("_UseBaseMap", 1f);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_StitchBump", StitchBump);
            m.SetFloat("_FuzzTone", 0.06f); // the fabric print already varies
            if (m.name.Contains("Dark")) m.SetFloat("_Smoothness", 0.45f); // shiny button eyes
            return true;
        }

        /// <summary>Colour texture (sRGB) whose alpha is a height map: no transparency dilation, mipmapped, full 2048.</summary>
        static Texture2D PatchTexture(string png)
        {
            if (AssetImporter.GetAtPath(png) is TextureImporter ti && ModelImportContract.ConfigurePatchTexture(ti)) ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(png);
        }

        [MenuItem("Mongle/Apply Felt Look")]
        public static void ApplyAll()
        {
            patchOf = null; // re-read the patch lists
            int n = 0;
            var names = new HashSet<string>(Presets.Keys);
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MatDir.TrimEnd('/') }))
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                if (name.StartsWith("TOY_") || IsPatched(name)) names.Add(name);
            }
            foreach (var name in names)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(MatDir + name + ".mat");
                if (m != null && TryApply(m)) n++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[FeltLook] applied to " + n + " materials (" + Patches.Count + " with patch textures)");
        }
    }
}

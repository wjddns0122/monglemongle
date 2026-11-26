using System.Collections.Generic;
using System.IO;
using Mongle.UI2;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Mongle.EditorTools
{
    /// <summary>
    /// D-038 text, 1:1 with Figma: every label is a TextMeshPro text placed from the Figma text node itself
    /// (docs/40_reports/shots/figma_v2/texts.tsv, made by texts.py): same top-left, size, colour and tracking, line box
    /// 1.3 em ("leading normal" of Cafe24 Ssurround: ascender 0.92 + descender 0.38). TextMeshPro lays glyphs out with the
    /// font's own float advances like Figma does (legacy Text rounds every advance to a whole pixel).
    /// </summary>
    public static class UiV2Text
    {
        public const string TextsPath = "docs/40_reports/shots/figma_v2/texts.tsv";
        public const string FontPath = "Assets/_Project/Resources/UI/Fonts/Cafe24Ssurround.ttf";
        public const string TmpFontPath = "Assets/_Project/Resources/UI2/Fonts/Cafe24Ssurround SDF.asset";

        public struct FT { public float x, y, w, h, size, track; public Color color; public string text; }
        static Dictionary<string, FT> texts;
        static TextMeshProUGUI measurer;
        public static TMP_FontAsset Font { get; private set; }

        /// <summary>TextMeshPro's shaders + settings (TMP Essential Resources, shipped inside com.unity.ugui) present?</summary>
        public static bool EssentialsReady => Shader.Find("TextMeshPro/Mobile/Distance Field") != null && Resources.Load<TMP_Settings>("TMP Settings") != null;

        public static void ImportEssentials()
        {
            if (EssentialsReady) return;
            var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui");
            UnityEditor.AssetPackage.Package.Import(Path.Combine(pkg.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage"), false);
        }

        /// <summary>Loads the Figma text table and the Cafe24 SDF font asset (made once, dynamic: new glyphs are added as needed).</summary>
        public static void Init()
        {
            texts = new Dictionary<string, FT>();
            foreach (var line in File.ReadAllLines(TextsPath))
            {
                if (line.StartsWith("#") || line.Trim().Length == 0) continue;
                var c = line.Split('\t');
                ColorUtility.TryParseHtmlString(c[7], out var col);
                texts[c[0] + "/" + c[1]] = new FT
                {
                    x = F(c[2]), y = F(c[3]), w = F(c[4]), h = F(c[5]), size = F(c[6]), color = col,
                    track = F(c[8]), text = c[9].Replace("\\n", "\n"),
                };
            }
            Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
            if (Font == null) Font = MakeFont();
            if (measurer == null)
            {
                var go = new GameObject("UiV2 measurer") { hideFlags = HideFlags.HideAndDontSave };
                measurer = go.AddComponent<TextMeshProUGUI>();
            }
            measurer.font = Font; measurer.textWrappingMode = TextWrappingModes.NoWrap; measurer.richText = false;
        }

        public static void Done()
        {
            if (measurer != null) Object.DestroyImmediate(measurer.gameObject);
            measurer = null;
            if (Font != null) { EditorUtility.SetDirty(Font); AssetDatabase.SaveAssets(); }
        }

        static float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

        static TMP_FontAsset MakeFont()
        {
            var src = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            var fa = TMP_FontAsset.CreateFontAsset(src, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            Directory.CreateDirectory(Path.GetDirectoryName(TmpFontPath));
            AssetDatabase.CreateAsset(fa, TmpFontPath);
            fa.atlasTextures[0].name = "Cafe24Ssurround Atlas"; AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = "Cafe24Ssurround Material"; AssetDatabase.AddObjectToAsset(fa.material, fa);
            // every letter the game says today (both languages), so the atlas is ready before the first frame
            var all = new HashSet<char>();
            foreach (var l in File.ReadAllLines("Assets/_Project/Resources/Loc/strings.txt")) foreach (var ch in l) if (ch >= 32) all.Add(ch);
            foreach (var t in File.ReadAllLines(TextsPath)) foreach (var ch in t) if (ch >= 32) all.Add(ch);
            for (char ch = ' '; ch <= '~'; ch++) all.Add(ch);
            fa.TryAddCharacters(new string(new List<char>(all).ToArray()));
            EditorUtility.SetDirty(fa); AssetDatabase.SaveAssets();
            return fa;
        }

        public static FT Get(string frame, string id)
        {
            if (!texts.TryGetValue(frame + "/" + id, out var t)) throw new System.Exception("[UiV2] no Figma text " + frame + "/" + id);
            return t;
        }

        public static float Measure(string s, float size, float track)
        {
            measurer.fontSize = size; measurer.characterSpacing = track / size * 100f;
            return measurer.GetPreferredValues(s).x;
        }

        /// <summary>Sets up a TMP label like Figma draws it (font, size, colour, tracking, no rich text, no raycast).</summary>
        public static TextMeshProUGUI Style(GameObject go, float size, Color color, float track = 0f)
        {
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = Font; t.fontSize = size; t.color = color; t.characterSpacing = track / size * 100f;
            t.raycastTarget = false; t.richText = false; t.overflowMode = TextOverflowModes.Overflow;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.extraPadding = true;
            return t;
        }
    }
}

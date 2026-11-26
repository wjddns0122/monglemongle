using UnityEditor;
using UnityEngine;

namespace Mongle.EditorTools
{
    /// <summary>
    /// Figma UI sprites (D-019, Resources/UI): plain sprites, no mipmaps, no compression (the thin stitch lines would smear).
    /// </summary>
    public class UiSpriteImporter : AssetPostprocessor
    {
        // the title font keeps the OS font behind it for ✓ ♥ ★ ◀ ▶ × and the like
        void OnPreprocessAsset()
        {
            if (!assetPath.EndsWith("/Resources/UI/Fonts/Cafe24Ssurround.ttf")) return;
            var fi = assetImporter as TrueTypeFontImporter;
            if (fi != null) fi.fontNames = new[] { "Apple SD Gothic Neo", "AppleGothic", "Noto Sans CJK KR", "Malgun Gothic", "Arial" };
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/UI/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 4096;
            ti.spritePixelsPerUnit = 100f;
            ti.wrapMode = TextureWrapMode.Clamp;
            // sprites that grow with their content are 9-sliced (textures are 2x, so the borders are doubled; UI images use pixelsPerUnitMultiplier = 2)
            var name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            // the speech plate: its tail sits in the bottom-left corner, so that corner keeps more room
            if (name == "shop_plate_left") ti.spriteBorder = new Vector4(84f, 64f, 56f, 48f);
            // the friends' speech bubble (Figma 58:1332): a white pill, its ends full half-circles; the tail is its own picture
            else if (name == "speech_body") ti.spriteBorder = new Vector4(97f, 97f, 97f, 97f);
            else if (name == "dlg_bubble") ti.spriteBorder = new Vector4(170f, 170f, 170f, 170f);
            else if (name == "dlg_tag") ti.spriteBorder = new Vector4(166f, 0f, 166f, 0f);
            else if (name == "dlg_choice") ti.spriteBorder = new Vector4(240f, 240f, 240f, 240f);
            // the rounded rect of UiKit.Round saved as a picture (D-035): 22 px corners
            else if (name == "kit_round") ti.spriteBorder = new Vector4(24f, 24f, 24f, 24f);
            // everything else: the border build.py measured (transparent margin + corner radius), so any sprite can be 9-sliced
            else if (Mongle.UI.FigmaLayout.Borders.TryGetValue(name, out var b)) ti.spriteBorder = b;
        }
    }
}

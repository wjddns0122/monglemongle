using Mongle.UI2;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.EditorTools
{
    /// <summary>
    /// Building blocks for the 11-26 screens (Figma page "새 디자인 SVG 11-26", 88:2). Places are given as in Figma: x, y from the
    /// top-left of a 1920x1080 frame (or of the parent piece), and each piece is pinned to the corner / edge named by its anchor so
    /// it stays there on every screen shape. Pictures are the Figma vectors rendered at 2x (work/figma_1126/tools/compose.py) and
    /// kept in <see cref="ArtDir"/>; words are TextMeshPro in Cafe24 Ssurround.
    /// </summary>
    public static class ViewKit
    {
        public const string ArtDir = "Assets/_Project/Art/UI_1126";
        public const string FontPath = "Assets/_Project/Resources/UI2/Fonts/Cafe24Ssurround SDF.asset";
        public static readonly Color Brown = Hex("#7A594D");

        public static readonly Vector2 TL = new Vector2(0, 1), T = new Vector2(0.5f, 1), TR = new Vector2(1, 1),
            L = new Vector2(0, 0.5f), C = new Vector2(0.5f, 0.5f), R = new Vector2(1, 0.5f),
            BL = new Vector2(0, 0), B = new Vector2(0.5f, 0), BR = new Vector2(1, 0);

        public static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        /// <summary>A screen's root: its own overlay canvas, scaled like every 11-26 screen (1920x1080, <see cref="AdaptiveScaler"/>).</summary>
        public static RectTransform Root(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = 5;
            var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = order;
            var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<AdaptiveScaler>();
            return (RectTransform)go.transform;
        }

        /// <summary>An empty piece at Figma place (x, y, w, h) inside a parent of size <paramref name="parentSize"/>, pinned to <paramref name="anchor"/>.</summary>
        public static RectTransform Node(Transform parent, string name, float x, float y, float w, float h, Vector2 anchor, Vector2? parentSize = null)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Place(rt, x, y, w, h, anchor, parentSize ?? new Vector2(1920, 1080));
            return rt;
        }

        public static void Place(RectTransform rt, float x, float y, float w, float h, Vector2 anchor, Vector2 parentSize)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x + anchor.x * w - anchor.x * parentSize.x, -(y + (1f - anchor.y) * h) + (1f - anchor.y) * parentSize.y);
        }

        public static Sprite Art(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/" + file);

        /// <summary>The TMP line spacing that puts two lines <paramref name="pitch"/> apart (Figma's line height).</summary>
        public static float LineSpacingFor(TMP_Text t, float pitch)
        {
            string keep = t.text; t.lineSpacing = 0f;
            t.text = "가\n가"; t.ForceMeshUpdate();
            float p0 = t.textInfo.lineCount > 1 ? t.textInfo.lineInfo[0].baseline - t.textInfo.lineInfo[1].baseline : pitch;
            t.text = keep;
            return (pitch - p0) / (t.fontSize * 0.01f);
        }

        public static Image Picture(Transform parent, string name, string art, float x, float y, float w, float h, Vector2 anchor, Vector2? parentSize = null, bool sliced = false)
        {
            var img = Node(parent, name, x, y, w, h, anchor, parentSize).gameObject.AddComponent<Image>();
            img.sprite = Art(art); img.raycastTarget = false;
            if (sliced) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f; }
            else img.preserveAspect = true;
            if (img.sprite == null) Debug.LogWarning("[ViewKit] missing art " + art);
            return img;
        }

        /// <summary>A picture that fills the whole screen behind everything, cut (not squashed) to the screen's shape.</summary>
        public static Image Backdrop(Transform parent, string name, string art)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            go.transform.SetParent(parent, false); go.transform.SetAsFirstSibling();
            var rt = (RectTransform)go.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>(); img.sprite = Art(art); img.raycastTarget = false;
            if (img.sprite == null) { Debug.LogWarning("[ViewKit] missing art " + art); return img; }
            var fit = go.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio = img.sprite.rect.width / img.sprite.rect.height;
            return img;
        }

        /// <summary>A picture that is pressed: a button with a little squash (<see cref="PressScale"/>).</summary>
        public static Button PictureButton(Transform parent, string name, string art, float x, float y, float w, float h, Vector2 anchor, Vector2? parentSize = null)
        {
            var img = Picture(parent, name, art, x, y, w, h, anchor, parentSize);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = img;
            img.gameObject.AddComponent<PressScale>();
            return b;
        }

        /// <summary>Words in Cafe24 Ssurround; <paramref name="key"/> (Resources/Loc/strings.txt) keeps them in the chosen language.</summary>
        public static TextMeshProUGUI Words(Transform parent, string name, string text, float size, Color color, float x, float y, float w, float h, Vector2 anchor,
            HorizontalAlignmentOptions align, string key = null, Vector2? parentSize = null)
        {
            var t = Node(parent, name, x, y, w, h, anchor, parentSize).gameObject.AddComponent<TextMeshProUGUI>();
            t.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            t.text = text; t.fontSize = size; t.color = color; t.raycastTarget = false;
            t.horizontalAlignment = align; t.verticalAlignment = VerticalAlignmentOptions.Middle;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow;
            if (!string.IsNullOrEmpty(key)) t.gameObject.AddComponent<LocText>().key = key;
            return t;
        }

        /// <summary>A rounded shape drawn as a mesh (<see cref="SoftRect"/>): Figma fills, strokes and stitches that stay crisp at any size.
        /// Radii are top-left, top-right, bottom-right, bottom-left; the border is drawn inside the box.</summary>
        public static SoftRect Shape(GameObject go, Color fill, Vector4 radius, float border = 0f, Color? borderColor = null)
        {
            var s = go.AddComponent<SoftRect>();
            s.color = fill; s.radius = radius; s.borderWidth = border; s.raycastTarget = false;
            if (borderColor.HasValue) s.borderColor = borderColor.Value;
            return s;
        }

        /// <summary>Lists <paramref name="prefab"/> as <paramref name="name"/> in Resources/Views/ViewIndex (what the game opens on demand).</summary>
        public static void Register(string name, GameObject prefab)
        {
            const string path = "Assets/_Project/Resources/Views/ViewIndex.asset";
            var index = AssetDatabase.LoadAssetAtPath<Mongle.Views.ViewIndex>(path);
            if (index == null)
            {
                System.IO.Directory.CreateDirectory("Assets/_Project/Resources/Views");
                index = ScriptableObject.CreateInstance<Mongle.Views.ViewIndex>();
                AssetDatabase.CreateAsset(index, path);
            }
            var list = new System.Collections.Generic.List<Mongle.Views.ViewIndex.Entry>(index.screens);
            list.RemoveAll(e => e.name == name);
            list.Add(new Mongle.Views.ViewIndex.Entry { name = name, prefab = prefab });
            index.screens = list.ToArray();
            EditorUtility.SetDirty(index); AssetDatabase.SaveAssetIfDirty(index);
        }

        /// <summary>A clear box that takes clicks (the hit area of words-only buttons).</summary>
        public static Image HitArea(GameObject go)
        {
            var img = go.AddComponent<Image>(); img.color = new Color(1f, 1f, 1f, 0f); img.raycastTarget = true;
            return img;
        }

        public static void IgnoreLayout(Component c) { var le = c.gameObject.AddComponent<LayoutElement>(); le.ignoreLayout = true; }
    }
}

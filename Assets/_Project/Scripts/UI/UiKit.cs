using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Mongle.UI
{
    /// <summary>Small runtime UI toolkit (uGUI). Uses an OS font so Korean text renders on macOS.</summary>
    public static class UiKit
    {
        public static readonly Color Cream = new Color(1f, 0.957f, 0.878f, 1f);
        public static readonly Color Paper = new Color(1f, 0.98f, 0.93f, 0.97f);
        public static readonly Color Brown = new Color(0.43f, 0.29f, 0.19f, 1f);
        public static readonly Color Peach = new Color(1f, 0.71f, 0.54f, 1f);
        public static readonly Color Mint = new Color(0.66f, 0.86f, 0.78f, 1f);
        public static readonly Color Pink = new Color(1f, 0.62f, 0.71f, 1f);
        public static readonly Color Sky = new Color(0.61f, 0.83f, 0.96f, 1f);
        public static readonly Color Shade = new Color(0.25f, 0.17f, 0.12f, 0.55f);

        static Font font, titleFont;
        static Sprite round;
        static readonly System.Collections.Generic.Dictionary<string, Sprite> uiSprites = new System.Collections.Generic.Dictionary<string, Sprite>();

        /// <summary>The Figma title font (Cafe24 Ssurround, D-019); falls back to the OS font when the file is missing.</summary>
        public static Font TitleFont
        {
            get
            {
                if (titleFont == null) titleFont = Resources.Load<Font>("UI/Fonts/Cafe24Ssurround");
                return titleFont != null ? titleFont : OsFont;
            }
        }

        /// <summary>A sprite exported from the Figma design (Resources/UI/Diagnosis/&lt;name&gt;), or null.</summary>
        public static Sprite UiSprite(string name)
        {
            if (!uiSprites.TryGetValue(name, out var sp) || sp == null) uiSprites[name] = sp = Resources.Load<Sprite>("UI/" + name) ?? Resources.Load<Sprite>("UI/Diagnosis/" + name);
            return sp;
        }

        public enum Edge { Start = 0, Middle = 1, End = 2 }

        /// <summary>
        /// A Figma sprite at its place on the 1920x1080 artboard (FigmaLayout). The edges say what the position hangs from, so it
        /// keeps its distance to that screen edge on other aspect ratios: Start = left/top, Middle = centre, End = right/bottom.
        /// dx/dy nudge it (artboard px, y down).
        /// </summary>
        public static Image Fig(Transform parent, string sprite, Edge h = Edge.Start, Edge v = Edge.Start, float dx = 0f, float dy = 0f, bool raycast = false, string name = null)
        {
            var r = FigmaLayout.Rects[sprite];
            var img = FigRect(parent, name ?? sprite, r, h, v, dx, dy);
            img.sprite = UiSprite(sprite); img.raycastTarget = raycast; img.preserveAspect = false;
            return img;
        }

        /// <summary>An empty Image rect for a region of the artboard (see Fig).</summary>
        public static Image FigRect(Transform parent, string name, Rect r, Edge h, Edge v, float dx = 0f, float dy = 0f)
        {
            float ax = (int)h * 0.5f, ay = (int)v * 0.5f;
            var rt = Rect(parent, name, new Vector2(ax, 1f - ay), new Vector2(ax, 1f - ay), Vector2.zero, Vector2.zero);
            rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(r.width, r.height);
            rt.anchoredPosition = new Vector2(r.center.x + dx - ax * 1920f, -(r.center.y + dy - ay * 1080f));
            return rt.gameObject.AddComponent<Image>();
        }

        /// <summary>A Figma sprite that is a button.</summary>
        public static Button FigButton(Transform parent, string sprite, Action onClick, Edge h = Edge.Start, Edge v = Edge.Start, float dx = 0f, float dy = 0f, string name = null)
        {
            var img = Fig(parent, sprite, h, v, dx, dy, true, name);
            var b = img.gameObject.AddComponent<Button>(); b.targetGraphic = img;
            var cb = b.colors; cb.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f); cb.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f); cb.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
            b.colors = cb;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            return b;
        }

        /// <summary>Title-font text centred on a point of the parent image (artboard px offset from its centre, y down); the label never blocks clicks.</summary>
        public static Text FigText(Transform parent, string text, int size, Color color, float cx = 0f, float cy = 0f, float w = 600f, float hgt = 120f, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var t = TitleLabel(parent, text, size, color, new Vector2(0.5f, 0.5f), new Vector2(cx, -cy), new Vector2(w, hgt), align);
            return t;
        }

        /// <summary>A Figma sprite placed inside another one (the parent's artboard rect is the frame of reference).</summary>
        public static Image FigIn(Transform parent, string sprite, string parentSprite, bool raycast = false, string name = null) => FigInRect(parent, name ?? sprite, FigmaLayout.Rects[sprite], parentSprite, UiSprite(sprite), raycast);

        public static Image FigInRect(Transform parent, string name, Rect r, string parentSprite, Sprite sp, bool raycast = false)
        {
            var pc = FigmaLayout.Rects[parentSprite].center;
            var rt = Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(r.width, r.height); rt.anchoredPosition = new Vector2(r.center.x - pc.x, -(r.center.y - pc.y));
            var img = rt.gameObject.AddComponent<Image>(); img.sprite = sp; img.raycastTarget = raycast; img.preserveAspect = false;
            return img;
        }

        /// <summary>Title-font text whose centre is at an artboard point (px), inside a placed Figma sprite.</summary>
        public static Text FigTextAt(Transform parent, string parentSprite, string text, int size, Color color, float artCx, float artCy, float w = 800f, float h = 120f, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var pc = FigmaLayout.Rects[parentSprite].center;
            return FigText(parent, text, size, color, artCx - pc.x, artCy - pc.y, w, h, align);
        }

        /// <summary>Title-font text centred on an artboard point (px) of a full-screen parent, hanging from the screen centre.</summary>
        public static Text FigLabel(Transform parent, string text, int size, Color color, float artCx, float artCy, float w = 800f, float h = 100f, TextAnchor align = TextAnchor.MiddleCenter)
            => FigText(parent, text, size, color, artCx - 960f, artCy - 540f, w, h, align);

        /// <summary>A full-screen transparent container (children use artboard coordinates through Fig/FigLabel).</summary>
        public static RectTransform FigRoot(Transform parent, string name)
        {
            var rt = Rect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return rt;
        }

        /// <summary>An invisible label on an icon-only button, so tests (and screen readers) can still find it by its name.</summary>
        public static void HiddenLabel(Button b, string text)
        {
            var t = Label(b.transform, text, 24, new Color(0, 0, 0, 0), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 40));
            t.name = "HiddenLabel";
        }

        public static readonly Color FigBrown = new Color32(0x7a, 0x59, 0x4d, 255), FigBrown2 = new Color32(0x81, 0x62, 0x53, 255);

        /// <summary>Image drawn from a Figma sprite at a fixed pixel size (reference 1920x1080 units); anchor/pivot decide where it hangs.</summary>
        public static Image SpriteImage(Transform parent, string name, string sprite, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, bool raycast = false)
        {
            var rt = Rect(parent, name, anchor, anchor, Vector2.zero, Vector2.zero);
            rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UiSprite(sprite); img.raycastTarget = raycast; img.preserveAspect = false;
            return img;
        }

        /// <summary>A button that is a Figma sprite (hover/press tint like the other buttons).</summary>
        public static Button SpriteButton(Transform parent, string name, string sprite, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Action onClick)
        {
            var img = SpriteImage(parent, name, sprite, anchor, pivot, pos, size, true);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors; cb.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f); cb.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f); cb.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
            b.colors = cb;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            return b;
        }

        /// <summary>Text in the Figma title font, laid out in a box at an anchor (pivot centre unless given).</summary>
        public static Text TitleLabel(Transform parent, string text, int size, Color color, Vector2 anchor, Vector2 pos, Vector2 box, TextAnchor align = TextAnchor.MiddleCenter, Vector2? pivot = null)
        {
            var t = Label(parent, text, size, color, anchor, pos, box, align, FontStyle.Normal);
            t.font = TitleFont; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (pivot.HasValue) { t.rectTransform.pivot = pivot.Value; t.rectTransform.anchoredPosition = pos; }
            return t;
        }

        /// <summary>The OS font (Korean text on macOS/Windows); fallback of the title font.</summary>
        public static Font OsFont
        {
            get
            {
                if (font == null)
                    font = Font.CreateDynamicFontFromOSFont(new[] { "Apple SD Gothic Neo", "AppleGothic", "Noto Sans CJK KR", "Malgun Gothic", "Arial" }, 32);
                return font;
            }
        }

        /// <summary>The game's UI font: Cafe24 Ssurround (Figma, D-020) with the OS font behind it for symbols it lacks.</summary>
        public static Font Font => TitleFont;

        public static Sprite Round
        {
            get
            {
                if (round != null) return round;
                // D-035: the same rounded rect saved as a picture (Resources/UI/kit_round), so screens saved as prefabs keep it
                round = UiSprite("kit_round"); if (round != null) return round;
                const int s = 64; const float r = 22f;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float dx = Mathf.Max(r - x - 0.5f, 0, x + 0.5f - (s - r));
                        float dy = Mathf.Max(r - y - 0.5f, 0, y + 0.5f - (s - r));
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(r - d + 0.5f);
                        tex.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                tex.Apply();
                round = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));
                return round;
            }
        }

        public static Canvas CreateCanvas(string name, int sortOrder, Transform parent = null)
        {
            EnsureEventSystem();
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = sortOrder;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sc.matchWidthOrHeight = 0.5f;
            return c;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(es);
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
            return rt;
        }

        /// <summary>Anchored rect with a fixed pixel size around an anchor point.</summary>
        public static RectTransform Fixed(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = Rect(parent, name, anchor, anchor, Vector2.zero, Vector2.zero);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        /// <summary>
        /// A panel. Big ones are the Figma sheet (D-021): a rounded fill (tinted with <paramref name="color"/>) with a dashed stitch inside
        /// (Figma blue on cream/paper colours, white on coloured ones); small ones (badges, icons) stay plain rounded rects.
        /// Both grow freely (9-sliced), so callers may resize them.
        /// </summary>
        public static Image Panel(Transform parent, string name, Color color, Vector2 anchor, Vector2 pos, Vector2 size, bool raycast = true)
        {
            var rt = Fixed(parent, name, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = raycast;
            var sheet = size.x >= 160f && size.y >= 70f ? UiSprite("kit_sheet") : null;
            if (sheet == null) { img.sprite = Round; img.type = Image.Type.Sliced; img.color = color; return img; }
            bool paper = IsPaper(color);
            img.sprite = sheet; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 2f;
            img.color = paper ? new Color(0.976f, 0.929f, 0.867f, color.a) : color;   // the Figma cream #F9EDDD
            Stitch(img, false, paper ? new Color(0.784f, 0.882f, 0.918f, 0.87f) : new Color(1f, 1f, 1f, 0.87f));
            return img;
        }

        /// <summary>Cream/paper-like colours get the Figma cream sheet with its blue stitch.</summary>
        static bool IsPaper(Color c) => c.r > 0.95f && c.g > 0.88f && c.b > 0.74f;

        /// <summary>
        /// The dashed stitch inside a kit sheet or pill, over its fill and never catching clicks. It is drawn as a shape
        /// (<see cref="Mongle.UI2.SoftRect"/>) like the Figma screens' stitches, not as a 9-sliced picture: slicing stretched the dashes
        /// on the long sides and broke them where a pill's round ends meet its straight edges ("닫기" and other wide buttons).
        /// Drawn, every dash keeps one length and spacing at any size. Measures are the kit pictures' (kit_pill_stitch:
        /// inset 24, line 7, dash 27, gap 14 px on a 233 px-high pill; kit_sheet_stitch: inset 26.5, line 2, dash 26, gap 14 px at 2x).
        /// </summary>
        static Mongle.UI2.SoftRect Stitch(Image fill, bool pill, Color dashes)
        {
            var rt = Rect(fill.transform, "Stitch", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            rt.SetAsFirstSibling();
            var st = rt.gameObject.AddComponent<Mongle.UI2.SoftRect>();
            st.raycastTarget = false;
            st.color = new Color(1f, 1f, 1f, 0f);   // no fill of its own: only the dashes
            float h = ((RectTransform)fill.transform).sizeDelta.y;
            float k = pill ? Mathf.Max(20f, h) * 0.98f / 233f : 0.5f;   // texture px → UI units (pills scale with their height)
            st.radius = pill ? new Vector4(999f, 999f, 999f, 999f) : new Vector4(28f, 28f, 28f, 28f);
            st.stitch = true; st.stitchColor = dashes;
            st.stitchInset = (pill ? 24f : 26.5f) * k;
            st.stitchWidth = pill ? 7f * k : 2f;
            st.dash = (pill ? 27f : 26f) * k; st.gap = 14f * k;
            st.roundCaps = pill;
            return st;
        }

        /// <summary>Removes the kit stitch from a panel whose sprite is replaced by a Figma one.</summary>
        public static void NoStitch(Image img)
        {
            var st = img.transform.Find("Stitch"); if (st == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(st.gameObject); else UnityEngine.Object.DestroyImmediate(st.gameObject);
        }

        /// <summary>Draws a Figma sprite 9-sliced at its design scale (textures are 2x); pill = round ends whatever the height.</summary>
        public static Image Slice(Image img, bool pill = false)
        {
            if (img == null || img.sprite == null) return img;
            img.type = Image.Type.Sliced;
            var b = img.sprite.border; float h = ((RectTransform)img.transform).sizeDelta.y;
            img.pixelsPerUnitMultiplier = pill && h > 1f && b.y + b.w > 0f ? (b.y + b.w) / (h * 0.98f) : 2f;
            foreach (Transform c in img.transform)
                if (c.name == "Stitch") { var st = c.GetComponent<Image>(); if (st != null) st.pixelsPerUnitMultiplier = img.pixelsPerUnitMultiplier; }
            return img;
        }

        /// <summary>Keeps an image as wide (and optionally as tall) as its text needs, never smaller than its design size (FitToText).</summary>
        public static FitToText AutoFit(Image bg, Text text, float minW, float padX, float maxW = 1860f, float minH = 0f, float padY = 0f)
        {
            if (!bg.TryGetComponent<FitToText>(out var f)) f = bg.gameObject.AddComponent<FitToText>();   // (?? would miss Unity's fake null)
            f.text = text; f.minW = minW; f.padX = padX; f.maxW = maxW; f.minH = minH; f.padY = padY; f.Apply();
            return f;
        }

        public static Text Label(Transform parent, string text, int size, Color color, Vector2 anchor, Vector2 pos, Vector2 boxSize, TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            var rt = Fixed(parent, "Label", anchor, pos, boxSize);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            t.fontStyle = t.font == titleFont ? FontStyle.Normal : style;   // Cafe24 Ssurround is already a bold face
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>
        /// A button. Big enough ones are the Figma pill (D-021): a rounded fill tinted with <paramref name="color"/>, a white dashed stitch
        /// and the title font; the label shrinks to fit instead of spilling out.
        /// </summary>
        public static Button MakeButton(Transform parent, string text, Color color, Vector2 anchor, Vector2 pos, Vector2 size, Action onClick, int fontSize = 34)
        {
            var rt = Fixed(parent, "Btn_" + text, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>(); img.raycastTarget = true; img.color = color;
            var pill = size.x >= 90f && size.y >= 44f ? UiSprite("kit_pill") : null;
            if (pill != null)
            {
                img.sprite = pill; Stitch(img, true, new Color(1f, 1f, 1f, 0.9f)); Slice(img, true);
            }
            else { img.sprite = Round; img.type = Image.Type.Sliced; }
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors; cb.highlightedColor = new Color(1.07f, 1.07f, 1.07f, 1f); cb.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f); cb.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
            b.colors = cb;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            var l = Label(img.transform, text, fontSize, FigBrown, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            // one line, centred; a long label shrinks (down to half size) instead of wrapping out of the pill
            float padX = Mathf.Min(28f, size.x * 0.12f), lineH = Mathf.Min(size.y, fontSize * 1.4f);
            l.rectTransform.anchorMin = new Vector2(0f, 0.5f); l.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            l.rectTransform.sizeDelta = new Vector2(-2f * padX, lineH); l.rectTransform.anchoredPosition = Vector2.zero;
            l.verticalOverflow = VerticalWrapMode.Truncate;
            l.resizeTextForBestFit = true; l.resizeTextMaxSize = fontSize; l.resizeTextMinSize = Mathf.Max(12, fontSize / 2);
            return b;
        }

        public static void SetButtonText(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>(); if (t != null) t.text = text;
        }
    }
}

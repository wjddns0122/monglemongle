using System.Collections.Generic;
using System.IO;
using Mongle.UI2;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Mongle.EditorTools
{
    /// <summary>
    /// D-038: builds the v2 UI screens from the Figma page "꽃 벡터 · 다운로드 11-23" as Unity prefabs
    /// (Prefabs/UI2/*.prefab) and the preview scene Scenes/UiPreview.unity. Coordinates are Figma frame pixels
    /// (1920x1080, y down); every element gets an anchor so the layout follows any screen (AdaptiveScaler).
    /// Shapes are SoftRect (resolution independent), texts are TextMeshPro labels placed from the Figma text nodes
    /// (<see cref="UiV2Text"/>, 1:1), pictures are the icons rasterized by docs/40_reports/shots/figma_v2/icons.py and the
    /// game's own item pictures (<see cref="ItemIconBaker"/>). Prefabs are the user's to edit afterwards: the menu only
    /// builds missing ones unless "다시 만들기" is chosen.
    /// </summary>
    public static class UiV2Builder
    {
        /// <summary>D-040: each screen lives in its scene's folder (<see cref="UiEditFolder.FrontPath"/>).</summary>
        public const string PrefabDir = UiEditFolder.Root + "/";
        const string IconDir = "Assets/_Project/Resources/UI2/Icons/";
        const string PreviewDir = "Assets/_Project/UI2Preview/";
        const string ScenePath = "Assets/_Project/Scenes/UiPreview.unity";
        static readonly Dictionary<RectTransform, Rect> Fig = new Dictionary<RectTransform, Rect>();
        static Font font;
        static Scene work;

        // ------------------------------------------------------------------ palette (Figma)
        static Color C(string hex, float a = 1f) { ColorUtility.TryParseHtmlString(hex, out var c); c.a = a; return c; }
        static readonly Color Cream = C("#F9EDDD"), Cream2 = C("#FAF0DF"), Edge = C("#C4DFE7"), Stitch = C("#C8E1EA"),
            Brown = C("#7A594D"), Brown2 = C("#816253"), Ink = C("#7D5D51"), Sky = C("#A0DCF8"), Peach = C("#FFB58A"),
            Pink = C("#FF8FAB"), Pink2 = C("#FFA9C2"), PinkPale = C("#FFE1E6"), Yellow = C("#FFEC76"), Sand = C("#FFEBCB"),
            NavOff = C("#9A7A6C"), Divider = C("#E8C9B4"), Cream2a = C("#FAF0DF", 0.6f);
        static readonly Color Shadow = new Color(0.25f, 0.16f, 0.1f, 0.45f);

        // ------------------------------------------------------------------ layout
        public enum A { TL, T, TR, L, C, R, BL, B, BR, Fill, LeftStretch, RightStretch }
        /// <summary>Which edge of a Figma text stays put when the string changes (other language): left, centre, right.</summary>
        public enum H { L, C, R }

        static Vector2 Anchor(A a)
        {
            switch (a)
            {
                case A.TL: return new Vector2(0, 1); case A.T: return new Vector2(0.5f, 1); case A.TR: return new Vector2(1, 1);
                case A.L: return new Vector2(0, 0.5f); case A.R: return new Vector2(1, 0.5f);
                case A.BL: return new Vector2(0, 0); case A.B: return new Vector2(0.5f, 0); case A.BR: return new Vector2(1, 0);
                default: return new Vector2(0.5f, 0.5f);
            }
        }

        /// <summary>A RectTransform at Figma (x, y, w, h) under a parent whose Figma rect is known, anchored by <paramref name="a"/>.</summary>
        static RectTransform Node(string name, RectTransform parent, float x, float y, float w, float h, A a)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
            var p = Fig[parent]; Fig[rt] = new Rect(x, y, w, h);
            if (a == A.Fill || a == A.LeftStretch || a == A.RightStretch)
            {
                rt.anchorMin = new Vector2(a == A.RightStretch ? 1 : 0, 0); rt.anchorMax = new Vector2(a == A.LeftStretch ? 0 : 1, 1);
                rt.pivot = new Vector2(0.5f, 0.5f);
                float left = x - p.x, right = (x + w) - (p.x + p.width), top = p.y - y, bottom = (p.y + p.height) - (y + h);
                if (a == A.LeftStretch) { rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(left + w, top); }
                else if (a == A.RightStretch) { rt.offsetMin = new Vector2(right - w, bottom); rt.offsetMax = new Vector2(right, top); }
                else { rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(right, top); }
                return rt;
            }
            var an = Anchor(a); rt.anchorMin = rt.anchorMax = an; rt.pivot = an;
            float refX = p.x + an.x * p.width, refY = p.y + (1 - an.y) * p.height;
            float cx = x + an.x * w, cy = y + (1 - an.y) * h;
            rt.anchoredPosition = new Vector2(cx - refX, -(cy - refY)); rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        static RectTransform Root(string name)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            SceneManager.MoveGameObjectToScene(go, work);   // built in a private preview scene: the user's open scene is never touched
            var rt = (RectTransform)go.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            Fig[rt] = new Rect(0, 0, 1920, 1080); return rt;
        }

        // ------------------------------------------------------------------ shapes, text, icons
        static SoftRect Shape(RectTransform parent, string name, float x, float y, float w, float h, A a, Color fill, float r)
        {
            var s = Node(name, parent, x, y, w, h, a).gameObject.AddComponent<SoftRect>();
            s.color = fill; s.radius = new Vector4(r, r, r, r); s.raycastTarget = false; return s;
        }
        /// <summary>A Figma (SVG) stroke: centred on the outline, so the shape grows by half the width all round.</summary>
        static SoftRect Border(this SoftRect s, Color c, float w)
        {
            s.borderWidth = w; s.borderColor = c;
            float d = w * 0.5f; Grow(s.RT(), d); s.radius += new Vector4(d, d, d, d);
            return s;
        }
        /// <summary>A CSS border (the settings screen's divs): inside the box.</summary>
        static SoftRect InnerBorder(this SoftRect s, Color c, float w) { s.borderWidth = w; s.borderColor = c; return s; }

        static void Grow(RectTransform rt, float d)
        {
            var p = rt.pivot;
            rt.sizeDelta += new Vector2(2 * d, 2 * d); rt.anchoredPosition += new Vector2(2 * d * (p.x - 0.5f), 2 * d * (p.y - 0.5f));
            var f = Fig[rt]; Fig[rt] = new Rect(f.x - d, f.y - d, f.width + 2 * d, f.height + 2 * d);
        }

        /// <summary>
        /// A Figma stitch: its own rounded rect (x, y, w, h = the dash centre line, radius r), exactly n dashes of length
        /// <paramref name="dash"/> with round caps, 3 px wide.
        /// </summary>
        static SoftRect Seam(this SoftRect s, Color c, float x, float y, float w, float h, float r, int n, float dash, float width = 3f)
        {
            var f = Fig[s.RT()];
            s.stitch = true; s.stitchColor = c; s.stitchWidth = width; s.roundCaps = true; s.stitchCount = n; s.dash = dash; s.gap = dash;
            s.stitchPad = new Vector4(x - f.x, y - f.y, f.xMax - (x + w), f.yMax - (y + h)); s.stitchRadius = r;
            return s;
        }

        /// <summary>Figma's drop shadow: black, 1 px down-right, gaussian sigma 1 (0.5 for the CSS rows); the stitch gets it too.</summary>
        static SoftRect Shadowed(this SoftRect s, float alpha = 0.75f, float sigma = 1f)
        {
            s.shadowColor = new Color(0, 0, 0, alpha); s.shadowOffset = new Vector2(1, -1); s.shadowBlur = sigma;
            if (s.stitch) s.stitchShadow = s.shadowColor;
            return s;
        }
        static SoftRect Corners(this SoftRect s, float tl, float tr, float br, float bl) { s.radius = new Vector4(tl, tr, br, bl); return s; }
        static RectTransform RT(this Component c) => (RectTransform)c.transform;

        /// <summary>
        /// One Figma text node as a TMP label at the exact Figma spot. <paramref name="h"/>: which edge stays put when the
        /// string changes; <paramref name="room"/> &gt; 0: the width the label may use (longer translations shrink to it).
        /// <paramref name="key"/> null keeps the Figma text as it is (numbers, initials).
        /// </summary>
        static TextMeshProUGUI Txt(RectTransform parent, string name, string frame, string id, A a, string key, H h = H.L, float room = 0, params object[] args)
            => Lines(parent, name, frame, new[] { id }, a, key, h, room, args);

        /// <summary>
        /// Several Figma text lines (Figma draws a two-line message as two texts) as one TMP label: the line pitch comes from
        /// the Figma lines, each line keeps its alignment. With <paramref name="room"/> &gt; 0 a longer translation wraps in it.
        /// </summary>
        static TextMeshProUGUI Lines(RectTransform parent, string name, string frame, string[] ids, A a, string key, H h, float room, params object[] args)
        {
            var t0 = UiV2Text.Get(frame, ids[0]); float size = t0.size; int n = ids.Length;
            float pitch = n > 1 ? (UiV2Text.Get(frame, ids[n - 1]).y - t0.y) / (n - 1) : 0f;
            float refX = 0f, widest = 0f; var lines = new List<string>();
            foreach (var id in ids)
            {
                var t = UiV2Text.Get(frame, id); lines.Add(t.text);
                float w = UiV2Text.Measure(t.text, size, t.track); widest = Mathf.Max(widest, w);
                refX += h == H.L ? t.x : h == H.C ? t.x + w * 0.5f : t.x + w;
            }
            refX /= n;
            float boxW = Mathf.Max(room, widest + 2f);
            float x = h == H.L ? refX : h == H.C ? refX - boxW * 0.5f : refX - boxW;
            float line = size * 1.3f;
            var go = Node(name, parent, x, t0.y, boxW, line + pitch * (n - 1), a).gameObject;
            var tmp = UiV2Text.Style(go, size, t0.color, t0.track);
            if (n > 1)
            {
                tmp.lineSpacing = (pitch - line) / size * 100f;
                tmp.alignment = h == H.L ? TextAlignmentOptions.TopLeft : h == H.C ? TextAlignmentOptions.Top : TextAlignmentOptions.TopRight;
                if (room > 0) tmp.textWrappingMode = TextWrappingModes.Normal;
            }
            else
            {
                tmp.alignment = h == H.L ? TextAlignmentOptions.Left : h == H.C ? TextAlignmentOptions.Center : TextAlignmentOptions.Right;
                if (room > 0) { tmp.enableAutoSizing = true; tmp.fontSizeMax = size; tmp.fontSizeMin = Mathf.Round(size * 0.55f); }
            }
            tmp.text = string.Join("\n", lines);
            if (key != null)
            {
                var l = LocText.On(tmp, key, args);
                if (!Mathf.Approximately(t0.track, 0f)) { l.koTracking = t0.track; l.Refresh(); }
            }
            return tmp;
        }

        /// <summary>A label with no Figma text node behind it (a value the game fills in).</summary>
        static TextMeshProUGUI Value(RectTransform parent, string name, float x, float y, float w, float h, A a, float size, Color color, TextAlignmentOptions align, string text)
        {
            var tmp = UiV2Text.Style(Node(name, parent, x, y, w, h, a).gameObject, size, color);
            tmp.alignment = align; tmp.text = text; tmp.enableAutoSizing = true; tmp.fontSizeMax = size; tmp.fontSizeMin = Mathf.Round(size * 0.55f);
            return tmp;
        }

        static Sprite Sprite(string path)
        {
            if (!path.Contains("/")) path = IconDir + path + ".png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null && (ti.textureType != TextureImporterType.Sprite || ti.mipmapEnabled == false))
            {
                ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.alphaIsTransparency = true;
                ti.mipmapEnabled = true; ti.filterMode = FilterMode.Trilinear; ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>An icon cut by icons.py (2 px of padding around the Figma region).</summary>
        static Image Icon(RectTransform parent, string name, float x, float y, float w, float h, A a, string icon)
        {
            var img = Node(name, parent, x - 2, y - 2, w + 4, h + 4, a).gameObject.AddComponent<Image>();
            img.sprite = Sprite(icon); img.preserveAspect = true; img.raycastTarget = false; return img;
        }

        static Button Clickable(Graphic target)
        {
            target.raycastTarget = true;
            var b = target.gameObject.AddComponent<Button>(); b.targetGraphic = target; b.transition = Selectable.Transition.None;
            target.gameObject.AddComponent<PressScale>();
            return b;
        }

        // ------------------------------------------------------------------ shared pieces
        /// <summary>The 100x100 cream square button with blue edge and stitch (gear, zoom); (sx, sy) = where Figma put the stitch.</summary>
        static Button CreamSquare(RectTransform p, string name, float x, float y, A a, float sx = 7f, float sy = 7f, float sh = 85f)
        {
            var s = Shape(p, name, x, y, 100, 100, a, Cream, 27.2f).Border(Edge, 3).Seam(Stitch, x + sx, y + sy, 85, sh, 26.2f, 16, 8.27f).Shadowed();
            return Clickable(s);
        }

        /// <summary>The blue square button with a white stitch (home, back).</summary>
        static Button BlueSquare(RectTransform p, string name, float x, float y, A a, float size = 100f, float sx = 6f, float sy = 7f)
        {
            float r = size >= 100 ? 27.2f : 26.9f, st = size - 15f;
            var s = Shape(p, name, x, y, size, size, a, Sky, r).Seam(Color.white, x + sx, y + sy, st, st, r - 1f, 16, size >= 100 ? 8.27f : 8.16f).Shadowed();
            return Clickable(s);
        }

        static Button Gear(RectTransform p, float x, float y, A a, float sx = 7f, float sy = 7f)
        {
            var b = CreamSquare(p, "Btn_Settings", x, y, a, sx, sy);
            Icon(b.RT(), "Icon", x + 23, y + 19, 56, 60, A.C, "gear"); return b;
        }

        static Button Home(RectTransform p, float x, float y, A a)
        {
            var b = BlueSquare(p, "Btn_Home", x, y, a);
            Icon(b.RT(), "Icon", x + 21, y + 24, 57, 52, A.C, "home"); return b;
        }

        /// <summary>Zoom + / − (Figma: the plus is two bars, the minus is a 60 px "-" text).</summary>
        static void ZoomPair(RectTransform p, float x1, float y1, float x2, float y2, A a, string frame, string minusId, float plusSx = 6f)
        {
            var plus = CreamSquare(p, "Btn_ZoomIn", x1, y1, a, plusSx, 7f);
            Shape(plus.RT(), "Bar", x1 + 36, y1 + 46, 29, 7, A.C, Ink, 3.5f); Shape(plus.RT(), "Bar2", x1 + 47, y1 + 35, 7, 29, A.C, Ink, 3.5f);
            var minus = CreamSquare(p, "Btn_ZoomOut", x2, y2, a, 7f, 7f);
            Txt(minus.RT(), "Minus", frame, minusId, A.C, null, H.C);
        }

        /// <summary>The big cream panel with blue edge (dialogs, toasts, banners); the stitch is added by the caller (Figma numbers).</summary>
        static SoftRect Panel(RectTransform p, string name, float x, float y, float w, float h, A a, float r = 27.2f)
            => Shape(p, name, x, y, w, h, a, Cream, r).Border(Edge, 3);

        /// <summary>A rounded button whose label is the Figma text <paramref name="id"/>, centred where Figma has it.</summary>
        static Button Pill(RectTransform p, string name, float x, float y, float w, float h, A a, Color fill, float r, string frame, string id, string key, params object[] args)
        {
            var s = Shape(p, name, x, y, w, h, a, fill, r);
            Txt(s.RT(), "Label", frame, id, A.C, key, H.C, w - 36, args);
            return Clickable(s);
        }

        static Button CoinPill(RectTransform p, float x, float y, A a)
        {
            var s = Shape(p, "Coins", x, y, 285, 93, a, Yellow, 46.7f).Seam(Color.white, x + 7, y + 10, 271, 74, 37f, 32, 9.86f).Shadowed();
            Icon(s.RT(), "Coin", x + 25, y + 17, 59, 59, A.L, "coin");
            // Figma leaves the pill empty; the game writes the amount here
            Value(s.RT(), "Amount", x + 94, y, 170, 93, A.Fill, 40, Brown2, TextAlignmentOptions.Center, "");
            return Clickable(s);
        }

        // ------------------------------------------------------------------ settings widgets
        static Slider MakeSlider(RectTransform row, string name, float x, float y, float value)
        {
            // Figma slider: 360x38 box; track 328x14 at (16,11) r7; knob 30 white, pink stroke 4; the group has the 50 % shadow
            var root = Node(name, row, x, y, 360, 38, A.R);
            var bg = Shape(root, "Track", x + 16, y + 11, 328, 14, A.Fill, Sand, 7).Shadowed(0.5f);
            var fillArea = Node("Fill Area", root, x + 16, y + 11, 328, 14, A.Fill);
            var fill = Shape(fillArea, "Fill", x + 16, y + 11, 328, 14, A.Fill, Pink, 7);
            var slideArea = Node("Handle Slide Area", root, x + 16, y + 4, 328, 30, A.Fill);
            var knob = Shape(slideArea, "Handle", x + 16, y + 4, 30, 30, A.L, Color.white, 15).Border(Pink2, 4).Shadowed(0.5f);
            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.RT(); s.handleRect = knob.RT(); s.targetGraphic = knob; s.direction = Slider.Direction.LeftToRight;
            s.minValue = 0; s.maxValue = 1; s.value = value; s.transition = Selectable.Transition.None;
            knob.raycastTarget = true; bg.raycastTarget = true;
            fill.RT().anchorMin = Vector2.zero; fill.RT().anchorMax = new Vector2(value, 1); fill.RT().offsetMin = fill.RT().offsetMax = Vector2.zero;
            knob.RT().anchorMin = new Vector2(value, 0); knob.RT().anchorMax = new Vector2(value, 1); knob.RT().pivot = new Vector2(0.5f, 0.5f);
            knob.RT().sizeDelta = new Vector2(34, 4); knob.RT().anchoredPosition = Vector2.zero;   // 30 + the stroke's outer half
            return s;
        }

        static Toggle MakeSwitch(RectTransform row, string name, float x, float y, bool on)
        {
            var root = Node(name, row, x, y, 104, 54, A.R);
            var track = Shape(root, "Track", x + 2, y + 2, 100, 50, A.C, on ? Pink : Sand, 25).Border(Pink2, 4).Shadowed(0.5f);
            var knob = Shape(root, "Knob", x + 31, y + 6, 42, 42, A.C, Color.white, 21).Shadowed(0.5f);
            var t = root.gameObject.AddComponent<Toggle>(); t.targetGraphic = track; t.transition = Selectable.Transition.None; t.isOn = on;
            track.raycastTarget = true;
            var sv = root.gameObject.AddComponent<SwitchVisual>(); sv.track = track; sv.knob = knob; sv.on = Pink; sv.off = Sand; sv.travel = 25; sv.Snap();
            return t;
        }

        static TMP_Dropdown MakeDropdown(RectTransform row, string name, float x, float y, string frame, string captionId)
        {
            // Figma: 298x61 white pill, pink edge 5, caption 34px, ▼ at the right
            var box = Shape(row, name, x, y, 298, 61, A.R, Color.white, 30.5f).InnerBorder(Pink2, 5);
            box.raycastTarget = true;
            var cap = Txt(box.RT(), "Label", frame, captionId, A.C, null, H.C, 205);
            Icon(box.RT(), "Arrow", x + 242, y + 18, 29, 25, A.R, "dropdown");
            // template: a white list under the box
            var tmpl = Node("Template", box.RT(), x, y + 66, 298, 260, A.T);
            var tbg = tmpl.gameObject.AddComponent<SoftRect>(); tbg.color = Color.white; tbg.radius = new Vector4(26, 26, 26, 26); tbg.borderWidth = 4; tbg.borderColor = Pink2; tbg.shadowColor = Shadow; tbg.shadowBlur = 4; tbg.shadowOffset = new Vector2(0, -3);
            var sr = tmpl.gameObject.AddComponent<ScrollRect>(); sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 20;
            var vp = Node("Viewport", tmpl, x + 6, y + 72, 286, 248, A.Fill); vp.gameObject.AddComponent<RectMask2D>();
            var content = Node("Content", vp, x + 6, y + 72, 286, 60, A.T);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1); content.offsetMin = new Vector2(0, -60); content.offsetMax = Vector2.zero;
            var item = Node("Item", content, x + 6, y + 72, 286, 56, A.T);
            item.anchorMin = new Vector2(0, 0.5f); item.anchorMax = new Vector2(1, 0.5f); item.sizeDelta = new Vector2(0, 56); item.anchoredPosition = Vector2.zero;
            var ibg = item.gameObject.AddComponent<SoftRect>(); ibg.color = new Color(1, 1, 1, 0); ibg.radius = new Vector4(22, 22, 22, 22);
            var chk = Shape(item, "Item Checkmark", x + 10, y + 74, 276, 52, A.Fill, PinkPale, 22);
            var il = Value(item, "Item Label", x + 6, y + 72, 286, 56, A.Fill, 30, Brown2, TextAlignmentOptions.Center, "");
            var tog = item.gameObject.AddComponent<Toggle>(); tog.targetGraphic = ibg; tog.graphic = chk; tog.isOn = true; ibg.raycastTarget = true;
            sr.viewport = vp; sr.content = content;
            tmpl.gameObject.SetActive(false);
            string shown = cap.text;   // setting captionText refreshes (and blanks) it while there are no options yet
            var dd = box.gameObject.AddComponent<TMP_Dropdown>();
            dd.targetGraphic = box; dd.template = tmpl; dd.captionText = cap; dd.itemText = il; dd.transition = Selectable.Transition.None;
            dd.options.Clear(); dd.options.Add(new TMP_Dropdown.OptionData(shown)); dd.SetValueWithoutNotify(0); dd.RefreshShownValue();   // a new TMP_Dropdown starts at -1 (nothing)
            return dd;
        }

        static RectTransform Row(RectTransform page, string name, float y, string frame, string labelId, string key)
        {
            var r = Shape(page, name, 425, y, 1326, 104, A.T, new Color(1, 1, 1, 0.9f), 38).Shadowed(0.5f, 0.5f);   // CSS drop-shadow 1px 1px 1px
            Txt(r.RT(), "Label", frame, labelId, A.L, key, H.L, 760);
            return r.RT();
        }

        // ------------------------------------------------------------------ screens
        static GameObject Settings()
        {
            const string G = "58-1007", Gx = "58-1098", Au = "58-1162";
            var root = Root("UI2_Settings");
            var scr = root.gameObject.AddComponent<SettingsScreen>();
            var back = BlueSquare(root, "Btn_Back", 44, 58, A.TL, 98, 7f, 7f);
            Icon(back.RT(), "Icon", 70, 86, 37, 41, A.C, "back");
            scr.back = back;
            Txt(root, "Title", G, "58:1014", A.TL, "settings.title", H.L, 420);
            var nav = Shape(root, "Nav", 44, 198, 288, 900, A.LeftStretch, new Color(0.976f, 0.929f, 0.867f, 0.94f), 0).Corners(144, 144, 0, 0).InnerBorder(Edge, 3);
            string[] navKeys = { "settings.nav.general", "settings.nav.graphics", "settings.nav.audio" };
            string[] navIds = { "58:1016", "58:1019", "58:1020" };
            for (int i = 0; i < 3; i++)
            {
                var ft = UiV2Text.Get(G, navIds[i]);
                var hit = Shape(nav.RT(), "NavItem" + i, 44, ft.y - 35, 288, 116, A.TL, new Color(1, 1, 1, 0), 0);
                scr.navButtons[i] = Clickable(hit);
                Object.DestroyImmediate(hit.GetComponent<PressScale>());
                scr.navLabels[i] = Txt(nav.RT(), "NavLabel" + i, G, navIds[i], A.TL, navKeys[i], H.C, 214);
                Shape(nav.RT(), "Divider" + i, 139, 404 + i * 129, 98, 6, A.TL, Divider, 3);   // CSS: inside the 3 px border
            }
            scr.navOn = Brown; scr.navOff = NavOff;
            scr.navArrow = Icon(nav.RT(), "NavArrow", 106, 331, 30, 32, A.TL, "nav_arrow").RT();
            // pages
            var general = Node("Page_General", root, 0, 0, 1920, 1080, A.Fill);
            var graphics = Node("Page_Graphics", root, 0, 0, 1920, 1080, A.Fill);
            var audio = Node("Page_Audio", root, 0, 0, 1920, 1080, A.Fill);
            scr.pages = new[] { general.gameObject, graphics.gameObject, audio.gameObject };
            float[] ry = { 308, 438, 568, 698 };
            scr.language = MakeDropdown(Row(general, "Row_Language", ry[0], G, "58:1025", "settings.language"), "Dropdown", 1421, ry[0] + 21, G, "58:1227");
            scr.fov = MakeSlider(Row(general, "Row_Fov", ry[1], G, "58:1031", "settings.fov"), "Slider", 1379, ry[1] + 34, 233f / 328f);
            scr.mouse = MakeSlider(Row(general, "Row_Mouse", ry[2], G, "58:1037", "settings.mouse"), "Slider", 1379, ry[2] + 34, 177f / 328f);
            scr.pad = MakeSlider(Row(general, "Row_Pad", ry[3], G, "58:1043", "settings.pad"), "Slider", 1379, ry[3] + 34, 157f / 328f);
            scr.quality = MakeDropdown(Row(graphics, "Row_Quality", ry[0], Gx, "58:1141", "settings.quality"), "Dropdown", 1421, ry[0] + 21, Gx, "58:1143");
            scr.resolution = MakeDropdown(Row(graphics, "Row_Resolution", ry[1], Gx, "58:1147", "settings.resolution"), "Dropdown", 1421, ry[1] + 21, Gx, "58:1149");
            scr.fullscreen = MakeSwitch(Row(graphics, "Row_Fullscreen", ry[2], Gx, "58:1153", "settings.fullscreen"), "Switch", 1619, ry[2] + 25, true);
            scr.brightness = MakeSlider(Row(graphics, "Row_Brightness", ry[3], Gx, "58:1157", "settings.brightness"), "Slider", 1379, ry[3] + 34, 197f / 328f);
            scr.master = MakeSlider(Row(audio, "Row_Master", ry[0], Au, "58:1205", "settings.master"), "Slider", 1379, ry[0] + 34, 262f / 328f);
            scr.music = MakeSlider(Row(audio, "Row_Music", ry[1], Au, "58:1211", "settings.music"), "Slider", 1379, ry[1] + 34, 197f / 328f);
            scr.effects = MakeSlider(Row(audio, "Row_Effects", ry[2], Au, "58:1217", "settings.effects"), "Slider", 1379, ry[2] + 34, 230f / 328f);
            scr.mute = MakeSwitch(Row(audio, "Row_Mute", ry[3], Au, "58:1223", "settings.mute"), "Switch", 1619, ry[3] + 25, false);
            graphics.gameObject.SetActive(false); audio.gameObject.SetActive(false);
            return root.gameObject;
        }

        static GameObject HouseInvite()
        {
            const string F = "59-1327";
            var root = Root("UI2_HouseInvite");
            var dlg = Panel(root, "Dialog", 377, 384, 1179, 442, A.C, 28f).Seam(Stitch, 389, 396, 1155, 418, 25.5f, 154, 9.99f);
            var head = Shape(dlg.RT(), "Header", 377, 384, 1179, 112, A.T, Sky, 0).Corners(28, 28, 0, 0);
            Txt(head.RT(), "Title", F, "59:1415", A.C, "village.house.title", H.C, 1000, "@name.mongi");
            Lines(dlg.RT(), "Body", F, new[] { "59:1417", "59:1418" }, A.T, "intake.body.mongi", H.C, 1080);
            Pill(dlg.RT(), "Btn_Invite", 778, 705, 345, 86, A.B, Sky, 43.3f, F, "59:1420", "village.house.invite");
            return root.gameObject;
        }

        // ------------------------------------------------------------------ batch 2: title, repair HUD, photo, quest, storage
        static Image Picture(RectTransform p, string name, float x, float y, float w, float h, A a, string assetPath, bool preserve = true)
        {
            var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.alphaIsTransparency = true;
                ti.mipmapEnabled = true; ti.filterMode = FilterMode.Trilinear; ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.maxTextureSize = 4096;
                ti.SaveAndReimport();
            }
            var img = Node(name, p, x, y, w, h, a).gameObject.AddComponent<Image>();
            img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath); img.preserveAspect = preserve; img.raycastTarget = false; return img;
        }

        static float tabRadius = 28f;   // Figma: 28 on the quest window

        static SoftRect Tab(RectTransform p, string name, float x, float y, float w, float h, Color fill, string frame, string id, string key, params object[] args)
        {
            var t = Shape(p, name, x, y, w, h, A.T, fill, tabRadius);
            Txt(t.RT(), "Label", frame, id, A.T, key, H.C, w - 32, args);
            return t;
        }


        // ------------------------------------------------------------------ build + preview scene
        static readonly (string name, string frame, System.Func<GameObject> make)[] Screens =
        {
            ("Settings", "58-1007", Settings),
            ("HouseInvite", "59-1327", HouseInvite),
        };

        static void Init()
        {
            Fig.Clear();
            font = AssetDatabase.LoadAssetAtPath<Font>(UiV2Text.FontPath);
            UiV2Text.Init();
            ItemIconBaker.BakeAll(false);
        }

        [MenuItem("Mongle/UI v2/프리팹 + 미리보기 씬 만들기 (없는 것만)")]
        public static void BuildMissing() => Build(false);

        [MenuItem("Mongle/UI v2/프리팹 다시 만들기 (덮어쓰기)")]
        public static void Rebuild()
        {
            if (!EditorUtility.DisplayDialog("UI v2", "_UI수정 의 피그마 화면 프리팹을 피그마 기준으로 다시 만듭니다. 직접 고친 내용은 사라집니다.", "다시 만들기", "취소")) return;
            Build(true);
        }

        public static void Build(bool overwrite)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[UiV2] stop play mode first"); return; }
            if (!UiV2Text.EssentialsReady) { UiV2Text.ImportEssentials(); Debug.LogWarning("[UiV2] importing TextMesh Pro essentials — run the build again once the import is done"); return; }
            Init();
            work = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (var s in Screens)
                {
                    var path = UiEditFolder.FrontPath(s.name);
                    if (!overwrite && File.Exists(path)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    var go = s.make();
                    PrefabUtility.SaveAsPrefabAsset(go, path);
                    Object.DestroyImmediate(go);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(work); UiV2Text.Done(); }
            UiEditFolder.RefreshIndex();
            BuildScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[UiV2] built " + Screens.Length + " screens + " + ScenePath);
        }

        static void BuildScene()
        {
            // open right now (maybe with edits): leave it alone — its screens are prefab instances and already show the new build
            if (SceneManager.GetSceneByPath(ScenePath).isLoaded) { Debug.Log("[UiV2] " + ScenePath + " is open: scene left as it is, prefabs updated"); return; }
            // built next to whatever is open (additive) and closed again: the user's open scene is never touched
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var prevActive = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            var camGo = new GameObject("Camera"); var cam = camGo.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.96f, 0.9f, 0.86f); cam.orthographic = true;
            var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<InputSystemUIInputModule>();
            var cgo = new GameObject("Canvas", typeof(RectTransform)); cgo.layer = 5;
            var canvas = cgo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            cgo.AddComponent<CanvasScaler>(); cgo.AddComponent<AdaptiveScaler>(); cgo.AddComponent<GraphicRaycaster>();
            var crt = (RectTransform)cgo.transform;
            var back = new GameObject("Backdrop", typeof(RectTransform)).AddComponent<RawImage>(); back.transform.SetParent(crt, false);
            var arf = back.gameObject.AddComponent<AspectRatioFitter>(); arf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; arf.aspectRatio = 16f / 9f;
            back.raycastTarget = false;
            var screens = new GameObject("Screens", typeof(RectTransform)); var srt = (RectTransform)screens.transform; srt.SetParent(crt, false);
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = srt.offsetMax = Vector2.zero;
            var refImg = new GameObject("FigmaReference", typeof(RectTransform)).AddComponent<RawImage>(); refImg.transform.SetParent(crt, false);
            var rf = refImg.gameObject.AddComponent<AspectRatioFitter>(); rf.aspectMode = AspectRatioFitter.AspectMode.FitInParent; rf.aspectRatio = 16f / 9f;
            refImg.color = new Color(1, 1, 1, 0.5f); refImg.raycastTarget = false; refImg.enabled = false;
            var info = new GameObject("Info", typeof(RectTransform)).AddComponent<Text>(); info.transform.SetParent(crt, false);
            var irt = (RectTransform)info.transform; irt.anchorMin = irt.anchorMax = irt.pivot = Vector2.zero; irt.anchoredPosition = new Vector2(16, 10); irt.sizeDelta = new Vector2(1400, 34);
            info.font = font; info.fontSize = 22; info.color = new Color(0.3f, 0.22f, 0.2f, 0.85f); info.raycastTarget = false;
            var pv = cgo.AddComponent<UiPreview>(); pv.backdrop = back; pv.reference = refImg; pv.label = info;
            foreach (var s in Screens)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiEditFolder.FrontPath(s.name));
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, srt);
                pv.entries.Add(new UiPreview.Entry
                {
                    name = s.name, screen = inst,
                    backdrop = AssetDatabase.LoadAssetAtPath<Texture>(PreviewDir + "Backdrops/" + s.frame + ".jpg"),
                    reference = AssetDatabase.LoadAssetAtPath<Texture>(PreviewDir + "Reference/" + s.frame + ".png"),
                });
            }
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (prevActive.IsValid()) SceneManager.SetActiveScene(prevActive);
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}

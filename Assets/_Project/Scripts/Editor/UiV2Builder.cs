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

        static GameObject Intake()
        {
            const string F = "57-3";
            var root = Root("UI2_Intake");
            Gear(root, 1794, 36, A.TR);
            var dlg = Panel(root, "Dialog", 380, 543, 1179, 442, A.B).Seam(Stitch, 393, 555, 1155, 418, 23.9f, 154, 10.02f);
            Txt(dlg.RT(), "Title", F, "57:16", A.T, "intake.arrived", H.C, 1000, "@name.mongi");
            Lines(dlg.RT(), "Body", F, new[] { "57:18", "57:19" }, A.T, "intake.body.mongi", H.C, 1080);
            Pill(dlg.RT(), "Btn_Sorry", 620, 853, 328, 87, A.B, Peach, 19.4f, F, "57:23", "intake.sorry");
            Pill(dlg.RT(), "Btn_Look", 997, 853, 328, 87, A.B, Sky, 19.4f, F, "57:21", "intake.look");
            return root.gameObject;
        }

        /// <summary>Village HUD as Figma 59:1327 draws it (the frame with every HUD piece: home, quest, zoom, coins, gear, toolbar).</summary>
        static GameObject VillageHud()
        {
            const string F = "59-1327";
            var root = Root("UI2_VillageHud");
            Home(root, 60, 53, A.TL);
            var q = Shape(root, "Btn_Quest", 44, 189, 187, 94, A.TL, Cream2a, 47);
            Txt(q.RT(), "Label", F, "59:1445", A.C, "village.quest", H.C, 170);
            Clickable(q);
            ZoomPair(root, 60, 468, 61, 587, A.L, F, "59:1435");
            CoinPill(root, 1467, 56, A.TR);
            Gear(root, 1784, 53, A.TR, 6f, 7f);
            var bar = Shape(root, "Toolbar", 1699, 302, 193, 555, A.R, Cream2a, 73.6f);
            (string icon, string key, string id, float x, float y, float s)[] items =
            {
                ("bar_furniture", "village.bar.furniture", "59:1436", 1736, 359, 114),
                ("bar_album", "village.bar.album", "59:1437", 1741, 509, 114),
                ("bar_storage", "village.bar.storage", "59:1438", 1739, 659, 118),
            };
            foreach (var it in items)
            {
                var btn = Node("Btn_" + it.icon.Substring(4), bar.RT(), 1699, it.y - 8, 193, it.s + 40, A.T);
                var hit = btn.gameObject.AddComponent<SoftRect>(); hit.color = new Color(1, 1, 1, 0);
                Icon(btn, "Icon", it.x, it.y, it.s, it.s, A.T, it.icon);
                Txt(btn, "Label", F, it.id, A.T, it.key, H.C, 180);
                Clickable(hit);
            }
            // things that show only when there is something to say (D-038: no empty banners)
            var hint = Shape(root, "Hint", 444, 60, 987, 122, A.T, Cream2a, 59.7f);
            Lines(hint.RT(), "Text", F, new[] { "59:1443", "59:1444" }, A.T, "village.hint", H.C, 940);
            hint.gameObject.SetActive(false);
            var banner = Panel(root, "Banner", 527, 83, 797, 93, A.T).Seam(Stitch, 535, 93, 781, 74, 26f, 82, 10.12f);
            Txt(banner.RT(), "Text", "57-263", "57:303", A.C, "village.banner", H.C, 760, "@name.mongi", "2026-10-22");
            banner.gameObject.SetActive(false);
            var toast = Panel(root, "Toast", 473, 876, 1051, 137, A.B, 28f).Seam(Stitch, 484, 890, 1029, 109, 26f, 112, 9.93f).Shadowed();
            Txt(toast.RT(), "Text", "59-1525", "59:1563", A.L, "village.toast.train", H.L, 630, "@name.mongi");
            Pill(toast.RT(), "Btn_Watch", 1208, 901, 268, 86, A.R, Sky, 28, "59-1525", "59:1564", "village.toast.watch");
            toast.gameObject.SetActive(false);
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

        static GameObject RideInvite()
        {
            const string F = "59-1146";
            var root = Root("UI2_RideInvite");
            var bubble = Shape(root, "Bubble", 220, 748, 959, 259, A.B, Color.white, 95.9f).Shadowed(0.5f);
            var tag = Shape(root, "NameTag", 227, 710, 204, 70, A.B, PinkPale, 28);
            Txt(tag.RT(), "Name", F, "59:1255", A.C, "name.mongi", H.C, 180);
            Lines(bubble.RT(), "Text", F, new[] { "59:1257", "59:1258" }, A.TL, "ride.ask.seesaw", H.L, 800);
            var sub = Value(bubble.RT(), "Sub", 296, 918, 760, 32, A.TL, 22, Peach, TextAlignmentOptions.Left, "");
            LocText.On(sub, "ride.sub", "20");
            Icon(bubble.RT(), "Next", 1092, 944, 33, 28, A.BR, "sort_arrow");
            var yes = Shape(root, "Btn_Yes", 1263, 778, 357, 75, A.B, Color.white, 37.7f).Border(PinkPale, 5).Shadowed(0.5f);
            Icon(yes.RT(), "Heart", 1293, 800, 33, 29, A.L, "heart").color = PinkPale;
            Txt(yes.RT(), "Label", F, "59:1259", A.C, "ride.yes", H.C, 260);
            Clickable(yes);
            var no = Shape(root, "Btn_Later", 1263, 887, 357, 75, A.B, Color.white, 37.7f).Shadowed(0.5f);
            Txt(no.RT(), "Label", F, "59:1260", A.C, "ride.later", H.C, 300);
            Clickable(no);
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

        static GameObject Title()
        {
            const string F = "57-726";
            var root = Root("UI2_Title");
            var bg = Picture(root, "Background", 0, 0, 1920, 1080, A.C, "Assets/_Project/Resources/UI2/Title/title_bg.jpg", false);
            var arf = bg.gameObject.AddComponent<AspectRatioFitter>(); arf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; arf.aspectRatio = 1920f / 1080f;
            Picture(root, "Logo", 548, 169, 753, 443, A.C, IconDir + "logo.png");
            Gear(root, 1790, 18, A.TR);
            var start = Shape(root, "Btn_New", 514, 732, 414, 136, A.B, Peach, 57.2f).Seam(Color.white, 532, 743, 380, 114, 57f, 42, 10.2f).Shadowed();
            Txt(start.RT(), "Label", F, "57:826", A.C, "title.new", H.C, 354);
            Clickable(start);
            var open = Shape(root, "Btn_Continue", 989, 732, 414, 136, A.B, Sky, 57.2f).Seam(Color.white, 1007, 743, 380, 114, 57f, 42, 10.2f).Shadowed();
            Txt(open.RT(), "Label", F, "57:821", A.C, "title.continue", H.C, 354);
            Clickable(open);
            return root.gameObject;
        }

        static GameObject RepairHud()
        {
            const string F = "57-829";
            var root = Root("UI2_RepairHud");
            var back = Shape(root, "Btn_Back", 34, 40, 90, 93, A.TL, Sky, 23.4f).Seam(Color.white, 40, 50, 78, 74, 22.4f, 16, 8.66f).Shadowed();
            Icon(back.RT(), "Icon", 52, 62, 43, 50, A.C, "arrow_left"); Clickable(back);
            Gear(root, 1784, 49, A.TR);
            var done = Panel(root, "Done", 716, 37, 630, 116, A.T).Seam(Stitch, 723, 49, 618, 92, 23.9f, 68, 10.09f);
            Txt(done.RT(), "Text", F, "57:908", A.C, "repair.done", H.C, 580);
            var reward = Shape(root, "Reward", 802, 210, 465, 94, A.T, Cream2a, 47.1f);
            Icon(reward.RT(), "Coin", 853, 229, 56, 56, A.L, "coin");
            Txt(reward.RT(), "Text", F, "57:910", A.L, "repair.coins", H.L, 320, "50");
            // care notes (진료 수첩)
            var notes = Shape(root, "Notes", 42, 290, 430, 543, A.L, Cream2, 44.7f).Seam(C("#C6DFE8"), 54, 305, 406, 513, 44.7f, 58, 15.13f);
            Icon(notes.RT(), "Badge", 44, 244, 78, 78, A.TL, "notebook_button");
            Picture(notes.RT(), "Star", 69, 337, 31, 30, A.TL, IconDir + "star_small.png");
            Txt(notes.RT(), "Title", F, "57:849", A.TL, "repair.notebook", H.L, 300);
            string[] qIds = { null, "57:857", "57:863", "57:869" };
            for (int i = 0; i < 4; i++)
            {
                float y = new[] { 381f, 489f, 599f, 709f }[i];
                bool isDone = i == 0;
                var row = Shape(notes.RT(), "Item" + i, 74, y, 368, 94, A.TL, isDone ? Sky : Cream2, 28.6f).Seam(Color.white, 83, y + (i == 3 ? 7 : 8), 350, 79, 27.6f, 40, 9.74f).Shadowed(0.5f);
                if (isDone)
                {
                    Icon(row.RT(), "Check", 91, y + 14, 63, 63, A.L, "check");
                    Txt(row.RT(), "Label", F, "57:850", A.L, "damage.thorn_ear", H.L, 262);
                }
                else
                {
                    var q = Shape(row.RT(), "Unknown", 94, y + 18, 56, 56, A.L, C("#FCD276"), 12);
                    Txt(q.RT(), "Q", F, qIds[i], A.C, null, H.C);
                }
            }
            ZoomPair(root, 1798, 475, 1800, 594, A.R, F, "57:907", 7f);
            // tool bar
            var bar = Shape(root, "ToolBar", 592, 844, 879, 216, A.B, Cream2, 34.6f).Seam(C("#C6DFE8"), 602, 855, 860, 194, 34.5f, 68, 15.2f);
            var left = CreamSquare(root, "Btn_ToolsLeft", 471, 901, A.B, 7f, 7f, 86f); Icon(left.RT(), "Icon", 494, 926, 43, 50, A.C, "arrow_left");
            var right = CreamSquare(root, "Btn_ToolsRight", 1491, 901, A.B, 7f, 7f, 86f); Icon(right.RT(), "Icon", 1525, 926, 43, 50, A.C, "arrow_right");
            var slots = Node("Slots", bar.RT(), 614, 872, 831, 161, A.B);
            (string icon, string key, string id, float iw, float ih)[] tools =
            {
                ("tool_tweezers", "tool.tweezers", "58:1285", 70, 94), ("tool_sponge", "tool.sponge", "58:1303", 96, 70),
                ("tool_needle", "tool.needle", "58:1313", 106, 106), ("tool_injector", "tool.injector", "58:1329", 69, 98),
            };
            for (int i = 0; i < tools.Length; i++)
            {
                float sx = new[] { 614f, 828f, 1041f, 1255f }[i];
                var slot = Shape(slots, "Slot_" + tools[i].key.Substring(5), sx, 872, 191, 161, A.TL, Cream2, 28.6f).Shadowed(0.5f);
                Icon(slot.RT(), "Icon", sx + 95.5f - tools[i].iw / 2, 878 + 52 - tools[i].ih / 2, tools[i].iw, tools[i].ih, A.T, tools[i].icon);
                Txt(slot.RT(), "Label", F, tools[i].id, A.T, tools[i].key, H.C, 185);
                Clickable(slot);
            }
            return root.gameObject;
        }

        static GameObject Photo()
        {
            const string F = "57-24";
            var root = Root("UI2_Photo");
            Gear(root, 1778, 33, A.TR);
            (string key, string id, float y, bool paw)[] poses = { ("photo.pose.basic", "57:61", 54, false), ("photo.pose.smile", "57:62", 185, true) };
            foreach (var po in poses)
            {
                var tag = Shape(root, "Pose_" + po.key.Substring(11), 46, po.y, 406, 98, A.TL, C("#FBECDE"), 34f).Corners(34.1f, 7.4f, 0.3f, 34.1f);
                Shape(root, "Pin", 25, po.y - 28, 53, 53, A.TL, Cream, 26.6f);
                if (po.paw) Icon(tag.RT(), "Icon", 91, po.y + 21, 62, 56, A.L, "pose_smile");
                else
                {
                    Shape(tag.RT(), "Frame", 91, po.y + 22, 54, 54, A.L, new Color(0, 0, 0, 0), 4).Border(Ink, 4);
                    Shape(tag.RT(), "Frame2", 99, po.y + 29, 39, 39, A.L, new Color(0, 0, 0, 0), 3).Border(Ink, 5);
                }
                Txt(tag.RT(), "Label", F, po.id, A.L, po.key, H.C, 230);
                Clickable(tag);
            }
            // vertical zoom
            var zoom = Shape(root, "Zoom", 370, 335, 86, 511, A.TL, C("#FBECDE"), 43f);
            Icon(zoom.RT(), "Plus", 398, 351, 29, 29, A.T, "slider_plus");
            var track = Shape(zoom.RT(), "Track", 406, 400, 15, 381, A.T, Ink, 7.5f);
            var fillArea = Node("Fill Area", track.RT(), 406, 400, 15, 381, A.Fill);
            var fill = Shape(fillArea, "Fill", 406, 400, 15, 213, A.Fill, C("#B68892"), 7.5f);
            var slide = Node("Handle Slide Area", track.RT(), 406, 400, 15, 381, A.Fill);
            var knob = Shape(slide, "Handle", 391, 569, 43, 43, A.C, Color.white, 21.7f);
            var sl = zoom.gameObject.AddComponent<Slider>(); sl.direction = Slider.Direction.TopToBottom; sl.fillRect = fill.RT(); sl.handleRect = knob.RT(); sl.targetGraphic = knob; sl.transition = Selectable.Transition.None;
            knob.raycastTarget = true; track.raycastTarget = true;
            fill.RT().anchorMin = new Vector2(0, 0.44f); fill.RT().anchorMax = Vector2.one; fill.RT().offsetMin = fill.RT().offsetMax = Vector2.zero;
            knob.RT().anchorMin = new Vector2(0, 0.44f); knob.RT().anchorMax = new Vector2(1, 0.44f); knob.RT().sizeDelta = new Vector2(28, 43); knob.RT().anchoredPosition = Vector2.zero;
            sl.value = 0.56f;
            Txt(zoom.RT(), "Minus", F, "57:60", A.B, null, H.C);
            // photo frame corners
            (float x, float y, float rot)[] corners = { (666, 245, 0), (1125, 245, -90), (1125, 705, 180), (666, 705, 90) };
            foreach (var c in corners)
            {
                var img = Icon(root, "Corner", c.x, c.y, 129, 129, A.C, "frame_corner");
                img.RT().localEulerAngles = new Vector3(0, 0, c.rot);
            }
            Icon(root, "Center", 934, 514, 53, 52, A.C, "photo_center");
            var cam = Icon(root, "Btn_Shoot", 1569, 726, 242, 242, A.BR, "camera");
            Clickable(cam);
            return root.gameObject;
        }

        static float tabRadius = 28f;   // Figma: 28 on the quest window, 25.6 on the storage box

        static SoftRect Tab(RectTransform p, string name, float x, float y, float w, float h, Color fill, string frame, string id, string key, params object[] args)
        {
            var t = Shape(p, name, x, y, w, h, A.T, fill, tabRadius);
            Txt(t.RT(), "Label", frame, id, A.T, key, H.C, w - 32, args);
            return t;
        }

        static GameObject Quest()
        {
            const string F = "59-1446";
            var root = Root("UI2_Quest"); tabRadius = 28f;
            var win = Node("Window", root, 225, 40, 1531, 984, A.C);
            Tab(win, "Tab_Story", 970, 42, 224, 102, Cream, F, "59:1451", "quest.tab.story");
            Tab(win, "Tab_Done", 746, 42, 224, 102, Cream, F, "59:1461", "quest.tab.done", "1");
            Tab(win, "Tab_Active", 522, 42, 224, 102, Pink, F, "59:1460", "quest.tab.active", "4");
            var panel = Shape(win, "Panel", 225, 118, 1493, 906, A.TL, Cream, 28f).Border(Edge, 3).Seam(Stitch, 240, 134, 1463, 874, 25.5f, 230, 10.05f);
            Tab(win, "Tab_Title", 257, 40, 247, 114, Sky, F, "59:1459", "quest.title");
            var close = Shape(win, "Btn_Close", 1650, 80, 106, 106, A.TL, Pink, 53);
            Icon(close.RT(), "X", 1681, 111, 45, 45, A.C, "close_x"); Clickable(close);
            // list (cards sit where Figma has them; the game lays out more of them below)
            var view = Node("List", panel.RT(), 289, 190, 1370, 800, A.TL);
            var sr = view.gameObject.AddComponent<ScrollRect>(); sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 30;
            view.gameObject.AddComponent<RectMask2D>();
            var content = Node("Content", view, 289, 190, 1370, 800, A.T);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(0, 800);
            sr.content = content; sr.viewport = view;
            (Color circle, Color btn, string btnKey, bool done, float y, string title, string initial, string desc, string action, string reward)[] rows =
            {
                (Pink, Sky, "quest.accept", false, 200, "59:1475", "59:1476", "59:1477", "59:1480", "59:1482"),
                (PinkPale, C("#FFD65C"), "quest.go", false, 401, "59:1488", "59:1489", "59:1490", "59:1492", "59:1494"),
                (C("#FFF492"), Sky, "quest.accept", false, 601, "59:1500", "59:1501", "59:1502", "59:1524", "59:1507"),
                (C("#ECE3D6"), Sky, "", true, 802, "59:1523", "59:1517", "59:1518", "59:1520", null),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i]; float y = r.y;
                var card = Shape(content, "Card" + i, 290, y, 1356, 174, A.TL, r.done ? C("#F6F0E7") : Color.white, 28.6f).Seam(Color.white, 305, y + 14, 1325, 146, 26.1f, 142, 10.06f).Shadowed(0.5f);
                var av = Shape(card.RT(), "Avatar", 321, new[] { 227f, 430f, 628f, 829f }[i], 120, 120, A.L, r.circle, 60);
                Txt(av.RT(), "Initial", F, r.initial, A.C, null, H.C);
                Txt(card.RT(), "Title", F, r.title, A.TL, "quest.sample.title", H.L, r.done ? 860 : 595);
                Txt(card.RT(), "Desc", F, r.desc, A.TL, "quest.sample.desc", H.L, r.done ? 860 : 595, i == 1 ? "@name.mongi" : "@name.tosun");   // as Figma writes them
                if (r.done)
                {
                    Icon(card.RT(), "Heart", 1360, y + 70, 48, 42, A.R, "heart");
                    Txt(card.RT(), "State", F, r.action, A.R, "quest.done", H.L, 160);
                    continue;
                }
                Icon(card.RT(), "Coin", 1096, new[] { 266f, 471f, 667f }[i], 43, 43, A.R, "coin");
                Txt(card.RT(), "Reward", F, r.reward, A.R, null, H.L);
                var b = Shape(card.RT(), "Btn_Action", 1304, new[] { 239f, 445f, 640f }[i], 296, 94, A.R, r.btn, 47);
                Shape(b.RT(), "Shine", 1330, y + 49, 25, 10, A.TL, Color.white, 5);
                Txt(b.RT(), "Label", F, r.action, A.C, r.btnKey, H.C, 260);
                Clickable(b);
            }
            var track = Shape(panel.RT(), "Scrollbar", 1674, 224, 16, 737, A.TL, Sand, 8);
            var thumb = Shape(track.RT(), "Thumb", 1674, 227, 16, 84, A.T, C("#B68892"), 8);
            var bar = track.gameObject.AddComponent<Scrollbar>(); bar.direction = Scrollbar.Direction.TopToBottom; bar.handleRect = thumb.RT(); bar.targetGraphic = thumb; bar.transition = Selectable.Transition.None;
            thumb.RT().anchorMin = new Vector2(0, 0.88f); thumb.RT().anchorMax = Vector2.one; thumb.RT().offsetMin = thumb.RT().offsetMax = Vector2.zero; thumb.raycastTarget = true;
            sr.verticalScrollbar = bar; sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            return root.gameObject;
        }

        static GameObject Storage()
        {
            const string F = "57-307";
            var root = Root("UI2_Storage"); tabRadius = 25.6f;
            var win = Node("Window", root, 249, 46, 1535, 985, A.C);
            Tab(win, "Tab_Accessory", 994, 49, 224, 102, Cream, F, "57:327", "storage.tab.accessory");
            Tab(win, "Tab_Furniture", 770, 49, 224, 102, Cream, F, "57:326", "storage.tab.furniture");
            Tab(win, "Tab_All", 546, 49, 224, 102, Pink, F, "57:325", "storage.tab.all");
            var panel = Shape(win, "Panel", 249, 125, 1494, 906, A.TL, Cream, 27.2f).Border(Edge, 3).Seam(Stitch, 264, 140, 1464, 874, 23.9f, 232, 9.94f);
            var pat = Picture(panel.RT(), "Pattern", 251, 127, 1490, 902, A.Fill, "Assets/_Project/Resources/UI2/Storage/pattern.png", false);
            pat.color = new Color(1, 1, 1, 0.18f);
            Tab(win, "Tab_Title", 281, 46, 247, 114, Sky, F, "57:324", "storage.title");
            var close = Shape(win, "Btn_Close", 1678, 78, 106, 106, A.TL, Pink, 53);
            Icon(close.RT(), "X", 1709, 108, 45, 45, A.C, "close_x"); Clickable(close);
            var search = Shape(panel.RT(), "Search", 1192, 161, 196, 55, A.TR, Color.white, 27.7f).Border(Pink2, 5);
            Icon(search.RT(), "Icon", 1206, 174, 32, 32, A.L, "search");
            var sort = Shape(panel.RT(), "Btn_Sort", 1405, 161, 196, 55, A.TR, Pink2, 27.7f);
            Txt(sort.RT(), "Label", F, "57:332", A.L, "storage.sort", H.L, 128);
            Icon(sort.RT(), "Arrow", 1553, 178, 33, 28, A.R, "sort_arrow"); Clickable(sort);
            var view = Node("Grid", panel.RT(), 284, 228, 1386, 790, A.TL);
            var sr = view.gameObject.AddComponent<ScrollRect>(); sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 30;
            view.gameObject.AddComponent<RectMask2D>();
            var content = Node("Content", view, 284, 228, 1386, 790, A.T);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(0, 790);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(246, 292); grid.spacing = new Vector2(19, 24); grid.padding = new RectOffset(20, 0, 12, 12); grid.constraint = GridLayoutGroup.Constraint.Flexible;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = content; sr.viewport = view;
            // the two cards Figma shows, with the game's own pieces (포근 의자 · 장난감 상자) and their pictures
            (string item, string count, string name, string use)[] items =
            {
                ("Fur_Armchair", "57:349", "57:350", "57:348"), ("Fur_ToyBox", "57:355", "57:356", "57:354"), (null, null, null, null), (null, null, null, null), (null, null, null, null),
            };
            for (int i = 0; i < items.Length; i++)
            {
                float x = new[] { 304f, 570f, 835f, 1099f, 1372f }[i], y = 240;
                var card = Shape(content, "Item" + i, x, y, 246, 292, A.TL, Color.white, 28.6f).Shadowed(0.5f);
                if (items[i].item == null) continue;
                var badge = Shape(card.RT(), "Count", x - 20, y - 7, 69, 35, A.TL, Pink2, 17.7f);
                Txt(badge.RT(), "Text", F, items[i].count, A.C, null, H.C);
                // the game's own names (포근 의자 · 장난감 상자), both languages
                Txt(card.RT(), "Name", F, items[i].name, A.T, items[i].item == "Fur_Armchair" ? "item.cozy_chair" : "item.toy_box", H.C, 220);
                var pic = Shape(card.RT(), "PicBack", x + 24, y + 51, 196, 155, A.T, PinkPale, 19.4f);
                var img = Node("Pic", pic.RT(), x + 34, y + 55, 176, 147, A.C).gameObject.AddComponent<Image>();
                img.sprite = Sprite(ItemIconBaker.PathOf(items[i].item)); img.preserveAspect = true; img.raycastTarget = false;
                var use = Shape(card.RT(), "Btn_Use", x + 55, y + 226, 136, 41, A.B, Pink2, 17.7f);
                Txt(use.RT(), "Label", F, items[i].use, A.C, "storage.use", H.C, 124);
                Clickable(use);
            }
            var track = Shape(panel.RT(), "Scrollbar", 1681, 227, 16, 683, A.TR, Sand, 8);
            var thumb = Shape(track.RT(), "Thumb", 1681, 227, 16, 84, A.T, C("#B68892"), 8);
            var bar = track.gameObject.AddComponent<Scrollbar>(); bar.direction = Scrollbar.Direction.TopToBottom; bar.handleRect = thumb.RT(); bar.targetGraphic = thumb; bar.transition = Selectable.Transition.None;
            thumb.RT().anchorMin = new Vector2(0, 0.88f); thumb.RT().anchorMax = Vector2.one; thumb.RT().offsetMin = thumb.RT().offsetMax = Vector2.zero; thumb.raycastTarget = true;
            sr.verticalScrollbar = bar; sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            return root.gameObject;
        }

        // ------------------------------------------------------------------ build + preview scene
        static readonly (string name, string frame, System.Func<GameObject> make)[] Screens =
        {
            ("Settings", "58-1007", Settings),
            ("Intake", "57-3", Intake),
            ("VillageHud", "59-1525", VillageHud),
            ("HouseInvite", "59-1327", HouseInvite),
            ("RideInvite", "59-1146", RideInvite),
            ("Title", "57-726", Title),
            ("RepairHud", "57-829", RepairHud),
            ("Photo", "57-24", Photo),
            ("Quest", "59-1446", Quest),
            ("Storage", "57-307", Storage),
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
            UiV2Wire.WireAll();            // D-040: tie the screens to the game's own again (a rebuild makes them fresh)
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

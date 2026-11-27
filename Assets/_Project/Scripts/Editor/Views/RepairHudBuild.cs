using Mongle.UI2;
using Mongle.Views;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Mongle.EditorTools.ViewKit;

namespace Mongle.EditorTools
{
    /// <summary>
    /// The 11-26 repair screen (Figma 인게임2 88:1704 / 인게임3 88:1767) as a prefab, Assets/_UI수정/2_수선점/RepairHud.prefab,
    /// placed in the RepairShop scene. Square buttons, the notebook pin and the row icons are the Figma vectors (Art/UI_1126);
    /// plates, rows, the tool bar and the hint bubble are <see cref="SoftRect"/> shapes with the SVG's values so they can grow.
    /// </summary>
    public static class RepairHudBuild
    {
        public const string PrefabPath = UiEditFolder.RepairDir + "/RepairHud.prefab";

        static readonly Color Sheet = Hex("#FAF0DF"), Pill = Hex("#F9EDDD"), Stitch = Hex("#C6DFE8"), Blue = Hex("#A0DCF8"),
            Icon = Hex("#816253"), Minus = Hex("#7D5D51"), Yellow = Hex("#FFEC76");
        static readonly Color Shadow = new Color(0f, 0f, 0f, 0.5f);

        public static GameObject Make()
        {
            var root = Root("RepairHud", 10);
            var v = root.gameObject.AddComponent<RepairHudView>();

            // the corner buttons every screen shares (Figma 통일본 115:1254): ◀ node 99.5 at (50, 48), the gear 99.52 at (1791, 48)
            v.backButton = PictureButton(root, "Btn_Back", "Common/back_corner.png", 48.5f, 46.5f, 104.5f, 108f, TL);
            v.gearButton = PictureButton(root, "Btn_Gear", "Village/gear.png", 1788.5f, 45.5f, 107f, 107f, TR);

            // the hint banner: the cream pill of Figma 115:1573 (user 2026-11-27: "여기 여백을 … 이렇게 하고 폰트사이즈도"): 97 tall,
            // its middle at y 97.75, round ends, 38 px words 62 in from its ends; in the middle of the screen
            var banner = Node(root, "Banner", 640.5f, 49.25f, 639f, 97f, T);
            banner.pivot = new Vector2(0.5f, 0.5f); banner.anchoredPosition = new Vector2(0f, -97.75f);
            Shape(banner.gameObject, Pill, Vector4.one * 48.5f);
            var bt = Words(banner, "Text", "해냈어요!", 38f, Brown, 0f, 0f, 639f, 97f, C, HorizontalAlignmentOptions.Center, null, new Vector2(639f, 97f));
            Stretch(bt.rectTransform, 0f, 0f);
            v.banner = banner; v.bannerText = bt; v.bannerPad = 62f; v.bannerMinWidth = 300f;   // hugs short lines too

            // zoom at the right edge (115:1573: nodes 99.52 at (1791, 441) / (1791, 569), the pictures 2.5 out; "-" 60 px at (1825.21, 579.11))
            v.zoomIn = PictureButton(root, "Btn_ZoomIn", "Village/zoom_plus.png", 1788.5f, 438.5f, 107f, 107f, R);
            v.zoomOut = PictureButton(root, "Btn_ZoomOut", "Village/zoom_minus.png", 1788.5f, 566.5f, 107f, 107f, R);
            Words(v.zoomOut.transform, "Minus", "-", 60f, Minus, 1825.21f - 1788.5f, 579.11f - 566.5f, 35f, 78f, TL, HorizontalAlignmentOptions.Center, null, new Vector2(107f, 107f));

            Notebook(root, v);
            ToolBar(root, v);
            return root.gameObject;
        }

        static void Stretch(RectTransform rt, float side, float up)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(side, up); rt.offsetMax = new Vector2(-side, up);
        }

        // ---------------- 진료 수첩 ----------------
        static void Notebook(RectTransform root, RepairHudView v)
        {
            var size = new Vector2(430.18f, 543.16f);
            // in the middle of the screen's height down the left column, under ◀ (user 2026-11-27: "진료수첩을 좀 밑으로 … Column줄에
            // Center로"): pinned to the left middle, it grows both ways with its rows
            var nb = Node(root, "Notebook", 47.45f, 540f - size.y / 2f, size.x, size.y, L);
            var sheet = Shape(nb.gameObject, Sheet, Vector4.one * 28f); sheet.raycastTarget = true;
            sheet.stitch = true; sheet.stitchColor = Stitch; sheet.stitchWidth = 3f; sheet.dash = 14.59f; sheet.gap = 14.6f; sheet.roundCaps = true;
            sheet.stitchPad = new Vector4(11.95f, 15.09f, 11.95f, 15.09f); sheet.stitchRadius = 28f; sheet.stitchPeriod = 29.19f;
            // rows: one column under the title, 109.1 apart (Figma), the sheet as tall as its rows
            var col = nb.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(32, 0, 91, 30); col.spacing = 14.95f; col.childAlignment = TextAnchor.UpperLeft;
            col.childControlWidth = col.childControlHeight = false; col.childForceExpandWidth = col.childForceExpandHeight = false;
            var fit = nb.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var title = Words(nb, "Title", "진료 수첩", 48f, Brown, 15.09f, 20.88f, 400f, 62f, T, HorizontalAlignmentOptions.Center, "repair.notebook", size);
            IgnoreLayout(title);
            var pin = Picture(nb, "Pin", "Repair/nb_pin.png", -0.95f, -49.61f, 85f, 126.5f, TL, size);
            IgnoreLayout(pin);

            // one row (the game copies it for each damage): plate with a soft shadow and a white stitch, the icon square, the name
            var rs = new Vector2(367.89f, 94.15f);
            var row = Node(nb, "Row", 31.52f, 91.25f, rs.x, rs.y, TL, size);
            var plate = Shape(row.gameObject, Blue, Vector4.one * 28.65f);
            plate.shadowColor = Shadow; plate.shadowOffset = new Vector2(1f, -1f); plate.shadowBlur = 1f;
            plate.stitch = true; plate.stitchColor = Color.white; plate.stitchWidth = 3f; plate.dash = 9.74f; plate.gap = 9.74f; plate.roundCaps = true;
            plate.stitchPad = new Vector4(9.16f, 7.5f, 9.16f, 7.5f); plate.stitchRadius = 28.68f; plate.stitchPeriod = 19.48f; plate.stitchShadow = Shadow;
            var mark = Picture(row, "Mark", "Repair/ic_yellow.png", 20.53f, 17.23f, 57f, 57f, TL, rs);
            var q = Words(mark.transform, "Text", "!", 46.77f, Icon, 0f, 0f, 57f, 57f, C, HorizontalAlignmentOptions.Center, null, new Vector2(57f, 57f));
            q.rectTransform.anchoredPosition = new Vector2(0f, 2.46f);
            Picture(row, "Check", "Repair/ic_check.png", 20.53f, 17.14f, 57f, 57f, TL, rs).gameObject.SetActive(false);
            var name = Words(row, "Name", "귀의 가시", 38f, Brown, 102.3f, 22.58f, 250f, 49f, TL, HorizontalAlignmentOptions.Left, null, rs);
            name.enableAutoSizing = true; name.fontSizeMin = 22f; name.fontSizeMax = 38f;
            v.notebook = nb; v.rowTemplate = row;
        }

        // ---------------- 도구 막대 ----------------
        static void ToolBar(RectTransform root, RepairHudView v)
        {
            // the bar with its arrows, 53 right of the middle (Figma), 60 above the bottom edge
            var gs = new Vector2(1123f, 215.43f);
            var group = Node(root, "ToolBar", 451.5f, 804.28f, gs.x, gs.y, B);
            var ps = new Vector2(878.52f, 215.43f);
            var panel = Node(group, "Panel", 122.31f, 0f, ps.x, ps.y, TL, gs);
            var sheet = Shape(panel.gameObject, Sheet, Vector4.one * 34.56f); sheet.raycastTarget = true;
            sheet.stitch = true; sheet.stitchColor = Stitch; sheet.stitchWidth = 3f; sheet.dash = 15.2f; sheet.gap = 15.2f; sheet.roundCaps = true;
            sheet.stitchPad = new Vector4(9.49f, 10.93f, 9.48f, 10.93f); sheet.stitchRadius = 34.56f; sheet.stitchPeriod = 30.4f;
            var row = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(22, 0, 28, 0); row.spacing = 22.57f; row.childAlignment = TextAnchor.UpperLeft;
            row.childControlWidth = row.childControlHeight = false; row.childForceExpandWidth = row.childForceExpandHeight = false;
            v.slots = new Button[4];
            for (int i = 0; i < 4; i++) v.slots[i] = Slot(panel, i);
            v.prevPage = PictureButton(group, "Btn_Prev", "Common/arrow_left.png", 0f, 56.22f, 103f, 103f, TL, gs);
            v.nextPage = PictureButton(group, "Btn_Next", "Common/arrow_right.png", 1020f, 56.22f, 103f, 103f, TL, gs);
            Tip(group, v, gs);
            v.toolBar = group;
        }

        static Button Slot(Transform panel, int i)
        {
            var ss = new Vector2(190.9f, 160.62f);
            var s = Node(panel, "Slot" + i, 0f, 0f, ss.x, ss.y, TL, ss);
            var face = Shape(s.gameObject, Color.white, Vector4.one * 28.65f); face.raycastTarget = true;
            face.shadowColor = Shadow; face.shadowOffset = new Vector2(1f, -1f); face.shadowBlur = 1f;
            var b = s.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = face;
            s.gameObject.AddComponent<PressScale>();
            var icon = Node(s, "Icon", 35.45f, 20.31f, 120f, 120f, C, ss).gameObject.AddComponent<Image>();
            icon.preserveAspect = true; icon.raycastTarget = false; icon.enabled = false;
            var name = Words(s, "Name", "", 30f, Brown, 10f, 55f, 170.9f, 50f, C, HorizontalAlignmentOptions.Center, null, ss);
            name.enableAutoSizing = true; name.fontSizeMin = 18f; name.fontSizeMax = 30f;
            // the outline of the hovered tool (Figma 88:1863: 5 px #A0DCF8 around the tile)
            var o = Node(s, "Outline", -2.5f, -2.5f, ss.x + 5f, ss.y + 5f, C, ss);
            o.anchorMin = Vector2.zero; o.anchorMax = Vector2.one; o.offsetMin = new Vector2(-2.5f, -2.5f); o.offsetMax = new Vector2(2.5f, 2.5f);
            var line = Shape(o.gameObject, new Color(1f, 1f, 1f, 0f), Vector4.one * 31.15f, 5f, Blue);
            o.gameObject.SetActive(false);
            return b;
        }

        /// <summary>The hint over a tool (Figma 88:1822–1826): a white pill, a yellow band, the "!" mark, the words and a tail.</summary>
        static void Tip(RectTransform group, RepairHudView v, Vector2 gs)
        {
            var ts = new Vector2(234.46f, 47.98f);
            var tip = Node(group, "Tip", 540.77f, -39.57f, ts.x, ts.y, TL, gs);
            Shape(tip.gameObject, Color.white, Vector4.one * 23.99f);
            var band = Node(tip, "Band", 34.82f, 1.56f, 197.66f, 44.51f, TL, ts);
            band.anchorMin = Vector2.zero; band.anchorMax = Vector2.one; band.offsetMin = new Vector2(34.82f, 1.91f); band.offsetMax = new Vector2(-1.98f, -1.56f);
            Shape(band.gameObject, Yellow, new Vector4(4.98f, 22.26f, 22.26f, 4.98f));
            Picture(tip, "Mark", "Repair/tip_icon.png", 16.23f, 9.79f, 8.5f, 28.5f, TL, ts);
            var text = Words(tip, "Text", "도구 설명", 18f, Brown, 47.6f, 10.62f, 160f, 23f, TL, HorizontalAlignmentOptions.Left, null, ts);
            Picture(tip, "Tail", "Repair/tip_tail.png", 170.73f, 45.29f, 17.5f, 15.5f, TL, ts);
            foreach (var g in tip.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            tip.gameObject.SetActive(false);
            v.tip = tip; v.tipText = text; v.tipTailX = 179.48f; v.tipGap = 19.77f;
        }

        public static GameObject Build()
        {
            var go = Make();
            try { return PrefabUtility.SaveAsPrefabAsset(go, PrefabPath); }
            finally { Object.DestroyImmediate(go); }
        }

        public static void AddTo(UnityEngine.SceneManagement.Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.name = "RepairHud";
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}

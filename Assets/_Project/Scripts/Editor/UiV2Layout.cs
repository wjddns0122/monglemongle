using System.Collections.Generic;
using Mongle.UI2;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.EditorTools
{
    /// <summary>
    /// Rows and columns for the Figma screens (Assets/_UI수정). Pieces that belong together — the buttons down the left, the coins
    /// and settings at the top right, the toolbars — go into one layout group pinned to their corner with the same screen margin
    /// (<see cref="Margin"/>), so they line up on every screen shape and close up when the game hides one of them. Stitched
    /// shapes also get the design's dash spacing (<see cref="SoftRect.stitchPeriod"/>), so a plate that grows keeps it.
    /// Safe to run again: groups that exist are only tuned, and nothing else about the look is touched.
    /// "게임 화면과 다시 연결하기" (<see cref="UiV2Wire"/>) runs this first.
    /// </summary>
    public static class UiV2Layout
    {
        /// <summary>Distance from the screen's edges to the corner groups.</summary>
        public const float Margin = 40f;

        static readonly string[] Screens = { "Title", "Intake", "RepairHud", "VillageHud", "Quest", "Storage", "RideInvite", "Photo", "Settings", "HouseInvite" };

        [MenuItem("Mongle/UI v2/줄·열로 정리하기")]
        public static void ApplyAll()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[UiV2Layout] stop play mode first"); return; }
            int n = 0;
            foreach (var name in Screens)
            {
                var path = UiEditFolder.FrontPath(name);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try { Apply(name, root.transform); PrefabUtility.SaveAsPrefabAsset(root, path); n++; }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Debug.Log("[UiV2Layout] " + n + " screens in rows / columns");
        }

        public static void Apply(string name, Transform root)
        {
            KeepStitchSpacing(root);
            switch (name)
            {
                case "VillageHud": Village(root); break;
                case "RepairHud": Repair(root); break;
                case "RideInvite": Ride(root); break;
                case "Quest": Window(root, "Tab_Active", "Tab_Done", "Tab_Story"); break;
                case "Storage": Window(root, "Tab_All", "Tab_Furniture", "Tab_Accessory"); StorageCards(root); break;
            }
        }

        /// <summary>
        /// A storage card is a column — name, picture, button — and its count sits on the picture's corner: at the card's corner it
        /// ran into longer names ("1개해바라기 화분"). The name gets the card's whole width.
        /// </summary>
        static void StorageCards(Transform r)
        {
            var content = r.Find("Window/Panel/Grid/Content"); if (content == null) return;
            foreach (Transform item in content)
            {
                var pic = item.Find("PicBack") as RectTransform; if (pic == null) continue;
                if (item.Find("Count") is RectTransform count)
                {
                    count.SetParent(pic, false);
                    count.anchorMin = count.anchorMax = count.pivot = new Vector2(0f, 1f); count.anchoredPosition = new Vector2(8f, -8f);
                }
                if (item.Find("Name") is RectTransform name && !Mathf.Approximately(name.anchorMax.x, 1f))
                {
                    float y = name.anchoredPosition.y, h = name.sizeDelta.y;
                    name.anchorMin = new Vector2(0f, name.anchorMin.y); name.anchorMax = new Vector2(1f, name.anchorMax.y);
                    name.anchoredPosition = new Vector2(0f, y); name.sizeDelta = new Vector2(-24f, h);   // 12 each side
                }
            }
        }

        /// <summary>Height of the top row of a HUD: every corner group and the banner share its middle line.</summary>
        public const float TopRow = 103f;

        /// <summary>
        /// A window (quests, storage): its tabs are one row beside the title (the title stays on its own, drawn over the sheet;
        /// the tabs tuck under it), and the sheet gets a line for "nothing here yet" that the game's own words fill.
        /// </summary>
        static void Window(Transform r, params string[] tabNames)
        {
            var w = r.Find("Window"); if (w == null) return;
            var row = w.Find("TabRow") as RectTransform;
            if (row == null)
            {
                var first = w.Find(tabNames[0]) as RectTransform; if (first == null) return;
                float left = first.anchoredPosition.x - first.pivot.x * first.rect.width, top = first.anchoredPosition.y + (1f - first.pivot.y) * first.rect.height;
                row = new GameObject("TabRow", typeof(RectTransform)).GetComponent<RectTransform>();
                row.SetParent(w, false);
                int at = int.MaxValue; foreach (var n in tabNames) if (w.Find(n) is Transform t) at = Mathf.Min(at, t.GetSiblingIndex());
                row.SetSiblingIndex(at);   // where the tabs were drawn: under the sheet
                row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f); row.pivot = new Vector2(0f, 1f); row.anchoredPosition = new Vector2(left, top);
                foreach (var n in tabNames) if (w.Find(n) is Transform t) t.SetParent(row, false);
            }
            var lg = Get<HorizontalLayoutGroup>(row);
            Tune(lg, TabGap, TextAnchor.LowerLeft, new RectOffset(), false);
            var fit = Get<ContentSizeFitter>(row); fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            WindowTop(w, row);
            LayoutRebuilder.ForceRebuildLayoutImmediate(row);

            if (w.Find("Panel") is RectTransform panel && panel.Find("Empty") == null)
            {
                var style = w.GetComponentInChildren<TMPro.TMP_Text>(true);
                var go = new GameObject("Empty", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                go.transform.SetParent(panel, false);
                var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(1100f, 200f);
                var t = go.GetComponent<TMPro.TextMeshProUGUI>();
                if (style != null) { t.font = style.font; t.fontSharedMaterial = style.fontSharedMaterial; t.color = style.color; }
                t.text = ""; t.fontSize = 40f; t.enableAutoSizing = true; t.fontSizeMin = 22f; t.fontSizeMax = 40f;
                t.horizontalAlignment = TMPro.HorizontalAlignmentOptions.Center; t.verticalAlignment = TMPro.VerticalAlignmentOptions.Middle;
                t.textWrappingMode = TMPro.TextWrappingModes.Normal; t.raycastTarget = false;
            }
        }

        // ---------------- the window's top (user 2026-11-24: "위에 부분 자연스럽게") ----------------

        /// <summary>Room between two tabs, and between the title and the first tab.</summary>
        public const float TabGap = 12f, TitleGap = 14f;
        /// <summary>The title stands this much above the tabs' top line (the chosen tab rises <see cref="TabRise"/>).</summary>
        public const float TitleRise = 14f, TabRise = 10f, TabHeight = 100f, TabEdge = 3f, TabRadius = 26f;
        /// <summary>A tab not chosen: a shade deeper than the sheet, so it reads as a tab behind it, not a piece of it.</summary>
        public static readonly Color TabOff = new Color(0.945f, 0.878f, 0.792f, 1f);
        public static readonly Color TabEdgeOn = new Color(1f, 0.78f, 0.84f, 1f), TabEdgeOff = new Color(0.769f, 0.875f, 0.906f, 1f), TitleEdge = new Color(0.55f, 0.78f, 0.9f, 1f);

        /// <summary>
        /// The Figma window top had the title box hanging over the sheet's edge and the tabs butting into each other. Now the title
        /// is the first and tallest tab and the tabs stand apart on one base line, all with their feet tucked under the sheet and
        /// edged like it; the chosen tab is a little taller (<see cref="UiTabRaise"/>), the others a shade deeper than the sheet.
        /// Same result when run again.
        /// </summary>
        static void WindowTop(Transform w, RectTransform row)
        {
            var panel = w.Find("Panel") as RectTransform; if (panel == null) return;
            float sheet = -(panel.anchoredPosition.y + (1f - panel.pivot.y) * panel.rect.height);   // the sheet's top edge below the window's top
            if (w.Find("Tab_Title") is RectTransform title)
            {
                if (title.GetSiblingIndex() > panel.GetSiblingIndex()) title.SetSiblingIndex(panel.GetSiblingIndex());   // under the sheet, like the tabs
                float shown = sheet + TitleRise;
                title.anchoredPosition = new Vector2(title.anchoredPosition.x, TitleRise + (title.pivot.y - 1f) * (shown + 40f));
                title.sizeDelta = new Vector2(title.sizeDelta.x, shown + 40f);
                if (title.TryGetComponent<SoftRect>(out var ts)) { ts.borderWidth = TabEdge; ts.borderColor = TitleEdge; ts.SetAll(); }
                if (title.Find("Label") is RectTransform tl) tl.anchoredPosition = new Vector2(tl.anchoredPosition.x, -(shown - tl.rect.height) * 0.5f - 2f);
                float right = title.anchoredPosition.x + (1f - title.pivot.x) * title.rect.width;
                row.anchoredPosition = new Vector2(right + TitleGap, row.anchoredPosition.y);
            }
            float rowTop = -row.anchoredPosition.y;   // the tallest (chosen) tab's top
            for (int i = 0; i < row.childCount; i++)
            {
                if (!(row.GetChild(i) is RectTransform tab) || !tab.TryGetComponent<SoftRect>(out var s)) continue;
                bool chosen = i == 0;   // the design shows the first tab chosen
                s.borderWidth = TabEdge; s.radius = new Vector4(TabRadius, TabRadius, TabRadius, TabRadius);
                if (!chosen) s.color = TabOff;
                var raise = Get<UiTabRaise>(tab);
                raise.onHeight = TabHeight + TabRise; raise.offHeight = TabHeight;
                float labelH = tab.Find("Label") is RectTransform lb ? lb.rect.height : 52f;
                // the words in the middle of the part that shows above the sheet
                raise.onLabelTop = (sheet - rowTop - labelH) * 0.5f + 2f;
                raise.offLabelTop = (sheet - rowTop - TabRise - labelH) * 0.5f + 2f;
                raise.onEdge = TabEdgeOn; raise.offEdge = TabEdgeOff;
                raise.Apply(chosen);
                s.SetAll();
            }
        }

        /// <summary>
        /// A friend's dialog: the answers (the game's choices — two, or twelve months, or 31 days — and its bottom buttons such as
        /// "좋아, 할게!" / "나중에") are one column right of the speech bubble, growing upwards from where "나중에" was drawn, so
        /// nothing is pushed below the screen. Choices sit in a grid (<see cref="UiLinkList.gridWidth"/> keeps it clear of the bubble).
        /// </summary>
        static void Ride(Transform r)
        {
            var answers = r.Find("Answers") as RectTransform;
            if (answers == null)
            {
                var yes = r.Find("Btn_Yes") as RectTransform; var footer = r.Find("Footer") as RectTransform;
                if (footer == null && r.Find("Btn_Later") is RectTransform loose)
                {
                    // a freshly built screen: the bottom buttons get their own holder (a second list needs its own object)
                    footer = new GameObject("Footer", typeof(RectTransform)).GetComponent<RectTransform>();
                    footer.SetParent(r, false); footer.SetSiblingIndex(loose.GetSiblingIndex());
                    footer.anchorMin = Vector2.zero; footer.anchorMax = Vector2.one; footer.offsetMin = footer.offsetMax = Vector2.zero;
                    loose.SetParent(footer, true);
                }
                var later = footer != null ? footer.Find("Btn_Later") as RectTransform : null;
                if (yes == null || footer == null || later == null) return;
                float x = yes.anchoredPosition.x, bottom = later.anchoredPosition.y - later.pivot.y * later.rect.height;
                answers = new GameObject("Answers", typeof(RectTransform)).GetComponent<RectTransform>();
                answers.SetParent(r, false); answers.SetSiblingIndex(yes.GetSiblingIndex());
                Pin(answers, new Vector2(0.5f, 0f), new Vector2(x, bottom));
                var choices = new GameObject("Choices", typeof(RectTransform)).GetComponent<RectTransform>();
                choices.SetParent(answers, false);
                yes.SetParent(choices, false);
                footer.SetParent(answers, false);
                footer.anchorMin = footer.anchorMax = new Vector2(0.5f, 1f); footer.pivot = new Vector2(0.5f, 1f);
            }
            else Pin(answers, new Vector2(0.5f, 0f), answers.anchoredPosition);

            var col = Get<VerticalLayoutGroup>(answers);
            Tune(col, 31.5f, TextAnchor.LowerCenter, new RectOffset(), true);
            col.childControlHeight = true;
            var fit = Get<ContentSizeFitter>(answers); fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (answers.Find("Choices") is RectTransform ch)
            {
                var grid = Get<GridLayoutGroup>(ch);
                var yes = ch.Find("Btn_Yes") as RectTransform;
                if (yes != null && grid.cellSize == new Vector2(100f, 100f)) grid.cellSize = yes.rect.size;   // a new grid: the design's button
                grid.spacing = new Vector2(12f, 18f); grid.childAlignment = TextAnchor.UpperCenter;
                grid.startCorner = GridLayoutGroup.Corner.UpperLeft; grid.startAxis = GridLayoutGroup.Axis.Horizontal;
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; if (grid.constraintCount < 1) grid.constraintCount = 1;
                if (yes != null) Shrinkable(yes.Find("Label") as RectTransform, 0.16f);
            }
            if (answers.Find("Footer") is RectTransform ft)
            {
                var lg = Get<VerticalLayoutGroup>(ft);
                Tune(lg, 18f, TextAnchor.UpperCenter, new RectOffset(), false);
                if (ft.Find("Btn_Later") is RectTransform later) Shrinkable(later.Find("Label") as RectTransform, 0.08f);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(answers);
        }

        /// <summary>A button's words keep a share of its width on each side and get smaller in a narrow button.</summary>
        static void Shrinkable(RectTransform label, float side)
        {
            if (label == null) return;
            float y = label.anchoredPosition.y, h = label.sizeDelta.y;
            label.anchorMin = new Vector2(side, label.anchorMin.y); label.anchorMax = new Vector2(1f - side, label.anchorMax.y);
            label.anchoredPosition = new Vector2(0f, y); label.sizeDelta = new Vector2(0f, h);
            if (label.TryGetComponent<TMPro.TMP_Text>(out var t))
            {
                float max = t.enableAutoSizing ? t.fontSizeMax : t.fontSize;
                t.fontSizeMax = max; t.fontSizeMin = Mathf.Min(14f, max); t.enableAutoSizing = true;
                t.horizontalAlignment = TMPro.HorizontalAlignmentOptions.Center;
            }
        }

        // ---------------- the screens ----------------

        static void Village(Transform r)
        {
            var left = Group(r, "LeftColumn", new Vector2(0f, 1f), new Vector2(Margin, -Margin), true, 24f, TextAnchor.UpperLeft, "Btn_Home", "Btn_Quest");
            // the top line is one row: home and quests side by side, as high as the coins / settings row on the right
            if (left != null)
            {
                var top0 = left.Find("TopLeftRow") as RectTransform;
                if (top0 == null)
                {
                    top0 = new GameObject("TopLeftRow", typeof(RectTransform)).GetComponent<RectTransform>();
                    top0.SetParent(left, false); top0.SetSiblingIndex(0);
                    foreach (var n in new[] { "Btn_Home", "Btn_Quest" }) if (left.Find(n) is Transform t) t.SetParent(top0, false);
                }
                Tune(Get<HorizontalLayoutGroup>(top0), 16f, TextAnchor.MiddleLeft, new RectOffset(), false);
                // a row inside the column gets a fixed box (a size fitter there would run after the column placed it)
                FixedRow(top0, new Vector2(0f, 1f), 16f);
            }
            // the game's quest tracker (no Figma design) takes the next place down the column (UiLink placeOld moves it there)
            if (left != null && left.Find("QuestTrackerSlot") == null)
            {
                var slot = new GameObject("QuestTrackerSlot", typeof(RectTransform)).GetComponent<RectTransform>();
                slot.SetParent(left, false); slot.sizeDelta = new Vector2(380f, 90f);
                LayoutRebuilder.ForceRebuildLayoutImmediate(left);
            }
            Group(r, "ZoomColumn", new Vector2(0f, 0.5f), new Vector2(Margin, 0f), true, 16f, TextAnchor.MiddleLeft, "Btn_ZoomIn", "Btn_ZoomOut");
            var top = Group(r, "TopRight", new Vector2(1f, 1f), new Vector2(-Margin, -Margin), false, 24f, TextAnchor.MiddleRight, "Coins", "Btn_Settings");
            if (top != null) FixedRow(top, new Vector2(1f, 1f), 24f);

            // the furniture / album / storage column: its own pill is the group
            if (r.Find("Toolbar") is RectTransform bar)
            {
                Pin(bar, new Vector2(1f, 0.5f), new Vector2(-Margin, 0f));
                var lg = Get<VerticalLayoutGroup>(bar);
                Tune(lg, 0f, TextAnchor.UpperCenter, new RectOffset(0, 0, 46, 46), false);
                var fit = Get<ContentSizeFitter>(bar);
                fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                // each tool is a column of its own: picture over words, both in the middle, every tool the same size
                foreach (Transform item in bar)
                {
                    if (!(item is RectTransform it) || it.GetComponent<Button>() == null) continue;
                    it.sizeDelta = new Vector2(it.sizeDelta.x, 154f);
                    Tune(Get<VerticalLayoutGroup>(it), 2f, TextAnchor.UpperCenter, new RectOffset(0, 0, 6, 0), false);
                    if (it.Find("Icon") is RectTransform icon) icon.sizeDelta = new Vector2(118f, 118f);
                    if (it.Find("Label") is RectTransform label) label.sizeDelta = new Vector2(180f, label.sizeDelta.y);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(it);
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate(bar);
            }

            Banner(r, "Banner", "Banner/Text", top != null ? top.rect.height : 103f);

            // the ride message at the bottom: a row that grows with its words
            if (r.Find("Toast") is RectTransform toast)
            {
                Pin(toast, new Vector2(0.5f, 0f), new Vector2(0f, toast.anchoredPosition.y));
                if (toast.Find("Btn_Watch") is RectTransform watch)
                {
                    var le = Get<LayoutElement>(watch);
                    le.minWidth = le.preferredWidth = watch.sizeDelta.x; le.minHeight = le.preferredHeight = watch.sizeDelta.y;
                }
                var lg = Get<HorizontalLayoutGroup>(toast);
                Tune(lg, 40f, TextAnchor.MiddleCenter, new RectOffset(64, 40, 0, 0), true);
                var fit = Get<ContentSizeFitter>(toast);
                fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; fit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                LayoutRebuilder.ForceRebuildLayoutImmediate(toast);
            }
        }

        static void Repair(Transform r)
        {
            if (r.Find("Btn_Back") is RectTransform back) Pin(back, new Vector2(0f, 1f), new Vector2(Margin, -Margin));
            float topRow = 103f;
            if (r.Find("Btn_Settings") is RectTransform gear) { Pin(gear, new Vector2(1f, 1f), new Vector2(-Margin, -Margin)); topRow = gear.rect.height; }
            Group(r, "ZoomColumn", new Vector2(1f, 0.5f), new Vector2(-Margin, 0f), true, 16f, TextAnchor.MiddleRight, "Btn_ZoomIn", "Btn_ZoomOut");
            Group(r, "ToolRow", new Vector2(0.5f, 0f), new Vector2(0f, 20f), false, 20f, TextAnchor.MiddleCenter, "Btn_ToolsLeft", "ToolBar", "Btn_ToolsRight");
            if (r.Find("Notes") is RectTransform notes) notes.anchoredPosition = new Vector2(Margin, notes.anchoredPosition.y);
            Banner(r, "Done", "Done/Text", topRow);
        }

        // ---------------- helpers ----------------

        /// <summary>
        /// A top banner: in the middle, level with the corner buttons, its words' box pinned to the plate's sides so the box's
        /// Left / Right are the padding (the game sizes the plate to the words plus that, see <see cref="UiLink"/> fitWidth).
        /// </summary>
        static void Banner(Transform r, string plate, string words, float rowHeight)
        {
            if (!(r.Find(plate) is RectTransform b)) return;
            Pin(b, new Vector2(0.5f, 1f), new Vector2(0f, -(Margin + (rowHeight - b.rect.height) * 0.5f)));
            if (!(r.Find(words) is RectTransform tr) || tr.parent != b) return;
            if (Mathf.Approximately(tr.anchorMin.x, 0f) && Mathf.Approximately(tr.anchorMax.x, 1f)) return;   // already pinned: keep its padding
            float y = tr.anchoredPosition.y, h = tr.sizeDelta.y;
            tr.anchorMin = new Vector2(0f, tr.anchorMin.y); tr.anchorMax = new Vector2(1f, tr.anchorMax.y);
            tr.anchoredPosition = new Vector2(0f, y); tr.sizeDelta = new Vector2(-96f, h);   // Left / Right 48
            if (tr.TryGetComponent<TMPro.TMP_Text>(out var tmp)) tmp.horizontalAlignment = TMPro.HorizontalAlignmentOptions.Center;
        }

        /// <summary>A row or column of <paramref name="kids"/> pinned to a corner (made the first time, tuned after).</summary>
        static RectTransform Group(Transform root, string name, Vector2 corner, Vector2 pos, bool column, float spacing, TextAnchor align, params string[] kids)
        {
            var g = root.Find(name) as RectTransform;
            if (g == null)
            {
                var found = new List<Transform>();
                foreach (var k in kids) { var t = root.Find(k); if (t != null) found.Add(t); }
                if (found.Count == 0) return null;
                g = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                g.SetParent(root, false);
                g.SetSiblingIndex(found[0].GetSiblingIndex());
                foreach (var t in found) t.SetParent(g, false);
            }
            Pin(g, corner, pos);
            HorizontalOrVerticalLayoutGroup lg = column ? Get<VerticalLayoutGroup>(g) : (HorizontalOrVerticalLayoutGroup)Get<HorizontalLayoutGroup>(g);
            Tune(lg, spacing, align, new RectOffset(), false);
            var fit = Get<ContentSizeFitter>(g);
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            LayoutRebuilder.ForceRebuildLayoutImmediate(g);
            return g;
        }

        /// <summary>
        /// A top-row group with a set box instead of a size fitter: as high as <see cref="TopRow"/> (so its middle is every top-row
        /// piece's middle) and as wide as all its pieces shown; pinned at <paramref name="pivot"/> so it never grows past its corner.
        /// A fitter here would be saved as size 0 (it drives the size) and, inside a column, run after the column placed it.
        /// </summary>
        static void FixedRow(RectTransform row, Vector2 pivot, float spacing)
        {
            if (row.TryGetComponent<ContentSizeFitter>(out var fit)) Object.DestroyImmediate(fit);
            float w = 0f; int n = 0;
            foreach (Transform c in row) if (c is RectTransform k) { w += k.sizeDelta.x; n++; }
            w += spacing * Mathf.Max(0, n - 1);
            var pos = row.anchoredPosition;
            row.pivot = pivot; row.sizeDelta = new Vector2(w, TopRow); row.anchoredPosition = pos;
            LayoutRebuilder.ForceRebuildLayoutImmediate(row);
        }

        static void Tune(HorizontalOrVerticalLayoutGroup lg, float spacing, TextAnchor align, RectOffset pad, bool controlWidth)
        {
            lg.padding = pad; lg.spacing = spacing; lg.childAlignment = align;
            lg.childControlWidth = controlWidth; lg.childControlHeight = false;
            lg.childForceExpandWidth = false; lg.childForceExpandHeight = false;
            lg.childScaleWidth = false; lg.childScaleHeight = false;
        }

        static void Pin(RectTransform t, Vector2 corner, Vector2 pos)
        {
            t.anchorMin = t.anchorMax = corner; t.pivot = corner; t.anchoredPosition = pos;
        }

        static T Get<T>(Component c) where T : Component
        {
            var x = c.GetComponent<T>();
            if (x == null) x = c.gameObject.AddComponent<T>();
            return x;
        }

        /// <summary>
        /// Remembers each stitched shape's dash spacing at its design size, so the game keeps it when the shape grows or shrinks
        /// (only where it is not set yet). The screen's root is sized like the design screen while measuring.
        /// </summary>
        static void KeepStitchSpacing(Transform root)
        {
            var rt = root as RectTransform;
            bool sized = rt != null && rt.parent == null && rt.rect.width < 1f;
            var keep = rt != null ? rt.sizeDelta : Vector2.zero;
            if (sized) rt.sizeDelta = new Vector2(1920f, 1080f);
            try
            {
                foreach (var s in root.GetComponentsInChildren<SoftRect>(true))
                {
                    if (!s.stitch || s.stitchCount <= 0 || s.stitchPeriod > 0f) continue;
                    float len = s.StitchLength();
                    if (len > 0f) s.stitchPeriod = len / s.stitchCount;
                }
            }
            finally { if (sized) rt.sizeDelta = keep; }
        }
    }
}

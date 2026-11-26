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
    /// (the D-040 rewiring tool is gone with the last 11-26 village screen)
    /// </summary>
    public static class UiV2Layout
    {
        /// <summary>Distance from the screen's edges to the corner groups.</summary>
        public const float Margin = 40f;

        static readonly string[] Screens = { "Settings", "HouseInvite" };

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

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.UI
{
    /// <summary>
    /// The 퀘스트 window (D-011): tabs 진행 중 / 완료 / 내 이야기 with one row per request (giver, what to do, progress, reward,
    /// an optional shortcut button). Pure view: the village fills the rows and handles the buttons.
    /// </summary>
    public class QuestBoard
    {
        public class Row
        {
            public string key;                       // button name suffix (QA)
            public string icon = "!";                // one syllable: the giver's initial
            public Color iconColor = UiKit.Pink;
            public string title, detail, progress;
            public string reward;                    // "+22", or empty
            public bool done;
            public string action; public Color actionColor = UiKit.Mint; public Action onAction;
        }

        public const int PerPage = 4;   // rows visible at once (the list scrolls, D-023)
        public Action OnClose; public Action<int> OnTab;
        /// <summary>Called with the window after every redraw (D-035: the screen's prefab is laid over it).</summary>
        public Action<Transform> Skin;
        public bool Visible => root.activeSelf;
        public int Tab { get; private set; }
        public IEnumerable<string> RowNames => list.Cast<Transform>().Select(t => t.name);

        // Figma D-020: the sheet is 1496x909, rows of 1359x178 (200 apart), tabs along the top, a scroll bar on the right.
        // D-023: every tab is one scrolling list (wheel / drag / the bar), no pages.
        const float RowPitch = 200.5f, RowH = 178f;
        readonly GameObject root;
        readonly RectTransform list;
        readonly ScrollRect scroll;
        readonly Text empty;
        readonly Button[] tabs = new Button[3];
        List<Row> rows = new List<Row>();
        static readonly float[] TabX = { 522.4f, 766.7f, 1005.7f }, TabY = { 42f, 39.4f, 39.4f };

        public QuestBoard(Transform canvas)
        {
            var rt = UiKit.FigRoot(canvas, "QuestBoard"); root = rt.gameObject;
            // tabs first: in the Figma they sit behind the sheet, whose top edge covers their bottoms (only the 퀘스트 tab is in front, D-024)
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                var r = FigmaLayout.Rects["q_tab_on"]; r = new Rect(TabX[i], TabY[i], r.width, r.height);
                var img = UiKit.FigRect(rt, "QuestTab" + i, r, UiKit.Edge.Middle, UiKit.Edge.Middle);
                img.sprite = UiKit.UiSprite("q_tab_off"); img.raycastTarget = true;
                var b = img.gameObject.AddComponent<Button>(); b.targetGraphic = img; b.onClick.AddListener(() => OnTab?.Invoke(k));
                // the sheet covers the tab's lower ~25: centre the label in the part that shows (tab top .. sheet edge at 118)
                var tl = UiKit.TitleLabel(img.transform, "", 40, UiKit.FigBrown2, new Vector2(0.5f, 0.5f), new Vector2(0, 12f), new Vector2(r.width, 70));
                img.rectTransform.pivot = new Vector2(0f, 0.5f); img.rectTransform.anchoredPosition += new Vector2(-r.width / 2f, 0f);   // tabs line up from the left edge
                UiKit.Slice(img); UiKit.AutoFit(img, tl, r.width, 50f, 420f);
                tabs[i] = b;
            }
            UiKit.Fig(rt, "q_panel", UiKit.Edge.Middle, UiKit.Edge.Middle, 0, 0, true);
            UiKit.Fig(rt, "q_tab_title", UiKit.Edge.Middle, UiKit.Edge.Middle);
            UiKit.FigLabel(rt, "퀘스트", 62, UiKit.FigBrown2, 381f, 102f, 250f, 90f);
            UiKit.HiddenLabel(UiKit.FigButton(rt, "q_close", () => OnClose?.Invoke(), UiKit.Edge.Middle, UiKit.Edge.Middle, 0, 0, "Btn_close"), "닫기");
            // the list: a masked viewport over the row area (Figma rows 199..979), the rows stacked in its content
            var vp = UiKit.FigRect(rt, "Viewport", new Rect(284f, 194f, 1370f, 791f), UiKit.Edge.Middle, UiKit.Edge.Middle);
            vp.color = new Color(1f, 1f, 1f, 0f); vp.raycastTarget = true;   // catches the wheel and drags between rows
            vp.gameObject.AddComponent<RectMask2D>();
            list = UiKit.Rect(vp.transform, "Rows", new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            list.pivot = new Vector2(0.5f, 1f);
            scroll = vp.gameObject.AddComponent<ScrollRect>();
            scroll.content = list; scroll.viewport = vp.rectTransform; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 60f; scroll.inertia = true; scroll.decelerationRate = 0.12f;
            // the Figma track and thumb become the scroll bar
            var track = UiKit.Fig(rt, "q_scroll_track", UiKit.Edge.Middle, UiKit.Edge.Middle, 0, 0, true, "ScrollBar");
            var area = UiKit.Rect(track.transform, "SlidingArea", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var handle = UiKit.Rect(area, "Handle", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var hi = handle.gameObject.AddComponent<Image>(); hi.sprite = UiKit.UiSprite("q_scroll_thumb"); hi.type = Image.Type.Sliced; hi.pixelsPerUnitMultiplier = 2f;
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle; bar.targetGraphic = hi; bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            empty = UiKit.FigLabel(rt, "", 40, UiKit.FigBrown2, 960f, 560f, 1100f, 200f);
            root.SetActive(false);
        }

        public void Show() { root.SetActive(true); root.transform.SetAsLastSibling(); }
        public void Hide() { root.SetActive(false); }

        /// <summary>Draws one tab as a scrolling list; a new tab starts at the top, a redraw of the same tab keeps the place.</summary>
        public void Render(int tab, IList<Row> newRows, string emptyText, string[] tabLabels)
        {
            bool newTab = tab != Tab;
            Tab = tab; rows = newRows != null ? newRows.ToList() : new List<Row>();
            for (int i = 0; i < tabs.Length; i++)
            {
                UiKit.SetButtonText(tabs[i], tabLabels != null && i < tabLabels.Length ? tabLabels[i] : "");
                bool on = i == tab;
                var img = tabs[i].GetComponent<Image>(); img.sprite = UiKit.UiSprite(on ? "q_tab_on" : "q_tab_off");
                tabs[i].GetComponentInChildren<Text>().color = UiKit.FigBrown2;
            }
            // labels decide the widths; the tabs follow each other from the 퀘스트 tab (20 apart)
            float x = TabX[0];
            for (int i = 0; i < tabs.Length; i++)
            {
                var img = tabs[i].GetComponent<Image>(); img.GetComponent<FitToText>().Apply();
                var trt = img.rectTransform; trt.anchoredPosition = new Vector2(x - 960f, trt.anchoredPosition.y); x += trt.sizeDelta.x + 20f;
            }
            empty.text = rows.Count == 0 ? emptyText : "";
            float keep = scroll.verticalNormalizedPosition;
            for (int i = list.childCount - 1; i >= 0; i--)
            {
                var c = list.GetChild(i).gameObject; c.transform.SetParent(null);
                if (Application.isPlaying) UnityEngine.Object.Destroy(c); else UnityEngine.Object.DestroyImmediate(c);
            }
            for (int i = 0; i < rows.Count; i++) BuildRow(rows[i], 5f + i * RowPitch);
            list.sizeDelta = new Vector2(0f, Mathf.Max(0f, 10f + (rows.Count - 1) * RowPitch + RowH));
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = newTab ? 1f : keep;
            scroll.verticalScrollbar.gameObject.SetActive(rows.Count > PerPage);
            Skin?.Invoke(root.transform);
        }

        void BuildRow(Row r, float top)
        {
            // tone follows the shortcut button (pink = go ahead, mint = claim, sky = go and look), grey = done, yellow = under way
            string tone = r.done ? "gray" : string.IsNullOrEmpty(r.action) || r.onAction == null ? "yellow" : r.actionColor == UiKit.Mint ? "mint" : r.actionColor == UiKit.Sky ? "yellow" : "pink";
            var rrt = UiKit.Rect(list, "QuestRow_" + r.key, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            rrt.pivot = new Vector2(0.5f, 1f); rrt.sizeDelta = new Vector2(1359.5f, RowH); rrt.anchoredPosition = new Vector2(0f, -top);
            var row = rrt.gameObject.AddComponent<Image>();
            row.sprite = UiKit.UiSprite("q_row_" + tone); row.raycastTarget = false;
            // everything below hangs from the row's top-left (Figma: icon 32,28; texts from x 177; coin pill 788,55; button 1015,40)
            var icon = UiKit.SpriteImage(row.transform, "Icon", "q_ic_" + tone, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(31.5f, -27.8f), new Vector2(120f, 120f));
            UiKit.TitleLabel(icon.transform, r.icon, 48, UiKit.FigBrown2, new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(110, 90));
            var title = UiKit.TitleLabel(row.transform, r.title, 44, UiKit.FigBrown2, new Vector2(0f, 1f), new Vector2(177f, -52f), new Vector2(580, 70), TextAnchor.MiddleLeft, new Vector2(0f, 0.5f));
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            var detail = UiKit.TitleLabel(row.transform, r.detail, 28, UiKit.FigBrown2, new Vector2(0f, 1f), new Vector2(177f, -112f), new Vector2(740, 60), TextAnchor.UpperLeft, new Vector2(0f, 0.5f));
            detail.horizontalOverflow = HorizontalWrapMode.Wrap; detail.resizeTextForBestFit = true; detail.resizeTextMinSize = 18; detail.resizeTextMaxSize = 28; detail.verticalOverflow = VerticalWrapMode.Truncate;
            if (!string.IsNullOrEmpty(r.reward))
            {
                var pill = UiKit.SpriteImage(row.transform, "Reward", "q_coinpill", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(787.6f, -55f), new Vector2(143f, 66f));
                UiKit.TitleLabel(pill.transform, r.reward, 30, UiKit.FigBrown2, new Vector2(0.5f, 0.5f), new Vector2(26, 0), new Vector2(90, 56));
            }
            var prog = UiKit.TitleLabel(row.transform, r.progress ?? "", 26, UiKit.FigBrown2, new Vector2(0f, 1f), new Vector2(860f, -142f), new Vector2(220, 40));
            prog.resizeTextForBestFit = true; prog.resizeTextMinSize = 16; prog.resizeTextMaxSize = 26;
            if (r.done)
            {
                UiKit.SpriteImage(row.transform, "Heart", "q_heart", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1072f, -72f), new Vector2(45.5f, 41f));
                UiKit.TitleLabel(row.transform, "완료", 40, UiKit.FigBrown2, new Vector2(0f, 1f), new Vector2(1180f, -92f), new Vector2(120, 60));
            }
            else if (!string.IsNullOrEmpty(r.action) && r.onAction != null)
            {
                var act = r.onAction;
                string sp = tone == "pink" ? "q_btn_pink" : tone == "mint" ? "q_btn_yellow" : "q_btn_white";
                var b = UiKit.SpriteButton(row.transform, "QuestAction_" + r.key, sp, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1014.4f, -39.9f), new Vector2(296f, 94f), () => act());
                var al = UiKit.TitleLabel(b.transform, r.action, 36, UiKit.FigBrown2, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(256, 50));
                al.horizontalOverflow = HorizontalWrapMode.Wrap; al.verticalOverflow = VerticalWrapMode.Truncate;   // one line, shrinking
                al.resizeTextForBestFit = true; al.resizeTextMinSize = 18; al.resizeTextMaxSize = 36;
            }
        }

        // ---- test hooks ----
        public bool QaClick(string buttonName)
        {
            var t = root.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == buttonName);
            if (t == null || !t.interactable) return false;
            t.onClick.Invoke(); return true;
        }
    }

    /// <summary>
    /// A friend talking to the player at the bottom of the screen (D-011): name tag, what it says, an optional grid of answers
    /// and a row of buttons ("좋아, 할게!" / "나중에"). Pure view.
    /// </summary>
    public class FriendDialog
    {
        public struct Choice
        {
            public string label; public Color color; public Action onClick;
            public Choice(string label, Color color, Action onClick) { this.label = label; this.color = color; this.onClick = onClick; }
        }

        public bool Visible => root.activeSelf;
        /// <summary>Called with the bubble after every new line (D-035: the screen's prefab is laid over it).</summary>
        public Action<Transform> Skin;
        public string LineShown => line.text;
        public IEnumerable<string> ButtonLabels => root.GetComponentsInChildren<Button>(false).Select(b => b.GetComponentInChildren<Text>()?.text ?? "");

        readonly GameObject root;
        readonly RectTransform rt, grid, footer;
        readonly Image nameTag;
        readonly Text nameText, line, sub;
        const float Width = 1360f;

        public FriendDialog(Transform canvas)
        {
            // Figma D-020: a white bubble with a pink name tag on its top-left corner
            var p = UiKit.Panel(canvas, "FriendDialog", Color.white, new Vector2(0.5f, 0f), new Vector2(0, 200), new Vector2(Width, 330), true);
            var bubbleSprite = UiKit.UiSprite("dlg_bubble");
            if (bubbleSprite != null) { UiKit.NoStitch(p); p.sprite = bubbleSprite; p.type = Image.Type.Sliced; p.pixelsPerUnitMultiplier = 2f; }
            root = p.gameObject; rt = p.rectTransform;
            nameTag = UiKit.Panel(p.transform, "NameTag", Color.white, new Vector2(0f, 1f), new Vector2(150, 8), new Vector2(240, 84), false);
            // a plain white pill (no stitch) tinted with the speaking friend's own pastel (D-025)
            var tagSprite = UiKit.UiSprite("kit_pill");
            if (tagSprite != null) { UiKit.NoStitch(nameTag); nameTag.sprite = tagSprite; UiKit.Slice(nameTag, true); }
            nameText = UiKit.Label(nameTag.transform, "", 39, UiKit.FigBrown, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(230, 70));
            nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            nameTag.rectTransform.pivot = new Vector2(0f, 0.5f); nameTag.rectTransform.anchoredPosition = new Vector2(30f, 8f);   // grows to the right with longer names
            UiKit.AutoFit(nameTag, nameText, 240f, 90f, 700f);
            line = UiKit.Label(p.transform, "", 38, UiKit.FigBrown, new Vector2(0.5f, 1f), new Vector2(0, -92), new Vector2(Width - 120f, 100), TextAnchor.MiddleLeft, FontStyle.Normal);
            sub = UiKit.Label(p.transform, "", 28, new Color(0.7f, 0.45f, 0.28f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -160), new Vector2(Width - 120f, 44), TextAnchor.MiddleLeft);
            grid = UiKit.Rect(p.transform, "Choices", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            grid.pivot = new Vector2(0.5f, 1f); grid.sizeDelta = new Vector2(Width - 100f, 10f);
            footer = UiKit.Rect(p.transform, "Footer", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            footer.anchoredPosition = new Vector2(0, 66); footer.sizeDelta = new Vector2(Width - 100f, 96);
            root.SetActive(false);
        }

        /// <summary>Shows a line; <paramref name="choices"/> go into a grid of <paramref name="columns"/>, <paramref name="buttons"/> into the bottom row.</summary>
        public void Show(string speaker, Color tag, string text, string subText, IList<Choice> choices, int columns, IList<Choice> buttons)
        {
            nameText.text = speaker; nameTag.color = tag;
            line.text = text; sub.text = subText ?? "";
            Clear(grid); Clear(footer);
            int n = choices?.Count ?? 0, cols = Mathf.Clamp(columns <= 0 ? n : columns, 1, 8);
            int rows = n == 0 ? 0 : Mathf.CeilToInt(n / (float)cols);
            float gw = Width - 100f, gap = 12f;
            float bw = Mathf.Min(300f, (gw - (cols - 1) * gap) / cols), bh = cols >= 7 ? 60f : 76f;
            float top = string.IsNullOrEmpty(subText) ? 150f : 196f;
            grid.anchoredPosition = new Vector2(0, -top);
            for (int i = 0; i < n; i++)
            {
                var c = choices[i]; int row = i / cols, col = i % cols;
                int inRow = Mathf.Min(cols, n - row * cols);
                float x = (col - (inRow - 1) / 2f) * (bw + gap);
                var b = UiKit.MakeButton(grid, c.label, c.color, new Vector2(0.5f, 1f), new Vector2(x, -(bh / 2f) - row * (bh + gap)), new Vector2(bw, bh), c.onClick, cols >= 7 ? 26 : 30);
                b.name = "Choice_" + c.label; UiKit.NoStitch(b.GetComponent<Image>());   // no dashed stitch inside the speech bubble (D-023)
            }
            int m = buttons?.Count ?? 0;
            for (int i = 0; i < m; i++)
            {
                var c = buttons[i];
                var b = UiKit.MakeButton(footer, c.label, c.color, new Vector2(0.5f, 0.5f), new Vector2((i - (m - 1) / 2f) * 330f, 0), new Vector2(300, 88), c.onClick, 34);
                b.name = "DialogBtn_" + c.label; UiKit.NoStitch(b.GetComponent<Image>());
            }
            float h = top + (rows > 0 ? rows * (bh + gap) + 8f : -36f) + (m > 0 ? 124f : 30f);
            h = Mathf.Max(h, 300f);
            rt.sizeDelta = new Vector2(Width, h); rt.anchoredPosition = new Vector2(0, 36f + h / 2f);
            root.SetActive(true); root.transform.SetAsLastSibling();
            Skin?.Invoke(root.transform);
        }

        public void Hide() { root.SetActive(false); }

        static void Clear(RectTransform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i).gameObject; c.transform.SetParent(null);   // gone from the list at once (the prefab is laid over it next)
                if (Application.isPlaying) UnityEngine.Object.Destroy(c); else UnityEngine.Object.DestroyImmediate(c);
            }
        }

        // ---- test hooks ----
        public bool QaClick(string label)
        {
            var b = root.GetComponentsInChildren<Button>(false).FirstOrDefault(x => x.name == "Choice_" + label || x.name == "DialogBtn_" + label);
            if (b == null) return false;
            b.onClick.Invoke(); return true;
        }
    }
}

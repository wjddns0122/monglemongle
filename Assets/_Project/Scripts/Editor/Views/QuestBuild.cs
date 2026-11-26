using Mongle.Views;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Mongle.EditorTools.ViewKit;

namespace Mongle.EditorTools
{
    /// <summary>
    /// The 11-26 퀘스트 window (Figma "퀘스트" 88:2130): Assets/_UI수정/3_마을/QuestWindow.prefab, listed as "Quest". The sheet —
    /// the stitched cream board with its 18 % bear-and-flower pattern — is one Figma picture (Art/UI_1126/Quest/board.png, drawn
    /// by headless Chrome); the tabs sit behind it as in Figma (the 보관함's tab pictures, the same vectors), the title tab, ✕,
    /// the rows (<see cref="AskRowBuild"/> at full size) in a scrolling list and its bar are laid over it. The window keeps
    /// Figma's place in the middle of the screen; the village behind it is dimmed. (Quest.prefab is the old D-040 front, kept
    /// until the open UiPreview scene lets go of it.)
    /// </summary>
    public static class QuestBuild
    {
        public const string PrefabPath = UiEditFolder.VillageDir + "/QuestWindow.prefab";
        static readonly Vector2 Frame = new Vector2(1920f, 1080f);
        static readonly Color TagBrown = Hex("#816253"), DimColor = new Color(0.16f, 0.1f, 0.07f, 0.42f);
        // the list: the rows' place on the sheet (Figma rows at x 289.4, y 200.2 + 200.5 n, 1356.6 x 173.7)
        const float ListX = 283f, ListY = 194f, ListW = 1370f, ListH = 790f;

        public static GameObject Make()
        {
            var root = Root("Quest", 10);
            var v = root.gameObject.AddComponent<QuestView>();
            var dim = Node(root, "Dim", 0f, 0f, 1920f, 1080f, C).gameObject.AddComponent<Image>();
            var drt = dim.rectTransform; drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
            dim.color = DimColor; dim.raycastTarget = true;
            var win = Node(root, "Window", 0f, 0f, 1920f, 1080f, C);

            // the tabs, behind the sheet: 진행중 N · 완료 N · 내 이야기 (Figma 88:2133 · 88:2134 · 88:2131)
            float[] tx = { 522.49f, 746.23f, 969.97f };
            string[] words = { "진행중 4", "완료 1", "내 이야기" };
            var tabBox = new Vector2(224.5f, 102f);
            v.tabs = new QuestView.Tab[3];
            for (int i = 0; i < 3; i++)
            {
                var b = PictureButton(win, "Tab_" + i, i == 0 ? "Storage/tab_on.png" : "Storage/tab_off.png", tx[i] - 0.49f, 41.99f, tabBox.x, tabBox.y, TL, Frame);
                var label = Words(b.transform, "Label", words[i], 40f, TagBrown, 0f, 13f, tabBox.x - 6f, 52f, TL, HorizontalAlignmentOptions.Center, null, tabBox);
                label.enableAutoSizing = true; label.fontSizeMin = 26f; label.fontSizeMax = 40f;
                v.tabs[i] = new QuestView.Tab { button = b, plate = (Image)b.targetGraphic, label = label };
            }
            v.tabOn = Art("Storage/tab_on.png"); v.tabOff = Art("Storage/tab_off.png");

            // the sheet, the title tab, ✕
            Picture(win, "Board", "Quest/board.png", 223.5f, 116f, 1496.5f, 909.5f, TL, Frame).raycastTarget = true;
            Picture(win, "TitleTab", "Storage/title.png", 257f, 39.5f, 248f, 114.5f, TL, Frame);
            Words(win, "Title", "퀘스트", 62f, TagBrown, 294.68f, 50.3f, 177f, 81f, TL, HorizontalAlignmentOptions.Center, "quest.title", Frame);
            v.closeButton = PictureButton(win, "Btn_나가기", "Quest/close.png", 1650f, 80f, 106.5f, 106.5f, TL, Frame);

            // the rows: a masked, scrolling list; one row kept off as the template
            var viewport = Node(win, "List", ListX, ListY, ListW, ListH, TL, Frame);
            var hit = viewport.gameObject.AddComponent<Image>(); hit.color = new Color(1f, 1f, 1f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>(); content.gameObject.layer = 5;
            content.SetParent(viewport, false); content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one; content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, ListH); content.anchoredPosition = Vector2.zero;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 60f; scroll.inertia = true; scroll.decelerationRate = 0.12f;
            v.scroll = scroll; v.content = content;
            var row = AskRowBuild.Make(content, "Row", 0f, 0f, AskRowBuild.W, 1f, new Vector2(ListW, ListH));
            var rt = (RectTransform)row.root.transform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f); rt.anchoredPosition = new Vector2(v.first.x, -v.first.y);
            row.root.SetActive(false);
            v.rowTemplate = row.root;
            v.style = AskRowBuild.Style();
            v.heart = Art("Quest/heart_icon.png");   // the ♥ in a clear square, so it sits on the round icon at the size of a letter
            // the bar (Figma 88:2265 / 88:2266: #FFEBCB track, #B68892 thumb, drawn so the thumb can be any length)
            var track = Node(win, "ScrollBar", 1667.03f, 202.13f, 15.87f, 773.41f, TL, Frame);
            var trackShape = Shape(track.gameObject, Hex("#FFEBCB"), Vector4.one * 7.9f); trackShape.raycastTarget = true;
            var area = new GameObject("SlidingArea", typeof(RectTransform)).GetComponent<RectTransform>(); area.gameObject.layer = 5;
            area.SetParent(track, false); area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = area.offsetMax = Vector2.zero;
            var handle = new GameObject("Handle", typeof(RectTransform)).GetComponent<RectTransform>(); handle.gameObject.layer = 5;
            handle.SetParent(area, false); handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one; handle.offsetMin = handle.offsetMax = Vector2.zero;
            var thumb = Shape(handle.gameObject, Hex("#B68892"), Vector4.one * 8.16f); thumb.raycastTarget = true;
            var bar = track.gameObject.AddComponent<Scrollbar>(); bar.handleRect = handle; bar.targetGraphic = thumb; bar.transition = Selectable.Transition.None;
            bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            // nothing on this tab
            v.emptyText = Words(win, "Empty", "", 44f, TagBrown, ListX, ListY, ListW, ListH * 0.8f, TL, HorizontalAlignmentOptions.Center, null, Frame);
            v.emptyText.textWrappingMode = TextWrappingModes.Normal; v.emptyText.gameObject.SetActive(false);

            root.gameObject.SetActive(false);   // the village opens it (QuestView.Get)
            return root.gameObject;
        }

        public static GameObject Build()
        {
            var go = Make();
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                Register("Quest", prefab);
                return prefab;
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

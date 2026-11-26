using Mongle.UI2;
using Mongle.Views;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Mongle.EditorTools.ViewKit;

namespace Mongle.EditorTools
{
    /// <summary>
    /// The 11-26 보관함 (Figma "보관함" 88:825, "보관함 비어있을때" 88:650): Assets/_UI수정/3_마을/StorageBox.prefab, listed as
    /// "Storage". The binder — both stitched cream pages with their 18 % bear-and-flower pattern, the rings between them — is one
    /// Figma picture (Art/UI_1126/Storage, drawn by headless Chrome); the tabs sit behind it as in Figma, the title tab, the
    /// close button, the search field, the 기본 정렬 ▼ list, the cards, the scroll bar and the right page are laid over it.
    /// The window keeps Figma's place in the middle of the screen; the village behind it is dimmed.
    /// </summary>
    public static class StorageBuild
    {
        public const string PrefabPath = UiEditFolder.VillageDir + "/StorageBox.prefab";   // (Storage.prefab is the old D-040 front, kept until UiPreview lets go of it)
        static readonly Vector2 Frame = new Vector2(1920f, 1080f);
        static readonly Color TagBrown = Hex("#816253"), DimColor = new Color(0.16f, 0.1f, 0.07f, 0.42f);
        // the grid: the cards' place on the left page (Figma cards at x 147.5 + 267.4 n, y 209.8 + 267.5 n, 246.5 x 247.1)
        const float GridX = 140f, GridY = 204f, GridW = 1062f, GridH = 762f, CardW = 246.54f, CardH = 247.14f;

        public static GameObject Make()
        {
            var root = Root("Storage", 10);
            var v = root.gameObject.AddComponent<StorageView>();
            var dim = Node(root, "Dim", 0f, 0f, 1920f, 1080f, C).gameObject.AddComponent<Image>();
            var drt = dim.rectTransform; drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
            dim.color = DimColor; dim.raycastTarget = true;   // the park behind does not take clicks while the box is open
            var win = Node(root, "Window", 0f, 0f, 1920f, 1080f, C);

            // the tabs, behind the pages (Figma draws them first): 전체 · 가구 · 악세서리
            float[] tx = { 408.49f, 632.23f, 855.97f };
            string[] words = { "전체", "가구", "악세서리" }, keys = { "storage.tab.all", "storage.tab.furniture", "storage.tab.accessory" };
            var tabBox = new Vector2(224.5f, 102f);
            v.tabs = new StorageView.Tab[3];
            for (int i = 0; i < 3; i++)
            {
                var b = PictureButton(win, "Tab_" + words[i], i == 0 ? "Storage/tab_on.png" : "Storage/tab_off.png", tx[i] - 0.49f, 29.5f, tabBox.x, tabBox.y, TL, Frame);
                var label = Words(b.transform, "Label", words[i], 40f, TagBrown, 0f, 13.28f, tabBox.x - 11f, 52f, TL, HorizontalAlignmentOptions.Center, keys[i], tabBox);
                v.tabs[i] = new StorageView.Tab { button = b, plate = (Image)b.targetGraphic, label = label };
            }
            v.tabOn = Art("Storage/tab_on.png"); v.tabOff = Art("Storage/tab_off.png");

            // the binder, the title tab, ✕
            Picture(win, "Board", "Storage/board.png", 109.5f, 103.5f, 1623f, 912f, TL, Frame).raycastTarget = true;
            Picture(win, "TitleTab", "Storage/title.png", 143f, 27f, 248f, 114.5f, TL, Frame);
            Words(win, "Title", "보관함", 62f, TagBrown, 180.68f, 37.81f, 177f, 81f, TL, HorizontalAlignmentOptions.Center, "storage.title", Frame);
            v.closeButton = PictureButton(win, "Btn_나가기", "Storage/close.png", 1668f, 69.5f, 106.5f, 106.5f, TL, Frame);

            // the search field (the magnifier is part of its picture) and the order list
            v.search = Search(win);
            v.sort = FriendTalkBuild.Dropdown(win, "Sort", 996f, 134f, 196f, 56f, "Storage/sort.png", "Storage/sort_arrow.png", new Rect(149f, 16.5f, 30.5f, 28f),
                new Vector4(12f, 50f, 0f, 0f), "기본 정렬", 30f, new Vector4(0f, 0f, 6f, 152f), 20f, 10f, new Vector4(6f, 6f, 4f, 8f), out _, Frame, 48f, 26f);

            // the cards: a masked, scrolling page; one card kept off as the template
            var viewport = Node(win, "Grid", GridX, GridY, GridW, GridH, TL, Frame);
            var hit = viewport.gameObject.AddComponent<Image>(); hit.color = new Color(1f, 1f, 1f, 0f);   // drags and the wheel between the cards
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>(); content.gameObject.layer = 5;
            content.SetParent(viewport, false); content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one; content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, GridH); content.anchoredPosition = Vector2.zero;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 60f; scroll.inertia = true; scroll.decelerationRate = 0.12f;
            v.scroll = scroll; v.content = content;
            v.cardTemplate = Card(content);
            // the scroll bar (Figma 88:971 / 88:972: #FFEBCB track, #B68892 thumb, drawn so the thumb can be any length)
            var track = Node(win, "ScrollBar", 1206.25f, 215.23f, 16.65f, 752.57f, TL, Frame);
            var trackShape = Shape(track.gameObject, Hex("#FFEBCB"), Vector4.one * 8.2f); trackShape.raycastTarget = true;
            var area = new GameObject("SlidingArea", typeof(RectTransform)).GetComponent<RectTransform>(); area.gameObject.layer = 5;
            area.SetParent(track, false); area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = area.offsetMax = Vector2.zero;
            var handle = new GameObject("Handle", typeof(RectTransform)).GetComponent<RectTransform>(); handle.gameObject.layer = 5;
            handle.SetParent(area, false); handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one; handle.offsetMin = handle.offsetMax = Vector2.zero;
            var thumb = Shape(handle.gameObject, Hex("#B68892"), Vector4.one * 8.16f); thumb.raycastTarget = true;
            var bar = track.gameObject.AddComponent<Scrollbar>(); bar.handleRect = handle; bar.targetGraphic = thumb; bar.transition = Selectable.Transition.None;
            bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            // nothing in the box (Figma 88:650) / nothing for this tab or search
            v.emptyText = Words(win, "Empty", "보관함이 비었어요.\n가게에서 마음에 드는 물건을 사 봐요!", 48f, TagBrown, 241.5f, 467.7f, 900f, 160f, TL, HorizontalAlignmentOptions.Center, null, Frame);
            v.emptyText.textWrappingMode = TextWrappingModes.Normal; v.emptyText.gameObject.SetActive(false);

            // the right page: the chosen thing (Figma 88:986, 88:1015, 88:1016, 88:1008, 88:1039, 88:1009)
            var detail = Picture(win, "Detail", "Storage/detail.png", 1378f, 153.5f, 315.5f, 813.5f, TL, Frame);
            var ds = new Vector2(315.5f, 813.5f); float dx = 1378f, dy = 153.5f;
            v.detail = detail.gameObject;
            v.picture = Picture(detail.transform, "Picture", "Storage/detail.png", 1447.49f - 10f - dx, 246.2f - 10f - dy, 189.54f, 189.54f, TL, ds);
            v.picture.sprite = null; v.picture.enabled = false;
            v.nameText = Words(detail.transform, "Name", "포근 의자", 40f, Brown, 1380f - dx, 438.64f - dy, 310f, 52f, TL, HorizontalAlignmentOptions.Center, null, ds);
            v.nameText.enableAutoSizing = true; v.nameText.fontSizeMin = 26f; v.nameText.fontSizeMax = 40f;
            v.descriptionText = Words(detail.transform, "About", "아이템에\n대한 설명", 30f, TagBrown, 1398f - dx, 520f - dy, 274f, 260f, TL, HorizontalAlignmentOptions.Center, null, ds);
            v.descriptionText.textWrappingMode = TextWrappingModes.Normal; v.descriptionText.verticalAlignment = VerticalAlignmentOptions.Top;
            v.descriptionText.enableAutoSizing = true; v.descriptionText.fontSizeMin = 20f; v.descriptionText.fontSizeMax = 30f;
            ((RectTransform)v.descriptionText.transform).anchoredPosition += new Vector2(0f, -39.8f);   // first line where Figma's starts (559.8)
            var use = PictureButton(detail.transform, "Btn_사용하기", "Storage/use.png", 1406.5f - dx, 807.5f - dy, 258.5f, 80.5f, TL, ds);
            v.useButton = use;
            v.useText = Words(use.transform, "Label", "사용하기", 40f, Brown, 0f, 12.3f, 258.5f, 52f, TL, HorizontalAlignmentOptions.Center, "storage.use", new Vector2(258.5f, 80.5f));

            root.gameObject.SetActive(false);   // the village opens it (StorageView.Get)
            return root.gameObject;
        }

        /// <summary>The search field: Figma's pill with the magnifier, the words typed after it.</summary>
        static TMP_InputField Search(Transform win)
        {
            var box = new Vector2(201f, 61f);
            var field = Picture(win, "Search", "Storage/search.png", 780.5f, 131.5f, box.x, box.y, TL, Frame); field.raycastTarget = true;
            var area = Node(field.transform, "Text Area", 54f, 6f, 136f, 49f, TL, box); area.gameObject.AddComponent<RectMask2D>();
            var holder = Words(area, "Placeholder", "찾기", 26f, new Color(Brown.r, Brown.g, Brown.b, 0.45f), 0f, 0f, 136f, 49f, TL, HorizontalAlignmentOptions.Left, "storage.search", new Vector2(136f, 49f));
            var text = Words(area, "Text", "", 26f, Brown, 0f, 0f, 136f, 49f, TL, HorizontalAlignmentOptions.Left, null, new Vector2(136f, 49f));
            var input = field.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area; input.textComponent = text; input.placeholder = holder; input.targetGraphic = field;
            input.fontAsset = text.font; input.pointSize = 26f; input.characterLimit = 16; input.lineType = TMP_InputField.LineType.SingleLine;
            input.caretColor = Brown; input.customCaretColor = true; input.selectionColor = new Color(0.63f, 0.86f, 0.97f, 0.6f);
            return input;
        }

        /// <summary>One card (Figma 88:977, chosen 88:979): the white card with its shadow, the blue-edged one over it when
        /// chosen, the thing's picture and ×N in the blue pill (88:1004).</summary>
        static GameObject Card(RectTransform content)
        {
            var cs = new Vector2(CardW, CardH);
            var card = Node(content, "Card", 0f, 0f, CardW, CardH, TL, new Vector2(GridW, GridH));
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0f, 1f); card.anchoredPosition = new Vector2(7.46f, -5.82f);
            var plate = Picture(card, "Plate", "Storage/card.png", -1.01f, -0.99f, 251f, 251.5f, TL, cs); plate.raycastTarget = true;
            var b = card.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = plate;
            card.gameObject.AddComponent<PressScale>();
            Picture(card, "Chosen", "Storage/card_on.png", -3.5f, -3.51f, 256f, 256.5f, TL, cs).gameObject.SetActive(false);
            var icon = Picture(card, "Icon", "Storage/card.png", (CardW - 214f) / 2f, (CardH - 214f) / 2f + 4f, 214f, 214f, TL, cs); icon.sprite = null; icon.enabled = false;
            var badge = Picture(card, "Badge", "Storage/badge.png", 171.04f, 6.18f, 69.5f, 35.5f, TL, cs);
            Words(badge.transform, "Count", "x 1", 24.19f, Brown, 0f, -1f, 69.5f, 35.5f, TL, HorizontalAlignmentOptions.Center, null, new Vector2(69.5f, 35.5f));
            card.gameObject.SetActive(false);
            return card.gameObject;
        }

        public static GameObject Build()
        {
            var go = Make();
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                Register("Storage", prefab);
                return prefab;
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

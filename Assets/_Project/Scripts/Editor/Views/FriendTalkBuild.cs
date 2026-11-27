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
    /// The 11-26 friend talk window (Figma "대화1" 88:231, "대화2" 88:367): Assets/_UI수정/3_마을/FriendTalk.prefab, listed as
    /// "FriendTalk". The bubble, the name tag, the date panel and the lists are SoftRects with the SVG values (they change size
    /// with the words); the two pills, the heart and the little ▼ are the Figma vectors (Art/UI_1126/Village). The drop-downs are
    /// TMP_Dropdowns dressed as Figma's: the pill with the pink edge, a white list with 56 px rows, the pink row of the chosen
    /// answer, grey lines between rows and the thin cream / mauve scrollbar; the birthday is Figma's white date pill with two such
    /// drop-downs in its own small fields, made the size of the 다음에 할게 pill and put above it.
    /// </summary>
    public static class FriendTalkBuild
    {
        public const string PrefabPath = UiEditFolder.VillageDir + "/FriendTalk.prefab";
        static readonly Color Sky = Hex("#A0DCF8"), Pink = Hex("#FFE1E6"), Peach = Hex("#FFB58A"), Row = Hex("#FDE5ED"), Line = Hex("#ECE9E8"),
            Track = Hex("#FFEBCB"), Handle = Hex("#B68892"), ItemBrown = Hex("#79594B"), Shadow = new Color(0f, 0f, 0f, 0.5f);

        public static GameObject Make()
        {
            var root = Root("FriendTalk", 30);   // over the HUD (9) and the village's panels (10), under the album (40)
            var v = root.gameObject.AddComponent<FriendTalkView>();

            // the bubble (Figma 88:242: 959 x 259, corners 95.9, a 1 px shadow) and the name tag on its corner
            var bs = new Vector2(959.2f, 259.4f);
            var bubble = Node(root, "Bubble", 219.9f, 747.5f, bs.x, bs.y, B);
            var bshape = Shape(bubble.gameObject, Color.white, Vector4.one * 95.9f); bshape.raycastTarget = true;
            bshape.shadowColor = Shadow; bshape.shadowOffset = new Vector2(1f, -1f); bshape.shadowBlur = 1f;
            var line = Words(bubble, "Line", "", 33.15f, Brown, 75.9f, 63.2f, 820f, 100f, TL, HorizontalAlignmentOptions.Left, null, bs);
            line.textWrappingMode = TextWrappingModes.Normal; line.verticalAlignment = VerticalAlignmentOptions.Top; line.lineSpacing = 20f;
            line.enableAutoSizing = true; line.fontSizeMin = 24f; line.fontSizeMax = 33.15f;
            var sub = Words(bubble, "Sub", "", 22f, Peach, 78.4f, 172f, 800f, 32f, TL, HorizontalAlignmentOptions.Left, null, bs);
            var next = Picture(bubble, "Next", "Village/next_arrow.png", 873.2f, 196.5f, 30.5f, 28f, TL, bs);
            var tag = Node(root, "NameTag", 226.5f, 710.5f, 204.09f, 70.59f, B);
            tag.pivot = new Vector2(0f, 0f); tag.anchoredPosition -= new Vector2(204.09f * 0.5f, 0f);   // grows to the right with longer names
            var tshape = Shape(tag.gameObject, Pink, Vector4.one * 28f);
            var name = Words(tag, "Name", "몽이", 33.15f, Brown, 0f, 0f, 204.09f, 70.59f, C, HorizontalAlignmentOptions.Center, null, new Vector2(204.09f, 70.59f));
            name.rectTransform.anchorMin = Vector2.zero; name.rectTransform.anchorMax = Vector2.one; name.rectTransform.offsetMin = new Vector2(0f, 2f); name.rectTransform.offsetMax = new Vector2(0f, 2f);
            v.bubble = bubble; v.nameTag = tshape; v.nameText = name; v.lineText = line; v.subText = sub; v.next = next.rectTransform;

            // a request: ♥ 좋아! (the pill with the pink edge) and 다음에 할게. Every pill of the answers' column — 좋아!, the answers'
            // drop-down, the birthday pill — takes the 다음에 할게 pill's box (x 1262, 361.5 x 80), right above it, so the column
            // lines up (user 2026-11-26: "버튼 위치 통일")
            // Both are the white pill; the one the pointer is on wears the sky-blue 5 px edge and the heart (user 2026-11-27:
            // "하늘색 5px로 바꾸고 마우스 커서 위로 올리면 밑에랑 위에 하트랑 같이 선택")
            var pill = new Vector2(361.5f, 80f);
            v.yesButton = PictureButton(root, "Btn_Yes", "Village/choice.png", 1262f, 774f, pill.x, pill.y, B);
            ((Image)v.yesButton.targetGraphic).preserveAspect = false;   // fills the box edge to edge, like the drop-down and 다음에 할게
            v.yesRing = Ring(v.yesButton.transform, pill);
            v.yesHeart = Picture(v.yesButton.transform, "Heart", "Village/heart.png", 34f, (pill.y - 29f) / 2f - 1f, 32f, 29f, TL, pill).gameObject;
            v.yesLabel = PillWords(v.yesButton.transform, "좋아!", pill, 3f);
            v.laterButton = PictureButton(root, "Btn_Later", "Village/choice.png", 1262f, 885.5f, 361.5f, 80f, B);
            v.laterRing = Ring(v.laterButton.transform, pill);
            v.laterHeart = Picture(v.laterButton.transform, "Heart", "Village/heart.png", 34f, (pill.y - 29f) / 2f - 1f, 32f, 29f, TL, pill).gameObject;
            v.laterLabel = PillWords(v.laterButton.transform, "다음에 할게", new Vector2(361.5f, 80f), 3f);
            v.yesButton.gameObject.SetActive(false);

            // a question: the answers in a drop-down in the first pill's place (Figma 88:369 / 88:470)
            Image arrow;
            v.answer = Dropdown(root, "Answer", 1262f, 774f, pill.x, pill.y, "Village/choice.png", "Village/next_arrow.png",
                new Rect(pill.x - 63.7f, (pill.y - 32.9f) / 2f, 35.8f, 32.9f), new Vector4(30f, 60f, 0f, 0f), "골라 볼래?", 33.15f,
                new Vector4(13.7f, 12.45f, 1.6f, 187.5f), 19f, 18f, new Vector4(4f, 14f, 7.7f, 14.2f), out arrow);
            v.answerRing = Ring(v.answer.transform, pill);
            v.answerRing.transform.SetSiblingIndex(0);   // under the words and the ▼
            v.answer.gameObject.SetActive(false);
            var arrows = new RectTransform[3]; arrows[0] = arrow.rectTransform;

            // the birthday: Figma 88:371's white date pill — [ ▼] 월 [ ▲] 일 — made the size of the 다음에 할게 pill and put right
            // above it in the answers' column (user: "다음에 할게 버튼처럼 사이즈만 줄여 딱 맞춰"): the same white pill picture, two
            // small fields with the pink edge, the ▼ that turns ▲ while a list is open, the words 28 px; each list opens under its field
            var ds = new Vector2(361.5f, 80f);   // the 다음에 할게 pill's picture box (its node 357.1 x 75.3 sits 1.2 in)
            var date = Picture(root, "Date", "Village/choice.png", 1262f, 885.5f - 111.5f, ds.x, ds.y, B);
            date.raycastTarget = true;
            var fieldBox = new Vector2(118f, 50f);
            var smallArrow = new Rect(85f, 16.35f, 19f, 17.3f);
            var smallList = new Vector4(2f, 2f, 4f, 156f);
            var smallBar = new Vector4(4f, 12f, 4f, 12f);
            float fy = 1.2f + (75.3f - fieldBox.y) * 0.5f;
            v.month = Dropdown(date.transform, "Month", 16.7f, fy, fieldBox.x, fieldBox.y, null, "Village/date_arrow.png",
                smallArrow, new Vector4(10f, 36f, 0f, 0f), "", 28f, smallList, 12f, 12f, smallBar, out arrow, ds, 46f, 28f);
            arrows[1] = arrow.rectTransform;
            Words(date.transform, "MonthWord", "월", 28f, Brown, 140.7f, 1.2f, 32f, 75.3f, TL, HorizontalAlignmentOptions.Center, "talk.month", ds);
            v.day = Dropdown(date.transform, "Day", 186.7f, fy, fieldBox.x, fieldBox.y, null, "Village/date_arrow.png",
                smallArrow, new Vector4(10f, 36f, 0f, 0f), "", 28f, smallList, 12f, 12f, smallBar, out arrow, ds, 46f, 28f);
            arrows[2] = arrow.rectTransform;
            Words(date.transform, "DayWord", "일", 28f, Brown, 310.7f, 1.2f, 32f, 75.3f, TL, HorizontalAlignmentOptions.Center, "talk.day", ds);
            date.gameObject.SetActive(false);
            v.datePanel = date.gameObject; v.arrows = arrows;

            root.gameObject.SetActive(false);   // the village opens it (FriendTalkView.Get)
            return root.gameObject;
        }

        /// <summary>The sky-blue 5 px edge of the chosen pill: over the white pill's own edge (its node 357.1 x 75.3 sits 1.2 in the
        /// picture), half outside, half inside.</summary>
        static Graphic Ring(Transform pill, Vector2 box)
        {
            var r = Node(pill, "Ring", 1.2f - 2.5f, 1.2f - 2.5f, 357.1f + 5f, 75.3f + 5f, TL, box);
            var s = Shape(r.gameObject, new Color(1f, 1f, 1f, 0f), Vector4.one * (75.3f + 5f) * 0.5f, 5f, Sky);
            r.gameObject.SetActive(false);
            return s;
        }

        static TextMeshProUGUI PillWords(Transform pill, string words, Vector2 size, float up)
        {
            var t = Words(pill, "Label", words, 33.15f, Brown, 0f, 0f, size.x, 50f, C, HorizontalAlignmentOptions.Center, null, size);
            t.rectTransform.anchoredPosition = new Vector2(0f, up);   // Figma: the words sit a little above the pill's middle
            return t;
        }

        /// <summary>
        /// A drop-down dressed as Figma's. <paramref name="list"/>: the list's left / right inset under the field, its gap below
        /// the field and its height; <paramref name="bar"/>: the viewport's top / bottom inset, the scrollbar's right margin and its
        /// top / bottom margin (frame px).
        /// </summary>
        internal static TMP_Dropdown Dropdown(Transform parent, string name, float x, float y, float w, float h, string backArt, string arrowArt, Rect arrowBox,
            Vector4 textInset, string placeholder, float size, Vector4 list, float listRadius, float barRoom, Vector4 bar, out Image arrow, Vector2? parentSize = null,
            float rowH = 56f, float itemSize = 34.08f)
        {
            var fs = new Vector2(w, h);
            var field = Node(parent, name, x, y, w, h, parentSize.HasValue ? TL : B, parentSize);
            Graphic back;
            if (backArt != null) { var img = field.gameObject.AddComponent<Image>(); img.sprite = Art(backArt); back = img; }
            else back = Shape(field.gameObject, Color.white, Vector4.one * (h * 0.5f), 3.5f, Pink);   // Figma's date field (white, the pink edge), drawn at any size
            back.raycastTarget = true;
            var caption = Words(field, "Label", "", size, ItemBrown, 0f, 0f, w, h, C, HorizontalAlignmentOptions.Center, null, fs);
            Fill(caption.rectTransform, textInset.x, 0f, textInset.y, 0f);
            var hold = Words(field, "Placeholder", placeholder, size, new Color(Brown.r, Brown.g, Brown.b, 0.55f), 0f, 0f, w, h, C, HorizontalAlignmentOptions.Center, null, fs);
            Fill(hold.rectTransform, textInset.x, 0f, textInset.y, 0f);
            // the ▼ (Figma's own vector), turned to ▲ about its middle while the list is open
            arrow = Picture(field, "Arrow", arrowArt, arrowBox.x, arrowBox.y, arrowBox.width, arrowBox.height, TL, fs);
            arrow.rectTransform.pivot = new Vector2(0.5f, 0.5f); arrow.rectTransform.anchoredPosition += new Vector2(arrowBox.width * 0.5f, -arrowBox.height * 0.5f);

            // the list (kept off; the drop-down copies it when it opens)
            var template = Node(field, "Template", 0f, 0f, w, list.w, TL, fs);
            template.anchorMin = new Vector2(0f, 0f); template.anchorMax = new Vector2(1f, 0f); template.pivot = new Vector2(0.5f, 1f);
            template.offsetMin = new Vector2(list.x, -list.z - list.w); template.offsetMax = new Vector2(-list.y, -list.z);
            var tshape = Shape(template.gameObject, Color.white, Vector4.one * listRadius); tshape.raycastTarget = true;
            tshape.shadowColor = Shadow; tshape.shadowOffset = new Vector2(1f, -1f); tshape.shadowBlur = 1f;
            var scroll = template.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 20f;
            var viewport = Stretch(template, "Viewport", 0f, bar.x, barRoom, bar.y);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>(); content.gameObject.layer = 5;
            content.SetParent(viewport, false); content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one; content.pivot = new Vector2(0.5f, 1f); content.sizeDelta = new Vector2(0f, rowH);
            var item = new GameObject("Item", typeof(RectTransform)).GetComponent<RectTransform>(); item.gameObject.layer = 5;
            item.SetParent(content, false); item.anchorMin = new Vector2(0f, 0.5f); item.anchorMax = new Vector2(1f, 0.5f); item.sizeDelta = new Vector2(0f, rowH);
            var ib = Stretch(item, "Item Background", 0f, 0f, 0f, 0f); var ibs = Shape(ib.gameObject, new Color(1f, 1f, 1f, 0f), Vector4.zero); ibs.raycastTarget = true;
            var mark = Stretch(item, "Item Checkmark", 0f, 0f, 0f, 0f); var marks = Shape(mark.gameObject, Row, Vector4.zero);
            var sep = Stretch(item, "Line", 3f, rowH - 1f, 3f, -1f); Shape(sep.gameObject, Line, Vector4.zero);
            var label = Words(item, "Item Label", "1", itemSize, ItemBrown, 0f, 0f, 100f, rowH, C, HorizontalAlignmentOptions.Center, null, new Vector2(100f, rowH));
            Fill(label.rectTransform, 0f, 0f, 0f, 0f);
            var toggle = item.gameObject.AddComponent<Toggle>(); toggle.transition = Selectable.Transition.None; toggle.targetGraphic = ibs; toggle.graphic = marks; toggle.isOn = true;
            var sb = new GameObject("Scrollbar", typeof(RectTransform)).GetComponent<RectTransform>(); sb.gameObject.layer = 5;
            sb.SetParent(template, false); sb.anchorMin = new Vector2(1f, 0f); sb.anchorMax = Vector2.one; sb.pivot = new Vector2(1f, 0.5f);
            sb.offsetMin = new Vector2(-bar.z - 6.43f, bar.w); sb.offsetMax = new Vector2(-bar.z, -bar.w);
            var track = Shape(sb.gameObject, Track, Vector4.one * 3.2f); track.raycastTarget = true;
            var area = Stretch(sb, "Sliding Area", 0f, 0f, 0f, 0f);
            var handle = Stretch(area, "Handle", 0f, 0f, 0f, 0f); var hshape = Shape(handle.gameObject, Handle, Vector4.one * 3.2f); hshape.raycastTarget = true;
            var scrollbar = sb.gameObject.AddComponent<Scrollbar>(); scrollbar.handleRect = handle; scrollbar.targetGraphic = hshape; scrollbar.transition = Selectable.Transition.None;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;   // (SetDirection(…, true) would turn the rect laid out above on its side)
            scroll.content = content; scroll.viewport = viewport; scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;   // FriendTalkView shows it only for lists longer than the 3 rows
            template.gameObject.SetActive(false);

            var dd = field.gameObject.AddComponent<TMP_Dropdown>();
            dd.targetGraphic = back; dd.transition = Selectable.Transition.None;
            dd.template = template; dd.captionText = caption; dd.itemText = label; dd.placeholder = hold; dd.alphaFadeSpeed = 0.1f;
            field.gameObject.AddComponent<PressScale>();
            return dd;
        }

        static RectTransform Stretch(Transform parent, string name, float l, float t, float r, float b)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rt.gameObject.layer = 5;
            rt.SetParent(parent, false); Fill(rt, l, t, r, b);
            return rt;
        }

        /// <summary>Stretches over the parent with insets (left, top, right, bottom).</summary>
        static void Fill(RectTransform rt, float l, float t, float r, float b)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        public static GameObject Build()
        {
            var go = Make();
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                Register("FriendTalk", prefab);
                return prefab;
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

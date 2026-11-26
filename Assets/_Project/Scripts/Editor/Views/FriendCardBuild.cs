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
    /// The 11-26 friend card (Figma "캐릭터 선택하면 뜨는창" 88:1912): Assets/_UI수정/3_마을/FriendCard.prefab, listed as "FriendCard".
    /// The panel with everything that never changes on it — the stitched cream board with its 18 % bear-and-flower pattern, the
    /// photo frame, the 프로필 / 꾸며주기 tags, the lines, the home and paw marks — is one Figma picture (Art/UI_1126/Card, drawn by
    /// headless Chrome); the photo, the words and the accessory cards are laid over it. The cards sit six whole ones to a page in a
    /// masked strip and slide a page at a time with ◀ ▶.
    /// </summary>
    public static class FriendCardBuild
    {
        public const string PrefabPath = UiEditFolder.VillageDir + "/FriendCard.prefab";
        // the panel's picture box (frame px). Figma's board runs to x 1900 behind a half-shown seventh card; with six whole cards
        // it is cut 59.7 px shorter (three stitch periods, so the dashes stay even: work/figma_1126/card/narrow.py) so the margin
        // after the last card matches the one before the photo (44 px), and the panel sits in the middle of the screen (the same
        // margin left and right). PX stays Figma's left edge: every piece is placed from it.
        const float PX = 110f, PY = 731f, PW = 1730.5f, PH = 324.5f;
        // the 꾸며주기 side moves 2.4 px left with it (the tag is baked into the picture at the same shift)
        const float RightShift = -2.38f;
        static readonly Vector2 Panel = new Vector2(PW, PH);
        static readonly Color TagBrown = Hex("#816253");
        const float CardW = 130.4f, CardH = 130.6f, Pad = 6f;

        public static GameObject Make()
        {
            var root = Root("FriendCard", 8);   // under the HUD (9): the home, zoom, coins and gear stay on top of the village
            var v = root.gameObject.AddComponent<FriendCardView>();
            var panel = Picture(root, "Panel", "Card/panel.png", 960f - PW / 2f, PY, PW, PH, B);
            panel.raycastTarget = true;   // clicks on the card do not reach the park

            // 프로필: the photo inside the frame's pink (rounded like it), the name, the day it moved in, the kind of toy
            var mask = Node(panel.transform, "Photo", 159.2f - PX, 775.4f - PY, 235.7f, 236.3f, TL, Panel);
            Shape(mask.gameObject, Color.white, Vector4.one * 21.3f).raycastTarget = false;
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var photo = new GameObject("Picture", typeof(RectTransform)).AddComponent<RawImage>(); photo.gameObject.layer = 5;
            photo.transform.SetParent(mask, false); var prt = photo.rectTransform; prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = prt.offsetMax = Vector2.zero;
            photo.raycastTarget = false; photo.enabled = false;
            Words(panel.transform, "ProfileTag", "프로필", 20f, TagBrown, 440.6f - PX, 763.3f - PY, 111.2f, 50.5f, TL, HorizontalAlignmentOptions.Center, "card.profile", Panel);
            v.nameText = Words(panel.transform, "Name", "몽이", 50f, Brown, 455.2f - PX, 817.3f - PY, 340f, 65f, TL, HorizontalAlignmentOptions.Left, null, Panel);
            v.dateText = Words(panel.transform, "MovedIn", "2026.11.12", 30f, Brown, 502.7f - PX, 909.5f - PY, 300f, 39f, TL, HorizontalAlignmentOptions.Left, null, Panel);
            v.kindText = Words(panel.transform, "Kind", "곰인형", 30f, Brown, 502.7f - PX, 976.4f - PY, 300f, 39f, TL, HorizontalAlignmentOptions.Left, null, Panel);
            v.photo = photo;

            // 꾸며주기: the tag, how it works, the strip of cards and the arrows
            Words(panel.transform, "DressTag", "꾸며주기", 20f, TagBrown, 960f + RightShift - PX, 760.9f - PY, 111.2f, 50.9f, TL, HorizontalAlignmentOptions.Center, "card.dress", Panel);
            Words(panel.transform, "Hint", "누르면 입히고, 다시 누르면 벗어요.", 20f, Brown, 1080f + RightShift - PX, 771.8f - PY, 420f, 26f, TL, HorizontalAlignmentOptions.Left, "card.hint", Panel);
            // six whole cards (the Figma strip ends in a half card; the user wants whole ones only, the next page behind ▶)
            float stripW = 5f * 141.3f + CardW + 2f * Pad;
            var viewport = Node(panel.transform, "Cards", 961.9f + RightShift - Pad - PX, 820f - Pad - PY, stripW, CardH + 2f * Pad + 4f, TL, Panel);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>(); hit.color = new Color(1f, 1f, 1f, 0f);   // clicks between the cards stay on the card
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>(); content.gameObject.layer = 5;
            content.SetParent(viewport, false); content.anchorMin = new Vector2(0f, 0f); content.anchorMax = new Vector2(0f, 1f); content.pivot = new Vector2(0f, 0.5f);
            content.sizeDelta = new Vector2(CardW + 2f * Pad, 0f); content.anchoredPosition = Vector2.zero;
            v.content = content;
            v.cardTemplate = Card(content);
            v.emptyText = Words(viewport, "Empty", "가진 악세서리가 없어요. 컵케이크 악세서리점에서 사 와요!", 24f, Brown, 0f, 0f, 900f, 60f, C, HorizontalAlignmentOptions.Center, "card.empty", viewport.sizeDelta);
            v.emptyText.gameObject.SetActive(false);
            v.prevButton = PictureButton(panel.transform, "Btn_Prev", "Card/nav_prev.png", 1328.5f + RightShift - PX, 964f - PY, 72.5f, 72.5f, TL, Panel);
            v.nextButton = PictureButton(panel.transform, "Btn_Next", "Card/nav_next.png", 1433.5f + RightShift - PX, 963f - PY, 72.5f, 72.5f, TL, Panel);

            root.gameObject.SetActive(false);   // the village opens it (FriendCardView.Get)
            return root.gameObject;
        }

        /// <summary>One card (Figma 88:2071 / 88:2069 / 88:2083): white with its shadow, the blue edge while worn, the picture, the
        /// name at the bottom, ×N in the little blue pill at the top right. Kept off; the card copies it for every accessory.</summary>
        static GameObject Card(RectTransform content)
        {
            var cs = new Vector2(CardW, CardH);
            var card = Node(content, "Card", Pad, Pad + 2f, CardW, CardH, TL, new Vector2(CardW + 2f * Pad, CardH + 2f * Pad + 4f));
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0f, 1f); card.anchoredPosition = new Vector2(Pad, -(Pad + 2f));
            var plate = Picture(card, "Plate", "Card/card.png", -1f, -1f, 135f, 135f, TL, cs); plate.raycastTarget = true;
            var b = card.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = plate;
            card.gameObject.AddComponent<PressScale>();
            Picture(card, "Worn", "Card/card_on.png", -0.9f, -1f, 134.5f, 135f, TL, cs).gameObject.SetActive(false);
            var sw = Node(card, "Swatch", 65.2f - 28f, 46f - 28f, 56f, 56f, TL, cs); Shape(sw.gameObject, Color.white, Vector4.one * 28f);
            var icon = Picture(card, "Icon", "Card/card.png", 65.2f - 40f, 46f - 40f, 80f, 80f, TL, cs); icon.sprite = null; icon.preserveAspect = true;
            Words(card, "Name", "빨간 리본", 18.13f, TagBrown, 0f, 98f, CardW, 26f, TL, HorizontalAlignmentOptions.Center, null, cs);
            var badge = Picture(card, "Badge", "Card/badge.png", 90.1f, 9f, 32.5f, 17f, TL, cs);
            Words(badge.transform, "Count", "x1", 11.36f, Brown, 0f, 0f, 32.5f, 17f, TL, HorizontalAlignmentOptions.Center, null, new Vector2(32.5f, 17f));
            card.gameObject.SetActive(false);
            return card.gameObject;
        }

        public static GameObject Build()
        {
            var go = Make();
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                Register("FriendCard", prefab);
                return prefab;
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

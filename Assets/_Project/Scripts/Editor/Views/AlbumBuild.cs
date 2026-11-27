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
    /// The 11-26 photo album (Figma "앨범" 88:1393): Assets/_UI수정/3_마을/Album.prefab, listed as "Album". A fixed-size binder,
    /// so every piece is the Figma vector itself (Art/UI_1126/Album, drawn by headless Chrome so the page patterns show): the two
    /// pages, the rings, the tab, the close button, the notebook paper, the cream photo slot, the gingham frame, the tape, the
    /// cards and the arrows. The book keeps its Figma place (the village's right-hand buttons stay in sight beside it).
    /// </summary>
    public static class AlbumBuild
    {
        public const string PrefabPath = UiEditFolder.VillageDir + "/Album.prefab";

        // Figma (frame px): the book's box (from the tab's top to the pages' bottom, the close button's right edge)
        const float BX = 85.5f, BY = 43f, BW = 1571f, BH = 953f;
        static readonly Vector2 Book = new Vector2(BW, BH);
        // the photo on the left page: a 318.4 square turned 1.86° (Figma rotate(-1.86deg)), centred here
        const float PhotoCX = 437.98f, PhotoCY = 471.98f, PhotoSize = 318.4f, PhotoTurn = 1.86f;
        // the cards (3 x 3) and their size
        static readonly float[] CardX = { 958.1f, 1162.54f, 1366.99f }, CardY = { 168.39f, 395.92f, 623.46f };
        const float CardW = 183.26f, CardH = 212.85f, CardInset = 10f;
        static readonly Color TabBrown = Hex("#816253");

        public static GameObject Make()
        {
            var root = Root("Album", 40);
            var v = root.gameObject.AddComponent<AlbumView>();
            var book = Node(root, "Book", BX, BY, BW, BH, C);

            // the binder: two pages (they take clicks, so nothing behind the book is touched), the left page's paper
            Pic(book, "PageLeft", "page_left", 85.5f, 87f, 763.5f, 909f).raycastTarget = true;
            Pic(book, "PageRight", "page_right", 855.5f, 87f, 763.5f, 909f).raycastTarget = true;
            Pic(book, "Paper", "paper", 133.5f, 168.5f, 632f, 774.5f);

            // the chosen photo: the cream slot when there is none, the gingham frame and the tape over it
            var slot = Turned(book, "Slot", 322f).gameObject.AddComponent<Image>();
            slot.sprite = Art("Album/slot.png"); slot.raycastTarget = false;
            // the photo as it was taken, its own frame pattern and all (user 2026-11-27: "왼쪽 프레임이 우리가 처음 사진 찍을때 프레임
            // 처럼"), on a white polaroid card like the cards on the right (16 round, 70 under)
            var card = Node(book, "Polaroid", 0f, 0f, PhotoSize + 32f, PhotoSize + 86f, C, Book);
            // its middle 27 under the photo's, along the turned card (so the pattern meets the photo's border exactly)
            Centre(card, PhotoCX + 27f * Mathf.Sin(PhotoTurn * Mathf.Deg2Rad), PhotoCY + 27f * Mathf.Cos(PhotoTurn * Mathf.Deg2Rad)); card.localEulerAngles = new Vector3(0f, 0f, PhotoTurn);
            var cardShape = Shape(card.gameObject, Color.white, Vector4.one * 2f);
            cardShape.shadowColor = new Color(0f, 0f, 0f, 0.25f); cardShape.shadowOffset = new Vector2(1.5f, -2f); cardShape.shadowBlur = 3f;
            // the whole card in the photo's frame pattern (AlbumView fills and lines it up with the photo's own border)
            var pat = Node(card, "Pattern", 0f, 0f, PhotoSize + 32f, PhotoSize + 86f, C, new Vector2(PhotoSize + 32f, PhotoSize + 86f)).gameObject.AddComponent<RawImage>();
            pat.rectTransform.anchorMin = Vector2.zero; pat.rectTransform.anchorMax = Vector2.one; pat.rectTransform.offsetMin = pat.rectTransform.offsetMax = Vector2.zero;
            pat.raycastTarget = false;
            card.gameObject.SetActive(false);
            var big = Turned(book, "Photo", PhotoSize).gameObject.AddComponent<RawImage>(); big.raycastTarget = false; big.enabled = false;
            var empty = Words(book, "Empty", "아직 사진이 없어요", 34f, Brown, 0f, 0f, 300f, 50f, C, HorizontalAlignmentOptions.Center, "album.empty", Book);
            Centre(empty.rectTransform, PhotoCX, PhotoCY); empty.rectTransform.localEulerAngles = new Vector3(0f, 0f, PhotoTurn);
            Pic(book, "Tape", "tape", 322.5f, 232f, 219.5f, 94f);
            // Figma: 39.7 px words under the frame, centred on it
            var cap = Words(book, "Caption", "", 39.7f, Brown, 437.6f - 280f - BX, 768.63f - BY, 560f, 52f, TL, HorizontalAlignmentOptions.Center, null, Book);

            // the cards: a white card, its blue edge when chosen, the photo in it like a polaroid (wider margin underneath)
            var cards = Node(book, "Cards", 0f, 0f, BW, BH, TL, Book);
            v.cards = new AlbumView.Card[AlbumView.PerPage];
            for (int i = 0; i < AlbumView.PerPage; i++)
            {
                float x = CardX[i % 3], y = CardY[i / 3];
                var cardSize = new Vector2(187.5f, 217.5f);
                var plate = Pic(cards, "Card" + i, "tile", x - 1.04f, y - 1.39f, cardSize.x, cardSize.y);
                plate.raycastTarget = true;
                var b = plate.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = plate;
                plate.gameObject.AddComponent<PressScale>();
                var mark = Picture(plate.transform, "Chosen", "Album/tile_selected.png", -2.56f, -2.5f, 192.5f, 222.5f, TL, cardSize);
                mark.gameObject.SetActive(false);
                var photo = Node(plate.transform, "Photo", 1.04f + CardInset, 1.39f + CardInset, CardW - 2f * CardInset, CardW - 2f * CardInset, TL, cardSize).gameObject.AddComponent<RawImage>();
                photo.raycastTarget = false; photo.enabled = false;
                v.cards[i] = new AlbumView.Card { button = b, mark = mark.gameObject, photo = photo };
            }

            // the rings over the pages' meeting edges, the tab, the arrows with the page count, the close button
            Pic(book, "Rings", "rings", 784f, 191f, 131.5f, 720f);
            Pic(book, "Tab", "tab", 117f, 43f, 248f, 114.5f);
            Words(book, "TabWords", "앨범", 62f, TabBrown, 180.74f - BX, 52.28f - BY, 118f, 81f, TL, HorizontalAlignmentOptions.Center, "album.title", Book);
            v.prevButton = PictureButton(book, "Btn_Prev", "Album/nav_prev.png", 1070f - BX, 860f - BY, 106f, 105.5f, TL, Book);
            v.pageLabel = Words(book, "Page", "1/1", 36f, Brown, 1241.7f - 70f - BX, 888.82f - BY, 140f, 47f, TL, HorizontalAlignmentOptions.Center, null, Book);
            v.nextButton = PictureButton(book, "Btn_Next", "Album/nav_next.png", 1318f - BX, 858.5f - BY, 105.5f, 105.5f, TL, Book);
            v.closeButton = PictureButton(book, "Btn_Close", "Album/close.png", 1550f - BX, 62f - BY, 106.5f, 106.5f, TL, Book);

            v.bigPhoto = big; v.slot = slot.gameObject; v.caption = cap; v.emptyText = empty; v.polaroid = card.gameObject; v.polaroidPattern = pat; v.bakedBorder = 0f;
            v.bigSize = PhotoSize; v.polaroidSide = 16f; v.polaroidFoot = 70f;
            root.gameObject.SetActive(false);   // the village opens it (AlbumView.Open)
            return root.gameObject;
        }

        /// <summary>An album picture at its Figma box (frame px), inside the book.</summary>
        static Image Pic(Transform book, string name, string art, float x, float y, float w, float h)
            => Picture(book, name, "Album/" + art + ".png", x - BX, y - BY, w, h, TL, Book);

        /// <summary>A square turned like the Figma photo slot, centred on it.</summary>
        static RectTransform Turned(Transform book, string name, float size)
        {
            var rt = Node(book, name, 0f, 0f, size, size, C, Book);
            Centre(rt, PhotoCX, PhotoCY);
            rt.localEulerAngles = new Vector3(0f, 0f, PhotoTurn);
            return rt;
        }

        /// <summary>Puts a centre-pinned piece's middle on a frame point.</summary>
        static void Centre(RectTransform rt, float cx, float cy)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(cx - BX - BW * 0.5f, -(cy - BY - BH * 0.5f));
        }

        public static GameObject Build()
        {
            var go = Make();
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                Register("Album", prefab);
                return prefab;
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

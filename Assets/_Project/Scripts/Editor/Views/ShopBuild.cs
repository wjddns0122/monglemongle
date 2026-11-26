using Mongle.Views;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Mongle.EditorTools.ViewKit;

namespace Mongle.EditorTools
{
    /// <summary>
    /// The 11-26 shop (Figma "상점" 88:1143): Assets/_UI수정/3_마을/Shop.prefab, listed as "Shop". The sheet — the stitched cream
    /// board with its 18 % bear-and-flower pattern and the keeper's field — is one Figma picture (Art/UI_1126/Shop, drawn by headless
    /// Chrome); the back button, the 부탁 보기 pill, the four cards, the arrows and all the words are laid over it. The request rows
    /// are the quest window's rows (88:2130, Art/UI_1126/Quest) at 70 %, as wide as the keeper's field, three to a page where the cards are.
    /// </summary>
    public static class ShopBuild
    {
        public const string PrefabPath = UiEditFolder.VillageDir + "/Shop.prefab";
        // the sheet's picture box (frame px); everything else is placed from its top-left corner
        const float SX = 621f, SY = 153.5f, SW = 1260f, SH = 887f;
        static readonly Vector2 Sheet = new Vector2(SW, SH);
        static readonly Color KeeperPeach = Hex("#FFB58A");
        // the cards (Figma 88:1230, 88:1228, 88:1232, 88:1234: 286.8 apart) and the place they take
        const float CardX = 692f - SX, CardY = 481.8f - SY, CardStep = 286.8f, CardW = 245.6f, CardH = 392.9f;
        // the request rows: the quest window's (1356.6 x 173.7) at 70 % in height and sizes, as wide as the keeper's field
        // (Figma 88:1226, x 672.4, 1153.2 wide) so the edges line up; left parts from the left, the reward and the button from the right
        const float K = 0.7f, RowX = 672.38f - SX, RowW = 1153.23f, RowStep = 133f;

        public static GameObject Make()
        {
            var root = Root("Shop", 10);   // over the HUD (9); the coins, gear and zoom stay where they are, clear of the sheet
            var v = root.gameObject.AddComponent<ShopView>();
            // the sheet stays to the right of the middle on every screen shape (the keeper stands on the left)
            var sheet = Node(root, "Sheet", SX, SY, SW, SH, C);
            var panel = Picture(sheet, "Panel", "Shop/panel.png", 0f, 0f, SW, SH, TL, Sheet);
            panel.raycastTarget = true;   // clicks on the sheet do not reach the park

            // top: ◀ back, the shop's name, 부탁 보기
            v.backButton = PictureButton(sheet, "Btn_나가기", "Shop/back.png", 676.5f - SX, 210f - SY, 95f, 97.5f, TL, Sheet);
            v.titleText = Words(sheet, "Title", "다람 가구점", 50f, Brown, 807.98f - SX, 220.29f - SY, 600f, 65f, TL, HorizontalAlignmentOptions.Left, null, Sheet);
            v.tabButton = PictureButton(sheet, "Btn_tab", "Shop/request.png", 1577f - SX, 213f - SY, 243f, 87f, TL, Sheet);
            v.tabText = Words(v.tabButton.transform, "Label", "부탁 보기 (!)", 30f, Brown, 8f, 20.85f, 239f, 39f, TL, HorizontalAlignmentOptions.Center, null, new Vector2(243f, 87f));

            // the keeper's field: the name, what they say (two lines), the next-step pill after a purchase
            v.keeperText = Words(sheet, "Keeper", "다람", 38f, KeeperPeach, 694.44f - SX, 369.84f - SY, 130f, 49f, TL, HorizontalAlignmentOptions.Center, null, Sheet);
            v.lineText = Words(sheet, "Line", "처음 보는 얼굴이네요! 저는 다람이에요.\n수선하고 받은 몽글 코인으로 마음에 드는 가구를 골라 봐요.", 30f, Brown, 835.12f - SX, 347.56f - SY, v.lineWidth, 104f, TL, HorizontalAlignmentOptions.Left, null, Sheet);
            v.lineText.textWrappingMode = TextWrappingModes.Normal; v.lineText.enableAutoSizing = true; v.lineText.fontSizeMin = 20f; v.lineText.fontSizeMax = 30f;
            v.lineText.lineSpacing = 2f;
            v.lineButton = PictureButton(sheet, "Btn_line", "Shop/request.png", 1797f - SX - 200f, 393.1f - SY - 35.8f, 200f, 71.6f, TL, Sheet);
            v.lineButtonText = Words(v.lineButton.transform, "Label", "지금 놓기", 26f, Brown, 6.6f, 17.2f, 196.7f, 32f, TL, HorizontalAlignmentOptions.Center, null, new Vector2(200f, 71.6f));
            v.lineButtonText.enableAutoSizing = true; v.lineButtonText.fontSizeMin = 18f; v.lineButtonText.fontSizeMax = 26f;
            v.lineButton.gameObject.SetActive(false);

            // the things for sale
            var itemsGroup = Node(sheet, "Items", 0f, 0f, SW, SH, TL, Sheet); v.itemsGroup = itemsGroup.gameObject;
            v.cards = new ShopView.Card[4];
            for (int i = 0; i < 4; i++) v.cards[i] = Card(itemsGroup, i);

            // the requests
            var asksGroup = Node(sheet, "Asks", 0f, 0f, SW, SH, TL, Sheet); v.asksGroup = asksGroup.gameObject;
            v.rows = new AskRow[3];
            for (int i = 0; i < 3; i++) v.rows[i] = AskRowBuild.Make(asksGroup, "Quest_" + i, RowX, CardY + 2.6f + i * RowStep, RowW, K, Sheet);
            asksGroup.gameObject.SetActive(false);
            v.style = AskRowBuild.Style();

            // ◀ 1/2 ▶ and the words for an empty page
            v.prevButton = PictureButton(sheet, "Btn_prev", "Shop/nav_prev.png", 1066f - SX, 885f - SY, 105.5f, 106f, TL, Sheet);
            v.nextButton = PictureButton(sheet, "Btn_next", "Shop/nav_next.png", 1313.5f - SX, 883.5f - SY, 105.5f, 105.5f, TL, Sheet);
            v.pageText = Words(sheet, "Page", "1/2", 36f, Brown, 1178.4f - SX, 913.35f - SY, 120f, 47f, TL, HorizontalAlignmentOptions.Center, null, Sheet);
            v.emptyText = Words(sheet, "Empty", "", 30f, Brown, CardX, CardY, 4f * CardStep - (CardStep - CardW), CardH, TL, HorizontalAlignmentOptions.Center, null, Sheet);
            v.emptyText.textWrappingMode = TextWrappingModes.Normal; v.emptyText.gameObject.SetActive(false);

            root.gameObject.SetActive(false);   // the village opens it (ShopView.Get)
            return root.gameObject;
        }

        /// <summary>One card (Figma 88:1228): the white card, the pink picture plate with the thing on it, its name, the coin and
        /// the price, the pink 구매 pill.</summary>
        static ShopView.Card Card(Transform parent, int i)
        {
            var cs = new Vector2(CardW, CardH);
            var card = Node(parent, "Card_" + i, CardX + i * CardStep, CardY, CardW, CardH, TL, Sheet);
            Picture(card, "Plate", "Shop/card.png", -1.42f, -1.31f, 250f, 397.5f, TL, cs);
            Picture(card, "PictureBack", "Shop/img_bg.png", 29.6f, 24.7f, 196f, 155.5f, TL, cs);
            var icon = Picture(card, "Icon", "Shop/img_bg.png", 127.6f - 82.5f, 102.4f - 82.5f, 165f, 165f, TL, cs); icon.sprite = null; icon.enabled = false;
            var name = Words(card, "Name", "포근 의자", 30f, Brown, 0f, 194f, CardW, 39f, TL, HorizontalAlignmentOptions.Center, null, cs);
            name.enableAutoSizing = true; name.fontSizeMin = 20f; name.fontSizeMax = 30f;
            Picture(card, "Coin", "Shop/coin.png", 21.9f, 268.7f, 43.5f, 43.5f, TL, cs);
            var price = Words(card, "Price", "45", 30f, Brown, 108.9f, 269.75f, 120f, 39f, TL, HorizontalAlignmentOptions.Left, null, cs);
            var buy = PictureButton(card, "Buy", "Shop/buy.png", 29.6f, 323.2f, 196f, 55.5f, TL, cs);
            var buyText = Words(buy.transform, "Label", "구매", 30f, Brown, 0f, 5.45f - 2.5f, 196f, 39f, TL, HorizontalAlignmentOptions.Center, null, new Vector2(196f, 55.5f));
            return new ShopView.Card { root = card.gameObject, icon = icon, name = name, price = price, buy = buy, buyText = buyText };
        }

        public static GameObject Build()
        {
            var go = Make();
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                Register("Shop", prefab);
                return prefab;
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

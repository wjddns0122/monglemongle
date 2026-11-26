using System.Linq;
using Mongle.EditorTools;
using Mongle.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>The 11-26 shop (Figma 88:1143): four cards a page, the keeper's line and its next-step pill, the requests drawn
    /// like the quest window's rows, and the HUD keeping only the coins, gear and zoom.</summary>
    public class ShopViewTests
    {
        static ShopView.Item[] Items(int n, int poorFrom = 99) => Enumerable.Range(0, n).Select(i => new ShopView.Item
        { id = "Fur_" + i, name = "의자 " + i, price = 10 + i, afford = i < poorFrom }).ToArray();

        static Ask Q(string id, AskState st, string progress = null) =>
            new Ask { id = id, title = "첫 화분 놓기", giver = "다람", line = "화분을 하나 사서 마을에 놓아 줄래요?", reward = 10, state = st, progress = progress };

        [Test]
        public void ThePrefabIsWiredAndListed()
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(ShopBuild.PrefabPath);
            Assert.IsNotNull(p, ShopBuild.PrefabPath);
            var v = p.GetComponent<ShopView>();
            Assert.IsNotNull(v.titleText); Assert.IsNotNull(v.backButton); Assert.IsNotNull(v.tabButton); Assert.IsNotNull(v.tabText);
            Assert.IsNotNull(v.keeperText); Assert.IsNotNull(v.lineText); Assert.IsNotNull(v.lineButton); Assert.IsNotNull(v.lineButtonText);
            Assert.AreEqual(4, v.cards.Length); Assert.AreEqual(3, v.rows.Length);
            foreach (var c in v.cards) { Assert.IsNotNull(c.root); Assert.IsNotNull(c.icon); Assert.IsNotNull(c.buy); Assert.IsNotNull(c.price); }
            foreach (var r in v.rows) { Assert.IsNotNull(r.plate); Assert.IsNotNull(r.picture); Assert.IsNotNull(r.button); Assert.IsNotNull(r.done); Assert.IsNotNull(r.rewardPill); }
            Assert.IsNotNull(v.style.openButton); Assert.IsNotNull(v.style.goButton); Assert.AreEqual(3, v.style.giverColors.Length);
            Assert.AreEqual("Btn_나가기", v.backButton.name, "QA presses it as 나가기");
            Assert.AreEqual(p, ViewIndex.Prefab("Shop"));
        }

        [Test]
        public void FourCardsAPageAndBuyingReportsTheThing()
        {
            var go = ShopBuild.Make();
            try
            {
                var v = go.GetComponent<ShopView>();
                v.Show("다람 가구점", "다람", "어서 와요!", Items(6, poorFrom: 5), null);
                Assert.IsTrue(v.Visible); Assert.AreEqual("다람 가구점", v.titleText.text); Assert.AreEqual("다람", v.keeperText.text);
                CollectionAssert.AreEqual(new[] { "Card_Fur_0", "Card_Fur_1", "Card_Fur_2", "Card_Fur_3" }, v.ShownCards.ToArray());
                Assert.AreEqual("10", v.cards[0].price.text);
                Assert.AreEqual(2, v.PageCount); Assert.IsTrue(v.pageText.gameObject.activeSelf); Assert.AreEqual("1/2", v.pageText.text);
                Assert.IsFalse(v.tabButton.gameObject.activeSelf, "no requests: no 부탁 보기");
                string bought = null; v.OnBuy = id => bought = id;
                Assert.IsTrue(v.QaClick("Buy_Fur_2")); Assert.AreEqual("Fur_2", bought);
                v.Turn(1);
                CollectionAssert.AreEqual(new[] { "Card_Fur_4", "Card_Fur_5" }, v.ShownCards.ToArray());
                Assert.AreEqual(v.poorTint, v.cards[1].buy.targetGraphic.color, "too dear: the pill is greyed");
                Assert.IsTrue(v.QaClick("Buy_Fur_5"), "still pressable: the keeper says the coins are short");
                Assert.AreEqual("Fur_5", bought);
                v.SetItems(Items(3));
                Assert.AreEqual(0, v.Page); Assert.IsFalse(v.pageText.gameObject.activeSelf, "one page: no arrows");
                Assert.IsFalse(v.prevButton.gameObject.activeSelf);
                bool closed = false; v.OnClose = () => closed = true;
                v.backButton.onClick.Invoke(); Assert.IsTrue(closed);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheKeepersLineCanOfferTheNextStep()
        {
            var go = ShopBuild.Make();
            try
            {
                var v = go.GetComponent<ShopView>();
                v.Show("다람 가구점", "다람", "어서 와요!", Items(2), null);
                Assert.IsFalse(v.LineActionShown); Assert.AreEqual(v.lineWidth, v.lineText.rectTransform.sizeDelta.x);
                bool placed = false;
                v.SetLine("보관함에 넣어 뒀어요.", "지금 놓기", () => placed = true);
                Assert.IsTrue(v.LineActionShown); Assert.AreEqual("지금 놓기", v.lineButtonText.text);
                Assert.AreEqual(v.lineWidthWithButton, v.lineText.rectTransform.sizeDelta.x, "the line makes room for the pill");
                Assert.IsTrue(v.QaLineAction()); Assert.IsTrue(placed);
                v.SetLine("고마워요!");
                Assert.IsFalse(v.LineActionShown); Assert.AreEqual("고마워요!", v.LineShown);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheRequestsAreRowsWithTheRightButton()
        {
            var go = ShopBuild.Make();
            try
            {
                var v = go.GetComponent<ShopView>();
                var asks = new[] { Q("Q_A", AskState.Open), Q("Q_B", AskState.Doing, "1/3"), Q("Q_C", AskState.Ready), Q("Q_D", AskState.Done) };
                v.Show("다람 가구점", "다람", "어서 와요!", Items(4), asks);
                Assert.IsFalse(v.QuestTabOpen); Assert.IsTrue(v.tabButton.gameObject.activeSelf);
                Assert.AreEqual("부탁 보기 (!)", v.tabText.text, "a request waits for an answer");
                v.tabButton.onClick.Invoke();
                Assert.IsTrue(v.QuestTabOpen); Assert.IsFalse(v.itemsGroup.activeSelf); Assert.IsTrue(v.asksGroup.activeSelf);
                Assert.AreEqual("물건 보기", v.tabText.text);
                CollectionAssert.AreEqual(new[] { "Quest_Q_A", "Quest_Q_B", "Quest_Q_C" }, v.ShownRows.ToArray());
                Assert.AreEqual(2, v.PageCount, "three rows a page");
                Assert.AreEqual("다", v.rows[0].initial.text); Assert.AreEqual("다람 : 화분을 하나 사서 마을에 놓아 줄래요?", v.rows[0].line.text);
                Assert.AreEqual("+10", v.rows[0].reward.text);
                Assert.AreEqual("Quest_Accept_Q_A", v.rows[0].button.name); Assert.AreEqual("수락할게요", v.rows[0].buttonText.text);
                Assert.AreEqual("Quest_Go_Q_B", v.rows[1].button.name); Assert.AreEqual(v.style.goButton, ((UnityEngine.UI.Image)v.rows[1].button.targetGraphic).sprite);
                StringAssert.Contains("1/3", v.rows[1].title.text);
                var pic = Sprite.Create(new Texture2D(4, 4), new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
                var withPic = asks.ToArray(); withPic[0].picture = pic;
                v.SetAsks(withPic);
                Assert.AreEqual(pic, v.rows[0].picture.sprite); Assert.IsTrue(v.rows[0].picture.enabled);
                Assert.IsFalse(v.rows[0].initial.gameObject.activeSelf, "a picture of the thing instead of the letter");
                Assert.IsFalse(v.rows[1].picture.enabled); Assert.IsTrue(v.rows[1].initial.gameObject.activeSelf);
                v.SetAsks(asks);
                Assert.AreEqual("Quest_Claim_Q_C", v.rows[2].button.name); Assert.AreEqual("보상 받기", v.rows[2].buttonText.text);
                string acc = null, claim = null, go2 = null; v.OnAccept = id => acc = id; v.OnClaim = id => claim = id; v.OnGo = id => go2 = id;
                Assert.IsTrue(v.QaClick("Quest_Accept_Q_A")); Assert.AreEqual("Q_A", acc);
                Assert.IsTrue(v.QaClick("Quest_Claim_Q_C")); Assert.AreEqual("Q_C", claim);
                Assert.IsTrue(v.QaClick("Quest_Go_Q_B")); Assert.AreEqual("Q_B", go2);
                v.Turn(1);
                var done = v.rows[0];
                Assert.AreEqual("Quest_Q_D", done.root.name);
                Assert.IsTrue(done.initial.gameObject.activeSelf, "no picture: the giver's first letter");
                Assert.IsTrue(done.done.activeSelf); Assert.IsFalse(done.button.gameObject.activeSelf); Assert.IsFalse(done.rewardPill.activeSelf);
                Assert.AreEqual(v.style.rowDoneColor, done.plate.color); Assert.AreEqual(v.style.rowColor, v.rows[1].plate.color);
                Assert.AreEqual(v.style.doneIconColor, v.rows[0].icon.color);
                v.SetAsks(new[] { Q("Q_D", AskState.Done) });
                Assert.AreEqual(1, v.PageCount); Assert.AreEqual(0, v.Page, "fewer rows: back to the page that still exists");
                v.ShowAsks(false);
                Assert.AreEqual("부탁 보기", v.tabText.text, "nothing waits: no (!)");
                v.Show("다람 가구점", "다람", "보상을 받아 가요.", Items(1), asks, openAsks: true);
                Assert.IsTrue(v.QuestTabOpen, "a reward waits: it opens on the requests");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheRowsAreAsWideAsTheKeepersField()
        {
            var go = ShopBuild.Make();
            try
            {
                var v = go.GetComponent<ShopView>();
                var row = (RectTransform)v.rows[0].root.transform;
                Assert.AreEqual(1153.23f, row.sizeDelta.x, 0.01f, "the field's width (Figma 88:1226)");
                Assert.AreEqual(672.38f - 621f, row.anchoredPosition.x, 0.01f, "its left edge");
                var btn = (RectTransform)v.rows[0].button.transform;
                Assert.AreEqual(new Vector2(1f, 1f), btn.anchorMin, "the button keeps to the right end");
                Assert.IsTrue(v.rows[0].plate is Mongle.UI2.SoftRect s && s.stitch, "a drawn plate: the stitch keeps its spacing at any width");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheHudKeepsOnlyCoinsGearAndZoomInAShop()
        {
            var go = VillageHudBuild.Make();
            try
            {
                var h = go.GetComponent<VillageHudView>();
                h.ShowToolbar(true); h.SetTracker(new[] { "첫 화분 놓기  0/1" });
                h.Focus(true, false);
                Assert.IsFalse(h.HomeShown); Assert.IsFalse(h.questButton.gameObject.activeSelf); Assert.IsFalse(h.toolbar.activeSelf); Assert.AreEqual("", h.TrackerText);
                Assert.IsTrue(h.coins.gameObject.activeInHierarchy); Assert.IsTrue(h.gearButton.gameObject.activeInHierarchy); Assert.IsTrue(h.zoom.activeSelf);
                h.Focus(false);
                Assert.IsTrue(h.HomeShown); Assert.IsTrue(h.questButton.gameObject.activeSelf); Assert.IsTrue(h.toolbar.activeSelf);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

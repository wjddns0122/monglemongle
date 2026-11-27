using System.Collections.Generic;
using System.Linq;
using Mongle.EditorTools;
using Mongle.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>Dressing up after the stamp (Figma 115:1573).</summary>
    public class CustomizeViewTests
    {
        static List<CustomizeView.Item> Items(int n, int count = -1) =>
            Enumerable.Range(0, n).Select(i => new CustomizeView.Item { id = "Acc_" + i, name = "악세서리 " + i, count = count }).ToList();

        [Test]
        public void ThePrefabIsWiredAndListed()
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(CustomizeBuild.PrefabPath);
            Assert.IsNotNull(p, CustomizeBuild.PrefabPath);
            var v = p.GetComponent<CustomizeView>();
            Assert.IsNotNull(v.title); Assert.IsNotNull(v.panel); Assert.IsNotNull(v.confirmButton); Assert.IsNotNull(v.prevButton); Assert.IsNotNull(v.nextButton);
            Assert.AreEqual(5, v.cards.Length, "Figma: five cards a page");
            Assert.IsNotNull(v.plateOff); Assert.IsNotNull(v.plateOn);
            foreach (var c in v.cards) { Assert.IsNotNull(c.button); Assert.IsNotNull(c.label); Assert.IsNotNull(c.badge); Assert.IsNotNull(c.icon); }
            Assert.AreEqual(p, ViewIndex.Prefab("Customize"));
        }

        [Test]
        public void ThePanelStandsInTheMiddleAtTheFoot()
        {
            var go = CustomizeBuild.Make();
            try
            {
                var v = go.GetComponent<CustomizeView>();
                Assert.AreEqual(new Vector2(0.5f, 0f), v.panel.anchorMin);
                // its node (506.61 + 971.14 / 2 = 992.18 in the frame) has its middle on the screen's: the picture box
                // (507.5 + 971.5 / 2 = 993.25) stands 1.07 right of the middle
                Assert.AreEqual(993.25f - 992.18f, v.panel.anchoredPosition.x, 0.01f);
                Assert.AreEqual(1080f - 727.5f - 292f, v.panel.anchoredPosition.y, 0.01f, "the frame's distance from the foot");
                Assert.AreEqual(-97.75f, v.title.anchoredPosition.y, 0.01f, "the pill's middle at y 97.75 like the village hint");
                // its insides are centred (user 2026-11-27: "Center로 고정"): the card row in the panel's middle, the head row spanning it
                var c0 = (RectTransform)v.cards[0].button.transform; var c4 = (RectTransform)v.cards[4].button.transform;
                float rowL = c0.anchoredPosition.x + 1.4f, rowR = c4.anchoredPosition.x + 1.4f + 155.42f;   // the card nodes
                float mid = 992.18f - 507.5f;   // the panel node's middle in the panel picture
                Assert.AreEqual(mid, (rowL + rowR) / 2f, 0.05f);
                var label = (RectTransform)v.panel.Find("Label"); var ok = (RectTransform)v.confirmButton.transform;
                Assert.AreEqual(rowL, label.anchoredPosition.x, 0.05f, "꾸며주기 starts with the first card");
                Assert.AreEqual(rowR, ok.anchoredPosition.x + ok.sizeDelta.x, 0.05f, "이대로 확정 ends with the last card");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void FiveCardsAPageTheArrowsTurnThemAndWornCardsGetTheEdge()
        {
            var go = CustomizeBuild.Make();
            try
            {
                var v = go.GetComponent<CustomizeView>();
                v.Show("악세서리점에서 산 것으로 꾸며줘요", Items(7, 2));
                Assert.AreEqual(2, v.Pages);
                Assert.IsTrue(v.cards.All(c => c.button.gameObject.activeSelf));
                Assert.AreEqual("악세서리 0", v.cards[0].label.text);
                Assert.IsTrue(v.cards[0].badge.gameObject.activeSelf); Assert.AreEqual("x2", v.cards[0].count.text);
                Assert.IsFalse(v.prevButton.interactable); Assert.IsTrue(v.nextButton.interactable);
                v.SetWorn(new[] { "Acc_1" });
                Assert.AreEqual(v.plateOn, v.cards[1].plate.sprite); Assert.AreEqual(v.plateOff, v.cards[0].plate.sprite);
                v.Turn(1);
                Assert.AreEqual(1, v.Page);
                Assert.AreEqual("악세서리 5", v.cards[0].label.text);
                Assert.IsTrue(v.cards[1].button.gameObject.activeSelf); Assert.IsFalse(v.cards[2].button.gameObject.activeSelf, "two left on the last page");
                Assert.IsTrue(v.prevButton.interactable); Assert.IsFalse(v.nextButton.interactable);
                Assert.AreEqual(v.plateOff, v.cards[1].plate.sprite, "Acc_6 is not worn");
                v.Turn(5); Assert.AreEqual(1, v.Page, "no page past the last");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void MongisGiftsHaveNoBadgeAndAnEmptyBoxSaysSo()
        {
            var go = CustomizeBuild.Make();
            try
            {
                var v = go.GetComponent<CustomizeView>();
                v.Show("마음에 드는 것을 골라 꾸며줘요", Items(3));
                Assert.AreEqual("마음에 드는 것을 골라 꾸며줘요", v.TitleText);
                Assert.IsFalse(v.cards[0].badge.gameObject.activeSelf, "the free gifts have no count");
                Assert.IsFalse(v.cards[3].button.gameObject.activeSelf);
                // three cards gather in the middle of the row
                float a = ((RectTransform)v.cards[0].button.transform).anchoredPosition.x, c = ((RectTransform)v.cards[2].button.transform).anchoredPosition.x;
                Assert.AreEqual(992.18f - 507.5f, (a + c) / 2f + 1.4f + 155.42f / 2f, 0.05f, "the shown cards' middle is the panel's");
                Assert.IsFalse(v.emptyText.gameObject.activeSelf);
                Assert.AreEqual(v.titleText.GetPreferredValues(v.TitleText, 99999f, 0f).x + 2f * v.titlePad, v.title.sizeDelta.x, 0.5f, "the pill hugs its words, 62 each side");
                v.Show("이번엔 그대로 가도 괜찮아요", new List<CustomizeView.Item>(), "아직 가진 악세서리가 없어요.");
                Assert.IsTrue(v.emptyText.gameObject.activeSelf);
                Assert.IsTrue(v.cards.All(c => !c.button.gameObject.activeSelf));
                // a bigger count makes the badge wider, to the left
                v.Show("x", Items(1, 12));
                Assert.Greater(v.cards[0].badge.sizeDelta.x, v.badgeMinWidth - 0.01f);
                Assert.AreEqual(new Vector2(1f, 1f), v.cards[0].badge.pivot);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

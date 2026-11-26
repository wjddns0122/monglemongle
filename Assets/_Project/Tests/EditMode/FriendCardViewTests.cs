using System.Linq;
using Mongle.EditorTools;
using Mongle.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>The 11-26 friend card (Figma 88:1912): 프로필 on the left, 꾸며주기 cards on the right, and the HUD stepping aside.</summary>
    public class FriendCardViewTests
    {
        static FriendCardView.Item[] Items(int n, int wornAt = -1) => Enumerable.Range(0, n).Select(i => new FriendCardView.Item
        { id = "Acc_" + i, name = "리본 " + i, color = Color.red, count = i % 3, worn = i == wornAt }).ToArray();

        [Test]
        public void ThePrefabIsWiredAndListed()
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(FriendCardBuild.PrefabPath);
            Assert.IsNotNull(p, FriendCardBuild.PrefabPath);
            var v = p.GetComponent<FriendCardView>();
            Assert.IsNotNull(v.photo); Assert.IsNotNull(v.nameText); Assert.IsNotNull(v.dateText); Assert.IsNotNull(v.kindText);
            Assert.IsNotNull(v.content); Assert.IsNotNull(v.cardTemplate); Assert.IsNotNull(v.emptyText);
            Assert.IsNotNull(v.prevButton); Assert.IsNotNull(v.nextButton);
            foreach (var part in new[] { "Plate", "Worn", "Icon", "Swatch", "Name", "Badge" }) Assert.IsNotNull(v.cardTemplate.transform.Find(part), part);
            Assert.AreEqual(p, ViewIndex.Prefab("FriendCard"));
        }

        [Test]
        public void TheCardShowsTheFriendAndItsAccessories()
        {
            var go = FriendCardBuild.Make(); var pic = new Texture2D(8, 8);
            try
            {
                var v = go.GetComponent<FriendCardView>();
                v.Show("몽이", pic, "2026.11.12", "곰인형", Items(4, wornAt: 2));
                Assert.AreEqual("몽이", v.NameShown); Assert.AreEqual("2026.11.12", v.dateText.text); Assert.AreEqual("곰인형", v.kindText.text);
                Assert.IsTrue(v.photo.enabled); Assert.AreEqual(pic, v.photo.texture);
                Assert.AreEqual(v.bakedBorder, v.photo.uvRect.xMin, 1e-5f, "the frame painted into the photo is cut off");
                var shown = v.content.Cast<Transform>().Where(t => t.gameObject.activeSelf).ToList();
                Assert.AreEqual(4, shown.Count);
                Assert.AreEqual("Dress_Acc_2", shown[2].name);
                Assert.IsTrue(shown[2].Find("Worn").gameObject.activeSelf); Assert.IsFalse(shown[1].Find("Worn").gameObject.activeSelf);
                Assert.IsFalse(shown[0].Find("Badge").gameObject.activeSelf, "nothing in the box: no ×N");
                Assert.AreEqual("x2", shown[2].Find("Badge/Count").GetComponent<TMPro.TMP_Text>().text);
                Assert.IsFalse(v.emptyText.gameObject.activeSelf);
                string got = null; v.OnToggle = id => got = id;
                Assert.IsTrue(v.QaToggle("Acc_1")); Assert.AreEqual("Acc_1", got);
                v.SetItems(Items(2));
                Assert.AreEqual(2, v.content.Cast<Transform>().Count(t => t.gameObject.activeSelf), "fewer cards after taking one off the list");
                v.SetItems(new FriendCardView.Item[0]);
                Assert.IsTrue(v.emptyText.gameObject.activeSelf);
                v.Hide();
                Assert.IsFalse(v.Visible); Assert.IsTrue(pic == null, "the card lets the photo go");
            }
            finally { Object.DestroyImmediate(go); if (pic != null) Object.DestroyImmediate(pic); }
        }

        [Test]
        public void SixWholeCardsAPageTheArrowsTurnThem()
        {
            var go = FriendCardBuild.Make();
            try
            {
                var v = go.GetComponent<FriendCardView>();
                var strip = (RectTransform)v.content.parent;
                var t = (RectTransform)v.cardTemplate.transform;
                float inner = strip.sizeDelta.x - 2f * t.anchoredPosition.x;
                Assert.AreEqual(v.perPage, Mathf.FloorToInt((inner - t.sizeDelta.x) / v.step + 1e-3f) + 1, "the strip holds exactly six whole cards, no half one");
                v.Show("몽이", null, "", "", Items(9));
                Assert.AreEqual(2, v.PageCount); Assert.AreEqual(0, v.Page);
                Assert.IsFalse(v.prevButton.interactable); Assert.IsTrue(v.nextButton.interactable);
                v.Turn(1);
                Assert.AreEqual(1, v.Page); Assert.AreEqual(-6 * v.step, v.TargetX, 1e-3f, "a whole page of cards slides in");
                Assert.IsTrue(v.prevButton.interactable); Assert.IsFalse(v.nextButton.interactable);
                v.Turn(1); Assert.AreEqual(1, v.Page, "nothing past the last page");
                v.SetItems(Items(5));
                Assert.AreEqual(0, v.Page, "fewer cards: back to the page that still exists");
                Assert.IsFalse(v.nextButton.interactable);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ThePanelSitsInTheMiddleWithEvenMargins()
        {
            var go = FriendCardBuild.Make();
            try
            {
                var v = go.GetComponent<FriendCardView>();
                var panel = (RectTransform)go.transform.Find("Panel");
                Assert.AreEqual(0f, panel.anchoredPosition.x, 0.01f, "the same room left and right of the panel");
                var photo = (RectTransform)panel.Find("Photo");
                var strip = (RectTransform)v.content.parent; var t = (RectTransform)v.cardTemplate.transform;
                float left = photo.anchoredPosition.x - 5f;   // the photo sits 5 px inside its frame (Figma 88:2065 at 154.2, photo at 159.2)
                float lastCardRight = strip.anchoredPosition.x + t.anchoredPosition.x + (v.perPage - 1) * v.step + t.sizeDelta.x;
                Assert.AreEqual(left, panel.sizeDelta.x - lastCardRight, 1f, "the margin after the last card matches the one before the photo");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheHudStepsAsideForTheCard()
        {
            var go = VillageHudBuild.Make();
            try
            {
                var h = go.GetComponent<VillageHudView>();
                h.ShowToolbar(true); h.SetTracker(new[] { "첫 화분 놓기  0/1" });
                h.Focus(true);
                Assert.IsFalse(h.questButton.gameObject.activeSelf); Assert.IsFalse(h.toolbar.activeSelf); Assert.AreEqual("", h.TrackerText);
                Assert.IsTrue(h.homeButton.gameObject.activeInHierarchy); Assert.IsTrue(h.gearButton.gameObject.activeInHierarchy); Assert.IsTrue(h.zoom.activeSelf);
                h.SetTracker(new[] { "첫 화분 놓기  0/1" }); Assert.AreEqual("", h.TrackerText, "nothing comes back while the card is open");
                h.Focus(false);
                Assert.IsTrue(h.questButton.gameObject.activeSelf); Assert.IsTrue(h.toolbar.activeSelf);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

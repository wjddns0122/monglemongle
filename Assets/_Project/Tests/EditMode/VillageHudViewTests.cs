using Mongle.EditorTools;
using Mongle.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>The 11-26 village HUD (Figma 88:231 / 88:510 / 88:2367): wired, listed, and its parts show what the village tells it.</summary>
    public class VillageHudViewTests
    {
        [Test]
        public void ThePrefabIsWiredAndListed()
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(VillageHudBuild.PrefabPath);
            Assert.IsNotNull(p, VillageHudBuild.PrefabPath);
            var v = p.GetComponent<VillageHudView>();
            foreach (var b in new[] { v.homeButton, v.questButton, v.gearButton, v.zoomInButton, v.zoomOutButton, v.furnitureButton, v.albumButton, v.storageButton, v.rideButton })
                Assert.IsNotNull(b);
            Assert.AreEqual(3, v.glows.Length); Assert.AreEqual(3, v.tips.Length);
            Assert.IsNotNull(v.hint); Assert.IsNotNull(v.tracker); Assert.IsNotNull(v.rideBar); Assert.IsNotNull(v.coinText);
            Assert.AreEqual("Btn_보관함", v.storageButton.name, "the QA run finds picture-only tiles by name");
            Assert.AreEqual(p, ViewIndex.Prefab("VillageHud"));
        }

        [Test]
        public void TheHintPillHugsItsWordsAndHides()
        {
            var go = VillageHudBuild.Make();
            try
            {
                var v = go.GetComponent<VillageHudView>();
                v.Hint("짧게");
                Assert.IsTrue(v.HintShown); Assert.AreEqual(v.hintMinWidth, v.hint.sizeDelta.x, 0.01f);
                v.Hint("오른쪽 버튼으로 끌거나 W·A·S·D로 공원을 둘러봐요.\n휠로 멀리 볼 수 있어요.");
                Assert.Greater(v.hint.sizeDelta.x, v.hintMinWidth); Assert.LessOrEqual(v.hint.sizeDelta.x, v.hintMaxWidth);
                Assert.Greater(v.hint.sizeDelta.y, 100f, "two lines make it taller");
                Assert.AreEqual(Rect.zero, v.FreeTopBand(), "no room for notices while the pill shows");
                v.Hint(null);
                Assert.IsFalse(v.HintShown); Assert.AreEqual("", v.HintText);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void NewsCoinsAndTheTracker()
        {
            var go = VillageHudBuild.Make();
            try
            {
                var v = go.GetComponent<VillageHudView>();
                v.SetCoins(160); Assert.AreEqual("160", v.CoinsText);
                v.SetQuestNews(2); Assert.IsTrue(v.questBadge.activeSelf); Assert.AreEqual("새 2", v.questBadgeText.text);
                v.SetQuestNews(0); Assert.IsFalse(v.questBadge.activeSelf);
                v.SetStorageNews(1); Assert.IsTrue(v.storageBadge.activeSelf);
                v.SetTracker(new[] { "첫 화분 놓기  0/1", "보리 · 시소 같이 타기" });
                Assert.AreEqual("첫 화분 놓기  0/1\n보리 · 시소 같이 타기", v.TrackerText);
                Assert.Greater(v.tracker.sizeDelta.y, 80f);
                v.SetTracker(new string[0]); Assert.AreEqual("", v.TrackerText);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheTilesTheirTipsAndTheRideBar()
        {
            var go = VillageHudBuild.Make();
            try
            {
                var v = go.GetComponent<VillageHudView>();
                foreach (var t in v.tips) Assert.IsFalse(t.activeSelf);
                v.SetPlacing(true);
                Assert.IsTrue(v.glows[0].activeSelf, "the 가구 tile keeps its blue edge while moving furniture");
                Assert.IsFalse(v.tips[0].activeSelf);
                v.SetPlacing(false); Assert.IsFalse(v.glows[0].activeSelf);
                v.ShowTips(5f);
                Assert.IsTrue(v.TipsShown);
                for (int i = 0; i < 3; i++) { Assert.IsTrue(v.tips[i].activeSelf); Assert.IsTrue(v.glows[i].activeSelf); }
                // admission: no tiles until Mongi has moved in, and the HUD coming back does not bring them early
                v.ShowToolbar(false); v.ShowHud(false); v.ShowHud(true);
                Assert.IsFalse(v.toolbar.activeSelf); Assert.IsTrue(v.top.activeSelf); Assert.IsTrue(v.zoom.activeSelf);
                v.ShowToolbar(true); Assert.IsTrue(v.toolbar.activeSelf);
                // watching a ride: the HUD steps aside for the bar
                v.ShowHud(false); v.ShowRide("몽이가 케이크 기차 타러 오고 있어요");
                Assert.IsFalse(v.top.activeSelf); Assert.IsFalse(v.toolbar.activeSelf);
                Assert.AreEqual("몽이가 케이크 기차 타러 오고 있어요", v.RideText);
                v.HideRide(); Assert.IsFalse(v.RideShown);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheLeftColumnSharesOneEdge()
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(VillageHudBuild.PrefabPath);
            var v = p.GetComponent<VillageHudView>();
            // home and the zoom pictures have a 1 px clear margin, the 퀘스트 pill none: their visible left edges line up
            Assert.AreEqual(VillageHudBuild.LeftEdge, ((RectTransform)v.homeButton.transform).anchoredPosition.x + 1f, 0.01f, "home");
            Assert.AreEqual(VillageHudBuild.LeftEdge, ((RectTransform)v.questButton.transform).anchoredPosition.x, 0.01f, "퀘스트");
            Assert.AreEqual(VillageHudBuild.LeftEdge, ((RectTransform)v.zoomInButton.transform).anchoredPosition.x + 1f, 0.01f, "+");
            Assert.AreEqual(VillageHudBuild.LeftEdge, ((RectTransform)v.zoomOutButton.transform).anchoredPosition.x + 1f, 0.01f, "−");
        }
    }
}

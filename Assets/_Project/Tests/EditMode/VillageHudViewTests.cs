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
                // something new: the Figma 115:1398 dot (no "새 N" any more) on 퀘스트, 앨범 and 보관함
                v.SetQuestNews(2); Assert.IsTrue(v.questBadge.activeSelf); Assert.IsNull(v.questBadgeText);
                v.SetQuestNews(0); Assert.IsFalse(v.questBadge.activeSelf);
                v.SetStorageNews(1); Assert.IsTrue(v.storageBadge.activeSelf);
                v.SetAlbumNews(1); Assert.IsTrue(v.AlbumNewsShown); v.SetAlbumNews(0); Assert.IsFalse(v.AlbumNewsShown);
                foreach (var dot in new[] { v.questBadge, v.storageBadge, v.albumBadge })
                {
                    var s = dot.GetComponent<Mongle.UI2.SoftRect>();
                    Assert.AreEqual(Mongle.EditorTools.ViewKit.Hex("#FD95A9"), s.color, dot.name); Assert.AreEqual(3f, s.borderWidth, 0.01f); Assert.AreEqual(Color.white, s.borderColor);
                    Assert.AreEqual(23.5f, ((RectTransform)dot.transform).sizeDelta.x, 0.01f);
                }
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
                // moving furniture (Figma 115:1398): the placing screen's ◀ takes the home button's place, no blue edges
                v.SetTracker(new[] { "첫 화분 놓기  0/1" });
                v.SetPlacing(true);
                Assert.IsFalse(v.glows[0].activeSelf); Assert.IsFalse(v.tips[0].activeSelf);
                Assert.IsFalse(v.HomeShown, "◀ stands there"); Assert.IsFalse(v.questButton.gameObject.activeSelf); Assert.AreEqual("", v.TrackerText);
                Assert.IsTrue(v.toolbar.activeSelf); Assert.IsTrue(v.zoom.activeSelf); Assert.IsTrue(v.coins.gameObject.activeInHierarchy);
                v.SetPlacing(false);
                Assert.IsTrue(v.HomeShown); Assert.IsTrue(v.questButton.gameObject.activeSelf);
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
            // home and the zoom pictures have a 1 px clear margin: their visible left edges line up; 퀘스트 stands where Figma 115:1255
            // has it (user 2026-11-27)
            Assert.AreEqual(VillageHudBuild.LeftEdge, ((RectTransform)v.homeButton.transform).anchoredPosition.x + 1f, 0.01f, "home");
            Assert.AreEqual(43.68f, ((RectTransform)v.questButton.transform).anchoredPosition.x, 0.01f, "퀘스트");
            Assert.AreEqual(VillageHudBuild.LeftEdge, ((RectTransform)v.zoomInButton.transform).anchoredPosition.x + 1f, 0.01f, "+");
            Assert.AreEqual(VillageHudBuild.LeftEdge, ((RectTransform)v.zoomOutButton.transform).anchoredPosition.x + 1f, 0.01f, "−");
        }

        [Test]
        public void TheCornerButtonsStandWhereEveryScreenHasThem()
        {
            var go = VillageHudBuild.Make();
            try
            {
                var v = go.GetComponent<VillageHudView>();
                // Figma 115:1255: home 99.52 at (50, 48), its picture 1 px out on the top-left; the gear at (1791, 48), 2.5 out
                var home = (RectTransform)v.homeButton.transform; var gear = (RectTransform)v.gearButton.transform;
                Assert.AreEqual(49f, home.anchoredPosition.x, 0.01f); Assert.AreEqual(-47f, home.anchoredPosition.y, 0.01f);
                Assert.AreEqual(1788.5f + 107f - 1920f, gear.anchoredPosition.x, 0.01f); Assert.AreEqual(-45.5f, gear.anchoredPosition.y, 0.01f);
                v.Hint("오른쪽 버튼으로 끌거나 W.A.S.D로 공원을 둘러봐요.\n휠로 멀리 볼 수 있어요.");
                Assert.AreEqual(142.6f, v.hint.sizeDelta.y, 4f, "two lines make the Figma pill (94.6 + 24 + 24)");
                Assert.AreEqual(-97.75f, v.hint.anchoredPosition.y, 0.01f, "the pill's middle stays at y 97.75");
                v.Hint("옮길 가구를 눌러서 끌어요.");
                Assert.AreEqual(97f, v.hint.sizeDelta.y, 3f, "one line: Figma 115:1398's 97 tall pill");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheTilesStandEvenlyApartAndQuestIsTheFigmaPill()
        {
            var go = VillageHudBuild.Make();
            try
            {
                var v = go.GetComponent<VillageHudView>();
                // user 2026-11-27: the same gap between P, E and T (and between their keys and tips)
                // the tiles' nodes (114.45): their pictures sit 0.4 / 0.3 / 0.3 up of them, pinned at their right middle
                float NodeTop(UnityEngine.UI.Button btn, float up) { var r = (RectTransform)btn.transform; return 540f - r.anchoredPosition.y - r.sizeDelta.y / 2f + up; }
                float y0 = NodeTop(v.furnitureButton, 0.4f), y1 = NodeTop(v.albumButton, 0.3f), y2 = NodeTop(v.storageButton, 0.3f);
                Assert.AreEqual(535.515f, y1, 0.01f, "the middle tile stays");
                Assert.AreEqual(114.45f + 30f, y1 - y0, 0.01f, "30 between the tiles (user 2026-11-27)"); Assert.AreEqual(y1 - y0, y2 - y1, 0.01f, "even gaps");
                var tips = v.tips; float a = ((RectTransform)tips[0].transform).anchoredPosition.y, b = ((RectTransform)tips[1].transform).anchoredPosition.y, c = ((RectTransform)tips[2].transform).anchoredPosition.y;
                Assert.AreEqual(a - b, b - c, 0.01f, "the tips too");
                // 퀘스트: Figma 115:1386, fully round ends
                var q = v.questButton.targetGraphic as Mongle.UI2.SoftRect;
                Assert.IsNotNull(q); Assert.AreEqual(47.135f, q.radius.x, 0.01f); Assert.AreEqual(0.6f, q.color.a, 0.01f);
                Assert.AreEqual(new Vector2(186.8f, 94.27f), ((RectTransform)v.questButton.transform).sizeDelta);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheStarHuntStandsInTheCoinsRowWithWhatToDoUnderIt()
        {
            var go = VillageHudBuild.Make();
            try
            {
                var v = go.GetComponent<VillageHudView>();
                v.ShowHunt("★ 별 찾기 0/5   1:47", "공원에 숨은 반짝 별 5개를 시간 안에 모두 찾기");
                Assert.IsTrue(v.HuntShown); Assert.IsTrue(v.HuntInfoShown); Assert.AreEqual("★ 별 찾기 0/5   1:47", v.HuntText);
                // its middle level with the coin pill's (y 104); the line under it
                Assert.AreEqual(-104f, v.hunt.anchoredPosition.y, 0.01f);
                var coins = v.coins; float coinMid = -coins.anchoredPosition.y + coins.sizeDelta.y / 2f;
                Assert.AreEqual(104f, coinMid, 1.5f, "the coins' row");
                Assert.Greater(-v.huntInfo.anchoredPosition.y, 104f + 97f / 2f, "what to do sits under the hunt pill");
                Assert.AreEqual(Rect.zero, v.FreeTopBand(), "notices keep out of the hunt's row");
                // a hint in the same place goes first, the hunt comes back after it
                v.Hint("새 가구! 끌어서 자리를 고르고 '여기에 두기'를 눌러요"); Assert.IsFalse(v.HuntShown);
                v.Hint(null); Assert.IsTrue(v.HuntShown);
                v.HideHunt(); Assert.IsFalse(v.HuntShown); Assert.IsFalse(v.HuntInfoShown);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void AWindowTakesTheWholeHudAway()
        {
            var go = VillageHudBuild.Make();
            try
            {
                var v = go.GetComponent<VillageHudView>();
                v.SetWindowOpen(true);
                var g = go.GetComponent<CanvasGroup>();
                Assert.IsTrue(v.WindowHidden); Assert.AreEqual(0f, g.alpha); Assert.IsFalse(g.blocksRaycasts);
                v.SetWindowOpen(false);
                Assert.IsFalse(v.WindowHidden); Assert.AreEqual(1f, g.alpha); Assert.IsTrue(g.blocksRaycasts);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

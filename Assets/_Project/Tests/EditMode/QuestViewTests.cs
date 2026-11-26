using System.Linq;
using Mongle.EditorTools;
using Mongle.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>The 11-26 퀘스트 window (Figma 88:2130): the three tabs, the rows in a scrolling list, the buttons that take you there.</summary>
    public class QuestViewTests
    {
        static Ask Row(string id, AskState st, string action = "", System.Action press = null) =>
            new Ask { id = id, title = "열매 모으기", giver = "토순", line = "사탕나무 열매 3개 모아 줘", reward = 20, state = st, action = action, onPress = press };

        [Test]
        public void ThePrefabIsWiredAndListed()
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(QuestBuild.PrefabPath);
            Assert.IsNotNull(p, QuestBuild.PrefabPath);
            var v = p.GetComponent<QuestView>();
            Assert.AreEqual(3, v.tabs.Length); Assert.IsNotNull(v.tabOn); Assert.IsNotNull(v.tabOff); Assert.IsNotNull(v.closeButton);
            Assert.IsNotNull(v.scroll); Assert.IsNotNull(v.content); Assert.IsNotNull(v.rowTemplate); Assert.IsNotNull(v.emptyText); Assert.IsNotNull(v.heart);
            var r = AskRow.Find(v.rowTemplate);
            Assert.IsNotNull(r.plate); Assert.IsNotNull(r.icon); Assert.IsNotNull(r.picture); Assert.IsNotNull(r.initial); Assert.IsNotNull(r.button); Assert.IsNotNull(r.rewardPill); Assert.IsNotNull(r.done);
            Assert.AreEqual(AskRowBuild.W, ((RectTransform)v.rowTemplate.transform).sizeDelta.x, 0.01f, "full-size rows (Figma 1356.6 wide)");
            Assert.AreEqual("Btn_나가기", v.closeButton.name);
            Assert.AreEqual(p, ViewIndex.Prefab("Quest"));
        }

        [Test]
        public void RowsTabsAndButtons()
        {
            var go = QuestBuild.Make();
            try
            {
                var v = go.GetComponent<QuestView>(); v.Show();
                bool went = false; int tabAsked = -1; v.OnTab = t => tabAsked = t;
                var list = new[] { Row("F1", AskState.Doing, "나무 보기", () => went = true), Row("F2", AskState.Doing), Row("S1", AskState.Ready, "가게 가기"), Row("F3", AskState.Doing), Row("F4", AskState.Doing) };
                v.Render(0, list, "", new[] { "진행중 5", "완료 1", "내 이야기" });
                Assert.AreEqual("진행중 5", v.tabs[0].label.text); Assert.AreEqual(v.tabOn, v.tabs[0].plate.sprite); Assert.AreEqual(v.tabOff, v.tabs[1].plate.sprite);
                var shown = v.content.Cast<Transform>().Where(t => t.gameObject.activeSelf).ToList();
                Assert.AreEqual(5, shown.Count);
                Assert.AreEqual(-v.step, ((RectTransform)shown[1]).anchoredPosition.y - ((RectTransform)shown[0]).anchoredPosition.y, 0.01f, "200.5 apart");
                Assert.IsTrue(v.scroll.verticalScrollbar.gameObject.activeSelf, "five rows scroll (four fit)");
                Assert.IsTrue(v.QaPress("F1")); Assert.IsTrue(went);
                Assert.IsFalse(v.QaPress("F2"), "nothing to do from here: no button");
                Assert.AreEqual("Quest_Claim_S1", v.RowOf("S1").button.name); Assert.AreEqual(v.style.openButton, ((UnityEngine.UI.Image)v.RowOf("S1").button.targetGraphic).sprite);
                Assert.AreEqual("가게 가기", v.RowOf("S1").buttonText.text);
                Assert.AreEqual(v.style.goButton, ((UnityEngine.UI.Image)v.RowOf("F1").button.targetGraphic).sprite);
                Assert.AreEqual("토순 : 사탕나무 열매 3개 모아 줘", v.RowOf("F1").line.text);
                v.tabs[1].button.onClick.Invoke(); Assert.AreEqual(1, tabAsked);
                v.Render(1, new[] { Row("D1", AskState.Done) }, "", null);
                var d = v.RowOf("D1");
                Assert.IsTrue(d.done.activeSelf); Assert.IsFalse(d.button.gameObject.activeSelf); Assert.IsFalse(d.rewardPill.activeSelf);
                Assert.AreEqual(v.style.rowDoneColor, d.plate.color);
                Assert.IsFalse(v.scroll.verticalScrollbar.gameObject.activeSelf);
                v.Render(0, new Ask[0], "진행 중인 퀘스트가 없어요.", null);
                Assert.IsTrue(v.emptyText.gameObject.activeSelf); Assert.AreEqual("진행 중인 퀘스트가 없어요.", v.emptyText.text);
                v.Render(2, new[] { new Ask { id = "birthday", title = "생일", progress = "3월 4일", line = "친구들이 기억하고 있어요", state = AskState.Note, picture = v.heart, initial = "?" } }, "", null);
                var n = v.RowOf("birthday");
                Assert.IsFalse(n.button.gameObject.activeSelf); Assert.IsFalse(n.rewardPill.activeSelf); Assert.IsFalse(n.done.activeSelf);
                Assert.IsTrue(n.picture.enabled); StringAssert.Contains("3월 4일", n.title.text);
                bool closed = false; v.OnClose = () => closed = true; v.closeButton.onClick.Invoke(); Assert.IsTrue(closed);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

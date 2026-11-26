using Mongle.EditorTools;
using Mongle.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>The 11-26 friend talk window (Figma 88:231 / 88:367): a request's two pills, a question's drop-down, the birthday's 월 / 일.</summary>
    public class FriendTalkViewTests
    {
        [Test]
        public void ThePrefabIsWiredAndListed()
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(FriendTalkBuild.PrefabPath);
            Assert.IsNotNull(p, FriendTalkBuild.PrefabPath);
            var v = p.GetComponent<FriendTalkView>();
            Assert.IsNotNull(v.bubble); Assert.IsNotNull(v.nameTag); Assert.IsNotNull(v.lineText); Assert.IsNotNull(v.subText);
            Assert.IsNotNull(v.yesButton); Assert.IsNotNull(v.laterButton); Assert.IsNotNull(v.answer); Assert.IsNotNull(v.month); Assert.IsNotNull(v.day);
            foreach (var d in new[] { v.answer, v.month, v.day }) { Assert.IsNotNull(d.template); Assert.IsNotNull(d.itemText); Assert.IsNotNull(d.placeholder); }
            Assert.AreEqual(p, ViewIndex.Prefab("FriendTalk"));
        }

        [Test]
        public void TheAnswerColumnLinesUp()
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(FriendTalkBuild.PrefabPath);
            var v = p.GetComponent<FriendTalkView>();
            var later = (RectTransform)v.laterButton.transform;
            foreach (var t in new[] { (RectTransform)v.yesButton.transform, (RectTransform)v.answer.transform, (RectTransform)v.datePanel.transform })
            {
                Assert.AreEqual(later.anchoredPosition.x, t.anchoredPosition.x, 0.01f, t.name + ": same column as 다음에 할게");
                Assert.AreEqual(later.sizeDelta, t.sizeDelta, t.name + ": same size as 다음에 할게");
                Assert.AreEqual(later.anchoredPosition.y + 111.5f, t.anchoredPosition.y, 0.01f, t.name + ": right above it");
            }
        }

        [Test]
        public void ARequestHasItsTwoPills()
        {
            var go = FriendTalkBuild.Make();
            try
            {
                var v = go.GetComponent<FriendTalkView>();
                bool yes = false, later = false;
                v.Ask("몽이", Color.white, "시소 혼자 타기는 좀 무서워 ...\n같이 타줄래?", "· 같이 타기      · 보상 : 몽글 코인 +20", "좋아!", () => yes = true, "다음에 할게", () => later = true);
                Assert.IsTrue(v.Visible); Assert.AreEqual("몽이", v.NameShown);
                Assert.IsTrue(v.yesButton.gameObject.activeSelf); Assert.IsTrue(v.laterButton.gameObject.activeSelf);
                Assert.IsFalse(v.answer.gameObject.activeSelf); Assert.IsFalse(v.datePanel.activeSelf);
                Assert.AreEqual("좋아!", v.yesLabel.text);
                Assert.IsTrue(v.QaYes()); Assert.IsTrue(yes);
                Assert.IsTrue(v.QaLater()); Assert.IsTrue(later);
                float w = ((RectTransform)v.nameTag.transform).sizeDelta.x;
                v.Ask("아주아주 긴 이름의 친구", Color.white, "안녕", "", "좋아!", null, "다음에 할게", null);
                Assert.Greater(((RectTransform)v.nameTag.transform).sizeDelta.x, w, "the tag grows with a long name");
                v.Hide(); Assert.IsFalse(v.Visible);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void AQuestionIsAnsweredFromItsDropDown()
        {
            var go = FriendTalkBuild.Make();
            try
            {
                var v = go.GetComponent<FriendTalkView>();
                string got = null;
                v.Question("몽이", Color.white, "저기... 너는 어떤 동물을 제일 좋아해?", "· 좋아하는 동물", new[] { "토끼", "고양이", "곰" }, a => got = a, "다음에 할게", null);
                Assert.IsTrue(v.answer.gameObject.activeSelf); Assert.IsFalse(v.yesButton.gameObject.activeSelf);
                Assert.AreEqual(-1, v.answer.value, "nothing chosen yet: the placeholder shows");
                Assert.AreEqual(3, v.answer.options.Count);
                Assert.IsTrue(v.QaAnswer("곰")); Assert.AreEqual("곰", got);
                Assert.IsFalse(v.QaAnswer("기린"));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheBirthdayDaysFollowTheMonth()
        {
            var go = FriendTalkBuild.Make();
            try
            {
                var v = go.GetComponent<FriendTalkView>();
                int gm = 0, gd = 0;
                v.Birthday("몽이", Color.white, "너 생일이 언제야?", "· 생일", m => m == 2 ? 29 : m == 4 ? 30 : 31, (m, d) => { gm = m; gd = d; }, "다음에 할게", null);
                Assert.IsTrue(v.datePanel.activeSelf); Assert.AreEqual(12, v.month.options.Count);
                Assert.AreEqual(-1, v.month.value); Assert.AreEqual(-1, v.day.value);
                Assert.IsFalse(v.QaDate(4, 31), "April has no 31st");
                Assert.AreEqual(30, v.day.options.Count);
                Assert.IsTrue(v.QaDate(2, 29)); Assert.AreEqual(2, gm); Assert.AreEqual(29, gd);
                Assert.AreEqual(29, v.day.options.Count);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

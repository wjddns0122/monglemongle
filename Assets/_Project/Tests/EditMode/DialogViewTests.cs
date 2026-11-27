using Mongle.EditorTools;
using Mongle.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>The 11-26 message box: wired, listed for the game, and it grows with its words.</summary>
    public class DialogViewTests
    {
        [Test]
        public void ThePrefabIsWiredAndListed()
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(DialogBuild.PrefabPath);
            Assert.IsNotNull(p, DialogBuild.PrefabPath);
            var v = p.GetComponent<DialogView>();
            Assert.IsNotNull(v); Assert.IsNotNull(v.panel); Assert.IsNotNull(v.title); Assert.IsNotNull(v.body); Assert.IsNotNull(v.okButton); Assert.IsNotNull(v.secondButton);
            Assert.AreEqual(p, ViewIndex.Prefab("Dialog"));
        }

        [Test]
        public void OneOrTwoButtonsAndLongWordsGrowTheSheet()
        {
            var go = DialogBuild.Make();
            try
            {
                var v = go.GetComponent<DialogView>();
                v.Show("몽이가 왔어요", "짧은 글", "살펴볼게요", null);
                Assert.IsFalse(v.secondButton.gameObject.activeSelf);
                var small = v.panel.sizeDelta;
                v.Show("몽이가 왔어요", "한 줄\n두 줄\n세 줄\n네 줄", "살펴볼게요", null, "죄송합니다", null);
                Assert.IsTrue(v.secondButton.gameObject.activeSelf);
                Assert.Greater(v.panel.sizeDelta.y, small.y);
                Assert.GreaterOrEqual(v.panel.sizeDelta.x, v.minWidth);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheBandWearsTheFigmaStripesInsideItsRoundTop()
        {
            var go = DialogBuild.Make();
            try
            {
                var v = go.GetComponent<DialogView>();
                var band = v.panel.Find("Band");
                Assert.IsNotNull(band.GetComponent<UnityEngine.UI.Mask>(), "the stripes are cut to the band's round top");
                var stripes = band.Find("Stripes").GetComponent<UnityEngine.UI.Image>();
                Assert.IsNotNull(stripes.sprite, "Art/UI_1126/Common/dialog_stripes.png");
                Assert.GreaterOrEqual(((RectTransform)stripes.transform).sizeDelta.x, v.maxWidth - 3f, "wide enough for the widest sheet");
                Assert.Less(stripes.transform.GetSiblingIndex(), band.Find("Title").GetSiblingIndex(), "the title is drawn over the stripes");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TwoLinesAndTwoButtonsMakeTheFigmaSheet()
        {
            var go = DialogBuild.Make();
            try
            {
                var v = go.GetComponent<DialogView>();
                v.Show("몽이의 새집", "몽이는 오랫동안 사랑받아 조금 지친 곰인형이에요.\n다시 포근해질 수 있도록 도와주세요.", "마을로 초대하기", null, "나중에", null);
                Assert.AreEqual(445.12f, v.panel.sizeDelta.y, 4f, "Figma 115:1255: the sheet is 445 tall");
                Assert.AreEqual(-(113.47f + 70.36f), v.body.rectTransform.anchoredPosition.y, 0.01f, "the words start 70.36 under the band");
                Assert.AreEqual(39.76f, ((RectTransform)v.okButton.transform.parent).anchoredPosition.y, 0.01f, "the buttons sit 39.76 over the foot");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

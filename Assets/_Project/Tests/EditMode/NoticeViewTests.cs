using Mongle.EditorTools;
using Mongle.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>The 11-26 notice pill (Figma 88:1848–88:1850): it hugs its words, a coin notice gets the coin in front.</summary>
    public class NoticeViewTests
    {
        [Test]
        public void ThePrefabIsWiredAndListed()
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(NoticeBuild.PrefabPath);
            Assert.IsNotNull(p, NoticeBuild.PrefabPath);
            var v = p.GetComponent<NoticeView>();
            Assert.IsNotNull(v.pill); Assert.IsNotNull(v.text); Assert.IsNotNull(v.coin);
            Assert.IsNotNull(v.coin.GetComponent<UnityEngine.UI.Image>().sprite);
            Assert.AreEqual(p, ViewIndex.Prefab("Notice"));
        }

        [Test]
        public void CoinNoticesAndLongLines()
        {
            var go = NoticeBuild.Make();
            try
            {
                var v = go.GetComponent<NoticeView>();
                Assert.IsFalse(v.Shown);
                v.Show("몽글 코인 + 50");
                Assert.IsTrue(v.Shown); Assert.IsTrue(v.CoinShown); Assert.AreEqual("몽글 코인 + 50", v.TextShown);
                float coinW = v.pill.sizeDelta.x;
                Assert.AreEqual(v.pad + v.coinSize + v.coinGap, v.text.rectTransform.offsetMin.x, 0.01f, "the words start after the coin");
                v.Show("보관함에 넣었어요");
                Assert.IsFalse(v.CoinShown); Assert.AreEqual(v.pad, v.text.rectTransform.offsetMin.x, 0.01f);
                v.Show("포근 의자를 보관함에 넣었어요! 오른쪽 위 '보관함'에서도 놓을 수 있어요");
                Assert.Greater(v.pill.sizeDelta.x, coinW, "a long line makes a long pill");
                Assert.LessOrEqual(v.pill.sizeDelta.x, v.maxWidth);
                v.Hide(); Assert.IsFalse(v.Shown); Assert.AreEqual("", v.TextShown);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

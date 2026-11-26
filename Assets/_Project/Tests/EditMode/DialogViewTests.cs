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
    }
}

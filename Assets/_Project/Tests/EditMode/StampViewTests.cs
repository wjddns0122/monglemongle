using Mongle.EditorTools;
using Mongle.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>The 11-26 stamp card: wired, listed, and the stamps already in the book show.</summary>
    public class StampViewTests
    {
        [Test]
        public void ThePrefabIsWiredAndListed()
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(StampBuild.PrefabPath);
            Assert.IsNotNull(p, StampBuild.PrefabPath);
            var v = p.GetComponent<StampView>();
            Assert.IsNotNull(v.card); Assert.IsNotNull(v.stampButton); Assert.AreEqual(8, v.marks.Length);
            Assert.AreEqual(p, ViewIndex.Prefab("Stamp"));
        }

        [Test]
        public void FilledSlotsShowTheirStamps()
        {
            var go = StampBuild.Make();
            try
            {
                var v = go.GetComponent<StampView>();
                v.Show(3, null);
                for (int i = 0; i < v.marks.Length; i++) Assert.AreEqual(i < 3, v.marks[i].activeSelf, "slot " + i);
                Assert.IsTrue(v.stampButton.interactable);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

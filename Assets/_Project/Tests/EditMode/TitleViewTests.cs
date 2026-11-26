using Mongle.EditorTools;
using Mongle.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>The 11-26 start screen: the game only finds it and listens to its buttons, so the parts must be wired.</summary>
    public class TitleViewTests
    {
        static void CheckParts(TitleView t)
        {
            Assert.IsNotNull(t, "TitleView");
            Assert.IsNotNull(t.openButton); Assert.IsNotNull(t.newButton); Assert.IsNotNull(t.settingsButton); Assert.IsNotNull(t.quitButton);
            Assert.IsNotNull(t.gearButton); Assert.IsNotNull(t.menu); Assert.IsNotNull(t.highlight); Assert.IsNotNull(t.cursor);
            Assert.IsTrue(t.openButton.transform.IsChildOf(t.menu.transform) && t.quitButton.transform.IsChildOf(t.menu.transform));
        }

        [Test]
        public void TheBuiltScreenHasEveryPart()
        {
            var go = TitleBuild.Make();
            try
            {
                var t = go.GetComponent<TitleView>();
                CheckParts(t);
                t.ShowMenu(false); Assert.IsFalse(t.menu.activeSelf);
                t.ShowMenu(true); Assert.IsTrue(t.menu.activeSelf);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ThePrefabInTheProjectIsWired()
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(TitleBuild.PrefabPath);
            Assert.IsNotNull(p, TitleBuild.PrefabPath);
            CheckParts(p.GetComponent<TitleView>());
        }
    }
}

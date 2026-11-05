using Mongle.EditorTools;
using Mongle.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.Tests
{
    /// <summary>The start screen as scene objects (D-030): the game only finds it and wires its buttons, so the parts must be there.</summary>
    public class TitleScreenTests
    {
        // only the wiring: words, sprites and places are the designer's to change in the prefab
        static void CheckParts(TitleScreen t)
        {
            Assert.IsNotNull(t.Canvas);
            Assert.IsNotNull(t.openButton); Assert.IsNotNull(t.againButton); Assert.IsNotNull(t.settingsButton); Assert.IsNotNull(t.menu);
        }

        [Test]
        public void TheCodeBuiltScreenHasEveryPart()
        {
            var t = TitleScreen.Build(null);
            try
            {
                CheckParts(t);
                Assert.IsTrue(t.openButton.transform.IsChildOf(t.menu.transform) && t.againButton.transform.IsChildOf(t.menu.transform));
                Assert.AreEqual("가게 문 열기", t.openButton.GetComponentInChildren<Text>(true).text);
                Assert.AreEqual("처음부터", t.againButton.GetComponentInChildren<Text>(true).text);
                t.ShowMenu(false); Assert.IsFalse(t.menu.activeSelf);
                t.ShowMenu(true); Assert.IsTrue(t.menu.activeSelf);
            }
            finally { Object.DestroyImmediate(t.gameObject); }
        }

        [Test]
        public void ThePrefabInTheProjectIsWired()
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(TitleScreenBaker.PrefabPath);
            Assert.IsNotNull(p, "Prefabs/UI/TitleScreen.prefab");
            CheckParts(p.GetComponent<TitleScreen>());
        }
    }
}

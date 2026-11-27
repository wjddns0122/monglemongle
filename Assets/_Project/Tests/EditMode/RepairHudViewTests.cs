using System.Linq;
using Mongle.Data;
using Mongle.EditorTools;
using Mongle.Toy;
using Mongle.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>The 11-26 repair screen: wired, one notebook row per damage, four tools a page.</summary>
    public class RepairHudViewTests
    {
        [Test]
        public void ThePrefabIsWiredAndListed()
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(RepairHudBuild.PrefabPath);
            Assert.IsNotNull(p, RepairHudBuild.PrefabPath);
            var v = p.GetComponent<RepairHudView>();
            Assert.IsNotNull(v.backButton); Assert.IsNotNull(v.gearButton); Assert.IsNotNull(v.banner); Assert.IsNotNull(v.notebook); Assert.IsNotNull(v.rowTemplate);
            Assert.IsNotNull(v.zoomIn); Assert.IsNotNull(v.zoomOut); Assert.IsNotNull(v.toolBar); Assert.AreEqual(4, v.slots.Length); Assert.IsNotNull(v.tip);
            Assert.AreEqual(p, ViewIndex.Prefab("RepairHud"));
        }

        [Test]
        public void RowsFollowTheDamagesAndToolsArePaged()
        {
            var go = RepairHudBuild.Make();
            try
            {
                var v = go.GetComponent<RepairHudView>();
                var catalog = ContentCatalog.Load(); var def = catalog.ToyDef("TeddyBear_001") ?? catalog.toy;
                var ids = def.damageSequence.Where(d => d != null).Select(d => d.id).ToList();
                var toy = new ToyRuntime(def.kindId, ids);
                v.RefreshNotebook(toy, catalog);
                int rows = 0; foreach (Transform c in v.notebook) if (c.name.StartsWith("Row") && c != v.rowTemplate && c.gameObject.activeSelf) rows++;
                Assert.AreEqual(ids.Count, rows);
                var tools = catalog.tools.Where(t => t != null).ToArray();
                v.BuildToolShelf(tools, _ => { });
                Assert.AreEqual(tools.Length > 4, v.nextPage.gameObject.activeSelf);
                v.Banner("해냈어요!"); Assert.IsTrue(v.BannerShown); Assert.AreEqual("해냈어요!", v.BannerText);
                v.Banner(null); Assert.IsFalse(v.BannerShown);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheNotebookStandsInTheMiddleOfTheLeftColumnUnderBack()
        {
            var go = RepairHudBuild.Make();
            try
            {
                var v = go.GetComponent<RepairHudView>();
                var nb = (RectTransform)v.notebook;
                // user 2026-11-27: lower, in the middle of the left column (pinned to the left middle, growing both ways)
                Assert.AreEqual(new Vector2(0f, 0.5f), nb.anchorMin); Assert.AreEqual(new Vector2(0f, 0.5f), nb.pivot);
                Assert.AreEqual(0f, nb.anchoredPosition.y, 0.01f);
                // its pin (49.6 over its top) stays clear of ◀ (bottom at 154.5) even with five rows
                float tallest = 91f + 5f * 109.1f + 30f;
                Assert.Greater(540f - tallest / 2f - 49.61f, 154.5f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheBannerIsTheFigmaPillInTheMiddle()
        {
            var go = RepairHudBuild.Make();
            try
            {
                var v = go.GetComponent<RepairHudView>();
                // Figma 115:1573 (user 2026-11-27): 97 tall, middle at y 97.75, 38 px words 62 in from its ends
                Assert.AreEqual(0f, v.banner.anchoredPosition.x, 0.01f, "in the middle of the screen");
                Assert.AreEqual(-97.75f, v.banner.anchoredPosition.y, 0.01f);
                Assert.AreEqual(97f, v.banner.sizeDelta.y, 0.01f);
                Assert.AreEqual(38f, v.bannerText.fontSize, 0.01f);
                Assert.AreEqual(62f, v.bannerPad, 0.01f);
                var t = v.bannerText.rectTransform;
                Assert.AreEqual(0f, t.offsetMin.x, 0.01f); Assert.AreEqual(0f, t.offsetMax.x, 0.01f);
                v.Banner("마음에 드는 것을 골라 꾸며줘요");
                Assert.AreEqual(639f, v.banner.sizeDelta.x, 1.5f, "the frame's pill for its words");
                const string line = "몸이 축 처져 있어요. 솜이 모자란가 봐요.";
                v.Banner(line);
                Assert.AreEqual(Mathf.Max(v.bannerMinWidth, v.bannerText.GetPreferredValues(line, 99999f, 0f).x + 124f), v.banner.sizeDelta.x, 0.5f);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

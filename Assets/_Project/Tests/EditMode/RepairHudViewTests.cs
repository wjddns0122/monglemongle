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
    }
}

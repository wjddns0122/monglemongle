using System.Linq;
using Mongle.EditorTools;
using Mongle.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>The 11-26 보관함 (Figma 88:825 / 88:650): cards four to a row, the tabs, the search and the order, the chosen
    /// thing on the right page and 사용하기.</summary>
    public class StorageViewTests
    {
        static StorageView.Item It(string id, string name, int count, StorageView.Kind kind) =>
            new StorageView.Item { id = id, name = name, description = name + " 설명", count = count, kind = kind };

        static StorageView.Item[] Box() => new[]
        {
            It("Fur_Armchair", "포근 의자", 1, StorageView.Kind.Furniture), It("Fur_ToyBox", "장난감 상자", 2, StorageView.Kind.Furniture),
            It("Plant_Tulip", "튤립 화분", 3, StorageView.Kind.Furniture), It("Acc_RedRibbon", "빨간 리본", 1, StorageView.Kind.Accessory),
            It("Acc_Beret", "베레모", 2, StorageView.Kind.Accessory),
        };

        [Test]
        public void ThePrefabIsWiredAndListed()
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(StorageBuild.PrefabPath);
            Assert.IsNotNull(p, StorageBuild.PrefabPath);
            var v = p.GetComponent<StorageView>();
            Assert.AreEqual(3, v.tabs.Length); foreach (var t in v.tabs) { Assert.IsNotNull(t.button); Assert.IsNotNull(t.plate); }
            Assert.IsNotNull(v.tabOn); Assert.IsNotNull(v.tabOff); Assert.IsNotNull(v.search); Assert.IsNotNull(v.sort); Assert.IsNotNull(v.closeButton);
            Assert.IsNotNull(v.scroll); Assert.IsNotNull(v.content); Assert.IsNotNull(v.cardTemplate); Assert.IsNotNull(v.emptyText);
            foreach (var part in new[] { "Plate", "Chosen", "Icon", "Badge/Count" }) Assert.IsNotNull(v.cardTemplate.transform.Find(part), part);
            Assert.IsNotNull(v.detail); Assert.IsNotNull(v.picture); Assert.IsNotNull(v.nameText); Assert.IsNotNull(v.descriptionText); Assert.IsNotNull(v.useButton);
            Assert.AreEqual("Btn_나가기", v.closeButton.name, "QA closes it as 나가기");
            Assert.AreEqual(p, ViewIndex.Prefab("Storage"));
        }

        [Test]
        public void CardsFourToARowAndTheChosenOneOnTheRight()
        {
            var go = StorageBuild.Make();
            try
            {
                var v = go.GetComponent<StorageView>();
                v.Show(Box());
                CollectionAssert.AreEqual(new[] { "Fur_Armchair", "Fur_ToyBox", "Plant_Tulip", "Acc_RedRibbon", "Acc_Beret" }, v.ShownIds.ToArray());
                var cards = v.content.Cast<Transform>().Where(t => t.gameObject.activeSelf).ToList();
                Assert.AreEqual(5, cards.Count);
                var a = (RectTransform)cards[0]; var e = (RectTransform)cards[4];
                Assert.AreEqual(a.anchoredPosition.x, e.anchoredPosition.x, 0.01f, "the fifth card starts the second row");
                Assert.AreEqual(-v.step.y, e.anchoredPosition.y - a.anchoredPosition.y, 0.01f);
                Assert.AreEqual("x 2", cards[1].Find("Badge/Count").GetComponent<TMPro.TMP_Text>().text);
                Assert.AreEqual("Fur_Armchair", v.Chosen, "the first thing is chosen at once");
                Assert.IsTrue(cards[0].Find("Chosen").gameObject.activeSelf); Assert.IsFalse(cards[1].Find("Chosen").gameObject.activeSelf);
                Assert.IsTrue(v.detail.activeSelf); Assert.AreEqual("포근 의자", v.nameText.text); Assert.AreEqual("포근 의자 설명", v.descriptionText.text);
                cards[2].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.AreEqual("Plant_Tulip", v.Chosen); Assert.AreEqual("튤립 화분", v.nameText.text);
                Assert.IsTrue(cards[2].Find("Chosen").gameObject.activeSelf); Assert.IsFalse(cards[0].Find("Chosen").gameObject.activeSelf);
                string used = null; v.OnUse = id => used = id;
                v.useButton.onClick.Invoke(); Assert.AreEqual("Plant_Tulip", used);
                Assert.IsFalse(v.emptyText.gameObject.activeSelf);
                bool closed = false; v.OnClose = () => closed = true; v.closeButton.onClick.Invoke(); Assert.IsTrue(closed);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TabsSearchAndOrder()
        {
            var go = StorageBuild.Make();
            try
            {
                var v = go.GetComponent<StorageView>();
                v.Show(Box());
                v.tabs[2].button.onClick.Invoke();
                CollectionAssert.AreEqual(new[] { "Acc_RedRibbon", "Acc_Beret" }, v.ShownIds.ToArray(), "악세서리");
                Assert.AreEqual(v.tabOn, v.tabs[2].plate.sprite); Assert.AreEqual(v.tabOff, v.tabs[0].plate.sprite);
                Assert.AreEqual("Acc_RedRibbon", v.Chosen, "the choice moves to what is shown");
                v.SetTab(1);
                CollectionAssert.AreEqual(new[] { "Fur_Armchair", "Fur_ToyBox", "Plant_Tulip" }, v.ShownIds.ToArray(), "가구 (plants too)");
                v.SetTab(0); v.SetSearch("상자");
                CollectionAssert.AreEqual(new[] { "Fur_ToyBox" }, v.ShownIds.ToArray());
                v.SetSearch("없는 물건");
                Assert.AreEqual(0, v.ShownIds.Count()); Assert.IsTrue(v.emptyText.gameObject.activeSelf);
                Assert.AreEqual("찾는 물건이 없어요.", v.emptyText.text); Assert.IsFalse(v.detail.activeSelf, "nothing chosen: the right page is bare");
                v.SetSearch("");
                v.SetOrder(2);
                Assert.AreEqual("Plant_Tulip", v.ShownIds.First(), "많은 순");
                v.SetOrder(1);
                Assert.AreEqual("베레모", Box().First(i => i.id == v.ShownIds.First()).name, "이름 순 (가나다)");
                Assert.AreEqual(3, v.sort.options.Count);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void AnEmptyBoxSaysSo()
        {
            var go = StorageBuild.Make();
            try
            {
                var v = go.GetComponent<StorageView>();
                v.Show(new StorageView.Item[0]);
                Assert.IsTrue(v.emptyText.gameObject.activeSelf);
                StringAssert.StartsWith("보관함이 비었어요.", v.emptyText.text);
                Assert.IsFalse(v.detail.activeSelf); Assert.IsNull(v.Chosen);
                Assert.IsFalse(v.QaUse("Fur_Armchair"));
                v.SetItems(Box());
                Assert.IsTrue(v.QaUse("Acc_Beret") && v.Chosen == "Acc_Beret");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

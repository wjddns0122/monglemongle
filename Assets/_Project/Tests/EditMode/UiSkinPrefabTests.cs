using System.Collections.Generic;
using Mongle.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mongle.Tests
{
    /// <summary>D-035: the real screen prefabs (Resources/UI/Skins) over the screens the code builds.</summary>
    public class UiSkinPrefabTests
    {
        GameObject host, skin;
        EventSystem[] before;

        [SetUp] public void Before() { before = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include); }

        [TearDown]
        public void After()
        {
            if (host != null) Object.DestroyImmediate(host);
            if (skin != null) Object.DestroyImmediate(skin);
            foreach (var es in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include))
                if (System.Array.IndexOf(before, es) < 0) Object.DestroyImmediate(es.gameObject);
        }

        ShopUI Repair()
        {
            host = new GameObject("ShopUI-test"); var ui = host.AddComponent<ShopUI>(); ui.Build();
            return ui;
        }

        static Dictionary<string, string> State(Transform root)
        {
            var d = new Dictionary<string, string>();
            foreach (var t in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (t == root) continue;
                var k = UiSkin.PathOf(t, root);
                var s = t.anchoredPosition + " " + t.sizeDelta + " " + t.localScale;
                if (t.TryGetComponent<Text>(out var tx)) s += " '" + tx.text + "' " + tx.fontSize + " " + tx.color;
                if (t.TryGetComponent<Image>(out var im)) s += " " + (im.sprite != null ? im.sprite.name : "-") + " " + im.color;
                d[k] = s;
            }
            return d;
        }

        [Test]
        public void TheUntouchedPrefabChangesNothing()
        {
            Assume.That(UiSkin.Find("RepairScreen"), Is.Not.Null, "the repair screen prefab exists");
            var ui = Repair();
            var was = State(ui.Canvas.transform);
            Assert.IsTrue(UiSkin.Apply(ui.Canvas.transform, "RepairScreen"));
            var now = State(ui.Canvas.transform);
            foreach (var kv in was) Assert.AreEqual(kv.Value, now[kv.Key], kv.Key);
        }

        [Test]
        public void AnEditInThePrefabReachesTheScreen()
        {
            var prefab = UiSkin.Find("RepairScreen");
            Assume.That(prefab, Is.Not.Null, "the repair screen prefab exists");
            skin = Object.Instantiate(prefab);   // a copy: the asset itself is never touched
            var banner = (RectTransform)UiSkin.At(skin.transform, "Banner");
            banner.anchoredPosition += new Vector2(0f, -40f);
            var title = UiSkin.At(skin.transform, "Notebook").GetComponentInChildren<Text>(true);
            title.color = Color.magenta;
            var sticker = new GameObject("Sticker", typeof(RectTransform), typeof(Image)); sticker.transform.SetParent(skin.transform, false);

            var ui = Repair();
            var zoom = State(ui.Canvas.transform);
            var codeBanner = ((RectTransform)UiSkin.At(ui.Canvas.transform, "Banner")).anchoredPosition;
            UiSkin.Apply(ui.Canvas.transform, skin.transform);

            Assert.AreEqual(codeBanner + new Vector2(0f, -40f), ((RectTransform)UiSkin.At(ui.Canvas.transform, "Banner")).anchoredPosition);
            Assert.AreEqual(Color.magenta, UiSkin.At(ui.Canvas.transform, "Notebook").GetComponentInChildren<Text>(true).color);
            Assert.IsNotNull(UiSkin.At(ui.Canvas.transform, "Sticker"));
            var now = State(ui.Canvas.transform);
            foreach (var kv in zoom)
            {
                if (kv.Key == "Banner" || kv.Key.StartsWith("Notebook")) continue;
                Assert.AreEqual(kv.Value, now[kv.Key], kv.Key + " was not edited, so it stays as the code made it");
            }
        }
    }
}

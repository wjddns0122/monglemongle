using Mongle.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.Tests
{
    /// <summary>D-035: an edited screen prefab laid over the screen the code built.</summary>
    public class UiSkinTests
    {
        GameObject built, skin;
        Sprite red, blue;

        static Sprite Pic(Color c)
        {
            var t = new Texture2D(4, 4); var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = c; t.SetPixels(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
        }

        static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform;
        }

        /// <summary>What the code builds: a panel with a label, a list with two cards, and a button.</summary>
        GameObject Screen(string name)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var panel = Node(root.transform, "Panel"); panel.sizeDelta = new Vector2(100, 50);
            var img = panel.gameObject.AddComponent<Image>(); img.sprite = red; img.color = Color.white;
            var label = Node(panel, "Label").gameObject.AddComponent<Text>(); label.text = "hi"; label.fontSize = 20; label.alignment = TextAnchor.UpperLeft;
            var list = Node(root.transform, "List");
            for (int i = 0; i < 2; i++)
            {
                var card = Node(list, "Card_" + i); card.anchoredPosition = new Vector2(0, -60 * i);
                card.gameObject.AddComponent<Image>().color = Color.white;
                var t = Node(card, "Title").gameObject.AddComponent<Text>(); t.text = "card " + i; t.fontSize = 24;
            }
            return root;
        }

        /// <summary>The prefab as the baker makes it: the same screen, every object a part that remembers how it was.</summary>
        static void Bake(Transform t, string path, Transform top)
        {
            if (t != top)
            {
                var p = t.gameObject.AddComponent<UiSkinPart>(); p.bakedPath = path; p.baked = UiSkin.Snapshot(t);
                if (t.name == "List") p.listOf = "Card_";
            }
            foreach (Transform c in t) Bake(c, t == top ? UiSkin.KeyOf(c) : path + "/" + UiSkin.KeyOf(c), top);
        }

        [SetUp]
        public void Make()
        {
            red = Pic(Color.red); blue = Pic(Color.blue);
            built = Screen("Built");
            skin = Screen("Skin"); Bake(skin.transform, "", skin.transform);
            // the list in the prefab keeps one sample card (the model for all)
            Object.DestroyImmediate(skin.transform.Find("List/Card_1").gameObject);
        }

        [TearDown]
        public void Clean() { Object.DestroyImmediate(built); Object.DestroyImmediate(skin); }

        Transform S(string path) => skin.transform.Find(path);
        Transform B(string path) => built.transform.Find(path);

        [Test]
        public void TheDesignersChangesReachTheBuiltScreen()
        {
            var sp = (RectTransform)S("Panel"); sp.anchoredPosition = new Vector2(10, 20); sp.sizeDelta = new Vector2(300, 80);
            var si = sp.GetComponent<Image>(); si.sprite = blue; si.color = Color.yellow;
            var st = S("Panel/Label").GetComponent<Text>(); st.text = "안녕"; st.fontSize = 44;
            S("Panel/Label").GetComponent<UiSkinPart>().hideInGame = true;
            UiSkin.Apply(built.transform, skin.transform);
            var panel = (RectTransform)B("Panel");
            Assert.AreEqual(new Vector2(10, 20), panel.anchoredPosition);
            Assert.AreEqual(new Vector2(300, 80), panel.sizeDelta);
            var img = panel.GetComponent<Image>();
            Assert.AreEqual(Color.yellow, img.color);
            Assert.AreEqual(blue, img.sprite);
            var label = B("Panel/Label").GetComponent<Text>();
            Assert.AreEqual("안녕", label.text); Assert.AreEqual(44, label.fontSize);
            Assert.AreEqual(TextAnchor.UpperLeft, label.alignment, "settings left alone stay the game's");
            Assert.AreEqual(0f, label.GetComponent<CanvasGroup>().alpha, "hideInGame keeps it invisible");
        }

        [Test]
        public void WhatTheDesignerLeftAloneStaysTheCodes()
        {
            // the game has moved on since it built the screen: a new text, another picture, the panel elsewhere
            var panel = (RectTransform)B("Panel"); panel.anchoredPosition = new Vector2(400, 0);
            panel.GetComponent<Image>().sprite = blue; panel.GetComponent<Image>().color = Color.green;
            B("Panel/Label").GetComponent<Text>().text = "몽이";
            S("Panel/Label").GetComponent<Text>().fontSize = 30;   // the one thing the designer changed
            UiSkin.Apply(built.transform, skin.transform);
            Assert.AreEqual(new Vector2(400, 0), panel.anchoredPosition);
            Assert.AreEqual(blue, panel.GetComponent<Image>().sprite);
            Assert.AreEqual(Color.green, panel.GetComponent<Image>().color);
            Assert.AreEqual("몽이", B("Panel/Label").GetComponent<Text>().text);
            Assert.AreEqual(30, B("Panel/Label").GetComponent<Text>().fontSize);
        }

        [Test]
        public void NewWordsReplaceOnlyTheWordsTheScreenWasMadeWith()
        {
            // D-039: the sample card's words changed: every card keeps its own name (the code wrote them), the style still spreads
            var sample = S("List/Card_0/Title").GetComponent<Text>(); sample.text = "새 이름"; sample.fontSize = 33;
            // a label the game has since rewritten keeps the game's words; the panel label the game left alone takes the new ones
            S("Panel/Label").GetComponent<Text>().text = "안녕";
            UiSkin.Apply(built.transform, skin.transform);
            Assert.AreEqual("새 이름", B("List/Card_0/Title").GetComponent<Text>().text, "the sample card itself had the made-with words");
            Assert.AreEqual("card 1", B("List/Card_1/Title").GetComponent<Text>().text, "another card keeps its own name");
            Assert.AreEqual(33, B("List/Card_1/Title").GetComponent<Text>().fontSize);
            Assert.AreEqual("안녕", B("Panel/Label").GetComponent<Text>().text);

            B("Panel/Label").GetComponent<Text>().text = "몽이";   // live words from the game
            UiSkin.Apply(built.transform, skin.transform);
            Assert.AreEqual("몽이", B("Panel/Label").GetComponent<Text>().text);
        }

        [Test]
        public void TheSampleCardIsTheModelForEveryCardButTheCodePlacesThem()
        {
            var sc = (RectTransform)S("List/Card_0"); sc.anchoredPosition = new Vector2(999, 999);
            sc.GetComponent<Image>().color = Color.cyan;
            S("List/Card_0/Title").GetComponent<Text>().fontSize = 40;
            UiSkin.Apply(built.transform, skin.transform);
            for (int i = 0; i < 2; i++)
            {
                var card = (RectTransform)B("List/Card_" + i);
                Assert.AreEqual(new Vector2(0, -60 * i), card.anchoredPosition, "the code lays the cards out");
                Assert.AreEqual(Color.cyan, card.GetComponent<Image>().color);
                var t = card.Find("Title").GetComponent<Text>();
                Assert.AreEqual(40, t.fontSize); Assert.AreEqual("card " + i, t.text);
            }
        }

        [Test]
        public void TheDesignersOwnObjectsAppearOnceAndMissingPartsAreNotMade()
        {
            Node(skin.transform, "Sticker").gameObject.AddComponent<Image>().color = Color.green;
            Node(S("List/Card_0"), "Star").gameObject.AddComponent<Image>();
            Object.DestroyImmediate(B("Panel").gameObject);   // a part the code has not made (yet)
            UiSkin.Apply(built.transform, skin.transform);
            UiSkin.Apply(built.transform, skin.transform);   // laid again (e.g. after a list is filled again)
            int stickers = 0; foreach (Transform c in built.transform) if (c.name == "Sticker") stickers++;
            Assert.AreEqual(1, stickers);
            Assert.IsNotNull(B("Sticker").GetComponent<UiSkinAdded>());
            Assert.AreEqual(Color.green, B("Sticker").GetComponent<Image>().color);
            Assert.IsNotNull(B("List/Card_1/Star"), "a decoration on the sample card is on every card");
            Assert.IsNull(B("Panel"), "the code decides when its parts exist");
        }

        [Test]
        public void APartLaidOverLaterUsesTheSkinsObjectAtTheSamePlace()
        {
            S("List/Card_0/Title").GetComponent<Text>().fontSize = 50;
            var path = UiSkin.PathOf(B("List"), built.transform);
            Assert.AreEqual("List", path);
            // the skin is not in Resources here, so lay the skin's own list over the built one
            UiSkin.Apply(B("List"), S("List"));
            Assert.AreEqual(50, B("List/Card_1/Title").GetComponent<Text>().fontSize);
        }

        [Test]
        public void LinksAreLeftOutOfTheComparedSettings()
        {
            var json = JsonUtility.ToJson(B("Panel").GetComponent<Image>());
            var bare = UiSkin.Strip(json);
            StringAssert.DoesNotContain("instanceID", bare);
            Assert.IsNull(UiSkin.Partial(bare, bare));
            var p = UiSkin.Partial("{\"a\":1,\"f\":{\"size\":20,\"align\":3},\"s\":\"x,\\\"y\"}", "{\"a\":1,\"f\":{\"size\":10,\"align\":3},\"s\":\"x\"}");
            Assert.AreEqual("{\"f\":{\"size\":20},\"s\":\"x,\\\"y\"}", p);
        }

        [Test]
        public void SameNamedSiblingsMatchByOrder()
        {
            var a = Node(built.transform, "Label"); var b = Node(built.transform, "Label");
            Assert.AreEqual("Label#0", UiSkin.KeyOf(a)); Assert.AreEqual("Label#1", UiSkin.KeyOf(b));
            Assert.AreEqual("Panel", UiSkin.KeyOf(B("Panel")));
        }
    }
}

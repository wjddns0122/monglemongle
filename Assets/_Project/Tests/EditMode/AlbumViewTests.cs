using System.Collections.Generic;
using Mongle.EditorTools;
using Mongle.Toy;
using Mongle.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>The 11-26 album (Figma 88:1393): nine cards a page, the chosen photo big on the left page, empty and missing photos.</summary>
    public class AlbumViewTests
    {
        static List<AlbumView.Entry> Photos(int n, List<Texture2D> made, int[] loads = null, int missing = -1)
        {
            var list = new List<AlbumView.Entry>();
            for (int i = 0; i < n; i++)
            {
                int k = i;
                list.Add(new AlbumView.Entry("2026.11." + (10 + k) + " · 친구" + k, () =>
                {
                    if (loads != null) loads[k]++;
                    if (k == missing) return null;
                    var t = new Texture2D(4, 4); made.Add(t); return t;
                }));
            }
            return list;
        }

        [Test]
        public void ThePrefabIsWiredAndListed()
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(AlbumBuild.PrefabPath);
            Assert.IsNotNull(p, AlbumBuild.PrefabPath);
            var v = p.GetComponent<AlbumView>();
            Assert.AreEqual(AlbumView.PerPage, v.cards.Length);
            foreach (var c in v.cards) { Assert.IsNotNull(c.button); Assert.IsNotNull(c.mark); Assert.IsNotNull(c.photo); }
            Assert.IsNotNull(v.bigPhoto); Assert.IsNotNull(v.slot); Assert.IsNotNull(v.caption); Assert.IsNotNull(v.emptyText);
            Assert.IsNotNull(v.prevButton); Assert.IsNotNull(v.nextButton); Assert.IsNotNull(v.pageLabel); Assert.IsNotNull(v.closeButton);
            Assert.AreEqual(p, ViewIndex.Prefab("Album"));
        }

        [Test]
        public void PhotosGoNineToAPage()
        {
            var go = AlbumBuild.Make(); var made = new List<Texture2D>();
            try
            {
                var v = go.GetComponent<AlbumView>();
                v.Show(Photos(11, made));
                Assert.AreEqual(2, v.PageCount); Assert.AreEqual("1/2", v.pageLabel.text);
                Assert.IsFalse(v.prevButton.interactable); Assert.IsTrue(v.nextButton.interactable);
                for (int i = 0; i < 9; i++) Assert.IsTrue(v.cards[i].photo.enabled, "card " + i);
                v.ShowPage(1);
                Assert.AreEqual("2/2", v.pageLabel.text);
                Assert.IsTrue(v.prevButton.interactable); Assert.IsFalse(v.nextButton.interactable);
                for (int i = 0; i < 9; i++)
                {
                    Assert.AreEqual(i < 2, v.cards[i].photo.enabled, "card " + i);
                    Assert.AreEqual(i < 2, v.cards[i].button.interactable, "card " + i + " can be chosen");
                }
                v.ShowPage(2); v.ShowPage(-1);
                Assert.AreEqual(1, v.Page, "no page past the ends");
            }
            finally { Object.DestroyImmediate(go); foreach (var t in made) if (t != null) Object.DestroyImmediate(t); }
        }

        [Test]
        public void TheChosenPhotoShowsBigWithItsLine()
        {
            var go = AlbumBuild.Make(); var made = new List<Texture2D>();
            try
            {
                var v = go.GetComponent<AlbumView>();
                v.Show(Photos(11, made));
                Assert.AreEqual(0, v.Chosen); Assert.IsTrue(v.cards[0].mark.activeSelf, "the first photo is chosen on opening");
                v.Choose(3);
                Assert.AreEqual(v.cards[3].photo.texture, v.bigPhoto.texture, "the same picture, big");
                Assert.IsTrue(v.bigPhoto.enabled); Assert.IsFalse(v.slot.activeSelf); Assert.IsFalse(v.emptyText.gameObject.activeSelf);
                Assert.AreEqual("2026.11.13 · 친구3", v.caption.text);
                for (int i = 0; i < 9; i++) Assert.AreEqual(i == 3, v.cards[i].mark.activeSelf, "blue edge on " + i);
                // the photo as it was taken, its own frame and all, on a white polaroid (user 2026-11-27); no gingham frame any more
                Assert.AreEqual(0f, v.bigPhoto.uvRect.xMin, 1e-5f); Assert.AreEqual(1f, v.bigPhoto.uvRect.yMax, 1e-5f);
                Assert.IsTrue(v.polaroid.activeSelf); Assert.IsNull(go.transform.Find("Book/Frame"));
                Assert.IsNotNull(v.polaroidPattern.texture, "the whole polaroid in a frame pattern (user 2026-11-27)");
                v.ShowPage(1);
                foreach (var c in v.cards) Assert.IsFalse(c.mark.activeSelf, "the chosen card is on the other page");
                v.Choose(10);
                Assert.IsTrue(v.cards[1].mark.activeSelf);
            }
            finally { Object.DestroyImmediate(go); foreach (var t in made) if (t != null) Object.DestroyImmediate(t); }
        }

        [Test]
        public void AnEmptyAlbumSaysSo()
        {
            var go = AlbumBuild.Make();
            try
            {
                var v = go.GetComponent<AlbumView>();
                v.Show(new List<AlbumView.Entry>());
                Assert.AreEqual(1, v.PageCount); Assert.AreEqual("1/1", v.pageLabel.text);
                Assert.IsTrue(v.slot.activeSelf); Assert.IsTrue(v.emptyText.gameObject.activeSelf); Assert.IsFalse(v.bigPhoto.enabled);
                Assert.AreEqual("", v.caption.text);
                Assert.IsFalse(v.prevButton.interactable); Assert.IsFalse(v.nextButton.interactable);
                foreach (var c in v.cards) { Assert.IsFalse(c.photo.enabled); Assert.IsFalse(c.button.interactable); Assert.IsFalse(c.mark.activeSelf); }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void PhotosLoadOnceAndAreLetGoOnClosing()
        {
            var go = AlbumBuild.Make(); var made = new List<Texture2D>(); var loads = new int[3];
            try
            {
                var v = go.GetComponent<AlbumView>();
                bool closed = false; v.OnClosed = () => closed = true;
                v.Show(Photos(3, made, loads, missing: 1));
                v.ShowPage(0); v.Choose(2); v.Choose(1);
                CollectionAssert.AreEqual(new[] { 1, 1, 1 }, loads, "each photo file is read once");
                Assert.IsFalse(v.cards[1].photo.enabled, "a missing file leaves its card white");
                Assert.IsTrue(v.slot.activeSelf, "and the slot shows instead of a big photo");
                Assert.AreEqual("2026.11.11 · 친구1", v.caption.text);
                v.Hide();
                Assert.IsTrue(closed); Assert.IsFalse(go.activeSelf);
                foreach (var t in made) Assert.IsTrue(t == null, "the album's copies are freed");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheLineUnderAPhotoIsItsDayAndName()
        {
            Assert.AreEqual("2026.11.26 · 몽이", Mongle.Village.VillageDirector.AlbumLine(new PhotoRecord { takenAt = "2026-11-26 14:03" }, "몽이"));
            Assert.AreEqual("토끼", Mongle.Village.VillageDirector.AlbumLine(new PhotoRecord(), "토끼"));
        }
    }
}

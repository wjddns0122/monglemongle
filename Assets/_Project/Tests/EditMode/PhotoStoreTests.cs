using System.IO;
using Mongle.Core;
using Mongle.Photo;
using Mongle.Toy;
using NUnit.Framework;
using UnityEngine;

namespace Mongle.Tests
{
    public class PhotoStoreTests
    {
        string dir; SaveService svc;
        [SetUp] public void SetUp() { dir = Path.Combine(Path.GetTempPath(), "mongle_photo_" + System.Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); svc = new SaveService(Path.Combine(dir, "save.json")); }
        [TearDown] public void TearDown() { if (Directory.Exists(dir)) Directory.Delete(dir, true); }

        static byte[] Png(int size, Color c)
        {
            var t = new Texture2D(size, size, TextureFormat.RGB24, false);
            var px = new Color[size * size]; for (int i = 0; i < px.Length; i++) px[i] = c; t.SetPixels(px); t.Apply();
            var b = t.EncodeToPNG(); Object.DestroyImmediate(t); return b;
        }

        [Test]
        public void WriteThenLoadRoundTrips()
        {
            PhotoStore.Write(svc, PhotoStore.FirstPhotoFile, Png(64, Color.red));
            var tex = PhotoStore.Load(svc, new PhotoRecord { imageFile = PhotoStore.FirstPhotoFile });
            Assert.NotNull(tex); Assert.AreEqual(64, tex.width);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void MissingFileGivesNullNotException()
        {
            Assert.IsNull(PhotoStore.Load(svc, new PhotoRecord { imageFile = "nope.png" }));
            Assert.IsNull(PhotoStore.Load(svc, null));
            Assert.IsNull(PhotoStore.Load(svc, new PhotoRecord { imageFile = "" }));
        }

        [Test]
        public void CorruptImageGivesNullNotException()
        {
            Directory.CreateDirectory(svc.PhotoFolder);
            File.WriteAllText(PhotoStore.PathFor(svc, "bad.png"), "not an image");
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            Assert.IsNull(PhotoStore.Load(svc, new PhotoRecord { imageFile = "bad.png" }));
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void SecondWriteReplacesFileAtomically()
        {
            PhotoStore.Write(svc, "a.png", Png(32, Color.red));
            PhotoStore.Write(svc, "a.png", Png(48, Color.blue));
            var tex = PhotoStore.Load(svc, new PhotoRecord { imageFile = "a.png" });
            Assert.AreEqual(48, tex.width);
            Assert.IsFalse(File.Exists(PhotoStore.PathFor(svc, "a.png") + ".tmp"));
            Object.DestroyImmediate(tex);
        }
    }
}

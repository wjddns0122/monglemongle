using Mongle.Photo;
using NUnit.Framework;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>Photo frames (D-025): four tiling patterns; the chosen one is painted only into the photo's border.</summary>
    public class PhotoFrameTests
    {
        [Test]
        public void EveryFrameHasARepeatingTile()
        {
            Assert.AreEqual(PhotoFrames.Ids.Length, PhotoFrames.Names.Length);
            for (int i = 0; i < PhotoFrames.Ids.Length; i++)
            {
                var t = PhotoFrames.Pattern(i);
                Assert.AreEqual(PhotoFrames.Tile, t.width); Assert.AreEqual(TextureWrapMode.Repeat, t.wrapMode);
            }
            // the patterned frames really have two colours in them (dots/checks/stars), the plain one does not
            Assert.AreNotEqual(PhotoFrames.Pattern(1).GetPixel(24, 24), PhotoFrames.Pattern(1).GetPixel(48, 0));
            Assert.AreNotEqual(PhotoFrames.Pattern(2).GetPixel(5, 5), PhotoFrames.Pattern(2).GetPixel(30, 5));
            Assert.AreNotEqual(PhotoFrames.Pattern(3).GetPixel(26, 26), PhotoFrames.Pattern(3).GetPixel(50, 50));
        }

        [Test]
        public void BakingPaintsOnlyTheBorder()
        {
            var photo = new Texture2D(128, 128, TextureFormat.RGBAFloat, false);   // exact colours for the comparisons
            var blue = new Color(0.2f, 0.4f, 0.9f, 1f);
            var px = new Color[128 * 128]; for (int i = 0; i < px.Length; i++) px[i] = blue; photo.SetPixels(px); photo.Apply();
            PhotoFrames.Bake(photo, 0, 8);
            Assert.AreEqual(blue, photo.GetPixel(2, 2), "the plain frame leaves the photo as it is");
            PhotoFrames.Bake(photo, 2, 8);
            Assert.AreNotEqual(blue, photo.GetPixel(2, 2), "border painted");
            Assert.AreEqual(blue, photo.GetPixel(64, 64), "inside untouched");
            Assert.AreEqual(blue, photo.GetPixel(8, 8), "the first pixel inside the border stays");
            Object.DestroyImmediate(photo);
        }
    }
}

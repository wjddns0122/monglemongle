using Mongle.EditorTools;
using Mongle.Photo;
using Mongle.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>The 11-26 photo studio (Figma 88:1081): wired and listed, the frame pattern runs on across the polaroid's
    /// four border pieces, and a taken photo swaps the controls for the keep / again pills.</summary>
    public class PhotoViewTests
    {
        [Test]
        public void ThePrefabIsWiredAndListed()
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(PhotoBuild.PrefabPath);
            Assert.IsNotNull(p, PhotoBuild.PrefabPath);
            var v = p.GetComponent<PhotoView>();
            Assert.IsNotNull(v.frameButton); Assert.IsNotNull(v.poseButton); Assert.IsNotNull(v.shutter); Assert.IsNotNull(v.zoom);
            Assert.IsNotNull(v.keepButton); Assert.IsNotNull(v.retakeButton); Assert.IsNotNull(v.gearButton);
            Assert.IsNotNull(v.photo); Assert.IsNotNull(v.caption); Assert.IsNotNull(v.captionBox); Assert.IsNotNull(v.flash);
            Assert.AreEqual(4, v.borders.Length);
            foreach (var b in v.borders) Assert.IsNotNull(b);
            Assert.AreEqual(p, ViewIndex.Prefab("Photo"));
        }

        [Test]
        public void TheFramePatternRunsOnAcrossTheBorderPieces()
        {
            var go = PhotoBuild.Make();
            try
            {
                var v = go.GetComponent<PhotoView>();
                var pattern = PhotoFrames.Pattern(3);
                v.SetFrame(pattern, "별");
                Assert.AreEqual("별", v.frameLabel.text);
                var tile = new Vector2(pattern.width, pattern.height) * v.texelSize;
                foreach (var b in v.borders)
                {
                    Assert.AreEqual(pattern, b.texture);
                    // each piece starts the pattern where it sits on the polaroid, so neighbours line up
                    var r = b.rectTransform.rect; var pos = (Vector2)b.rectTransform.localPosition;
                    Assert.AreEqual((pos.x + r.xMin) / tile.x, b.uvRect.x, 1e-4f, b.name);
                    Assert.AreEqual((pos.y + r.yMin) / tile.y, b.uvRect.y, 1e-4f, b.name);
                    Assert.AreEqual(r.width / tile.x, b.uvRect.width, 1e-4f, b.name);
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ATakenPhotoSwapsTheControlsForThePills()
        {
            var go = PhotoBuild.Make();
            var shot = new Texture2D(8, 8);
            try
            {
                var v = go.GetComponent<PhotoView>();
                Assert.IsFalse(v.Reviewing);
                v.ShowReview(shot, "2026-11-26 · 몽이");
                Assert.IsTrue(v.Reviewing); Assert.IsFalse(v.controls.activeSelf); Assert.IsFalse(v.marks.activeSelf);
                Assert.IsTrue(v.photo.enabled); Assert.AreEqual(shot, v.photo.texture);
                Assert.IsTrue(v.captionBox.activeSelf); Assert.AreEqual("2026-11-26 · 몽이", v.caption.text);
                v.HideReview(); v.ShowControls(true);
                Assert.IsFalse(v.Reviewing); Assert.IsTrue(v.controls.activeSelf); Assert.IsTrue(v.marks.activeSelf);
                Assert.IsFalse(v.photo.enabled); Assert.IsFalse(v.captionBox.activeSelf);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(shot); }
        }

        [Test]
        public void TheReviewShowsThePhotosInsideCutToTheWindow()
        {
            var go = PhotoBuild.Make();
            var shot = new Texture2D(16, 16);
            try
            {
                var v = go.GetComponent<PhotoView>();
                Assert.AreEqual(731.44f / 631.59f, v.WindowAspect, 1e-3f);
                // the baked frame (1/16 of the photo on each side) stays out: the polaroid already draws it around the window
                v.ShowReview(shot, "", PhotoAim.Border);
                var uv = v.photo.uvRect;
                Assert.AreEqual(PhotoAim.Border, uv.xMin, 1e-4f); Assert.AreEqual(1f - PhotoAim.Border, uv.xMax, 1e-4f);
                Assert.AreEqual(uv.width / v.WindowAspect, uv.height, 1e-4f, "cut to the window's shape, not squeezed");
                Assert.AreEqual(0.5f, uv.center.y, 1e-4f);
                Assert.IsFalse(v.captionBox.activeSelf, "no plate without a line");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(shot); }
        }

        [Test]
        public void ThePillsAfterTheShotStayClearOfThePolaroid()
        {
            // a 16:9 and a 4:3 screen (the canvas is 1920 wide on both)
            foreach (float height in new[] { 1080f, 1440f })
            {
                // a world canvas of that size stands in for the screen (it is not resized by the editor's Game view)
                var screen = new GameObject("screen", typeof(RectTransform), typeof(Canvas));
                var go = PhotoBuild.Make();
                try
                {
                    screen.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                    ((RectTransform)screen.transform).sizeDelta = new Vector2(1920f, height);
                    var rt = (RectTransform)go.transform; rt.SetParent(screen.transform, false);
                    rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; rt.localScale = Vector3.one;
                    var v = go.GetComponent<PhotoView>();
                    var c = new Vector3[4];
                    v.viewfinder.GetWorldCorners(c); float polaroidRight = c[2].x;
                    ((RectTransform)v.review.transform).GetWorldCorners(c); float pillsLeft = c[0].x, pillsRight = c[2].x;
                    Assert.Greater(pillsLeft, polaroidRight + 20f, "height " + height);
                    rt.GetWorldCorners(c);
                    Assert.Less(pillsRight, c[2].x - 20f, "inside the screen, height " + height);
                    ((RectTransform)v.shutter.transform).GetWorldCorners(c);
                    Assert.Greater(c[0].x, polaroidRight, "the camera button too");
                }
                finally { Object.DestroyImmediate(go); Object.DestroyImmediate(screen); }
            }
        }

        [Test]
        public void TheBannerGrowsWithItsWordsAndHidesWhenEmpty()
        {
            var go = PhotoBuild.Make();
            try
            {
                var v = go.GetComponent<PhotoView>();
                Assert.AreEqual(0f, v.banner.anchoredPosition.x, 0.01f, "in the middle of the screen");
                v.Banner("찰칵");
                Assert.AreEqual(v.bannerMinWidth, v.banner.sizeDelta.x, 0.01f);
                v.Banner("프레임과 포즈를 고르고 사진을 찍어요, 마음에 들면 남겨요");
                Assert.Greater(v.banner.sizeDelta.x, v.bannerMinWidth);
                Assert.IsTrue(v.banner.gameObject.activeSelf);
                v.Banner("");
                Assert.IsFalse(v.banner.gameObject.activeSelf);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

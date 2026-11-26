using Mongle.Photo;
using NUnit.Framework;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>The studio's framing (PhotoAim): the toy sits in the middle of the polaroid's window whatever its size,
    /// and the photo keeps exactly what the window shows.</summary>
    public class PhotoAimTests
    {
        // the polaroid's window on a 1920 x 1080 screen (Figma 88:1081): x 665.96..1397.40, y 246.71..878.30 from the top
        static readonly Rect Window = Rect.MinMaxRect(665.96f / 1920f, 1f - 878.30f / 1080f, 1397.40f / 1920f, 1f - 246.71f / 1080f);
        const float Aspect = 16f / 9f, WindowAspect = 731.44f / 631.59f;
        static readonly Vector3 Focus = new Vector3(0f, 0.24f, 0f);

        static Camera Studio(float windowWidth, float zoom)
        {
            var cam = new GameObject("cam").AddComponent<Camera>();
            cam.aspect = Aspect; cam.transform.position = Focus + Vector3.forward * PhotoAim.Distance; cam.transform.LookAt(Focus);
            cam.fieldOfView = PhotoAim.LiveFov(windowWidth, zoom, Window.width * Aspect);
            cam.ResetProjectionMatrix();
            cam.projectionMatrix = PhotoAim.Shift(cam.projectionMatrix, Window.center);
            return cam;
        }

        static Camera Capture(float windowWidth, float zoom)
        {
            var cam = new GameObject("capture").AddComponent<Camera>();
            cam.aspect = 1f; cam.transform.position = Focus + Vector3.forward * PhotoAim.Distance; cam.transform.LookAt(Focus);
            cam.fieldOfView = PhotoAim.CaptureFov(windowWidth, zoom);
            cam.ResetProjectionMatrix();
            return cam;
        }

        /// <summary>Viewport point (0..1) of a world point, straight from the camera's matrices.</summary>
        static Vector2 Project(Camera cam, Vector3 world)
        {
            var c = cam.projectionMatrix * cam.worldToCameraMatrix * new Vector4(world.x, world.y, world.z, 1f);
            return new Vector2(c.x / c.w * 0.5f + 0.5f, c.y / c.w * 0.5f + 0.5f);
        }

        [Test]
        public void TheToyLandsInTheMiddleOfTheWindow()
        {
            foreach (float zoom in new[] { PhotoAim.ZoomNear, 1f, PhotoAim.ZoomFar })
            {
                var cam = Studio(PhotoAim.WindowWidth(new Vector3(0.39f, 0.47f, 0.35f), WindowAspect), zoom);
                try
                {
                    var p = Project(cam, Focus);
                    Assert.AreEqual(Window.center.x, p.x, 1e-4f, "x at zoom " + zoom);
                    Assert.AreEqual(Window.center.y, p.y, 1e-4f, "y at zoom " + zoom);
                    Assert.Greater(Window.center.x, 0.5f, "the window is right of the screen's middle, the toy follows it");
                }
                finally { Object.DestroyImmediate(cam.gameObject); }
            }
        }

        [Test]
        public void EveryToyFillsTheSameShareOfTheWindow()
        {
            // a bear, a tall bunny, a wide plane (their measured boxes)
            foreach (var size in new[] { new Vector3(0.39f, 0.47f, 0.35f), new Vector3(0.38f, 0.64f, 0.33f), new Vector3(0.57f, 0.44f, 0.55f) })
            {
                var cam = Studio(PhotoAim.WindowWidth(size, WindowAspect), 1f);
                try
                {
                    float tall = (Project(cam, Focus + Vector3.up * size.y * 0.5f).y - Project(cam, Focus - Vector3.up * size.y * 0.5f).y) / Window.height;
                    float wide = Mathf.Abs(Project(cam, Focus + Vector3.right * size.x * 0.5f).x - Project(cam, Focus - Vector3.right * size.x * 0.5f).x) / Window.width;
                    Assert.LessOrEqual(tall, PhotoAim.FillHeight + 1e-3f, size + " height");
                    Assert.LessOrEqual(wide, PhotoAim.FillWidth + 1e-3f, size + " width");
                    Assert.IsTrue(Mathf.Abs(tall - PhotoAim.FillHeight) < 1e-3f || Mathf.Abs(wide - PhotoAim.FillWidth) < 1e-3f, size + " fills the window as far as it may");
                }
                finally { Object.DestroyImmediate(cam.gameObject); }
            }
        }

        [Test]
        public void ThePhotoKeepsWhatTheWindowShows()
        {
            float w = PhotoAim.WindowWidth(new Vector3(0.39f, 0.47f, 0.35f), WindowAspect);
            foreach (float zoom in new[] { PhotoAim.ZoomNear, 1f, PhotoAim.ZoomFar })
            {
                Camera live = Studio(w, zoom), shot = Capture(w, zoom);
                try
                {
                    // a point on the edge of the window's view, beside the toy
                    var edge = Focus + Vector3.right * w * zoom * 0.5f;
                    Assert.AreEqual(Window.width * 0.5f, Mathf.Abs(Project(live, edge).x - Window.center.x), 1e-4f, "on the window's edge at zoom " + zoom);
                    Assert.AreEqual(0.5f - PhotoAim.Border, Mathf.Abs(Project(shot, edge).x - 0.5f), 1e-4f, "on the edge of the photo's inside at zoom " + zoom);
                    var middle = Project(shot, Focus);
                    Assert.AreEqual(0.5f, middle.x, 1e-4f); Assert.AreEqual(0.5f, middle.y, 1e-4f);
                }
                finally { Object.DestroyImmediate(live.gameObject); Object.DestroyImmediate(shot.gameObject); }
            }
        }

        [Test]
        public void MeasuringTakesWhatIsDrawn()
        {
            var toy = new GameObject("toy");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube); body.transform.SetParent(toy.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.3f, 0f); body.transform.localScale = new Vector3(0.4f, 0.6f, 0.3f);
            var hidden = GameObject.CreatePrimitive(PrimitiveType.Cube); hidden.transform.SetParent(toy.transform, false);
            hidden.transform.localScale = Vector3.one * 5f; hidden.GetComponent<Renderer>().enabled = false;
            try
            {
                var b = PhotoAim.Measure(toy);
                Assert.AreEqual(0.3f, b.center.y, 1e-4f); Assert.AreEqual(0.6f, b.size.y, 1e-4f); Assert.AreEqual(0.4f, b.size.x, 1e-4f);
            }
            finally { Object.DestroyImmediate(toy); }
        }
    }
}

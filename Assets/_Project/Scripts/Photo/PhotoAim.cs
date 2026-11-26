using UnityEngine;

namespace Mongle.Photo
{
    /// <summary>
    /// Framing of the photo studio (11-26 design). The viewfinder is a window in the polaroid, a little right of the screen's
    /// middle, so the studio camera is shifted to put the toy in the middle of that window, and sized from the toy itself so
    /// every friend — a short bear or a tall bunny — fills the same share of it. The photo is taken with the same framing:
    /// its inside (without the frame's border) is exactly as wide as the window, so what the window shows is what is kept.
    /// </summary>
    public static class PhotoAim
    {
        /// <summary>The frame's share of the square photo on each side (PhotoFrames.Bake paints it).</summary>
        public const float Border = 1f / 16f;
        /// <summary>Camera to toy, metres (the studio's look: close enough for a soft, round toy).</summary>
        public const float Distance = 1.35f;
        /// <summary>The toy's share of the window at the middle zoom: its height, and at most this much of the width.</summary>
        public const float FillHeight = 0.72f, FillWidth = 0.78f;
        /// <summary>The zoom slider: ＋ (closest) … － (farthest), as a scale of the framed width.</summary>
        public const float ZoomNear = 0.6f, ZoomFar = 1.4f;

        /// <summary>The toy's box from what is drawn of it (particles left out).</summary>
        public static Bounds Measure(GameObject toy)
        {
            var b = new Bounds(toy.transform.position + Vector3.up * 0.235f, new Vector3(0.39f, 0.47f, 0.35f));
            bool any = false;
            foreach (var r in toy.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (any) b.Encapsulate(r.bounds); else { b = r.bounds; any = true; }
            }
            return b;
        }

        /// <summary>How wide (metres, at the toy) the window's view is at the middle zoom, for a toy of this size.</summary>
        public static float WindowWidth(Vector3 toySize, float windowAspect)
            => Mathf.Max(toySize.y / FillHeight * Mathf.Max(0.1f, windowAspect), toySize.x / FillWidth);

        /// <summary>The square photo's field of view: its inside is as wide as the window's view.</summary>
        public static float CaptureFov(float windowWidth, float zoom)
            => 2f * Mathf.Atan(windowWidth * zoom / (1f - 2f * Border) * 0.5f / Distance) * Mathf.Rad2Deg;

        /// <summary>The studio camera's (vertical) field of view that makes the window show exactly that width;
        /// <paramref name="windowOverScreenHeight"/> is the window's width on the screen divided by the screen's height.</summary>
        public static float LiveFov(float windowWidth, float zoom, float windowOverScreenHeight)
            => 2f * Mathf.Atan(windowWidth * zoom * 0.5f / Distance / Mathf.Max(0.01f, windowOverScreenHeight)) * Mathf.Rad2Deg;

        /// <summary>The projection moved sideways so that what the camera looks straight at lands on this point of the
        /// screen (viewport 0..1) instead of the middle — a lens shift, the toy keeps its shape.</summary>
        public static Matrix4x4 Shift(Matrix4x4 projection, Vector2 viewportPoint)
        {
            projection.m02 = -(viewportPoint.x * 2f - 1f);
            projection.m12 = -(viewportPoint.y * 2f - 1f);
            return projection;
        }
    }
}

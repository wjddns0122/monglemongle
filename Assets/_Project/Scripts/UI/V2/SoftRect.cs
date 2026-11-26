using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.UI2
{
    /// <summary>
    /// Resolution-independent UI shape for the v2 UI (D-038): a rounded rectangle with its own fill (optionally a vertical
    /// gradient), border, dashed "stitch" line and soft drop shadow, drawn as a mesh — crisp at any screen size, so panels,
    /// pills, tabs and knobs can stretch with the layout instead of being fixed-size sprites.
    /// Radii are per corner (top-left, top-right, bottom-right, bottom-left) in reference pixels; 999 = fully round.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class SoftRect : MaskableGraphic
    {
        [Header("Shape")]
        public Vector4 radius = new Vector4(20, 20, 20, 20);
        [Header("Fill (Graphic.color is the top colour)")]
        public bool gradient;
        public Color bottomColor = Color.white;
        [Header("Border (inside the rect)")]
        public float borderWidth;
        public Color borderColor = new Color(0.77f, 0.87f, 0.91f, 1f);
        [Header("Stitch: a dashed line inside the edge")]
        public bool stitch;
        public float stitchInset = 8f, stitchWidth = 2f, dash = 8f, gap = 6f;
        public Color stitchColor = new Color(0.85f, 0.78f, 0.7f, 1f);
        /// <summary>
        /// Figma draws the stitch as its own rounded rect: distance from each edge (left, top, right, bottom) to the dash
        /// centre line. x &lt; 0 = the old uniform <see cref="stitchInset"/>.
        /// </summary>
        public Vector4 stitchPad = new Vector4(-1, 0, 0, 0);
        /// <summary>Corner radius of the stitch's own rect (&lt; 0: the shape's radius minus the inset).</summary>
        public float stitchRadius = -1f;
        /// <summary>Exact number of dashes round the shape (0 = as many as dash + gap allow).</summary>
        public int stitchCount;
        /// <summary>
        /// Distance from one dash to the next along the stitch at the design size (Figma's spacing). When set, a shape that grows
        /// or shrinks (a banner fitting its words, a folding panel) keeps this spacing and gets more or fewer dashes, instead of
        /// stretching <see cref="stitchCount"/> dashes round it. 0 = the count stays fixed.
        /// </summary>
        [Tooltip("점선 사이 간격 (크기가 바뀌어도 이 간격을 지켜요, 0이면 점선 개수 고정)")] public float stitchPeriod;
        /// <summary>Round line caps (Figma): each dash reaches half a line width past its ends.</summary>
        public bool roundCaps;
        /// <summary>The dashes' own drop shadow (Figma puts the shadow effect on the stitch group too).</summary>
        public Color stitchShadow = new Color(0, 0, 0, 0);
        [Header("Drop shadow (Figma: offset, gaussian blur sigma)")]
        public Color shadowColor = new Color(0, 0, 0, 0);
        public Vector2 shadowOffset = new Vector2(1, -1);
        /// <summary>Gaussian blur standard deviation in reference px (Figma "blur" = 2 sigma).</summary>
        public float shadowBlur = 1f;

        static readonly List<Vector2> ringA = new List<Vector2>(), ringB = new List<Vector2>(), ringC = new List<Vector2>(), path = new List<Vector2>();

        public void SetAll() { SetVerticesDirty(); }

        float Px()
        {   // one screen pixel in local units, for the anti-aliased edge
            var c = canvas; float s = c != null ? c.scaleFactor : 1f;
            return 1f / Mathf.Max(0.01f, s);
        }

        Vector4 Radii(Rect r)
        {
            float m = Mathf.Min(r.width, r.height) * 0.5f;
            return new Vector4(Mathf.Clamp(radius.x, 0, m), Mathf.Clamp(radius.y, 0, m), Mathf.Clamp(radius.z, 0, m), Mathf.Clamp(radius.w, 0, m));
        }

        static int Segs(Vector4 rad) { float m = Mathf.Max(Mathf.Max(rad.x, rad.y), Mathf.Max(rad.z, rad.w)); return Mathf.Clamp(Mathf.CeilToInt(4 + m / 3f), 4, 40); }

        /// <summary>Outline of the rounded rect grown by <paramref name="off"/> (negative = shrunk), clockwise from the top-left corner.</summary>
        static void Outline(List<Vector2> o, Rect r, Vector4 rad, float off, int seg)
        {
            o.Clear();
            float x0 = r.xMin - off, x1 = r.xMax + off, y0 = r.yMin - off, y1 = r.yMax + off;
            // corners: tl (x0,y1), tr (x1,y1), br (x1,y0), bl (x0,y0)
            Corner(o, new Vector2(r.xMin + rad.x, r.yMax - rad.x), Mathf.Max(0, rad.x + off), 180f, 90f, seg, x0, y1);
            Corner(o, new Vector2(r.xMax - rad.y, r.yMax - rad.y), Mathf.Max(0, rad.y + off), 90f, 0f, seg, x1, y1);
            Corner(o, new Vector2(r.xMax - rad.z, r.yMin + rad.z), Mathf.Max(0, rad.z + off), 0f, -90f, seg, x1, y0);
            Corner(o, new Vector2(r.xMin + rad.w, r.yMin + rad.w), Mathf.Max(0, rad.w + off), -90f, -180f, seg, x0, y0);
        }

        static void Corner(List<Vector2> o, Vector2 c, float rr, float a0, float a1, int seg, float sx, float sy)
        {
            if (rr <= 0.0001f) { for (int i = 0; i <= seg; i++) o.Add(new Vector2(sx, sy)); return; }   // same vertex count for every ring
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Deg2Rad * Mathf.Lerp(a0, a1, i / (float)seg);
                o.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr);
            }
        }

        Color Fill(float y, Rect r)
        {
            if (!gradient) return color;
            return Color.Lerp(bottomColor, color, Mathf.InverseLerp(r.yMin, r.yMax, y));
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            if (r.width <= 0 || r.height <= 0) return;
            var rad = Radii(r); int seg = Segs(rad); float aa = Px();

            // shadow: the shape offset and blurred — a gaussian edge, close to piecewise linear: full 2σ inside, half on
            // the edge, nothing 2σ outside
            if (shadowColor.a > 0.001f)
            {
                var sr = new Rect(r.position + shadowOffset, r.size);
                float s = Mathf.Max(aa * 0.5f, shadowBlur);
                Outline(ringA, sr, rad, -2f * s, seg); Outline(ringB, sr, rad, 0f, seg); Outline(ringC, sr, rad, 2f * s, seg);
                var c0 = shadowColor; var cm = shadowColor; cm.a *= 0.5f; var c1 = shadowColor; c1.a = 0;
                Fan(vh, ringA, sr.center, c0, null, r);
                Ring(vh, ringA, ringB, c0, cm);
                Ring(vh, ringB, ringC, cm, c1);
            }

            float bw = Mathf.Max(0, borderWidth);
            // fill (inside the border), anti-aliased edge
            Outline(ringA, r, rad, -bw - aa * 0.5f, seg);
            Fan(vh, ringA, r.center, color, this, r);
            if (bw > 0.001f)
            {
                Outline(ringB, r, rad, -bw + aa * 0.5f, seg);
                RingFill(vh, ringA, ringB, r);   // blend fill into the border without a gap
                Outline(ringA, r, rad, -bw + aa * 0.5f, seg); Outline(ringB, r, rad, -aa * 0.5f, seg);
                Ring(vh, ringA, ringB, borderColor, borderColor);
                Outline(ringA, r, rad, -aa * 0.5f, seg); Outline(ringB, r, rad, aa * 0.5f, seg);
                var bc1 = borderColor; bc1.a = 0;
                Ring(vh, ringA, ringB, borderColor, bc1);   // anti-aliased outer edge
            }
            else
            {
                Outline(ringB, r, rad, aa * 0.5f, seg);
                FringeFill(vh, ringA, ringB, r);
            }

            if (stitch && stitchWidth > 0 && dash > 0) Stitch(vh, r, rad, seg, aa);
        }

        void Fan(VertexHelper vh, List<Vector2> ring, Vector2 center, Color c, SoftRect fillFrom, Rect r)
        {
            int start = vh.currentVertCount;
            vh.AddVert(center, fillFrom != null ? Fill(center.y, r) : (Color32)c, Vector2.zero);
            for (int i = 0; i < ring.Count; i++) vh.AddVert(ring[i], fillFrom != null ? Fill(ring[i].y, r) : (Color32)c, Vector2.zero);
            for (int i = 0; i < ring.Count; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % ring.Count);
        }

        static void Ring(VertexHelper vh, List<Vector2> inner, List<Vector2> outer, Color ci, Color co)
        {
            int start = vh.currentVertCount, n = inner.Count;
            for (int i = 0; i < n; i++) { vh.AddVert(inner[i], ci, Vector2.zero); vh.AddVert(outer[i], co, Vector2.zero); }
            for (int i = 0; i < n; i++)
            {
                int a = start + i * 2, b = start + ((i + 1) % n) * 2;
                vh.AddTriangle(a, b, a + 1); vh.AddTriangle(b, b + 1, a + 1);
            }
        }

        void RingFill(VertexHelper vh, List<Vector2> inner, List<Vector2> outer, Rect r)
        {
            int start = vh.currentVertCount, n = inner.Count;
            for (int i = 0; i < n; i++) { vh.AddVert(inner[i], Fill(inner[i].y, r), Vector2.zero); vh.AddVert(outer[i], Fill(outer[i].y, r), Vector2.zero); }
            for (int i = 0; i < n; i++)
            {
                int a = start + i * 2, b = start + ((i + 1) % n) * 2;
                vh.AddTriangle(a, b, a + 1); vh.AddTriangle(b, b + 1, a + 1);
            }
        }

        void FringeFill(VertexHelper vh, List<Vector2> inner, List<Vector2> outer, Rect r)
        {
            int start = vh.currentVertCount, n = inner.Count;
            for (int i = 0; i < n; i++)
            {
                var ci = Fill(inner[i].y, r); var co = ci; co.a = 0;
                vh.AddVert(inner[i], ci, Vector2.zero); vh.AddVert(outer[i], co, Vector2.zero);
            }
            for (int i = 0; i < n; i++)
            {
                int a = start + i * 2, b = start + ((i + 1) % n) * 2;
                vh.AddTriangle(a, b, a + 1); vh.AddTriangle(b, b + 1, a + 1);
            }
        }

        /// <summary>Fills <see cref="path"/> with the line the dashes run along; false when the shape is too small for one.</summary>
        bool StitchPath(Rect r, Vector4 rad, int seg, out int arcSeg)
        {
            arcSeg = seg * 2;   // points per corner on the dash path
            if (stitchPad.x >= 0f)
            {
                // the stitch's own rect (Figma): pads are left, top, right, bottom (top/bottom in UI y-up)
                var sr = Rect.MinMaxRect(r.xMin + stitchPad.x, r.yMin + stitchPad.w, r.xMax - stitchPad.z, r.yMax - stitchPad.y);
                if (sr.width <= 0 || sr.height <= 0) return false;
                float m = Mathf.Min(sr.width, sr.height) * 0.5f;
                float avg = (stitchPad.x + stitchPad.y + stitchPad.z + stitchPad.w) * 0.25f;
                Vector4 sradius = stitchRadius >= 0f ? new Vector4(stitchRadius, stitchRadius, stitchRadius, stitchRadius) : rad - new Vector4(avg, avg, avg, avg);
                sradius = new Vector4(Mathf.Clamp(sradius.x, 0, m), Mathf.Clamp(sradius.y, 0, m), Mathf.Clamp(sradius.z, 0, m), Mathf.Clamp(sradius.w, 0, m));
                arcSeg = Mathf.Max(seg, Segs(sradius)) * 2;
                Outline(path, sr, sradius, 0f, arcSeg);
            }
            else Outline(path, r, rad, -stitchInset, seg * 2);
            return path.Count >= 3;
        }

        /// <summary>Length of the stitch line at the shape's current size (editor tools use it to keep the design's spacing).</summary>
        public float StitchLength()
        {
            var r = rectTransform.rect;
            if (r.width <= 0 || r.height <= 0) return 0f;
            var rad = Radii(r);
            if (!StitchPath(r, rad, Segs(rad), out _)) return 0f;
            float total = 0f; int n = path.Count;
            for (int i = 0; i < n; i++) total += (path[(i + 1) % n] - path[i]).magnitude;
            return total;
        }

        void Stitch(VertexHelper vh, Rect r, Vector4 rad, int seg, float aa)
        {
            if (!StitchPath(r, rad, seg, out int arcSeg)) return;
            int n = path.Count;
            // arc length along the closed path
            var cum = new float[n + 1]; cum[0] = 0;
            for (int i = 0; i < n; i++) cum[i + 1] = cum[i] + (path[(i + 1) % n] - path[i]).magnitude;
            float total = cum[n]; if (total <= dash) return;
            int count = stitchCount > 0
                ? (stitchPeriod > 0f ? Mathf.Max(4, Mathf.RoundToInt(total / stitchPeriod)) : stitchCount)   // the design's spacing at any size
                : Mathf.Max(1, Mathf.RoundToInt(total / (dash + gap)));
            float period = total / count, d = stitchCount > 0 ? Mathf.Min(dash, period * 0.9f) : dash * period / (dash + gap);   // even dashes all the way round
            float hw = stitchWidth * 0.5f, cap = roundCaps ? hw : 0f;
            // Figma's stitch has a dash centred on the top-left corner's diagonal (the path starts where that arc begins)
            float phase = stitchPad.x >= 0f ? cum[Mathf.Min(arcSeg, n)] * 0.5f - d * 0.5f : 0f;
            bool shade = stitchShadow.a > 0.001f;
            var dashes = new List<List<Vector2>>(count);
            for (int k = 0; k < count; k++)
            {
                float s0 = phase + k * period - cap, s1 = phase + k * period + d + cap;
                // corner points inside the dash, in order along the path (a dash may cross the path's start)
                var inner = new List<KeyValuePair<float, Vector2>>();
                for (int i = 0; i < n; i++)
                    for (int w = -1; w <= 1; w++) { float c = cum[i] + w * total; if (c > s0 && c < s1) inner.Add(new KeyValuePair<float, Vector2>(c, path[i])); }
                inner.Sort((a, b) => a.Key.CompareTo(b.Key));
                var pts = new List<Vector2> { At(path, cum, s0) };
                foreach (var kv in inner) pts.Add(kv.Value);
                pts.Add(At(path, cum, s1));
                dashes.Add(pts);
            }
            if (shade)
            {
                // blurred copy under the dashes: wider soft edge, a little lighter at the core (gaussian of a thin line)
                float s = Mathf.Max(aa, shadowBlur);
                var sc = stitchShadow; sc.a *= 0.85f;
                foreach (var pts in dashes)
                {
                    var moved = new List<Vector2>(pts.Count); foreach (var p in pts) moved.Add(p + shadowOffset);
                    Strip(vh, moved, hw, 3f * s, sc);
                }
            }
            foreach (var pts in dashes) Strip(vh, pts, hw, aa, stitchColor);
        }

        static Vector2 At(List<Vector2> p, float[] cum, float s)
        {
            int n = p.Count; s = Mathf.Repeat(s, cum[n]);
            for (int i = 0; i < n; i++)
                if (s <= cum[i + 1]) { float t = (s - cum[i]) / Mathf.Max(1e-5f, cum[i + 1] - cum[i]); return Vector2.Lerp(p[i], p[(i + 1) % n], t); }
            return p[0];
        }

        static void Strip(VertexHelper vh, List<Vector2> pts, float hw, float aa, Color c)
        {
            if (pts.Count < 2) return;
            var c0 = c; c0.a = 0;
            int start = vh.currentVertCount;
            for (int i = 0; i < pts.Count; i++)
            {
                var t = pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)];
                var nrm = new Vector2(-t.y, t.x).normalized;
                vh.AddVert(pts[i] + nrm * (hw + aa * 0.5f), c0, Vector2.zero);
                vh.AddVert(pts[i] + nrm * Mathf.Max(0, hw - aa * 0.5f), c, Vector2.zero);
                vh.AddVert(pts[i] - nrm * Mathf.Max(0, hw - aa * 0.5f), c, Vector2.zero);
                vh.AddVert(pts[i] - nrm * (hw + aa * 0.5f), c0, Vector2.zero);
            }
            for (int i = 0; i < pts.Count - 1; i++)
            {
                int a = start + i * 4, b = a + 4;
                for (int j = 0; j < 3; j++) { vh.AddTriangle(a + j, b + j, a + j + 1); vh.AddTriangle(a + j + 1, b + j, b + j + 1); }
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate() { base.OnValidate(); SetVerticesDirty(); }
#endif
        protected override void OnRectTransformDimensionsChange() { base.OnRectTransformDimensionsChange(); SetVerticesDirty(); }
        protected override void OnCanvasHierarchyChanged() { base.OnCanvasHierarchyChanged(); SetVerticesDirty(); }
    }
}

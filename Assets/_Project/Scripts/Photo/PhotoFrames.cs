using UnityEngine;

namespace Mongle.Photo
{
    /// <summary>
    /// Photo frames (D-025): the plain cream frame plus the three Figma patterns (아트보드 11 copy) redrawn as tiling textures —
    /// pink dots on chocolate, pink gingham, brown stars on cream. The studio's viewfinder is drawn with them and the chosen
    /// pattern is painted into the photo's border when it is taken.
    /// </summary>
    public static class PhotoFrames
    {
        public static readonly string[] Ids = { "plain", "dots", "gingham", "stars" };
        public static readonly string[] Names = { "기본", "물방울", "깅엄", "별" };
        public const int Tile = 96;
        static readonly Texture2D[] cache = new Texture2D[Ids.Length];

        static readonly Color Cream = new Color(1f, 0.99f, 0.95f, 1f);
        static readonly Color Choco = new Color(0.27f, 0.13f, 0.06f, 1f), DotPink = new Color(1f, 0.87f, 0.9f, 1f);
        static readonly Color G0 = new Color(1f, 0.97f, 0.97f, 1f), G1 = new Color(1f, 0.88f, 0.89f, 1f), G2 = new Color(0.99f, 0.8f, 0.82f, 1f);
        static readonly Color StarBg = new Color(0.97f, 0.91f, 0.77f, 1f), StarBrown = new Color(0.48f, 0.35f, 0.3f, 1f);

        public static int Index(string id) { int i = System.Array.IndexOf(Ids, id); return i < 0 ? 0 : i; }

        /// <summary>A repeating tile of the pattern (wrap mode Repeat, so a RawImage can tile it with its uvRect).</summary>
        public static Texture2D Pattern(int index)
        {
            index = Mathf.Clamp(index, 0, Ids.Length - 1);
            if (cache[index] != null) return cache[index];
            var t = new Texture2D(Tile, Tile, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "Frame_" + Ids[index] };
            var px = new Color[Tile * Tile];
            for (int y = 0; y < Tile; y++)
                for (int x = 0; x < Tile; x++)
                    px[y * Tile + x] = Sample(index, x + 0.5f, y + 0.5f);
            t.SetPixels(px); t.Apply();
            return cache[index] = t;
        }

        /// <summary>The pattern's colour at a point of its tile (pixels, anti-aliased edges).</summary>
        static Color Sample(int index, float x, float y)
        {
            switch (Ids[index])
            {
                case "dots":
                {
                    // two dots per tile, staggered
                    float d = Mathf.Min(Dist(x, y, 24f, 24f), Dist(x, y, 72f, 72f));
                    return Color.Lerp(DotPink, Choco, Mathf.Clamp01(d - 11f));
                }
                case "gingham":
                {
                    bool vx = (x % 48f) < 24f, hy = (y % 48f) < 24f;
                    return vx && hy ? G2 : vx || hy ? G1 : G0;
                }
                case "stars":
                {
                    float s = Mathf.Max(Star(x - 26f, y - 26f, 17f), Star(x - 74f, y - 74f, 17f));
                    return Color.Lerp(StarBg, StarBrown, s);
                }
                default: return Cream;
            }
        }

        /// <summary>Distance on the tile with wrap-around, so dots on the edges join the next tile.</summary>
        static float Dist(float x, float y, float cx, float cy)
        {
            float dx = Mathf.Abs(x - cx), dy = Mathf.Abs(y - cy);
            dx = Mathf.Min(dx, Tile - dx); dy = Mathf.Min(dy, Tile - dy);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Coverage (0..1) of a five-point star of outer radius r centred at the origin.</summary>
        static float Star(float x, float y, float r)
        {
            // wrap around the tile like the dots
            if (x > Tile / 2f) x -= Tile; if (x < -Tile / 2f) x += Tile;
            if (y > Tile / 2f) y -= Tile; if (y < -Tile / 2f) y += Tile;
            float ang = Mathf.Atan2(x, y), dist = Mathf.Sqrt(x * x + y * y);
            // the star's radius at this angle: between the outer points (r) and the inner corners (0.45 r)
            float seg = Mathf.PI * 2f / 5f, a = Mathf.Repeat(ang, seg) - seg / 2f;
            float k = Mathf.Abs(a) / (seg / 2f);
            float edge = Mathf.Lerp(r * 0.45f, r, 1f - k);
            return Mathf.Clamp01(edge - dist + 0.5f);
        }

        /// <summary>Paints the pattern into the outer <paramref name="border"/> pixels of a photo (the plain frame leaves it as it is).</summary>
        public static void Bake(Texture2D photo, int index, int border)
        {
            if (photo == null || index <= 0) return;
            int w = photo.width, h = photo.height;
            var px = photo.GetPixels();
            float scale = Mathf.Max(1f, w / 1024f) * 0.75f;   // the tile looks the same size as in the studio's viewfinder
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (x >= border && x < w - border && y >= border && y < h - border) continue;
                    px[y * w + x] = Sample(index, Mathf.Repeat(x / scale, Tile), Mathf.Repeat(y / scale, Tile));
                }
            photo.SetPixels(px); photo.Apply();
        }
    }
}

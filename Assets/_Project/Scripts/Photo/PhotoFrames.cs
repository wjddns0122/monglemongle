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
        // the stars run in staggered rows (Figma 88:1081): 68 apart along a row, rows 56 apart, every other row shifted half a
        // step, so one repeat is 68 x 112 pixels; at the studio's 1.875 screen px per pattern pixel that is Figma's 126 x 106 spacing
        const int StarW = 68, StarH = 112;
        const float StarR = 20f, StarInner = 0.5f, StarRound = 2.5f;
        static readonly Texture2D[] cache = new Texture2D[Ids.Length];

        /// <summary>One repeat of the pattern in pixels (the stars' staggered rows need a tall tile; the rest are square).</summary>
        public static Vector2Int Size(int index) => Ids[Mathf.Clamp(index, 0, Ids.Length - 1)] == "stars" ? new Vector2Int(StarW, StarH) : new Vector2Int(Tile, Tile);

        static readonly Color Cream = new Color(1f, 0.99f, 0.95f, 1f);
        static readonly Color Choco = new Color(0.27f, 0.13f, 0.06f, 1f), DotPink = new Color(1f, 0.87f, 0.9f, 1f);
        // 11-26 (Figma 빈창 88:1040 / 사진찍기 88:1081): white + #FDEFEF + #FDDADA gingham, #C69C6D stars on #F9EDC8
        static readonly Color G0 = new Color(1f, 1f, 1f, 1f), G1 = new Color(0.992f, 0.937f, 0.937f, 1f), G2 = new Color(0.992f, 0.855f, 0.855f, 1f);
        static readonly Color StarBg = new Color(0.976f, 0.929f, 0.784f, 1f), StarBrown = new Color(0.776f, 0.612f, 0.427f, 1f);

        public static int Index(string id) { int i = System.Array.IndexOf(Ids, id); return i < 0 ? 0 : i; }

        /// <summary>A repeating tile of the pattern (wrap mode Repeat, so a RawImage can tile it with its uvRect).</summary>
        public static Texture2D Pattern(int index)
        {
            index = Mathf.Clamp(index, 0, Ids.Length - 1);
            if (cache[index] != null) return cache[index];
            var sz = Size(index);
            var t = new Texture2D(sz.x, sz.y, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "Frame_" + Ids[index] };
            var px = new Color[sz.x * sz.y];
            for (int y = 0; y < sz.y; y++)
                for (int x = 0; x < sz.x; x++)
                    px[y * sz.x + x] = Sample(index, x + 0.5f, y + 0.5f);
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
                    // a star in the middle of the lower row, and the shifted row above it on the tile's corners
                    float s = Mathf.Max(Star(x - StarW * 0.5f, y - StarH * 0.25f), Star(x, y - StarH * 0.75f));
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

        /// <summary>Coverage (0..1) of an upright five-point star centred at the origin, its points rounded off (Figma 사진찍기 / 빈창).</summary>
        static float Star(float x, float y)
        {
            // wrap around the star tile, so stars on its edges join the next tile
            if (x > StarW / 2f) x -= StarW; if (x < -StarW / 2f) x += StarW;
            if (y > StarH / 2f) y -= StarH; if (y < -StarH / 2f) y += StarH;
            return Mathf.Clamp01(StarRound - StarDistance(new Vector2(x, y), StarR - StarRound, StarInner) + 0.5f);
        }

        /// <summary>Signed distance to a five-point star pointing up (+y): outer radius r, inner corners at rf x r.</summary>
        static float StarDistance(Vector2 p, float r, float rf)
        {
            var k1 = new Vector2(0.809016994f, -0.587785252f); var k2 = new Vector2(-k1.x, k1.y);
            p.x = Mathf.Abs(p.x);
            p -= 2f * Mathf.Max(Vector2.Dot(k1, p), 0f) * k1;
            p -= 2f * Mathf.Max(Vector2.Dot(k2, p), 0f) * k2;
            p.x = Mathf.Abs(p.x);
            p.y -= r;
            var ba = rf * new Vector2(-k1.y, k1.x) - new Vector2(0f, 1f);
            float h = Mathf.Clamp(Vector2.Dot(p, ba) / Vector2.Dot(ba, ba), 0f, r);
            return (p - ba * h).magnitude * Mathf.Sign(p.y * ba.x - p.x * ba.y);
        }

        /// <summary>How big one pattern pixel is in a photo <paramref name="width"/> pixels wide (what <see cref="Bake"/> paints with).</summary>
        public static float PhotoScale(int width) => Mathf.Max(1f, width / 1024f) * 0.75f;

        /// <summary>
        /// Which pattern <see cref="Bake"/> painted into a photo's border (0, the plain frame, when none matches): the border's
        /// pixels against each pattern at the same places. The photo record does not keep the frame, its picture does.
        /// </summary>
        public static int Detect(Texture2D photo)
        {
            if (photo == null || !photo.isReadable || photo.width < 64) return 0;
            int w = photo.width, h = photo.height, border = Mathf.Max(2, Mathf.Min(w, h) / 16);
            float scale = PhotoScale(w);
            int best = 0; float bestErr = 0.08f;   // a mean colour difference under 8 % is that pattern
            for (int i = 1; i < Ids.Length; i++)
            {
                var sz = Size(i); float err = 0f; int n = 0;
                for (int k = 0; k < 48; k++)
                {
                    // along the bottom and the left border, inside it
                    int x = k < 24 ? border + (w - 2 * border) * k / 24 : border / 2, y = k < 24 ? border / 2 : border + (h - 2 * border) * (k - 24) / 24;
                    Color a = photo.GetPixel(x, y), b = Sample(i, Mathf.Repeat(x / scale, sz.x), Mathf.Repeat(y / scale, sz.y));
                    err += (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b)) / 3f; n++;
                }
                err /= n;
                if (err < bestErr) { bestErr = err; best = i; }
            }
            return best;
        }

        /// <summary>Paints the pattern into the outer <paramref name="border"/> pixels of a photo (the plain frame leaves it as it is).</summary>
        public static void Bake(Texture2D photo, int index, int border)
        {
            if (photo == null || index <= 0) return;
            int w = photo.width, h = photo.height;
            var px = photo.GetPixels();
            float scale = Mathf.Max(1f, w / 1024f) * 0.75f;   // the tile looks the same size as in the studio's viewfinder
            var sz = Size(index);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (x >= border && x < w - border && y >= border && y < h - border) continue;
                    px[y * w + x] = Sample(index, Mathf.Repeat(x / scale, sz.x), Mathf.Repeat(y / scale, sz.y));
                }
            photo.SetPixels(px); photo.Apply();
        }
    }
}

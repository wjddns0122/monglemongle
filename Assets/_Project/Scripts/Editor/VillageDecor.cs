using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mongle.EditorTools
{
    /// <summary>
    /// Dreamy daytime amusement-park dressing for the Village (D-004): pastel gradient sky, a tiled promenade around the
    /// park, star lamps with balloon bunches, pennant bunting, candy trees, puff bushes, gumdrops, and far-off
    /// hot-air balloons, clouds and a castle (the rides themselves stand in the park, D-006). Nothing here has colliders,
    /// so clicks, the furniture grid and the walk area are not affected — except the candy trees, which carry a click capsule and
    /// a FruitTree to shake (D-011; the NavMesh only collects the walk area's children, so they stay plain holes).
    /// </summary>
    public static class VillageDecor
    {
        const string MeshDir = "Assets/_Project/Art/Meshes/";
        public static readonly Color Pink = new Color(1f, 0.74f, 0.84f), Lav = new Color(0.82f, 0.74f, 1f), Mint = new Color(0.68f, 0.93f, 0.84f),
            Peach = new Color(1f, 0.84f, 0.7f), SkyBlue = new Color(0.7f, 0.85f, 1f), Cream = new Color(1f, 0.96f, 0.9f), Butter = new Color(1f, 0.93f, 0.64f);
        static readonly Color[] Pastels = { Pink, Lav, Mint, Peach, SkyBlue };
        static readonly string[] PastelNames = { "Pink", "Lav", "Mint", "Peach", "Sky" };
        /// <summary>Horizontal direction from the garden toward the default orbit camera (yaw 28).</summary>
        static readonly Vector3 ToCamera = new Vector3(0.47f, 0f, 0.88f);

        static Material M(string n, Color c, float s = 0.05f, float metal = 0f, Color? emit = null) => ContentBuilder.GetOrCreateMat(n, c, s, metal, emit);
        static Material Candy(int i) => M("MC_Candy" + PastelNames[i % 5], Pastels[i % 5]);        // soft felt, big fuzz (FeltLook "MC_")
        static Material CandyCream => M("MC_CandyCream", Cream);
        static Material Glossy(string n, Color c) => M(n, c, 0.65f);                                // plain glossy Lit (balloons, gumdrops)

        static GameObject P(PrimitiveType t, string name, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3? euler = null, bool shadow = true)
        {
            var g = GameObject.CreatePrimitive(t); g.name = name; g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale; if (euler.HasValue) g.transform.localEulerAngles = euler.Value;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            var r = g.GetComponent<Renderer>(); r.sharedMaterial = m; if (!shadow) r.shadowCastingMode = ShadowCastingMode.Off;
            return g;
        }
        static GameObject PM(Mesh mesh, string name, Transform parent, Vector3 pos, Vector3 scale, Material m, Quaternion rot)
        {
            var g = new GameObject(name); g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale; g.transform.localRotation = rot;
            g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterial = m;
            return g;
        }
        static Transform Group(Transform parent, string name) { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }

        // ---------------- generated meshes (star, cone, pennant) ----------------
        static Mesh SaveMesh(string name, Mesh m)
        {
            Directory.CreateDirectory(MeshDir); string path = MeshDir + name + ".asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null) { EditorUtility.CopySerialized(m, old); Object.DestroyImmediate(m); return old; }
            AssetDatabase.CreateAsset(m, path); return m;
        }
        static Mesh FromLists(string name, System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<int> t)
        {
            var m = new Mesh { name = name }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }
        /// <summary>Five-point star, outer radius 1, facing +Z, depth 0.25, flat faces.</summary>
        static Mesh StarMesh()
        {
            var v = new System.Collections.Generic.List<Vector3>(); var t = new System.Collections.Generic.List<int>();
            var rim = new Vector3[10];
            for (int i = 0; i < 10; i++) { float r = i % 2 == 0 ? 1f : 0.45f, a = Mathf.PI / 2 + i * Mathf.PI / 5; rim[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0); }
            const float d = 0.125f;
            foreach (int side in new[] { 1, -1 })
            {
                int c = v.Count; v.Add(new Vector3(0, 0, d * side * 1.6f));
                for (int i = 0; i < 10; i++) v.Add(rim[i] + new Vector3(0, 0, d * side));
                // Unity: counter-clockwise in the XY plane faces +Z
                for (int i = 0; i < 10; i++) { int a = c + 1 + i, b = c + 1 + (i + 1) % 10; if (side > 0) { t.Add(c); t.Add(a); t.Add(b); } else { t.Add(c); t.Add(b); t.Add(a); } }
            }
            for (int i = 0; i < 10; i++)
            {
                int c = v.Count; var p0 = rim[i]; var p1 = rim[(i + 1) % 10];
                v.Add(p0 + Vector3.forward * d); v.Add(p1 + Vector3.forward * d); v.Add(p1 - Vector3.forward * d); v.Add(p0 - Vector3.forward * d);
                t.Add(c); t.Add(c + 2); t.Add(c + 1); t.Add(c); t.Add(c + 3); t.Add(c + 2);
            }
            return SaveMesh("ME_Star", FromLists("ME_Star", v, t));
        }
        /// <summary>Cone with base radius 1 at y=0 and apex at y=1.</summary>
        static Mesh ConeMesh()
        {
            var v = new System.Collections.Generic.List<Vector3>(); var t = new System.Collections.Generic.List<int>(); const int n = 32;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2 / n, a1 = (i + 1) * Mathf.PI * 2 / n; int c = v.Count;
                v.Add(new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0))); v.Add(new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1))); v.Add(Vector3.up);
                t.Add(c); t.Add(c + 2); t.Add(c + 1);
                int b = v.Count; v.Add(Vector3.zero); v.Add(new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0))); v.Add(new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)));
                t.Add(b); t.Add(b + 1); t.Add(b + 2);
            }
            var m = FromLists("ME_Cone", v, t);
            // smooth the side: normals point out and up from the axis
            var nr = m.normals;
            for (int i = 0; i < n; i++) for (int k = 0; k < 3; k++)
                {
                    int idx = i * 6 + k; var p = v[idx]; var flat = k == 2 ? new Vector3(Mathf.Cos((i + 0.5f) * Mathf.PI * 2 / n), 0, Mathf.Sin((i + 0.5f) * Mathf.PI * 2 / n)) : new Vector3(p.x, 0, p.z);
                    nr[idx] = (flat.normalized + Vector3.up * 0.7f).normalized;
                }
            m.normals = nr;
            return SaveMesh("ME_Cone", m);
        }
        /// <summary>Hanging triangle flag: top edge along X from -0.5 to 0.5 at y=0, tip at y=-1, two-sided.</summary>
        static Mesh PennantMesh()
        {
            var v = new System.Collections.Generic.List<Vector3> { new Vector3(-0.5f, 0, 0), new Vector3(0.5f, 0, 0), new Vector3(0, -1, 0), new Vector3(-0.5f, 0, 0), new Vector3(0.5f, 0, 0), new Vector3(0, -1, 0) };
            var t = new System.Collections.Generic.List<int> { 0, 1, 2, 3, 5, 4 };
            return SaveMesh("ME_Pennant", FromLists("ME_Pennant", v, t));
        }

        // ---------------- sky ----------------
        public static void Sky(Camera cam)
        {
            var sh = Shader.Find("Mongle/SkyGradient");
            const string path = "Assets/_Project/Art/Look/M_SkyVillage.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.SetColor("_Top", new Color(0.6f, 0.77f, 1f)); m.SetColor("_Mid", new Color(0.83f, 0.8f, 1f));
            m.SetColor("_Horizon", new Color(1f, 0.86f, 0.9f)); m.SetColor("_Bottom", new Color(0.96f, 0.9f, 0.95f));
            m.SetVector("_SunDir", new Vector4(-0.45f, 0.55f, -0.7f, 0)); EditorUtility.SetDirty(m);
            RenderSettings.skybox = m;
            cam.clearFlags = CameraClearFlags.Skybox;
            // soft pastel haze so far hills and castles melt into the sky
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.97f, 0.88f, 0.95f); RenderSettings.fogStartDistance = 30f; RenderSettings.fogEndDistance = 85f;
        }

        // ---------------- promenade around the garden ----------------
        /// <summary>Checkered pink/cream walkway framing the lawn (inner edge = lawn edge), centred on <paramref name="center"/>.</summary>
        public static void Promenade(Transform world, Vector3 center, float ix, float iz)
        {
            var root = Group(world, "Promenade"); root.localPosition = center;
            var a = M("MS_TilePink", new Color(1f, 0.7f, 0.8f)); var b = M("MS_TileCream", new Color(1f, 0.93f, 0.82f));
            var curb = M("MS_Curb", new Color(1f, 0.98f, 0.95f));
            const float t = 0.36f; const int rows = 2;
            float ox = ix + rows * t, oz = iz + rows * t;
            void Tile(float x, float z)
            {
                int k = Mathf.RoundToInt((x + 40f) / t) + Mathf.RoundToInt((z + 40f) / t);
                P(PrimitiveType.Cube, "Tile", root, new Vector3(x, -0.004f, z), new Vector3(t - 0.025f, 0.03f, t - 0.025f), k % 2 == 0 ? a : b, null, false);
            }
            for (int r = 0; r < rows; r++)
            {
                for (float x = -ox + t / 2; x < ox; x += t) { Tile(x, -(iz + t * (r + 0.5f))); Tile(x, iz + t * (r + 0.5f)); }
                for (float z = -iz + t / 2; z < iz; z += t) { Tile(-(ix + t * (r + 0.5f)), z); Tile(ix + t * (r + 0.5f), z); }
            }
            foreach (var e in new[] { new Vector2(ix, iz), new Vector2(ox, oz) })
            {
                P(PrimitiveType.Cube, "Curb", root, new Vector3(0, 0.01f, -e.y), new Vector3(2 * e.x + 0.06f, 0.04f, 0.05f), curb, null, false);
                P(PrimitiveType.Cube, "Curb", root, new Vector3(0, 0.01f, e.y), new Vector3(2 * e.x + 0.06f, 0.04f, 0.05f), curb, null, false);
                P(PrimitiveType.Cube, "Curb", root, new Vector3(-e.x, 0.01f, 0), new Vector3(0.05f, 0.04f, 2 * e.y), curb, null, false);
                P(PrimitiveType.Cube, "Curb", root, new Vector3(e.x, 0.01f, 0), new Vector3(0.05f, 0.04f, 2 * e.y), curb, null, false);
            }
        }

        // ---------------- star lamps, balloons, bunting ----------------
        /// <summary>Pink lamp posts with a glowing globe and a golden star; returns the globe positions.</summary>
        public static Vector3[] Lamps(Transform world, Vector2[] spots)
        {
            var root = Group(world, "StarLamps");
            var post = M("MS_LampPink", new Color(1f, 0.7f, 0.8f)); var baseM = M("MS_LampBase", Cream);
            var gold = M("M_GoldTrim", new Color(1f, 0.8f, 0.45f), 0.6f, 0.6f);
            var glow = M("M_DreamGlow", new Color(1f, 0.94f, 0.76f), 0.3f, 0f, new Color(1.8f, 1.5f, 1.0f));
            var starM = M("M_StarGlow", new Color(1f, 0.9f, 0.55f), 0.3f, 0f, new Color(1.5f, 1.25f, 0.5f));
            var star = StarMesh(); var cone = ConeMesh();
            var tops = new Vector3[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                var g = Group(root, "Lamp" + i); g.localPosition = new Vector3(spots[i].x, 0, spots[i].y);
                P(PrimitiveType.Cylinder, "Base", g, new Vector3(0, 0.025f, 0), new Vector3(0.16f, 0.025f, 0.16f), baseM);
                P(PrimitiveType.Cylinder, "Post", g, new Vector3(0, 0.31f, 0), new Vector3(0.045f, 0.28f, 0.045f), post);
                P(PrimitiveType.Cylinder, "Ring", g, new Vector3(0, 0.595f, 0), new Vector3(0.085f, 0.012f, 0.085f), gold);
                P(PrimitiveType.Sphere, "Globe", g, new Vector3(0, 0.69f, 0), Vector3.one * 0.17f, glow, null, false);
                PM(cone, "Cap", g, new Vector3(0, 0.765f, 0), new Vector3(0.075f, 0.08f, 0.075f), post, Quaternion.identity);
                PM(star, "Star", g, new Vector3(0, 0.9f, 0), Vector3.one * 0.065f, starM, Quaternion.LookRotation(ToCamera));
                tops[i] = g.localPosition + new Vector3(0, 0.69f, 0);
            }
            return tops;
        }

        /// <summary>A bunch of glossy pastel balloons tied to a point.</summary>
        public static void Balloons(Transform world, Vector3 tie, int count, int seed)
        {
            var root = Group(world, "Balloons"); var rng = new System.Random(seed);
            var stringM = M("MS_BalloonString", Cream);
            for (int i = 0; i < count; i++)
            {
                float ang = (i / (float)count + (float)rng.NextDouble() * 0.15f) * Mathf.PI * 2f;
                var top = tie + new Vector3(Mathf.Cos(ang) * 0.16f, 0.45f + (float)rng.NextDouble() * 0.3f, Mathf.Sin(ang) * 0.16f);
                var mat = Glossy("M_Balloon" + ((i + seed) % 5), Pastels[(i + seed) % 5]);
                P(PrimitiveType.Sphere, "Balloon", root, top, new Vector3(0.15f, 0.18f, 0.15f), mat);
                P(PrimitiveType.Sphere, "Knot", root, top - new Vector3(0, 0.095f, 0), Vector3.one * 0.03f, mat);
                Rope(root, tie, top - new Vector3(0, 0.1f, 0), 0.006f, stringM);
            }
        }

        static void Rope(Transform parent, Vector3 a, Vector3 b, float thick, Material m)
        {
            var d = b - a;
            P(PrimitiveType.Cylinder, "Rope", parent, (a + b) * 0.5f, new Vector3(thick, d.magnitude * 0.5f, thick), m, null, false).transform.localRotation = Quaternion.FromToRotation(Vector3.up, d);
        }

        /// <summary>A sagging string of pastel pennants between two points.</summary>
        public static void Bunting(Transform world, Vector3 a, Vector3 b, float sag, int flags)
        {
            var root = Group(world, "Bunting");
            var rope = M("MS_BalloonString", Cream); var pennant = PennantMesh();
            Vector3 At(float t) => Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1 - t));
            const int seg = 12;
            for (int i = 0; i < seg; i++) Rope(root, At(i / (float)seg), At((i + 1) / (float)seg), 0.007f, rope);
            var dir = (b - a); dir.y = 0; dir.Normalize();
            var rot = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up);
            for (int i = 0; i < flags; i++)
            {
                float t = (i + 0.5f) / flags;
                PM(pennant, "Flag", root, At(t), new Vector3(0.1f, 0.13f, 1f), M("MS_Flag" + PastelNames[i % 5], Pastels[i % 5]), rot);
            }
        }

        // ---------------- plants and candy ----------------
        /// <summary>Kind 0 cotton-candy tree, 1 soft-serve tree, 2 lollipop; i picks the colours.</summary>
        public static Transform CandyTree(Transform world, Vector3 p, float sc, int kind, int i)
        {
            var g = Group(world, "CandyTree"); g.localPosition = p;
            var c1 = Candy(i); var c2 = Candy(i + 2);
            if (kind == 0)
            {
                P(PrimitiveType.Cylinder, "Trunk", g, new Vector3(0, 0.3f * sc - 0.02f, 0), new Vector3(0.11f, 0.32f, 0.11f) * sc, CandyCream);
                P(PrimitiveType.Sphere, "Puff", g, new Vector3(0, 0.88f * sc, 0), Vector3.one * 0.92f * sc, c1);
                P(PrimitiveType.Sphere, "PuffB", g, new Vector3(0.3f, 0.76f, 0.1f) * sc, Vector3.one * 0.6f * sc, c2);
                P(PrimitiveType.Sphere, "PuffC", g, new Vector3(-0.26f, 0.8f, -0.14f) * sc, Vector3.one * 0.5f * sc, c1);
            }
            else if (kind == 1)
            {
                P(PrimitiveType.Cylinder, "Trunk", g, new Vector3(0, 0.17f * sc, 0), new Vector3(0.1f, 0.2f, 0.1f) * sc, CandyCream);
                for (int t = 0; t < 4; t++)
                    P(PrimitiveType.Sphere, "Swirl", g, new Vector3(0, (0.46f + t * 0.25f) * sc, 0), new Vector3(0.95f - t * 0.2f, 0.34f, 0.95f - t * 0.2f) * sc, t % 2 == 0 ? c1 : CandyCream);
                P(PrimitiveType.Sphere, "Cherry", g, new Vector3(0, 1.36f * sc, 0), Vector3.one * 0.15f * sc, Glossy("M_Cherry", new Color(1f, 0.45f, 0.6f)));
            }
            else
            {
                P(PrimitiveType.Cylinder, "Stick", g, new Vector3(0, 0.45f * sc, 0), new Vector3(0.05f, 0.45f, 0.05f) * sc, CandyCream);
                var face = Quaternion.FromToRotation(Vector3.up, ToCamera).eulerAngles;
                P(PrimitiveType.Cylinder, "Candy", g, new Vector3(0, 1.05f * sc, 0), new Vector3(0.72f, 0.06f, 0.72f) * sc, c1, face);
                P(PrimitiveType.Cylinder, "Swirl", g, new Vector3(0, 1.05f * sc, 0) + ToCamera * 0.012f * sc, new Vector3(0.48f, 0.065f, 0.48f) * sc, CandyCream, face);
                P(PrimitiveType.Cylinder, "Core", g, new Vector3(0, 1.05f * sc, 0) + ToCamera * 0.024f * sc, new Vector3(0.24f, 0.07f, 0.24f) * sc, c2, face);
            }
            // D-011: a click shakes the tree and candy fruit falls (the capsule is only for clicks; the walk area is cut out as before)
            Mongle.Village.FruitTree.Attach(g.gameObject, kind, Pastels[i % 5]);
            return g;
        }

        public static Transform PuffBush(Transform world, Vector3 p, float s, int i)
        {
            var g = Group(world, "PuffBush"); g.localPosition = p;
            P(PrimitiveType.Sphere, "Puff", g, new Vector3(0, 0.1f, 0) * s, new Vector3(0.45f, 0.3f, 0.4f) * s, Candy(i + 2));
            P(PrimitiveType.Sphere, "PuffB", g, new Vector3(0.16f, 0.12f, 0.05f) * s, new Vector3(0.28f, 0.22f, 0.26f) * s, Candy(i));
            return g;
        }

        public static void Gumdrop(Transform world, Vector3 p, float s, int i)
        {
            P(PrimitiveType.Sphere, "Gumdrop", world, p + new Vector3(0, 0.03f, 0), new Vector3(0.24f, 0.22f, 0.24f) * s, Glossy("M_Gumdrop" + (i % 5), Pastels[i % 5]));
        }

        // ---------------- sky and castle (the big wheel is a ride in the park now, D-006) ----------------
        /// <summary>Sky dressing above the park (D-006: everything inside the park): Blender hot-air balloons drifting high up, and clouds.</summary>
        public static void Far(Transform world, Vector3 center)
        {
            var root = Group(world, "Sky");
            // hot-air balloons modelled in Blender (three colour schemes), drifting lazily; positions are envelope centres
            var balloons = BackdropBuilder.HotAirBalloons();
            // high over the north half of the park, so they never come between the camera (south) and the toys
            var hot = new[] { center + new Vector3(-11f, 14f, -9f), center + new Vector3(12f, 15f, -10f), center + new Vector3(2f, 17f, -11f) };
            for (int i = 0; i < hot.Length && i < balloons.Length; i++)
            {
                float h = 3.0f + i * 0.4f;   // whole balloon height; the envelope centre sits about 70% up
                var b = (GameObject)PrefabUtility.InstantiatePrefab(balloons[i]); b.transform.SetParent(root, false);
                b.transform.localPosition = hot[i] - new Vector3(0, h * 0.7f, 0); b.transform.localScale = Vector3.one * h;
                b.transform.localEulerAngles = new Vector3(0, 40f * i, 0);
            }
            // clouds
            var cloud = M("MC_Cloud", new Color(1f, 0.99f, 1f));
            var rng = new System.Random(11);
            for (int i = 0; i < 8; i++)
            {
                float x = -16f + i * 4.6f + (float)rng.NextDouble() * 1.5f, z = -12f + (float)rng.NextDouble() * 9f;
                var g = Group(root, "Cloud"); g.localPosition = center + new Vector3(x, 17f + (float)rng.NextDouble() * 4f, z);
                for (int k = 0; k < 5; k++)
                    P(PrimitiveType.Sphere, "Puff", g, new Vector3((k - 2) * 0.7f, (k % 2) * 0.25f, ((float)rng.NextDouble() - 0.5f) * 0.6f), Vector3.one * (1.0f + (2 - Mathf.Abs(k - 2)) * 0.35f), cloud, null, false);
            }
        }

        /// <summary>The pastel castle (front faces +Z); returns its root.</summary>
        public static Transform Castle(Transform world, Vector3 pos, float scale)
        {
            var cone = ConeMesh(); var pennant = PennantMesh();
            var castle = Group(world, "Castle"); castle.localPosition = pos; castle.localScale = Vector3.one * scale;
            var wall = M("MS_CastleWall", new Color(1f, 0.93f, 0.95f)); var window = M("M_DreamGlow", new Color(1f, 0.94f, 0.76f), 0.3f, 0f, new Color(1.8f, 1.5f, 1.0f));
            P(PrimitiveType.Cube, "Wall", castle, new Vector3(0, 0.55f, 0), new Vector3(3.6f, 1.1f, 0.8f), wall);
            var towers = new[] { new Vector4(0f, 3.0f, 0.75f, 1), new Vector4(-1.7f, 2.1f, 0.55f, 0), new Vector4(1.7f, 2.1f, 0.55f, 2), new Vector4(-0.9f, 2.5f, 0.42f, 4), new Vector4(0.95f, 2.4f, 0.4f, 3) };
            foreach (var tw in towers)
            {
                var c = new Vector3(tw.x, 0, Mathf.Abs(tw.x) < 1.2f && tw.z < 0.5f ? 0.3f : 0f);
                P(PrimitiveType.Cylinder, "Tower", castle, c + new Vector3(0, tw.y * 0.5f, 0), new Vector3(tw.z, tw.y * 0.5f, tw.z), (int)tw.w % 2 == 0 ? wall : Candy((int)tw.w));
                PM(cone, "Roof", castle, c + new Vector3(0, tw.y, 0), new Vector3(tw.z * 0.62f, tw.z * 1.3f, tw.z * 0.62f), Candy((int)tw.w + 1), Quaternion.identity);
                P(PrimitiveType.Sphere, "Window", castle, c + new Vector3(0, tw.y * 0.7f, tw.z * 0.5f), new Vector3(0.12f, 0.2f, 0.04f), window, null, false);
                PM(pennant, "Flag", castle, c + new Vector3(0.12f, tw.y + tw.z * 1.3f + 0.12f, 0), new Vector3(0.22f, 0.16f, 1f), Candy((int)tw.w + 2), Quaternion.Euler(0f, 0f, 90f));
            }
            return castle;
        }
    }
}

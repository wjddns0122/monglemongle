using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mongle.Village;
using UnityEditor;
using UnityEngine;

namespace Mongle.EditorTools
{
    /// <summary>
    /// Set pieces modelled in Blender (docs/40_reports/shots/blender_map/scripts/*_export.py). Each export writes an FBX
    /// plus a material table; this builds URP materials from the table, remaps the FBX onto them and saves prefabs
    /// normalised to 1 unit tall with the pivot on the ground (the scene scales the root).
    /// </summary>
    public static class BackdropBuilder
    {
        const string Art = "Assets/_Project/Art/Backdrop/";
        const string PrefabDir = "Assets/_Project/Prefabs/Props/";

        [System.Serializable] class MatRow { public string name; public float[] color; public float smoothness; public float metallic; public float[] emission; }
        [System.Serializable] class MatTable { public MatRow[] materials; }

        /// <summary>Creates BD_* materials from the exported table and remaps the FBX onto them. Null when the export is missing.</summary>
        static Dictionary<string, MatRow> ImportWithTable(string fbx, string table)
        {
            var imp = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (imp == null || !File.Exists(table)) { Debug.LogWarning("[Backdrop] missing " + fbx + " or its material table"); return null; }
            var rows = new Dictionary<string, MatRow>();
            foreach (var row in JsonUtility.FromJson<MatTable>(File.ReadAllText(table)).materials)
            {
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), row.name), Mat("BD_" + row.name, row, Rgb(row.color)));
                rows[row.name] = row;
            }
            imp.SaveAndReimport();
            return rows;
        }

        static Color Rgb(float[] c) => new Color(c[0], c[1], c[2]);
        static Material Mat(string name, MatRow row, Color color)
        {
            Color? glow = row.emission != null && row.emission.Length == 3 ? Rgb(row.emission) : (Color?)null;
            return ContentBuilder.GetOrCreateMat(name, color, row.smoothness, row.metallic, glow);
        }

        /// <summary>Instantiates the FBX under a new root, 1 unit tall, standing on the root's origin.</summary>
        static GameObject Normalised(string fbx, string rootName)
        {
            var root = new GameObject(rootName);
            var model = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx), root.transform);
            model.name = "Model";
            var rs = model.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            model.transform.localScale = Vector3.one / b.size.y;
            model.transform.localPosition = new Vector3(0, -b.min.y / b.size.y, 0);
            foreach (var r in rs) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
            foreach (var c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            return root;
        }

        static GameObject Save(GameObject root, string name)
        {
            Directory.CreateDirectory(PrefabDir);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + name + ".prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        [MenuItem("Mongle/Build Backdrop")]
        public static void BuildAll() { Carousel(); HotAirBalloons(); Hospital(); WheelRide(); BalloonRide(); Playground(); Houses(); Fountain(); ClawMachine(); AssetDatabase.SaveAssets(); }

        /// <summary>The toy hospital (village repair shop): clickable through a box collider, front faces +Z.</summary>
        public static GameObject Hospital()
        {
            string fbx = Art + "ToyHospital.fbx";
            var rows = ImportWithTable(fbx, Art + "ToyHospital_materials.json"); if (rows == null) return null;
            var root = Normalised(fbx, "ToyHospital");
            var rs = root.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var box = root.AddComponent<BoxCollider>();            // body only (not the weather vane), for clicks
            box.center = new Vector3(b.center.x, 0.32f, b.center.z); box.size = new Vector3(b.size.x * 0.9f, 0.64f, b.size.z * 0.8f);
            root.AddComponent<HospitalBuilding>();
            return Save(root, "PF_Village_Hospital");
        }

        public static GameObject Carousel()
        {
            string fbx = Art + "Carousel.fbx";
            var rows = ImportWithTable(fbx, Art + "Carousel_materials.json"); if (rows == null) return null;
            var root = Normalised(fbx, "Backdrop_Carousel");
            root.AddComponent<CarouselMotion>();
            root.AddComponent<CarouselRide>(); // a ride in the park (D-006); the scene sets its boarding spot
            var click = root.AddComponent<CapsuleCollider>(); click.center = new Vector3(0, 0.5f, 0); click.radius = 0.52f; click.height = 1f;
            Debug.Log("[Backdrop] carousel prefab built with " + rows.Count + " materials");
            return Save(root, "PF_Backdrop_Carousel");
        }

        static Color Lin(float r, float g, float b) => new Color(Mathf.LinearToGammaSpace(r), Mathf.LinearToGammaSpace(g), Mathf.LinearToGammaSpace(b));

        /// <summary>Three colour schemes of the Blender hot-air balloon (same as the preview render), each slowly drifting.</summary>
        public static GameObject[] HotAirBalloons()
        {
            string fbx = Art + "HotAirBalloon.fbx";
            var rows = ImportWithTable(fbx, Art + "HotAirBalloon_materials.json"); if (rows == null) return new GameObject[0];
            var variants = new[]
            {
                new Dictionary<string, Color>(),
                new Dictionary<string, Color> { { "HB_GoreA", Lin(0.5f, 0.88f, 0.78f) }, { "HB_Accent", Lin(0.55f, 0.72f, 1f) }, { "HB_Rim", Lin(0.45f, 0.8f, 0.72f) } },
                new Dictionary<string, Color> { { "HB_GoreA", Lin(0.55f, 0.72f, 1f) }, { "HB_GoreB", Lin(1f, 0.92f, 0.6f) }, { "HB_Accent", Lin(1f, 0.6f, 0.77f) }, { "HB_Rim", Lin(0.55f, 0.7f, 1f) } },
            };
            var prefabs = new GameObject[variants.Length];
            for (int v = 0; v < variants.Length; v++)
            {
                var root = Normalised(fbx, "Backdrop_HotAir" + v);
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        string src = mats[i] != null ? mats[i].name.Replace("BD_", "") : null;
                        if (src != null && variants[v].TryGetValue(src, out var c)) mats[i] = Mat("BD_" + src + "_v" + v, rows[src], c);
                    }
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // far up in the sky: no shadow on the garden
                }
                root.AddComponent<FloatDrift>();
                prefabs[v] = Save(root, "PF_Backdrop_HotAir" + v);
            }
            Debug.Log("[Backdrop] hot-air balloon prefabs built: " + prefabs.Length);
            return prefabs;
        }

        // ---------------- park rides (D-006) ----------------
        public const float WheelScale = 8f;      // the shop's little wheel (0.62 m) grown into a 5 m ride
        public const float BalloonHeight = 5.5f; // basket about 0.76 m wide: room for two toys

        /// <summary>
        /// The big Ferris wheel ride, built from the furniture shop's wheel model: each gondola gets its own pivot at the
        /// hanging point (so it can stay upright), a lower wall with four posts under the roof (so the toy inside shows)
        /// and a seat on its floor. The WheelRide component turns the wheel itself.
        /// </summary>
        static Mesh cabinWall;
        /// <summary>An octagonal ring wall (outer + inner faces + top rim), base at y 0, flat shaded. Shared by every gondola.</summary>
        static Mesh CabinWallMesh(float rOut, float rIn, float h)
        {
            if (cabinWall != null) return cabinWall;
            var v = new List<Vector3>(); var tri = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f) { var t = b; b = d; d = t; }
                int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d); tri.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            const int n = 8; float off = Mathf.PI / n;
            var up = Vector3.up * h;
            for (int k = 0; k < n; k++)
            {
                float a0 = off + k * 2f * Mathf.PI / n, a1 = off + (k + 1) * 2f * Mathf.PI / n;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)); var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)); var dm = (d0 + d1).normalized;
                Quad(d0 * rOut, d0 * rOut + up, d1 * rOut + up, d1 * rOut, dm);          // outside
                Quad(d0 * rIn, d0 * rIn + up, d1 * rIn + up, d1 * rIn, -dm);            // inside
                Quad(d0 * rOut + up, d0 * rIn + up, d1 * rIn + up, d1 * rOut + up, Vector3.up); // rim
            }
            var m = new Mesh { name = "WheelCabinWall" }; m.SetVertices(v); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
            string path = Art + "WheelCabinWall.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);   // generated
            AssetDatabase.CreateAsset(m, path);
            return cabinWall = m;
        }

        public static GameObject WheelRide()
        {
            cabinWall = null;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "PF_Fur_Wheel.prefab");
            if (src == null) { Debug.LogWarning("[Backdrop] PF_Fur_Wheel missing (Mongle/Build Content first)"); return null; }
            var root = new GameObject("Ride_Wheel");
            var w = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(w, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            w.name = "Wheel"; w.transform.SetParent(root.transform, false); w.transform.localScale = Vector3.one * WheelScale;
            foreach (var c in w.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            foreach (var sp in w.GetComponentsInChildren<SpinPart>()) Object.DestroyImmediate(sp);
            foreach (var ku in w.GetComponentsInChildren<KeepUpright>()) Object.DestroyImmediate(ku); // the ride makes its own upright gondola pivots
            Transform spinner = null; foreach (var t in w.GetComponentsInChildren<Transform>()) if (t.name == "Spinner") spinner = t;
            Material cream = null; foreach (var r in w.GetComponentsInChildren<Renderer>()) if (r.sharedMaterial != null && r.sharedMaterial.name.Contains("WheelCream")) { cream = r.sharedMaterial; break; }
            // a gondola is four parts stacked straight up (cap, roof, body, floor): group parts that share x and sit close in y
            // (grouping by angle around the hub splits the gondolas at 45 degrees, their stack spans two angle slots)
            var groups = new List<List<Renderer>>();
            foreach (var r in spinner.GetComponentsInChildren<Renderer>())
            {
                if (r.sharedMaterial == null || !r.sharedMaterial.name.Contains("WheelGondola")) continue;
                var c = r.bounds.center; List<Renderer> home = null;
                foreach (var g in groups)
                {
                    var gc = g[0].bounds.center; float gy = 0f; foreach (var m in g) gy += m.bounds.center.y; gy /= g.Count;
                    if (Mathf.Abs(c.x - gc.x) < 0.03f * WheelScale && Mathf.Abs(c.z - gc.z) < 0.03f * WheelScale && Mathf.Abs(c.y - gy) < 0.1f * WheelScale) { home = g; break; }
                }
                if (home == null) groups.Add(home = new List<Renderer>());
                home.Add(r);
            }
            var hub = spinner.position;
            groups.Sort((a, b) => Mathf.Atan2(a[0].bounds.center.y - hub.y, a[0].bounds.center.x - hub.x).CompareTo(Mathf.Atan2(b[0].bounds.center.y - hub.y, b[0].bounds.center.x - hub.x)));
            var gondolas = new List<Transform>(); var seats = new List<Transform>();
            foreach (var parts in groups)
            {
                if (parts.Count < 4) Debug.LogWarning("[Backdrop] wheel gondola with " + parts.Count + " parts");
                parts.Sort((a, b) => b.bounds.center.y.CompareTo(a.bounds.center.y)); // cap, roof, body, base
                var cap = parts[0]; var roof = parts[1]; var body = parts[parts.Count - 2]; var floor = parts[parts.Count - 1];
                var pivot = new GameObject("Gondola" + gondolas.Count).transform; pivot.SetParent(spinner, false);
                pivot.position = new Vector3(cap.bounds.center.x, cap.bounds.max.y, cap.bounds.center.z);
                pivot.rotation = root.transform.rotation;
                foreach (var p in parts) p.transform.SetParent(pivot, true);
                // lower the closed body to a little wall standing on the floor
                var before = body.bounds; var bt = body.transform;
                var upLocal = bt.InverseTransformDirection(Vector3.up); int ax = Mathf.Abs(upLocal.x) > Mathf.Abs(upLocal.y) ? (Mathf.Abs(upLocal.x) > Mathf.Abs(upLocal.z) ? 0 : 2) : (Mathf.Abs(upLocal.y) > Mathf.Abs(upLocal.z) ? 1 : 2);
                var ls = bt.localScale; ls[ax] *= 0.42f; bt.localScale = ls;
                bt.position += Vector3.up * (before.min.y - body.bounds.min.y);
                // the cabin was only ~0.29 m from floor to roof: a 0.45 m toy stuck through the roof. Hang the floor and the
                // little wall lower (longer posts) so a sitting toy fits inside, under the roof
                const float CabinDrop = 0.26f;
                floor.transform.position += Vector3.down * CabinDrop; bt.position += Vector3.down * CabinDrop;
                foreach (var p in parts) if (roof.sharedMaterial != null) p.sharedMaterial = roof.sharedMaterial;   // one colour per gondola
                // the lowered body was a solid block (an empty gondola looked like a pedestal and a rider sank into it):
                // swap it for a hollow wall around the floor, so the cabin is a little cup the rider sits inside
                float baseY = floor.bounds.max.y, wallH = Mathf.Max(0.12f, body.bounds.max.y - baseY), rOut = body.bounds.extents.x;
                var wallMat = body.sharedMaterial; Object.DestroyImmediate(body.gameObject);
                var wall = new GameObject("CabinWall").transform; wall.SetParent(pivot, false);
                wall.position = new Vector3(floor.bounds.center.x, baseY, floor.bounds.center.z); wall.rotation = root.transform.rotation;
                var wl = pivot.lossyScale; wall.localScale = new Vector3(1f / wl.x, 1f / wl.y, 1f / wl.z);
                wall.gameObject.AddComponent<MeshFilter>().sharedMesh = CabinWallMesh(rOut, rOut - 0.035f, wallH);
                wall.gameObject.AddComponent<MeshRenderer>().sharedMaterial = wallMat;
                float wallTop = baseY + wallH, roofBottom = roof.bounds.min.y;
                var mid = floor.bounds.center; float rad = before.extents.x * 0.92f;
                for (int k = 0; k < 4; k++)
                {
                    float a = Mathf.PI / 4f + k * Mathf.PI / 2f;
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder); post.name = "Post"; Object.DestroyImmediate(post.GetComponent<Collider>());
                    post.transform.SetParent(pivot, false);
                    float h = roofBottom - wallTop + 0.02f;
                    post.transform.position = new Vector3(mid.x + Mathf.Cos(a) * rad, (wallTop + roofBottom) * 0.5f, mid.z + Mathf.Sin(a) * rad);
                    var lossy = pivot.lossyScale; post.transform.localScale = new Vector3(0.045f / lossy.x, h * 0.5f / lossy.y, 0.045f / lossy.z);
                    if (cream != null) post.GetComponent<Renderer>().sharedMaterial = cream;
                }
                var seat = new GameObject("Seat").transform; seat.SetParent(pivot, false);
                seat.position = new Vector3(mid.x, floor.bounds.max.y, mid.z);
                gondolas.Add(pivot); seats.Add(seat);
            }
            var ride = root.AddComponent<WheelRide>();
            ride.spinner = spinner; ride.gondolas = gondolas.ToArray(); ride.seats = seats.ToArray();
            var rs = root.GetComponentsInChildren<Renderer>(); var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
            var box = root.AddComponent<BoxCollider>(); box.center = bb.center; box.size = new Vector3(bb.size.x, bb.size.y, Mathf.Max(0.8f, bb.size.z));
            foreach (var r in rs) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
            Debug.Log("[Backdrop] wheel ride: " + gondolas.Count + " gondolas (" + string.Join("/", groups.ConvertAll(g => g.Count.ToString())) + " parts), size " + bb.size);
            return Save(root, "PF_Ride_Wheel");
        }

        /// <summary>
        /// A tethered hot-air balloon ride: the Blender balloon on a "Lift" node that rises and turns, two standing spots on
        /// a raised floor inside the basket, and two ropes down to stakes in the ground.
        /// </summary>
        /// <summary>
        /// The Blender basket is closed by a flat lid (BD_HB_BasketFloor) at its rim, which hid the riders (only antennas
        /// poked through). The ride balloon gets a copy of the mesh without that lid, an inward-facing liner wall and a
        /// raised floor at <paramref name="floorY"/> (lift space) for the riders to stand on.
        /// </summary>
        static void OpenBasket(GameObject balloon, float floorY)
        {
            var mr = System.Array.Find(balloon.GetComponentsInChildren<MeshRenderer>(true), r => r.name == "Model");
            var mf = mr != null ? mr.GetComponent<MeshFilter>() : null; if (mf == null) return;
            int lid = System.Array.FindIndex(mr.sharedMaterials, m => m != null && m.name.Contains("BasketFloor"));
            Material liner = System.Array.Find(mr.sharedMaterials, m => m != null && m.name.Contains("BasketLight")) ?? mr.sharedMaterials[0];
            if (lid >= 0)
            {
                string path = Art + "HB_Balloon_OpenBasket.asset";
                var open = Object.Instantiate(mf.sharedMesh); open.name = "HB_Balloon_OpenBasket";
                open.SetTriangles(new int[0], lid, false);
                var old = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (old != null) AssetDatabase.DeleteAsset(path); // generated
                AssetDatabase.CreateAsset(open, path); mf.sharedMesh = open;
            }
            // liner: an open cylinder facing inward plus the raised floor, just inside the woven wall (basket radius ~0.385 m)
            const int seg = 28; const float r = 0.355f, top = 0.62f;
            var v = new System.Collections.Generic.List<Vector3>(); var tri = new System.Collections.Generic.List<int>(); var n = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg; var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(d * r + Vector3.up * (floorY - 0.02f)); n.Add(-d);
                v.Add(d * r + Vector3.up * top); n.Add(-d);
            }
            for (int i = 0; i < seg; i++) { int k = i * 2; tri.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 }); }
            int c0 = v.Count; v.Add(Vector3.up * floorY); n.Add(Vector3.up);
            for (int i = 0; i <= seg; i++) { float a = i * Mathf.PI * 2f / seg; v.Add(new Vector3(Mathf.Cos(a) * r, floorY, Mathf.Sin(a) * r)); n.Add(Vector3.up); }
            for (int i = 0; i < seg; i++) tri.AddRange(new[] { c0, c0 + 2 + i, c0 + 1 + i });
            var m2 = new Mesh { name = "HB_BasketLiner" }; m2.SetVertices(v); m2.SetNormals(n); m2.SetTriangles(tri, 0); m2.RecalculateBounds();
            string lpath = Art + "HB_BasketLiner.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(lpath) != null) AssetDatabase.DeleteAsset(lpath);
            AssetDatabase.CreateAsset(m2, lpath);
            var go = new GameObject("BasketLiner"); go.transform.SetParent(balloon.transform.parent, false);   // the Lift
            go.AddComponent<MeshFilter>().sharedMesh = m2;
            var lr = go.AddComponent<MeshRenderer>(); lr.sharedMaterial = liner; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static Transform Deep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = Deep(c, name); if (r != null) return r; }
            return null;
        }

        static Bounds WorldBounds(params Transform[] parts)
        {
            bool any = false; var b = new Bounds();
            foreach (var p in parts)
            {
                if (p == null) continue;
                foreach (var r in p.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            }
            return b;
        }

        /// <summary>
        /// The pastel playground (D-011, playground.py): real metres (no normalising, its nodes are in metres), front = +Z.
        /// Three rides sit on children placed at their equipment: swings (board from behind the bar), the pony seesaw (board
        /// in front of the sand) and the slides (board at the foot of the stairs). PlaygroundLayout records the ground it blocks.
        /// </summary>
        public static GameObject Playground()
        {
            string fbx = Art + "Playground.fbx";
            var rows = ImportWithTable(fbx, Art + "Playground_materials.json"); if (rows == null) return null;
            var root = new GameObject("Ride_Playground");
            var model = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx), root.transform);
            model.name = "Model"; model.transform.localPosition = Vector3.zero;
            foreach (var r in model.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
            foreach (var c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var m = model.transform;
            Transform N(string n) { var t = Deep(m, n); if (t == null) Debug.LogWarning("[Playground] missing node " + n); return t; }
            Transform Child(string name, Vector3 world)
            {
                var t = new GameObject(name).transform; t.SetParent(root.transform, false);
                t.position = new Vector3(world.x, 0f, world.z); return t;
            }
            BoxCollider Click(Transform owner, Bounds b)
            {
                var c = owner.gameObject.AddComponent<BoxCollider>();
                c.center = owner.InverseTransformPoint(b.center); c.size = b.size; return c;
            }
            Vector3 Local(Transform ride, Vector3 world) { var l = world - ride.position; l.y = 0f; return l; }

            // ---- swings: toys climb in from behind the bar (the open slide runs in front of them) ----
            var s1 = N("PG_Swing_1"); var s2 = N("PG_Swing_2");
            var frame = WorldBounds(N("PG_SwingFrame"), s1, s2);
            var swings = Child("Swings", (s1.position + s2.position) * 0.5f);
            var swing = swings.gameObject.AddComponent<SwingRide>();
            swing.swings = new[] { s1, s2 }; swing.seatPoints = new[] { N("PG_SwingSeat_1"), N("PG_SwingSeat_2") }; swing.front = root.transform;
            swing.boardLocal = Local(swings, new Vector3(swings.position.x, 0f, frame.min.z - 0.3f));
            Click(swings, frame);

            // ---- pony seesaw: board in front of its sand patch ----
            var beam = N("PG_Seesaw_Beam");
            var saw = WorldBounds(N("PG_SeesawBase"), beam);
            var seesawT = Child("Seesaw", beam.position);
            var seesaw = seesawT.gameObject.AddComponent<SeesawRide>();
            seesaw.beam = beam; seesaw.seatPoints = new[] { N("PG_SeesawSeat_A"), N("PG_SeesawSeat_B") };
            seesaw.boardLocal = Local(seesawT, new Vector3(beam.position.x, 0f, saw.max.z + 0.35f));
            Click(seesawT, saw);

            // ---- slides: up the stairs, across the deck, down the open slide or the tube ----
            var deck = N("PG_Deck");
            var slidesT = Child("Slides", deck.position);
            var slide = slidesT.gameObject.AddComponent<SlideRide>();
            slide.stairBottom = N("PG_Stair_Bottom"); slide.stairTop = N("PG_Stair_Top");
            Transform[] Path(string prefix) { var l = new System.Collections.Generic.List<Transform>(); for (int i = 0; i < 40; i++) { var t = Deep(m, prefix + i.ToString("00")); if (t == null) break; l.Add(t); } return l.ToArray(); }
            slide.slidePath = Path("PG_Slide_"); slide.tubePath = Path("PG_Tube_");
            var tower = WorldBounds(N("PG_Tower"), N("PG_TubeSlide"));
            // the foot of the stairs, a step further out and off to the side away from the seesaw
            var foot = slide.stairBottom.position;
            float side = Mathf.Sign(foot.x - saw.center.x); if (side == 0f) side = 1f;
            var boardAt = new Vector3(Mathf.Abs(foot.x - saw.center.x) > saw.extents.x + 0.25f ? foot.x : saw.center.x + side * (saw.extents.x + 0.3f), 0f, foot.z + 0.25f);
            slide.boardLocal = Local(slidesT, boardAt);
            Click(slidesT, WorldBounds(N("PG_Tower"), N("PG_TubeSlide"), N("PG_Slide")));

            // ---- ground it blocks (root space; the boarding spots stay outside) ----
            var layout = root.AddComponent<PlaygroundLayout>();
            var boxes = new[] { tower, WorldBounds(N("PG_Slide")), frame, saw };
            layout.boxCenters = new Vector3[boxes.Length]; layout.boxSizes = new Vector3[boxes.Length];
            for (int i = 0; i < boxes.Length; i++) { layout.boxCenters[i] = new Vector3(boxes[i].center.x, 0f, boxes[i].center.z); layout.boxSizes[i] = new Vector3(boxes[i].size.x, 1f, boxes[i].size.z); }
            Debug.Log("[Backdrop] playground: swings board " + swing.boardLocal + ", seesaw board " + seesaw.boardLocal + ", slide board " + slide.boardLocal
                      + ", " + slide.slidePath.Length + "/" + slide.tubePath.Length + " path points, " + rows.Count + " materials");
            return Save(root, "PF_Ride_Playground");
        }

        // ---------------- D-012: friends' gingerbread houses ----------------
        /// <summary>
        /// One prefab per house variant in CandyHouses.fbx (HS_House_01..08), each standing on its own origin, front door +Z:
        /// HouseView (door / top spots, roof tint), a click box and a carving NavMeshObstacle (houses appear while playing).
        /// Also puts them into the content catalog.
        /// </summary>
        public static GameObject[] Houses()
        {
            string fbx = Art + "CandyHouses.fbx";
            var rows = ImportWithTable(fbx, Art + "CandyHouses_materials.json"); if (rows == null) return new GameObject[0];
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var outList = new List<GameObject>();
            for (int i = 1; i <= HouseLots.Variants; i++)
            {
                string id = i.ToString("00");
                var inst = (GameObject)Object.Instantiate(src);
                var house = Deep(inst.transform, "HS_House_" + id);
                if (house == null) { Object.DestroyImmediate(inst); Debug.LogWarning("[Backdrop] no HS_House_" + id); continue; }
                var root = new GameObject("House_" + id);
                var model = house; model.SetParent(root.transform, true);
                var origin = model.position; origin.y = 0f;
                model.position -= origin;           // the variant's own ground centre becomes the prefab origin
                Object.DestroyImmediate(inst);
                foreach (var r in model.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
                foreach (var c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                var hv = root.AddComponent<HouseView>();
                hv.door = Deep(model, "HS_Door_" + id); hv.top = Deep(model, "HS_Top_" + id);
                var b = WorldBounds(model);
                var box = root.AddComponent<BoxCollider>(); box.center = b.center; box.size = b.size;
                var obs = root.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                obs.shape = UnityEngine.AI.NavMeshObstacleShape.Box; obs.carving = true; obs.carveOnlyStationary = true;
                obs.center = new Vector3(b.center.x, 0.5f, b.center.z); obs.size = new Vector3(b.size.x * 0.92f, 1f, b.size.z * 0.92f);
                outList.Add(Save(root, "PF_House_" + id));
            }
            var cat = Mongle.Data.ContentCatalog.Load();
            if (cat != null) { cat.houses = outList.ToArray(); EditorUtility.SetDirty(cat); }
            Debug.Log("[Backdrop] houses: " + outList.Count + " variants");
            return outList.ToArray();
        }

        // ---------------- D-014: the capsule claw machine ----------------
        /// <summary>The pastel claw machine (claw_machine.py), real metres, front +Z: nodes wired into ClawMachine, glass see-through.</summary>
        public static GameObject ClawMachine()
        {
            string fbx = Art + "ClawMachine.fbx";
            var rows = ImportWithTable(fbx, Art + "ClawMachine_materials.json"); if (rows == null) return null;
            var root = new GameObject("ClawMachine");
            var model = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx), root.transform);
            model.name = "Model"; model.transform.localPosition = Vector3.zero;
            foreach (var r in model.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
            foreach (var c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var m = model.transform;
            var glass = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/BD_CM_Glass.mat");
            if (glass != null)
            {
                glass.SetColor("_BaseColor", new Color(0.82f, 0.93f, 1f, 0.16f)); glass.SetFloat("_Smoothness", 0.95f);
                glass.SetFloat("_Surface", 1f); glass.SetFloat("_Blend", 0f);
                glass.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); glass.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); glass.SetFloat("_ZWrite", 0f);
                glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); glass.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                EditorUtility.SetDirty(glass);
            }
            // the FBX gives every part as a direct child (nested parts came out with the wrong axes): group them here, keeping world poses
            var clawNode = Deep(m, "CM_Claw");
            var cmRoot = Deep(m, "CM_Root") ?? m;
            var pileNode = new GameObject("CM_CapsulePile").transform; pileNode.SetParent(cmRoot, false);
            foreach (var t in m.GetComponentsInChildren<Transform>(true).Where(t => t.parent == cmRoot && t.name.StartsWith("CM_Cap_") && t.name.Length > 7 && char.IsDigit(t.name[7])).ToList()) t.SetParent(pileNode, true);
            if (clawNode != null)
                for (int i = 1; i <= 3; i++)
                {
                    var pr = Deep(m, "CM_Prong_" + i); var tp = Deep(m, "CM_ProngTip_" + i);
                    if (pr != null) pr.SetParent(clawNode, true);
                    if (tp != null && pr != null) tp.SetParent(pr, true);
                }
            var cm = root.AddComponent<global::Mongle.Village.ClawMachine>();
            cm.claw = Deep(m, "CM_Claw"); cm.chamberA = Deep(m, "CM_ChamberMin"); cm.chamberB = Deep(m, "CM_ChamberMax");
            cm.prizeFloor = Deep(m, "CM_PrizeFloor"); cm.chute = Deep(m, "CM_Chute"); cm.chuteOut = Deep(m, "CM_ChuteOut");
            cm.chuteDrop = Deep(m, "CM_ChuteDrop"); cm.playerSpot = Deep(m, "CM_PlayerSpot"); cm.top = Deep(m, "CM_Top"); cm.joystick = Deep(m, "CM_Joystick");
            var caps = Deep(m, "CM_Capsules"); cm.capsules = caps != null ? caps.GetComponentInChildren<Renderer>() : null;
            cm.markerMat = ContentBuilder.GetOrCreateMat("MS_ClawMarker", new Color(1f, 0.93f, 0.4f), 0.6f, 0f, new Color(2.2f, 1.7f, 0.5f));
            var b = WorldBounds(Deep(m, "CM_Body"), Deep(m, "CM_Glass"));
            var box = root.AddComponent<BoxCollider>(); box.center = root.transform.InverseTransformPoint(b.center); box.size = b.size;
            Debug.Log("[Backdrop] claw machine: size " + b.size.ToString("F2") + (cm.claw != null && cm.chamberA != null && cm.joystick != null ? "" : " (MISSING NODES)"));
            return Save(root, "PF_Prop_ClawMachine");
        }

        // ---------------- D-012: the cake fountain ----------------
        /// <summary>
        /// See-through water (D-012) with soft lighter streaks that FountainRide slides down every stream (UV V = along the flow).
        /// Depth writing stays on so the inner side of a stream never shows through its front (the cut-up look the user saw).
        /// </summary>
        static Material WaterMat()
        {
            string path = "Assets/_Project/Art/Materials/M_FountainWater.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
            m.shader = lit;
            m.SetTexture("_BaseMap", WaterStreaks()); m.SetTextureScale("_BaseMap", new Vector2(1f, 1f));
            m.SetColor("_BaseColor", new Color(0.66f, 0.88f, 1f, 0.78f)); m.SetFloat("_Smoothness", 0.88f); m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetFloat("_Cull", 2f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 1f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.1f, 0.16f, 0.22f));
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>A tiling streak texture: mostly clear blue-white with soft brighter threads running along V.</summary>
        static Texture2D WaterStreaks()
        {
            string path = "Assets/_Project/Art/Materials/T_WaterStreaks.png";
            const int W = 64, H = 256;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var rng = new System.Random(1012);
            var threads = new (float u, float w, float ph, float amp)[9];
            for (int i = 0; i < threads.Length; i++) threads[i] = ((float)rng.NextDouble(), 0.03f + 0.05f * (float)rng.NextDouble(), (float)rng.NextDouble() * 6.28f, 0.25f + 0.5f * (float)rng.NextDouble());
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = y / (float)H, glow = 0f;
                    foreach (var th in threads)
                    {
                        float du = Mathf.Abs(Mathf.Repeat(u - th.u + 0.5f, 1f) - 0.5f);
                        float along = 0.5f + 0.5f * Mathf.Sin(v * 6.2832f * 2f + th.ph);              // brighter and dimmer along the flow
                        glow += Mathf.Exp(-du * du / (th.w * th.w)) * th.amp * along;
                    }
                    glow = Mathf.Clamp01(glow);
                    tex.SetPixel(x, y, new Color(0.86f + 0.14f * glow, 0.94f + 0.06f * glow, 1f, 0.72f + 0.28f * glow));
                }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Repeat; imp.alphaIsTransparency = true; imp.mipmapEnabled = true; imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material DropMat()
        {
            string path = "Assets/_Project/Art/Materials/M_FountainDrop.mat";
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh; m.SetColor("_BaseColor", new Color(0.88f, 0.96f, 1f, 0.85f));
            m.SetFloat("_Surface", 1f); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            var particle = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_Particle.mat");
            if (particle != null && particle.mainTexture != null) m.SetTexture("_BaseMap", particle.mainTexture);
            EditorUtility.SetDirty(m); return m;
        }

        /// <summary>The cake fountain (cake_fountain.py), real metres: water made see-through and animated, toys sit on the rim.</summary>
        public static GameObject Fountain()
        {
            string fbx = Art + "CakeFountain.fbx";
            var rows = ImportWithTable(fbx, Art + "CakeFountain_materials.json"); if (rows == null) return null;
            var root = new GameObject("Fountain");
            var model = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx), root.transform);
            model.name = "Model"; model.transform.localPosition = Vector3.zero;
            foreach (var r in model.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
            foreach (var c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var m = model.transform;
            var water = WaterMat();
            var waters = m.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("FT_Jet") || t.name.StartsWith("FT_Fall") || t.name.StartsWith("FT_WaterPool") || t.name.StartsWith("FT_Splash")).ToList();
            foreach (var w in waters) foreach (var r in w.GetComponentsInChildren<Renderer>()) { r.sharedMaterials = r.sharedMaterials.Select(_ => water).ToArray(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            var ride = root.AddComponent<FountainRide>();
            ride.jet = Deep(m, "FT_Jet"); ride.pool = Deep(m, "FT_WaterPool");
            ride.falls = m.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("FT_Fall_")).ToArray();
            ride.splashes = m.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("FT_Splash")).ToArray();
            ride.sitSpots = Enumerable.Range(1, 6).Select(i => Deep(m, "FT_SitSpot_" + i)).Where(t => t != null).ToArray();
            var b = WorldBounds(m);
            ride.boardLocal = new Vector3(b.extents.x * 1.2f + 0.3f, 0f, 0f); // east side, where the path from the hub ends (the train passes the south side); outside the round walk hole
            var click = root.AddComponent<CapsuleCollider>(); click.center = new Vector3(0f, b.size.y * 0.5f, 0f); click.radius = Mathf.Max(b.extents.x, b.extents.z) * 0.8f; click.height = b.size.y;
            // a few sparkling drops round the top of the jet
            if (ride.jet != null)
            {
                var ps = new GameObject("Drops").AddComponent<ParticleSystem>(); ps.transform.SetParent(root.transform, false);
                var jb = WorldBounds(ride.jet); ps.transform.position = new Vector3(0f, jb.max.y - 0.05f, 0f);
                var main = ps.main; main.startLifetime = 1.1f; main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.0f); main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
                main.startColor = new Color(0.85f, 0.95f, 1f, 0.9f); main.gravityModifier = 0.6f; main.maxParticles = 80; main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = ps.emission; em.rateOverTime = 26f;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 28f; sh.radius = 0.05f; sh.rotation = new Vector3(-90f, 0f, 0f);
                var pr = ps.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = DropMat(); pr.renderMode = ParticleSystemRenderMode.Billboard;
            }
            Debug.Log("[Backdrop] fountain: " + ride.sitSpots.Length + " rim spots, " + ride.falls.Length + " falls, size " + b.size.ToString("F2"));
            return Save(root, "PF_Ride_Fountain");
        }

        // ---------------- D-012: the strawberry cake train on its loop round the park ----------------
        const float Gauge = 0.42f, RailTop = 0.045f, SleeperStep = 0.36f;

        /// <summary>
        /// The cake train (cake_train.py) as a ride sitting at its station; the loop (world points) becomes track: a frosting bed,
        /// biscuit sleepers (TR_Sleeper) and two rails. The wagons follow the loco at the spacing they have in the FBX.
        /// </summary>
        public static GameObject TrainRide(Vector3 station, Vector3[] loopWorld, Vector3 stopNear)
        {
            string fbx = Art + "CakeTrain.fbx";
            var rows = ImportWithTable(fbx, Art + "CakeTrain_materials.json"); if (rows == null) return null;
            var root = new GameObject("Ride_Train");
            root.transform.position = station;
            var model = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
            foreach (var r in model.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
            foreach (var c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var m = model.transform;
            var names = new[] { "TR_Loco", "TR_Car_1", "TR_Car_2", "TR_Car_3", "TR_Car_4" };
            var pieces = names.Select(n => Deep(m, n)).Where(t => t != null).ToArray();
            if (pieces.Length == 0) { Debug.LogWarning("[Backdrop] CakeTrain.fbx has no TR_Loco"); Object.DestroyImmediate(model); Object.DestroyImmediate(root); return null; }
            var loco0 = pieces[0].position; var fwd0 = pieces[0].forward;
            var behind = pieces.Select(p => Vector3.Dot(loco0 - p.position, fwd0)).ToArray();
            var seats = new[] { "TR_Seat_Loco", "TR_Seat_1", "TR_Seat_2", "TR_Seat_3", "TR_Seat_4" }.Select(n => Deep(m, n)).ToArray();
            var wheels = m.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("TR_Wheel_")).ToArray();
            var smoke = Deep(m, "TR_Smoke");
            var sleeperRoot = Deep(m, "TR_Sleeper");
            var sleeperSrc = sleeperRoot != null ? sleeperRoot.GetComponentInChildren<MeshFilter>()?.transform : null;
            var sleeperMesh = sleeperSrc != null ? sleeperSrc.GetComponent<MeshFilter>().sharedMesh : null;
            var sleeperMats = sleeperSrc != null ? sleeperSrc.GetComponent<MeshRenderer>().sharedMaterials : null;
            var sleeperRot = sleeperSrc != null ? sleeperSrc.rotation : Quaternion.identity; var sleeperScale = sleeperSrc != null ? sleeperSrc.lossyScale : Vector3.one;
            foreach (var p in pieces)
            {
                p.SetParent(root.transform, true);
                var b = WorldBounds(p);
                var click = p.gameObject.AddComponent<BoxCollider>(); click.center = p.InverseTransformPoint(b.center); click.size = p.InverseTransformVector(b.size); click.size = new Vector3(Mathf.Abs(click.size.x), Mathf.Abs(click.size.y), Mathf.Abs(click.size.z));
            }
            Object.DestroyImmediate(model);   // the track kit and anything else left over

            var ride = root.AddComponent<TrainRide>();
            ride.pieces = pieces; ride.pieceBehind = behind; ride.seats = seats; ride.wheels = wheels; ride.smoke = smoke;
            ride.loopLocal = loopWorld.Select(p => p - station).ToArray();
            var loop = new LoopPath(loopWorld);
            ride.stationAt = loop.Nearest(stopNear);
            ride.boardLocal = Vector3.zero;
            ride.wheelRadii = wheels.Select(w => Mathf.Max(0.04f, WorldBounds(w).extents.y)).ToArray();
            ride.wheelRadius = ride.wheelRadii.Length > 0 ? ride.wheelRadii.Average() : 0.1f;

            // ---- track ----
            var track = new GameObject("Track").transform; track.SetParent(root.transform, false);
            var railMat = Mat("BD_TR_Rail", rows.TryGetValue("TR_Rail", out var rr) ? rr : new MatRow { color = new[] { 0.78f, 0.78f, 0.84f }, smoothness = 0.6f }, rows.ContainsKey("TR_Rail") ? Rgb(rows["TR_Rail"].color) : new Color(0.78f, 0.78f, 0.84f));
            var bedMat = ContentBuilder.GetOrCreateMat("MS_TrackBed", new Color(0.72f, 0.9f, 0.93f), 0.05f, 0f, null);
            var pts = loopWorld.Select(p => p - station).ToArray();
            Vector3 Side(int i) { var d = pts[(i + 1) % pts.Length] - pts[(i - 1 + pts.Length) % pts.Length]; d.y = 0f; return Vector3.Cross(Vector3.up, d.normalized); }
            // frosting bed: a flat ribbon with soft raised edges
            {
                var vs = new System.Collections.Generic.List<Vector3>(); var ts = new System.Collections.Generic.List<int>();
                float[] prof = { -0.48f, -0.42f, 0.42f, 0.48f }; float[] hgt = { 0.002f, 0.012f, 0.012f, 0.002f };
                for (int i = 0; i < pts.Length; i++) { var s = Side(i); for (int k = 0; k < prof.Length; k++) vs.Add(pts[i] + s * prof[k] + Vector3.up * hgt[k]); }
                int w = prof.Length;
                for (int i = 0; i < pts.Length; i++)
                {
                    int a = i * w, b2 = ((i + 1) % pts.Length) * w;
                    for (int k = 0; k < w - 1; k++) { ts.Add(a + k); ts.Add(b2 + k); ts.Add(a + k + 1); ts.Add(a + k + 1); ts.Add(b2 + k); ts.Add(b2 + k + 1); }
                }
                AddMesh(track, "TrackBed", vs, ts, bedMat, false);
            }
            // rails: two rounded tubes
            {
                var vs = new System.Collections.Generic.List<Vector3>(); var ts = new System.Collections.Generic.List<int>(); const int seg = 8; const float rad = 0.014f;
                foreach (float off in new[] { -Gauge / 2f, Gauge / 2f })
                {
                    int start = vs.Count;
                    for (int i = 0; i < pts.Length; i++)
                    {
                        var s = Side(i); var c = pts[i] + s * off + Vector3.up * (RailTop - rad);
                        for (int k = 0; k < seg; k++) { float a = k * Mathf.PI * 2f / seg; vs.Add(c + s * (Mathf.Cos(a) * rad) + Vector3.up * (Mathf.Sin(a) * rad * 1.3f)); }
                    }
                    for (int i = 0; i < pts.Length; i++)
                    {
                        int a = start + i * seg, b2 = start + ((i + 1) % pts.Length) * seg;
                        for (int k = 0; k < seg; k++) { int k2 = (k + 1) % seg; ts.Add(a + k); ts.Add(a + k2); ts.Add(b2 + k); ts.Add(a + k2); ts.Add(b2 + k2); ts.Add(b2 + k); }
                    }
                }
                AddMesh(track, "TrackRails", vs, ts, railMat, true);
            }
            // biscuit sleepers, every SleeperStep metres along the loop, combined into one mesh per material
            if (sleeperMesh != null)
            {
                var localLoop = new LoopPath(pts);
                var placed = new System.Collections.Generic.List<Matrix4x4>();
                for (float d = 0f; d < localLoop.Length - SleeperStep * 0.5f; d += SleeperStep)
                {
                    localLoop.At(d, out var p, out var dir); dir.y = 0f;
                    // the sleeper lies across the track: its long side (Blender X) along the side vector
                    var rot = Quaternion.LookRotation(dir.normalized, Vector3.up) * sleeperRot;
                    placed.Add(Matrix4x4.TRS(p + Vector3.up * 0.004f, rot, sleeperScale));
                }
                // one merged mesh per material, then those as the submeshes of the final mesh (one submesh per material)
                var perMat = new CombineInstance[sleeperMesh.subMeshCount];
                for (int k = 0; k < sleeperMesh.subMeshCount; k++)
                {
                    var part = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    part.CombineMeshes(placed.Select(mx => new CombineInstance { mesh = sleeperMesh, subMeshIndex = k, transform = mx }).ToArray(), true, true);
                    perMat[k] = new CombineInstance { mesh = part, transform = Matrix4x4.identity };
                }
                var mesh = new Mesh { name = "TrackSleepers", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(perMat, false, false);
                mesh = SaveMeshAsset(mesh, "TrackSleepers");
                var g = new GameObject("TrackSleepers"); g.transform.SetParent(track, false);
                g.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterials = sleeperMats; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // station platform: cookie tiles beside the track where the train stops
            {
                loop.At(ride.stationAt, out var sp, out var sdir); sdir.y = 0f;
                var len = pieces.Length > 0 ? behind.Max() + 1.0f : 4f;
                var tileMat = rows.ContainsKey("TR_Biscuit") ? Mat("BD_TR_Biscuit", rows["TR_Biscuit"], Rgb(rows["TR_Biscuit"].color)) : bedMat;
                var plat = new GameObject("Platform").transform; plat.SetParent(root.transform, false);
                var side = Vector3.Cross(Vector3.up, sdir.normalized); if (Vector3.Dot(side, station - sp) < 0f) side = -side;
                int n = Mathf.CeilToInt(len / 0.5f);
                for (int i = 0; i < n; i++)
                {
                    var tile = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(tile.GetComponent<Collider>());
                    tile.name = "Cookie"; tile.transform.SetParent(plat, true);
                    tile.transform.position = sp - sdir.normalized * (i * 0.5f - 0.6f) + side * 0.82f + Vector3.up * 0.008f;
                    tile.transform.localScale = new Vector3(0.46f, 0.008f, 0.46f);
                    tile.GetComponent<Renderer>().sharedMaterial = tileMat;
                }
            }
            Debug.Log("[Backdrop] cake train: " + pieces.Length + " pieces, " + wheels.Length + " wheels, loop " + loop.Length.ToString("F1") + " m, stops at " + ride.stationAt.ToString("F1"));
            return Save(root, "PF_Ride_Train");
        }

        static void AddMesh(Transform parent, string name, System.Collections.Generic.List<Vector3> vs, System.Collections.Generic.List<int> ts, Material mat, bool shadows)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vs); mesh.SetTriangles(ts, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            mesh = SaveMeshAsset(mesh, name);
            var g = new GameObject(name); g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Stores a generated mesh as an asset (reusing the old asset so prefabs keep their reference) and returns the stored mesh.</summary>
        static Mesh SaveMeshAsset(Mesh mesh, string name)
        {
            string path = Art + name + ".asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            old.Clear(); EditorUtility.CopySerialized(mesh, old); EditorUtility.SetDirty(old);
            return old;
        }

        public static GameObject BalloonRide()
        {
            string fbx = Art + "HotAirBalloon.fbx";
            var rows = ImportWithTable(fbx, Art + "HotAirBalloon_materials.json"); if (rows == null) return null;
            var root = new GameObject("Ride_Balloon");
            var lift = new GameObject("Lift").transform; lift.SetParent(root.transform, false);
            var b = Normalised(fbx, "Balloon"); b.transform.SetParent(lift, false); b.transform.localScale = Vector3.one * BalloonHeight;
            const float floor = 0.40f; // raised floor inside the basket (rim 0.67 m): heads and shoulders show above it
            OpenBasket(b, floor);
            var seats = new[] { new Vector3(-0.14f, floor, -0.05f), new Vector3(0.15f, floor, 0.06f) };
            var seatT = new Transform[seats.Length];
            for (int i = 0; i < seats.Length; i++) { var t = new GameObject("Seat" + i).transform; t.SetParent(lift, false); t.localPosition = seats[i]; seatT[i] = t; }
            var rope = ContentBuilder.GetOrCreateMat("MS_BalloonString", new Color(1f, 0.96f, 0.9f), 0.05f, 0f, null);
            var wood = ContentBuilder.GetOrCreateMat("MS_TetherStake", new Color(0.72f, 0.52f, 0.4f), 0.1f, 0f, null);
            var tops = new Transform[2]; var stakes = new Transform[2]; var lines = new LineRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                float sx = i == 0 ? -1f : 1f;
                var top = new GameObject("TetherTop" + i).transform; top.SetParent(lift, false); top.localPosition = new Vector3(0.34f * sx, 0.12f, 0.3f); tops[i] = top;
                var stake = GameObject.CreatePrimitive(PrimitiveType.Cylinder); stake.name = "Stake" + i; Object.DestroyImmediate(stake.GetComponent<Collider>());
                stake.transform.SetParent(root.transform, false); stake.transform.localPosition = new Vector3(1.05f * sx, 0.06f, 0.7f); stake.transform.localScale = new Vector3(0.07f, 0.06f, 0.07f);
                stake.GetComponent<Renderer>().sharedMaterial = wood; stakes[i] = stake.transform;
                var lrGo = new GameObject("Tether" + i); lrGo.transform.SetParent(root.transform, false);
                var lr = lrGo.AddComponent<LineRenderer>(); lr.positionCount = 3; lr.widthMultiplier = 0.018f; lr.sharedMaterial = rope; lr.useWorldSpace = true;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.numCapVertices = 2; lines[i] = lr;
            }
            var ride = root.AddComponent<BalloonRide>();
            ride.lift = lift; ride.seats = seatT; ride.tetherTops = tops; ride.tetherStakes = stakes; ride.tethers = lines;
            ride.boardLocal = new Vector3(0f, 0f, 0.95f);
            // clicks: the envelope and the basket (both rise with the lift)
            var env = lift.gameObject.AddComponent<SphereCollider>(); env.center = new Vector3(0, BalloonHeight * 0.7f, 0); env.radius = BalloonHeight * 0.3f;
            var basket = new GameObject("BasketClick"); basket.transform.SetParent(lift, false);
            var bc = basket.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.35f, 0); bc.size = new Vector3(0.8f, 0.7f, 0.8f);
            return Save(root, "PF_Ride_Balloon");
        }
    }
}

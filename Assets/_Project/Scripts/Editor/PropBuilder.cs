using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mongle.Village;
using UnityEditor;
using UnityEngine;

namespace Mongle.EditorTools
{
    /// <summary>
    /// Builds the shop furniture, plant, watering can prefabs from primitives (felt look). Idempotent: regenerates the
    /// prefab assets this builder owns. Front of every piece faces +Z; pivot is the footprint center on the ground.
    /// </summary>
    public static class PropBuilder
    {
        const string Dir = "Assets/_Project/Prefabs/Props/";
        static Material M(string n, Color c, float s = 0.05f, Color? emit = null) => ContentBuilder.GetOrCreateMat(n, c, s, 0f, emit);

        static GameObject P(Transform parent, PrimitiveType t, string name, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(t); g.name = name; g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale; if (euler.HasValue) g.transform.localEulerAngles = euler.Value;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.GetComponent<Renderer>().sharedMaterial = mat;
            return g;
        }

        static Transform Node(Transform parent, string name, Vector3 pos) { var n = new GameObject(name).transform; n.SetParent(parent, false); n.localPosition = pos; return n; }

        static GameObject Save(GameObject root, string name)
        {
            Directory.CreateDirectory(Dir);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Dir + name + ".prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        public static GameObject Armchair()
        {
            var r = new GameObject("Fur_Armchair").transform;
            var fabric = M("MP_ChairPink", new Color(1f, 0.74f, 0.78f)); var cush = M("MP_ChairCushion", new Color(1f, 0.86f, 0.86f)); var wood = M("MP_ChairWood", new Color(0.72f, 0.52f, 0.36f), 0.15f);
            P(r, PrimitiveType.Cube, "Base", new Vector3(0, 0.1f, 0), new Vector3(0.34f, 0.09f, 0.32f), fabric);
            P(r, PrimitiveType.Sphere, "Cushion", new Vector3(0, 0.15f, 0.015f), new Vector3(0.3f, 0.09f, 0.28f), cush);
            P(r, PrimitiveType.Cube, "Back", new Vector3(0, 0.23f, -0.13f), new Vector3(0.34f, 0.24f, 0.07f), fabric, new Vector3(-8, 0, 0));
            P(r, PrimitiveType.Sphere, "BackTop", new Vector3(0, 0.355f, -0.14f), new Vector3(0.34f, 0.1f, 0.09f), fabric);
            foreach (float sx in new[] { -1f, 1f })
            {
                P(r, PrimitiveType.Sphere, "Arm", new Vector3(sx * 0.175f, 0.2f, 0.0f), new Vector3(0.075f, 0.12f, 0.3f), fabric);
                P(r, PrimitiveType.Cylinder, "Leg", new Vector3(sx * 0.14f, 0.025f, 0.12f), new Vector3(0.035f, 0.025f, 0.035f), wood);
                P(r, PrimitiveType.Cylinder, "Leg", new Vector3(sx * 0.14f, 0.025f, -0.12f), new Vector3(0.035f, 0.025f, 0.035f), wood);
            }
            Node(r, "IP_SEAT", new Vector3(0, 0.185f, 0.02f)); Node(r, "IP_APPROACH", new Vector3(0, 0, 0.34f));
            return Save(r.gameObject, "PF_Fur_Armchair");
        }

        public static GameObject ToyBox()
        {
            var r = new GameObject("Fur_ToyBox").transform;
            var box = M("MP_ToyBoxBlue", new Color(0.62f, 0.8f, 1f)); var band = M("MP_ToyBoxBand", new Color(1f, 0.97f, 0.88f)); var yellow = M("MP_ToyYellow", new Color(1f, 0.88f, 0.45f));
            var pink = M("MP_ToyPink", new Color(1f, 0.7f, 0.76f)); var mint = M("MP_ToyMint", new Color(0.7f, 0.9f, 0.78f));
            P(r, PrimitiveType.Cube, "Box", new Vector3(0, 0.1f, 0), new Vector3(0.34f, 0.2f, 0.27f), box);
            P(r, PrimitiveType.Cube, "Band", new Vector3(0, 0.1f, 0.0f), new Vector3(0.355f, 0.045f, 0.285f), band);
            var lid = Node(r, "LidHinge", new Vector3(0, 0.2f, -0.135f));
            // D-036: the lid used to be only 24° open, so its slab cut through the toys; now it stands open behind the box
            lid.localEulerAngles = new Vector3(-100f, 0, 0);
            P(lid, PrimitiveType.Cube, "Lid", new Vector3(0, -0.02f, 0.145f), new Vector3(0.36f, 0.04f, 0.29f), box);
            P(r, PrimitiveType.Sphere, "Ball", new Vector3(-0.06f, 0.23f, 0.02f), Vector3.one * 0.11f, yellow);
            P(r, PrimitiveType.Cube, "Block", new Vector3(0.07f, 0.23f, 0.0f), new Vector3(0.09f, 0.09f, 0.09f), pink, new Vector3(0, 25, 0));
            P(r, PrimitiveType.Sphere, "Bear", new Vector3(0.0f, 0.27f, -0.04f), new Vector3(0.09f, 0.1f, 0.08f), mint);
            Node(r, "IP_APPROACH", new Vector3(0, 0, 0.34f));
            return Save(r.gameObject, "PF_Fur_ToyBox");
        }

        public static GameObject Lamp()
        {
            var r = new GameObject("Fur_Lamp").transform;
            var wood = M("MP_LampPost", new Color(0.66f, 0.5f, 0.36f), 0.15f); var cap = M("MP_LampCap", new Color(0.55f, 0.72f, 0.9f), 0.1f);
            var bulb = M("MP_LampBulb", new Color(1f, 0.94f, 0.72f), 0.3f, new Color(1.5f, 1.2f, 0.65f));
            P(r, PrimitiveType.Cylinder, "Foot", new Vector3(0, 0.012f, 0), new Vector3(0.15f, 0.012f, 0.15f), wood);
            P(r, PrimitiveType.Cylinder, "Post", new Vector3(0, 0.23f, 0), new Vector3(0.028f, 0.21f, 0.028f), wood);
            // D-036: the cap's underside and the bulb's top were almost the same curve (speckled z-fighting): the cap now
            // sits higher and only its centre dips into the bulb
            var b = P(r, PrimitiveType.Sphere, "Bulb", new Vector3(0, 0.485f, 0), new Vector3(0.15f, 0.16f, 0.15f), bulb);
            P(r, PrimitiveType.Sphere, "Cap", new Vector3(0, 0.582f, 0), new Vector3(0.19f, 0.072f, 0.19f), cap);
            var lightGo = new GameObject("Glow"); lightGo.transform.SetParent(r, false); lightGo.transform.localPosition = new Vector3(0, 0.49f, 0);
            var l = lightGo.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.85f, 0.6f); l.range = 1.3f; l.intensity = 0.55f; l.shadows = LightShadows.None;
            var ll = r.gameObject.AddComponent<LampLight>(); ll.Glow = l; ll.Bulb = b.GetComponent<Renderer>();
            Node(r, "IP_LOOK", new Vector3(0, 0.3f, 0.3f)); Node(r, "IP_APPROACH", new Vector3(0, 0, 0.34f));
            return Save(r.gameObject, "PF_Fur_Lamp");
        }

        public static GameObject Table()
        {
            var r = new GameObject("Fur_Table").transform;
            var wood = M("MP_TableWood", new Color(0.78f, 0.57f, 0.38f), 0.15f); var cloth = M("MP_TableCloth", new Color(1f, 0.93f, 0.85f));
            var pot = M("MP_Teapot", new Color(1f, 0.78f, 0.8f), 0.3f); var cup = M("MP_Cup", new Color(0.78f, 0.9f, 1f), 0.3f);
            P(r, PrimitiveType.Cube, "Top", new Vector3(0, 0.185f, 0), new Vector3(0.74f, 0.035f, 0.34f), wood);
            P(r, PrimitiveType.Cube, "Cloth", new Vector3(0, 0.206f, 0), new Vector3(0.5f, 0.012f, 0.26f), cloth);
            foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f })
                    P(r, PrimitiveType.Cylinder, "Leg", new Vector3(sx * 0.32f, 0.085f, sz * 0.13f), new Vector3(0.04f, 0.085f, 0.04f), wood);
            P(r, PrimitiveType.Sphere, "Teapot", new Vector3(-0.1f, 0.255f, 0), new Vector3(0.11f, 0.085f, 0.11f), pot);
            P(r, PrimitiveType.Sphere, "TeapotLid", new Vector3(-0.1f, 0.305f, 0), new Vector3(0.04f, 0.03f, 0.04f), pot);
            P(r, PrimitiveType.Sphere, "Spout", new Vector3(-0.17f, 0.26f, 0), new Vector3(0.06f, 0.03f, 0.03f), pot, new Vector3(0, 0, 25));
            P(r, PrimitiveType.Cylinder, "Cup", new Vector3(0.1f, 0.235f, 0.04f), new Vector3(0.05f, 0.025f, 0.05f), cup);
            P(r, PrimitiveType.Cylinder, "Cup", new Vector3(0.18f, 0.235f, -0.04f), new Vector3(0.05f, 0.025f, 0.05f), cup);
            Node(r, "IP_LOOK", new Vector3(0, 0.2f, 0.3f)); Node(r, "IP_APPROACH", new Vector3(0, 0, 0.34f));
            return Save(r.gameObject, "PF_Fur_Table");
        }

        // ---------------- plants ----------------
        static void PotAndSoil(Transform r, Color potColor)
        {
            var pot = M("MP_PotTerracotta", new Color(0.88f, 0.58f, 0.43f), 0.15f); var soil = M("MP_Soil", new Color(0.42f, 0.3f, 0.2f));
            P(r, PrimitiveType.Cylinder, "Pot", new Vector3(0, 0.05f, 0), new Vector3(0.17f, 0.05f, 0.17f), pot);
            P(r, PrimitiveType.Cylinder, "Rim", new Vector3(0, 0.1f, 0), new Vector3(0.19f, 0.012f, 0.19f), pot);
            P(r, PrimitiveType.Cylinder, "Soil", new Vector3(0, 0.108f, 0), new Vector3(0.15f, 0.005f, 0.15f), soil);
            if (potColor.a > 0) { var band = M("MP_PotBand", potColor, 0.15f); P(r, PrimitiveType.Cylinder, "Band", new Vector3(0, 0.07f, 0), new Vector3(0.178f, 0.012f, 0.178f), band); }
        }

        static void Drop(Transform r, float y)
        {
            var d = Node(r, "Drop", new Vector3(0, y, 0));
            var m = M("MP_Drop", new Color(0.55f, 0.82f, 1f), 0.4f, new Color(0.25f, 0.5f, 0.8f));
            P(d, PrimitiveType.Sphere, "DropBody", Vector3.zero, new Vector3(0.055f, 0.065f, 0.055f), m);
            P(d, PrimitiveType.Sphere, "DropTip", new Vector3(0, 0.04f, 0), new Vector3(0.028f, 0.05f, 0.028f), m);
        }

        /// <summary>A leaf growing out of the stem at height y on one side (+1 right, -1 left), pointing outward and up by
        /// tilt degrees. D-036: leaves used to sit beside the stem pointing forward, with a visible gap to it.</summary>
        static void Leaf(Transform parent, float y, float size, float side, float tilt, Material mat)
        {
            var dir = new Vector3(side * Mathf.Cos(tilt * Mathf.Deg2Rad), Mathf.Sin(tilt * Mathf.Deg2Rad), 0f);
            P(parent, PrimitiveType.Sphere, "Leaf", new Vector3(0, y, 0) + dir * (size * 0.44f), new Vector3(size * 0.45f, size * 0.12f, size), mat, new Vector3(-tilt, side * 90f, 0));
        }

        static GameObject Plant(string id, System.Action<Transform, Material, Material> stage0, System.Action<Transform, Material, Material> stage1, System.Action<Transform, Material, Material> stage2, Color potBand, float dropY)
        {
            var r = new GameObject(id).transform;
            var green = M("MP_PlantGreen", new Color(0.52f, 0.78f, 0.5f), 0.1f); var dark = M("MP_PlantDark", new Color(0.4f, 0.67f, 0.42f), 0.1f);
            PotAndSoil(r, potBand);
            var s0 = Node(r, "Stage0", Vector3.zero); stage0(s0, green, dark);
            var s1 = Node(r, "Stage1", Vector3.zero); stage1(s1, green, dark);
            var s2 = Node(r, "Stage2", Vector3.zero); stage2(s2, green, dark);
            Drop(r, dropY);
            Node(r, "IP_LOOK", new Vector3(0, 0, 0.28f)); Node(r, "IP_APPROACH", new Vector3(0, 0, 0.34f));
            return Save(r.gameObject, "PF_" + id);
        }

        static void Stem(Transform p, float h, Material m) => P(p, PrimitiveType.Cylinder, "Stem", new Vector3(0, 0.11f + h / 2f, 0), new Vector3(0.016f, h / 2f, 0.016f), m);

        public static GameObject Tulip()
        {
            var petal = M("MP_TulipPetal", new Color(1f, 0.55f, 0.65f), 0.1f); var petalB = M("MP_TulipPetalB", new Color(1f, 0.72f, 0.78f), 0.1f);
            return Plant("Plant_Tulip",
                (p, g, d) => { Stem(p, 0.05f, g); Leaf(p, 0.14f, 0.07f, 1, 30, g); Leaf(p, 0.13f, 0.06f, -1, 30, d); },
                (p, g, d) => { Stem(p, 0.14f, g); Leaf(p, 0.16f, 0.11f, 1, 40, g); Leaf(p, 0.15f, 0.1f, -1, 40, d); P(p, PrimitiveType.Sphere, "Bud", new Vector3(0, 0.27f, 0), new Vector3(0.05f, 0.07f, 0.05f), petalB); },
                (p, g, d) =>
                {
                    Stem(p, 0.2f, g); Leaf(p, 0.17f, 0.13f, 1, 40, g); Leaf(p, 0.16f, 0.12f, -1, 40, d);
                    for (int i = 0; i < 3; i++) { float a = i * 120f * Mathf.Deg2Rad; P(p, PrimitiveType.Sphere, "Petal", new Vector3(Mathf.Cos(a) * 0.022f, 0.34f, Mathf.Sin(a) * 0.022f), new Vector3(0.05f, 0.085f, 0.05f), i == 1 ? petalB : petal); }
                }, new Color(1f, 0.78f, 0.8f), 0.46f);
        }

        public static GameObject Sunflower()
        {
            var petal = M("MP_SunPetal", new Color(1f, 0.85f, 0.3f), 0.1f); var core = M("MP_SunCore", new Color(0.55f, 0.36f, 0.2f), 0.05f);
            return Plant("Plant_Sunflower",
                (p, g, d) => { Stem(p, 0.05f, g); Leaf(p, 0.14f, 0.07f, 1, 30, g); Leaf(p, 0.13f, 0.06f, -1, 30, d); },
                (p, g, d) => { Stem(p, 0.2f, g); Leaf(p, 0.17f, 0.12f, 1, 40, g); Leaf(p, 0.21f, 0.12f, -1, 40, d); P(p, PrimitiveType.Sphere, "Bud", new Vector3(0, 0.33f, 0.01f), new Vector3(0.07f, 0.07f, 0.06f), d); },
                (p, g, d) =>
                {
                    Stem(p, 0.3f, g); Leaf(p, 0.19f, 0.14f, 1, 40, g); Leaf(p, 0.25f, 0.14f, -1, 40, d);
                    var head = Node(p, "Head", new Vector3(0, 0.43f, 0.02f)); head.localRotation = Quaternion.Euler(-12, 0, 0);
                    for (int i = 0; i < 10; i++) { float a = i * 36f * Mathf.Deg2Rad; P(head, PrimitiveType.Sphere, "Petal", new Vector3(Mathf.Cos(a) * 0.065f, Mathf.Sin(a) * 0.065f, 0), new Vector3(0.045f, 0.07f, 0.02f), petal, new Vector3(0, 0, i * 36f - 90f)); }
                    P(head, PrimitiveType.Sphere, "Core", new Vector3(0, 0, 0.012f), new Vector3(0.08f, 0.08f, 0.04f), core);
                }, new Color(1f, 0.9f, 0.5f), 0.56f);
        }

        public static GameObject Cactus()
        {
            var cac = M("MP_Cactus", new Color(0.5f, 0.76f, 0.58f), 0.1f); var cacDark = M("MP_CactusDark", new Color(0.42f, 0.68f, 0.5f), 0.1f); var spine = M("MP_CactusSpine", new Color(1f, 0.97f, 0.85f)); var flower = M("MP_CactusFlower", new Color(1f, 0.6f, 0.72f), 0.1f);
            return Plant("Plant_Cactus",
                (p, g, d) => P(p, PrimitiveType.Sphere, "Body", new Vector3(0, 0.15f, 0), new Vector3(0.09f, 0.08f, 0.09f), cac),
                (p, g, d) => { P(p, PrimitiveType.Sphere, "Body", new Vector3(0, 0.19f, 0), new Vector3(0.11f, 0.17f, 0.11f), cac); P(p, PrimitiveType.Sphere, "Arm", new Vector3(0.075f, 0.2f, 0), new Vector3(0.05f, 0.08f, 0.05f), cacDark); },
                (p, g, d) =>
                {
                    P(p, PrimitiveType.Sphere, "Body", new Vector3(0, 0.22f, 0), new Vector3(0.12f, 0.22f, 0.12f), cac);
                    P(p, PrimitiveType.Sphere, "ArmR", new Vector3(0.085f, 0.23f, 0), new Vector3(0.055f, 0.1f, 0.055f), cacDark); P(p, PrimitiveType.Sphere, "ArmL", new Vector3(-0.08f, 0.19f, 0), new Vector3(0.05f, 0.08f, 0.05f), cacDark);
                    // D-036: spine dots on the body's ellipsoid (centre y 0.22, radii 0.06 / 0.11) instead of a flat plane in front of it
                    for (int i = 0; i < 9; i++)
                    {
                        float yy = 0.16f + 0.05f * (i / 3) + (i % 2) * 0.012f, ang = (-50f + 50f * (i % 3) + (i / 3) * 12f) * Mathf.Deg2Rad;
                        float rr = 0.06f * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((yy - 0.22f) / 0.11f, 2f)));
                        P(p, PrimitiveType.Sphere, "Dot", new Vector3(Mathf.Sin(ang) * rr, yy, Mathf.Cos(ang) * rr), Vector3.one * 0.012f, spine);
                    }
                    for (int i = 0; i < 5; i++) { float a = i * 72f * Mathf.Deg2Rad; P(p, PrimitiveType.Sphere, "Petal", new Vector3(Mathf.Cos(a) * 0.022f, 0.345f, Mathf.Sin(a) * 0.022f), new Vector3(0.035f, 0.04f, 0.035f), flower); }
                }, new Color(0.7f, 0.88f, 0.8f), 0.5f);
        }

        public static GameObject Sapling()
        {
            var trunk = M("MP_SaplingTrunk", new Color(0.62f, 0.45f, 0.32f), 0.1f); var blossom = M("MP_Blossom", new Color(1f, 0.76f, 0.82f), 0.1f); var blossomB = M("MP_BlossomB", new Color(1f, 0.85f, 0.88f), 0.1f);
            return Plant("Plant_Sapling",
                (p, g, d) => { P(p, PrimitiveType.Cylinder, "Twig", new Vector3(0, 0.15f, 0), new Vector3(0.014f, 0.05f, 0.014f), trunk); Leaf(p, 0.185f, 0.07f, 1, 30, g); Leaf(p, 0.175f, 0.07f, -1, 30, d); },
                (p, g, d) => { P(p, PrimitiveType.Cylinder, "Trunk", new Vector3(0, 0.2f, 0), new Vector3(0.022f, 0.1f, 0.022f), trunk); P(p, PrimitiveType.Sphere, "Crown", new Vector3(0, 0.36f, 0), new Vector3(0.18f, 0.15f, 0.18f), g); },
                (p, g, d) =>
                {
                    P(p, PrimitiveType.Cylinder, "Trunk", new Vector3(0, 0.23f, 0), new Vector3(0.028f, 0.13f, 0.028f), trunk);
                    P(p, PrimitiveType.Sphere, "Crown", new Vector3(0, 0.42f, 0), new Vector3(0.26f, 0.2f, 0.26f), blossom);
                    P(p, PrimitiveType.Sphere, "CrownB", new Vector3(0.07f, 0.5f, 0.03f), new Vector3(0.15f, 0.12f, 0.15f), blossomB);
                    P(p, PrimitiveType.Sphere, "CrownC", new Vector3(-0.08f, 0.38f, -0.04f), new Vector3(0.14f, 0.11f, 0.14f), blossomB);
                }, new Color(0.78f, 0.9f, 0.7f), 0.66f);
        }

        public static GameObject WateringCan()
        {
            var r = new GameObject("WateringCan").transform;
            var body = M("MP_CanBody", new Color(0.62f, 0.85f, 0.9f), 0.35f); var dark = M("MP_CanDark", new Color(0.5f, 0.74f, 0.8f), 0.35f);
            P(r, PrimitiveType.Cylinder, "Body", Vector3.zero, new Vector3(0.12f, 0.06f, 0.12f), body);
            P(r, PrimitiveType.Cylinder, "Top", new Vector3(0, 0.062f, 0), new Vector3(0.1f, 0.006f, 0.1f), dark);
            var sp = P(r, PrimitiveType.Cylinder, "SpoutPipe", new Vector3(0.1f, 0.02f, 0), new Vector3(0.025f, 0.06f, 0.025f), body, new Vector3(0, 0, -62));
            P(r, PrimitiveType.Sphere, "Rose", new Vector3(0.158f, 0.062f, 0), new Vector3(0.05f, 0.05f, 0.05f), dark);
            Node(r, "Spout", new Vector3(0.17f, 0.06f, 0));
            // D-036: the handle was a flat oval 3 cm off the body; now a D-shaped loop of three capsules joined to it
            P(r, PrimitiveType.Capsule, "HandleTop", new Vector3(-0.08f, 0.045f, 0), new Vector3(0.022f, 0.026f, 0.022f), dark, new Vector3(0, 0, 90));
            P(r, PrimitiveType.Capsule, "HandleBack", new Vector3(-0.104f, 0.01f, 0), new Vector3(0.022f, 0.046f, 0.022f), dark);
            P(r, PrimitiveType.Capsule, "HandleLow", new Vector3(-0.08f, -0.025f, 0), new Vector3(0.022f, 0.026f, 0.022f), dark, new Vector3(0, 0, 90));
            return Save(r.gameObject, "PF_WateringCan");
        }


        // ---------------- imported models (user-supplied gray FBX: recolored per part, scaled, grounded) ----------------
        const string ShopArt = "Assets/_Project/Art/Props/Shop/";

        /// <summary>Instantiate an FBX as a plain hierarchy, color it per part, scale to a target height, ground it at y=0 and center it.</summary>
        public static Transform ImportedModel(string fbx, Transform parent, float targetHeight, float yaw, System.Func<Renderer, string> pick, System.Func<string, Color, Material> mat, System.Action<Transform> prep = null)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(ShopArt + fbx + ".fbx");
            var model = (GameObject)Object.Instantiate(src); model.name = "Model"; model.transform.SetParent(parent, false);
            model.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            foreach (var r in model.GetComponentsInChildren<Renderer>()) { var key = pick(r); if (key != null) r.sharedMaterial = mat(key, Color.white); }
            prep?.Invoke(model.transform);
            var rs = model.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float s = targetHeight / b.size.y;
            model.transform.localScale = Vector3.one * s;
            b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var delta = parent.position - new Vector3(b.center.x, b.min.y, b.center.z); // center x/z on the parent, rest on its ground
            model.transform.localPosition += parent.InverseTransformVector(delta);
            foreach (var r in rs) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
            return model.transform;
        }

        static Material Mf(int fuzz, string n, Color c, float smooth = 0.05f) => M("MF" + fuzz + "_" + n, c, smooth);

        public static GameObject FerrisWheel()
        {
            var root = new GameObject("Fur_Wheel").transform;
            var cream = new Color(1f, 0.96f, 0.88f); var legC = new Color(0.9f, 0.66f, 0.62f); var hub = new Color(1f, 0.55f, 0.65f);
            var pastel = new[] { new Color(1f, 0.72f, 0.78f), new Color(0.7f, 0.86f, 1f), new Color(1f, 0.9f, 0.55f), new Color(0.72f, 0.9f, 0.78f) };
            const float hubY = 0.134f;
            ImportedModel("Fur_Wheel", root, 0.62f, 0f, r =>
            {
                string n = r.name; var c = r.bounds.center;
                if (n.StartsWith("Circle")) return "rim";
                if (n == "Cylinder1") return "hub";
                if (n == "pCube12" || n == "pCube16" || n == "pCube17" || n == "pCube18") return "leg";
                if (n == "pCube34" || n == "pCube35") return "heart";
                if (n.StartsWith("pCube")) return "spoke";
                if (r.bounds.size.x < 0.004f && r.bounds.size.y < 0.004f) return "rope";
                int slot = Mathf.RoundToInt(Mathf.Atan2(c.y - hubY, c.x) / (Mathf.PI / 4f)); slot = ((slot % 8) + 8) % 8;
                return "gond" + (slot % 4);
            }, (key, _) =>
            {
                switch (key)
                {
                    case "rim": case "spoke": case "rope": return Mf(2600, "WheelCream", cream);
                    case "hub": return Mf(2600, "WheelHub", hub);
                    case "leg": return Mf(2600, "WheelLeg", legC);
                    case "heart": return Mf(2600, "WheelHeart", hub);
                    default: return Mf(2600, "WheelGondola" + key.Substring(4), pastel[int.Parse(key.Substring(4))]);
                }
            }, model =>
            {
                // everything that is not the A-frame legs / hub heart turns around the hub
                var spin = new GameObject("Spinner").transform; spin.SetParent(model, false); spin.localPosition = new Vector3(0, hubY, 0);
                spin.gameObject.AddComponent<Mongle.Village.SpinPart>();
                foreach (var r in model.GetComponentsInChildren<Renderer>().ToArray())
                {
                    string n = r.name; if (n == "pCube12" || n == "pCube16" || n == "pCube17" || n == "pCube18" || n == "pCube34" || n == "pCube35" || n == "Cylinder1") continue;
                    r.transform.SetParent(spin, true);
                }
                // each gondola (cap, roof, body, floor stacked straight up) hangs from a pivot that stays upright while the
                // wheel turns (user: the gondolas used to turn with the wheel and stuck out at odd angles)
                // (this runs before ImportedModel scales the model to 0.62 m, so the tolerances follow the raw model size)
                var all = model.GetComponentsInChildren<Renderer>(); var mb = all[0].bounds; foreach (var r in all) mb.Encapsulate(r.bounds);
                float k = mb.size.y / 0.62f;
                var groups = new List<List<Renderer>>();
                foreach (var r in spin.GetComponentsInChildren<Renderer>())
                {
                    if (r.sharedMaterial == null || !r.sharedMaterial.name.Contains("WheelGondola")) continue;
                    var c = r.bounds.center; List<Renderer> home = null;
                    foreach (var g in groups)
                    {
                        var gc = g[0].bounds.center; float gy = 0f; foreach (var m in g) gy += m.bounds.center.y; gy /= g.Count;
                        if (Mathf.Abs(c.x - gc.x) < 0.03f * k && Mathf.Abs(c.z - gc.z) < 0.03f * k && Mathf.Abs(c.y - gy) < 0.1f * k) { home = g; break; }
                    }
                    if (home == null) groups.Add(home = new List<Renderer>());
                    home.Add(r);
                }
                var gmats = groups.SelectMany(g => g).Select(x => x.sharedMaterial).Distinct().OrderBy(m => m.name).ToArray();
                for (int gi = 0; gi < groups.Count; gi++)
                {
                    var parts = groups[gi]; foreach (var p2 in parts) p2.sharedMaterial = gmats[gi % gmats.Length];   // one colour per gondola
                    float top = float.MinValue; Renderer cap = parts[0];
                    foreach (var p2 in parts) if (p2.bounds.max.y > top) { top = p2.bounds.max.y; cap = p2; }
                    var pivot = new GameObject("Gondola").transform; pivot.SetParent(spin, false);
                    pivot.position = new Vector3(cap.bounds.center.x, top, cap.bounds.center.z); pivot.rotation = model.rotation;
                    foreach (var p2 in parts) p2.transform.SetParent(pivot, true);
                    pivot.gameObject.AddComponent<Mongle.Village.KeepUpright>().frame = model;
                }
            });
            Node(root, "IP_LOOK", new Vector3(0, 0, 0.3f)); Node(root, "IP_APPROACH", new Vector3(0, 0, 0.34f));
            return Save(root.gameObject, "PF_Fur_Wheel");
        }

        public static GameObject TreeSet()
        {
            var root = new GameObject("Fur_Tree").transform;
            ImportedModel("Fur_Tree", root, 0.34f, 0f, r =>
            {
                switch (r.name)
                {
                    case "pCylinder17": case "pCylinder18": case "pCylinder19": return "pine";
                    case "pCylinder20": case "pCylinder21": case "pCylinder53": return "trunk";
                    case "pSphere2": case "pSphere6": return "blossom";
                    default: return "leaf";
                }
            }, (key, _) =>
            {
                switch (key)
                {
                    case "pine": return Mf(900, "TreePine", new Color(0.34f, 0.62f, 0.45f));
                    case "trunk": return Mf(900, "TreeTrunk", new Color(0.62f, 0.45f, 0.32f), 0.1f);
                    case "blossom": return Mf(900, "TreeBlossom", new Color(1f, 0.76f, 0.82f));
                    default: return Mf(900, "TreeLeaf", new Color(0.52f, 0.78f, 0.46f));
                }
            });
            Node(root, "IP_LOOK", new Vector3(0, 0, 0.3f)); Node(root, "IP_APPROACH", new Vector3(0, 0, 0.34f));
            return Save(root.gameObject, "PF_Fur_Tree");
        }

        public static GameObject CakeHouse()
        {
            var root = new GameObject("Fur_Cake").transform;
            ImportedModel("Fur_Cake", root, 0.42f, 0f, r =>
            {
                string n = r.name.Replace("pasted__", "");
                switch (n)
                {
                    case "polySurface1": return "wrapper";
                    case "pTorus1": return "rim";
                    case "pSphere1": return "icing";
                    case "pCylinder4": return "cream";
                    case "pSphere3": return "cherry";
                    case "pCylinder29": return "stem";
                    case "pCylinder10": return "wafer";
                    case "pCylinder8": return "sprinkle" + (r.transform.GetSiblingIndex() % 3);
                    case "polySurface2": return "door";
                    case "polySurface7": case "polySurface10": return "window";
                    default: return "frame";
                }
            }, (key, _) =>
            {
                switch (key)
                {
                    case "wrapper": return Mf(750, "CakeWrapper", new Color(1f, 0.8f, 0.68f));
                    case "rim": return Mf(750, "CakeRim", new Color(1f, 0.93f, 0.82f));
                    case "icing": return Mf(750, "CakeIcing", new Color(1f, 0.72f, 0.8f));
                    case "cream": return Mf(750, "CakeCream", new Color(1f, 0.95f, 0.9f));
                    case "cherry": return Mf(750, "CakeCherry", new Color(0.95f, 0.3f, 0.4f), 0.3f);
                    case "stem": case "wafer": return Mf(750, "CakeChoco", new Color(0.62f, 0.42f, 0.32f));
                    case "door": return Mf(750, "CakeDoor", new Color(0.72f, 0.5f, 0.36f));
                    case "window": return Mf(750, "CakeWindow", new Color(0.8f, 0.92f, 1f), 0.3f);
                    case "frame": return Mf(750, "CakeFrame", new Color(1f, 0.96f, 0.88f));
                    default: var cols = new[] { new Color(1f, 0.9f, 0.45f), new Color(0.65f, 0.85f, 1f), new Color(0.7f, 0.92f, 0.75f) }; return Mf(750, "CakeSprinkle" + key.Substring(8), cols[int.Parse(key.Substring(8))]);
                }
            });
            Node(root, "IP_LOOK", new Vector3(0, 0, 0.3f)); Node(root, "IP_APPROACH", new Vector3(0, 0, 0.34f));
            return Save(root.gameObject, "PF_Fur_Cake");
        }

        /// <summary>The watering-can cottage that replaces the plant shop building (colored per part, door facing +Z).</summary>
        public const float PlantShopHeight = 1.9f; // was 1.2: it looked tiny next to the hospital and the cupcake shop

        public static void PlantShopBody(Transform parent)
        {
            ImportedModel("Shop_Plant", parent, PlantShopHeight, 90f, r =>
            {
                switch (r.name)
                {
                    case "pCylinder1": case "pCube1": return "body";
                    case "pCylinder2": return "spout";
                    case "pPlane1": return "handle";
                    case "pCylinder4": return "pot";
                    case "pCube6": case "pCube7": return "leaf";
                    case "pCube3": return "door";
                    case "pCube4": return "awning";
                    case "pCube8": return "cream";
                    case "pCube2": return "window";
                    case "pCylinder3": return "knob";
                    case "pCube9": return "bench";
                    default: return r.name.Contains("pCube10") || r.name.Contains("pCube11") || r.name.Contains("pCube12") ? "bench" : "wood";
                }
            }, (key, _) =>
            {
                switch (key)
                {
                    case "body": return Mf(3400, "CanBody", new Color(0.7f, 0.9f, 0.78f));
                    case "spout": case "handle": return Mf(3400, "CanSpout", new Color(0.5f, 0.76f, 0.62f));
                    case "pot": return Mf(3400, "CanPot", new Color(0.88f, 0.58f, 0.43f));
                    case "leaf": return Mf(3400, "CanLeaf", new Color(0.45f, 0.75f, 0.45f));
                    case "door": return Mf(3400, "CanDoor", new Color(0.8f, 0.58f, 0.4f));
                    case "awning": return Mf(3400, "CanAwning", new Color(1f, 0.68f, 0.74f));
                    case "cream": return Mf(3400, "CanCream", new Color(1f, 0.96f, 0.88f));
                    case "window": return Mf(3400, "CanWindow", new Color(0.8f, 0.92f, 1f), 0.3f);
                    case "knob": return Mf(3400, "CanKnob", new Color(1f, 0.85f, 0.4f), 0.3f);
                    case "bench": return Mf(3400, "CanBench", new Color(0.82f, 0.62f, 0.44f));
                    default: return Mf(3400, "CanWood", new Color(0.7f, 0.5f, 0.36f));
                }
            });
        }

        [MenuItem("Mongle/Build Props")]
        public static void BuildAll()
        {
            Armchair(); ToyBox(); Lamp(); Table(); Tulip(); Sunflower(); Cactus(); Sapling(); WateringCan(); FerrisWheel(); TreeSet(); CakeHouse();
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[PropBuilder] built prop prefabs");
        }

        public static GameObject Load(string prefabName) => AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "PF_" + prefabName + ".prefab");
    }
}

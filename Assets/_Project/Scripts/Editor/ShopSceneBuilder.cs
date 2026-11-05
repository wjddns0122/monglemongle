using System.Linq;
using Mongle.Data;
using Mongle.Village;
using UnityEditor;
using UnityEngine;

namespace Mongle.EditorTools
{
    /// <summary>Builds a shop cottage and its keeper from primitives (felt look) for the Village scene, driven by a ShopDefinition.</summary>
    public static class ShopSceneBuilder
    {
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

        public static GameObject Build(ShopDefinition def, Transform parent, bool rabbit)
        {
            var root = new GameObject(def.id).transform; root.SetParent(parent, false);
            root.position = def.position; root.rotation = Quaternion.Euler(0, def.yaw, 0);
            string k = def.id;
            var wall = M("MS_" + k + "_Wall", def.wallColor); var roof = M("MS_" + k + "_Roof", def.roofColor); var cream = M("MS_Cream", new Color(1f, 0.96f, 0.88f));
            var wood = M("MS_ShopWood", new Color(0.72f, 0.52f, 0.36f), 0.15f); var woodDark = M("MS_ShopWoodDark", new Color(0.55f, 0.38f, 0.26f), 0.15f);
            var glass = M("MS_ShopGlass", new Color(0.82f, 0.93f, 1f), 0.4f, new Color(0.18f, 0.22f, 0.26f));
            var warm = M("MS_ShopLamp", new Color(1f, 0.93f, 0.7f), 0.3f, new Color(1.5f, 1.2f, 0.65f));

            bool useModel = rabbit && AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Props/Shop/Shop_Plant.fbx") != null;
            // the accessory shop lives in the big cupcake house (it used to be a piece of furniture)
            bool cupcake = def.id == "Shop_Accessory" && PropBuilder.Load("Fur_Cake") != null;
            if (cupcake)
            {
                var body = (GameObject)PrefabUtility.InstantiatePrefab(PropBuilder.Load("Fur_Cake"));
                PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                body.name = "Model"; body.transform.SetParent(root, false);
                foreach (var c in body.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                body.transform.localScale = Vector3.one * (CupcakeHeight / 0.42f); body.transform.localPosition = new Vector3(0f, 0f, -0.2f);
                useModel = true;
            }
            else if (useModel)
            {
                PropBuilder.PlantShopBody(root);
                // push the bigger can back so the counter and the keeper still stand in front of it
                var body = root.Find("Model"); if (body != null) body.localPosition += new Vector3(0f, 0f, -0.3f - (PropBuilder.PlantShopHeight - 1.2f) * 0.45f);
            }
            else
            {
            // house body, trim, barrel roof
            P(root, PrimitiveType.Cube, "Walls", new Vector3(0, 0.43f, 0), new Vector3(1.8f, 0.86f, 1.2f), wall);
            P(root, PrimitiveType.Cube, "Base", new Vector3(0, 0.05f, 0), new Vector3(1.88f, 0.1f, 1.28f), cream);
            // gable roof: two slabs + a wall-colored gable diamond (skewed through a scaled parent)
            foreach (float sx in new[] { -1f, 1f })
                P(root, PrimitiveType.Cube, "RoofSlab", new Vector3(sx * 0.5f, 1.14f, 0), new Vector3(1.24f, 0.06f, 1.4f), roof, new Vector3(0, 0, -sx * 35f));
            P(root, PrimitiveType.Cylinder, "RidgeCap", new Vector3(0, 1.49f, 0), new Vector3(0.08f, 0.7f, 0.08f), cream, new Vector3(90, 0, 0));
            foreach (float gz in new[] { -1f, 1f })
            {
                var gp = Node(root, "Gable", new Vector3(0, 0.86f, gz * 0.598f)); gp.localScale = new Vector3(1.27f, 0.89f, 1f);
                P(gp, PrimitiveType.Cube, "GableDiamond", Vector3.zero, new Vector3(1f, 1f, 0.005f), wall, new Vector3(0, 0, 45));
            }
            P(root, PrimitiveType.Cube, "Chimney", new Vector3(-0.62f, 1.22f, -0.2f), new Vector3(0.2f, 0.42f, 0.2f), M("MS_Chimney", new Color(0.78f, 0.52f, 0.42f)));

            // door, window, awning, counter
            P(root, PrimitiveType.Cube, "Door", new Vector3(0.58f, 0.3f, 0.61f), new Vector3(0.32f, 0.56f, 0.03f), woodDark);
            P(root, PrimitiveType.Sphere, "DoorTop", new Vector3(0.58f, 0.58f, 0.61f), new Vector3(0.32f, 0.16f, 0.03f), woodDark);
            P(root, PrimitiveType.Sphere, "Knob", new Vector3(0.68f, 0.3f, 0.635f), Vector3.one * 0.035f, warm);
            P(root, PrimitiveType.Cube, "Window", new Vector3(-0.38f, 0.5f, 0.605f), new Vector3(0.78f, 0.42f, 0.03f), glass);
            P(root, PrimitiveType.Cube, "WindowBar", new Vector3(-0.38f, 0.5f, 0.625f), new Vector3(0.04f, 0.42f, 0.03f), cream);
            P(root, PrimitiveType.Cube, "WindowSill", new Vector3(-0.38f, 0.28f, 0.66f), new Vector3(0.9f, 0.04f, 0.14f), cream);
            for (int i = 0; i < 6; i++)
            {
                var stripe = M("MS_" + k + "_Awning" + (i % 2), i % 2 == 0 ? def.roofColor : new Color(1f, 0.97f, 0.9f));
                P(root, PrimitiveType.Cube, "Awning", new Vector3(-0.75f + i * 0.3f, 0.82f, 0.86f), new Vector3(0.3f, 0.025f, 0.5f), stripe, new Vector3(-16, 0, 0));
                P(root, PrimitiveType.Sphere, "AwningLip", new Vector3(-0.75f + i * 0.3f, 0.72f, 1.07f), new Vector3(0.3f, 0.07f, 0.07f), stripe);
            }
            }
            P(root, PrimitiveType.Cube, "Counter", new Vector3(-0.4f, 0.13f, 1.0f), new Vector3(1.0f, 0.26f, 0.22f), wood);
            P(root, PrimitiveType.Cube, "CounterTop", new Vector3(-0.4f, 0.27f, 1.0f), new Vector3(1.08f, 0.03f, 0.3f), woodDark);
            if (!useModel) { var lamp = Node(root, "DoorLamp", new Vector3(0.9f, 0.62f, 0.64f)); P(lamp, PrimitiveType.Sphere, "Bulb", Vector3.zero, Vector3.one * 0.08f, warm); }

            // counter goods + side decor depend on the shop
            if (rabbit) PlantDecor(root, def, useModel); else if (cupcake) AccessoryDecor(root, def); else FurnitureDecor(root, def);

            // sign post
            P(root, PrimitiveType.Cylinder, "SignPost", new Vector3(1.15f, 0.3f, 0.98f), new Vector3(0.04f, 0.3f, 0.04f), woodDark);
            P(root, PrimitiveType.Cube, "SignBoard", new Vector3(1.15f, 0.66f, 0.98f), new Vector3(0.46f, 0.3f, 0.04f), cream);
            if (cupcake)
            {
                var bow = M("MS_SignBow", new Color(1f, 0.55f, 0.66f));
                P(root, PrimitiveType.Sphere, "SignBowL", new Vector3(1.09f, 0.66f, 1.01f), new Vector3(0.12f, 0.09f, 0.03f), bow, new Vector3(0, 0, 20));
                P(root, PrimitiveType.Sphere, "SignBowR", new Vector3(1.21f, 0.66f, 1.01f), new Vector3(0.12f, 0.09f, 0.03f), bow, new Vector3(0, 0, -20));
                P(root, PrimitiveType.Sphere, "SignBowKnot", new Vector3(1.15f, 0.66f, 1.02f), Vector3.one * 0.05f, bow);
            }
            else if (rabbit)
            {
                var pet = M("MS_SignPetal", new Color(1f, 0.62f, 0.72f));
                for (int i = 0; i < 5; i++) { float a = i * 72f * Mathf.Deg2Rad; P(root, PrimitiveType.Sphere, "SignPetal", new Vector3(1.15f + Mathf.Cos(a) * 0.065f, 0.66f + Mathf.Sin(a) * 0.065f, 1.01f), Vector3.one * 0.07f, pet); }
                P(root, PrimitiveType.Sphere, "SignCore", new Vector3(1.15f, 0.66f, 1.015f), Vector3.one * 0.06f, M("MS_SignCore", new Color(1f, 0.9f, 0.45f)));
            }
            else
            {
                var sc = M("MS_SignSofa", new Color(0.58f, 0.76f, 0.92f));
                P(root, PrimitiveType.Cube, "SignSeat", new Vector3(1.15f, 0.62f, 1.01f), new Vector3(0.26f, 0.08f, 0.02f), sc);
                P(root, PrimitiveType.Sphere, "SignBack", new Vector3(1.15f, 0.7f, 1.01f), new Vector3(0.26f, 0.12f, 0.02f), sc);
            }

            // keeper behind the counter
            var keeper = Keeper(root, def, rabbit);

            // click colliders + component
            var box = root.gameObject.AddComponent<BoxCollider>();
            if (cupcake) { box.center = new Vector3(0, CupcakeHeight * 0.5f, 0.4f); box.size = new Vector3(2.2f, CupcakeHeight, 2.0f); }
            else if (rabbit && useModel) { box.center = new Vector3(0, PropBuilder.PlantShopHeight * 0.5f, 0.2f); box.size = new Vector3(2.4f, PropBuilder.PlantShopHeight, 2.4f); }
            else { box.center = new Vector3(0, 0.65f, 0.5f); box.size = new Vector3(2.0f, 1.3f, 1.8f); }
            var sb = root.gameObject.AddComponent<ShopBuilding>(); sb.shopId = def.id; sb.Keeper = keeper;
            var cam = Node(root, "CAM_SHOP", new Vector3(-0.2f, 0.75f, 3.2f)); sb.CameraSpot = cam;
            return root.gameObject;
        }

        const float CupcakeHeight = 2.1f;

        /// <summary>The accessory shop shows what it sells on its counter (the real accessory models, a little bigger).</summary>
        static void AccessoryDecor(Transform root, ShopDefinition def)
        {
            // four goods on the counter top (x -0.94..0.14), two each side of the keeper (x -0.4) so its face stays in view
            var spots = new[] { new Vector3(-0.85f, 0.3f, 1.0f), new Vector3(-0.64f, 0.3f, 1.0f), new Vector3(-0.16f, 0.3f, 1.0f), new Vector3(0.04f, 0.3f, 1.0f) };
            // a mix of old and new goods first (the rest follow in shop order when one is missing)
            string[] showcase = { "Acc_Crown", "Acc_RedRibbon", "Acc_FlowerCrown", "Acc_HeartGlasses" };
            var goods = (def.items ?? new Mongle.Data.FurnitureDefinition[0]).Where(it => it != null && it.prefab != null)
                .OrderBy(it => { int k = System.Array.IndexOf(showcase, it.id); return k >= 0 ? k : 99; }).ToList();
            int shown = 0;
            for (int i = 0; shown < spots.Length && i < goods.Count; i++)
            {
                var it = goods[i];
                var g = (GameObject)Object.Instantiate(it.prefab, root); g.name = "Good_" + it.id;
                foreach (var c in g.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                var rs = g.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) { Object.DestroyImmediate(g); continue; }
                g.transform.localRotation = Quaternion.identity; g.transform.localScale = Vector3.one;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float s = 0.19f / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.y, b.size.z));
                g.transform.localScale = Vector3.one * s;
                b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                g.transform.position += root.TransformPoint(spots[shown++]) - new Vector3(b.center.x, b.min.y, b.center.z);
            }
            // ribbon boxes stacked by the door
            var boxA = M("MS_GiftBoxA", new Color(1f, 0.78f, 0.84f)); var boxB = M("MS_GiftBoxB", new Color(0.78f, 0.86f, 1f)); var tie = M("MS_GiftTie", new Color(1f, 0.92f, 0.6f));
            for (int i = 0; i < 2; i++)
            {
                var p = new Vector3(1.3f, 0.12f + i * 0.22f, 0.25f + i * 0.03f);
                P(root, PrimitiveType.Cube, "GiftBox", p, new Vector3(0.3f - i * 0.06f, 0.22f, 0.3f - i * 0.06f), i == 0 ? boxA : boxB, new Vector3(0, 10 - i * 25, 0));
                P(root, PrimitiveType.Cube, "GiftTie", p + new Vector3(0, 0.002f, 0), new Vector3(0.05f, 0.226f, 0.31f - i * 0.06f), tie, new Vector3(0, 10 - i * 25, 0));
            }
        }

        static void FurnitureDecor(Transform root, ShopDefinition def)
        {
            var cush = M("MS_ShopCushionA", new Color(1f, 0.74f, 0.78f)); var cushB = M("MS_ShopCushionB", new Color(0.7f, 0.86f, 1f)); var yellow = M("MS_ShopYellow", new Color(1f, 0.88f, 0.5f));
            var wood = M("MS_ShopWood", new Color(0.72f, 0.52f, 0.36f), 0.15f);
            // goods on the counter
            P(root, PrimitiveType.Sphere, "GoodCushion", new Vector3(-0.7f, 0.32f, 1.0f), new Vector3(0.16f, 0.07f, 0.14f), cush);
            P(root, PrimitiveType.Sphere, "GoodCushion", new Vector3(-0.45f, 0.32f, 1.0f), new Vector3(0.14f, 0.065f, 0.14f), cushB);
            P(root, PrimitiveType.Cylinder, "GoodLampPost", new Vector3(-0.15f, 0.38f, 1.0f), new Vector3(0.016f, 0.1f, 0.016f), wood);
            P(root, PrimitiveType.Sphere, "GoodLampBulb", new Vector3(-0.15f, 0.5f, 1.0f), new Vector3(0.08f, 0.09f, 0.08f), yellow);
            // stacked crates and a spare chair beside the door
            for (int i = 0; i < 2; i++) P(root, PrimitiveType.Cube, "Crate", new Vector3(1.28f, 0.1f + i * 0.2f, 0.2f + i * 0.04f), new Vector3(0.32f, 0.2f, 0.32f), wood, new Vector3(0, 12 - i * 30, 0));
            P(root, PrimitiveType.Sphere, "CrateCushion", new Vector3(1.28f, 0.44f, 0.22f), new Vector3(0.2f, 0.08f, 0.2f), cush);
        }

        static void PlantDecor(Transform root, ShopDefinition def, bool useModel)
        {
            var pot = M("MS_ShopPot", new Color(0.88f, 0.58f, 0.43f), 0.15f); var leaf = M("MS_ShopLeaf", new Color(0.5f, 0.76f, 0.5f), 0.1f);
            var wood = M("MS_ShopWood", new Color(0.72f, 0.52f, 0.36f), 0.15f);
            var cols = new[] { new Color(1f, 0.62f, 0.72f), new Color(1f, 0.88f, 0.4f), new Color(0.82f, 0.74f, 1f), new Color(1f, 0.72f, 0.5f) };
            for (int i = 0; i < 4; i++)
            {
                float x = -0.72f + i * 0.2f;
                P(root, PrimitiveType.Cylinder, "GoodPot", new Vector3(x, 0.325f, 1.0f), new Vector3(0.1f, 0.045f, 0.1f), pot);
                P(root, PrimitiveType.Sphere, "GoodLeaf", new Vector3(x, 0.4f, 1.0f), new Vector3(0.09f, 0.08f, 0.09f), leaf);
                P(root, PrimitiveType.Sphere, "GoodBloom", new Vector3(x, 0.46f, 1.0f), Vector3.one * 0.06f, M("MS_ShopBloom" + i, cols[i]));
            }
            // flower box under the window + big pots by the door
            if (!useModel) P(root, PrimitiveType.Cube, "FlowerBox", new Vector3(-0.38f, 0.22f, 0.7f), new Vector3(0.8f, 0.1f, 0.14f), wood);
            if (!useModel) for (int i = 0; i < 5; i++) P(root, PrimitiveType.Sphere, "BoxBloom", new Vector3(-0.68f + i * 0.15f, 0.3f, 0.7f), Vector3.one * 0.1f, M("MS_ShopBloom" + (i % 4), cols[i % 4]));
            for (int i = 0; i < 2; i++)
            {
                P(root, PrimitiveType.Cylinder, "BigPot", new Vector3(1.28f, 0.11f, 0.15f + i * 0.34f), new Vector3(0.26f, 0.11f, 0.26f), pot);
                P(root, PrimitiveType.Sphere, "BigLeaf", new Vector3(1.28f, 0.34f, 0.15f + i * 0.34f), new Vector3(0.3f, 0.32f, 0.3f), leaf);
            }
        }

        static KeeperActor Keeper(Transform root, ShopDefinition def, bool rabbit)
        {
            var kroot = Node(root, "Keeper", new Vector3(-0.4f, 0.04f, 0.74f));
            var fur = M("MS_Keeper_" + def.id, def.keeperColor, 0.05f); var belly = M("MS_KeeperBelly", new Color(1f, 0.95f, 0.88f));
            var dark = M("MS_KeeperEye", new Color(0.22f, 0.15f, 0.12f), 0.3f); var pink = M("MS_KeeperPink", new Color(1f, 0.68f, 0.72f));
            var apron = M("MS_KeeperApron_" + def.id, rabbit ? new Color(0.62f, 0.84f, 0.66f) : new Color(0.62f, 0.78f, 0.95f));
            var body = P(kroot, PrimitiveType.Sphere, "Body", new Vector3(0, 0.14f, 0), new Vector3(0.22f, 0.26f, 0.2f), fur);
            P(kroot, PrimitiveType.Sphere, "Apron", new Vector3(0, 0.12f, 0.075f), new Vector3(0.16f, 0.2f, 0.07f), apron);
            P(kroot, PrimitiveType.Sphere, "ArmL", new Vector3(-0.12f, 0.15f, 0.03f), new Vector3(0.06f, 0.12f, 0.06f), fur, new Vector3(0, 0, -15));
            P(kroot, PrimitiveType.Sphere, "ArmR", new Vector3(0.12f, 0.15f, 0.03f), new Vector3(0.06f, 0.12f, 0.06f), fur, new Vector3(0, 0, 15));
            var head = Node(kroot, "Head", new Vector3(0, 0.34f, 0));
            P(head, PrimitiveType.Sphere, "Skull", Vector3.zero, new Vector3(0.22f, 0.19f, 0.19f), fur);
            P(head, PrimitiveType.Sphere, "Muzzle", new Vector3(0, -0.03f, 0.075f), new Vector3(0.1f, 0.075f, 0.06f), belly);
            P(head, PrimitiveType.Sphere, "Nose", new Vector3(0, -0.012f, 0.108f), new Vector3(0.028f, 0.02f, 0.02f), pink);
            var eyeL = P(head, PrimitiveType.Sphere, "EyeL", new Vector3(-0.052f, 0.02f, 0.088f), new Vector3(0.026f, 0.034f, 0.02f), dark);
            var eyeR = P(head, PrimitiveType.Sphere, "EyeR", new Vector3(0.052f, 0.02f, 0.088f), new Vector3(0.026f, 0.034f, 0.02f), dark);
            P(head, PrimitiveType.Sphere, "CheekL", new Vector3(-0.08f, -0.018f, 0.075f), new Vector3(0.036f, 0.022f, 0.012f), pink);
            P(head, PrimitiveType.Sphere, "CheekR", new Vector3(0.08f, -0.018f, 0.075f), new Vector3(0.036f, 0.022f, 0.012f), pink);
            Transform earL, earR;
            if (rabbit)
            {
                earL = P(head, PrimitiveType.Sphere, "EarL", new Vector3(-0.055f, 0.17f, -0.01f), new Vector3(0.05f, 0.2f, 0.035f), fur, new Vector3(0, 0, 8)).transform;
                earR = P(head, PrimitiveType.Sphere, "EarR", new Vector3(0.055f, 0.17f, -0.01f), new Vector3(0.05f, 0.2f, 0.035f), fur, new Vector3(0, 0, -8)).transform;
                P(earL, PrimitiveType.Sphere, "InnerL", new Vector3(0, 0, 0.3f), new Vector3(0.55f, 0.75f, 0.3f), pink);
                P(earR, PrimitiveType.Sphere, "InnerR", new Vector3(0, 0, 0.3f), new Vector3(0.55f, 0.75f, 0.3f), pink);
                P(head, PrimitiveType.Sphere, "HeadFlower", new Vector3(0.07f, 0.075f, 0.07f), Vector3.one * 0.04f, M("MS_KeeperFlower", new Color(1f, 0.85f, 0.4f)));
            }
            else
            {
                if (def.id == "Shop_Accessory")
                {
                    // the accessory keeper wears a big bow
                    var bow = M("MS_KeeperBow", new Color(1f, 0.5f, 0.64f));
                    P(head, PrimitiveType.Sphere, "BowL", new Vector3(-0.045f, 0.11f, 0.02f), new Vector3(0.07f, 0.05f, 0.03f), bow, new Vector3(0, 0, 25));
                    P(head, PrimitiveType.Sphere, "BowR", new Vector3(0.045f, 0.11f, 0.02f), new Vector3(0.07f, 0.05f, 0.03f), bow, new Vector3(0, 0, -25));
                    P(head, PrimitiveType.Sphere, "BowKnot", new Vector3(0f, 0.11f, 0.03f), Vector3.one * 0.03f, bow);
                }
                earL = P(head, PrimitiveType.Sphere, "EarL", new Vector3(-0.08f, 0.1f, -0.01f), new Vector3(0.06f, 0.07f, 0.04f), fur).transform;
                earR = P(head, PrimitiveType.Sphere, "EarR", new Vector3(0.08f, 0.1f, -0.01f), new Vector3(0.06f, 0.07f, 0.04f), fur).transform;
                P(kroot, PrimitiveType.Sphere, "Tail", new Vector3(0, 0.2f, -0.17f), new Vector3(0.14f, 0.26f, 0.12f), fur, new Vector3(-15, 0, 0));
            }
            // little feet on the ground
            P(kroot, PrimitiveType.Sphere, "FootL", new Vector3(-0.055f, 0.012f, 0.03f), new Vector3(0.07f, 0.03f, 0.1f), fur);
            P(kroot, PrimitiveType.Sphere, "FootR", new Vector3(0.055f, 0.012f, 0.03f), new Vector3(0.07f, 0.03f, 0.1f), fur);
            var col = kroot.gameObject.AddComponent<CapsuleCollider>(); col.center = new Vector3(0, 0.22f, 0); col.radius = 0.14f; col.height = 0.46f;
            var ka = kroot.gameObject.AddComponent<KeeperActor>();
            ka.Body = body.transform; ka.Head = head; ka.EyeL = eyeL.transform; ka.EyeR = eyeR.transform; ka.EarL = earL; ka.EarR = earR;
            return ka;
        }
    }
}

using System.IO;
using System.Linq;
using Mongle.Core;
using Mongle.Photo;
using Mongle.Village;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Mongle.EditorTools
{
    /// <summary>Creates the four scenes (Bootstrap, RepairShop, PhotoStudio, Village) with procedural, cozy primitive sets.</summary>
    public static class SceneBuilder
    {
        const string SceneDir = "Assets/_Project/Scenes/";
        static Material M(string n, Color c, float s = 0.15f, Color? emit = null) => ContentBuilder.GetOrCreateMat(n, c, s, 0f, emit);

        /// <summary>D-036: stepping stones use the rounded 48-sided pebble mesh instead of the 20-sided cylinder.</summary>
        static GameObject Pebble(GameObject g) { g.GetComponent<MeshFilter>().sharedMesh = VillageDecor.PebbleMesh(); return g; }

        static GameObject Prim(PrimitiveType t, string name, Vector3 pos, Vector3 scale, Material mat, Transform parent = null, Vector3? euler = null, bool collider = false)
        {
            var g = GameObject.CreatePrimitive(t);
            g.name = name; g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale;
            if (euler.HasValue) g.transform.localEulerAngles = euler.Value;
            if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
            g.GetComponent<Renderer>().sharedMaterial = mat;
            return g;
        }

        static Camera MakeCamera(Vector3 pos, Vector3 look, float fov, Color bg)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = bg; cam.fieldOfView = fov; cam.nearClipPlane = 0.03f; cam.farClipPlane = 60f;
            go.AddComponent<AudioListener>();
            go.transform.position = pos; go.transform.LookAt(look);
            return cam;
        }

        static void Light(string name, LightType type, Vector3 euler, Vector3 pos, Color c, float intensity, float range = 10f, LightShadows sh = LightShadows.Soft)
        {
            var go = new GameObject(name); var l = go.AddComponent<Light>();
            l.type = type; l.color = c; l.intensity = intensity; l.range = range; l.shadows = sh;
            go.transform.position = pos; go.transform.eulerAngles = euler;
        }

        static void Ambient(Color sky, Color eq, Color ground)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = eq; RenderSettings.ambientGroundColor = ground;
            RenderSettings.fog = false;
        }

        static Scene New() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        static void Save(Scene s, string name) { Directory.CreateDirectory(SceneDir); EditorSceneManager.SaveScene(s, SceneDir + name + ".unity"); }

        [MenuItem("Mongle/Build Scenes")]
        public static void BuildAll()
        {
            ConfigureProject();
            var prev = EditorSceneManager.GetActiveScene().path;
            Bootstrap(); RepairShop(); PhotoStudio(); Village();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SceneDir + "Bootstrap.unity", true), new EditorBuildSettingsScene(SceneDir + "RepairShop.unity", true),
                new EditorBuildSettingsScene(SceneDir + "PhotoStudio.unity", true), new EditorBuildSettingsScene(SceneDir + "Village.unity", true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] scenes built");
        }

        // ---------------- project settings (this project only) ----------------
        public static void ConfigureProject()
        {
            PlayerSettings.productName = "MongleMongle";
            PlayerSettings.companyName = "Mongle";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.mongle.repairshop");
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneOSX, true);
            // small agent for a 0.45m toy
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
            if (assets != null && assets.Length > 0)
            {
                var so = new SerializedObject(assets[0]); var arr = so.FindProperty("m_Settings");
                if (arr != null && arr.arraySize > 0)
                {
                    var s0 = arr.GetArrayElementAtIndex(0);
                    s0.FindPropertyRelative("agentRadius").floatValue = 0.1f; s0.FindPropertyRelative("agentHeight").floatValue = 0.5f;
                    s0.FindPropertyRelative("agentClimb").floatValue = 0.08f; s0.FindPropertyRelative("agentSlope").floatValue = 40f;
                    so.ApplyModifiedProperties();
                }
            }
        }


        // ---------------- RepairShop details ----------------
        static void Frame(Transform parent, Vector3 pos, Vector2 size, Vector3 euler, Material frame, Material art, bool round = false)
        {
            var f = Prim(PrimitiveType.Cube, "FrameBack", pos, new Vector3(size.x, size.y, 0.04f), frame, parent, euler);
            var inner = Prim(round ? PrimitiveType.Sphere : PrimitiveType.Cube, "FrameArt", Vector3.zero, new Vector3(size.x * 0.72f, size.y * 0.72f, round ? 0.03f : 0.03f), art, f.transform);
            inner.transform.localPosition = new Vector3(0, 0, 0.6f); inner.transform.localScale = new Vector3(0.72f, 0.72f, 0.8f);
        }

        static void ShopDecor(Transform room, Material wood, Material woodDark, Material wall, float d)
        {
            var cream = M("M_Cream", new Color(1f, 0.96f, 0.88f), 0.1f);
            var pink = M("M_FeltPink", new Color(1f, 0.74f, 0.78f), 0.05f);
            var blue = M("M_FeltBlue", new Color(0.72f, 0.86f, 1f), 0.05f);
            var yellow = M("M_FeltYellow", new Color(1f, 0.9f, 0.58f), 0.05f);
            var mint = M("M_FeltMint", new Color(0.74f, 0.9f, 0.8f), 0.05f);
            var rug = M("M_Rug", new Color(0.96f, 0.76f, 0.72f), 0.05f);
            var rugB = M("M_RugRing", new Color(0.99f, 0.9f, 0.8f), 0.05f);
            var curtain = M("M_Curtain", new Color(1f, 0.8f, 0.82f), 0.05f);
            var plantLeaf = M("M_PlantLeaf", new Color(0.5f, 0.75f, 0.5f), 0.1f);
            var pot = M("M_PotTerracotta", new Color(0.85f, 0.55f, 0.4f), 0.2f);
            var lampShade = M("M_LampShade", new Color(1f, 0.9f, 0.65f), 0.3f, new Color(1.5f, 1.2f, 0.7f));
            var floorA = M("M_FloorA", new Color(0.62f, 0.45f, 0.32f), 0.2f);
            var floorB = M("M_FloorB", new Color(0.56f, 0.4f, 0.28f), 0.2f);
            var wainscot = M("M_Wainscot", new Color(0.86f, 0.93f, 0.86f), 0.1f);

            // floor planks + rug
            for (int i = 0; i < 16; i++)
                Prim(PrimitiveType.Cube, "Plank", new Vector3(0, -0.842f, -3.75f + i * 0.5f), new Vector3(7.9f, 0.01f, 0.48f), i % 2 == 0 ? floorA : floorB, room);
            Prim(PrimitiveType.Cylinder, "Rug", new Vector3(0, -0.832f, 0), new Vector3(3.6f, 0.004f, 3.6f), rugB, room);
            Prim(PrimitiveType.Cylinder, "RugInner", new Vector3(0, -0.828f, 0), new Vector3(2.9f, 0.004f, 2.9f), rug, room);

            // wainscot + rail along the back wall
            Prim(PrimitiveType.Cube, "Wainscot", new Vector3(0, 0.35f, -d + 0.07f), new Vector3(8f, 0.9f, 0.03f), wainscot, room);
            Prim(PrimitiveType.Cube, "ChairRail", new Vector3(0, 0.82f, -d + 0.08f), new Vector3(8f, 0.05f, 0.05f), cream, room);

            // window: frame, sill, curtains
            Prim(PrimitiveType.Cube, "WindowFrameT", new Vector3(-1.6f, 1.83f, -d + 0.09f), new Vector3(1.7f, 0.08f, 0.06f), cream, room);
            Prim(PrimitiveType.Cube, "WindowFrameB", new Vector3(-1.6f, 0.57f, -d + 0.09f), new Vector3(1.7f, 0.08f, 0.06f), cream, room);
            Prim(PrimitiveType.Cube, "WindowFrameL", new Vector3(-2.4f, 1.2f, -d + 0.09f), new Vector3(0.08f, 1.3f, 0.06f), cream, room);
            Prim(PrimitiveType.Cube, "WindowFrameR", new Vector3(-0.8f, 1.2f, -d + 0.09f), new Vector3(0.08f, 1.3f, 0.06f), cream, room);
            Prim(PrimitiveType.Cube, "WindowSill", new Vector3(-1.6f, 0.52f, -d + 0.2f), new Vector3(1.9f, 0.05f, 0.28f), wood, room);
            Prim(PrimitiveType.Cube, "CurtainL", new Vector3(-2.55f, 1.22f, -d + 0.16f), new Vector3(0.35f, 1.5f, 0.07f), curtain, room);
            Prim(PrimitiveType.Cube, "CurtainR", new Vector3(-0.65f, 1.22f, -d + 0.16f), new Vector3(0.35f, 1.5f, 0.07f), curtain, room);
            Prim(PrimitiveType.Cylinder, "CurtainRod", new Vector3(-1.6f, 1.98f, -d + 0.16f), new Vector3(0.03f, 1.15f, 0.03f), woodDark, room, new Vector3(0, 0, 90));
            // plant on the sill
            Prim(PrimitiveType.Cylinder, "SillPot", new Vector3(-2.05f, 0.62f, -d + 0.2f), new Vector3(0.16f, 0.07f, 0.16f), pot, room);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Sphere, "SillLeaf", new Vector3(-2.05f + (i - 1.5f) * 0.05f, 0.82f + (i % 2) * 0.06f, -d + 0.2f), new Vector3(0.14f, 0.2f, 0.1f), plantLeaf, room, new Vector3(0, 0, (i - 1.5f) * 18f));

            // framed felt pictures
            Frame(room, new Vector3(0.1f, 1.45f, -d + 0.07f), new Vector2(0.6f, 0.75f), Vector3.zero, woodDark, pink, true);
            Frame(room, new Vector3(2.35f, 1.7f, -d + 0.07f), new Vector2(0.7f, 0.5f), Vector3.zero, woodDark, blue);
            Frame(room, new Vector3(1.25f, 1.9f, -d + 0.07f), new Vector2(0.45f, 0.45f), new Vector3(0, 0, 6), woodDark, yellow, true);
            Frame(room, new Vector3(-d + 0.07f, 1.3f, -0.8f), new Vector2(0.6f, 0.7f), new Vector3(0, 90, 0), woodDark, mint); // side wall (thin)
            Frame(room, new Vector3(-d + 0.07f, 1.45f, 1.1f), new Vector2(0.5f, 0.5f), new Vector3(0, 90, 0), woodDark, pink, true);

            // shelf extras: book stack + little plant
            for (int i = 0; i < 3; i++)
                Prim(PrimitiveType.Cube, "Book", new Vector3(2.45f, 0.97f + i * 0.05f, -d + 0.3f), new Vector3(0.28f - i * 0.03f, 0.045f, 0.2f), i == 0 ? pink : i == 1 ? blue : yellow, room, new Vector3(0, i * 9f, 0));
            Prim(PrimitiveType.Cylinder, "ShelfPot", new Vector3(2.0f, 0.99f, -d + 0.3f), new Vector3(0.12f, 0.05f, 0.12f), pot, room);
            Prim(PrimitiveType.Sphere, "ShelfLeaf", new Vector3(2.0f, 1.09f, -d + 0.3f), new Vector3(0.14f, 0.12f, 0.14f), plantLeaf, room);

            // counter front: paneled doors with knobs
            var panel = M("M_CounterPanel", new Color(0.86f, 0.68f, 0.5f), 0.2f);
            for (int i = 0; i < 7; i++)
            {
                float px = -3.0f + i * 1.0f;
                Prim(PrimitiveType.Cube, "CounterPanel", new Vector3(px, -0.15f, -d + 0.8f + 0.355f), new Vector3(0.86f, 0.95f, 0.02f), panel, room);
                Prim(PrimitiveType.Sphere, "CounterKnob", new Vector3(px + 0.3f, -0.05f, -d + 0.8f + 0.385f), Vector3.one * 0.06f, yellow, room);
            }
            // counter props: jars with colored tools, a bell, spools
            var jar = M("M_Jar", new Color(0.95f, 0.97f, 1f), 0.5f);
            for (int j = 0; j < 3; j++)
            {
                float x = -0.3f + j * 0.34f;
                Prim(PrimitiveType.Cylinder, "Jar", new Vector3(x, 0.62f, -d + 0.8f), new Vector3(0.15f, 0.1f, 0.15f), jar, room);
                for (int k = 0; k < 3; k++)
                    Prim(PrimitiveType.Cylinder, "JarTool", new Vector3(x + (k - 1) * 0.035f, 0.78f, -d + 0.8f), new Vector3(0.018f, 0.1f, 0.018f), k == 0 ? pink : k == 1 ? blue : yellow, room, new Vector3((k - 1) * 8f, 0, (k - 1) * -8f));
            }
            for (int i = 0; i < 3; i++)
                Prim(PrimitiveType.Cylinder, "Spool", new Vector3(1.4f + (i % 2) * 0.22f, 0.53f + (i / 2) * 0.12f, -d + 0.78f), new Vector3(0.16f, 0.05f, 0.16f), i == 0 ? mint : i == 1 ? pink : yellow, room);
            Prim(PrimitiveType.Sphere, "CounterBell", new Vector3(-1.0f, 0.55f, -d + 0.8f), new Vector3(0.16f, 0.14f, 0.16f), yellow, room);

            // cushion pile in the corner + floor plant
            Prim(PrimitiveType.Sphere, "Cushion", new Vector3(3.2f, -0.62f, -2.6f), new Vector3(0.7f, 0.4f, 0.7f), pink, room);
            Prim(PrimitiveType.Sphere, "CushionB", new Vector3(2.7f, -0.67f, -2.4f), new Vector3(0.55f, 0.32f, 0.55f), blue, room);
            Prim(PrimitiveType.Cylinder, "FloorPot", new Vector3(-3.3f, -0.62f, -2.5f), new Vector3(0.45f, 0.22f, 0.45f), pot, room);
            for (int i = 0; i < 5; i++)
                Prim(PrimitiveType.Sphere, "FloorLeaf", new Vector3(-3.3f + Mathf.Cos(i * 1.26f) * 0.12f, -0.2f + (i % 2) * 0.12f, -2.5f + Mathf.Sin(i * 1.26f) * 0.12f), new Vector3(0.28f, 0.5f, 0.2f), plantLeaf, room, new Vector3(Mathf.Sin(i * 1.26f) * 20f, i * 70f, Mathf.Cos(i * 1.26f) * 20f));

            // hanging lamp over the table
            Prim(PrimitiveType.Cylinder, "LampCord", new Vector3(0.5f, 2.35f, -0.4f), new Vector3(0.012f, 0.9f, 0.012f), woodDark, room);
            Prim(PrimitiveType.Sphere, "LampShade", new Vector3(0.5f, 1.45f, -0.4f), new Vector3(0.34f, 0.22f, 0.34f), lampShade, room);
        }

        // ---------------- Bootstrap ----------------
        static void Bootstrap()
        {
            var s = New();
            MakeCamera(new Vector3(0, 0, -5), Vector3.zero, 30, new Color(1f, 0.957f, 0.878f));
            new GameObject("Bootstrap").AddComponent<BootstrapController>();
            TitleBuild.AddTo(s);   // the start screen (11-26 design), edited in its prefab Assets/_UI수정/1_타이틀/Title.prefab
            Save(s, "Bootstrap");
        }

        // ---------------- RepairShop ----------------
        static void RepairShop()
        {
            var s = New();
            Ambient(new Color(1f, 0.96f, 0.9f), new Color(0.86f, 0.76f, 0.66f), new Color(0.5f, 0.4f, 0.33f));
            var cam = MakeCamera(new Vector3(0, 0.42f, 1.4f), new Vector3(0, 0.22f, 0), 34, new Color(0.96f, 0.86f, 0.74f));
            cam.gameObject.AddComponent<OrbitCamera>();
            LookPostFx.AddTo(cam, "VP_Shop", LookPostFx.Shop);
            Light("Sun", LightType.Directional, new Vector3(48, -28, 0), Vector3.zero, new Color(1f, 0.93f, 0.82f), 1.15f);
            Light("Lamp", LightType.Point, Vector3.zero, new Vector3(0.5f, 1.15f, 0.7f), new Color(1f, 0.85f, 0.65f), 1.6f, 5f, LightShadows.None);
            Light("WindowGlow", LightType.Point, Vector3.zero, new Vector3(-1.5f, 0.9f, -0.6f), new Color(0.8f, 0.9f, 1f), 0.9f, 5f, LightShadows.None);

            var wood = M("M_Wood", new Color(0.72f, 0.5f, 0.32f), 0.25f);
            var woodDark = M("M_WoodDark", new Color(0.52f, 0.36f, 0.24f), 0.2f);
            var felt = M("M_WorkMat", new Color(0.93f, 0.78f, 0.74f), 0.05f);
            var wall = M("M_Wall", new Color(0.99f, 0.93f, 0.82f), 0.05f);
            var wallB = M("M_WallAccent", new Color(0.78f, 0.9f, 0.84f), 0.05f);
            var room = new GameObject("Room").transform;
            Prim(PrimitiveType.Cylinder, "Table", new Vector3(0, -0.035f, 0), new Vector3(1.6f, 0.035f, 1.6f), wood, room);
            Prim(PrimitiveType.Cylinder, "WorkMat", new Vector3(0, 0.0015f, 0), new Vector3(0.95f, 0.002f, 0.95f), felt, room);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f * Mathf.Deg2Rad + 0.785f;
                Prim(PrimitiveType.Cylinder, "Leg" + i, new Vector3(Mathf.Cos(a) * 0.55f, -0.45f, Mathf.Sin(a) * 0.55f), new Vector3(0.1f, 0.4f, 0.1f), woodDark, room);
            }
            Prim(PrimitiveType.Plane, "Floor", new Vector3(0, -0.85f, 0), new Vector3(0.8f, 1, 0.8f), woodDark, room);
            float d = 4f, h = 2.2f;
            Prim(PrimitiveType.Cube, "WallBack", new Vector3(0, h - 0.85f, -d), new Vector3(8, 2 * h, 0.1f), wall, room);
            Prim(PrimitiveType.Cube, "WallFront", new Vector3(0, h - 0.85f, d), new Vector3(8, 2 * h, 0.1f), wall, room);
            Prim(PrimitiveType.Cube, "WallLeft", new Vector3(-d, h - 0.85f, 0), new Vector3(0.1f, 2 * h, 8), wallB, room);
            Prim(PrimitiveType.Cube, "WallRight", new Vector3(d, h - 0.85f, 0), new Vector3(0.1f, 2 * h, 8), wallB, room);
            Prim(PrimitiveType.Cube, "Ceiling", new Vector3(0, 2 * h - 0.85f, 0), new Vector3(8, 0.1f, 8), wall, room);
            // window with warm glow
            var glow = M("M_WindowGlow", new Color(1f, 0.95f, 0.8f), 0.1f, new Color(1.2f, 1.05f, 0.7f));
            Prim(PrimitiveType.Cube, "Window", new Vector3(-1.6f, 1.2f, -d + 0.07f), new Vector3(1.5f, 1.2f, 0.04f), glow, room);
            Prim(PrimitiveType.Cube, "WindowBarV", new Vector3(-1.6f, 1.2f, -d + 0.1f), new Vector3(0.05f, 1.2f, 0.04f), woodDark, room);
            Prim(PrimitiveType.Cube, "WindowBarH", new Vector3(-1.6f, 1.2f, -d + 0.1f), new Vector3(1.5f, 0.05f, 0.04f), woodDark, room);
            // shelf with toy blocks
            Prim(PrimitiveType.Cube, "Shelf", new Vector3(1.6f, 0.9f, -d + 0.3f), new Vector3(2f, 0.06f, 0.5f), wood, room);
            var cols = new[] { new Color(1f, 0.7f, 0.74f), new Color(0.7f, 0.85f, 1f), new Color(1f, 0.9f, 0.55f), new Color(0.72f, 0.9f, 0.76f), new Color(0.88f, 0.76f, 1f) };
            for (int i = 0; i < 5; i++)
                Prim(i % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere, "Block" + i, new Vector3(0.95f + i * 0.36f, 1.03f, -d + 0.3f), Vector3.one * 0.2f, M("M_Block" + i, cols[i], 0.3f), room);
            var counterM = M("M_Counter", new Color(0.8f, 0.62f, 0.44f), 0.2f);
            Prim(PrimitiveType.Cube, "Counter", new Vector3(0, -0.2f, -d + 0.8f), new Vector3(7, 1.3f, 0.7f), counterM, room);
            Prim(PrimitiveType.Cube, "CounterTop", new Vector3(0, 0.46f, -d + 0.78f), new Vector3(7.1f, 0.06f, 0.8f), wood, room);
            ShopDecor(room, wood, woodDark, wall, d);
            new GameObject("ShopDirector").AddComponent<ShopDirector>();
            RepairHudBuild.AddTo(s);   // the 11-26 repair screen (Assets/_UI수정/2_수선점/RepairHud.prefab)
            Save(s, "RepairShop");
        }

        // ---------------- PhotoStudio ----------------
        static void PhotoStudio()
        {
            var s = New();
            Ambient(new Color(1f, 0.95f, 0.9f), new Color(0.9f, 0.82f, 0.75f), new Color(0.6f, 0.5f, 0.45f));
            var studioCam = MakeCamera(new Vector3(0, 0.27f, 1.35f), new Vector3(0, 0.235f, 0), 24, new Color(0.99f, 0.86f, 0.8f));
            LookPostFx.AddTo(studioCam, "VP_Studio", LookPostFx.Studio);
            Light("Key", LightType.Directional, new Vector3(35, -30, 0), Vector3.zero, new Color(1f, 0.94f, 0.85f), 1.2f);
            Light("Fill", LightType.Point, Vector3.zero, new Vector3(-0.8f, 0.6f, 1.0f), new Color(1f, 0.85f, 0.85f), 1.2f, 5f, LightShadows.None);
            StudioSet(new GameObject("Set").transform);
            new GameObject("PhotoDirector").AddComponent<PhotoDirector>();
            PhotoBuild.AddTo(s);   // the 11-26 photo screen (Assets/_UI수정/4_사진관/Photo.prefab)
            Save(s, "PhotoStudio");
        }

        /// <summary>
        /// The studio's little garden set. The viewfinder is a window in the middle of the screen (11-26 design), so the camera
        /// sees about four times as wide as the photo keeps: the floor, the wall, the hedge and the light garlands run well
        /// past the screen's edges at every zoom and screen shape. Pieces are found by name, so this also updates a built scene.
        /// </summary>
        public static void StudioSet(Transform set)
        {
            Piece(set, PrimitiveType.Cylinder, "Floor", new Vector3(0, -0.01f, 0), new Vector3(16f, 0.01f, 16f), M("M_StudioFloor", new Color(0.99f, 0.93f, 0.84f), 0.05f));
            Piece(set, PrimitiveType.Cube, "Backdrop", new Vector3(0, 1.5f, -0.9f), new Vector3(14f, 4.4f, 0.04f), M("M_BackdropPeach", new Color(1f, 0.82f, 0.74f), 0.02f));
            Piece(set, PrimitiveType.Cube, "BackdropLow", new Vector3(0, 0.12f, -0.87f), new Vector3(14f, 0.5f, 0.04f), M("M_BackdropMint", new Color(0.76f, 0.91f, 0.82f), 0.02f));
            var leaf = M("M_Leaf", new Color(0.55f, 0.78f, 0.5f), 0.1f);
            // the hedge along the wall: the nine in the middle keep their names (Hedge0..8), more run out to both sides
            for (int i = -26; i <= 34; i++) Piece(set, PrimitiveType.Sphere, i >= 0 && i < 9 ? "Hedge" + i : "HedgeMore" + i, new Vector3(-0.8f + i * 0.2f, 0.13f, -0.8f), Vector3.one * 0.34f, leaf);
            var petals = new[] { new Color(1f, 0.6f, 0.7f), new Color(1f, 0.85f, 0.45f), new Color(0.75f, 0.7f, 1f), new Color(1f, 0.75f, 0.55f) };
            for (int i = 0; i < 8; i++)
            {
                float x = i < 4 ? -0.55f - i * 0.12f : 0.5f + (i - 4) * 0.12f; float z = -0.15f + (i % 3) * 0.08f;
                Piece(set, PrimitiveType.Cylinder, "Stem" + i, new Vector3(x, 0.05f, z), new Vector3(0.012f, 0.05f, 0.012f), leaf);
                Piece(set, PrimitiveType.Sphere, "Bloom" + i, new Vector3(x, 0.11f, z), Vector3.one * 0.07f, M("M_Bloom" + (i % 4), petals[i % 4], 0.2f));
            }
            // the light garland: one swag behind the toy (Bulb0..10), and the same swag repeated to both sides
            var bulb = M("M_Bulb", new Color(1f, 0.95f, 0.7f), 0.5f, new Color(1.6f, 1.4f, 0.8f));
            for (int k = -3; k <= 3; k++)
                for (int i = 0; i < 11; i++)
                {
                    if (k > 0 && i == 0 || k < 0 && i == 10) continue;   // neighbours share the bulb where they meet
                    float t = i / 10f; float x = Mathf.Lerp(-0.85f, 0.85f, t) + k * 1.7f;
                    Piece(set, PrimitiveType.Sphere, k == 0 ? "Bulb" + i : "Bulb" + i + (k < 0 ? "L" + -k : "R" + k), new Vector3(x, 0.62f - Mathf.Sin(t * Mathf.PI) * 0.12f, -0.82f), Vector3.one * 0.035f, bulb);
                }
        }

        /// <summary>A primitive of the set by name: made if it is not there yet, then put in its place.</summary>
        static GameObject Piece(Transform set, PrimitiveType t, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var found = set.Find(name);
            if (found == null) return Prim(t, name, pos, scale, mat, set);
            found.localPosition = pos; found.localScale = scale;
            var r = found.GetComponent<Renderer>(); if (r != null) r.sharedMaterial = mat;
            return found.gameObject;
        }

        // ---------------- Village ----------------
        // D-006: one big park that holds everything. First the old 6.7 x 5.3 m garden grew 5x (16 x 11.1 m), then the user
        // asked for 5x that again with every outside element brought in: the park is now 35.8 x 24.8 m (888 m2) and holds the
        // shops, the hospital, the castle, the candy trees, bushes, flowers, lamps and the three rides. Only the rolling hills
        // stay outside as the horizon; balloons and clouds float high over its north half.
        static readonly Vector3 ParkCenter = new Vector3(0f, 0f, 2.9f);
        const float WalkHalfX = 17.9f, WalkHalfZ = 12.4f;        // walk area 35.8 x 24.8 (x -17.9..17.9, z -9.5..15.3)
        const float LawnHalfX = 18.6f, LawnHalfZ = 13.1f;        // lawn a little wider than the walk area
        const float PathHalfX = 16.9f, PathHalfZ = 11.4f;        // inner edge of the promenade ring (the ring itself is walkable)
        // park rides (yaw: the boarding side faces the default camera)
        static readonly Vector3 CarouselPos = new Vector3(-8f, 0f, 9.5f);
        const float CarouselHeight = 4.5f, CarouselYaw = 95f;   // user: bigger again (3 m -> 4.5 m)
        static readonly Vector3 WheelPos = new Vector3(12f, 0f, -2.5f);
        const float WheelYaw = -12f;
        static readonly Vector3 BalloonPos = new Vector3(-12f, 0f, -3f);
        const float BalloonYaw = 38f;
        static readonly Vector3 HospitalPos = new Vector3(0f, 0f, -4.35f);
        const float HospitalHeight = 2.3f;
        static readonly Vector3 CastlePos = new Vector3(0f, 0f, -8.2f);
        const float CastleScale = 1.2f;
        // D-011/D-012: the playground west of the fountain, the cake fountain between the old garden and the carousel,
        // friends' house lots east of the old garden (HouseLots)
        static readonly Vector3 PlaygroundPos = new Vector3(0.5f, 0f, 9.6f);   // user: the lawn inside the track loop, west of the house lots
        const float PlaygroundYaw = 0f;
        static readonly Vector3 FountainPos = new Vector3(-5.5f, 0f, 3.0f);
        // D-014: the claw machine stands just east of the third shop, facing the camera
        static readonly Vector3 ClawPos = new Vector3(8.2f, 0f, -4.2f);
        // the cake train winds through the village (user: not a square round the outside): north of the house lots, down the
        // east side, along the south lawn, round the carousel's west side, between the carousel and the fountain and back past
        // the hub. Checked against every walk hole, ride stop and house lot (>= 0.5 m from the track bed). Station north of the houses.
        static readonly Vector3[] TrainWaypoints =
        {
            new Vector3(6.0f, 0f, 1.8f), new Vector3(10.5f, 0f, 1.4f), new Vector3(14.4f, 0f, 2.4f), new Vector3(15.8f, 0f, 5.6f), new Vector3(15.9f, 0f, 9.6f),
            new Vector3(15.4f, 0f, 12.8f), new Vector3(11.0f, 0f, 13.5f), new Vector3(3.0f, 0f, 13.5f), new Vector3(-5.0f, 0f, 13.5f), new Vector3(-11.0f, 0f, 13.2f),
            new Vector3(-12.8f, 0f, 10.8f), new Vector3(-12.2f, 0f, 8.0f), new Vector3(-10.0f, 0f, 5.7f), new Vector3(-6.0f, 0f, 5.8f), new Vector3(-2.6f, 0f, 6.4f),
            new Vector3(0.4f, 0f, 6.4f), new Vector3(2.0f, 0f, 4.6f), new Vector3(3.7f, 0f, 2.5f),
        };
        static readonly Vector3 TrainStation = new Vector3(7.0f, 0f, 0.55f), TrainStop = new Vector3(8.6f, 0f, 1.6f);
        const float TrackHoleStep = 0.8f, CrossingHalf = 0.9f, MaxCrossingGap = 12f;
        public static Vector3[] TrainLoop() => LoopPath.CatmullRomClosed(TrainWaypoints, 0.25f).ToArray();

        static Vector3 Flat(Vector3 p) => new Vector3(p.x, 0f, p.z);
        /// <summary>Do ground segments a-b and c-d cross? t = where along a-b.</summary>
        static bool SegCross(Vector3 a, Vector3 b, Vector3 c, Vector3 d, out float t)
        {
            t = 0f;
            float r_x = b.x - a.x, r_z = b.z - a.z, s_x = d.x - c.x, s_z = d.z - c.z;
            float den = r_x * s_z - r_z * s_x; if (Mathf.Abs(den) < 1e-6f) return false;
            float qx = c.x - a.x, qz = c.z - a.z;
            t = (qx * s_z - qz * s_x) / den; float u = (qx * r_z - qz * r_x) / den;
            return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
        }
        static float DistToSeg(Vector3 p, Vector3 a, Vector3 b)
        {
            p = Flat(p); a = Flat(a); b = Flat(b); var ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }

        static void Village()
        {
            var s = New();
            // dreamy daytime amusement park (D-004): pastel sky and haze, minty lawn, candy scenery
            Ambient(new Color(0.9f, 0.9f, 1f), new Color(1f, 0.92f, 0.94f), new Color(0.7f, 0.78f, 0.7f));
            var villageCam = MakeCamera(new Vector3(0, 14f, 20f), ParkCenter, 32, new Color(0.9f, 0.86f, 0.98f));
            villageCam.gameObject.AddComponent<OrbitCamera>();
            villageCam.farClipPlane = 300f;
            LookPostFx.AddTo(villageCam, "VP_Village", LookPostFx.Village);
            VillageDecor.Sky(villageCam);
            Light("Sun", LightType.Directional, new Vector3(52, -38, 0), Vector3.zero, new Color(1f, 0.95f, 0.92f), 1.25f);
            var world = new GameObject("World").transform;
            var grass = M("M_Grass", new Color(0.64f, 0.86f, 0.6f), 0.05f);
            var grassOuter = M("M_GrassOuter", new Color(0.58f, 0.81f, 0.6f), 0.05f);
            var grassPatchA = M("M_GrassPatchA", new Color(0.67f, 0.87f, 0.62f), 0.05f);
            var grassPatchB = M("M_GrassPatchB", new Color(0.61f, 0.84f, 0.6f), 0.05f);
            var ground = Prim(PrimitiveType.Cube, "Ground", ParkCenter + new Vector3(0, -0.05f, 0), new Vector3(LawnHalfX * 2f, 0.1f, LawnHalfZ * 2f), grass, world, null, false);
            ground.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            // wide meadow around the park so the horizon hills stand on something
            Prim(PrimitiveType.Cube, "Meadow", ParkCenter + new Vector3(0, -0.075f, 0), new Vector3(140f, 0.1f, 110f), grassOuter, world);
            var rng = new System.Random(3);
            float R() => (float)rng.NextDouble();
            // soft rolling hills on the horizon (the only things left outside the park)
            for (int i = 0; i < 24; i++)
            {
                float ang = i / 24f * Mathf.PI * 2f + R() * 0.25f; float rx = 27f + R() * 6f, rz = 21f + R() * 5f;
                float w = 5f + R() * 4f;
                Prim(PrimitiveType.Sphere, "Hill", ParkCenter + new Vector3(Mathf.Cos(ang) * rx, -0.2f, Mathf.Sin(ang) * rz), new Vector3(w, 1.0f + R() * 1.4f, w * 0.8f), i % 2 == 0 ? grassOuter : grass, world);
            }
            // lawn patches (tone variety)
            for (int i = 0; i < 60; i++)
            {
                float px = Mathf.Lerp(-PathHalfX + 0.6f, PathHalfX - 0.6f, R()), pz = ParkCenter.z + Mathf.Lerp(-PathHalfZ + 0.6f, PathHalfZ - 0.6f, R()); float rr = 0.5f + R() * 1.1f;
                var patch = Prim(PrimitiveType.Cylinder, "Patch", new Vector3(px, 0.0015f + i * 0.00002f, pz), new Vector3(rr * 2f, 0.001f, rr * 1.6f), i % 2 == 0 ? grassPatchA : grassPatchB, world);
                patch.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off; patch.GetComponent<MeshFilter>().sharedMesh = VillageDecor.DiscMesh();   // D-036
            }
            // ---- where the big things stand (used to keep trees, bushes and flowers clear of them) ----
            var keepOut = new System.Collections.Generic.List<(Vector3 c, float r)>
            {
                (CarouselPos, 0.66f * CarouselHeight + 1.2f), (WheelPos, 3.2f), (BalloonPos, 2.4f), (HospitalPos, 2.6f),
                (new Vector3(-2.3f, 0, -3.95f), 1.9f), (new Vector3(2.3f, 0, -3.95f), 1.9f), (new Vector3(5.4f, 0, -4.1f), 2.1f), (CastlePos, 3.4f),
                (new Vector3(0f, 0f, 0f), 3.6f),   // the old garden in the middle (starter furniture, residents' first steps)
                (PlaygroundPos, 3.4f), (FountainPos, 2.1f), (ClawPos, 1.6f),
            };
            // (D-015: houses go wherever the player puts them, so no lots are kept clear)
            var pathSegs = new System.Collections.Generic.List<(Vector3 a, Vector3 b)>();
            var stoneSegs = new System.Collections.Generic.List<(Vector3 a, Vector3 b)>();
            var trackPts = TrainLoop();   // trees and bushes keep clear of the track too
            for (int i = 0; i < trackPts.Length; i += 4) pathSegs.Add((trackPts[i], trackPts[(i + 4) % trackPts.Length]));
            bool Clear(Vector3 p, float r)
            {
                foreach (var k in keepOut) if ((Flat(p) - Flat(k.c)).magnitude < k.r + r) return false;
                foreach (var sg in pathSegs) if (DistToSeg(p, sg.a, sg.b) < 0.75f + r) return false;
                return true;
            }
            // stepping-stone paths: the old garden path (west->center, north-south), then out to every ride and the castle
            var stone = M("M_Path", new Color(1f, 0.74f, 0.83f), 0.05f);
            var stone2 = M("M_PathB", new Color(1f, 0.87f, 0.68f), 0.05f);
            int si = 0;
            for (float x = -3.45f; x < -0.15f; x += 0.38f, si++)
                Pebble(Prim(PrimitiveType.Cylinder, "Stone", new Vector3(x, 0.006f, 0.2f + (si % 2 == 0 ? 0.04f : -0.05f)), new Vector3(0.32f + (si % 3) * 0.03f, 0.008f, 0.3f), si % 2 == 0 ? stone : stone2, world, new Vector3(0, si * 37f, 0)));
            for (float z = -1.0f; z < 1.45f; z += 0.38f, si++)
                Pebble(Prim(PrimitiveType.Cylinder, "Stone", new Vector3(-0.3f + (si % 2 == 0 ? 0.04f : -0.05f), 0.006f, z), new Vector3(0.3f, 0.008f, 0.32f + (si % 3) * 0.03f), si % 2 == 0 ? stone : stone2, world, new Vector3(0, si * 29f, 0)));
            void StonePath(Vector3 a, Vector3 b)
            {
                pathSegs.Add((a, b)); stoneSegs.Add((a, b));
                var d = b - a; d.y = 0; float len = d.magnitude; var n = new Vector3(-d.z, 0, d.x) / Mathf.Max(0.01f, len);
                for (float t = 0.19f; t < len - 0.1f; t += 0.38f, si++)
                {
                    var p = a + d * (t / len) + n * (si % 2 == 0 ? 0.045f : -0.045f); p.y = 0.006f;
                    Pebble(Prim(PrimitiveType.Cylinder, "Stone", p, new Vector3(0.31f + (si % 3) * 0.03f, 0.008f, 0.3f), si % 2 == 0 ? stone : stone2, world, new Vector3(0, si * 33f, 0)));
                }
            }
            var hubStone = new Vector3(-0.3f, 0f, 1.6f);
            Vector3 Board(Vector3 pos, float yaw, Vector3 local) => pos + Quaternion.Euler(0, yaw, 0) * local;
            // carousel boarding spot: half a metre out from its stairs (stairs sit at (-0.425, 0.384) of the 1-unit model)
            var carouselBoard = Mongle.Village.CarouselMath.BoardLocal(CarouselHeight);
            var wheelBoard = new Vector3(0f, 0f, 1.05f);
            var balloonBoard = new Vector3(0f, 0f, 0.95f);
            StonePath(hubStone, Board(CarouselPos, CarouselYaw, carouselBoard));
            StonePath(hubStone, Board(WheelPos, WheelYaw, wheelBoard));
            StonePath(hubStone, Board(BalloonPos, BalloonYaw, balloonBoard));
            StonePath(new Vector3(-3.6f, 0f, 0.2f), new Vector3(-6.5f, 0f, -6.6f));     // round the furniture shop to the castle lawn
            // D-012: the cookie lanes between the rows of house lots, and a path out to the fountain
            StonePath(hubStone, new Vector3(3.9f, 0f, 6.5f));
            StonePath(new Vector3(3.9f, 0f, 6.5f), new Vector3(14.4f, 0f, 6.5f));
            StonePath(new Vector3(3.9f, 0f, 6.5f), new Vector3(3.9f, 0f, 9.8f));
            StonePath(new Vector3(3.9f, 0f, 9.8f), new Vector3(14.4f, 0f, 9.8f));
            StonePath(hubStone, FountainPos + new Vector3(1.5f, 0f, -0.3f));
            StonePath(new Vector3(-6.5f, 0f, -6.6f), CastlePos + new Vector3(-2.4f, 0f, 1.2f));
            var catalog = Mongle.Data.ContentCatalog.Load();
            // ---- promenade ring along the inside of the park edge, star lamps on it, balloon bunches and bunting ----
            VillageDecor.Promenade(world, ParkCenter, PathHalfX, PathHalfZ);
            float lx = PathHalfX + 0.36f, lzN = ParkCenter.z - PathHalfZ - 0.36f, lzS = ParkCenter.z + PathHalfZ + 0.36f;
            var lampSpots = new System.Collections.Generic.List<Vector2>();
            for (int k = 0; k <= 6; k++) { float x = Mathf.Lerp(-lx, lx, k / 6f); lampSpots.Add(new Vector2(x, lzS)); lampSpots.Add(new Vector2(x, lzN)); }
            for (int k = 1; k < 4; k++) { float z = Mathf.Lerp(lzN, lzS, k / 4f); lampSpots.Add(new Vector2(-lx, z)); lampSpots.Add(new Vector2(lx, z)); }
            var lamps = VillageDecor.Lamps(world, lampSpots.ToArray());
            // corners: 0 = south-west, 1 = north-west, 12 = south-east, 13 = north-east
            VillageDecor.Balloons(world, lamps[0] + new Vector3(0, 0.1f, 0), 5, 0);
            VillageDecor.Balloons(world, lamps[1] + new Vector3(0, 0.1f, 0), 4, 2);
            VillageDecor.Balloons(world, lamps[12] + new Vector3(0, 0.1f, 0), 4, 4);
            VillageDecor.Balloons(world, lamps[13] + new Vector3(0, 0.1f, 0), 5, 1);
            VillageDecor.Bunting(world, new Vector3(-1.0f, 0.92f, -3.42f), new Vector3(1.35f, 0.92f, -3.55f), 0.16f, 11); // between the two shops
            for (int k = 0; k < 6; k++) VillageDecor.Bunting(world, lamps[k * 2] + Vector3.up * 0.12f, lamps[k * 2 + 2] + Vector3.up * 0.12f, 0.35f, 17); // along the south side
            // ---- castle at the back of the park, behind the hospital ----
            var castle = VillageDecor.Castle(world, CastlePos, CastleScale);
            // ---- candy trees, puff bushes, gumdrops and flowers spread through the park ----
            var obstacleDiscs = new System.Collections.Generic.List<(Transform t, Vector3 c, float r)>();
            int placed = 0;
            for (int tries = 0; tries < 2000 && placed < 52; tries++)
            {
                var p = ParkCenter + new Vector3(Mathf.Lerp(-PathHalfX + 1.1f, PathHalfX - 1.1f, R()), 0, Mathf.Lerp(-PathHalfZ + 1.1f, PathHalfZ - 1.1f, R()));
                // groves gather along the park edge; a few stand in the open
                float edge = Mathf.Min(PathHalfX - Mathf.Abs(p.x - ParkCenter.x), PathHalfZ - Mathf.Abs(p.z - ParkCenter.z));
                if (edge > 3.2f && R() > 0.18f) continue;
                float sc = 0.95f + R() * 0.8f; int kind = placed % 3;
                if (p.z > ParkCenter.z + PathHalfZ - 3.2f) { kind = 2; sc = 0.8f + R() * 0.3f; } // near the camera: slim lollipops only
                float trunk = kind == 2 ? 0.12f * sc : 0.28f * sc;
                if (!Clear(p, 0.9f * sc)) continue;
                bool crowded = false; foreach (var o in obstacleDiscs) if ((Flat(o.c) - Flat(p)).magnitude < o.r + 0.9f * sc + 0.4f) { crowded = true; break; }
                if (crowded) continue;
                var t = VillageDecor.CandyTree(world, p, sc, kind, placed);
                obstacleDiscs.Add((t, p, Mathf.Max(trunk, 0.2f))); placed++;
            }
            int bushes = 0;
            for (int tries = 0; tries < 2000 && bushes < 46; tries++)
            {
                var p = ParkCenter + new Vector3(Mathf.Lerp(-PathHalfX + 0.6f, PathHalfX - 0.6f, R()), 0, Mathf.Lerp(-PathHalfZ + 0.6f, PathHalfZ - 0.6f, R()));
                float sc = 0.8f + R() * 0.7f;
                if (!Clear(p, 0.4f * sc)) continue;
                bool crowded = false; foreach (var o in obstacleDiscs) if ((Flat(o.c) - Flat(p)).magnitude < o.r + 0.45f * sc + 0.25f) { crowded = true; break; }
                if (crowded) continue;
                if (bushes % 4 == 0) { VillageDecor.Gumdrop(world, p, 0.8f + R() * 0.6f, bushes); obstacleDiscs.Add((null, p, 0.16f)); }
                else { var t = VillageDecor.PuffBush(world, p, sc, bushes); obstacleDiscs.Add((t, p, 0.3f * sc)); }
                bushes++;
            }
            VillageDecor.Far(world, ParkCenter);
            var shrub = M("M_Shrub", new Color(0.56f, 0.82f, 0.62f), 0.1f);
            var bloom = new[] { new Color(1f, 0.72f, 0.84f), new Color(1f, 0.92f, 0.6f), new Color(0.84f, 0.78f, 1f) };
            int flowers = 0;
            for (int tries = 0; tries < 3000 && flowers < 170; tries++)
            {
                var p = ParkCenter + new Vector3(Mathf.Lerp(-PathHalfX + 0.3f, PathHalfX - 0.3f, R()), 0, Mathf.Lerp(-PathHalfZ + 0.3f, PathHalfZ - 0.3f, R()));
                if (!Clear(p, 0.05f)) continue;
                Prim(PrimitiveType.Sphere, "Wild" + flowers, p + new Vector3(0, 0.07f, 0), Vector3.one * 0.07f, M("M_Wild" + (flowers % 3), bloom[flowers % 3], 0.2f), world);
                Prim(PrimitiveType.Cylinder, "WildStem", p + new Vector3(0, 0.035f, 0), new Vector3(0.008f, 0.035f, 0.008f), shrub, world);
                flowers++;
            }
            // shops (D-002): cottages + keepers, one per ShopDefinition
            var buildings = new System.Collections.Generic.List<GameObject>();
            if (catalog != null && catalog.shops != null)
                foreach (var sd in catalog.shops) if (sd != null) buildings.Add(ShopSceneBuilder.Build(sd, world, sd.id == "Shop_Plant"));
            var hospitalPrefab = BackdropBuilder.Hospital();
            if (hospitalPrefab != null)
            {
                var h = (GameObject)PrefabUtility.InstantiatePrefab(hospitalPrefab); h.transform.SetParent(world, false);
                h.transform.localPosition = HospitalPos; h.transform.localScale = Vector3.one * HospitalHeight;
                buildings.Add(h);
            }
            buildings.Add(castle.gameObject);
            // ---- park rides (D-006) ----
            var rides = new GameObject("Rides").transform; rides.SetParent(world, false);
            GameObject Place(GameObject prefab, Vector3 pos, float yaw, float scale)
            {
                if (prefab == null) return null;
                var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab); g.transform.SetParent(rides, false);
                g.transform.localPosition = pos; g.transform.localEulerAngles = new Vector3(0, yaw, 0); g.transform.localScale = Vector3.one * scale;
                return g;
            }
            var carousel = Place(BackdropBuilder.Carousel(), CarouselPos, CarouselYaw, CarouselHeight);
            if (carousel != null) carousel.GetComponent<CarouselRide>().boardLocal = carouselBoard;
            var wheel = Place(BackdropBuilder.WheelRide(), WheelPos, WheelYaw, 1f);
            if (wheel != null) wheel.GetComponent<WheelRide>().boardLocal = wheelBoard;
            var balloon = Place(BackdropBuilder.BalloonRide(), BalloonPos, BalloonYaw, 1f);
            if (balloon != null) balloon.GetComponent<BalloonRide>().boardLocal = balloonBoard;
            var playground = Place(BackdropBuilder.Playground(), PlaygroundPos, PlaygroundYaw, 1f);
            var train = Place(BackdropBuilder.TrainRide(TrainStation, trackPts, TrainStop), TrainStation, 0f, 1f);
            if (train != null) train.GetComponent<TrainRide>().SnapToTrack();
            var fountain = Place(BackdropBuilder.Fountain(), FountainPos, 0f, 1f);
            var clawM = Place(BackdropBuilder.ClawMachine(), ClawPos, 0f, 1f);
            BackdropBuilder.Houses(); // friends' houses are placed while playing (one per resident, D-012)
            // ---- walkable area = the whole park; rides, buildings, trees and bushes are cut out of it ----
            var walk = new GameObject("WalkArea").transform; walk.SetParent(world, false); walk.localPosition = ParkCenter + new Vector3(0, -0.05f, 0);
            var walkBox = walk.gameObject.AddComponent<BoxCollider>(); walkBox.size = new Vector3(WalkHalfX * 2f, 0.1f, WalkHalfZ * 2f);
            void Hole(string name, Vector3 pos, float yaw, Vector3 size)
            {
                var hole = new GameObject(name).transform; hole.SetParent(walk, false);
                hole.position = new Vector3(pos.x, 0f, pos.z); hole.rotation = Quaternion.Euler(0, yaw, 0);
                var vol = hole.gameObject.AddComponent<NavMeshModifierVolume>();
                vol.size = size; vol.center = Vector3.zero; vol.area = 1; // 1 = Not Walkable
            }
            // a rough disc (four strips) with a flat end at the stairs: one axis-aligned square put the boarding spot in its corner
            if (carousel != null)
                foreach (var (yaw, half) in Mongle.Village.CarouselMath.HoleStrips(carousel.GetComponent<CarouselMotion>().FootprintWorldRadius, Mongle.Village.CarouselMath.StairsYaw(CarouselYaw)))
                    Hole("CarouselHole", CarouselPos, yaw, new Vector3(half.x * 2f, 1f, half.y * 2f));
            if (wheel != null) Hole("WheelHole", WheelPos, WheelYaw, new Vector3(4.5f, 1f, 1.3f));
            if (balloon != null) Hole("BalloonHole", BalloonPos, BalloonYaw, new Vector3(1.0f, 1f, 1.0f));
            if (clawM != null) { Hole("ClawHole", ClawPos, 0f, new Vector3(1.5f, 1f, 1.2f)); var cpo = clawM.AddComponent<ParkObstacle>(); cpo.center = ClawPos; cpo.halfSize = new Vector2(0.8f, 0.65f); }
            if (playground != null)
            {
                var lay = playground.GetComponent<PlaygroundLayout>();
                for (int i = 0; i < lay.boxCenters.Length; i++)
                {
                    var c = playground.transform.TransformPoint(lay.boxCenters[i]);
                    Hole("PlaygroundHole", c, PlaygroundYaw, new Vector3(lay.boxSizes[i].x + 0.1f, 1f, lay.boxSizes[i].z + 0.1f));
                    var block = new GameObject("PlaygroundBlock" + i); block.transform.SetParent(playground.transform, false);
                    var po = block.AddComponent<ParkObstacle>(); po.center = new Vector3(c.x, 0f, c.z); po.halfSize = new Vector2(lay.boxSizes[i].x * 0.5f + 0.1f, lay.boxSizes[i].z * 0.5f + 0.1f);
                }
            }
            if (fountain != null)
            {
                var fb = fountain.GetComponentsInChildren<MeshRenderer>().Select(r => r.bounds).Aggregate((a, b2) => { a.Encapsulate(b2); return a; });
                float fr = Mathf.Max(fb.extents.x, fb.extents.z);
                foreach (float yaw in new[] { 0f, 30f, 60f })   // three turned squares: a near-circle (corners at 1.15 r) round the basin
                    Hole("FountainHole", FountainPos, yaw, new Vector3((fr + 0.03f) * 2f, 1f, (fr + 0.03f) * 2f));
                var po = fountain.AddComponent<ParkObstacle>(); po.center = new Vector3(FountainPos.x, 0f, FountainPos.z); po.halfSize = new Vector2(fr + 0.1f, fr + 0.1f);
            }
            // the track: cut out of the walkable ground except at level crossings (where stone paths cross it, and at least every
            // MaxCrossingGap metres), so toys only step onto it there; the train waits for anyone standing on a crossing
            if (train != null)
            {
                var loop = new LoopPath(trackPts);
                var crossings = new System.Collections.Generic.List<float>();
                float acc = 0f;
                for (int i = 0; i < trackPts.Length; i++)
                {
                    var a = trackPts[i]; var b = trackPts[(i + 1) % trackPts.Length]; float seg = (b - a).magnitude;
                    foreach (var sg in stoneSegs)
                        if (SegCross(a, b, sg.a, sg.b, out float t)) crossings.Add(acc + seg * t);
                    acc += seg;
                }
                crossings.Sort();
                if (crossings.Count == 0) crossings.Add(0f);
                var all = new System.Collections.Generic.List<float>(crossings);
                for (int i = 0; i < crossings.Count; i++)
                {
                    float c0 = crossings[i], c1 = i + 1 < crossings.Count ? crossings[i + 1] : crossings[0] + loop.Length;
                    int extra = Mathf.FloorToInt((c1 - c0) / MaxCrossingGap);
                    for (int k = 1; k <= extra; k++) all.Add(Mathf.Repeat(c0 + (c1 - c0) * k / (extra + 1), loop.Length));
                }
                bool NearCrossing(float d) { foreach (var c in all) { float g = Mathf.Abs(Mathf.Repeat(d - c + loop.Length * 0.5f, loop.Length) - loop.Length * 0.5f); if (g < CrossingHalf) return true; } return false; }
                int holes = 0;
                for (float d = 0f; d < loop.Length; d += TrackHoleStep)
                {
                    if (NearCrossing(d)) continue;
                    loop.At(d, out var p, out var dir);
                    Hole("TrackHole", p, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, new Vector3(1.0f, 1f, TrackHoleStep + 0.08f));
                    holes++;
                }
                Debug.Log("[Village] train track " + loop.Length.ToString("F1") + " m, " + all.Count + " level crossings (" + crossings.Count + " on paths), " + holes + " track holes");
            }
            foreach (var b in buildings)
            {
                if (b == null) continue;
                var rs = b.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
                var half = new Vector2(bb.extents.x + 0.12f, bb.extents.z + 0.12f);
                Hole(b.name + "Hole", bb.center, 0f, new Vector3(half.x * 2f, 1f, half.y * 2f));
                var po = b.AddComponent<ParkObstacle>(); po.center = new Vector3(bb.center.x, 0f, bb.center.z); po.halfSize = half;
            }
            foreach (var o in obstacleDiscs)
            {
                Hole("PlantHole", o.c, 0f, new Vector3(o.r * 2f, 1f, o.r * 2f));
                if (o.t != null) { var po = o.t.gameObject.AddComponent<ParkObstacle>(); po.center = new Vector3(o.c.x, 0f, o.c.z); po.halfSize = new Vector2(o.r, o.r); }
            }
            var surface = walk.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children; surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.BuildNavMesh();
            if (surface.navMeshData != null)
            {
                string dir = SceneDir + "Village_NavMesh/"; Directory.CreateDirectory(dir);
                string path = dir + "NavMesh.asset";
                if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(surface.navMeshData, path);
            }
            Debug.Log("[Village] park " + (WalkHalfX * 2f) + " x " + (WalkHalfZ * 2f) + " m: " + placed + " trees, " + bushes + " bushes, " + flowers + " flowers, " + lamps.Length + " lamps");
            new GameObject("VillageDirector").AddComponent<VillageDirector>();
            VillageHudBuild.AddTo(s);   // the 11-26 village HUD (Assets/_UI수정/3_마을/VillageHud.prefab)
            Save(s, "Village");
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mongle.Data;
using Mongle.Toy;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Mongle.EditorTools
{
    /// <summary>
    /// Builds all static content assets: materials, definitions, animator, toy prefab and the ContentCatalog.
    /// Idempotent: reuses existing assets by path and only creates/updates assets owned by this project setup.
    /// </summary>
    public static class ContentBuilder
    {
        const string Root = "Assets/_Project/";
        const string MatDir = Root + "Art/Materials/";
        const string SoDir = Root + "ScriptableObjects/";
        const string MongiDir = Root + "Art/Toys/Mongi/";
        /// <summary>Mongi's patchwork model (D-009); the old TeddyBear_001.fbx only while the new one is missing. Prefab name and kindId stay TeddyBear_001.</summary>
        static string ToyFbx => AssetDatabase.LoadAssetAtPath<GameObject>(MongiDir + "Mongi_002.fbx") != null ? MongiDir + "Mongi_002.fbx" : MongiDir + "TeddyBear_001.fbx";

        static Shader Lit => Shader.Find("Universal Render Pipeline/Lit");

        [MenuItem("Mongle/Build Content")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(MatDir); Directory.CreateDirectory(SoDir); Directory.CreateDirectory(Root + "Resources"); Directory.CreateDirectory(Root + "Prefabs/Toys");
            AssetDatabase.Refresh();
            RemapModelMaterials();
            PropBuilder.BuildAll();
            FeltLook.ApplyAll();
            var mats = BuildUtilityMaterials();
            // every toy is animated by its own clips: bone positions differ per toy since D-009 (chibi plush, original hard toys)
            var ac = BuildAnimator(ToyFbx, MongiDir + "AC_Mongi.controller");
            var toyPrefab = BuildToyPrefab(ac, ToyFbx, "PF_Toy_TeddyBear_001");
            var friendPrefabs = new Dictionary<string, GameObject>();
            foreach (var f in Friends)
            {
                string fbx = FriendDir + f.kind + ".fbx";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null) continue;
                var fac = BuildAnimator(fbx, FriendDir + "AC_" + f.kind + ".controller") ?? ac; // a model without clips borrows Mongi's
                friendPrefabs[f.kind] = BuildToyPrefab(fac, fbx, "PF_Toy_" + f.kind);
            }
            var catalog = BuildDefinitions(toyPrefab, mats, friendPrefabs);
            AssetDatabase.SaveAssets();
            Debug.Log("[ContentBuilder] done: " + AssetDatabase.GetAssetPath(catalog));
        }

        // ---------------- materials ----------------
        public static Material GetOrCreateMat(string name, Color color, float smooth = 0.2f, float metal = 0f, Color? emission = null)
        {
            string path = MatDir + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Lit); AssetDatabase.CreateAsset(m, path); }
            m.shader = Lit;
            m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            if (emission.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            FeltLook.TryApply(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void RemapModelMaterials()
        {
            foreach (var dir in new[] { "Art/Toys/Mongi", "Art/Toys/Friends", "Art/Tools", "Art/Props" })
                if (Directory.Exists(Root + dir))
                foreach (var fbx in Directory.GetFiles(Root + dir, "*.fbx"))
                {
                    var path = fbx.Replace('\\', '/');
                    var imp = AssetImporter.GetAtPath(path) as ModelImporter; if (imp == null) continue;
                    if (dir.StartsWith("Art/Toys")) RefreshToyColors(path, imp);
                    bool changed = false;
                    foreach (var src in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().ToList())
                    {
                        var map = imp.GetExternalObjectMap();
                        var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name);
                        if (map.ContainsKey(id)) continue;
                        string n = src.name;
                        bool metal = n.Contains("Metal") || n.Contains("Glasses") || n.Contains("Tweezer") || n.Contains("Needle");
                        bool felt = n.StartsWith("M_Mongi") || n.StartsWith("MF");
                        var color = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : src.color;
                        bool glow = n.Contains("Glow"); // robot eyes, car lamps (D-005)
                        var mat = GetOrCreateMat(n, color, felt ? 0.08f : metal ? 0.75f : 0.3f, metal ? 0.7f : 0f, glow ? color * 1.6f : (Color?)null);
                        imp.AddRemap(id, mat); changed = true;
                    }
                    if (changed) { imp.SaveAndReimport(); }
                }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Toy colours are tuned in Blender (D-009): remapped TOY_/MF materials take the FBX's embedded colour again (unless a patch
        /// texture gives the colour) and get their felt/clay look re-applied. The embedded colour only exists without the remap, so
        /// the remaps are lifted for one import. Skipped when the materials were saved after the FBX last changed.
        /// </summary>
        static void RefreshToyColors(string path, ModelImporter imp)
        {
            var map = imp.GetExternalObjectMap();
            var ids = map.Keys.Where(k => k.type == typeof(Material) && (k.name.StartsWith("TOY_") || k.name.StartsWith("MF")) && !FeltLook.IsPatched(k.name) && map[k] is Material).ToList();
            if (ids.Count == 0) return;
            var fbxTime = File.GetLastWriteTimeUtc(path);
            if (ids.All(k => { var mp = AssetDatabase.GetAssetPath(map[k]); return File.Exists(mp) && File.GetLastWriteTimeUtc(mp) > fbxTime; })) return;
            var mats = ids.ToDictionary(k => k, k => (Material)map[k]);
            foreach (var k in ids) imp.RemoveRemap(k);
            imp.SaveAndReimport();
            var embedded = new Dictionary<string, Color>();
            foreach (var src in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                if (!embedded.ContainsKey(src.name)) embedded[src.name] = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : src.color;
            foreach (var k in ids)
            {
                var m = mats[k];
                if (embedded.TryGetValue(k.name, out var c))
                {
                    m.SetColor("_BaseColor", c);
                    if (k.name.Contains("Glow")) m.SetColor("_EmissionColor", c * 1.6f);
                    FeltLook.TryApply(m);
                    EditorUtility.SetDirty(m);
                }
                imp.AddRemap(k, m);
            }
            imp.SaveAndReimport();
            AssetDatabase.SaveAssets();
        }

        static Texture2D SoftDot()
        {
            string path = Root + "Art/VFX/T_SoftDot.png";
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Root + "Art/VFX");
                var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32, 32)) / 32f;
                        float a = Mathf.Clamp01(1f - d); a = a * a * (3 - 2 * a);
                        t.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                t.Apply(); File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material ParticleMaterial(string name, Texture tex, Color tint)
        {
            string path = MatDir + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000;
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material[] BuildUtilityMaterials()
        {
            var list = new System.Collections.Generic.List<Material>
            {
                GetOrCreateMat("M_Dirt", new Color(0.36f, 0.25f, 0.15f), 0.05f),
                GetOrCreateMat("M_Thread", new Color(1f, 0.93f, 0.8f), 0.25f, 0f, new Color(0.25f, 0.2f, 0.1f)),
                ParticleMaterial("M_Particle", SoftDot(), Color.white),
                ParticleMaterial("M_Marker", null, Color.white),
            };
            AssetDatabase.SaveAssets();
            return list.ToArray();
        }

        // ---------------- animator ----------------
        /// <summary>One controller per toy from that toy's own FBX clips (same state names everywhere, default Idle). Null when the FBX has no clips.</summary>
        static AnimatorController BuildAnimator(string fbx, string path)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).OrderBy(c => c.name).ToList();
            if (clips.Count == 0) return null;
            // refill the existing controller instead of deleting it: prefabs loaded in memory keep pointing at the same object
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = ac.layers[0].stateMachine;
            foreach (var old in sm.states.ToArray()) sm.RemoveState(old.state);
            foreach (var clip in clips)
            {
                var st = sm.AddState(clip.name); st.motion = clip;
                if (clip.name == "Idle") sm.defaultState = st;
            }
            EditorUtility.SetDirty(ac);
            return ac;
        }

        // ---------------- prefab ----------------
        /// <summary>Toy prefab from a contract FBX (Mongi or a friend), driven by its own animator controller.</summary>
        static GameObject BuildToyPrefab(AnimatorController ac, string fbxPath, string prefabName)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            var animator = Mongle.Core.UnityExt.GetOrAdd<Animator>(inst);
            animator.runtimeAnimatorController = ac;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var avatar = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Avatar>().FirstOrDefault();
            if (avatar != null) animator.avatar = avatar;
            foreach (var s in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { s.updateWhenOffscreen = true; s.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; s.receiveShadows = true; }
            if (inst.GetComponent<ToyActor>() == null) inst.AddComponent<ToyActor>();
            string path = Root + "Prefabs/Toys/" + prefabName + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(inst, path);
            Object.DestroyImmediate(inst);
            return prefab;
        }

        // ---------------- definitions ----------------
        static T Make<T>(string name) where T : ScriptableObject
        {
            string path = SoDir + name + ".asset";
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) { a = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a, path); }
            return a;
        }

        static GameObject Fbx(string rel) => AssetDatabase.LoadAssetAtPath<GameObject>(Root + rel);

        static DamageDefinition Damage(string id, DamageType type, RepairPhase phase, string name, string note, string tool, string game, string dp, string ca, string repairCa, float radius, int prio)
        {
            var d = Make<DamageDefinition>("SO_Damage_" + id);
            d.id = id; d.type = type; d.phase = phase; d.displayName = name; d.noteText = note; d.requiredToolId = tool; d.miniGameKey = game;
            d.damagePointName = dp; d.cameraAnchorName = ca; d.repairAnchorName = repairCa; d.interactionRadius = radius; d.hintPriority = prio;
            // Mongi's tutorial damages use the C-001 nodes: no D-009 site fields
            d.site = d.boneName = d.visualName = d.visualDoneName = d.openName = d.shapeKey = d.grabPointName = d.normalPointName = d.stitchPrefix = d.limpBone = d.workPointName = "";
            d.spread = 0.06f; d.blobCount = 26; d.stainColor = new Color(0.36f, 0.25f, 0.15f); d.cleanColor = new Color(0.95f, 0.88f, 0.72f);
            EditorUtility.SetDirty(d); return d;
        }

        // ---------------- hospital friends (D-005): Blender exports under Art/Toys/Friends, same model contract as Mongi ----------------
        const string FriendDir = Root + "Art/Toys/Friends/";
        struct FriendSpec
        {
            public string kind, name, story; public Personality personality; public InteractionTag tags; public bool plush;
        }
        static readonly FriendSpec[] Friends =
        {
            new FriendSpec { kind = "Bunny_001", name = "보리", personality = Personality.Energetic, tags = InteractionTag.Play | InteractionTag.Flower, plush = true,
                story = "보리는 깡충깡충 뛰다가 넘어진 토끼 인형이에요.\n눈 단추가 떨어지고 귀도 찢어졌대요. 도와줄래요?" },
            new FriendSpec { kind = "Cat_001", name = "치즈", personality = Personality.Sleepy, tags = InteractionTag.Sit | InteractionTag.Watch, plush = true,
                story = "치즈는 햇볕 아래서 낮잠만 자던 고양이 인형이에요.\n왼팔이 덜렁거리고 꼬리에 가시가 박혔대요." },
            new FriendSpec { kind = "Robot_001", name = "삐삐", personality = Personality.Curious, tags = InteractionTag.Play | InteractionTag.Watch, plush = false,
                story = "삐삐는 호기심 많은 장난감 로봇이에요.\n건전지가 다 닳아서 눈빛이 깜빡깜빡해요." },
            new FriendSpec { kind = "Car_001", name = "부릉이", personality = Personality.Mischievous, tags = InteractionTag.Play, plush = false,
                story = "부릉이는 신나게 달리던 장난감 자동차예요.\n연료가 바닥나고 바퀴에 돌멩이가 끼었대요." },
            // D-008: five more friends
            new FriendSpec { kind = "Dino_001", name = "디노", personality = Personality.Mischievous, tags = InteractionTag.Play | InteractionTag.Watch, plush = true,
                story = "디노는 꼬리를 흔들며 쿵쿵 걷던 공룡 인형이에요.\n꼬리 솜이 빠지고 발에 가시가 박혔대요." },
            new FriendSpec { kind = "Bee_001", name = "붕붕", personality = Personality.Energetic, tags = InteractionTag.Flower | InteractionTag.Play, plush = true,
                story = "붕붕이는 꽃밭을 날아다니던 꿀벌 인형이에요.\n팔 솜이 빠지고 배에 꿀 얼룩이 묻었대요." },
            new FriendSpec { kind = "Plane_001", name = "슝이", personality = Personality.Curious, tags = InteractionTag.Watch | InteractionTag.Play, plush = false,
                story = "슝이는 하늘을 날던 장난감 비행기예요.\n연료가 떨어지고 프로펠러에 나뭇가지가 걸렸대요." },
            new FriendSpec { kind = "FireTruck_001", name = "용감이", personality = Personality.Gentle, tags = InteractionTag.Watch | InteractionTag.Sit, plush = false,
                story = "용감이는 마을을 지키던 장난감 소방차예요.\n물탱크가 텅 비고 사이렌에 가시가 걸렸대요." },
            new FriendSpec { kind = "Pawn_001", name = "단추", personality = Personality.Shy, tags = InteractionTag.Sit | InteractionTag.Flower, plush = false,
                story = "단추는 체스판에서 놀던 작은 나무 인형이에요.\n옷 칠이 벗겨지고 머리 방울에 가시가 걸렸대요." },
            // D-011: a frog plush that carries a lily-pad umbrella
            new FriendSpec { kind = "Frog_001", name = "개굴이", personality = Personality.Gentle, tags = InteractionTag.Flower | InteractionTag.Sit, plush = true,
                story = "개굴이는 연잎 우산을 쓰고 연못가를 산책하던 개구리 인형이에요.\n미끄러져서 눈이 떨어지고 왼팔 솜이 빠졌대요." },
        };
        public static IEnumerable<string> FriendKinds => Friends.Select(f => f.kind);

        /// <summary>
        /// One friend's hurt (D-009 §4). Node names follow the §2 contract from the site (DP_/DN_/CA_&lt;site&gt;, DMG_*_&lt;site&gt;, SP_&lt;site&gt;_NN,
        /// FP_&lt;site&gt;, shape keys); dp/ca/visual/grab override them where a friend keeps a legacy node.
        /// </summary>
        public class DamageSpec
        {
            public string kind, id, site, bone, name, note, limp; public DamageType type;
            public string dp, ca, visual, grab;
            public Color stain = new Color(0.36f, 0.25f, 0.15f), clean = new Color(0.95f, 0.88f, 0.72f);
            public float spread = 0.06f; public int blobs = 26;
        }

        static readonly Color Mud = new Color(0.42f, 0.3f, 0.18f), Oil = new Color(0.24f, 0.22f, 0.25f), Shiny = new Color(0.93f, 0.94f, 0.97f);
        static readonly Color FuelPink = new Color(1f, 0.68f, 0.55f), Water = new Color(0.6f, 0.82f, 1f);

        /// <summary>Every friend's hurts in repair order (D-009 §4/§5). Mongi keeps the tutorial damages.</summary>
        public static readonly DamageSpec[] FriendDamages =
        {
            new DamageSpec { kind = "Bunny_001", id = "Bunny_EyeR", type = DamageType.EyeButton, site = "EyeR", bone = "Head", name = "떨어진 눈 단추",
                note = "오른쪽 눈 단추가 떨어져서 대롱대롱 매달려 있어요! 바늘로 다시 꿰매 줘요." },
            new DamageSpec { kind = "Bunny_001", id = "Bunny_EarL", type = DamageType.Tear, site = "EarL", bone = "Ear_L", name = "찢어진 귀",
                note = "왼쪽 귀가 쭉 찢어졌어요. 한 땀 한 땀 꿰매 줘요." },
            new DamageSpec { kind = "Bunny_001", id = "Bunny_FootR", type = DamageType.Dirt, site = "FootR", bone = "Leg_R", name = "발바닥 흙 얼룩",
                note = "깡충깡충 뛰다가 오른발에 흙이 잔뜩 묻었어요. 쓱쓱 닦아 줘요.", stain = new Color(0.45f, 0.32f, 0.2f), spread = 0.04f, blobs = 16 },

            new DamageSpec { kind = "Cat_001", id = "Cat_ArmL", type = DamageType.Tear, site = "ArmL", bone = "Spine", limp = "Arm_L", name = "덜렁거리는 왼팔",
                note = "왼팔 어깨 솔기가 터져서 팔이 덜렁거려요. 꼼꼼히 꿰매 줘요." },
            new DamageSpec { kind = "Cat_001", id = "Cat_Tail", type = DamageType.Thorn, site = "Tail", bone = "Tail", name = "꼬리의 가시",
                note = "꼬리 끝에 가시가 콕 박혀 있어요! 핀셋으로 살살 빼 줘요." },

            new DamageSpec { kind = "Dino_001", id = "Dino_Tail", type = DamageType.LowCotton, site = "Tail", bone = "Tail", name = "홀쭉해진 꼬리",
                note = "꼬리에서 솜이 빠져서 축 늘어졌어요. 솜을 꾹꾹 채워 줘요." },
            new DamageSpec { kind = "Dino_001", id = "Dino_FootR", type = DamageType.Thorn, site = "FootR", bone = "Leg_R", name = "발에 박힌 가시",
                note = "쿵쿵 걷다가 오른발에 가시가 박혔대요. 핀셋으로 빼 줘요." },
            new DamageSpec { kind = "Dino_001", id = "Dino_Cheek", type = DamageType.Dirt, site = "CheekL", bone = "Head", name = "볼의 초코 얼룩",
                note = "초코 과자를 먹다가 볼에 잔뜩 묻었어요. 쓱쓱 닦아 줘요.", stain = new Color(0.35f, 0.2f, 0.12f), spread = 0.035f, blobs = 14 },

            new DamageSpec { kind = "Bee_001", id = "Bee_Belly", type = DamageType.Dirt, site = "BELLY", bone = "Spine", name = "배의 꿀 얼룩",
                note = "끈적한 꿀이 배에 묻었어요. 쓱쓱 닦아 줘요.", stain = new Color(0.93f, 0.66f, 0.2f), clean = new Color(1f, 0.93f, 0.72f), spread = 0.05f, blobs = 20 },
            new DamageSpec { kind = "Bee_001", id = "Bee_ArmR", type = DamageType.LowCotton, site = "ArmR", bone = "Arm_R", name = "홀쭉해진 오른팔",
                note = "오른팔 솜이 빠져서 힘이 하나도 없대요. 솜을 꾹꾹 채워 줘요." },

            new DamageSpec { kind = "Robot_001", id = "Robot_Battery", type = DamageType.Battery, site = "Battery", bone = "Spine", name = "다 된 건전지",
                note = "등의 건전지가 다 닳아서 눈빛이 희미해요. 새 건전지로 바꿔 줘요." },
            new DamageSpec { kind = "Robot_001", id = "Robot_Oil", type = DamageType.Dirt, site = "Chest", bone = "Spine", name = "가슴의 기름 얼룩",
                note = "가슴에 끈적한 기름 얼룩이 묻었어요. 반짝반짝 닦아 줘요.", stain = Oil, clean = Shiny, spread = 0.05f, blobs = 20 },

            new DamageSpec { kind = "Car_001", id = "Car_Fuel", type = DamageType.Fuel, site = "Fuel", bone = "Spine", name = "텅 빈 연료통",
                note = "연료가 바닥나서 부릉부릉 소리가 안 나요. 주유기로 가득 채워 줘요.", stain = FuelPink },
            new DamageSpec { kind = "Car_001", id = "Car_Mud", type = DamageType.Dirt, site = "Bumper", bone = "Hips", name = "범퍼의 흙탕물",
                note = "흙탕물을 지나오다 범퍼가 얼룩투성이가 됐어요. 깨끗이 닦아 줘요.", stain = Mud, clean = Shiny, spread = 0.05f, blobs = 20 },
            new DamageSpec { kind = "Car_001", id = "Car_WheelR", type = DamageType.Thorn, site = "WheelR", bone = "Hips", name = "바퀴에 낀 돌멩이",
                note = "오른쪽 바퀴에 돌멩이가 꽉 끼었어요! 핀셋으로 쏙 빼 줘요." },

            new DamageSpec { kind = "Plane_001", id = "Plane_Fuel", type = DamageType.Fuel, site = "Fuel", bone = "Spine", name = "텅 빈 연료통",
                note = "연료가 떨어져서 하늘을 못 날아요. 주유기로 가득 채워 줘요.", stain = FuelPink },
            new DamageSpec { kind = "Plane_001", id = "Plane_WingL", type = DamageType.Dirt, site = "WingL", bone = "Arm_L", name = "날개의 기름 얼룩",
                note = "왼쪽 날개에 기름 얼룩이 묻었어요. 쓱쓱 닦아 줘요.", stain = Oil, clean = Shiny, spread = 0.05f, blobs = 18 },
            new DamageSpec { kind = "Plane_001", id = "Plane_Prop", type = DamageType.Thorn, site = "Prop", bone = "Spine", name = "프로펠러에 낀 나뭇가지",
                note = "프로펠러에 나뭇가지가 걸려서 빙글빙글 못 돌아요! 빼 줘요." },

            new DamageSpec { kind = "FireTruck_001", id = "FireTruck_Water", type = DamageType.Fuel, site = "Fuel", bone = "Spine", name = "텅 빈 물탱크",
                note = "불을 끄느라 물탱크가 텅 비었어요. 물을 가득 채워 줘요.", stain = Water },
            new DamageSpec { kind = "FireTruck_001", id = "FireTruck_Siren", type = DamageType.Thorn, site = "EAR_L", bone = "Ear_L", name = "사이렌의 가시",
                note = "빨간 사이렌에 가시가 걸려 있어요! 핀셋으로 빼 줘요.", visual = "DMG_Thorn_EarL", grab = "GP_THORN" },
            new DamageSpec { kind = "FireTruck_001", id = "FireTruck_Mud", type = DamageType.Dirt, site = "Bumper", bone = "Hips", name = "범퍼의 흙탕물",
                note = "앞 범퍼에 흙탕물이 튀었어요. 쓱쓱 닦아 줘요.", stain = Mud, clean = Shiny, spread = 0.05f, blobs = 20 },

            new DamageSpec { kind = "Pawn_001", id = "Pawn_Paint", type = DamageType.Paint, site = "Dress", bone = "Spine", name = "벗겨진 옷 칠",
                note = "파란 옷의 칠이 군데군데 벗겨졌어요. 붓으로 예쁘게 칠해 줘요.", stain = new Color(0.86f, 0.72f, 0.55f), clean = new Color(0.62f, 0.76f, 0.98f), spread = 0.05f, blobs = 22 },
            new DamageSpec { kind = "Pawn_001", id = "Pawn_Bun", type = DamageType.Thorn, site = "EAR_L", bone = "Ear_L", name = "머리 방울의 가시",
                note = "머리 방울에 가시가 걸려 있어요! 핀셋으로 빼 줘요.", visual = "DMG_Thorn_EarL", grab = "GP_THORN" },

            new DamageSpec { kind = "Frog_001", id = "Frog_EyeR", type = DamageType.EyeButton, site = "EyeR", bone = "Head", name = "떨어진 눈",
                note = "연못가에서 미끄러져 오른쪽 눈이 대롱대롱 매달렸어요! 바늘로 다시 꿰매 줘요." },
            new DamageSpec { kind = "Frog_001", id = "Frog_ArmL", type = DamageType.LowCotton, site = "ArmL", bone = "Arm_L", name = "홀쭉해진 왼팔",
                note = "왼팔 솜이 빠져서 축 늘어졌어요. 솜을 꾹꾹 채워 줘요." },
            new DamageSpec { kind = "Frog_001", id = "Frog_Tummy", type = DamageType.Dirt, site = "Tummy", bone = "Spine", name = "배의 진흙 얼룩",
                note = "연못가 진흙이 배에 잔뜩 묻었어요. 쓱쓱 닦아 줘요.", stain = Mud, spread = 0.05f, blobs = 20 },
        };

        public static string GameOf(DamageType t) =>
            t == DamageType.Thorn ? "Thorn" : t == DamageType.Dirt ? "Dirt" : t == DamageType.Tear || t == DamageType.EyeButton ? "Sew" : t == DamageType.LowCotton ? "Cotton"
            : t == DamageType.Fuel ? "Fuel" : t == DamageType.Battery ? "Battery" : "Paint";
        public static string ToolOf(DamageType t) =>
            t == DamageType.Thorn ? "Tool_Tweezer" : t == DamageType.Dirt ? "Tool_Sponge" : t == DamageType.Tear || t == DamageType.EyeButton ? "Tool_Needle" : t == DamageType.LowCotton ? "Tool_CottonInjector"
            : t == DamageType.Fuel ? "Tool_FuelNozzle" : t == DamageType.Battery ? "Tool_Battery" : "Tool_Brush";
        static RepairPhase PhaseOf(DamageType t) =>
            t == DamageType.Thorn ? RepairPhase.Remove : t == DamageType.Dirt ? RepairPhase.Clean : t == DamageType.Paint ? RepairPhase.Finish
            : t == DamageType.LowCotton || t == DamageType.Fuel ? RepairPhase.Restore : RepairPhase.Repair;
        static int RewardOf(DamageType t) => t == DamageType.Thorn || t == DamageType.Dirt ? 25 : t == DamageType.Paint ? 30 : t == DamageType.Tear || t == DamageType.EyeButton ? 40 : 35;

        /// <summary>Fills a definition from its spec (pure: no asset access, EditMode-testable). Every D-009 field is written, so reused assets never keep stale values.</summary>
        public static void Fill(DamageDefinition d, DamageSpec s, int order)
        {
            string site = s.site;
            d.id = s.id; d.type = s.type; d.phase = PhaseOf(s.type); d.displayName = s.name; d.noteText = s.note;
            d.requiredToolId = ToolOf(s.type); d.miniGameKey = GameOf(s.type);
            d.damagePointName = s.dp ?? "DP_" + site; d.cameraAnchorName = s.ca ?? "CA_" + site; d.repairAnchorName = d.cameraAnchorName; d.lookPointName = "";
            d.interactionRadius = 0.08f; d.hintPriority = order + 1; d.rewardCoins = RewardOf(s.type);
            d.site = site; d.boneName = s.bone; d.normalPointName = "DN_" + site; d.limpBone = s.limp ?? "";
            d.visualName = s.visual ?? (s.type == DamageType.Thorn ? "DMG_Thorn_" + site : s.type == DamageType.Tear ? "DMG_Tear_" + site : s.type == DamageType.EyeButton ? "DMG_EyeLoose_" + site
                : s.type == DamageType.LowCotton ? "DMG_FillOpen_" + site : s.type == DamageType.Fuel ? "DMG_FuelCap_" + site : s.type == DamageType.Battery ? "DMG_BatteryOld_" + site : "");
            d.visualDoneName = s.type == DamageType.Battery ? "DMG_BatteryNew_" + site : "";
            d.openName = s.type == DamageType.Fuel ? "DMG_FuelOpen_" + site : "";
            d.shapeKey = s.type == DamageType.EyeButton ? "EyeGone_" + site : s.type == DamageType.LowCotton ? "Deflated_" + site : "";
            d.grabPointName = s.type == DamageType.Thorn ? s.grab ?? "GP_THORN_" + site : "";
            d.stitchPrefix = s.type == DamageType.Tear || s.type == DamageType.EyeButton ? "SP_" + site + "_" : "";
            d.workPointName = s.type == DamageType.LowCotton || s.type == DamageType.Fuel || s.type == DamageType.Battery ? "FP_" + site : "";
            d.spread = s.spread; d.blobCount = s.blobs; d.stainColor = s.stain; d.cleanColor = s.clean;
        }

        static DamageDefinition[] FriendSequence(string kind)
        {
            var list = new List<DamageDefinition>(); int i = 0;
            foreach (var s in FriendDamages.Where(x => x.kind == kind))
            {
                var d = Make<DamageDefinition>("SO_Damage_" + s.id);
                Fill(d, s, i++);
                EditorUtility.SetDirty(d); list.Add(d);
            }
            return list.ToArray();
        }

        static ContentCatalog BuildDefinitions(GameObject toyPrefab, Material[] mats, Dictionary<string, GameObject> friendPrefabs)
        {
            var thorn = Damage("Thorn", DamageType.Thorn, RepairPhase.Remove, "귀의 가시", "왼쪽 귀에 가시가 박혀 있어요! 많이 따끔했겠어요.", "Tool_Tweezer", "Thorn", "DP_EAR_L", "CA_EAR_L", "CA_EAR_L", 0.085f, 1);
            thorn.lookPointName = "GP_THORN"; EditorUtility.SetDirty(thorn);
            var dirt = Damage("Dirt", DamageType.Dirt, RepairPhase.Clean, "배의 얼룩", "배에 얼룩이 잔뜩 묻어 있어요. 닦아주면 좋아할 거예요.", "Tool_Sponge", "Dirt", "DP_BELLY", "CA_BELLY", "CA_BELLY", 0.08f, 2);
            var tear = Damage("Tear", DamageType.Tear, RepairPhase.Repair, "등의 찢어진 틈", "등이 살짝 터져 있어요. 한 땀 한 땀 꿰매줘야겠어요.", "Tool_Needle", "Sew", "DP_BACK", "CA_BACK", "CA_BACK", 0.085f, 3);
            var cotton = Damage("LowCotton", DamageType.LowCotton, RepairPhase.Restore, "솜이 부족한 몸", "몸이 축 처져 있어요. 솜이 모자란가 봐요.", "Tool_CottonInjector", "Cotton", "DP_BODY", "CA_BODY", "CA_FILL", 0.075f, 4);

            var tweezer = Tool("Tool_Tweezer", "핀셋", "콕 집어서 당겨요", Fbx("Art/Tools/Tool_Tweezer.fbx"), new Color(0.78f, 0.88f, 0.98f));
            var sponge = Tool("Tool_Sponge", "스펀지", "쓱쓱 문질러요", Fbx("Art/Tools/Tool_Sponge.fbx"), new Color(1f, 0.93f, 0.6f));
            var needle = Tool("Tool_Needle", "바늘", "점을 따라 꿰매요", Fbx("Art/Tools/Tool_Needle.fbx"), new Color(0.98f, 0.78f, 0.82f));
            var injector = Tool("Tool_CottonInjector", "솜 주입기", "꾹 눌러서 채워요", Fbx("Art/Tools/Tool_CottonInjector.fbx"), new Color(0.78f, 0.93f, 0.85f));
            // D-009 tools (a missing FBX leaves the prefab empty: ToolView then shows a simple stand-in)
            var nozzle = Tool("Tool_FuelNozzle", "주유기", "대고 꾹 채워요", Fbx("Art/Tools/Tool_FuelNozzle.fbx"), new Color(1f, 0.8f, 0.62f));
            var battery = Tool("Tool_Battery", "새 건전지", "빼고 쏙 끼워요", Fbx("Art/Tools/Tool_Battery.fbx"), new Color(0.8f, 0.92f, 0.66f));
            var brush = Tool("Tool_Brush", "붓", "슥슥 칠해요", Fbx("Art/Tools/Tool_Brush.fbx"), new Color(0.84f, 0.8f, 1f));

            var ribbon = Accessory("Acc_RedRibbon", "빨간 리본", AccessorySlot.Neck, "AP_NECK", Fbx("Art/Props/Acc_RedRibbon.fbx"), new Color(1f, 0.72f, 0.76f));
            var hat = Accessory("Acc_BlueHat", "파란 모자", AccessorySlot.Head, "AP_HEAD", Fbx("Art/Props/Acc_BlueHat.fbx"), new Color(0.7f, 0.85f, 1f));
            var glasses = Accessory("Acc_RoundGlasses", "동그란 안경", AccessorySlot.Face, "AP_FACE", Fbx("Art/Props/Acc_RoundGlasses.fbx"), new Color(1f, 0.92f, 0.65f));
            var gifts = new[] { ribbon, hat, glasses }; // Mongi's first dress-up: free gifts (unchanged)
            // more to buy at the accessory shop (each FBX root = its attach point); a model that is not exported yet is left out
            var extras = new List<(AccessoryDefinition acc, int price, string desc)>();
            void Extra(string id, string name, AccessorySlot slot, string ap, Color c, int price, string desc)
            {
                var prefab = Fbx("Art/Props/" + id + ".fbx");
                if (prefab == null) { Debug.LogWarning("[ContentBuilder] " + id + ".fbx not found: not sold yet"); return; }
                extras.Add((Accessory(id, name, slot, ap, prefab, c), price, desc));
            }
            Extra("Acc_FlowerCrown", "꽃 화관", AccessorySlot.Head, "AP_HEAD", new Color(1f, 0.74f, 0.84f), 35, "들꽃을 엮은 화관. 쓰면 봄 냄새가 나요.");
            Extra("Acc_Crown", "반짝 왕관", AccessorySlot.Head, "AP_HEAD", new Color(1f, 0.85f, 0.4f), 45, "반짝반짝 금빛 왕관. 오늘의 주인공!");
            Extra("Acc_Beret", "베레모", AccessorySlot.Head, "AP_HEAD", new Color(0.82f, 0.76f, 1f), 30, "말랑한 보라 베레모. 꼬마 화가 같아요.");
            Extra("Acc_HeartGlasses", "하트 안경", AccessorySlot.Face, "AP_FACE", new Color(1f, 0.68f, 0.8f), 30, "하트 모양 안경. 세상이 분홍빛이에요.");
            Extra("Acc_BowTie", "나비넥타이", AccessorySlot.Neck, "AP_NECK", new Color(0.66f, 0.84f, 1f), 20, "하늘색 나비넥타이. 의젓해 보여요.");
            Extra("Acc_FlowerBrooch", "꽃 브로치", AccessorySlot.Neck, "AP_NECK", new Color(1f, 0.93f, 0.55f), 25, "노란 꽃 브로치. 가슴에 톡 달아 줘요.");
            var allAccs = gifts.Concat(extras.Select(e => e.acc)).ToArray();

            var bench = Furniture("Fur_Bench", "벤치", Fbx("Art/Props/Fur_Bench.fbx"), new Vector2Int(2, 1), InteractionTag.Sit, new Vector2Int(8, 6));
            var pot = Furniture("Fur_FlowerPot", "꽃", Fbx("Art/Props/Fur_FlowerPot.fbx"), new Vector2Int(1, 1), InteractionTag.Flower, new Vector2Int(3, 2));
            bench.starter = true; pot.starter = true; EditorUtility.SetDirty(bench); EditorUtility.SetDirty(pot);

            // D-002: shop items (prices are tunable recommendations, see D-002 doc)
            var chair = Item("Fur_Armchair", "포근 의자", "분홍빛 폭신한 의자. 앉으면 잠이 솔솔 와요.", PropBuilder.Load("Fur_Armchair"), ItemCategory.Furniture, 45, new Vector2Int(1, 1), InteractionTag.Sit, new Color(1f, 0.74f, 0.78f));
            var toybox = Item("Fur_ToyBox", "장난감 상자", "공과 블록이 가득! 몽이가 신나게 놀아요.", PropBuilder.Load("Fur_ToyBox"), ItemCategory.Furniture, 60, new Vector2Int(1, 1), InteractionTag.Play, new Color(0.62f, 0.8f, 1f));
            var lamp = Item("Fur_Lamp", "달빛 가로등", "눌러서 켜고 끌 수 있는 따뜻한 등불.", PropBuilder.Load("Fur_Lamp"), ItemCategory.Furniture, 35, new Vector2Int(1, 1), InteractionTag.Watch, new Color(1f, 0.92f, 0.6f));
            var table = Item("Fur_Table", "찻상", "찻주전자와 컵이 놓인 작은 탁자.", PropBuilder.Load("Fur_Table"), ItemCategory.Furniture, 40, new Vector2Int(2, 1), InteractionTag.Watch, new Color(0.85f, 0.66f, 0.46f));
            // the little model of the park's big wheel (D-006 grew it into the ride), so its name says so
            var wheel = Item("Fur_Wheel", "하트 관람차 모형", "공원 관람차의 작은 모형. 누르면 빙글빙글 빨라져요!", PropBuilder.Load("Fur_Wheel"), ItemCategory.Furniture, 90, new Vector2Int(2, 1), InteractionTag.Play, new Color(1f, 0.72f, 0.78f));
            var trees = Item("Fur_Tree", "아기 숲 나무", "동글동글 나무 세 그루. 마을이 푸릇해져요.", PropBuilder.Load("Fur_Tree"), ItemCategory.Furniture, 55, new Vector2Int(2, 1), InteractionTag.Watch, new Color(0.6f, 0.82f, 0.55f));
            // the cupcake house is the accessory shop's building now (not for sale; the definition stays so old saves load)
            var cake = Item("Fur_Cake", "컵케이크 집", "체리가 올라간 달콤한 작은 집.", PropBuilder.Load("Fur_Cake"), ItemCategory.Furniture, 0, new Vector2Int(1, 1), InteractionTag.Watch, new Color(1f, 0.8f, 0.85f));
            // accessory shop goods: friends wear them after their repair (one is used up per friend)
            var ribbonItem = AccessoryItem(ribbon, 20, "목에 매는 빨간 리본. 누구에게나 잘 어울려요.");
            var hatItem = AccessoryItem(hat, 30, "포근한 파란 모자. 머리에 쏙 씌워 줘요.");
            var glassesItem = AccessoryItem(glasses, 25, "동그란 안경. 쓰면 똑똑해 보여요!");
            var accItems = new[] { ribbonItem, hatItem, glassesItem }.Concat(extras.Select(e => AccessoryItem(e.acc, e.price, e.desc))).ToArray();
            var tulip = Item("Plant_Tulip", "튤립 화분", "물을 주면 쑥쑥 자라 분홍 튤립이 피어요.", PropBuilder.Load("Plant_Tulip"), ItemCategory.Plant, 15, new Vector2Int(1, 1), InteractionTag.Flower, new Color(1f, 0.6f, 0.7f));
            var sunflower = Item("Plant_Sunflower", "해바라기 화분", "키가 쑥! 커다란 해바라기가 피어요.", PropBuilder.Load("Plant_Sunflower"), ItemCategory.Plant, 25, new Vector2Int(1, 1), InteractionTag.Flower, new Color(1f, 0.85f, 0.35f));
            var cactus = Item("Plant_Cactus", "동글 선인장", "작고 동그란 선인장. 꽃이 피면 귀여워요.", PropBuilder.Load("Plant_Cactus"), ItemCategory.Plant, 30, new Vector2Int(1, 1), InteractionTag.Flower, new Color(0.6f, 0.82f, 0.65f));
            var sapling = Item("Plant_Sapling", "벚나무 묘목", "정성껏 키우면 벚꽃이 활짝 피어요.", PropBuilder.Load("Plant_Sapling"), ItemCategory.Plant, 70, new Vector2Int(1, 1), InteractionTag.Flower, new Color(1f, 0.78f, 0.84f));
            foreach (var pl in new[] { tulip, sunflower, cactus, sapling }) { pl.stageWaters = new[] { 2, 5 }; pl.waterCooldownSeconds = 25f; pl.bloomReward = 15; EditorUtility.SetDirty(pl); }
            sapling.bloomReward = 25; EditorUtility.SetDirty(sapling);

            // D-003: sub-quests (3-step chains per keeper)
            var qTulip = Quest("Q_Tulip", "Shop_Plant", "첫 화분 놓기", "화분을 하나 사서 마을에 놓아 줄래요? 튤립이 예뻐요!", "고마워요! 튤립 화분을 사서 마을에 놓아 봐요.", "마을이 한결 예뻐졌어요!", "ItemPlaced", "Plant_Tulip", 1, 15, "");
            var qWater = Quest("Q_Water", "Shop_Plant", "물 주기 연습", "식물에 물을 다섯 번 줘 보세요. 쑥쑥 자랄 거예요!", "화분을 클릭하면 물을 줄 수 있어요. 25초마다 다시 줄 수 있어요.", "정말 잘 키우네요!", "PlantWatered", "", 5, 20, "Q_Tulip");
            var qSun = Quest("Q_Sunflower", "Shop_Plant", "해바라기 활짝", "해바라기를 활짝 피워 줄래요? 아주 크게 자라요!", "해바라기를 사서 놓고 물을 다섯 번 주면 활짝 펴요.", "와, 해바라기가 활짝 폈네요!", "PlantBloomed", "Plant_Sunflower", 1, 40, "Q_Water");
            var qChair = Quest("Q_Chair", "Shop_Furniture", "몽이의 의자", "몽이가 앉을 포근한 의자를 놓아 줄래요? 앉는 걸 보고 싶어요!", "의자를 마을에 놓으면 몽이가 다가와서 앉아 볼 거예요.", "몽이가 편안해 보여요!", "MongiSat", "Fur_Armchair", 1, 30, "");
            var qWheel = Quest("Q_Wheel", "Shop_Furniture", "관람차 모형 놀이", "하트 관람차 모형 옆에서 몽이가 두 번 놀게 해 줘요!", "관람차 모형을 놓고 기다리면 몽이가 와서 놀아요. 모형을 클릭하면 더 빨리 돌아요.", "몽이가 신나 보여요!", "MongiPlayed", "Fur_Wheel", 2, 50, "Q_Chair");
            var qAcc = Quest("Q_Accessory", "Shop_Accessory", "첫 악세서리", "악세서리를 사서 친구에게 입혀 줄래요? 다들 멋쟁이가 될 거예요!", "산 악세서리는 친구가 수선을 마친 뒤 꾸미기에서, 또는 마을에서 친구를 클릭해서 입혀 줄 수 있어요.", "정말 잘 어울려요!", "AccessoryWorn", "", 1, 25, "");

            var furnShop = Make<ShopDefinition>("SO_Shop_Furniture");
            furnShop.id = "Shop_Furniture"; furnShop.displayName = "다람 가구점"; furnShop.keeperName = "다람";
            furnShop.items = new[] { chair, toybox, lamp, table, wheel, trees };
            furnShop.firstVisitLine = "처음 보는 얼굴이네요! 저는 다람이에요. 수선하고 받은 몽글 코인으로 마음에 드는 가구를 골라 봐요.";
            furnShop.greetings = new[] { "어서 와요! 포근한 가구가 가득해요.", "오늘은 어떤 걸 찾으세요?", "몽이는 어떤 가구를 좋아할까요?" };
            furnShop.buyLines = new[] { "{0}, 좋은 선택이에요!", "{0} 잘 포장했어요!" }; // where it went is said after the line
            furnShop.poorLines = new[] { "코인이 조금 모자라요. 몽이를 도와주면 모을 수 있어요!", "아직은 좀 모자라네요. 천천히 모아 와요." };
            furnShop.pokeLine = "구경 더 하세요!";
            furnShop.position = new Vector3(-2.3f, 0f, -3.95f); furnShop.yaw = 0f;
            furnShop.wallColor = new Color(1f, 0.91f, 0.74f); furnShop.roofColor = new Color(0.58f, 0.76f, 0.92f); furnShop.keeperColor = new Color(0.94f, 0.64f, 0.34f);
            EditorUtility.SetDirty(furnShop);
            var plantShop = Make<ShopDefinition>("SO_Shop_Plant");
            plantShop.id = "Shop_Plant"; plantShop.displayName = "토순 꽃집"; plantShop.keeperName = "토순";
            plantShop.items = new[] { tulip, sunflower, cactus, sapling };
            plantShop.firstVisitLine = "어서 와요! 저는 토순이에요. 화분을 사서 마을에 놓고 물을 주면 꽃이 피어요.";
            plantShop.greetings = new[] { "꽃 향기가 좋죠?", "물을 자주 주면 쑥쑥 자라요!", "어서 오세요! 오늘 새싹이 예뻐요." };
            plantShop.buyLines = new[] { "{0}, 잘 키워 주세요!", "{0}! 물 주는 거 잊지 말아요." };
            plantShop.poorLines = new[] { "아직 코인이 모자라요. 천천히 모아도 괜찮아요.", "조금만 더 모아서 다시 와요!" };
            plantShop.pokeLine = "꽃 구경 더 하세요!";
            plantShop.position = new Vector3(2.3f, 0f, -3.95f); plantShop.yaw = 0f;
            plantShop.wallColor = new Color(0.86f, 0.95f, 0.84f); plantShop.roofColor = new Color(1f, 0.72f, 0.78f); plantShop.keeperColor = new Color(0.99f, 0.96f, 0.94f);
            EditorUtility.SetDirty(plantShop);
            var accShop = Make<ShopDefinition>("SO_Shop_Accessory");
            accShop.id = "Shop_Accessory"; accShop.displayName = "컵케이크 악세서리점"; accShop.keeperName = "달콩";
            accShop.items = accItems;
            accShop.firstVisitLine = "어서 와요! 저는 달콩이에요. 수선을 마친 친구들에게 입혀 줄 예쁜 악세서리를 팔아요.";
            accShop.greetings = new[] { "오늘은 누구를 꾸며 줄까요?", "반짝 왕관이랑 꽃 화관이 새로 들어왔어요!", "친구들이 멋쟁이가 되면 마을이 더 즐거워요." };
            accShop.buyLines = new[] { "{0}, 정말 잘 어울릴 거예요!", "{0} 예쁘게 포장했어요!" };
            accShop.poorLines = new[] { "코인이 조금 모자라요. 친구들을 고쳐 주면 모을 수 있어요!", "조금만 더 모아서 다시 와요!" };
            accShop.pokeLine = "다른 것도 구경해 봐요!";
            accShop.position = new Vector3(5.4f, 0f, -4.1f); accShop.yaw = 0f;
            accShop.wallColor = new Color(1f, 0.86f, 0.9f); accShop.roofColor = new Color(1f, 0.6f, 0.72f); accShop.keeperColor = new Color(1f, 0.8f, 0.85f);
            EditorUtility.SetDirty(accShop);

            var toy = Make<ToyDefinition>("SO_Toy_TeddyBear_001");
            toy.kindId = "TeddyBear_001"; toy.displayName = "몽이"; toy.category = ToyCategory.Plush; toy.personality = Personality.Shy;
            toy.favoriteTags = InteractionTag.Flower | InteractionTag.Sit;
            toy.shortBackstory = "몽이는 오랫동안 사랑받아 조금 지친 곰인형이에요.\n다시 포근해질 수 있도록 도와주세요.";
            toy.prefab = toyPrefab; toy.heightMeters = 0.45f;
            toy.damageSequence = new[] { thorn, dirt, tear, cotton };
            toy.accessories = allAccs;
            toy.giftAccessories = gifts;
            toy.stampRewardCoins = 30;
            thorn.rewardCoins = 25; dirt.rewardCoins = 25; tear.rewardCoins = 40; cotton.rewardCoins = 40;
            foreach (var dd in new[] { thorn, dirt, tear, cotton }) EditorUtility.SetDirty(dd);
            EditorUtility.SetDirty(toy);

            var friends = new List<ToyDefinition>();
            foreach (var f in Friends)
            {
                if (!friendPrefabs.TryGetValue(f.kind, out var fp)) continue;
                var fd = Make<ToyDefinition>("SO_Toy_" + f.kind);
                fd.kindId = f.kind; fd.displayName = f.name; fd.category = f.plush ? ToyCategory.Plush : ToyCategory.WindUp; fd.personality = f.personality;
                fd.favoriteTags = f.tags; fd.shortBackstory = f.story; fd.prefab = fp; fd.heightMeters = 0.45f;
                // every friend hurts in its own places (D-009 §4); the shared-damage wording is no longer needed
                fd.damageSequence = FriendSequence(f.kind);
                fd.accessories = allAccs;
                fd.giftAccessories = new AccessoryDefinition[0]; // friends only wear what was bought
                fd.stampRewardCoins = 30;
                fd.wording = new DamageWording[0];
                EditorUtility.SetDirty(fd); friends.Add(fd);
            }

            var cat = Make<ContentCatalog>("SO_ContentCatalog");
            cat.toy = toy; cat.friends = friends.ToArray(); cat.tools = new[] { tweezer, sponge, needle, injector, nozzle, battery, brush }; cat.furniture = new[] { bench, pot, chair, toybox, lamp, table, wheel, trees, cake, tulip, sunflower, cactus, sapling }.Concat(accItems).ToArray(); cat.shops = new[] { furnShop, plantShop, accShop };
            cat.houses = Enumerable.Range(1, Mongle.Village.HouseLots.Variants).Select(i => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Props/PF_House_" + i.ToString("00") + ".prefab")).Where(h => h != null).ToArray(); // D-012 (BackdropBuilder.Houses)
            cat.wateringCan = PropBuilder.Load("WateringCan"); cat.quests = new[] { qTulip, qWater, qSun, qChair, qWheel, qAcc }; cat.materials = mats;
            EditorUtility.SetDirty(cat);
            // The runtime loads Resources/ContentCatalog
            string res = Root + "Resources/ContentCatalog.asset";
            if (AssetDatabase.LoadAssetAtPath<ContentCatalog>(res) != null) AssetDatabase.DeleteAsset(res);
            AssetDatabase.CopyAsset(SoDir + "SO_ContentCatalog.asset", res);
            // the Resources copy is the one used at runtime; keep it referencing the same definitions
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<ContentCatalog>(res);
        }

        static ToolDefinition Tool(string id, string name, string hint, GameObject prefab, Color c)
        {
            var t = Make<ToolDefinition>("SO_" + id); t.id = id; t.displayName = name; t.shortHint = hint; t.prefab = prefab; t.uiColor = c; EditorUtility.SetDirty(t); return t;
        }
        static AccessoryDefinition Accessory(string id, string name, AccessorySlot slot, string ap, GameObject prefab, Color c)
        {
            var a = Make<AccessoryDefinition>("SO_" + id); a.id = id; a.displayName = name; a.slot = slot; a.attachPointName = ap; a.prefab = prefab; a.uiColor = c; EditorUtility.SetDirty(a); return a;
        }
        static QuestDefinition Quest(string id, string shop, string title, string request, string accept, string done, string evt, string param, int target, int reward, string requires)
        {
            var q = Make<QuestDefinition>("SO_" + id);
            q.id = id; q.giverShopId = shop; q.title = title; q.request = request; q.acceptLine = accept; q.doneLine = done;
            q.eventId = evt; q.eventParam = param; q.targetCount = target; q.rewardCoins = reward; q.requires = requires;
            EditorUtility.SetDirty(q); return q;
        }
        /// <summary>A shop item for an accessory (own asset "SO_Item_*": the accessory itself is "SO_" + id; same id so the inventory matches it).</summary>
        static FurnitureDefinition AccessoryItem(AccessoryDefinition acc, int price, string desc)
        {
            var f = Make<FurnitureDefinition>("SO_Item_" + acc.id);
            f.id = acc.id; f.displayName = acc.displayName; f.prefab = acc.prefab; f.footprint = Vector2Int.one; f.tags = InteractionTag.None; f.defaultCell = Vector2Int.zero; f.capacity = 1;
            f.category = ItemCategory.Accessory; f.price = price; f.description = desc; f.uiColor = acc.uiColor; f.starter = false;
            EditorUtility.SetDirty(f); return f;
        }
        static FurnitureDefinition Item(string id, string name, string desc, GameObject prefab, ItemCategory cat, int price, Vector2Int fp, InteractionTag tag, Color ui)
        {
            var f = Furniture(id, name, prefab, fp, tag, Vector2Int.zero);
            f.category = cat; f.price = price; f.description = desc; f.uiColor = ui; f.starter = false; EditorUtility.SetDirty(f); return f;
        }
        static FurnitureDefinition Furniture(string id, string name, GameObject prefab, Vector2Int fp, InteractionTag tag, Vector2Int cell)
        {
            var f = Make<FurnitureDefinition>("SO_" + id); f.id = id; f.displayName = name; f.prefab = prefab; f.footprint = fp; f.tags = tag; f.defaultCell = cell; f.capacity = 1; EditorUtility.SetDirty(f); return f;
        }
    }
}

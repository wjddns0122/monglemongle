using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mongle.Core;
using Mongle.Data;
using Mongle.Photo;
using Mongle.Toy;
using Mongle.UI;
using Mongle.Village;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Mongle.EditorTools
{
    /// <summary>
    /// D-035: writes the editable screen prefabs, Resources/UI/Skins/&lt;screen&gt;.prefab. Each screen is built by the game's own code with
    /// sample contents; every object it made gets a <see cref="UiSkinPart"/> that remembers its path and how it looked, lists keep one
    /// sample item (the model for all), and the canvas is saved as the prefab.
    /// A prefab that already exists is the designer's work: "만들기" leaves it alone, "다시 만들기" asks before overwriting.
    /// </summary>
    public static class UiSkinBaker
    {
        /// <summary>D-039: the prefabs live in the UI folder, one subfolder per scene (<see cref="UiEditFolder.SkinPath"/>).</summary>
        public const string Dir = UiEditFolder.Root;

        class Screen
        {
            public string name, label;
            public Func<Scene, GameObject> build;
            /// <summary>Lists the code fills: path under the canvas → name prefixes of its items.</summary>
            public Dictionary<string, string> lists = new Dictionary<string, string>();
        }

        static readonly Screen[] Screens =
        {
            new Screen { name = "RepairScreen", label = "수선 화면", build = BuildRepair, lists = { ["Custom"] = "Acc_" } },
            new Screen
            {
                name = VillageDirector.SkinName, label = "마을 화면", build = BuildVillage,
                lists =
                {
                    ["ShopPanel/Cards"] = "Card_,Quest_", ["StoragePanel/Storage/Viewport/Cards"] = "Card_",
                    ["QuestBoard/Viewport/Rows"] = "QuestRow_", ["FriendDialog/Choices"] = "Choice_", ["FriendDialog/Footer"] = "DialogBtn_",
                    ["DressPanel/List"] = "Dress_",
                },
            },
            new Screen { name = PhotoDirector.SkinName, label = "사진 화면", build = BuildPhoto },
            new Screen { name = GameRoot.OverlaySkin, label = "알림", build = BuildOverlay },
        };

        [MenuItem("Mongle/UI/화면 프리팹 만들기 (없는 것만)")]
        static void MenuMissing()
        {
            var made = BakeAll(false);
            EditorUtility.DisplayDialog("화면 프리팹", made.Count == 0 ? "새로 만든 화면이 없어요 (모두 있어요)." : string.Join(", ", made) + " 을(를) " + Dir + " 에 만들었어요.", "확인");
        }

        [MenuItem("Mongle/UI/화면 프리팹 다시 만들기 (덮어쓰기)")]
        static void MenuRemake()
        {
            if (!EditorUtility.DisplayDialog("화면 프리팹 다시 만들기", "모든 화면 프리팹을 지금 코드 모양으로 새로 만들어요.\n프리팹에서 직접 고친 내용은 모두 없어져요. 계속할까요?", "다시 만들기", "취소")) return;
            BakeAll(true);
        }

        /// <summary>For -batchmode -executeMethod: makes the missing prefabs (and those named by -skinRemake=A,B) and quits.</summary>
        public static void BakeBatch()
        {
            int code = 0;
            try
            {
                var remake = Environment.GetCommandLineArgs().Where(a => a.StartsWith("-skinRemake=")).SelectMany(a => a.Substring(12).Split(',')).ToList();
                Debug.Log("[UiSkin] made " + string.Join(", ", BakeAll(false, remake)));
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        public static List<string> BakeAll(bool overwrite, ICollection<string> remake = null)
        {
            var made = new List<string>();
            foreach (var s in Screens)
            {
                var path = UiEditFolder.SkinPath(s.name);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (!overwrite && File.Exists(path) && (remake == null || !remake.Contains(s.name))) continue;
                Bake(s, path); made.Add(s.label);
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            UiEditFolder.RefreshIndex();
            return made;
        }

        static void Bake(Screen s, string path)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var before = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
            GameObject root = null;
            UiSkin.Off = true;   // the code's own screen, not one with the old prefab laid over it
            try
            {
                root = s.build(scene);
                Settle(root);
                foreach (var kv in s.lists) { var list = UiSkin.At(root.transform, kv.Key); if (list != null) KeepOneEach(list, kv.Value.Split(',')); }
                foreach (Transform c in root.transform) Mark(c, UiSkin.KeyOf(c));
                foreach (var kv in s.lists)
                {
                    var list = UiSkin.At(root.transform, kv.Key);
                    if (list != null) list.GetComponent<UiSkinPart>().listOf = kv.Value;
                    else Debug.LogWarning("[UiSkin] " + s.name + ": no list at " + kv.Key);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UiSkin.Off = false;
                if (root != null) Object.DestroyImmediate(root.transform.root.gameObject);
                EditorSceneManager.ClosePreviewScene(scene);
                // the UI kit adds an EventSystem to the open scene when it builds a canvas; the prefab does not need it
                foreach (var es in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include))
                    if (es != null && Array.IndexOf(before, es) < 0) Object.DestroyImmediate(es.gameObject);
            }
            // every part remembers how it looks in the saved prefab, so later only the designer's changes are laid over the game
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var part in contents.GetComponentsInChildren<UiSkinPart>(true)) part.baked = UiSkin.Snapshot(part.transform);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            Debug.Log("[UiSkin] " + s.name + " -> " + path);
        }

        /// <summary>Plates sized to their texts (as the game does every frame), and only look components left: the game's
        /// behaviour scripts (hover tips, the claw's hold buttons …) stay in the code.</summary>
        static void Settle(GameObject root)
        {
            foreach (var f in root.GetComponentsInChildren<FitToText>(true)) f.Apply();
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var t = mb.GetType();
                if ((t.Namespace ?? "").StartsWith("UnityEngine") || t == typeof(FitToText)) continue;
                Object.DestroyImmediate(mb);
            }
        }

        /// <summary>A list keeps one sample item of each kind: editing it changes every item the game makes.</summary>
        static void KeepOneEach(Transform list, string[] prefixes)
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < list.childCount; i++)
            {
                var c = list.GetChild(i);
                var p = prefixes.FirstOrDefault(x => x == "*" || c.name.StartsWith(x, StringComparison.Ordinal));
                if (p == null) continue;
                if (seen.Add(p)) continue;
                Object.DestroyImmediate(c.gameObject); i--;
            }
        }

        static void Mark(Transform t, string path)
        {
            var part = t.gameObject.AddComponent<UiSkinPart>(); part.bakedPath = path;
            foreach (Transform c in t) Mark(c, path + "/" + UiSkin.KeyOf(c));
        }

        /// <summary>
        /// For -batchmode -executeMethod: pictures of every screen into docs/40_reports/shots/skins — the prefab as it is (&lt;name&gt;_prefab.png)
        /// and the screen the game builds with the prefab laid over it (&lt;name&gt;_game.png), to check that edits reach the game.
        /// </summary>
        public static void PreviewBatch()
        {
            int code = 0;
            try
            {
                const string outDir = "docs/40_reports/shots/skins";
                Directory.CreateDirectory(outDir);
                foreach (var s in Screens)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiEditFolder.SkinPath(s.name)); if (prefab == null) continue;
                    var scene = EditorSceneManager.NewPreviewScene();
                    GameObject built = null;
                    try
                    {
                        var copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                        Shot(copy.GetComponent<Canvas>(), scene, outDir + "/" + s.name + "_prefab.png");
                        // each switched-off panel on its own, as the designer sees it after ticking it on
                        foreach (Transform c in copy.transform)
                        {
                            if (c.gameObject.activeSelf || c.GetComponentsInChildren<Graphic>(true).Length == 0) continue;
                            c.gameObject.SetActive(true);
                            Shot(copy.GetComponent<Canvas>(), scene, outDir + "/" + s.name + "__" + c.name + ".png");
                            c.gameObject.SetActive(false);
                        }
                        Object.DestroyImmediate(copy);
                        built = s.build(scene);
                        UiSkin.Apply(built.transform, s.name);
                        Shot(built.GetComponent<Canvas>(), scene, outDir + "/" + s.name + "_game.png");
                    }
                    finally
                    {
                        if (built != null) Object.DestroyImmediate(built.transform.root.gameObject);
                        EditorSceneManager.ClosePreviewScene(scene);
                    }
                }
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        static void Shot(Canvas canvas, Scene scene, string file)
        {
            var camGo = new GameObject("PreviewCam"); SceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.AddComponent<Camera>(); cam.enabled = false; cam.scene = scene; cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color32(150, 120, 100, 255);
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1f;
            foreach (var f in canvas.GetComponentsInChildren<FitToText>(true)) f.Apply();
            Canvas.ForceUpdateCanvases(); cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply();
            RenderTexture.active = prev; File.WriteAllBytes(file, tex.EncodeToPNG());
            rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(camGo);
        }

        static T Host<T>(Scene scene, string name) where T : Component
        {
            var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene);
            return go.AddComponent<T>();
        }

        // ---------------- screens ----------------

        /// <summary>수선 화면: banner, 진료 수첩 (one row mended, one current, one found, one unknown), tool shelf, zoom, back, gear;
        /// the dialog, the stamp sheet and the dress-up list are in it too, switched off (switch them on to edit them).</summary>
        static GameObject BuildRepair(Scene scene)
        {
            var catalog = ContentCatalog.Load();
            var ui = Host<ShopUI>(scene, "RepairScreen"); ui.Build();
            ui.Banner("배의 얼룩 · 알맞은 도구를 골라요");
            var def = catalog.ToyDef("TeddyBear_001") ?? catalog.toy;
            var ids = def.damageSequence.Where(d => d != null).Select(d => d.id).Take(4).ToList();
            var toy = new ToyRuntime(def.kindId, ids);
            if (toy.Damages.Count > 0) toy.Damages[0].status = DamageStatus.Completed;
            if (toy.Damages.Count > 1) toy.Damages[1].status = DamageStatus.Active;
            if (toy.Damages.Count > 2) toy.Damages[2].status = DamageStatus.Discovered;
            ui.ShowNotebook(true); ui.RefreshNotebook(toy, catalog, ids.Count > 1 ? ids[1] : null);
            ui.BuildToolShelf(catalog.tools.Where(t => t != null).ToArray(), _ => { }); ui.ShowShelf(true);
            ui.SetInspectControls(true); ui.ShowBack(true);
            ui.BuildCustomization(catalog.AllAccessories.Take(6).ToArray(), _ => { }, () => { }); ui.ShowCustomization(false);
            return ui.Canvas.gameObject;
        }

        /// <summary>마을 화면: the HUD (status, coins, tiles, 퀘스트 button and tracker, zoom); every panel switched off.</summary>
        static GameObject BuildVillage(Scene scene)
        {
            var root = Host<VillageDirector>(scene, "VillageScreen").BuildSkinSample(ContentCatalog.Load());
            var top = root.transform.root.gameObject;
            if (top.scene != scene) SceneManager.MoveGameObjectToScene(top, scene);
            return root;
        }

        /// <summary>사진 화면: the frame, the caption and the three round buttons; the photo sheet switched off.</summary>
        static GameObject BuildPhoto(Scene scene) => Host<PhotoDirector>(scene, "PhotoScreen").BuildSkinSample();

        /// <summary>알림: the notice pill (with the coin) at the top, and the fade over the whole screen.</summary>
        static GameObject BuildOverlay(Scene scene) => Host<GameRoot>(scene, "Overlay").BuildSkinSample();
    }
}

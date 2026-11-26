using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mongle.Core;
using Mongle.Photo;
using Mongle.UI;
using Mongle.Village;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mongle.EditorTools
{
    /// <summary>
    /// D-039: the UI folder, Assets/_UI수정 — everything a designer edits, one subfolder per scene: that scene's screen prefabs and a
    /// link that opens the scene. Every game scene holds an editable copy of its screen (<see cref="UiEditCopy"/>, tagged EditorOnly).
    /// Edits made in a scene to any prefab from this folder are written into the prefab file when the scene is saved or Play is
    /// pressed, so the files here are always what the game shows (and what "고친 UI 내보내기" sends). No code to touch.
    /// </summary>
    [InitializeOnLoad]
    public static class UiEditFolder
    {
        public const string Root = "Assets/_UI수정";
        public const string TitleDir = Root + "/1_타이틀";
        public const string RepairDir = Root + "/2_수선점";
        public const string VillageDir = Root + "/3_마을";
        public const string PhotoDir = Root + "/4_사진관";
        public const string CommonDir = Root + "/5_공통";
        public const string NewDesignDir = Root + "/6_미리보기";
        /// <summary>D-040: the code's own screens (still drawn under the Figma ones, and showing the windows Figma has no design for).</summary>
        public const string OldSub = "예전화면";
        public const string IndexPath = "Assets/_Project/Resources/UI/SkinIndex.asset";
        const string Scenes = "Assets/_Project/Scenes/";

        /// <summary>The code's screen prefabs (their <see cref="UiSkin"/> names) → their scene folder.</summary>
        public static readonly Dictionary<string, string> SkinDirs = new Dictionary<string, string>
        {
            ["RepairScreen"] = RepairDir,
            [VillageDirector.SkinName] = VillageDir,
            [PhotoDirector.SkinName] = PhotoDir,
            [GameRoot.OverlaySkin] = CommonDir,
        };

        public static string SkinPath(string skin) => (SkinDirs.TryGetValue(skin, out var d) ? d : CommonDir) + "/" + OldSub + "/" + skin + ".prefab";

        /// <summary>D-040: the Figma screens → their scene folder (HouseInvite has no twin in the game yet: preview only).</summary>
        public static readonly Dictionary<string, string> FrontDirs = new Dictionary<string, string>
        {
            ["Title"] = TitleDir, ["RepairHud"] = RepairDir, ["Intake"] = RepairDir,
            ["VillageHud"] = VillageDir, ["Quest"] = VillageDir, ["Storage"] = VillageDir, ["RideInvite"] = VillageDir,
            ["Photo"] = PhotoDir, ["Settings"] = CommonDir, ["HouseInvite"] = NewDesignDir,
        };

        public static string FrontPath(string name) => (FrontDirs.TryGetValue(name, out var d) ? d : NewDesignDir) + "/" + name + ".prefab";

        class Copy
        {
            public string scene, label; public (string name, bool on)[] fronts; public int order;
        }

        /// <summary>The editable copies of the Figma screens in each scene (the first one shown, the windows switched off).</summary>
        static readonly Copy[] Copies =
        {
            new Copy { scene = Scenes + "Bootstrap.unity", label = "타이틀 화면", fronts = new[] { ("Title", true), ("Settings", false) }, order = 50 },
            new Copy { scene = Scenes + "RepairShop.unity", label = "수선점 화면", fronts = new[] { ("RepairHud", true), ("Intake", false) }, order = 10 },
            new Copy { scene = Scenes + "Village.unity", label = "마을 화면", fronts = new[] { ("VillageHud", true), ("Quest", false), ("Storage", false), ("RideInvite", false), ("Intake", false) }, order = 10 },
            new Copy { scene = Scenes + "PhotoStudio.unity", label = "사진관 화면", fronts = new[] { ("Photo", true) }, order = 10 },
        };

        /// <summary>Folder → (link name, scene it opens).</summary>
        static readonly (string dir, string name, string scene)[] Links =
        {
            (TitleDir, "타이틀 씬 열기", Scenes + "Bootstrap.unity"),
            (RepairDir, "수선점 씬 열기", Scenes + "RepairShop.unity"),
            (VillageDir, "마을 씬 열기", Scenes + "Village.unity"),
            (PhotoDir, "사진관 씬 열기", Scenes + "PhotoStudio.unity"),
            (CommonDir, "타이틀 씬 열기 (설정 창)", Scenes + "Bootstrap.unity"),
            (NewDesignDir, "미리보기 씬 열기", Scenes + "UiPreview.unity"),
        };

        static UiEditFolder()
        {
            EditorSceneManager.sceneSaving += (scene, path) => ApplyEdits(scene);
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.ExitingEditMode) ApplyAllOpen(); };
        }

        // ---------------- edits in a scene → the prefab files ----------------

        public static int ApplyAllOpen()
        {
            int n = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++) n += ApplyEdits(SceneManager.GetSceneAt(i));
            return n;
        }

        /// <summary>Writes the scene's edits of prefabs from the UI folder into the prefab files. Returns how many prefabs changed.</summary>
        public static int ApplyEdits(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || EditorApplication.isPlaying) return 0;
            int n = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var go = t.gameObject;
                    if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go)) continue;
                    var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                    if (!InFolder(path)) continue;
                    if (!PrefabUtility.HasPrefabInstanceAnyOverrides(go, false) || !RealEdits(go, out var real)) continue;
                    if (PointsOutside(go)) { Debug.LogWarning("[UI수정] " + go.name + ": 씬의 다른 오브젝트를 가리키는 칸이 있어서 파일에 자동 저장하지 않았어요.", go); continue; }
                    try { ApplyOnly(go, path, real); }
                    catch (System.Exception e) { Debug.LogWarning("[UI수정] 하나씩 저장하지 못해서 통째로 저장해요: " + e.Message); PrefabUtility.ApplyPrefabInstance(go, InteractionMode.AutomatedAction); }
                    Debug.Log("[UI수정] " + path.Normalize(NormalizationForm.FormC) + " 에 저장했어요.");
                    n++;
                }
            return n;
        }

        /// <summary>Is the asset in the UI folder? (macOS hands Korean paths back decomposed, so both sides are composed first.)</summary>
        public static bool InFolder(string assetPath) =>
            !string.IsNullOrEmpty(assetPath) && assetPath.Normalize(NormalizationForm.FormC).StartsWith(Root + "/", System.StringComparison.Ordinal);

        /// <summary>Has the copy really been edited? Float rounding (36.2 → 36.19995: Unity re-derives positions against the scene's
        /// canvas) and leftovers of objects no longer in the prefab are not edits, so opening and saving a scene never rewrites a
        /// prefab file for nothing. <paramref name="real"/>: the edited settings.</summary>
        static bool RealEdits(GameObject inst, out List<PropertyModification> real)
        {
            real = new List<PropertyModification>();
            bool structural = PrefabUtility.GetAddedGameObjects(inst).Count > 0 || PrefabUtility.GetAddedComponents(inst).Count > 0
                || PrefabUtility.GetRemovedComponents(inst).Count > 0 || PrefabUtility.GetRemovedGameObjects(inst).Count > 0;
            var mods = PrefabUtility.GetPropertyModifications(inst); if (mods == null) return structural;
            var cache = new Dictionary<Object, SerializedObject>();
            var top = PrefabUtility.GetCorrespondingObjectFromSource(inst);
            foreach (var m in mods)
            {
                if (m.target == null || PrefabUtility.IsDefaultOverride(m)) continue;
                if (m.target == top && m.propertyPath == "m_IsActive") continue;   // showing / hiding the whole screen in the scene
                if (m.propertyPath.StartsWith("m_fontColor32") || m.propertyPath == "m_TextStyleHashCode") continue;   // TextMeshPro fills these in itself
                if (Driven(m.target, m.propertyPath)) continue;   // placed by a layout / slider, not by hand
                if (!cache.TryGetValue(m.target, out var so)) cache[m.target] = so = new SerializedObject(m.target);
                var p = so.FindProperty(m.propertyPath);
                if (p == null) continue;   // a leftover
                if (p.propertyType == SerializedPropertyType.Float && float.TryParse(m.value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var f) && Mathf.Abs(f - p.floatValue) <= 1e-3f) continue;
                if (Differs(m, p)) real.Add(m);
            }
            return structural || real.Count > 0;
        }

        /// <summary>Writes only the real edits (objects added or removed, edited settings) into the prefab file.</summary>
        static void ApplyOnly(GameObject inst, string assetPath, List<PropertyModification> real)
        {
            const InteractionMode Auto = InteractionMode.AutomatedAction;
            foreach (var x in PrefabUtility.GetRemovedGameObjects(inst)) x.Apply(assetPath, Auto);
            foreach (var x in PrefabUtility.GetRemovedComponents(inst)) x.Apply(assetPath, Auto);
            foreach (var x in PrefabUtility.GetAddedGameObjects(inst)) x.Apply(assetPath, Auto);
            foreach (var x in PrefabUtility.GetAddedComponents(inst)) x.Apply(assetPath, Auto);
            if (real.Count == 0) return;
            // the copy's objects by the prefab's objects
            var mine = new Dictionary<Object, Object>();
            foreach (var c in inst.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                var src = PrefabUtility.GetCorrespondingObjectFromSource(c); if (src != null) mine[src] = c;
                var gsrc = PrefabUtility.GetCorrespondingObjectFromSource(c.gameObject); if (gsrc != null) mine[gsrc] = c.gameObject;
            }
            foreach (var m in real)
            {
                if (!mine.TryGetValue(m.target, out var o)) continue;
                var prop = new SerializedObject(o).FindProperty(m.propertyPath);
                if (prop != null) PrefabUtility.ApplyPropertyOverride(prop, assetPath, Auto);
            }
        }

        /// <summary>A place or size that a component sets (a layout group, a size fitter, a slider or scrollbar handle, a scrolled
        /// list): Unity resets these while saving, so they are never the designer's edit.</summary>
        static bool Driven(Object target, string path)
        {
            if (!(target is RectTransform rt)) return false;
            bool place = path.StartsWith("m_Anchor") || path.StartsWith("m_SizeDelta") || path.StartsWith("m_Pivot") || path.StartsWith("m_LocalPosition");
            if (!place) return false;
            var le = rt.GetComponent<UnityEngine.UI.LayoutElement>();
            if (rt.parent != null && (le == null || !le.ignoreLayout))
                foreach (var g in rt.parent.GetComponents<UnityEngine.UI.LayoutGroup>()) if (g.enabled) return true;
            foreach (var c in rt.GetComponents<MonoBehaviour>())
                if (c is UnityEngine.UI.ILayoutSelfController && c.enabled && path.StartsWith("m_SizeDelta")) return true;
            // a row / column box: its size is set by the layout tool (UiV2Layout), and a copy saved while a size fitter drove it
            // holds 0 — that must never be written back over the box
            if (path.StartsWith("m_SizeDelta") && rt.GetComponent<UnityEngine.UI.LayoutGroup>() != null) return true;
            foreach (var s in rt.GetComponentsInParent<UnityEngine.UI.Slider>(true)) if (s.fillRect == rt || s.handleRect == rt) return true;
            foreach (var s in rt.GetComponentsInParent<UnityEngine.UI.Scrollbar>(true)) if (s.handleRect == rt) return true;
            foreach (var s in rt.GetComponentsInParent<UnityEngine.UI.ScrollRect>(true)) if (s.content == rt && path.StartsWith("m_Anchored")) return true;
            return false;
        }

        static bool Differs(PropertyModification m, SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Float: return true;   // beyond rounding (checked before)
                case SerializedPropertyType.ObjectReference:
                    var o = m.objectReference;
                    var src = o != null && !EditorUtility.IsPersistent(o) ? PrefabUtility.GetCorrespondingObjectFromSource(o) : o;
                    return src != p.objectReferenceValue;
                case SerializedPropertyType.String: return m.value != p.stringValue;
                case SerializedPropertyType.Boolean: return (m.value == "1") != p.boolValue;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Character:
                    long cur = p.propertyType == SerializedPropertyType.Integer ? p.longValue : p.intValue;
                    return !long.TryParse(m.value, out var l) || (l != cur && (uint)l != (uint)cur);
                default: return true;
            }
        }

        /// <summary>An override that links to an object outside the copy (a scene object) cannot go into the prefab file.</summary>
        static bool PointsOutside(GameObject root)
        {
            var mods = PrefabUtility.GetPropertyModifications(root); if (mods == null) return false;
            foreach (var m in mods)
            {
                var o = m.objectReference; if (o == null || EditorUtility.IsPersistent(o)) continue;
                var t = o is GameObject g ? g.transform : o is Component c ? c.transform : null;
                if (t == null || !t.IsChildOf(root.transform)) return true;
            }
            return false;
        }

        // ---------------- the folder ----------------

        [MenuItem("Mongle/공유/0. UI 수정 폴더 보기", priority = -1)]
        public static void Reveal()
        {
            var f = AssetDatabase.LoadAssetAtPath<Object>(Root); if (f == null) return;
            EditorUtility.FocusProjectWindow(); Selection.activeObject = f; EditorGUIUtility.PingObject(f);
        }

        [MenuItem("Mongle/UI/UI 수정 폴더 정리 (없는 것만 채우기)")]
        static void MenuSetup()
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("UI 수정 폴더", "플레이(▶)를 먼저 멈춰 주세요.", "확인"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Debug.Log("[UI수정] " + string.Join(" · ", Setup()));
            Reveal();
        }

        /// <summary>Makes the folder (only what is missing): moves the screen prefabs in (their links stay), fills the list the game
        /// reads, adds the scene links and puts the editable copies into each scene. Safe to run again.</summary>
        public static List<string> Setup()
        {
            var log = new List<string>();
            // D-039 names → D-040 names
            RenameFolder(Root + "/5_공통알림", CommonDir, log);
            RenameFolder(Root + "/6_새디자인_미리보기", NewDesignDir, log);
            foreach (var d in new[] { TitleDir, RepairDir, VillageDir, PhotoDir, CommonDir, NewDesignDir }) MakeFolder(d);
            foreach (var d in new[] { TitleDir, RepairDir, VillageDir, PhotoDir, CommonDir }) MakeFolder(d + "/" + OldSub);

            foreach (var kv in SkinDirs)
                foreach (var from in new[] { "Assets/_Project/Resources/UI/Skins/" + kv.Key + ".prefab", kv.Value + "/" + kv.Key + ".prefab" })
                    Move(from, SkinPath(kv.Key), log);
            foreach (var from in new[] { "Assets/_Project/Prefabs/UI/TitleScreen.prefab", TitleDir + "/TitleScreen.prefab" })
                Move(from, TitleScreenBaker.PrefabPath, log);
            foreach (var kv in FrontDirs)
                foreach (var from in new[] { "Assets/_Project/Prefabs/UI2/" + kv.Key + ".prefab", NewDesignDir + "/" + kv.Key + ".prefab" })
                    if (from != FrontPath(kv.Key)) Move(from, FrontPath(kv.Key), log);
            foreach (var old in new[] { "Assets/_Project/Resources/UI/Skins", "Assets/_Project/Prefabs/UI", "Assets/_Project/Prefabs/UI2" })
                if (AssetDatabase.IsValidFolder(old) && AssetDatabase.FindAssets("", new[] { old }).Length == 0) { AssetDatabase.DeleteAsset(old); log.Add("빈 폴더 정리 " + old); }
            var oldLink = CommonDir + "/공통알림 씬 열기 (타이틀 씬).asset";
            if (AssetDatabase.LoadAssetAtPath<Object>(oldLink) != null) { AssetDatabase.DeleteAsset(oldLink); log.Add("옛 링크 정리"); }

            RefreshIndex();
            foreach (var l in Links) MakeLink(l.dir, l.name, l.scene, log);
            foreach (var c in Copies) PutCopies(c, log);
            AssetDatabase.SaveAssets();
            return log;
        }

        static void RenameFolder(string from, string to, List<string> log)
        {
            if (!AssetDatabase.IsValidFolder(from) || AssetDatabase.IsValidFolder(to)) return;
            var err = AssetDatabase.MoveAsset(from, to);
            log.Add(string.IsNullOrEmpty(err) ? "폴더 이름 " + Path.GetFileName(from) + " → " + Path.GetFileName(to) : "폴더 이름 못 바꿈 " + from + ": " + err);
        }

        static void MakeFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            MakeFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void Move(string from, string to, List<string> log)
        {
            if (from == to || AssetDatabase.LoadAssetAtPath<Object>(from) == null || AssetDatabase.LoadAssetAtPath<Object>(to) != null) return;
            var err = AssetDatabase.MoveAsset(from, to);
            log.Add(string.IsNullOrEmpty(err) ? "옮김 " + Path.GetFileName(to) + " → " + Path.GetDirectoryName(to) : "못 옮김 " + from + ": " + err);
        }

        /// <summary>The list of screen prefabs the game reads (Resources/UI/SkinIndex): the code's screens and the Figma ones.</summary>
        public static void RefreshIndex()
        {
            var index = AssetDatabase.LoadAssetAtPath<UiSkinIndex>(IndexPath);
            if (index == null)
            {
                index = ScriptableObject.CreateInstance<UiSkinIndex>();
                Directory.CreateDirectory(Path.GetDirectoryName(IndexPath));
                AssetDatabase.CreateAsset(index, IndexPath);
            }
            var skins = new List<GameObject>();
            foreach (var skin in SkinDirs.Keys)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(SkinPath(skin)) ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/UI/Skins/" + skin + ".prefab");
                if (p != null) skins.Add(p);
            }
            var fronts = new List<GameObject>();
            foreach (var name in FrontDirs.Keys) { var p = AssetDatabase.LoadAssetAtPath<GameObject>(FrontPath(name)); if (p != null) fronts.Add(p); }
            if (index.skins != null && index.skins.SequenceEqual(skins) && index.fronts != null && index.fronts.SequenceEqual(fronts)) return;
            index.skins = skins.ToArray(); index.fronts = fronts.ToArray();
            EditorUtility.SetDirty(index);
            AssetDatabase.SaveAssetIfDirty(index);
        }

        static void MakeLink(string dir, string name, string scene, List<string> log)
        {
            var path = dir + "/" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<UiSceneLink>(path) != null) return;
            var link = ScriptableObject.CreateInstance<UiSceneLink>();
            link.scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
            AssetDatabase.CreateAsset(link, path);
            log.Add("링크 " + path);
        }

        /// <summary>
        /// The editable copies in a scene: a holder "UI 수정 · …" (EditorOnly, hidden in the game) with a canvas holding the scene's
        /// Figma screens; the D-039 copy of the code's screen stays beside it, switched off ("예전 화면").
        /// </summary>
        static void PutCopies(Copy c, List<string> log)
        {
            var scene = SceneManager.GetSceneByPath(c.scene);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(c.scene, OpenSceneMode.Additive);
            try
            {
                bool changed = false; UiEditCopy front = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (!root.TryGetComponent<UiEditCopy>(out var hold)) continue;
                    bool old = false;
                    foreach (Transform ch in hold.transform)
                    {
                        var src = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(ch.gameObject);
                        if (!string.IsNullOrEmpty(src) && src.Normalize(NormalizationForm.FormC).Contains("/" + OldSub + "/")) old = true;
                    }
                    if (old)
                    {
                        const string oldName = "UI 수정 · 예전 화면 (피그마에 없는 창, 켜서 고치기)";
                        if (root.name != oldName || root.activeSelf) { root.name = oldName; root.SetActive(false); changed = true; }
                    }
                    else front = hold;
                }
                if (front == null)
                {
                    var holder = new GameObject("UI 수정 · " + c.label) { tag = "EditorOnly" };
                    SceneManager.MoveGameObjectToScene(holder, scene);
                    holder.AddComponent<UiEditCopy>();
                    var cgo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
                    cgo.layer = 5; cgo.transform.SetParent(holder.transform, false);
                    var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = c.order;
                    cgo.AddComponent<Mongle.UI2.AdaptiveScaler>();
                    foreach (var (name, on) in c.fronts)
                    {
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FrontPath(name));
                        if (prefab == null) { log.Add("화면 없음 " + name); continue; }
                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                        inst.transform.SetParent(cgo.transform, false);
                        var rt = (RectTransform)inst.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                        inst.SetActive(on);
                    }
                    holder.transform.SetAsLastSibling();
                    changed = true;
                    log.Add("씬에 넣음 " + c.label + " → " + Path.GetFileName(c.scene));
                }
                if (changed) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        /// <summary>Opens a scene from a link and points at its editable UI.</summary>
        public static void OpenScene(SceneAsset sceneAsset)
        {
            if (sceneAsset == null) return;
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("씬 열기", "플레이(▶)를 먼저 멈춰 주세요.", "확인"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(sceneAsset), OpenSceneMode.Single);
            GameObject show = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.activeSelf && root.GetComponent<UiEditCopy>() != null) { show = root; break; }
            if (show != null) { Selection.activeGameObject = show; EditorGUIUtility.PingObject(show); }
        }
    }
}

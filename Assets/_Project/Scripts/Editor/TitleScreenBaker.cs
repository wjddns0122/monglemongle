using System.IO;
using Mongle.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mongle.EditorTools
{
    /// <summary>
    /// D-030: puts the start screen into the Bootstrap scene as real objects (prefab Prefabs/UI/TitleScreen), so it is edited in Unity
    /// (Scene view + Inspector) instead of in code. Remaking the prefab throws away edits made in it, so the menu asks first.
    /// </summary>
    public static class TitleScreenBaker
    {
        public const string PrefabPath = UiEditFolder.TitleDir + "/" + UiEditFolder.OldSub + "/TitleScreen.prefab";
        const string ScenePath = "Assets/_Project/Scenes/Bootstrap.unity";

        [MenuItem("Mongle/UI/시작 화면을 씬에 꺼내기 (Bootstrap)")]
        static void Menu()
        {
            if (File.Exists(PrefabPath) && !EditorUtility.DisplayDialog("시작 화면 다시 만들기",
                "TitleScreen 프리팹을 피그마 배치로 새로 만들어요.\n프리팹에서 직접 고친 내용은 없어져요. 계속할까요?", "새로 만들기", "취소")) return;
            Bake(true);
        }

        /// <summary>Writes the prefab (only when missing, unless <paramref name="remake"/>) and makes sure the Bootstrap scene holds one instance.</summary>
        public static void Bake(bool remake)
        {
            bool made = remake || !File.Exists(PrefabPath);
            if (made)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                var t = TitleScreen.Build(null);
                PrefabUtility.SaveAsPrefabAsset(t.gameObject, PrefabPath);
                Object.DestroyImmediate(t.gameObject);
                Debug.Log("[TitleScreen] prefab written: " + PrefabPath);
            }
            PlaceInBootstrap(made);
        }

        /// <summary>
        /// Makes sure the Bootstrap scene holds one start screen (opened alongside, so the open scene is kept); after a remake the old
        /// instance is swapped for a fresh one, so no overrides of the previous layout are left on it.
        /// </summary>
        static void PlaceInBootstrap(bool replace)
        {
            var open = EditorSceneManager.GetSceneByPath(ScenePath);
            bool wasOpen = open.IsValid() && open.isLoaded;
            var scene = wasOpen ? open : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            bool has = false, changed = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<TitleScreen>(true) == null) continue;
                if (replace && root.GetComponent<TitleScreen>() != null) { Object.DestroyImmediate(root); changed = true; }
                else has = true;
            }
            if (!has) { AddTo(scene); changed = true; }
            if (changed) { EditorSceneManager.SaveScene(scene); Debug.Log("[TitleScreen] placed in " + ScenePath); }
            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
        }

        /// <summary>One instance of the prefab in <paramref name="scene"/> (SceneBuilder calls this for a freshly built Bootstrap scene).</summary>
        public static void AddTo(UnityEngine.SceneManagement.Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.name = "TitleScreen";
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}

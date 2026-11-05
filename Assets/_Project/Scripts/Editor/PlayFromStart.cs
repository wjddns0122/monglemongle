using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Mongle.EditorTools
{
    /// <summary>
    /// One-click manual play-through in the editor: always starts at the title (Bootstrap) scene, either with a brand-new
    /// throwaway save (the real save is left untouched) or with the normal save. Settings are cleared when play stops.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromStart
    {
        const string Key = "mongle.playFreshSave";
        const string Bootstrap = "Assets/_Project/Scenes/Bootstrap.unity";

        static PlayFromStart()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s != PlayModeStateChange.EnteredEditMode) return;
                SessionState.EraseString(Key);
                EditorSceneManager.playModeStartScene = null;
            };
        }

        [MenuItem("Mongle/Play From Start (new save)", priority = 0)]
        static void PlayNew()
        {
            if (EditorApplication.isPlaying) return;
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "PlayFresh");
            Directory.CreateDirectory(dir);
            SessionState.SetString(Key, Path.Combine(dir, "save_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json"));
            Begin();
        }

        [MenuItem("Mongle/Play From Title (my save)", priority = 1)]
        static void PlayMine()
        {
            if (EditorApplication.isPlaying) return;
            SessionState.EraseString(Key);
            Begin();
        }

        static void Begin()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { SessionState.EraseString(Key); return; }
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Bootstrap);
            EditorApplication.isPlaying = true;
        }
    }
}

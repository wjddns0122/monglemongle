using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mongle.EditorTools
{
    /// <summary>
    /// D-038 sharing: menu items for someone who edits the UI without git — open the UI preview in one click, and send the
    /// edited UI back as a single .unitypackage on the desktop (the UI folder Assets/_UI수정, UI pictures, texts, preview scene; no
    /// scripts, no game scenes: edits made in a scene are written into the prefab files, D-039).
    /// </summary>
    public static class ShareTools
    {
        /// <summary>Everything a UI edit can touch. Scripts and game data stay out, so importing it back cannot break code.</summary>
        public static readonly string[] UiPaths =
        {
            UiEditFolder.Root,                       // D-039: every screen prefab, one folder per scene
            "Assets/_Project/Resources/UI",
            "Assets/_Project/Resources/UI2",
            "Assets/_Project/Resources/Loc",
            "Assets/_Project/UI2Preview",
            "Assets/_Project/Scenes/UiPreview.unity",
        };

        [MenuItem("Mongle/공유/1. UI 미리보기 열기", priority = 0)]
        static void OpenPreview()
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("UI 미리보기", "플레이(▶)를 먼저 멈춰 주세요.", "확인"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/UiPreview.unity", OpenSceneMode.Single);
        }

        [MenuItem("Mongle/공유/2. 고친 UI 내보내기 (바탕화면에 파일 만들기)", priority = 1)]
        static void ExportUi()
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("고친 UI 내보내기", "플레이(▶)를 먼저 멈춰 주세요.", "확인"); return; }
            UiEditFolder.ApplyAllOpen();             // edits made in an open scene go into the prefab files first
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            var paths = new List<string>();
            foreach (var p in UiPaths) if (AssetDatabase.IsValidFolder(p) || File.Exists(p)) paths.Add(p);
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string file = Path.Combine(desktop, "몽글UI_수정본_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".unitypackage");
            AssetDatabase.ExportPackage(paths.ToArray(), file, ExportPackageOptions.Recurse);
            EditorUtility.RevealInFinder(file);
            EditorUtility.DisplayDialog("고친 UI 내보내기", "바탕화면에 파일을 만들었어요:\n" + Path.GetFileName(file) + "\n\n이 파일 하나만 보내 주면 돼요.", "확인");
        }
    }
}

using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Mongle.EditorTools
{
    /// <summary>macOS standalone test build used for real quit/relaunch verification.</summary>
    public static class BuildTool
    {
        public const string AppPath = "Builds/MongleMongle.app";

        public const string WindowsDir = "Builds/Windows/MongleMongle";

        static readonly string[] AllScenes = { "Assets/_Project/Scenes/Bootstrap.unity", "Assets/_Project/Scenes/RepairShop.unity", "Assets/_Project/Scenes/PhotoStudio.unity", "Assets/_Project/Scenes/Village.unity" };

        /// <summary>A Windows build to hand to a friend (D-015): rebuilds content and scenes first, then writes Builds/Windows/MongleMongle/MongleMongle.exe.</summary>
        [MenuItem("Mongle/Build Windows App")]
        public static void BuildWindows()
        {
            ContentBuilder.BuildAll(); SceneBuilder.BuildAll();
            Directory.CreateDirectory(WindowsDir);
            var opts = new BuildPlayerOptions { scenes = AllScenes, locationPathName = WindowsDir + "/MongleMongle.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.None };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            string text = $"result={s.result} size={s.totalSize} errors={s.totalErrors} warnings={s.totalWarnings} time={s.totalTime}";
            File.WriteAllText("Logs/build_windows_result.txt", text);
            Debug.Log("[Build] windows " + text);
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        [MenuItem("Mongle/Build macOS Test App")]
        public static void BuildMac()
        {
            Directory.CreateDirectory("Builds");
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/Bootstrap.unity", "Assets/_Project/Scenes/RepairShop.unity", "Assets/_Project/Scenes/PhotoStudio.unity", "Assets/_Project/Scenes/Village.unity" },
                locationPathName = AppPath, target = BuildTarget.StandaloneOSX, options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            string text = $"result={s.result} size={s.totalSize} errors={s.totalErrors} warnings={s.totalWarnings} time={s.totalTime}";
            File.WriteAllText("Logs/build_result.txt", text);
            Debug.Log("[Build] " + text);
        }
    }
}

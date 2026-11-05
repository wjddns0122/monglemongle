using System.Collections;
using System.IO;
using System.Linq;
using Mongle.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Mongle.Tests
{
    public class QaPlaythroughTests
    {
        static string Dir => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "qa_editor");

        static void FreshRoot(string saveFile)
        {
            if (GameRoot.Instance != null) Object.DestroyImmediate(GameRoot.Instance.gameObject);
            GameRoot.SavePathOverride = saveFile;
            var go = new GameObject("GameRoot"); Object.DontDestroyOnLoad(go); go.AddComponent<GameRoot>();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator CorruptSaveIsKeptAndPlayerDecides()
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "qa_corrupt");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "save.json"), "{ this is not json");
            FreshRoot(Path.Combine(dir, "save.json"));
            SceneManager.LoadScene("Bootstrap");
            yield return null; yield return null;
            var qa = QaDriver.StartForTest("corrupt", Path.Combine(dir, "qa_corrupt.txt"), Path.Combine(dir, "shots"));
            float deadline = Time.realtimeSinceStartup + 120f;
            while (!qa.Finished && Time.realtimeSinceStartup < deadline) yield return null;
            qa.ForceFlush();
            Assert.AreEqual(0, qa.Fails, string.Join("\n", qa.Lines.Where(l => l.StartsWith("FAIL") || l.StartsWith("LOGERR"))));
        }

        [UnityTest, Timeout(1200000)]
        public IEnumerator FullPlaythroughNewGame()
        {
            if (Directory.Exists(Dir)) Directory.Delete(Dir, true);
            Directory.CreateDirectory(Dir);
            FreshRoot(Path.Combine(Dir, "save.json"));
            SceneManager.LoadScene("Bootstrap");
            yield return null; yield return null;
            var qa = QaDriver.StartForTest("full", Path.Combine(Dir, "qa_full.txt"), Path.Combine(Dir, "shots"));
            float deadline = Time.realtimeSinceStartup + 900f;
            while (!qa.Finished && Time.realtimeSinceStartup < deadline) yield return null;
            qa.ForceFlush();
            Assert.AreEqual(0, qa.Fails, string.Join("\n", qa.Lines.Where(l => l.StartsWith("FAIL") || l.StartsWith("LOGERR"))));
            Assert.Greater(qa.Passes, 40);
        }
    }
}

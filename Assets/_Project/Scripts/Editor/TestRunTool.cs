using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Mongle.EditorTools
{
    /// <summary>
    /// Runs Test Runner suites from code and writes a plain result file (Logs/&lt;Mode&gt;_results.txt).
    /// Callbacks are registered on every domain load and keep their counters in SessionState, so PlayMode runs
    /// (which reload the domain) still report.
    /// </summary>
    [InitializeOnLoad]
    public static class TestRunTool
    {
        const string KMode = "mongle.test.mode", KPass = "mongle.test.pass", KFail = "mongle.test.fail", KSkip = "mongle.test.skip", KText = "mongle.test.text";
        static readonly TestRunnerApi api;

        static TestRunTool()
        {
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
        }

        public static string ResultFile(string mode) => Path.Combine(Directory.GetCurrentDirectory(), "Logs", mode + "_results.txt");

        public static void Run(TestMode mode, string testNameContains = null)
        {
            SessionState.SetString(KMode, mode.ToString()); SessionState.SetInt(KPass, 0); SessionState.SetInt(KFail, 0); SessionState.SetInt(KSkip, 0); SessionState.SetString(KText, "");
            File.WriteAllText(ResultFile(mode.ToString()), "RUNNING");
            var filter = new Filter { testMode = mode };
            if (!string.IsNullOrEmpty(testNameContains)) filter.testNames = new[] { testNameContains };
            api.Execute(new ExecutionSettings(filter));
        }

        class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor r)
            {
                if (r.HasChildren) return;
                if (r.TestStatus == TestStatus.Passed) SessionState.SetInt(KPass, SessionState.GetInt(KPass, 0) + 1);
                else if (r.TestStatus == TestStatus.Skipped || r.TestStatus == TestStatus.Inconclusive) SessionState.SetInt(KSkip, SessionState.GetInt(KSkip, 0) + 1);
                else
                {
                    SessionState.SetInt(KFail, SessionState.GetInt(KFail, 0) + 1);
                    SessionState.SetString(KText, SessionState.GetString(KText, "") + "FAIL " + r.FullName + "\n  " + r.Message + "\n  " + r.StackTrace + "\n");
                }
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                string mode = SessionState.GetString(KMode, "Unknown");
                string head = $"DONE mode={mode} passed={SessionState.GetInt(KPass, 0)} failed={SessionState.GetInt(KFail, 0)} skipped={SessionState.GetInt(KSkip, 0)}\n";
                File.WriteAllText(ResultFile(mode), head + SessionState.GetString(KText, ""));
            }
        }
    }
}

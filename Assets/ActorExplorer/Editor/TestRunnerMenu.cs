using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ActorExplorer.Editor
{
    /// Tools > ActorExplorer > Run EditMode Tests: 結果を Temp/ae-test-results.txt に書く（Cowork から読むため）。
    public static class TestRunnerMenu
    {
        public static readonly string ResultPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "ae-test-results.txt");

        [MenuItem("Tools/ActorExplorer/Run EditMode Tests")]
        public static void Run()
        {
            if (File.Exists(ResultPath)) File.Delete(ResultPath);
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { "ActorExplorer.Tests" } }));
        }

        class Callbacks : ICallbacks
        {
            readonly StringBuilder sb = new StringBuilder();
            public void RunStarted(ITestAdaptor t) { }
            public void TestStarted(ITestAdaptor t) { }
            public void TestFinished(ITestResultAdaptor r)
            {
                if (r.Test.IsSuite) return;
                sb.AppendLine($"{r.TestStatus}\t{r.Test.FullName}");
                if (r.TestStatus != TestStatus.Passed) sb.AppendLine("  " + (r.Message ?? "").Replace("\n", "\n  "));
            }
            public void RunFinished(ITestResultAdaptor r)
            {
                sb.Insert(0, $"RESULT {r.TestStatus} passed={r.PassCount} failed={r.FailCount} skipped={r.SkipCount}\n");
                File.WriteAllText(ResultPath, sb.ToString());
                Debug.Log("[ActorExplorer tests] " + sb.ToString().Split('\n')[0]);
            }
        }
    }
}

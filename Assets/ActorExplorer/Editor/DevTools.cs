using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ActorExplorer.Editor
{
    /// Tools > ActorExplorer > Settings: PlayerPrefs の設定を Editor から編集（ランタイム設定画面ができるまでの開発用）。
    public class SettingsWindow : EditorWindow
    {
        [MenuItem("Tools/ActorExplorer/Settings")]
        static void Open() => GetWindow<SettingsWindow>("ActorExplorer Settings");

        void OnGUI()
        {
            int idx = Array.IndexOf(Settings.Providers, Settings.Provider);
            int newIdx = EditorGUILayout.Popup("Provider", Math.Max(0, idx), Settings.Providers);
            if (newIdx != idx) Settings.ApplyProviderDefaults(Settings.Providers[newIdx]);
            Settings.BaseUrl = EditorGUILayout.TextField("Base URL", Settings.BaseUrl);
            Settings.Model = EditorGUILayout.TextField("Model", Settings.Model);
            Settings.ApiKey = EditorGUILayout.PasswordField("API Key", Settings.ApiKey);
            Settings.Language = EditorGUILayout.Popup("Language", Settings.Language == "en" ? 1 : 0, new[] { "ja", "en" }) == 1 ? "en" : "ja";
            Settings.HistoryLimit = EditorGUILayout.IntField("History limit", Settings.HistoryLimit);
            Settings.TypeSpeed = EditorGUILayout.FloatField("Type speed (chars/s)", Settings.TypeSpeed);
            EditorGUILayout.HelpBox("キーは PlayerPrefs にだけ保存されます（リポジトリには入りません）。", MessageType.None);
        }
    }

    /// Tools > ActorExplorer > GM Smoke Test: サンプルシナリオで導入描写→1 行動→判定要求まで 1 往復以上通るかを Console で確認。
    public static class GmSmokeTest
    {
        [MenuItem("Tools/ActorExplorer/GM Smoke Test")]
        static async void Run()
        {
            if (string.IsNullOrEmpty(Settings.ApiKey)) { Debug.LogError("[smoke] API key が未設定。Tools > ActorExplorer > Settings で入力してください"); return; }
            // EditorPrefs "ae.smoke.scenario" / "ae.smoke.actions"（改行区切り）で差し替え可。既定は sample-01。
            string scenario = EditorPrefs.GetString("ae.smoke.scenario", "sample-01");
            string[] actions = EditorPrefs.GetString("ae.smoke.actions", "[Alex] 待合室を隅々まで調べる。缶コーヒーにも触ってみる。\n[Alex] 倉庫へ行って、棚を整理整頓しながら使えそうな道具を探す。").Split('\n');
            var rs = Ruleset.Load("sample-d100");
            var pc = Actor.CreateRandom(rs, "Alex", SkillProfile.Generalist);
            var gm = GmLoop.New("sample-d100", scenario, new[] { pc }, Settings.Language);
            gm.OnEvent += e => Debug.Log("[smoke:event] " + e);
            try
            {
                Debug.Log($"[smoke] {Settings.Provider} {Settings.Model} @ {Settings.BaseUrl}");
                Debug.Log("[smoke:gm] " + await gm.Step(""));
                foreach (var a in actions) if (!string.IsNullOrWhiteSpace(a)) Debug.Log("[smoke:gm] " + await gm.Step(a.Trim()));
                Debug.Log("[smoke] skills used: " + string.Join(", ", gm.State.messages.Where(m => m.role == "tool" && m.content.Contains("\"skill\"")).Select(m => m.content)));
                Debug.Log($"[smoke] done. messages={gm.State.messages.Count} toolCalls={gm.State.messages.Count(m => m.role == "tool")} HP={pc.Resource("HP").value}/{pc.Resource("HP").max}");
            }
            catch (Exception e) { Debug.LogError("[smoke] " + e.Message); }
        }
    }

    /// Tools > ActorExplorer > UI Preview: Play 中にプレイ画面へ切り替え、見本のログ（GM/プレイヤー/判定/リソース）を流し込む。
    /// API を叩かずに見た目を確認するためのもの（UI 調整のとき用）。
    public static class UiPreview
    {
        [MenuItem("Tools/ActorExplorer/UI Preview (Play mode)")]
        static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[ui-preview] Play 中に実行してください"); return; }
            var app = UnityEngine.Object.FindFirstObjectByType<App>();
            if (app == null) { Debug.LogWarning("[ui-preview] App が見つかりません"); return; }
            var f = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var t = typeof(App);
            var show = t.GetMethod("Show", f); var log = t.GetMethod("Log", f); var ev = t.GetMethod("LogEvent", f); var win = t.GetMethod("SetWindow", f);
            var rule = new CheckRule();
            show.Invoke(app, new object[] { "play" });
            log.Invoke(app, new object[] { "秋の夕方。三年前に廃線になった支線の終着駅「霧谷」に、あなたたちは立っている。待合室のベンチには、まだ温かい缶コーヒーが置かれている。", "log-gm" });
            log.Invoke(app, new object[] { "[アクター 1] 倉庫へ行って、棚を整理整頓しながら使えそうな道具を探す。", "log-player" });
            ev.Invoke(app, new object[] { GmEvent.Check("アクター 1", "organize", Difficulty.Normal, Check.Resolve(rule, 65, Difficulty.Normal, 42)) });
            log.Invoke(app, new object[] { "棚は几帳面に整理されていて、保線用のヘッドランプとロープがすぐに見つかった。倉庫の奥、床板の隙間から、かすかにきしむ音がする。", "log-gm" });
            win.Invoke(app, new object[] { "GM", "棚は几帳面に整理されていて、保線用のヘッドランプとロープがすぐに見つかった。倉庫の奥、床板の隙間から、かすかにきしむ音がする。" });
            ev.Invoke(app, new object[] { GmEvent.Check("アクター 1", "climb", Difficulty.Hard, Check.Resolve(rule, 40, Difficulty.Hard, 77), true) });
            ev.Invoke(app, new object[] { GmEvent.Resource("アクター 1", "HP", 13, 11, 13, "瓦礫で足を切った") });
            ev.Invoke(app, new object[] { GmEvent.Check("アクター 1", "sense", Difficulty.Normal, Check.Resolve(rule, 55, Difficulty.Normal, 1)) });
            Debug.Log("[ui-preview] done");
        }
    }

    /// Tools > ActorExplorer > Fetch Fonts: 同梱フォントを配布元から取得して Assets/ActorExplorer/Fonts/ に置く（開発者用・初回のみ）。
    /// ライセンス文は同フォルダの LICENSE.txt（リポジトリに同梱済み）。
    public static class FontFetcher
    {
        static readonly (string url, string path)[] Files =
        {
            ("https://hicchicc.github.io/00ff/x12y16pxMaruMonica.ttf", "Assets/ActorExplorer/Fonts/MaruMonica/x12y16pxMaruMonica.ttf"),
            ("https://hicchicc.github.io/00ff/x14y24pxHeadUpDaisy.ttf", "Assets/ActorExplorer/Fonts/HeadUpDaisy/x14y24pxHeadUpDaisy.ttf"),
        };

        [MenuItem("Tools/ActorExplorer/Fetch Fonts")]
        static void Run()
        {
            using (var http = new System.Net.Http.HttpClient())
            {
                foreach (var (url, path) in Files)
                {
                    if (System.IO.File.Exists(path)) { Debug.Log($"[fonts] skip (exists): {path}"); continue; }
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                    var bytes = http.GetByteArrayAsync(url).GetAwaiter().GetResult();
                    System.IO.File.WriteAllBytes(path, bytes);
                    Debug.Log($"[fonts] {path} ({bytes.Length} bytes)");
                }
            }
            AssetDatabase.Refresh();
        }
    }
}

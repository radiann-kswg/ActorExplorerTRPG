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
            var rs = Ruleset.Load("sample-d100");
            var pc = Actor.Create(rs, "Alex");
            var gm = GmLoop.New("sample-d100", "sample-01", new[] { pc }, Settings.Language);
            gm.OnEvent += e => Debug.Log("[smoke:event] " + e);
            try
            {
                Debug.Log($"[smoke] {Settings.Provider} {Settings.Model} @ {Settings.BaseUrl}");
                Debug.Log("[smoke:gm] " + await gm.Step(""));
                Debug.Log("[smoke:gm] " + await gm.Step("[Alex] 待合室を隅々まで調べる。缶コーヒーにも触ってみる。"));
                Debug.Log($"[smoke] done. messages={gm.State.messages.Count} toolCalls={gm.State.messages.Count(m => m.role == "tool")} HP={pc.Resource("HP").value}/{pc.Resource("HP").max}");
            }
            catch (Exception e) { Debug.LogError("[smoke] " + e.Message); }
        }
    }
}

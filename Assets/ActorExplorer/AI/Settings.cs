using UnityEngine;

namespace ActorExplorer
{
    /// PlayerPrefs に保存する設定（ae.*）。API キーはここ以外に置かない・ログに出さない。
    public static class Settings
    {
        public static readonly string[] Providers = { "claude", "openai", "gemini", "custom" };

        public static string Provider { get => Get("ae.provider", "claude"); set => Set("ae.provider", value); }
        public static string BaseUrl { get => Get("ae.baseUrl", DefaultBaseUrl("claude")); set => Set("ae.baseUrl", value); }
        public static string Model { get => Get("ae.model", DefaultModel("claude")); set => Set("ae.model", value); }
        public static string ApiKey { get => Get("ae.apiKey", ""); set => Set("ae.apiKey", value); }
        public static string Language { get => Get("ae.language", "ja"); set => Set("ae.language", value); }
        public static int HistoryLimit { get => PlayerPrefs.GetInt("ae.historyLimit", 40); set => PlayerPrefs.SetInt("ae.historyLimit", value); }
        public static float TypeSpeed { get => PlayerPrefs.GetFloat("ae.typeSpeed", 40f); set => PlayerPrefs.SetFloat("ae.typeSpeed", value); }

        public static string DefaultBaseUrl(string provider) => provider switch
        {
            "claude" => "https://api.anthropic.com/v1/",
            "openai" => "https://api.openai.com/v1/",
            "gemini" => "https://generativelanguage.googleapis.com/v1beta/openai/",
            _ => "",
        };

        public static string DefaultModel(string provider) => provider switch
        {
            "claude" => "claude-sonnet-4-5",
            "openai" => "gpt-4.1-mini",
            "gemini" => "gemini-2.5-flash",
            _ => "",
        };

        /// プロバイダを切り替えたとき baseUrl とモデルを初期値で埋め直す（キーは触らない）。
        public static void ApplyProviderDefaults(string provider)
        {
            Provider = provider;
            BaseUrl = DefaultBaseUrl(provider);
            Model = DefaultModel(provider);
        }

        static string Get(string k, string d) => PlayerPrefs.GetString(k, d);
        static void Set(string k, string v) { PlayerPrefs.SetString(k, v ?? ""); PlayerPrefs.Save(); }
    }
}

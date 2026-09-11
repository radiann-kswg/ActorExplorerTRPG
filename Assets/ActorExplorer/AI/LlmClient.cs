using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ActorExplorer
{
    /// 会話履歴の 1 件。セーブにそのまま入る（system は保存しない）。
    [Serializable]
    public class ChatMessage
    {
        public string role;            // user / assistant / tool
        public string content = "";
        public string toolCallsJson = ""; // assistant のツール呼び出し（OpenAI 形式の配列 JSON）
        public string toolCallId = "";    // tool の応答先
    }

    // --- レスポンス（JsonUtility で読む） ---
    [Serializable] public class LlmFn { public string name; public string arguments; }
    [Serializable] public class LlmToolCall { public string id; public string type; public LlmFn function; }
    [Serializable] public class LlmMsg { public string role; public string content; public LlmToolCall[] tool_calls; }
    [Serializable] public class LlmChoice { public LlmMsg message; public string finish_reason; }
    [Serializable] public class LlmError { public string message; public string type; }
    [Serializable] public class LlmResponse { public LlmChoice[] choices; public LlmError error; }

    /// OpenAI 互換 chat/completions を叩く 1 クラス。非ストリーミング。
    /// リクエスト JSON は手組み（空配列や null を送らないため）、レスポンスは JsonUtility。
    public static class LlmClient
    {
        public static async Awaitable<LlmMsg> Send(string system, IList<ChatMessage> messages, string toolsJson)
        {
            string url = Settings.BaseUrl.TrimEnd('/') + "/chat/completions";
            string body = BuildBody(Settings.Model, system, messages, toolsJson);
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + Settings.ApiKey);
            req.timeout = 120;
            await req.SendWebRequest();

            string text = req.downloadHandler.text ?? "";
            LlmResponse res = null;
            try { res = JsonUtility.FromJson<LlmResponse>(text); } catch { }
            if (res?.error != null && !string.IsNullOrEmpty(res.error.message))
                throw new Exception($"API error: {res.error.message}");
            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"HTTP {req.responseCode}: {req.error} {Truncate(text, 300)}");
            if (res?.choices == null || res.choices.Length == 0 || res.choices[0].message == null)
                throw new Exception("API returned no choices: " + Truncate(text, 300));
            var msg = res.choices[0].message;
            msg.content ??= "";
            return msg;
        }

        public static string BuildBody(string model, string system, IList<ChatMessage> messages, string toolsJson)
        {
            var sb = new StringBuilder();
            sb.Append("{\"model\":").Append(Q(model)).Append(",\"messages\":[");
            sb.Append("{\"role\":\"system\",\"content\":").Append(Q(system)).Append('}');
            foreach (var m in messages)
            {
                sb.Append(",{\"role\":").Append(Q(m.role)).Append(",\"content\":").Append(Q(m.content ?? ""));
                if (!string.IsNullOrEmpty(m.toolCallsJson)) sb.Append(",\"tool_calls\":").Append(m.toolCallsJson);
                if (!string.IsNullOrEmpty(m.toolCallId)) sb.Append(",\"tool_call_id\":").Append(Q(m.toolCallId));
                sb.Append('}');
            }
            sb.Append(']');
            if (!string.IsNullOrEmpty(toolsJson)) sb.Append(",\"tools\":").Append(toolsJson);
            sb.Append('}');
            return sb.ToString();
        }

        /// 応答の tool_calls を、次のリクエストで assistant メッセージに載せる JSON 配列へ戻す。
        public static string ToolCallsJson(LlmToolCall[] calls)
        {
            if (calls == null || calls.Length == 0) return "";
            var sb = new StringBuilder("[");
            for (int i = 0; i < calls.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"id\":").Append(Q(calls[i].id)).Append(",\"type\":\"function\",\"function\":{\"name\":")
                  .Append(Q(calls[i].function.name)).Append(",\"arguments\":").Append(Q(calls[i].function.arguments ?? "{}")).Append("}}");
            }
            return sb.Append(']').ToString();
        }

        /// JSON 文字列リテラル化。
        public static string Q(string s)
        {
            if (s == null) return "\"\"";
            var sb = new StringBuilder(s.Length + 2).Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        static string Truncate(string s, int n) => s.Length <= n ? s : s.Substring(0, n) + "…";
    }
}

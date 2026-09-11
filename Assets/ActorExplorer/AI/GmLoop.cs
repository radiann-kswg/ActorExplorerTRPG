using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ActorExplorer
{
    /// セッション状態＝セーブファイル。system は保存せず毎回組み立てる。
    [Serializable]
    public class SaveData
    {
        public string rulesetId;
        public string scenarioId;
        public string language = "ja";
        public List<Actor> actors = new List<Actor>();
        public List<ChatMessage> messages = new List<ChatMessage>();
        public bool ended;

        public static string Path => System.IO.Path.Combine(Application.persistentDataPath, "actorexplorer", "save.json");
        public static bool Exists => System.IO.File.Exists(Path);
        public void Save()
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            System.IO.File.WriteAllText(Path, JsonUtility.ToJson(this, true));
        }
        public static SaveData Load() => JsonUtility.FromJson<SaveData>(System.IO.File.ReadAllText(Path));
    }

    [Serializable] class CheckArgs { public string actor; public string skill; public string difficulty; }
    [Serializable] class ResourceArgs { public string actor; public string resource; public int delta; public string reason; }
    [Serializable] class EndArgs { public string outcome; }

    /// AI GM のループ。数値・ダイス・判定はここ（エンジン）が正で、AI はツールを呼ぶだけ。
    public class GmLoop
    {
        public readonly Ruleset Ruleset;
        public readonly Scenario Scenario;
        public readonly SaveData State;
        /// 判定結果・リソース増減などエンジン側の出来事（UI のログ用）。
        public event Action<string> OnEvent;

        const int MaxToolRounds = 5;

        public GmLoop(Ruleset rs, Scenario sc, SaveData state) { Ruleset = rs; Scenario = sc; State = state; }

        public static GmLoop New(string rulesetId, string scenarioId, IEnumerable<Actor> actors, string language)
        {
            var state = new SaveData { rulesetId = rulesetId, scenarioId = scenarioId, language = language, actors = actors.ToList() };
            return new GmLoop(Ruleset.Load(rulesetId), Scenario.Load(scenarioId), state);
        }

        public static GmLoop Resume(SaveData state) => new GmLoop(Ruleset.Load(state.rulesetId), Scenario.Load(state.scenarioId), state);

        /// プレイヤー入力（空なら導入描写の要求）を送り、GM の本文を返す。
        public async Awaitable<string> Step(string userText)
        {
            State.messages.Add(new ChatMessage { role = "user", content = userText });
            for (int round = 0; ; round++)
            {
                var msg = await LlmClient.Send(BuildSystemPrompt(), Window(), ToolsJson());
                bool hasTools = msg.tool_calls != null && msg.tool_calls.Length > 0 && round < MaxToolRounds;
                State.messages.Add(new ChatMessage { role = "assistant", content = msg.content, toolCallsJson = hasTools ? LlmClient.ToolCallsJson(msg.tool_calls) : "" });
                if (!hasTools) return msg.content;
                foreach (var call in msg.tool_calls)
                    State.messages.Add(new ChatMessage { role = "tool", toolCallId = call.id, content = RunTool(call.function.name, call.function.arguments) });
            }
        }

        /// 直近 HistoryLimit 件。先頭が孤立した tool 応答なら落とす（assistant の tool_calls と対で送る必要があるため）。
        // ponytail: 件数で切るだけ。要約圧縮は入れない。
        List<ChatMessage> Window()
        {
            int n = Math.Max(2, Settings.HistoryLimit);
            var w = State.messages.Skip(Math.Max(0, State.messages.Count - n)).ToList();
            while (w.Count > 0 && w[0].role == "tool") w.RemoveAt(0);
            return w;
        }

        public string RunTool(string name, string argsJson)
        {
            try
            {
                switch (name)
                {
                    case "request_check": return RequestCheck(JsonUtility.FromJson<CheckArgs>(argsJson));
                    case "modify_resource": return ModifyResource(JsonUtility.FromJson<ResourceArgs>(argsJson));
                    case "end_session":
                        State.ended = true;
                        OnEvent?.Invoke($"[end] {JsonUtility.FromJson<EndArgs>(argsJson)?.outcome}");
                        return "{\"ok\":true}";
                    default: return Err("unknown tool " + name);
                }
            }
            catch (Exception e) { return Err(e.Message); }
        }

        string RequestCheck(CheckArgs a)
        {
            var actor = FindActor(a.actor); if (actor == null) return Err("unknown actor: " + a.actor + ". actors: " + string.Join(", ", State.actors.Select(x => x.name)));
            if (!actor.HasSkill(a.skill)) return Err("unknown skill: " + a.skill + ". skills: " + string.Join(", ", Ruleset.skills.Select(s => s.id)));
            var d = ParseDifficulty(a.difficulty);
            var r = Check.Roll(Ruleset.check, actor.Skill(a.skill), d);
            OnEvent?.Invoke($"[check] {actor.name} {a.skill}({d}) → {r.outcome} ({r.roll}/{r.target})");
            return $"{{\"actor\":{LlmClient.Q(actor.name)},\"skill\":{LlmClient.Q(a.skill)},\"difficulty\":\"{d}\",\"roll\":{r.roll},\"target\":{r.target},\"outcome\":\"{r.outcome}\"}}";
        }

        string ModifyResource(ResourceArgs a)
        {
            var actor = FindActor(a.actor); if (actor == null) return Err("unknown actor: " + a.actor);
            if (Ruleset.resources.All(r => r.id != a.resource)) return Err("unknown resource: " + a.resource + ". resources: " + string.Join(", ", Ruleset.resources.Select(r => r.id)));
            var res = actor.Resource(a.resource);
            int before = res.value;
            int v = actor.Modify(a.resource, a.delta);
            OnEvent?.Invoke($"[resource] {actor.name} {a.resource} {before} → {v}/{res.max} ({a.reason})");
            return $"{{\"actor\":{LlmClient.Q(actor.name)},\"resource\":{LlmClient.Q(a.resource)},\"value\":{v},\"max\":{res.max}}}";
        }

        public Actor FindActor(string name) => State.actors.FirstOrDefault(x => string.Equals(x.name, name, StringComparison.OrdinalIgnoreCase));

        public static Difficulty ParseDifficulty(string s) => (s ?? "").ToLowerInvariant() switch
        {
            "hard" => Difficulty.Hard,
            "extreme" => Difficulty.Extreme,
            _ => Difficulty.Normal,
        };

        static string Err(string m) => "{\"error\":" + LlmClient.Q(m) + "}";

        // --- プロンプト ---

        public string BuildSystemPrompt()
        {
            string lang = State.language == "en" ? "English" : "Japanese";
            var sb = new StringBuilder();
            sb.AppendLine("You are the game master (GM) of a tabletop RPG played by one human who controls the player characters (PCs) listed below.");
            sb.AppendLine($"Narrate in {lang}. Stay in the world; never mention being an AI. Keep each reply concise (a few paragraphs), end by prompting the player for their next action.");
            sb.AppendLine("RULES: The game engine owns all numbers and dice. Never invent roll results, damage or resource values.");
            sb.AppendLine("- When an action's outcome is uncertain, call request_check(actor, skill, difficulty) and narrate based on the returned outcome (Critical/Success/Failure/Fumble). Use difficulty 'normal' by default, 'hard' or 'extreme' only when justified.");
            sb.AppendLine("- When a resource changes (injury, healing, shock, spending), call modify_resource(actor, resource, delta, reason). Damage is a negative delta.");
            sb.AppendLine("- When the scenario reaches an ending (success, failure, escape, death of all PCs), call end_session(outcome) after the final narration.");
            sb.AppendLine("- Player messages are prefixed with the acting PC's name in brackets, e.g. [Name] text. Lines starting with [check] are results of rolls the player made themselves; treat them as fact.");
            sb.AppendLine();
            sb.AppendLine("RULESET " + Ruleset.id + ": d100 roll-under. stats: " + string.Join(", ", Ruleset.stats.Select(s => s.id))
                + ". resources: " + string.Join(", ", Ruleset.resources.Select(r => r.id))
                + ". skills: " + string.Join(", ", Ruleset.skills.Select(s => s.id)) + ".");
            sb.AppendLine();
            sb.AppendLine("PLAYER CHARACTERS:");
            foreach (var a in State.actors)
            {
                sb.AppendLine("- " + a.name
                    + " | " + string.Join(" ", a.stats.Select(e => $"{e.key}:{e.value}"))
                    + " | " + string.Join(" ", a.resources.Select(r => $"{r.key}:{r.value}/{r.max}"))
                    + " | " + string.Join(" ", a.skills.Select(e => $"{e.key}:{e.value}")));
            }
            sb.AppendLine();
            sb.AppendLine("SCENARIO: " + Scenario.title.Get(State.language));
            sb.AppendLine("Opening (use this for the first narration): " + Scenario.openingText.Get(State.language));
            sb.AppendLine("GM NOTES (secret, never reveal directly):");
            sb.AppendLine(Scenario.gmNotes);
            return sb.ToString();
        }

        public string ToolsJson()
        {
            string skills = string.Join(",", Ruleset.skills.Select(s => LlmClient.Q(s.id)));
            string resources = string.Join(",", Ruleset.resources.Select(r => LlmClient.Q(r.id)));
            string actors = string.Join(",", State.actors.Select(a => LlmClient.Q(a.name)));
            return "[" +
                "{\"type\":\"function\",\"function\":{\"name\":\"request_check\",\"description\":\"Ask the engine to roll a skill check for a PC. Returns roll, target and outcome.\"," +
                "\"parameters\":{\"type\":\"object\",\"properties\":{" +
                "\"actor\":{\"type\":\"string\",\"enum\":[" + actors + "]}," +
                "\"skill\":{\"type\":\"string\",\"enum\":[" + skills + "]}," +
                "\"difficulty\":{\"type\":\"string\",\"enum\":[\"normal\",\"hard\",\"extreme\"]}}," +
                "\"required\":[\"actor\",\"skill\",\"difficulty\"]}}}," +
                "{\"type\":\"function\",\"function\":{\"name\":\"modify_resource\",\"description\":\"Change a PC's resource by delta (negative = damage/loss). The engine clamps to 0..max and returns the new value.\"," +
                "\"parameters\":{\"type\":\"object\",\"properties\":{" +
                "\"actor\":{\"type\":\"string\",\"enum\":[" + actors + "]}," +
                "\"resource\":{\"type\":\"string\",\"enum\":[" + resources + "]}," +
                "\"delta\":{\"type\":\"integer\"}," +
                "\"reason\":{\"type\":\"string\"}}," +
                "\"required\":[\"actor\",\"resource\",\"delta\",\"reason\"]}}}," +
                "{\"type\":\"function\",\"function\":{\"name\":\"end_session\",\"description\":\"End the scenario. Call once after the final narration.\"," +
                "\"parameters\":{\"type\":\"object\",\"properties\":{\"outcome\":{\"type\":\"string\"}},\"required\":[\"outcome\"]}}}" +
                "]";
        }
    }
}

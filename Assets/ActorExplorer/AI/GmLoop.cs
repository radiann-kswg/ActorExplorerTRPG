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

    /// エンジン側の出来事（判定・リソース増減・終了）。ID/英語のまま持ち、表示側（UI）で和文化する。
    [Serializable]
    public struct GmEvent
    {
        public string kind;        // "check" | "resource" | "end"
        public string actor;
        public string id;          // 技能 ID / リソース ID / 終了 outcome
        public Difficulty difficulty;
        public CheckResult result;
        public int before, after, max;
        public string reason;
        public bool manual;        // プレイヤーが自分で振った判定
        public bool isStat;        // id が技能ではなく能力値

        public static GmEvent Check(string actor, string skill, Difficulty d, CheckResult r, bool manual = false, bool isStat = false)
            => new GmEvent { kind = "check", actor = actor, id = skill, difficulty = d, result = r, manual = manual, isStat = isStat };
        public static GmEvent Resource(string actor, string res, int before, int after, int max, string reason)
            => new GmEvent { kind = "resource", actor = actor, id = res, before = before, after = after, max = max, reason = reason };
        public static GmEvent End(string outcome) => new GmEvent { kind = "end", id = outcome };

        /// AI やログ向けの英語 1 行（数値はここが正）。
        public override string ToString() => kind switch
        {
            "check" => $"[check] {actor} {id}({difficulty}) → {result.outcome} ({result.roll}/{result.target})",
            "resource" => $"[resource] {actor} {id} {before} → {after}/{max} ({reason})",
            _ => $"[end] {id}",
        };
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
        public event Action<GmEvent> OnEvent;

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
                        OnEvent?.Invoke(GmEvent.End(JsonUtility.FromJson<EndArgs>(argsJson)?.outcome));
                        return "{\"ok\":true}";
                    default: return Err("unknown tool " + name);
                }
            }
            catch (Exception e) { return Err(e.Message); }
        }

        string RequestCheck(CheckArgs a)
        {
            var actor = FindActor(a.actor); if (actor == null) return Err("unknown actor: " + a.actor + ". actors: " + string.Join(", ", State.actors.Select(x => x.name)));
            // 技能 → 能力値の順に探す。能力値判定（STR で力比べ、POW で精神力 …）は値をそのまま目標値にする。
            bool isStat = !actor.HasSkill(a.skill) && Ruleset.stats.Any(st => st.id == a.skill);
            if (!actor.HasSkill(a.skill) && !isStat)
                return Err("unknown skill: " + a.skill + ". skills: " + string.Join(", ", Ruleset.skills.Select(s => s.id)) + ". stats: " + string.Join(", ", Ruleset.stats.Select(s => s.id)));
            var d = ParseDifficulty(a.difficulty);
            int value = isStat ? actor.Stat(a.skill) : actor.Skill(a.skill);
            var r = Check.Roll(Ruleset.check, value, d);
            OnEvent?.Invoke(GmEvent.Check(actor.name, a.skill, d, r, isStat: isStat));
            string name = isStat ? Strings.Stat(a.skill, State.language) : Strings.Skill(a.skill, State.language);
            return $"{{\"actor\":{LlmClient.Q(actor.name)},\"skill\":{LlmClient.Q(a.skill)},\"skillName\":{LlmClient.Q(name)},\"difficulty\":\"{d}\",\"roll\":{r.roll},\"target\":{r.target},\"outcome\":\"{r.outcome}\"}}";
        }

        string ModifyResource(ResourceArgs a)
        {
            var actor = FindActor(a.actor); if (actor == null) return Err("unknown actor: " + a.actor);
            if (Ruleset.resources.All(r => r.id != a.resource)) return Err("unknown resource: " + a.resource + ". resources: " + string.Join(", ", Ruleset.resources.Select(r => r.id)));
            var res = actor.Resource(a.resource);
            int before = res.value;
            int v = actor.Modify(a.resource, a.delta);
            OnEvent?.Invoke(GmEvent.Resource(actor.name, a.resource, before, v, res.max, a.reason));
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
            string L = State.language;
            var sb = new StringBuilder();
            sb.AppendLine("You are the game master (GM) of a tabletop RPG played by one human who controls the player characters (PCs) listed below.");
            sb.AppendLine($"Narrate in {lang}. Stay in the world; never mention being an AI. Keep each reply concise (a few paragraphs), end by prompting the player for their next action.");
            sb.AppendLine("RULES: The game engine owns all numbers and dice. Never invent roll results, damage or resource values.");
            sb.AppendLine("- When an action's outcome is uncertain AND failure would change the story, call request_check(actor, skill, difficulty) and narrate based on the returned outcome (Critical/Success/Failure/Fumble).");
            sb.AppendLine("- Do NOT roll for routine, safe or obvious actions (looking around a room, talking normally, walking, picking up an object in plain sight): simply narrate them as succeeding.");
            sb.AppendLine("- Use difficulty 'normal' by default. Use 'hard' or 'extreme' only when the fiction clearly justifies it (darkness, time pressure, active resistance).");
            sb.AppendLine("- Before requesting a check, pick the skill whose description in the SKILLS table best matches the player's action. Use the 'skill' ID from the first column, never the display name. If no skill fits but a raw attribute does (a contest of strength, willpower, a flash of insight, a first impression, a reflex), pass a STAT id from the STATS table as 'skill' instead. If nothing fits, do not roll; narrate instead.");
            sb.AppendLine("- Facts the PC directly touches, reads or sees in plain sight are perceived without a roll (a warm can feels warm). Roll only for hidden details, interpretation, or physical feats.");
            sb.AppendLine("- A Failure means the PC did not notice the hidden detail / could not do the feat this time. It NEVER changes the facts in the GM NOTES: never describe an object or situation as different from the notes to explain a failure (if the notes say the coffee is warm, it stays warm; the PC simply fails to realise what that means). Leave a way to find the clue again later by another method, another place or another PC. A Fumble may add a small complication, but still does not alter the notes' facts.");
            sb.AppendLine("- When a resource changes (injury, healing, shock, spending), call modify_resource(actor, resource, delta, reason). Damage is a negative delta.");
            sb.AppendLine("- When the scenario reaches an ending (success, failure, escape, death of all PCs), call end_session(outcome) after the final narration.");
            sb.AppendLine("- Player messages are prefixed with the acting PC's name in brackets, e.g. [Name] text. Lines starting with [check] are results of rolls the player made themselves; treat them as fact.");
            sb.AppendLine();
            sb.AppendLine("RULESET " + Ruleset.id + ": d100 roll-under (roll <= skill value succeeds; hard = half, extreme = one fifth).");
            sb.AppendLine("STATS (id — name — when to roll it directly):");
            foreach (var st in Ruleset.stats) sb.AppendLine("  " + StatRow(st, L));
            sb.AppendLine("RESOURCES (id — name): " + string.Join(", ", Ruleset.resources.Select(r => $"{r.id} — {Strings.Res(r.id, L)}")));
            sb.AppendLine("SKILLS (id — name — what it covers):");
            foreach (var s in Ruleset.skills) sb.AppendLine("  " + SkillRow(s, L));
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

        string StatRow(StatDef s, string lang)
        {
            string d = s.desc?.Get(lang);
            return string.IsNullOrEmpty(d) ? $"{s.id} — {Strings.Stat(s.id, lang)}" : $"{s.id} — {Strings.Stat(s.id, lang)} — {d}";
        }

        /// "organize — 整理整頓 — 片付け・収納…"。desc が無ければ 2 列。
        string SkillRow(SkillDef s, string lang)
        {
            string d = s.desc?.Get(lang);
            return string.IsNullOrEmpty(d) ? $"{s.id} — {Strings.Skill(s.id, lang)}" : $"{s.id} — {Strings.Skill(s.id, lang)} — {d}";
        }

        public string ToolsJson()
        {
            string skills = string.Join(",", Ruleset.skills.Select(s => LlmClient.Q(s.id)).Concat(Ruleset.stats.Select(s => LlmClient.Q(s.id))));
            string skillTable = LlmClient.Q("Skill ID (or a stat ID for a raw attribute roll). Pick by the action's meaning: "
                + string.Join("; ", Ruleset.skills.Select(s => SkillRow(s, State.language))) + ". STATS: " + string.Join("; ", Ruleset.stats.Select(s => StatRow(s, State.language))));
            string resources = string.Join(",", Ruleset.resources.Select(r => LlmClient.Q(r.id)));
            string actors = string.Join(",", State.actors.Select(a => LlmClient.Q(a.name)));
            return "[" +
                "{\"type\":\"function\",\"function\":{\"name\":\"request_check\",\"description\":\"Ask the engine to roll a skill check for a PC. Returns roll, target and outcome.\"," +
                "\"parameters\":{\"type\":\"object\",\"properties\":{" +
                "\"actor\":{\"type\":\"string\",\"enum\":[" + actors + "]}," +
                "\"skill\":{\"type\":\"string\",\"description\":" + skillTable + ",\"enum\":[" + skills + "]}," +
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

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ActorExplorer.Tests
{
    public class LlmClientTests
    {
        [Test] public void QuotesJsonStrings()
        {
            Assert.AreEqual("\"a\\\"b\\\\c\\n\\t\\u0001\"", LlmClient.Q("a\"b\\c\n\t"));
            Assert.AreEqual("\"\"", LlmClient.Q(null));
            Assert.AreEqual("\"日本語\"", LlmClient.Q("日本語"));
        }

        [Test] public void BodyOmitsEmptyToolFields()
        {
            var msgs = new List<ChatMessage>
            {
                new ChatMessage { role = "user", content = "hi" },
                new ChatMessage { role = "assistant", content = "", toolCallsJson = "[{\"id\":\"c1\",\"type\":\"function\",\"function\":{\"name\":\"end_session\",\"arguments\":\"{}\"}}]" },
                new ChatMessage { role = "tool", toolCallId = "c1", content = "{\"ok\":true}" },
            };
            string body = LlmClient.BuildBody("m", "sys", msgs, "[]");
            Assert.AreEqual("{\"model\":\"m\",\"messages\":[{\"role\":\"system\",\"content\":\"sys\"},{\"role\":\"user\",\"content\":\"hi\"},"
                + "{\"role\":\"assistant\",\"content\":\"\",\"tool_calls\":[{\"id\":\"c1\",\"type\":\"function\",\"function\":{\"name\":\"end_session\",\"arguments\":\"{}\"}}]},"
                + "{\"role\":\"tool\",\"content\":\"{\\\"ok\\\":true}\",\"tool_call_id\":\"c1\"}],\"tools\":[]}", body);
            Assert.IsFalse(LlmClient.BuildBody("m", "s", new List<ChatMessage>(), "").Contains("tools"));
        }

        [Test] public void ParsesResponseAndRebuildsToolCalls()
        {
            string json = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":null,\"tool_calls\":[{\"id\":\"call_1\",\"type\":\"function\",\"function\":{\"name\":\"request_check\",\"arguments\":\"{\\\"actor\\\":\\\"A\\\",\\\"skill\\\":\\\"observe\\\",\\\"difficulty\\\":\\\"normal\\\"}\"}}]},\"finish_reason\":\"tool_calls\"}]}";
            var res = JsonUtility.FromJson<LlmResponse>(json);
            var msg = res.choices[0].message;
            Assert.AreEqual(1, msg.tool_calls.Length);
            Assert.AreEqual("request_check", msg.tool_calls[0].function.name);
            var args = JsonUtility.FromJson<CheckArgsPublic>(msg.tool_calls[0].function.arguments);
            Assert.AreEqual("observe", args.skill);
            string rebuilt = LlmClient.ToolCallsJson(msg.tool_calls);
            var again = JsonUtility.FromJson<LlmMsg>("{\"tool_calls\":" + rebuilt + "}");
            Assert.AreEqual(msg.tool_calls[0].function.arguments, again.tool_calls[0].function.arguments);
        }

        [System.Serializable] class CheckArgsPublic { public string actor; public string skill; public string difficulty; }
    }

    public class GmLoopTests
    {
        static GmLoop Make()
        {
            Expr.Rng = new System.Random(3);
            var rs = Ruleset.Load("sample-d100");
            var state = new SaveData { rulesetId = rs.id, scenarioId = "sample-01", actors = new List<Actor> { Actor.Create(rs, "Alex") } };
            return new GmLoop(rs, Scenario.Load("sample-01"), state);
        }

        [Test] public void ToolsRunAgainstEngine()
        {
            var gm = Make();
            var events = new List<string>();
            gm.OnEvent += events.Add;

            string r = gm.RunTool("request_check", "{\"actor\":\"alex\",\"skill\":\"observe\",\"difficulty\":\"hard\"}");
            StringAssert.Contains("\"outcome\":\"", r);
            StringAssert.Contains("\"target\":" + (gm.State.actors[0].Skill("observe") / 2), r);

            int max = gm.State.actors[0].Resource("HP").max;
            r = gm.RunTool("modify_resource", "{\"actor\":\"Alex\",\"resource\":\"HP\",\"delta\":-999,\"reason\":\"test\"}");
            StringAssert.Contains("\"value\":0", r);
            Assert.AreEqual(0, gm.State.actors[0].Resource("HP").value);

            StringAssert.Contains("error", gm.RunTool("request_check", "{\"actor\":\"Nobody\",\"skill\":\"observe\",\"difficulty\":\"normal\"}"));
            StringAssert.Contains("error", gm.RunTool("request_check", "{\"actor\":\"Alex\",\"skill\":\"nope\",\"difficulty\":\"normal\"}"));
            StringAssert.Contains("error", gm.RunTool("teleport", "{}"));

            Assert.IsFalse(gm.State.ended);
            gm.RunTool("end_session", "{\"outcome\":\"rescued\"}");
            Assert.IsTrue(gm.State.ended);
            Assert.AreEqual(3, events.Count);
        }

        [Test] public void PromptAndToolsMentionRulesetContent()
        {
            var gm = Make();
            string p = gm.BuildSystemPrompt();
            StringAssert.Contains("Alex", p);
            StringAssert.Contains("GM NOTES", p);
            StringAssert.Contains("Japanese", p);
            string t = gm.ToolsJson();
            StringAssert.Contains("\"dodge\"", t);
            StringAssert.Contains("\"HP\"", t);
            StringAssert.Contains("\"Alex\"", t);
        }

        [Test] public void SaveDataRoundTrips()
        {
            var gm = Make();
            gm.State.messages.Add(new ChatMessage { role = "user", content = "hi" });
            gm.State.messages.Add(new ChatMessage { role = "assistant", content = "", toolCallsJson = "[{\"id\":\"c\",\"type\":\"function\",\"function\":{\"name\":\"end_session\",\"arguments\":\"{}\"}}]" });
            var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(gm.State));
            Assert.AreEqual(2, back.messages.Count);
            Assert.AreEqual(gm.State.messages[1].toolCallsJson, back.messages[1].toolCallsJson);
            Assert.AreEqual("Alex", GmLoop.Resume(back).State.actors[0].name);
        }
    }
}

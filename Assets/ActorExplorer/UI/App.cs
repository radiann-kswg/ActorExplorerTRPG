using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ActorExplorer
{
    /// 画面 4 つ（タイトル / 設定 / キャラ作成 / プレイ）を 1 つの UXML で切り替える。
    [RequireComponent(typeof(UIDocument))]
    public class App : MonoBehaviour
    {
        public string rulesetId = "sample-d100";

        VisualElement root;
        Ruleset rs;
        GmLoop gm;
        readonly List<Actor> draft = new List<Actor>();
        int sel;
        string pendingChecks = "";
        bool busy;

        // 文字送り
        readonly Queue<(Label label, string text)> typeQueue = new Queue<(Label, string)>();
        Label typing; string typingText; float typed;

        static readonly string[] Screens = { "title", "settings", "chargen", "play" };

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            Strings.Load();
            rs = Ruleset.Load(rulesetId);
            BindTitle(); BindSettings(); BindChargen(); BindPlay();
            Show("title");
        }

        void Show(string name)
        {
            ApplyTexts();
            foreach (var s in Screens) root.Q(s).style.display = s == name ? DisplayStyle.Flex : DisplayStyle.None;
            if (name == "title") root.Q<Button>("title-continue").SetEnabled(SaveData.Exists);
        }

        void ApplyTexts()
        {
            void T(string el, string key) { var e = root.Q(el); if (e is Button b) b.text = Strings.T(key); else if (e is Label l) l.text = Strings.T(key); else if (e is BaseField<string> f) f.label = Strings.T(key); else if (e is IntegerField i) i.label = Strings.T(key); else if (e is FloatField fl) fl.label = Strings.T(key); }
            T("title-label", "app.title"); T("title-new", "title.new"); T("title-continue", "title.continue"); T("title-settings", "title.settings"); T("title-quit", "title.quit");
            T("settings-title", "settings.title"); T("settings-provider", "settings.provider"); T("settings-baseUrl", "settings.baseUrl"); T("settings-model", "settings.model");
            T("settings-apiKey", "settings.apiKey"); T("settings-language", "settings.language"); T("settings-historyLimit", "settings.historyLimit"); T("settings-typeSpeed", "settings.typeSpeed"); T("settings-back", "settings.back");
            T("chargen-title", "chargen.title"); T("chargen-scenario", "chargen.scenario"); T("chargen-name", "chargen.name"); T("chargen-random", "chargen.random"); T("chargen-add", "chargen.add"); T("chargen-remove", "chargen.remove");
            T("chargen-stats-label", "chargen.stats"); T("chargen-skills-label", "chargen.skills"); T("chargen-resources-label", "chargen.resources"); T("chargen-start", "chargen.start"); T("chargen-back", "chargen.back");
            T("play-send", "play.send"); T("play-save", "play.save"); T("play-title", "play.title"); T("play-diff-title", "play.difficulty"); T("play-diff-cancel", "settings.back");
            foreach (var d in new[] { "Normal", "Hard", "Extreme" }) T("play-diff-" + d, "diff." + d);
        }

        // ---------- タイトル ----------
        void BindTitle()
        {
            root.Q<Button>("title-new").clicked += () => { draft.Clear(); AddActor(); Show("chargen"); RefreshChargen(); };
            root.Q<Button>("title-continue").clicked += () => { try { StartPlay(GmLoop.Resume(SaveData.Load()), resume: true); } catch (Exception e) { Debug.LogError(e); } };
            root.Q<Button>("title-settings").clicked += () => { Show("settings"); RefreshSettings(); };
            root.Q<Button>("title-quit").clicked += () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            };
        }

        // ---------- 設定 ----------
        void BindSettings()
        {
            var provider = root.Q<DropdownField>("settings-provider");
            provider.choices = Settings.Providers.ToList();
            provider.RegisterValueChangedCallback(e => { Settings.ApplyProviderDefaults(e.newValue); RefreshSettings(); });
            root.Q<TextField>("settings-baseUrl").RegisterValueChangedCallback(e => Settings.BaseUrl = e.newValue);
            root.Q<TextField>("settings-model").RegisterValueChangedCallback(e => Settings.Model = e.newValue);
            root.Q<TextField>("settings-apiKey").RegisterValueChangedCallback(e => Settings.ApiKey = e.newValue);
            var lang = root.Q<DropdownField>("settings-language");
            lang.choices = new List<string> { "ja", "en" };
            lang.RegisterValueChangedCallback(e => { Settings.Language = e.newValue; ApplyTexts(); });
            root.Q<IntegerField>("settings-historyLimit").RegisterValueChangedCallback(e => Settings.HistoryLimit = Mathf.Max(2, e.newValue));
            root.Q<FloatField>("settings-typeSpeed").RegisterValueChangedCallback(e => Settings.TypeSpeed = Mathf.Max(1, e.newValue));
            root.Q<Button>("settings-back").clicked += () => Show("title");
        }

        void RefreshSettings()
        {
            root.Q<DropdownField>("settings-provider").SetValueWithoutNotify(Settings.Provider);
            root.Q<TextField>("settings-baseUrl").SetValueWithoutNotify(Settings.BaseUrl);
            root.Q<TextField>("settings-model").SetValueWithoutNotify(Settings.Model);
            root.Q<TextField>("settings-apiKey").SetValueWithoutNotify(Settings.ApiKey);
            root.Q<DropdownField>("settings-language").SetValueWithoutNotify(Settings.Language);
            root.Q<IntegerField>("settings-historyLimit").SetValueWithoutNotify(Settings.HistoryLimit);
            root.Q<FloatField>("settings-typeSpeed").SetValueWithoutNotify(Settings.TypeSpeed);
        }

        // ---------- キャラ作成 ----------
        void BindChargen()
        {
            var scen = root.Q<DropdownField>("chargen-scenario");
            scen.choices = Scenario.Ids().ToList();
            if (scen.choices.Count > 0) scen.value = scen.choices[0];
            root.Q<Button>("chargen-add").clicked += () => { AddActor(); RefreshChargen(); };
            root.Q<Button>("chargen-remove").clicked += () => { if (draft.Count > 1) { draft.RemoveAt(sel); sel = Mathf.Clamp(sel, 0, draft.Count - 1); RefreshChargen(); } };
            root.Q<Button>("chargen-random").clicked += () => { string n = draft[sel].name; draft[sel] = Actor.Create(rs, n); RefreshChargen(); };
            root.Q<TextField>("chargen-name").RegisterValueChangedCallback(e => { draft[sel].name = e.newValue; RefreshActorList(); });
            root.Q<Button>("chargen-back").clicked += () => Show("title");
            root.Q<Button>("chargen-start").clicked += () =>
            {
                if (draft.Any(a => string.IsNullOrWhiteSpace(a.name)) || string.IsNullOrEmpty(scen.value)) return;
                StartPlay(GmLoop.New(rulesetId, scen.value, draft, Settings.Language), resume: false);
            };
        }

        void AddActor() { draft.Add(Actor.Create(rs, Strings.T("chargen.defaultName") + " " + (draft.Count + 1))); sel = draft.Count - 1; }

        void RefreshChargen() { RefreshActorList(); RefreshSheet(); }

        void RefreshActorList()
        {
            var list = root.Q<ScrollView>("chargen-actors");
            list.Clear();
            for (int i = 0; i < draft.Count; i++)
            {
                int idx = i;
                var b = new Button(() => { sel = idx; RefreshChargen(); }) { text = draft[i].name };
                b.AddToClassList("actor-item");
                if (i == sel) b.AddToClassList("actor-item-selected");
                list.Add(b);
            }
        }

        void RefreshSheet()
        {
            var a = draft[sel];
            root.Q<TextField>("chargen-name").SetValueWithoutNotify(a.name);
            var stats = root.Q("chargen-stats"); stats.Clear();
            foreach (var s in rs.stats)
            {
                var f = new IntegerField(Strings.Stat(s.id)) { value = a.Stat(s.id) };
                string id = s.id;
                f.RegisterValueChangedCallback(e => { a.SetStat(id, Mathf.Max(0, e.newValue)); a.Rederive(rs); RefreshResources(a); RefreshBudget(a); });
                stats.Add(f);
            }
            RefreshResources(a);
            var skills = root.Q("chargen-skills"); skills.Clear();
            foreach (var s in rs.skills)
            {
                var f = new IntegerField(Strings.Skill(s.id)) { value = a.Skill(s.id) };
                string id = s.id;
                f.RegisterValueChangedCallback(e => { a.SetSkill(id, Mathf.Max(0, e.newValue)); RefreshBudget(a); });
                skills.Add(f);
            }
            RefreshBudget(a);
        }

        void RefreshResources(Actor a)
        {
            var res = root.Q("chargen-resources"); res.Clear();
            foreach (var r in a.resources) res.Add(new Label($"{Strings.Res(r.key)}: {r.value}/{r.max}") { });
        }

        void RefreshBudget(Actor a) => root.Q<Label>("chargen-budget").text = $"{Strings.T("chargen.budget")}: {a.SkillSpent(rs)} / {a.SkillBudget(rs)}";

        // ---------- プレイ ----------
        void BindPlay()
        {
            root.Q<Button>("play-send").clicked += () => _ = Send();
            root.Q<TextField>("play-input").RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return && (e.ctrlKey || e.commandKey)) { _ = Send(); e.StopPropagation(); } });
            root.Q<DropdownField>("play-actor").RegisterValueChangedCallback(_ => RefreshPlaySheet());
            root.Q<Button>("play-save").clicked += () => { gm.State.Save(); Status(Strings.T("play.saved")); };
            root.Q<Button>("play-title").clicked += () => Show("title");
            root.Q<ScrollView>("play-log").RegisterCallback<ClickEvent>(_ => SkipTyping());
            root.Q<Button>("play-diff-cancel").clicked += () => root.Q("play-diff").style.display = DisplayStyle.None;
        }

        void StartPlay(GmLoop loop, bool resume)
        {
            gm = loop;
            gm.OnEvent += e => Log(e, "log-event");
            pendingChecks = "";
            var log = root.Q<ScrollView>("play-log"); log.Clear();
            var dd = root.Q<DropdownField>("play-actor");
            dd.choices = gm.State.actors.Select(a => a.name).ToList();
            dd.SetValueWithoutNotify(dd.choices[0]);
            Show("play");
            RefreshPlaySheet();
            Status("");
            if (resume)
            {
                foreach (var m in gm.State.messages)
                    if (m.role == "user" && !string.IsNullOrEmpty(m.content)) Log(m.content, "log-player");
                    else if (m.role == "assistant" && !string.IsNullOrEmpty(m.content)) Log(m.content, "log-gm");
                if (gm.State.ended) Status(Strings.T("play.ended"));
            }
            else _ = Ask("");
        }

        async Awaitable Send()
        {
            var input = root.Q<TextField>("play-input");
            string text = input.value?.Trim();
            if (busy || string.IsNullOrEmpty(text) || gm.State.ended) return;
            string actor = root.Q<DropdownField>("play-actor").value;
            input.value = "";
            string line = $"[{actor}] {text}";
            Log(line, "log-player");
            await Ask(pendingChecks + line);
        }

        async Awaitable Ask(string message)
        {
            if (string.IsNullOrEmpty(Settings.ApiKey)) { Log(Strings.T("play.noKey"), "log-error"); return; }
            busy = true; Status(Strings.T("play.thinking")); root.Q<Button>("play-send").SetEnabled(false);
            try
            {
                string reply = await gm.Step(message);
                pendingChecks = "";
                if (!string.IsNullOrEmpty(reply)) Log(reply, "log-gm", type: true);
                Status(gm.State.ended ? Strings.T("play.ended") : "");
            }
            catch (Exception e) { Log(Strings.T("play.error") + ": " + e.Message, "log-error"); Status(""); }
            finally { busy = false; root.Q<Button>("play-send").SetEnabled(true); RefreshPlaySheet(); }
        }

        void RefreshPlaySheet()
        {
            if (gm == null) return;
            var a = gm.FindActor(root.Q<DropdownField>("play-actor").value) ?? gm.State.actors[0];
            var sheet = root.Q<ScrollView>("play-sheet"); sheet.Clear();
            foreach (var r in a.resources) { var l = new Label($"{Strings.Res(r.key)}: {r.value}/{r.max}"); l.AddToClassList("res-label"); sheet.Add(l); }
            foreach (var s in rs.skills)
            {
                string id = s.id;
                var b = new Button(() => OpenDifficulty(a, id)) { text = $"{Strings.Skill(id)}  {a.Skill(id)}" };
                b.AddToClassList("skill-btn");
                sheet.Add(b);
            }
        }

        void OpenDifficulty(Actor a, string skill)
        {
            var modal = root.Q("play-diff");
            modal.style.display = DisplayStyle.Flex;
            foreach (Difficulty d in Enum.GetValues(typeof(Difficulty)))
            {
                var b = root.Q<Button>("play-diff-" + d);
                b.clickable = new Clickable(() =>
                {
                    modal.style.display = DisplayStyle.None;
                    var r = Check.Roll(rs.check, a.Skill(skill), d);
                    string line = $"{Strings.T("play.checkPrefix")} {a.name} {Strings.Skill(skill)}({Strings.T("diff." + d)}) → {Strings.T("out." + r.outcome)} ({r.roll}/{r.target})";
                    Log(line, "log-event");
                    pendingChecks += $"[check] {a.name} {skill}({d}) → {r.outcome} ({r.roll}/{r.target})\n";
                });
            }
        }

        void Status(string s) => root.Q<Label>("play-status").text = s;

        void Log(string text, string cls, bool type = false)
        {
            var log = root.Q<ScrollView>("play-log");
            var l = new Label(type ? "" : text);
            l.AddToClassList(cls);
            log.Add(l);
            if (type) typeQueue.Enqueue((l, text));
            log.schedule.Execute(() => log.ScrollTo(l)).ExecuteLater(30);
        }

        void Update()
        {
            if (typing == null && typeQueue.Count > 0) { (typing, typingText) = typeQueue.Dequeue(); typed = 0; }
            if (typing == null) return;
            typed += Settings.TypeSpeed * Time.unscaledDeltaTime;
            int n = Mathf.Min(typingText.Length, (int)typed);
            typing.text = typingText.Substring(0, n);
            if (n >= typingText.Length) { typing = null; root.Q<ScrollView>("play-log").ScrollTo(root.Q<ScrollView>("play-log").contentContainer.Children().Last()); }
        }

        void SkipTyping()
        {
            if (typing != null) { typing.text = typingText; typing = null; }
            while (typeQueue.Count > 0) { var (l, t) = typeQueue.Dequeue(); l.text = t; }
        }
    }
}

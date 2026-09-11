using System;
using System.Collections.Generic;
using System.Linq;

namespace ActorExplorer
{
    [Serializable] public class Entry { public string key; public int value; }
    [Serializable] public class ResEntry { public string key; public int value; public int max; }

    /// PC / NPC の共通データ。JsonUtility でそのまま保存できるよう Dictionary は使わない。
    [Serializable]
    public class Actor
    {
        public string name = "";
        public List<Entry> stats = new List<Entry>();
        public List<Entry> skills = new List<Entry>();
        public List<ResEntry> resources = new List<ResEntry>();

        public int Stat(string id) => Find(stats, id)?.value ?? throw new KeyNotFoundException($"能力値 '{id}' がありません");
        public int Skill(string id) => Find(skills, id)?.value ?? throw new KeyNotFoundException($"技能 '{id}' がありません");
        public ResEntry Resource(string id) => resources.FirstOrDefault(r => r.key == id) ?? throw new KeyNotFoundException($"リソース '{id}' がありません");
        public bool HasSkill(string id) => Find(skills, id) != null;

        public void SetStat(string id, int v) => Set(stats, id, v);
        public void SetSkill(string id, int v) => Set(skills, id, v);

        /// リソースを増減して 0..max にクランプ。現在値を返す。
        public int Modify(string resourceId, int delta)
        {
            var r = Resource(resourceId);
            r.value = Math.Max(0, Math.Min(r.max, r.value + delta));
            return r.value;
        }

        /// 式の中の識別子を能力値として解決する。
        public int Var(string id) => Find(stats, id)?.value ?? throw new FormatException($"未知の識別子 '{id}'");

        /// ルールセットの生成式でランダム生成し、技能初期値・リソース最大値を導出する。
        public static Actor Create(Ruleset rs, string name)
        {
            var a = new Actor { name = name };
            foreach (var s in rs.stats) a.stats.Add(new Entry { key = s.id, value = Expr.Eval(s.gen) });
            foreach (var s in rs.skills) a.skills.Add(new Entry { key = s.id, value = Expr.Eval(s.init, a.Var) });
            foreach (var r in rs.resources) { int m = Expr.Eval(r.max, a.Var); a.resources.Add(new ResEntry { key = r.id, value = m, max = m }); }
            return a;
        }

        /// 能力値を手で書き換えた後にリソース最大値を引き直す（現在値は最大値に合わせる）。
        public void Rederive(Ruleset rs)
        {
            foreach (var r in rs.resources)
            {
                var e = Resource(r.id);
                if (System.Text.RegularExpressions.Regex.IsMatch(r.max, @"\dd\d")) continue; // ダイス由来（LP など）は振り直さない
                e.max = Expr.Eval(r.max, Var);
                e.value = e.max;
            }
        }

        public int SkillBudget(Ruleset rs) => Expr.Eval(rs.skillBudget, Var);
        public int SkillSpent(Ruleset rs) => rs.skills.Sum(s => Skill(s.id) - Expr.Eval(s.init, Var));

        static Entry Find(List<Entry> list, string id) => list.FirstOrDefault(e => e.key == id);
        static void Set(List<Entry> list, string id, int v)
        {
            var e = Find(list, id);
            if (e == null) list.Add(new Entry { key = id, value = v }); else e.value = v;
        }
    }
}

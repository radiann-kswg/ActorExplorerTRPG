using System;
using System.Collections.Generic;
using System.Linq;

namespace ActorExplorer
{
    /// ランダム生成時の技能ポイントの配り方。
    public enum SkillProfile { Generalist, Specialist }

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

        /// ランダム生成＋技能ポイントの自動配分（キャラ作成画面の「ランダム生成」）。
        public static Actor CreateRandom(Ruleset rs, string name, SkillProfile profile)
        {
            var a = Create(rs, name);
            a.Allocate(rs, profile);
            return a;
        }

        /// 技能を初期値に戻してから予算（skillBudget）を配る。
        /// Specialist: 主要 3 技能を 70〜80 まで。Generalist: 主要 4〜5 技能を 60〜70 まで。
        /// 残りは別の 2〜4 技能に 5 点刻みで散らし、予算を使い切る（上限 Cap）。
        public void Allocate(Ruleset rs, SkillProfile profile)
        {
            const int Step = 5, Cap = 85;
            foreach (var s in rs.skills) SetSkill(s.id, Expr.Eval(s.init, Var));
            int budget = SkillBudget(rs);
            var ids = rs.skills.Select(s => s.id).OrderBy(_ => Expr.Rng.Next()).ToList();
            bool spec = profile == SkillProfile.Specialist;
            int majors = spec ? 3 : Expr.Rng.Next(4, 6);
            int lo = spec ? 70 : 60, hi = spec ? 80 : 70;
            var major = ids.Take(majors).ToList();
            var minor = ids.Skip(majors).Take(Expr.Rng.Next(2, spec ? 4 : 5)).ToList();

            // 主要技能: 1 つずつ目標値まで上げる（予算が少なくても最初の技能は確実に高くなる）
            foreach (var id in major)
            {
                int target = Expr.Rng.Next(lo / Step, hi / Step + 1) * Step;
                while (budget >= Step && Skill(id) + Step <= target) { SetSkill(id, Skill(id) + Step); budget -= Step; }
            }
            // 残りを副技能へ。副技能も上限なら主要技能へ戻し、それでも余れば Cap まで誰かに
            var pool = minor.Concat(major).Concat(ids).ToList();
            for (int guard = 0; budget >= Step && guard < 200; guard++)
            {
                var id = pool[guard % pool.Count];
                if (Skill(id) + Step > Cap) continue;
                SetSkill(id, Skill(id) + Step); budget -= Step;
            }
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

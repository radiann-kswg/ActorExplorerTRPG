using System;
using System.Linq;
using NUnit.Framework;

namespace ActorExplorer.Tests
{
    public class ExprTests
    {
        [Test] public void Arithmetic()
        {
            Assert.AreEqual(11, Expr.Eval("(50+60)/10"));
            Assert.AreEqual(10, Expr.Eval("(55+50)/10"));   // 10.5 → 切り捨て
            Assert.AreEqual(7, Expr.Eval("1+2*3"));
            Assert.AreEqual(-2, Expr.Eval("-2"));
        }

        [Test] public void Functions()
        {
            Assert.AreEqual(2, Expr.Eval("ceil(3/2)"));
            Assert.AreEqual(5, Expr.Eval("max(3,5)"));
            Assert.AreEqual(3, Expr.Eval("min(3,5)"));
            Assert.AreEqual(1, Expr.Eval("floor(3/2)"));
        }

        [Test] public void Variables()
        {
            Func<string, int> v = n => n == "DEX" ? 70 : throw new FormatException(n);
            Assert.AreEqual(35, Expr.Eval("DEX/2", v));
            Assert.Throws<FormatException>(() => Expr.Eval("STR", v));
        }

        [Test] public void DiceStayInRange()
        {
            Expr.Rng = new Random(1);
            for (int i = 0; i < 200; i++)
            {
                int v = Expr.Eval("3d6*5");
                Assert.That(v, Is.InRange(15, 90));
                Assert.AreEqual(0, v % 5);
                Assert.That(Expr.Eval("1d100"), Is.InRange(1, 100));
            }
        }

        [Test] public void Malformed()
        {
            Assert.Throws<FormatException>(() => Expr.Eval("(1+2"));
            Assert.Throws<FormatException>(() => Expr.Eval("1+"));
            Assert.Throws<FormatException>(() => Expr.Eval("foo(1)"));
        }
    }

    public class CheckTests
    {
        static readonly CheckRule R = new CheckRule();

        [TestCase(50, 96)] [TestCase(20, 96)] [TestCase(70, 97)] [TestCase(90, 98)]
        [TestCase(110, 99)] [TestCase(130, 100)] [TestCase(150, 100)]
        public void FumbleThreshold(int skill, int expected) => Assert.AreEqual(expected, Check.FumbleFrom(R, skill));

        [Test] public void Targets()
        {
            Assert.AreEqual(50, Check.Target(R, 50, Difficulty.Normal));
            Assert.AreEqual(25, Check.Target(R, 50, Difficulty.Hard));
            Assert.AreEqual(10, Check.Target(R, 50, Difficulty.Extreme));
            Assert.AreEqual(11, Check.Target(R, 55, Difficulty.Extreme));
        }

        [Test] public void Outcomes()
        {
            Assert.AreEqual(Outcome.Critical, Check.Resolve(R, 50, Difficulty.Normal, 1).outcome);
            Assert.AreEqual(Outcome.Success, Check.Resolve(R, 50, Difficulty.Normal, 50).outcome);
            Assert.AreEqual(Outcome.Failure, Check.Resolve(R, 50, Difficulty.Normal, 51).outcome);
            Assert.AreEqual(Outcome.Fumble, Check.Resolve(R, 50, Difficulty.Normal, 96).outcome);
            Assert.AreEqual(Outcome.Failure, Check.Resolve(R, 90, Difficulty.Normal, 97).outcome); // 90 → 98 から
            Assert.AreEqual(Outcome.Fumble, Check.Resolve(R, 90, Difficulty.Normal, 98).outcome);
            Assert.AreEqual(Outcome.Success, Check.Resolve(R, 150, Difficulty.Normal, 99).outcome);
            Assert.AreEqual(Outcome.Fumble, Check.Resolve(R, 150, Difficulty.Normal, 100).outcome);
        }
    }

    public class ActorTests
    {
        static Ruleset Sample() => Ruleset.Load("sample-d100");

        [Test] public void SampleRulesetLoads()
        {
            var rs = Sample();
            Assert.AreEqual("sample-d100", rs.id);
            Assert.AreEqual(8, rs.stats.Length);
            Assert.AreEqual(4, rs.resources.Length);
            Assert.AreEqual(15, rs.skills.Length);
            Assert.AreEqual("96+ceil(max(0,SKILL-50)/20)", rs.check.fumble);
        }

        [Test] public void CreateDerivesEverything()
        {
            Expr.Rng = new Random(7);
            var a = Actor.Create(Sample(), "A");
            Assert.AreEqual(a.Stat("DEX") / 2, a.Skill("dodge"));
            Assert.AreEqual((a.Stat("STR") + a.Stat("SIZ")) / 10, a.Resource("HP").max);
            Assert.AreEqual(a.Stat("POW"), a.Resource("SP").max);
            Assert.AreEqual(a.Resource("HP").max, a.Resource("HP").value);
            Assert.AreEqual(a.Stat("EDU") * 4, a.SkillBudget(Sample()));
            Assert.AreEqual(0, a.SkillSpent(Sample()));
        }

        [Test] public void SkillDescLoads()
        {
            var rs = Sample();
            var organize = rs.skills.First(s => s.id == "organize");
            StringAssert.Contains("片付け", organize.desc.ja);
            StringAssert.Contains("Tidy", organize.desc.en);
            Assert.AreEqual("", UnityEngine.JsonUtility.FromJson<SkillDef>("{\"id\":\"x\",\"init\":\"1\"}").desc.ja); // desc 無しでも落ちない
        }

        [TestCase(SkillProfile.Generalist, 4, 60, 5 * 65)] [TestCase(SkillProfile.Specialist, 3, 70, 3 * 75)]
        public void AllocateSpendsBudgetOnMajors(SkillProfile profile, int majors, int floor, int enoughBudget)
        {
            var rs = Sample();
            for (int seed = 0; seed < 50; seed++)
            {
                Expr.Rng = new Random(seed);
                var a = Actor.CreateRandom(rs, "A", profile);
                int budget = a.SkillBudget(rs);
                Assert.AreEqual(budget, a.SkillSpent(rs), $"seed {seed}: 予算を使い切る");
                Assert.IsTrue(a.skills.All(s => s.value <= 85), $"seed {seed}: 上限 85");
                int high = a.skills.Count(s => s.value >= floor);
                if (budget >= enoughBudget) Assert.GreaterOrEqual(high, majors, $"seed {seed}: 主要技能が {floor} 以上 (budget {budget})");
                else Assert.GreaterOrEqual(high, 1, $"seed {seed}: 予算が少なくても 1 つは {floor} 以上 (budget {budget})");
            }
        }

        [Test] public void AllocateIsRepeatableAndResets()
        {
            var rs = Sample();
            Expr.Rng = new Random(11);
            var a = Actor.Create(rs, "A");
            a.Allocate(rs, SkillProfile.Specialist);
            a.Allocate(rs, SkillProfile.Generalist);
            Assert.AreEqual(a.SkillBudget(rs), a.SkillSpent(rs)); // 2 回目も初期値からやり直して使い切る
            foreach (var s in rs.skills) Assert.GreaterOrEqual(a.Skill(s.id), Expr.Eval(s.init, a.Var), s.id + " は初期値を下回らない");
        }

        [Test] public void ModifyClamps()
        {
            var a = Actor.Create(Sample(), "A");
            int max = a.Resource("HP").max;
            Assert.AreEqual(0, a.Modify("HP", -999));
            Assert.AreEqual(max, a.Modify("HP", 999));
            Assert.AreEqual(max - 1, a.Modify("HP", -1));
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => a.Modify("XX", 1));
        }

        [Test] public void RederiveKeepsDiceResources()
        {
            var a = Actor.Create(Sample(), "A");
            int lp = a.Resource("LP").max;
            a.SetStat("STR", 100); a.SetStat("SIZ", 100);
            a.Rederive(Sample());
            Assert.AreEqual(20, a.Resource("HP").max);
            Assert.AreEqual(lp, a.Resource("LP").max);
        }

        [Test] public void SurvivesJsonRoundTrip()
        {
            var a = Actor.Create(Sample(), "往復");
            a.Modify("HP", -2);
            var b = UnityEngine.JsonUtility.FromJson<Actor>(UnityEngine.JsonUtility.ToJson(a));
            Assert.AreEqual(a.name, b.name);
            Assert.AreEqual(a.Resource("HP").value, b.Resource("HP").value);
            Assert.AreEqual(a.skills.Count, b.skills.Count);
            Assert.IsTrue(a.stats.Select(e => e.value).SequenceEqual(b.stats.Select(e => e.value)));
        }
    }
}

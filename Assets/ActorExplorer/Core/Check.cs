using System;

namespace ActorExplorer
{
    public enum Difficulty { Normal, Hard, Extreme }
    public enum Outcome { Critical, Success, Failure, Fumble }

    [Serializable]
    public struct CheckResult
    {
        public int roll, target, fumbleFrom;
        public Outcome outcome;
        public bool Ok => outcome == Outcome.Critical || outcome == Outcome.Success;
        public override string ToString() => $"{outcome}({roll}/{target})";
    }

    /// d100 ロールアンダー判定。数値はここが正で、AI は結果を受け取るだけ。
    public static class Check
    {
        public static int Target(CheckRule rule, int skill, Difficulty d)
            => d == Difficulty.Hard ? skill / rule.hard : d == Difficulty.Extreme ? skill / rule.extreme : skill;

        public static int FumbleFrom(CheckRule rule, int skill)
            => Math.Min(100, Expr.Eval(rule.fumble, n => n == "SKILL" ? skill : throw new FormatException($"未知の識別子 '{n}'")));

        public static CheckResult Resolve(CheckRule rule, int skill, Difficulty d, int roll)
        {
            var r = new CheckResult { roll = roll, target = Target(rule, skill, d), fumbleFrom = FumbleFrom(rule, skill) };
            r.outcome = roll <= rule.critical ? Outcome.Critical
                      : roll >= r.fumbleFrom ? Outcome.Fumble
                      : roll <= r.target ? Outcome.Success : Outcome.Failure;
            return r;
        }

        public static CheckResult Roll(CheckRule rule, int skill, Difficulty d)
            => Resolve(rule, skill, d, Expr.Eval(rule.die));
    }
}

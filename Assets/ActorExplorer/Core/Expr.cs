using System;
using System.Collections.Generic;

namespace ActorExplorer
{
    /// ルールセット JSON の式ミニ言語: 数値, NdM(ダイス), 識別子(能力値など), + - * / (), ceil() floor() max() min()。
    /// 計算は double、結果は切り捨てて int。
    public static class Expr
    {
        public static Random Rng = new Random();

        public static int Eval(string expr, Func<string, int> vars = null)
            => (int)Math.Floor(new Parser(expr, vars).ParseAll());

        public static int Roll(int count, int sides)
        {
            int sum = 0;
            for (int i = 0; i < count; i++) sum += Rng.Next(1, sides + 1);
            return sum;
        }

        class Parser
        {
            readonly string s; readonly Func<string, int> vars; int i;
            public Parser(string s, Func<string, int> vars) { this.s = s ?? ""; this.vars = vars; }

            public double ParseAll()
            {
                double v = Sum();
                Skip();
                if (i < s.Length) throw new FormatException($"式の末尾に余分な文字: '{s}' @{i}");
                return v;
            }

            void Skip() { while (i < s.Length && s[i] == ' ') i++; }

            double Sum()
            {
                double v = Term();
                for (Skip(); i < s.Length && (s[i] == '+' || s[i] == '-'); Skip())
                { char op = s[i++]; double r = Term(); v = op == '+' ? v + r : v - r; }
                return v;
            }

            double Term()
            {
                double v = Unary();
                for (Skip(); i < s.Length && (s[i] == '*' || s[i] == '/'); Skip())
                { char op = s[i++]; double r = Unary(); v = op == '*' ? v * r : v / r; }
                return v;
            }

            double Unary()
            {
                Skip();
                if (i < s.Length && s[i] == '-') { i++; return -Unary(); }
                return Atom();
            }

            double Atom()
            {
                Skip();
                if (i >= s.Length) throw new FormatException($"式が途中で終わっています: '{s}'");
                char c = s[i];
                if (c == '(')
                {
                    i++; double v = Sum(); Skip();
                    if (i >= s.Length || s[i] != ')') throw new FormatException($"')' がありません: '{s}'");
                    i++; return v;
                }
                if (char.IsDigit(c))
                {
                    int n = ReadInt();
                    if (i < s.Length && s[i] == 'd') { i++; return Roll(n, ReadInt()); }
                    return n;
                }
                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                    string name = s.Substring(start, i - start);
                    Skip();
                    if (i < s.Length && s[i] == '(') { i++; return Call(name); }
                    if (vars == null) throw new FormatException($"未知の識別子 '{name}' in '{s}'");
                    return vars(name);
                }
                throw new FormatException($"解釈できない文字 '{c}' in '{s}' @{i}");
            }

            double Call(string name)
            {
                var args = new List<double>();
                while (true)
                {
                    args.Add(Sum()); Skip();
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == ')') { i++; break; }
                    throw new FormatException($"関数 {name} の括弧が閉じていません: '{s}'");
                }
                switch (name)
                {
                    case "ceil": return Math.Ceiling(args[0]);
                    case "floor": return Math.Floor(args[0]);
                    case "max": return Math.Max(args[0], args[1]);
                    case "min": return Math.Min(args[0], args[1]);
                    default: throw new FormatException($"未知の関数 '{name}' in '{s}'");
                }
            }

            int ReadInt()
            {
                int start = i;
                while (i < s.Length && char.IsDigit(s[i])) i++;
                if (start == i) throw new FormatException($"数字が必要です: '{s}' @{i}");
                return int.Parse(s.Substring(start, i - start));
            }
        }
    }
}

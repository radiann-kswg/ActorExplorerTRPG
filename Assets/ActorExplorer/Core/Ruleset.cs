using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ActorExplorer
{
    [Serializable] public class StatDef { public string id; public string gen; }
    [Serializable] public class ResourceDef { public string id; public string max; }
    /// desc は AI が「行動→技能」を引くための短い説明（任意）。表示名は strings.json 側。
    [Serializable] public class SkillDef { public string id; public string init; public LocText desc = new LocText(); }
    [Serializable]
    public class CheckRule
    {
        public string die = "1d100";
        public int hard = 2;
        public int extreme = 5;
        public int critical = 1;
        public string fumble = "96+ceil(max(0,SKILL-50)/20)";
    }

    /// ルールブック。StreamingAssets/ActorExplorer/rulesets/<id>.json をそのまま写した形。
    [Serializable]
    public class Ruleset
    {
        public string id;
        public StatDef[] stats = new StatDef[0];
        public ResourceDef[] resources = new ResourceDef[0];
        public string skillBudget = "0";
        public SkillDef[] skills = new SkillDef[0];
        public CheckRule check = new CheckRule();

        public static string Dir => Path.Combine(Application.streamingAssetsPath, "ActorExplorer", "rulesets");
        public static Ruleset Load(string id) => JsonUtility.FromJson<Ruleset>(File.ReadAllText(Path.Combine(Dir, id + ".json")));
        public static IEnumerable<string> Ids() => Directory.Exists(Dir)
            ? Directory.GetFiles(Dir, "*.json").Select(Path.GetFileNameWithoutExtension) : Enumerable.Empty<string>();
    }
}

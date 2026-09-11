using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ActorExplorer
{
    /// 英日文字列テーブル。StreamingAssets/ActorExplorer/strings.json の [{key, ja, en}]。
    /// 表示名は "stat.STR" "skill.observe" "res.HP" のキーで引く。未定義ならキーをそのまま返す。
    public static class Strings
    {
        [Serializable] class Row { public string key; public string ja; public string en; }
        [Serializable] class Table { public Row[] rows; }

        static Dictionary<string, Row> map;

        public static string Path => System.IO.Path.Combine(Application.streamingAssetsPath, "ActorExplorer", "strings.json");

        public static void Load()
        {
            map = new Dictionary<string, Row>();
            if (!File.Exists(Path)) return;
            var t = JsonUtility.FromJson<Table>("{\"rows\":" + File.ReadAllText(Path) + "}");
            foreach (var r in t.rows) map[r.key] = r;
        }

        public static string T(string key)
        {
            if (map == null) Load();
            if (!map.TryGetValue(key, out var r)) return key;
            string s = Settings.Language == "en" ? r.en : r.ja;
            return string.IsNullOrEmpty(s) ? (string.IsNullOrEmpty(r.ja) ? key : r.ja) : s;
        }

        public static string Stat(string id) => T("stat." + id);
        public static string Skill(string id) => T("skill." + id);
        public static string Res(string id) => T("res." + id);
    }
}

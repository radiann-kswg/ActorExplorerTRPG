using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ActorExplorer
{
    [Serializable]
    public class LocText
    {
        public string ja = "", en = "";
        public string Get(string lang) => lang == "en" ? (string.IsNullOrEmpty(en) ? ja : en) : (string.IsNullOrEmpty(ja) ? en : ja);
    }

    /// シナリオ。StreamingAssets/ActorExplorer/scenarios/<id>.json。
    [Serializable]
    public class Scenario
    {
        public string id;
        public LocText title = new LocText();
        public LocText summary = new LocText();
        public LocText openingText = new LocText();
        public string gmNotes = "";
        public string[] images = new string[0]; // 段階拡張用の予約

        public static string Dir => Path.Combine(Application.streamingAssetsPath, "ActorExplorer", "scenarios");
        public static Scenario Load(string id) => JsonUtility.FromJson<Scenario>(File.ReadAllText(Path.Combine(Dir, id + ".json")));
        public static IEnumerable<string> Ids() => Directory.Exists(Dir)
            ? Directory.GetFiles(Dir, "*.json").Select(Path.GetFileNameWithoutExtension) : Enumerable.Empty<string>();
    }
}

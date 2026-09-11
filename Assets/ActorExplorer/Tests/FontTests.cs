using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ActorExplorer.Tests
{
    /// 同梱フォントが UI で使う文字を持っているか（ピクセルフォントは収録漢字が少ないものがあるため、差し替え時の保険）。
    public class FontTests
    {
        const string Body = "Assets/ActorExplorer/Fonts/MaruMonica/x12y16pxMaruMonica.ttf";
        const string Hud = "Assets/ActorExplorer/Fonts/HeadUpDaisy/x14y24pxHeadUpDaisy.ttf";

        // 判定カード・ログの数値行と結果行（HUD 書体）で必ず出る文字
        const string HudChars = "出目目標値成功失敗クリティカルファンブル0123456789/→ ";

        [TestCase(Hud, HudChars)]
        [TestCase(Body, HudChars + "判定通常困難極限手動耐久力行動力忍耐力幸福度観察感性隠密統括生物医学応急手当整理整頓雑学運転計算解読機械技術回避武術射撃登攀")]
        public void FontCoversUiText(string path, string chars)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(path);
            Assert.IsNotNull(font, path + " が無い。Tools > ActorExplorer > Fetch Fonts で取得");
            string missing = new string(chars.Where(c => !font.HasCharacter(c)).Distinct().ToArray());
            Assert.AreEqual("", missing, path + " に無い文字");
        }

        [Test] public void LicenseFilesAreBundled()
        {
            foreach (var p in new[] { Body, Hud })
                Assert.IsTrue(System.IO.File.Exists(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(p), "LICENSE.txt")), p + " の隣に LICENSE.txt");
        }
    }
}

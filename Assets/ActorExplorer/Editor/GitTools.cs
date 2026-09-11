using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

/// Tools > Git Commit All: 変更を全ステージして規定メッセージでコミットする。
/// メッセージは EditorPrefs "GitTools.Message" があればそれを使用（コミット後クリア）。
public static class GitTools
{
    [MenuItem("Tools/Git Commit All")]
    public static void CommitAll()
    {
        string root = Directory.GetCurrentDirectory();
        string lockFile = Path.Combine(root, ".git", "index.lock");
        if (File.Exists(lockFile)) File.Delete(lockFile);

        string msg = EditorPrefs.GetString("GitTools.Message", "");
        if (string.IsNullOrEmpty(msg)) msg = "WIP: 定期コミット";
        EditorPrefs.DeleteKey("GitTools.Message");
        msg = msg.Replace('"', '\'').TrimEnd('\\'); // 引用符・末尾\で引数が壊れてコミット失敗するのを防ぐ

        UnityEngine.Debug.Log("[GitTools] add: " + Run(root, "add -A"));
        UnityEngine.Debug.Log("[GitTools] commit: " + Run(root,
            $"-c user.name=Radian -c user.email=snine9801@gmail.com commit -m \"{msg}\""));
        UnityEngine.Debug.Log("[GitTools] log: " + Run(root, "log --oneline -3"));
    }

    /// 任意のgitコマンド実行（RunCommandのコンパイル文脈からProcessが使えないため公開）
    public static string RunGit(string args)
    {
        return Run(Directory.GetCurrentDirectory(), args);
    }

    static string Run(string cwd, string args)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        using (var p = Process.Start(psi))
        {
            // stdout/stderrは必ず非同期で両方読む。順次ReadToEnd()はstderrが
            // パイプバッファ(数KB)を超えた時点でデッドロックする（実測: TMP導入時の
            // CRLF警告大量出力でgit add -Aが永久停止しUnityごとフリーズした）
            var so = p.StandardOutput.ReadToEndAsync();
            var se = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(300000))
            {
                try { p.Kill(); } catch { }
                return "[timeout] git " + args;
            }
            return $"[exit {p.ExitCode}] {so.Result}{se.Result}";
        }
    }
}

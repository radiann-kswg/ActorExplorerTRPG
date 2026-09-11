# CLAUDE.md — ActorExplorerTRPG

このリポジトリの AI エージェント設定の単一情報源（SSOT）は `AGENTS.md` です。

@AGENTS.md

- 設定の追加・変更は必ず `AGENTS.md` に対して行ってください。本ファイルには設定内容を直接書かないでください。
- **ブランチ運用（`AGENTS.md` 2章）を厳守**: 作業は常に `develop` で行い、`main` へは直接コミットしないこと。
- **git の書き込みはサンドボックスから行わない**（`AGENTS.md` 2章）。`Unity_RunCommand` → `GitTools.RunGit(...)` / `GitTools.CommitAll()`。
- 仕様の正本は `docs/REQUIREMENTS.md`。
- ロールプレイ設定はこのリポジトリに置きません（`AGENTS.md` 5章）。利用者側の環境設定に従ってください。

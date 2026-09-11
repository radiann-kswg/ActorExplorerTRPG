# AGENTS.md — ActorExplorerTRPG

AI エージェント向けの設定の単一情報源（SSOT）。`CLAUDE.md` はここへのポインタ。設定の追加・変更はこのファイルにだけ行う。

## 1. プロジェクト

- クトゥルフ神話 TRPG から汎用性の高い部分を取り出し、**神話・異形探索以外の題材にも使える**ルールブックを JSON で定義して、AI GM と Unity 6 で 1 人プレイできるフレームワーク。MIT ライセンス。テンプレートリポジトリとしてフォークし、ルール・シナリオを改変する用途を想定。
- 要件の正本: `docs/REQUIREMENTS.md`。仕様で迷ったらまずそこを読む。
- 特定作品の固有名詞・用語は使わない（README の "inspired by" のみ）。ルールの汎用名で迷ったら User に確認する。

## 2. ブランチと git

- 作業は `develop`。`main` へのマージ・push は User の指示があるときだけ。
- Cowork（Linux サンドボックス）からは git を書かない。Unity エディタ経由で `Tools > Git Commit All` または `Unity_RunCommand` → `GitTools.RunGit("...")` / `GitTools.CommitAll()`（`Assets/ActorExplorer/Editor/GitTools.cs`）。
- `Library/` `Temp/` `Logs/` `obj/` `UserSettings/` `*.csproj` `*.sln*` は編集・コミット対象外（`.gitignore` 済み）。`.meta` は Unity に任せる。

## 3. 構成

- ランタイム: `Assets/ActorExplorer/`（asmdef `ActorExplorer`）。`Core/`（ルール・判定・セーブ）、`AI/`（LLM クライアントと GM ループ）、`UI/`（UI Toolkit）。
- Editor 拡張: `Assets/ActorExplorer/Editor/`（asmdef `ActorExplorer.Editor`）。
- テスト: `Assets/ActorExplorer/Tests/`（EditMode のみ。対象はダイス・式評価・判定・リソース。AI 呼び出しはテストしない）。
- データ: `Assets/StreamingAssets/ActorExplorer/{rulesets,scenarios}/*.json`、`strings.json`（ja/en）。Unity を開かずにテキスト編集できることを保つ。
- 設定・API キー: `PlayerPrefs`（`ae.*`）。キーをリポジトリに書かない・ログに出さない。
- セーブ: `Application.persistentDataPath/actorexplorer/save.json`。

## 4. 設計原則

- 数値・ダイス・判定・リソースは**エンジンが正**。AI は描写とツール呼び出し（`request_check` / `modify_resource` / `end_session`）だけ。AI に数値を決めさせない。
- LLM は OpenAI 互換 `chat/completions` を 1 クラスで叩く。プロバイダごとのクラスや抽象化を増やさない。非ストリーミング。
- シリアライズは `JsonUtility` の範囲に収める（Dictionary は key/value 配列）。外部 JSON ライブラリを足さない。
- 依存パッケージを増やさない。標準ライブラリ → Unity 標準機能 → 既存パッケージの順で探す。
- 段階拡張（画像切替 `set_scene`、所持品、技能予算の強制）は `docs/REQUIREMENTS.md` §8 に従い、基本動線が安定してから。

## 5. ロールプレイ

- このリポジトリはロールプレイ設定を**持たない**。口調や人格の設定は利用者側の環境（Cowork プロジェクトやローカル設定）に委ねる。
- ロールプレイが有効な環境でも、技術タスクの正確性・安全性・本ファイルの運用ルールを常に優先する。

# ActorExplorerTRPG 要件定義書

最終更新: 2026-09-11 ／ 状態: **合意済み**（2026-09-11、フェーズ 0 着手）

## 1. 目的

クトゥルフ神話 TRPG から**汎用性の高い部分だけを取り出し、神話・異形探索以外の題材にも使えるルールブック**を JSON で定義し、AI をゲームマスター（GM）にして Unity 6 上で 1 人で遊べるフレームワーク。MIT ライセンスのテンプレートリポジトリとして公開し、フォーク先でルールブック・シナリオを改変できることを主眼に置く。

- 数値・ダイス・判定・リソースは**エンジンが正**。AI は描写と「判定して」という要求（function calling）だけを出す。
- 特定作品由来の固有名詞は使わない。README に "inspired by" とだけ記す。

## 2. スコープ

| 項目 | 決定 |
| --- | --- |
| リポジトリ | 1 本で完結。`Assets/ActorExplorer/` に asmdef 1 本（Editor / Tests は別 asmdef） |
| プレイ形態 | 1 台ローカル・人間 1 人・PC（プレイヤーキャラ）複数体可 |
| 対象 | Unity 6000.3.x、2D URP テンプレート、Editor 再生＋Windows スタンドアロン |
| 非目標 | WebGL（API キー露出）、マルチプレイ、ストリーミング応答、画像の AI 切替（後述の段階拡張） |
| ブランチ | `develop` で作業。`main` へは指示時のみ。コミットは Unity 側 `GitTools.RunGit` / `CommitAll` 経由 |
| ロールプレイ | リポジトリ内に RP ファイルは置かない（利用者の環境設定に委ねる）。`AGENTS.md` は技術ルールのみ |

## 3. 用語

- **Actor**: PC / NPC の共通データ。能力値（stats）・技能（skills）・リソース（resources）を持つ。
- **Ruleset**: 能力値・技能・リソースの定義と、生成式・導出式・判定規則。`StreamingAssets/ActorExplorer/rulesets/*.json`。
- **Scenario**: プレイヤー向け導入と GM 専用ノート。`StreamingAssets/ActorExplorer/scenarios/*.json`。
- **Check（判定）**: d100 ロールアンダー。技能値と難易度から成功段階を返す。
- **Session**: 進行中のプレイ。セーブ 1 ファイル＝Session。

## 4. データ形式（すべて JsonUtility で読み書きできる形）

### 4.1 Ruleset（同梱サンプル `sample-d100.json`）

```json
{
  "id": "sample-d100",
  "stats": [
    { "id": "STR", "gen": "3d6*5" }, { "id": "CON", "gen": "3d6*5" }, { "id": "DEX", "gen": "3d6*5" },
    { "id": "APP", "gen": "3d6*5" }, { "id": "POW", "gen": "3d6*5" },
    { "id": "SIZ", "gen": "(2d6+6)*5" }, { "id": "INT", "gen": "(2d6+6)*5" }, { "id": "EDU", "gen": "(2d6+6)*5" }
  ],
  "resources": [
    { "id": "HP", "max": "(STR+SIZ)/10" }, { "id": "MP", "max": "(CON+POW)/10" },
    { "id": "SP", "max": "POW" },          { "id": "LP", "max": "3d6*5" }
  ],
  "skillBudget": "EDU*4",
  "skills": [
    { "id": "observe", "base": "25" }, { "id": "sense", "base": "20" }, { "id": "stealth", "base": "20" },
    { "id": "command", "base": "15" }, { "id": "biomed", "base": "5" }, { "id": "firstaid", "base": "30" },
    { "id": "organize", "base": "20" }, { "id": "trivia", "base": "20" }, { "id": "drive", "base": "20" },
    { "id": "decode", "base": "10" }, { "id": "mechanics", "base": "10" }, { "id": "dodge", "base": "DEX/2" },
    { "id": "martial", "base": "25" }, { "id": "firearms", "base": "20" }, { "id": "climb", "base": "20" }
  ],
  "check": { "die": "1d100", "hard": 2, "extreme": 5, "critical": 1, "fumble": "96+ceil(max(0,SKILL-50)/20)" }
}
```

表示名（日本語／英語）は文字列テーブル側に `stat.STR = 物理 / Strength`、`skill.sense = 感性 / Perception` のように持つ。JSON の `id` は不変キー。

**式ミニ言語**: `NdM`（ダイス）、`+ - * /`、括弧、能力値 ID、`ceil()` `max()`。整数除算は切り捨て。実装は「ダイスを正規表現で振ってから `System.Data.DataTable.Compute` で評価」を第一候補、Unity で使えなければ 60 行程度の再帰下降パーサ。

**判定**: 目標値 = 技能値（通常）／÷2（困難）／÷5（極限）。出目 ≤ 目標値で成功。出目 = 1 でクリティカル。ファンブルは `96 + ceil(max(0, 技能−50)/20)` 以上（上限 100。技能 ≤ 50 → 96〜100、技能 90 → 98〜100、技能 ≥ 130 → 100 のみ）。成功段階は `Critical / Success / Failure / Fumble` の 4 値。

### 4.2 Scenario（同梱サンプル 1 本）

```json
{
  "id": "sample-01",
  "title": { "ja": "…", "en": "…" },
  "summary": { "ja": "プレイヤーに見せる導入", "en": "…" },
  "openingText": { "ja": "GM の最初の描写の素材", "en": "…" },
  "gmNotes": "AI にだけ渡す。舞台・NPC・秘密・手がかり・終了条件を Markdown 風の自由文で。",
  "images": []
}
```

`images` は段階拡張（§8）用の予約。MVP では空。

### 4.3 Save（`Application.persistentDataPath/actorexplorer/save.json`）

```json
{
  "rulesetId": "sample-d100", "scenarioId": "sample-01", "language": "ja",
  "actors": [ { "name": "…", "stats": [{"key":"STR","value":60}], "skills": [...], "resources": [{"key":"HP","value":11,"max":11}] } ],
  "messages": [ { "role": "user|assistant|tool", "content": "…", "toolCallsJson": "…", "toolCallId": "…" } ],
  "ended": false
}
```

Dictionary は `key/value` の配列で代替。`system` は保存せず毎回組み立てる。

### 4.4 Settings（PlayerPrefs）

`ae.provider`（claude / openai / gemini / custom）、`ae.baseUrl`、`ae.model`、`ae.apiKey`、`ae.language`（ja / en）、`ae.historyLimit`（既定 40）、`ae.typeSpeed`（既定 40 文字/秒）。プロバイダ選択で baseUrl とモデルの初期値を埋め、いずれも自由編集可。

| プロバイダ | baseUrl 初期値 | モデル初期値 |
| --- | --- | --- |
| claude | `https://api.anthropic.com/v1/` | `claude-sonnet-4-5` |
| openai | `https://api.openai.com/v1/` | `gpt-4.1-mini` |
| gemini | `https://generativelanguage.googleapis.com/v1beta/openai/` | `gemini-2.5-flash` |
| custom | 空 | 空 |

### 4.5 文字列テーブル

`StreamingAssets/ActorExplorer/strings.json`: `[{ "key": "ui.send", "ja": "送信", "en": "Send" }, …]`。UI 文言・表示名・GM プロンプトの言語指示を含む。

## 5. AI GM

- クライアントは **1 クラス**。`POST {baseUrl}chat/completions`、`Authorization: Bearer {apiKey}`、非ストリーミング、`UnityWebRequest`。リクエスト JSON は手組み（空配列や null を送らないため）、レスポンスは JsonUtility で読む。
- **tools（MVP は 3 つ）**
  - `request_check(actor, skill, difficulty)` → エンジンが振り、`{ "roll": 37, "target": 50, "result": "Success" }` を tool result で返す
  - `modify_resource(actor, resource, delta, reason)` → 0〜max にクランプして適用、現在値を返す
  - `end_session(outcome)` → セッション終了フラグ
- **system prompt** = ルールセット要約（能力値・技能・リソースの一覧と判定方式） + シナリオ `gmNotes` + 全 PC のシート + 言語指示 + 「数値は自分で決めず必ずツールを使う」指示。
- **ループ**: 送信 → `tool_calls` があれば全部実行して `tool` メッセージを追加し再送 → 本文だけの応答が返るまで（上限 5 往復）。
- **履歴**: 直近 `historyLimit` 件 + system。古いものから落とす。要約はしない。
- 手動判定（シートの技能クリック → 難易度選択）の結果は `[判定] 観察(通常) → 成功(37)` の形で次の user メッセージ先頭に添える。
- 複数 PC: 入力欄横のドロップダウンで発言者を選び、`[<name>] <text>` として送る。

## 6. UI（UI Toolkit・ランタイム UXML/USS）

1. **タイトル**: 新規 / 続き / 設定 / 終了
2. **設定**: §4.4 の項目
3. **キャラ作成**: ランダム生成ボタン、能力値・技能の整数フィールド、導出リソースの表示、技能予算の表示（超過は止めない）、PC の追加・削除、シナリオ選択 → 開始
4. **プレイ**: ログ（文字送り、クリックでスキップ）、入力欄 + 発言 PC ドロップダウン + 送信、キャラクターシート（技能クリックで手動判定）、背景 1 枚 + 立ち絵 3 枚の**スロットのみ**（MVP は空）、セーブ / タイトルへ

## 7. リポジトリ構成

```
Assets/ActorExplorer/
  ActorExplorer.asmdef
  Core/      Ruleset.cs Actor.cs Dice.cs Check.cs Expr.cs Scenario.cs SaveData.cs Strings.cs
  AI/        LlmClient.cs GmLoop.cs
  UI/        *.uxml *.uss  Screens.cs
  Editor/    ActorExplorer.Editor.asmdef GitTools.cs
  Tests/     ActorExplorer.Tests.asmdef DiceTests.cs CheckTests.cs ExprTests.cs ResourceTests.cs
Assets/StreamingAssets/ActorExplorer/  rulesets/  scenarios/  strings.json
docs/REQUIREMENTS.md  AGENTS.md  CLAUDE.md  LICENSE(MIT, RadianN_kswg)  README.md
```

manifest から除外: `com.unity.ai.inference` `com.unity.visualscripting` `com.unity.timeline` `com.unity.multiplayer.center` `com.unity.collab-proxy` `com.unity.pipeline`。`com.unity.ai.assistant` は当初除外予定だったが **Unity MCP のリレー（`Unity.AI.MCP.Editor`）を含む**ため開発用依存として残す（2026-09-11 に外して Cowork 接続が落ちたのを確認）。`com.unity.recorder` はデバッグ録画用。ランタイムはどちらにも依存しない。

## 8. 段階拡張（MVP 後・順不同）

- `set_scene(background, portraits[])` と `images` の実装（画像が数枚揃ってから）
- `add_item / remove_item`（所持品）
- 技能予算の強制（バランス調整と同時に）
- 導出式の別案へ戻す余地: `HP=(CON+SIZ)/10`、`MP=POW/5`（JSON の式を書き換えるだけ）

## 9. フェーズ計画と完了条件

| # | 内容 | 完了条件 |
| --- | --- | --- |
| 0 | manifest 削減、GitTools、AGENTS.md/CLAUDE.md、LICENSE、asmdef、本書、ルート CLAUDE.md へ 1 行 | Unity がエラーなく開き、最初のコミットが通る |
| 1 | コア（Ruleset 読込、Actor、Dice、Expr、Check、リソース）+ EditMode テスト | Test Runner 全緑。ファンブル境界（50/90/130）と式評価のテスト含む |
| 2 | LlmClient、設定保存、GmLoop（tools 3 つ） | Editor 上でサンプルシナリオの導入描写と判定要求が 1 往復以上通る（3 プロバイダのうち 1 つで確認） |
| 3 | UI 4 画面、文字送り、手動判定、セーブ/ロード | 新規 → キャラ作成 → プレイ → セーブ → 再開 が通る |
| 4 | サンプルシナリオ、strings.json（ja/en）、README、Windows ビルド | ビルド済み exe で MVP 動線が通る |

各フェーズ末に GitTools でコミット。

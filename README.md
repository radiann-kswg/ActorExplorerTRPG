# ActorExplorerTRPG

[日本語](#日本語) / [English](#english)

![プレイ画面 — 判定カードとメッセージウィンドウ](docs/images/play.png)

## 日本語

クトゥルフ神話 TRPG に着想を得た**汎用 TRPG ルールブック**を JSON で定義し、AI をゲームマスターにして Unity 6 上で 1 人で遊べるフレームワークです。神話・異形探索以外の題材（日常・人間関係・仕事・旅）にも使えるよう、ルールの汎用的な部分だけを取り出しています。MIT ライセンス。テンプレートとしてフォークし、ルールブックとシナリオを書き換えて使ってください。

### 特徴

- **数値はエンジンが正**。ダイス・判定・リソースの増減はすべて C# 側で処理し、AI は描写と「この技能で判定して」という要求（function calling）だけを出します。AI に数値を決めさせません。
- **ルール・シナリオは JSON**。`Assets/StreamingAssets/ActorExplorer/` 以下のテキストを書き換えるだけで、能力値・技能・導出式・判定ルール・シナリオを変えられます。Unity を開かなくても編集できます。
- **AI GM は OpenAI 互換 API なら何でも**。Claude / OpenAI / Gemini / ローカルサーバを設定画面で切り替え。API キーは端末内（PlayerPrefs）にだけ保存されます。
- **平成中期のノベルゲーム風 UI**（UI Toolkit）。画面下のメッセージウィンドウ、判定カード、バックログ、手動判定シート。ピクセルフォント同梱。
- 英日 2 言語（`strings.json`）。

### 画面

| タイトル | キャラクター作成 |
| --- | --- |
| ![タイトル](docs/images/title.png) | ![キャラクター作成](docs/images/chargen.png) |

| ログ（バックログ） | シート（手動判定） |
| --- | --- |
| ![ログ](docs/images/log.png) | ![シート](docs/images/sheet.png) |

- **キャラクター作成**: 能力値をダイスで生成し、技能ポイント（`EDU×4`）を「万能型（主要 4〜5 技能を 60〜70）」か「専門家型（3 技能を 70〜80）」で自動配分。手で書き換えても構いません。PC は複数作れます。
- **プレイ**: 発言者（PC）を選んで行動を入力。GM が描写し、必要なときだけ判定を要求します。判定は中央のカードに「誰の何判定 → 出目／目標値 → 結果」で出て、次の送信まで残ります。上帯にリソース、右上の「ログ」で全文、「シート」で技能・能力値を押して自分で判定（結果は次の発言に添えて GM に渡ります）。「セーブ」で 1 スロット保存、タイトルの「つづきから」で再開。

### 動かす

1. Unity 6000.3 以降でこのリポジトリを開く（2D URP、UI Toolkit。追加パッケージ不要）。
2. `Assets/ActorExplorer/UI/Main.unity` を開いて Play。またはビルド（Windows で確認済み）。
3. タイトル → **設定** で プロバイダ・Base URL・モデル・API キーを入力。
   - claude: `https://api.anthropic.com/v1/`（OpenAI 互換エンドポイント）
   - openai: `https://api.openai.com/v1/`
   - gemini: `https://generativelanguage.googleapis.com/v1beta/openai/`
   - custom: 任意の OpenAI 互換 `chat/completions`（tool calling 対応のもの）
4. **はじめから** → シナリオを選び、PC を作って **開始**。

### 自分のルール・シナリオにする

すべて `Assets/StreamingAssets/ActorExplorer/` 以下。ID は不変キー、表示名は `strings.json` 側です。

**ルールセット** `rulesets/<id>.json`（サンプル: `sample-d100.json`）

```json
{
  "id": "my-rules",
  "stats":     [ { "id": "STR", "gen": "3d6*5", "desc": { "ja": "力比べ", "en": "Contests of strength" } } ],
  "resources": [ { "id": "HP",  "max": "(STR+SIZ)/10" } ],
  "skillBudget": "EDU*4",
  "skills":    [ { "id": "observe", "init": "25", "desc": { "ja": "目で見て探す・違和感に気づく", "en": "Search by sight" } } ],
  "check": { "die": "1d100", "hard": 2, "extreme": 5, "critical": 1, "fumble": "96+ceil(max(0,SKILL-50)/20)" }
}
```

- 式ミニ言語: `NdM`、`+ - * /`、括弧、能力値 ID、`ceil() floor() max() min()`。結果は切り捨て整数。
- `desc` は AI が「プレイヤーの行動 → どの技能／能力値で判定するか」を引くための説明です。短く具体的に書くほど取り違えが減ります。能力値に `desc` を書くと、技能に当てはまらない場面（力比べ・第一印象・ひらめき）で能力値そのものを目標値にした判定を AI が要求できます。
- 判定: 出目 ≤ 目標値（通常＝技能値、困難＝÷2、極限＝÷5）で成功。出目 `critical` 以下でクリティカル、`fumble` 式以上でファンブル。
- 表示名は `strings.json` に `stat.<ID>` / `skill.<ID>` / `res.<ID>` のキーで追加します。

**シナリオ** `scenarios/<id>.json`（サンプル: `sample-01` 廃駅の捜索、`sample-02` 喫茶店の一日店長＝探索なし）

```json
{
  "id": "my-scenario",
  "title":       { "ja": "…", "en": "…" },
  "summary":     { "ja": "プレイヤー向けの一言", "en": "…" },
  "openingText": { "ja": "GM の最初の描写の素材", "en": "…" },
  "gmNotes": "AI にだけ渡す秘密。舞台・NPC・真相・手がかり（どの技能で・どの難易度で）・危険・終了条件（end_session の outcome 名）を自由文で。",
  "images": []
}
```

`gmNotes` に「日常動作は判定しない」「失敗しても事実は変わらない」といった進行のコツを書いておくと安定します。`images` は背景・立ち絵の切り替え（今後）用の予約。

**AI が呼べるツール**（`AI/GmLoop.cs`）: `request_check(actor, skill|stat, difficulty)` / `modify_resource(actor, resource, delta, reason)` / `end_session(outcome)`。ツールを増やしたいときは `ToolsJson()` と `RunTool()` に 1 つずつ足します。

### コードの地図

```
Assets/ActorExplorer/
  Core/   Expr（式）・Ruleset・Check（d100 判定）・Actor（PC・配分）・Scenario・Strings
  AI/     Settings（PlayerPrefs）・LlmClient（OpenAI 互換 chat/completions を 1 クラス）・GmLoop（GM ループ・tools・セーブ）
  UI/     App.cs（4 画面の配線）・Main.uxml/.uss・Main.unity
  Fonts/  MaruMonica（本文）・HeadUpDaisy（数値・判定結果）＋各 LICENSE.txt
  Editor/ GitTools・TestRunnerMenu・DevTools（Settings 窓 / GM Smoke Test / UI Preview / Fetch Fonts）
  Tests/  EditMode（ダイス・式・判定・配分・プロンプト・フォント収録）
Assets/StreamingAssets/ActorExplorer/  rulesets/  scenarios/  strings.json
docs/   REQUIREMENTS.md（仕様の正本）・HANDOFF.md（現状と次の作業）・images/
```

- テスト: `Tools > ActorExplorer > Run EditMode Tests`（結果は `Temp/ae-test-results.txt`）。
- API を叩かずに画面だけ確認: Play 中に `Tools > ActorExplorer > UI Preview (Play mode)`。
- AI との 1 往復を Console で確認: `Tools > ActorExplorer > GM Smoke Test`。
- 設計上のルール（依存を増やさない、JsonUtility の範囲に収める、など）は `AGENTS.md`。

### サードパーティ

- フォント **x12y16pxMaruMonica**（本文）と **x14y24pxHeadUpDaisy**（見出し・数値）— hicc / 患者長ひっく、[x0y0pxFreeFont](https://hicchicc.github.io/00ff/)。`Assets/ActorExplorer/Fonts/` 以下。MIT の対象外で、各フォルダの `LICENSE.txt`（配布元の利用規約）に従います。フォントを差し替えるときは `Tests/FontTests.cs` で「出目・目標値・成功・失敗」などの収録を確認してください。

## English

A Unity 6 framework for solo tabletop RPG play with an AI game master, driven by a **generic TRPG rulebook defined in JSON**. Inspired by Call of Cthulhu–style d100 systems, but keeps only the generic parts so it also works for slice-of-life, social or travel scenarios. MIT licensed — fork it as a template and rewrite the rulebook and scenarios.

### Features

- **The engine owns the numbers.** Dice, checks and resource changes happen in C#; the AI only narrates and asks for checks via function calling. It never decides a number.
- **Rules and scenarios are JSON** under `Assets/StreamingAssets/ActorExplorer/`. Stats, skills, derived formulas, check rules and scenarios can be edited without opening Unity.
- **Any OpenAI-compatible API** as the GM: Claude / OpenAI / Gemini / local servers, chosen in the in-game settings. API keys stay on your machine (PlayerPrefs).
- **Mid-2000s Japanese visual-novel style UI** (UI Toolkit): bottom message window, check cards, backlog, a sheet for manual rolls. Pixel fonts bundled.
- Japanese / English strings (`strings.json`).

### Screens

| Title | Character creation |
| --- | --- |
| ![Title](docs/images/title.png) | ![Character creation](docs/images/chargen.png) |

| Log (backlog) | Sheet (manual checks) |
| --- | --- |
| ![Log](docs/images/log.png) | ![Sheet](docs/images/sheet.png) |

- **Character creation**: stats are rolled; skill points (`EDU×4`) are auto-allocated as *Generalist* (4–5 skills at 60–70) or *Specialist* (3 skills at 70–80). Edit anything by hand. Multiple PCs are supported.
- **Play**: pick the speaking PC and type an action. The GM narrates and requests a check only when it matters. Checks appear on a card (who / which skill → roll / target → outcome) and stay until your next message. Resources sit in the top bar; **Log** shows the full transcript, **Sheet** lets you roll a skill or stat yourself (the result is attached to your next message). **Save** writes one slot; *Continue* on the title screen resumes it.

### Run it

1. Open the repository in Unity 6000.3+ (2D URP, UI Toolkit; no extra packages).
2. Open `Assets/ActorExplorer/UI/Main.unity` and press Play, or build (tested on Windows).
3. Title → **Settings**: provider, base URL, model, API key.
   - claude: `https://api.anthropic.com/v1/` (OpenAI-compatible endpoint)
   - openai: `https://api.openai.com/v1/`
   - gemini: `https://generativelanguage.googleapis.com/v1beta/openai/`
   - custom: any OpenAI-compatible `chat/completions` with tool calling
4. **New Game** → choose a scenario, create PCs, **Start**.

### Make it yours

Everything lives under `Assets/StreamingAssets/ActorExplorer/`. IDs are stable keys; display names live in `strings.json`.

**Ruleset** `rulesets/<id>.json` (sample: `sample-d100.json`)

```json
{
  "id": "my-rules",
  "stats":     [ { "id": "STR", "gen": "3d6*5", "desc": { "ja": "力比べ", "en": "Contests of strength" } } ],
  "resources": [ { "id": "HP",  "max": "(STR+SIZ)/10" } ],
  "skillBudget": "EDU*4",
  "skills":    [ { "id": "observe", "init": "25", "desc": { "ja": "目で見て探す", "en": "Search by sight, notice details" } } ],
  "check": { "die": "1d100", "hard": 2, "extreme": 5, "critical": 1, "fumble": "96+ceil(max(0,SKILL-50)/20)" }
}
```

- Expression mini-language: `NdM`, `+ - * /`, parentheses, stat IDs, `ceil() floor() max() min()`; results are floored to integers.
- `desc` is what the AI reads to map a player's action to a skill (or a stat, for raw contests such as strength, first impressions or insight). Short and concrete beats long.
- Check: roll ≤ target succeeds (normal = skill, hard = ÷2, extreme = ÷5). Roll ≤ `critical` is a critical; roll ≥ the `fumble` expression is a fumble.
- Add display names to `strings.json` as `stat.<ID>` / `skill.<ID>` / `res.<ID>`.

**Scenario** `scenarios/<id>.json` (samples: `sample-01`, an abandoned-station search; `sample-02`, running a café for a day — no exploration)

```json
{
  "id": "my-scenario",
  "title":       { "ja": "…", "en": "…" },
  "summary":     { "ja": "…", "en": "one line for the player" },
  "openingText": { "ja": "…", "en": "material for the GM's first narration" },
  "gmNotes": "Secret notes for the AI only: setting, NPCs, the truth, clues (which skill, which difficulty), dangers, endings (end_session outcome names).",
  "images": []
}
```

Tips in `gmNotes` such as "don't roll for routine actions" and "a failure never changes the facts" keep sessions stable. `images` is reserved for background / portrait switching.

**Tools the AI can call** (`AI/GmLoop.cs`): `request_check(actor, skill|stat, difficulty)`, `modify_resource(actor, resource, delta, reason)`, `end_session(outcome)`. To add one, extend `ToolsJson()` and `RunTool()`.

### Code map

```
Assets/ActorExplorer/
  Core/   Expr, Ruleset, Check (d100), Actor (PCs, allocation), Scenario, Strings
  AI/     Settings (PlayerPrefs), LlmClient (one class for OpenAI-compatible chat/completions), GmLoop (GM loop, tools, save)
  UI/     App.cs (4 screens), Main.uxml/.uss, Main.unity
  Fonts/  MaruMonica (body), HeadUpDaisy (numbers, outcomes) + LICENSE.txt each
  Editor/ GitTools, TestRunnerMenu, DevTools (Settings window / GM Smoke Test / UI Preview / Fetch Fonts)
  Tests/  EditMode tests (dice, expressions, checks, allocation, prompt, font coverage)
Assets/StreamingAssets/ActorExplorer/  rulesets/  scenarios/  strings.json
docs/   REQUIREMENTS.md (spec, Japanese), HANDOFF.md (status and next steps, Japanese), images/
```

- Tests: `Tools > ActorExplorer > Run EditMode Tests` (results in `Temp/ae-test-results.txt`).
- Preview the UI without an API key: `Tools > ActorExplorer > UI Preview (Play mode)` while playing.
- One round-trip with the AI in the Console: `Tools > ActorExplorer > GM Smoke Test`.
- Design rules (no new dependencies, stay within JsonUtility, …) are in `AGENTS.md`.

### Third-party

- Fonts **x12y16pxMaruMonica** (body) and **x14y24pxHeadUpDaisy** (headings, numbers) by hicc ([x0y0pxFreeFont](https://hicchicc.github.io/00ff/)) — under `Assets/ActorExplorer/Fonts/`. Not covered by the MIT license; see each folder's `LICENSE.txt` (the distributor's terms). When swapping fonts, run `Tests/FontTests.cs` to make sure the glyphs the UI needs are present.

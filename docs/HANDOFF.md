# ActorExplorerTRPG 引き継ぎ書（2026-09-11 セッション末）

次のセッション以降で作業する人（人間でもエージェントでも）が、このファイルと `docs/REQUIREMENTS.md` だけ読めば続きに入れることを目的にする。仕様の正本は `docs/REQUIREMENTS.md`、ここは「現状」「初回プレイで見つかった改善点」「次にやる順番」。

## 1. 現状（MVP 完了・`develop`・未 push）

| コミット | 内容 |
| --- | --- |
| `b279b21` `1eca699` | フェーズ 0: manifest 整理、GitTools、AGENTS/CLAUDE/LICENSE/README、asmdef、Player 設定 |
| `bf376a8` | フェーズ 1: `Core/`（Expr・Ruleset・Check・Actor・Scenario）＋ `sample-d100.json`＋テスト 19 |
| `03529fe` | フェーズ 2: `AI/`（LlmClient・Settings・GmLoop）、`sample-01.json`、Editor 設定窓／スモークテスト、テスト計 25 |
| `9f30914` | フェーズ 3: UI Toolkit 4 画面、文字送り、手動判定、セーブ／ロード、`Main.unity`、`strings.json` |
| `3179c1e` | フェーズ 4: README、Windows ビルド確認 |

- ライブ確認済み: OpenAI `gpt-4.1-mini` で導入描写 → `request_check` → 失敗を踏まえた描写（`Tools > ActorExplorer > GM Smoke Test`）。User が実プレイも 1 回実施。
- Windows ビルド: `Build/Windows/ActorExplorerTRPG.exe`（git 管理外）。
- テスト: `Tools > ActorExplorer > Run EditMode Tests` → `Temp/ae-test-results.txt`。

### コードの地図

```
Assets/ActorExplorer/
  Core/Expr.cs      式パーサ（NdM, + - * /, (), ceil/floor/max/min, 識別子）。double 計算 → 切り捨て
  Core/Ruleset.cs   JSON の写し。skills の初期値キーは `init`
  Core/Check.cs     d100 ロールアンダー。Target / FumbleFrom / Resolve / Roll
  Core/Actor.cs     stats/skills/resources（key-value 配列）。Create / Rederive / Modify / SkillBudget / SkillSpent
  Core/Scenario.cs  LocText(ja/en) + gmNotes + images(予約)
  Core/Strings.cs   strings.json（[{key,ja,en}]）。Strings.T / Stat / Skill / Res
  AI/Settings.cs    PlayerPrefs ae.*（provider/baseUrl/model/apiKey/language/historyLimit/typeSpeed）
  AI/LlmClient.cs   OpenAI 互換 chat/completions を 1 クラス。リクエスト手組み、レスポンス JsonUtility
  AI/GmLoop.cs      SaveData（=セッション状態、Save/Load）、system prompt、tools JSON、Step ループ、RunTool
  UI/App.cs         4 画面の配線。文字送りは Update() でキュー消化
  UI/Main.uxml/.uss  画面定義。PanelSettings.asset + DefaultRuntimeTheme.tss
  Editor/GitTools.cs / TestRunnerMenu.cs / DevTools.cs（Settings 窓・GM Smoke Test）
  Tests/CoreTests.cs / AiTests.cs
Assets/StreamingAssets/ActorExplorer/  rulesets/sample-d100.json  scenarios/sample-01.json  strings.json
```

## 2. 初回プレイで見つかった改善点と対応方針

優先度は User の指定どおり。「案」は次セッションで実装前に軽く確認してから着手。

### 2.1 【優先】技能の取り違え（「整理整頓」を「生物医学」で判定した）

**原因の見立て**: ツールの `skill` 引数は英語 ID（`organize` / `biomed` …）の enum だけで、日本語の表示名との対応を AI に渡していない。日本語で描写している AI が「整理整頓 → organize」を引けず、近そうな ID を選んだ。

**対応案**
1. ルールセット JSON の技能に短い説明を持たせる（`"desc": {"ja": "片付け・収納・物の在処を探す", "en": "..."}`）。※ JSON スキーマ変更。
2. system prompt の RULESET 行を「一覧」から「表」にする: `organize — 整理整頓 — 片付け・収納・物の在処を探す` の 1 行/技能。能力値・リソースも同様に `STR — 物理`。
3. tools JSON の `request_check.skill` の description に同じ対応表を入れる（enum は ID のまま）。
4. AI からの `request_check` 結果を UI ログに出すとき、`Strings.Skill(id)` で日本語名を表示する（現状は ID と英語 enum。2.3 と同件）。
5. 追加の保険: prompt に「判定を要求する前に、プレイヤーの行動に最も近い技能を表から選ぶ。該当がなければ判定せず描写で進める」。

### 2.2 【優先】ゲームバランス（失敗が多い）

**原因の見立て**（順に効きが大きい）
1. **ランダム生成で技能ポイントを配っていない**。`Actor.Create` は初期値（観察 25 など）のまま。予算 `EDU*4` は表示するだけで未使用。参考にした d100 系では、職業技能に予算を振って主要技能が 60〜80% になるのが前提。初期値のままなら 7〜8 割失敗するのは計算どおり。
2. AI が些細な行動にも判定を要求している可能性（「待合室を調べる」で観察 25 に判定）。
3. 失敗＝「事実が逆になる」描写になっていた（缶コーヒーが冷めていた）。

**対応案**
1. `Actor.Create`（＝ランダム生成ボタン）で予算を自動配分する: 主要 4〜5 技能をランダムに選んで 60〜70 まで上げ、残りを 2〜4 技能に散らす。手編集はそのまま可。予算超過の強制は従来どおり後回し（REQUIREMENTS §8）。
2. system prompt に判定の基準を明記: 「日常的・確実な行動は判定せず成功として描写」「判定は失敗すると話が動く場面だけ」「難易度 hard/extreme は明確な理由があるときだけ」。
3. 失敗時の扱い: 「失敗は"気づけなかった／できなかった"であって、シナリオの事実を変えない。手がかりは別の方法や別の場所で再挑戦できる形で残す」。
4. ルールセットの初期値見直し（観察 25→30 等）は上 3 つの後で、実プレイの失敗率を見てから。目安: 一般的な行動の成功率 60〜75%、専門行動 40〜60%。
5. **「探索以外のシナリオでも安定して」**の観点: 社会・日常系の技能（統括・雑学・整理整頓・感性）の初期値を戦闘系と同等以上にし、サンプルシナリオ 2 本目として非探索（人間関係・仕事・旅など）のものを用意して確認する。

### 2.3 ダイスロールの表示（英文 ID → 和文）

- `GmLoop.OnEvent` はエンジン側なので ID/英語のまま、**受け取る `App.Log` 側で** `Strings.Skill(id)` / `Strings.T("diff.X")` / `Strings.T("out.X")` に変換して表示する。イベント文字列を構造化（`(kind, actor, skillId, difficulty, outcome, roll, target)`）にすると UI 側で自由に組める。手動判定の行は既に和文。
- 演出強化は別フェーズで本格的に（§3 のフェーズ C）。当面は「出目 → 目標値 → 結果」を 1 行ではなく 3 段で見せる程度の下準備だけ。

### 2.4 UI 全体（フォントが小さい・見た目）

- 基本 `font-size` 16px → 20〜22px、ログは 22px、見出し 32px を目安に `.root` から見直し、`PanelSettings` の基準解像度（1280×720）は維持。
- 見た目の方向: **平成中期のノベルゲーム／実況動画**の雰囲気（画面下のメッセージウィンドウ＋名前プレート、半透明の枠、ピクセルフォント、立ち絵は中央〜左右、ログは別画面）。この美化は Claude Design で先にモックを作ってから UXML/USS に落とす。
- 現状の USS の罠は `docs/REQUIREMENTS.md` ではなく Cowork のプロジェクトメモリに記録済み（テーマ tss 必須／複合セレクタ不可／grow は basis 0）。

### 2.5 【任意】基本パラメータ（STR / POW …）の活用

現状は能力値が「導出式の材料」にしか使われていない。候補:
1. **能力値判定**: `request_check` の `skill` に能力値 ID も受け付ける（エンジンが技能→能力値の順に探す）。`STR` 判定＝力比べ、`POW` 判定＝精神力、`INT` 判定＝ひらめき、`APP` 判定＝第一印象、`DEX` 判定＝反射。これだけで「探索以外」の場面がかなり回る。
2. **対抗判定**: `request_opposed(actorA, statOrSkill, actorB, statOrSkill)` を後で。NPC は Actor として GM ノートに書いておく形。
3. **戦闘補正**: `STR+SIZ` からダメージボーナスを導出（ルールセットの `derived` に式として持たせる）。
4. どの形式でも「ルールセット JSON で式として定義でき、Unity を開かずに変えられる」を守る。

### 2.6 【任意】ピクセルフォント「患者長ひっく」（hicchicc）導入

調査結果（2026-09-11、配布ページ https://hicchicc.github.io/00ff/ と GitHub https://github.com/hicchicc ）:

| フォント | ライセンス | 同梱・再配布 | 入手 |
| --- | --- | --- | --- |
| x8y12pxDenkiChip / x5y8pxNegaTape / x5y8pxNegaClip | **SIL OFL 1.1** | 可（OFL.txt 同梱、フォント単体販売は不可） | GitHub リポジトリあり → **サブモジュール可** |
| x12y16pxMaruMonica / x8y12pxTheStrongGamer / x14y24pxHeadUpDaisy ほか | 独自ライセンス（無償・商用可・「.ttf を同梱してのゲームリリース可」「加工・改変・再配布も可能」と明記） | 可（配布ページの利用規約文を同梱） | 配布ページから .ttf（GitHub 管理ではない → サブモジュール不可） |

- MIT リポジトリへの同梱は **どちらも可能**。OFL 系は `OFL.txt` を、独自ライセンス系は配布ページの規約文を `Assets/ActorExplorer/Fonts/<name>/LICENSE.txt` として一緒に置く。README の Third-party にも 1 行ずつ。
- 推奨: UI 本文には **x12y16pxMaruMonica**（12×16、丸ゴシック、読みやすい）、見出し／ダイス演出には **x8y12pxDenkiChip**（OFL、太字、サブモジュール可）。MaruMonica は配布ページ直の .ttf なのでリポジトリ直置き（ライセンス文付き）。
- 実装: `.ttf` を `Assets/ActorExplorer/Fonts/` に置き、UI Toolkit なら USS の `-unity-font-definition: url("…ttf")` で当てる（TextMeshPro は使っていない）。ピクセルフォントは基準サイズの整数倍（12/16 の倍数）で使うとにじまない。
- 同梱前に配布ページの利用規約の**原文全文**を読んで、上表の要約と食い違いがないか確認すること（この表はページ要約からの転記）。

## 3. 次の対応（順番）

| # | 内容 | 主な変更箇所 | 完了条件 |
| --- | --- | --- | --- |
| A | 技能取り違え対策（§2.1）＋失敗時の扱い（§2.2-3）＋判定基準（§2.2-2） | `sample-d100.json`（desc 追加）、`Ruleset.cs`、`GmLoop.BuildSystemPrompt/ToolsJson`、`App.Log` の和文化 | スモークテストで「整理整頓」と書いた行動に `organize` が選ばれる。失敗描写が事実を変えない |
| B | 技能ポイント自動配分（§2.2-1）＋初期値見直し | `Actor.Create`、`sample-d100.json`、テスト追加 | ランダム生成で主要技能が 60 以上になる。実プレイ 1 回で失敗率が目安に入る |
| C | ダイス演出の下準備（§2.3）：イベントの構造化と和文表示、出目→目標→結果の 3 段表示 | `GmLoop.OnEvent` の型、`App.cs` | ログに ID／英語が出ない |
| D | UI 拡大と美化（§2.4）：Claude Design でモック → USS/UXML | `Main.uss/.uxml`、Claude Design | 1280×720 で本文 20px 以上、平成中期ノベル風のレイアウト |
| E | フォント導入（§2.6） | `Assets/ActorExplorer/Fonts/`、USS、README | ライセンス文同梱、ゲーム画面で適用 |
| F | 能力値判定（§2.5-1） | `GmLoop.RequestCheck`、tools JSON、prompt | `request_check(skill="STR")` が通る |
| G | 非探索サンプルシナリオ 2 本目（§2.2-5） | `scenarios/sample-02.json` | 実プレイで判定分布を確認 |

A〜C はコード中心で 1 セッションにまとめられる。D〜E は見た目のセッション、F〜G はルール拡張のセッション、として分けるのが無理がない。各セッションの頭で `AGENTS.md` と本ファイルを読み、終わりに本ファイル §1 の表と §3 の状態を更新する。

## 4. 運用メモ（次セッションで踏まないために）

- Unity の操作は unity-mcp 経由。**`com.unity.ai.assistant` を manifest から外さない**（MCP リレーの本体）。
- git はサンドボックスから書かず `GitTools.RunGit` / `CommitAll`（Unity 側）。
- UXML/USS は Play 中に変えても反映されない。Stop → `AssetDatabase.ImportAsset(..., ForceUpdate)` → Play。
- UI の自動操作は `root.Q<Button>(name).SendEvent(new NavigationSubmitEvent{target=b})`、画面確認は `ScreenCapture.CaptureScreenshot` を `EditorApplication.delayCall` 2 段で。
- Unity Recorder は他プロジェクトで Editor クラッシュの前例あり。動画が要るなら PNG 連番＋ffmpeg を逃げ道に。

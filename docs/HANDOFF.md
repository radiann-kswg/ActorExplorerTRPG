# ActorExplorerTRPG 引き継ぎ書（2026-09-11 セッション 2 末）

次のセッション以降で作業する人（人間でもエージェントでも）が、このファイルと `docs/REQUIREMENTS.md` だけ読めば続きに入れることを目的にする。仕様の正本は `docs/REQUIREMENTS.md`、ここは「現状」「初回プレイで見つかった改善点」「次にやる順番」。

## 1. 現状（MVP 完了・`develop`・push 済み）

| コミット | 内容 |
| --- | --- |
| `b279b21` `1eca699` | フェーズ 0: manifest 整理、GitTools、AGENTS/CLAUDE/LICENSE/README、asmdef、Player 設定 |
| `bf376a8` | フェーズ 1: `Core/`（Expr・Ruleset・Check・Actor・Scenario）＋ `sample-d100.json`＋テスト 19 |
| `03529fe` | フェーズ 2: `AI/`（LlmClient・Settings・GmLoop）、`sample-01.json`、Editor 設定窓／スモークテスト、テスト計 25 |
| `9f30914` | フェーズ 3: UI Toolkit 4 画面、文字送り、手動判定、セーブ／ロード、`Main.unity`、`strings.json` |
| `3179c1e` | フェーズ 4: README、Windows ビルド確認 |
| `96148e9` | 引き継ぎ書 v1 |
| `e749f84` | **A〜C**: 技能 `desc` 表と判定基準・失敗の扱いをプロンプトへ、技能ポイント自動配分（万能/専門家）、`GmEvent` 構造化と和文 3 段表示 |
| `dd82844` | **D〜E**: 平成中期ノベル風 UI（メッセージ窓・判定カード・ログ/シートのオーバーレイ・3 色構成）、ピクセルフォント同梱 |
| （次） | **F〜G**: 能力値判定（`request_check` に能力値 ID）、非探索シナリオ `sample-02`、フォント収録テスト |

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

## 2. セッション 2 で入れたもの（A〜G の完了状態）

| # | 状態 | 何をしたか / 確認したこと |
| --- | --- | --- |
| A 技能取り違え | 完了 | `sample-d100.json` の技能に `desc`（ja/en）。system prompt と tools の `skill.description` に「ID — 和名 — 説明」の表。判定基準（日常行動は振らない／直接触れた事実は判定なし／失敗は GM NOTES の事実を変えない）。スモークで「整理整頓しながら道具を探す」→ `organize`、失敗しても缶コーヒーは温かいまま |
| B 技能ポイント | 完了 | `Actor.CreateRandom(rs, name, SkillProfile)`。万能型＝主要 4〜5 技能を 60〜70、専門家型＝3 技能を 70〜80、残りを副技能に 5 点刻み、予算 `EDU*4` を使い切る（上限 85）。キャラ作成の「配分」ドロップダウン。**初期値は据え置き**（実プレイの失敗率を見てから） |
| C ダイス表示 | 完了 | `GmEvent`（kind/actor/id/difficulty/result/before/after/max/reason/manual/isStat）。UI は判定カード（画面中央、次の送信か Esc まで表示）＋ログの 3 段（誰の何判定／出目・目標値／→ 結果）。ID・英語はログに出ない |
| D UI 美化 | 完了 | Claude Design のモック → `Main.uxml/.uss` を作り直し。全面の舞台（背景・立ち絵 3 枠は空スロット）、上帯（シナリオ名・リソース）、下のメッセージ窓（名前プレート・文字送り）、入力行。ログ／シート（手動判定）はオーバーレイ（外側クリックか Esc で閉じる）。本文 22px・UI 20px。配色 3 色: `#f2efe6` / `#0b0d12`〜`#171a22` / `#ffd591` |
| E フォント | 完了 | `Assets/ActorExplorer/Fonts/MaruMonica/x12y16pxMaruMonica.ttf`（本文）と `HeadUpDaisy/x14y24pxHeadUpDaisy.ttf`（見出し・数値・判定結果、USS クラス `hud`）。各フォルダに配布元規約の `LICENSE.txt`、README に Third-party。`.ttf` は git 管理、`Tools > ActorExplorer > Fetch Fonts` で再取得可。DenkiChip は収録漢字が小 1〜4 年分だけなので不採用 |
| F 能力値判定 | 完了 | `request_check(skill=<能力値 ID>)` はエンジンが技能→能力値の順に探し、能力値の値をそのまま目標値に。`StatDef.desc`（いつ振るか）をプロンプト／tools に載せる。シートのオーバーレイに能力値ボタンも並ぶ |
| G 非探索シナリオ | 完了 | `scenarios/sample-02.json`「一日店長、閉店まで」（喫茶店の 1 日、社会・日常系。command / sense / organize / trivia / APP / INT が中心）。スモークで動作確認（§3） |

### フォント差し替え時の約束

`Tests/FontTests.cs` が「出目・目標値・成功・失敗・クリティカル・ファンブル・数字」を HUD 書体が、技能名・能力値名などを本文書体が持っているかを `Font.HasCharacter` で検査する。書体を替えたらこのテストを通すこと（ピクセルフォントは収録漢字が少ないものがある）。

### 残っている観察・課題（次に見るもの）

- 実プレイでの判定分布はまだ本格的に計測していない。初期値見直し（観察 25→30 等）は User が 1〜2 回遊んでから。sample-02 のスモーク（万能型・主要技能に社会系が入らなかった回）では sense 20 / command 15 で 3 回中 3 回失敗、APP 70 は成功。**社会系（統括・感性・整理整頓・雑学）の初期値を戦闘系並みに上げる案（§HANDOFF v1 2.2-5）は有力**。
- 立ち絵・背景は空スロット（`play-background` / `play-portrait-0..2`、USS `.background` `.portrait`）。`set_scene` ツールと `images` の段階拡張は REQUIREMENTS §8。
- 文字送りは 1 窓のみ。GM 本文が 6 行を超えると窓からはみ出す（`.msg` は overflow hidden）。プロンプトの「a few paragraphs」で概ね収まるが、長文対策（ページ送り▼）はダイス演出フェーズで。
- キャラ作成の技能グリッドはスクロールしない（15 技能で収まる前提）。技能が増えるルールセットでは要対応。
- Unity Editor が非フォーカスだと Game view が描画されず `ScreenCapture` が出ない。RunCommand で `Application.runInBackground = true` にしてから撮る。
- **device_commit_files の罠**: 同じ staged パスを使い回すと古い内容が書かれることがあった（DevTools.cs で 1 回発生）。書いた後に SHA を Unity 側で照合するか、別パス名で書く。

## 3. 次の対応（候補、順不同）

| # | 内容 | メモ |
| --- | --- | --- |
| H | ダイスロール演出の本番（TRPG の見せ場） | 判定カードに出目のカウントアップ・効果音・クリティカル/ファンブルの派手さ。`GmEvent` はそのまま使える |
| I | 実プレイ計測とバランス調整 | User が sample-01 / sample-02 を各 1 回。失敗率が目安（一般 60〜75%・専門 40〜60%）から外れたら `init` を JSON で調整 |
| J | 背景・立ち絵の `set_scene` | REQUIREMENTS §8。画像は `StreamingAssets` から `Texture2D` 読み込み → `style.backgroundImage` |
| K | 対抗判定 `request_opposed` | NPC を Actor として GM ノートに置く形（§2.5-2） |
| L | 長文のページ送り／ログの見た目 | メッセージ窓の▼と、ログの GM/プレイヤーの名札 |

## 4. 運用メモ（次セッションで踏まないために）

- Unity の操作は Unity MCP 経由（Windows の Claude は純正 `com.unity.ai.assistant`、Mac の Claude と Windows の Codex は CoplayDev `com.coplaydev.unity-mcp`）。**どちらも manifest から外さない**。
- git はサンドボックスから書かず `GitTools.RunGit` / `CommitAll`（Unity 側）。
- UXML/USS は Play 中に変えても反映されない。Stop → `AssetDatabase.ImportAsset(..., ForceUpdate)` → Play。
- UI の自動操作は `root.Q<Button>(name).SendEvent(new NavigationSubmitEvent{target=b})`、画面確認は `ScreenCapture.CaptureScreenshot` を `EditorApplication.delayCall` 2 段で。API を叩かずに見た目だけ確認するなら Play 中に `Tools > ActorExplorer > UI Preview (Play mode)`。
- `GM Smoke Test` は EditorPrefs `ae.smoke.scenario` / `ae.smoke.actions`（改行区切り）で対象シナリオと行動を差し替えられる。
- RunCommand のスクリプトでは `System.Reflection` の using / 完全修飾どちらも拒否される。private メンバを叩きたいときは Editor 側のメニュー（DevTools.cs）に書く。
- Unity Recorder は他プロジェクトで Editor クラッシュの前例あり。動画が要るなら PNG 連番＋ffmpeg を逃げ道に。

# ActorExplorerTRPG

[日本語](#日本語) / [English](#english)

## 日本語

クトゥルフ神話 TRPG から着想を得た**汎用 TRPG ルールブック**を JSON で定義し、AI をゲームマスターにして Unity 6 上で 1 人で遊べるフレームワークです。神話・異形探索以外の題材にも使えるよう、ルールの汎用的な部分だけを取り出しています。

- ルール・シナリオは `Assets/StreamingAssets/ActorExplorer/` の JSON。Unity を開かずに書き換えられます。
- 数値・ダイス・判定はエンジンが担当し、AI は描写と「判定して」という要求だけを出します。
- AI GM は OpenAI 互換 `chat/completions` API（Claude / OpenAI / Gemini / ローカルサーバ）を設定画面から選べます。API キーは端末内（PlayerPrefs）にのみ保存されます。
- MIT ライセンス。テンプレートとしてフォークし、ルールブックを改変して使ってください。

> 開発中です。仕様は [docs/REQUIREMENTS.md](docs/REQUIREMENTS.md) を参照してください。

## English

A Unity 6 framework for solo tabletop RPG play with an AI game master, driven by a **generic TRPG rulebook defined in JSON**. Inspired by Call of Cthulhu–style d100 systems, but keeps only the generic parts so it can be used for settings beyond mythos / horror investigation.

- Rulesets and scenarios are JSON under `Assets/StreamingAssets/ActorExplorer/` and can be edited without opening Unity.
- The engine owns all numbers, dice and checks; the AI only narrates and asks for checks via function calling.
- The AI GM talks to any OpenAI-compatible `chat/completions` endpoint (Claude / OpenAI / Gemini / local servers), chosen in the in-game settings. API keys stay on your machine (PlayerPrefs).
- MIT licensed. Fork it as a template and change the rulebook.

> Work in progress. See [docs/REQUIREMENTS.md](docs/REQUIREMENTS.md) (Japanese).

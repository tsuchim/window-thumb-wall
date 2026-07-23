# Copilot Instructions — WindowThumbWall

## Project Truth

- Windows desktop app using WPF on .NET 10 and the native DWM thumbnail API.
- 配布、CI、ドキュメントの変更でも ZIP、MSI、MSIX の 3 形式を維持する。

## Key Rules

- この repo での作業は、remote EC2 app host や production host への SSH・調査・変更の許可を意味しない。
- domain 名、デプロイ文書、Compose 設定、infra 文書から remote host 作業の許可を推定しない。
- remote host 作業は、対象 host と意図された操作を明示したユーザー指示がある場合に限る。
- 完了に必要で現スコープ内の follow-up は自分で要否を判断し、その場で進める。`必要なら` や `次に` という条件付きの先送りで締めない。
- 準備・検証・引き渡しを求められたら、アクセス可能な入力は自分で読み、検証し、成果物を出す。計画文書や役割分担の書き換えで代替しない。
- 公開配布する成果物は tag-driven GitHub Actions が生成した CI ビルドを正本とする。
- ローカルビルドは開発と検証用であり、配布物の正本として扱わない。
- プロダクトを screen capture や streaming アプリへ拡張する前提で変更しない。
- release と packaging の変更では `docs/invariants.md`、`docs/releasing.md`、`docs/developer-guide.md` を正本として従う。

## Verification

- 通常の確認は `dotnet build WindowThumbWall.sln` または `dotnet run --project WindowThumbWall.csproj` を使う。
- packaging 変更では `.\packaging\build-all.ps1` か個別 build script で 3 形式を維持しているか確認する。

## Agent observability

When investigating stalled, slow, expensive, repetitive, or suspicious agent sessions, inspect the local Copilot OpenTelemetry output before guessing.

Primary local trace file: `C:\Users\tsuchim\copilot-otel\copilot-otel.jsonl`

When available, also use the exported SQLite trace database from `Chat: Export Agent Traces DB`.

Inspect:
- the last `invoke_agent`, `chat`, `execute_tool`, or `execute_hook` span before a stall
- long-running tool calls
- failed tool calls
- failed terminal commands
- repeated or unnecessary tool calls
- model names
- token usage
- cache read / cache creation token usage
- subagent parent-child relationships
- prompt / response / tool argument / tool result content when captured

The current observability policy is content-first: capture useful local hints from information already handed to agents unless log volume becomes impractical.

Do not silently disable content capture. If log volume becomes too large, report measured file size growth and propose a retention or truncation policy.

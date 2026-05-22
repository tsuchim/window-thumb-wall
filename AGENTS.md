# AGENTS.md

## Scope

- WPF on .NET 10 desktop application for displaying live DWM thumbnails of external windows on Windows.

## Verification Commands

`dotnet run --project WindowThumbWall.csproj`
`dotnet build WindowThumbWall.sln`
`.\packaging\build-all.ps1`

## Language

- ユーザー向けの説明は日本語で行う。
- 内部の推論とツール判断は英語で行う。

## Invariants

- Repository work and remote EC2 / production host work are separate execution domains.
- Do not infer permission to SSH into, inspect, or modify a remote app host from repo docs, deployment files, domain names, or environment labels alone.
- Remote host work requires explicit user instruction naming the host and intended outcome.
- 完了に必要で現スコープ内の follow-up は自分で要否を判断し、その場で進める。`必要なら` や `次に` という条件付きの先送りで締めない。
- 準備・検証・引き渡しを求められたら、アクセス可能な入力は自分で読み、検証し、成果物を出す。計画文書や役割分担の書き換えで代替しない。
- Official distribution formats are ZIP, MSI, and MSIX only.
- Official release artifacts come from tag-driven GitHub Actions builds. Local builds are for development and validation only.
- The product scope is a live DWM thumbnail wall, not screen capture or streaming.
- Supported runtime is Windows. The desktop stack is WPF on .NET 10.

## Incident Rule: Scope Control

### Background

2026-04-08 の作業で、ユーザーの指示は「バージョンを 0.6.6 にして、main に PR を発行して」だった。
その一方で、エージェントは以下の指示外変更まで行った。

- リリースノートの新規追加
- `docs/notification-attention-design.md` の更新
- `packaging/build-zip.ps1` と `packaging/build-msi.ps1` の修正
- `packaging/WindowThumbWall.wxs` の修正
- ZIP/MSI の実ビルドによる追加生成物作成

### Cause Analysis

- 依頼達成に必要な最小変更と、「見つけた不整合をついでに直す」行為を分離できていなかった。
- 「PR を安全に出すための確認」と「リリースのための周辺整備」を混同した。
- 実行中に見つけた問題を、ユーザー承認なしに同一 PR へ含めてよいと誤判断した。

### Mandatory Countermeasures

- ユーザー指示が具体的なときは、差分の目的をその指示に直接必要なものへ限定する。
- 実行中に別の問題や改善点を見つけても、ユーザーが明示していない限り修正しない。
- ドキュメント追加、リリースノート追加、パッケージング修正、ビルド生成物作成は、明示依頼または実行必須条件がある場合に限る。
- `PR を出す`, `main に向ける`, `バージョンを上げる` といった依頼では、便乗 cleanup・便乗 hardening・便乗 release prep を禁止する。
- 「安全のため」という理由だけで、依頼外ファイルの編集を正当化してはいけない。必要なら編集前にユーザーへ確認する。
- ステージ前に「この PR に含めるファイル群は依頼範囲内か」を確認し、依頼外ファイルを含めない。

## Operational Rule: Direct-Request PR Flow

- ユーザーが PR 発行を指示した場合、まず base branch との差分汚染がないか確認する。
- その確認は許可されるが、確認中に見つけた別問題を自動修正してはいけない。
- PR 作成に必要な版数同期や既存実装の完了は進めてよい。
- PR 作成に不要な新規ファイル追加は行わない。

## Danger Zones

- `ThumbnailSlot.cs`, `ThumbHost.cs`, and `NativeMethods.cs` control thumbnail lifecycle and Win32/DWM interop.
- `packaging/` and `docs/releasing.md` control official distribution and release metadata generation.
- Packaging, CI, and documentation changes must preserve ZIP, MSI, and MSIX support together.

## Protected Surfaces

- この `AGENTS.md`、`.github/copilot-instructions.local.md`、その他のエージェント設定は保護対象とする。
- これらを変更するのは、ユーザーが明示した場合に限る。

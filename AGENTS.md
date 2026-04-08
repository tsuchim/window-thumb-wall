# AGENTS.md

## Scope

- WPF on .NET 10 desktop application for displaying live DWM thumbnails of external windows on Windows.

## Verification Commands

`dotnet run --project WindowThumbWall.csproj`
`dotnet build WindowThumbWall.sln`
`.\packaging\build-all.ps1`

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

## Danger Zones

- `ThumbnailSlot.cs`, `ThumbHost.cs`, and `NativeMethods.cs` control thumbnail lifecycle and Win32/DWM interop.
- `packaging/` and `docs/releasing.md` control official distribution and release metadata generation.
- Packaging, CI, and documentation changes must preserve ZIP, MSI, and MSIX support together.
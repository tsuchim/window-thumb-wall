# Developer Guide

This document covers local development, packaging, and release-related references for WindowThumbWall.

## Environment Requirements
- Windows 10 or 11
- .NET 10 SDK as pinned in [global.json](../global.json)
- Visual Studio 2022 or later for the easiest local debugging experience
- WiX Toolset v5 for MSI packaging

## Project Layout
- [WindowThumbWall.csproj](../WindowThumbWall.csproj): main WPF desktop application
- [MainWindow.xaml.cs](../MainWindow.xaml.cs): main UI behavior and interaction flow
- [ThumbnailSlot.cs](../ThumbnailSlot.cs): thumbnail slot lifecycle and rendering behavior
- [ThumbHost.cs](../ThumbHost.cs): host for DWM thumbnail content
- [NativeMethods.cs](../NativeMethods.cs): Win32 and DWM interop declarations
- [AppState.cs](../AppState.cs): local state persistence
- [packaging/](../packaging): packaging scripts, manifests, release metadata generator, and installer assets
- [docs/](.): policy and process documentation

## Run Locally

### Visual Studio
1. Open [WindowThumbWall.sln](../WindowThumbWall.sln).
2. Set WindowThumbWall as the startup project.
3. Press F5 to debug or Ctrl+F5 to run.

### CLI
```powershell
dotnet run --project WindowThumbWall.csproj
```

### Codex "Run" Dialog
If Codex asks for a command to launch the app, use one of the following:

- Normal local run:

```powershell
dotnet run --project WindowThumbWall.csproj
```

- Installed-package run for notification listener verification:

```powershell
.\packaging\build-and-run-msix.ps1 -Configuration Release
```

Use the MSIX path when you need to verify packaged app identity or OS notification listener behavior. Use `dotnet run` for ordinary local UI work.
If you start the unpackaged `dotnet run` / `bin\Debug` build, the Settings window now keeps `Reflect OS notifications` disabled and explains that notification attention requires the installed MSIX runtime.
The installed MSIX build does not use constant notification polling; it scans immediately when notification-change events arrive and keeps a 5-second follow-up reconciliation window for near-term toast updates.

## Build And Test Split
Use the supported command split below when working locally.

### App Build
```powershell
dotnet build .\WindowThumbWall.csproj -c Release
```

### Tests
`--no-build` assumes the test project binaries already exist. From a clean CLI state, build the test project once before using the command below.

```powershell
dotnet build .\WindowThumbWall.Tests\WindowThumbWall.Tests.csproj -c Release
```

```powershell
dotnet test .\WindowThumbWall.Tests\WindowThumbWall.Tests.csproj -c Release --no-build
```

### Packaging Build
```powershell
.\packaging\build-msix.ps1 -Configuration Release
```

`packaging/WindowThumbWall.Package.wapproj` is built with Visual Studio MSBuild/Desktop Bridge tooling. Plain `dotnet build` is not the supported packaging path in this repo/environment.

### Install The Local Test MSIX
After building the MSIX locally, install the newest signed test package with:

```powershell
.\packaging\install-msix.ps1
```

### Build, Install, And Launch In One Step
If you want a single entrypoint for Codex or local repeated runs, use:

```powershell
.\packaging\build-and-run-msix.ps1 -Configuration Release
```

Notes:
- `build-msix.ps1` now creates or reuses a local self-signed test certificate under `%LOCALAPPDATA%\WindowThumbWall\devcert`.
- If the same package version is already installed locally, `build-msix.ps1` bumps only the fourth version segment for the local MSIX build, so a repeated local install becomes `0.6.5.1`, `0.6.5.2`, and so on without changing the source-controlled release version.
- `install-msix.ps1` runs in the current terminal, imports the local test certificate into the current user's trust stores, installs the newest bundle, and launches WindowThumbWall.
- If the local machine does not trust the test signing certificate, `install-msix.ps1` falls back to unpackaged registration only when Windows Developer Mode or sideloading policy is enabled.
- `build-and-run-msix.ps1` runs `build-msix.ps1` first and only continues to `install-msix.ps1` when the build succeeds.
- A pure current-user install is not always enough for signed `.msixbundle` sideloading. When Windows still rejects the bundle with `0x800B0109`, trust the certificate in the local machine store once or enable Developer Mode so the fallback register path can be used.
- Notification listener features are expected to be tested from the installed MSIX, not from an unpackaged `dotnet run`.

## Local Generated Directories
Keep `.codex/` if you use Codex workspace-local settings for this repo. It is local tooling state, not an app build artifact.

The directories below are disposable local outputs and are safe to delete when you no longer need the generated contents:
- `artifacts/`: investigation logs, temporary archives, and ad-hoc local test outputs
- `dist/` and `dist-release-*/`: locally generated release metadata output directories
- `packaging/AppPackages/`: local MSIX build output consumed by `build-msix.ps1` and `install-msix.ps1`
- `packaging/BundleArtifacts/`: local packaging helper outputs
- `packaging/local-register/`: fallback registration staging created by `install-msix.ps1`

Recommended cleanup points:
- before branch cleanup
- before release prep if you want a fresh local packaging state
- after packaging investigations that produced large local artifacts

Example PowerShell cleanup:

```powershell
Remove-Item -Recurse -Force artifacts, dist, dist-release-*, packaging\AppPackages, packaging\BundleArtifacts, packaging\local-register
```

## Local MSIX Verification Checklist
Use the checklist below when changing local packaging, install scripts, app identity, attention notifications, or taskbar integration.

### Packaging And Versioning
1. Run `.\packaging\build-msix.ps1 -Configuration Release`.
2. Confirm the newest directory under `packaging\AppPackages` matches the expected version.
3. If the same `X.Y.Z.0` package is already installed locally, confirm the new local test output increments only the fourth segment, for example `0.6.5.1`.
4. Confirm the source manifests still remain on the checked-in release version after the build completes.

### Install And Launch
1. Run `.\packaging\build-and-run-msix.ps1 -Configuration Release`.
2. Confirm the script stays in the current terminal and does not open a separate helper shell.
3. Confirm the newest package installs or, if blocked, the script prints the correct next action for certificate trust or Developer Mode.
4. Confirm WindowThumbWall launches after installation and resolves the installed package identity correctly.

### Attention And Notification Behavior
0. Open the Settings window from the `Settings` button under `Fullscreen`, then confirm `Reflect OS notifications` is off by default on a fresh state file.
1. Trigger an `HSHELL_FLASH` path and confirm the matching monitored slot flashes red.
2. Trigger a Windows notification that resolves to one monitored window and confirm only that slot flashes red.
3. Trigger a Windows notification that leaves multiple monitored candidates and confirm those slots flash orange.
4. Confirm only monitored slots participate in notification matching.
5. Confirm a notification without usable source-app metadata does not render red or orange attention.
6. Confirm notifications that were already present before the listener was enabled do not get replayed into the wall until they change.
7. Confirm activating one of the candidate windows clears the related attention state.
8. Confirm an unchanged notification that was cleared by activating its candidate window does not light up again by itself.
9. Confirm an ambiguous notification does not render orange when the same source app already has any monitored window flashing red from the taskbar.
10. Confirm notification text is matched against title tokens by exact token equality, not substring matching.
11. Confirm `AppUserModelId` matching uses exact equality only.
12. Confirm app-display or identity hints reduce candidates only through process name or executable base name, not full path fragments.
13. Confirm the resolver picks the narrowest non-empty candidate set instead of the first token that matches anything.
14. Confirm a notification with source-app metadata does not jump to a different app's window only because a generic title token happened to match.
15. Turn `Reflect OS notifications` off in the Settings window during runtime and confirm notification-derived red/orange borders clear immediately.
16. Confirm an in-place update to the same toast shortly after the notification event is still reflected during the 5-second follow-up reconciliation window.

### Window Chrome And Shell Integration
1. Open the shortcut guide and confirm the version label is shown at the lower left.
2. Confirm the shortcut guide no longer shows the `Available controls in the app:` / `アプリで利用できる操作:` helper line.
3. Toggle fullscreen on and off and confirm the taskbar icon stays on the intended app icon instead of falling back to the generic document icon.

### Regression Gate
1. Run `dotnet build WindowThumbWall.csproj`.
2. Run `dotnet test WindowThumbWall.Tests\WindowThumbWall.Tests.csproj`.

## Build Packages
Official distributions must continue to support ZIP, MSI, and MSIX.

### Build Everything
```powershell
.\packaging\build-all.ps1
```

### Build Individually
```powershell
.\packaging\build-zip.ps1
.\packaging\build-msi.ps1
.\packaging\build-msix.ps1
```

`build-msix.ps1` locates Visual Studio via `vswhere.exe`, verifies Desktop Bridge imports are available, and fails with an actionable error instead of falling back to `dotnet build`.

For signing expectations, see [code-signing-policy.md](code-signing-policy.md).

## Release Metadata
Release metadata is generated by [packaging/generate-release-metadata.ps1](../packaging/generate-release-metadata.ps1).

Generated outputs include:
- `release-notes.md` for the GitHub Release body
- `store-listing-ja.md` and `store-listing-en.md` for Microsoft Store listing text
- `store-whats-new-ja.md` and `store-whats-new-en.md` for the Store "What's new" field

The script accepts a version and optional output directory.

```powershell
.\packaging\generate-release-metadata.ps1 -Version v0.2.0 -OutputDir dist
```

If you want release-specific highlights, create `packaging/release-notes-<version>.md` before running a tagged release.

If you want different Japanese and English release text, you can instead create:
- `packaging/release-notes-ja-<version>.md`
- `packaging/release-notes-en-<version>.md`

## Related Documents
- [releasing.md](releasing.md)
- [invariants.md](invariants.md)
- [capture-architecture-clarification.md](capture-architecture-clarification.md)
- [notification-attention-design.md](notification-attention-design.md)
- [code-signing-policy.md](code-signing-policy.md)
- [Public Privacy Policy (EN)](https://tsuchim.github.io/WindowThumbWall/PRIVACY.html)
- [Public Privacy Policy (JA)](https://tsuchim.github.io/WindowThumbWall/PRIVACY.ja.html)
- [PRIVACY.md](../PRIVACY.md)
- [PRIVACY.ja.md](../PRIVACY.ja.md)

# WindowThumbWall

[日本語 (README.ja.md)](README.ja.md)

A WPF desktop application (.NET 10) that displays **live DWM thumbnails** of
selected external windows in a flexible grid layout.

## Features

| Feature | Detail |
|---------|--------|
| **Live thumbnails** | DWM Thumbnail API (`DwmRegisterThumbnail` / `DwmUpdateThumbnailProperties`) |
| **Flexible grid** | Automatically grows from 1x1 to NxN as you add windows; shrinks when you remove them |
| **Fullscreen mode** | **Enter** toggles fullscreen; **Esc** exits fullscreen |
| **Shortcut guide window** | Click the shortcut hint text at the bottom of the left menu to open controls help |
| **Internationalization** | UI and guide window automatically switch between **English** and **Japanese** based on OS UI language |
| **Window picker** | Searchable list of visible top-level windows with real-time filter |
| **Auto-cleanup** | Cells are removed automatically when source windows close |

## Quick Start

1. Open `WindowThumbWall.slnx` in **Visual Studio 2026** (or later).
2. **F5** to build & run.
3. **Double-click** a window in the left panel to add it to the grid.
4. Click the shortcut hint text in the bottom-left menu to show the controls window.
5. Press **Enter** to toggle fullscreen.

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `Enter` | Toggle fullscreen |
| `Esc` | Exit fullscreen |

## Architecture

| File | Role |
|------|------|
| `NativeMethods.cs` | P/Invoke wrappers and native helpers |
| `ThumbHost.cs` | `HwndHost` subclass that creates a child HWND per cell |
| `ThumbnailSlot.cs` | Manages DWM thumbnail lifecycle for one slot |
| `WindowInfo.cs` | Data model for window-list items |
| `MainWindow.xaml/.cs` | Main UI layout and interaction logic |
| `LocalizedText.cs` | English/Japanese localized string table |
| `ShortcutGuideWindow.cs` | Popup window that shows operation list |

## Download

| Format | File | Notes |
|--------|------|-------|
| **ZIP** | `WindowThumbWall-v0.2-win-x64.zip` | Portable |
| **MSI** | `WindowThumbWall-v0.2-win-x64.msi` | Installer (uninstall removes local app state) |
| **MSIX** | `WindowThumbWall-v0.2-win-x64.msix` | Package (uninstall removes packaged app data) |

## Privacy Summary

- WindowThumbWall does not collect or transmit personal information.
- For display/restore, it stores only user-designated window entries and app names in local app data.
- Thumbnails/screenshot-like window images are captured only for on-screen display and are not used for any other purpose.
- Uninstall removes stored app data for MSI and MSIX packages (ZIP is portable and requires manual cleanup).

## Security / Code Signing

See [docs/code-signing-policy.md](docs/code-signing-policy.md).
For privacy and local data handling, see [PRIVACY.md](PRIVACY.md).

## Release Process

1. Create a GPG-signed tag (`git tag -s vX.Y.Z`)
2. Push the tag — GitHub Actions builds ZIP, MSI, and MSIX artifacts
3. Download CI artifacts and manually attach them to a GitHub Release

See [docs/releasing.md](docs/releasing.md) for the full process.

## Building Packages

```powershell
.\packaging\build-all.ps1
```

## Requirements

- Windows 10 or later
- .NET 10 SDK
- Visual Studio 2026 (recommended)

## License

GNU General Public License v3.0. See [LICENSE](LICENSE).

# WindowThumbWall

A WPF desktop application (.NET 10) that displays **live DWM thumbnails** of
selected external windows in a flexible grid layout — no screen capture, no OBS.

## Features

| Feature | Detail |
|---------|--------|
| **Live thumbnails** | DWM Thumbnail API (`DwmRegisterThumbnail` / `DwmUpdateThumbnailProperties`) — zero CPU capture |
| **Flexible grid** | Automatically grows from 1×1 to N×N as you add windows; shrinks when you remove them |
| **Fullscreen mode** | **Enter** expands the monitor wall to full screen (hides sidebar & title bar); **Esc** or **Enter** to exit |
| **Click to zoom** | Left-click a cell to maximize it across the entire grid; click again to restore |
| **Window picker** | Searchable list of all visible top-level windows with real-time filter |
| **Auto-cleanup** | Cells are removed automatically when source windows close |

## Quick Start

1. Open `WindowThumbWall.slnx` in **Visual Studio 2026** (or later).
2. **F5** to build & run.
3. **Double-click** a window in the left panel → it appears in a new grid cell.
4. **Right-click** a cell to remove it.
5. **Enter** to go fullscreen.

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `Enter` | Toggle fullscreen (thumbnail grid only) |
| `Esc` | Exit fullscreen |

## Architecture

| File | Role |
|------|------|
| `NativeMethods.cs` | P/Invoke: DWM Thumbnail API, `EnumWindows`, coordinate helpers |
| `ThumbHost.cs` | `HwndHost` subclass — creates a child HWND per cell |
| `ThumbnailSlot.cs` | Manages DWM thumbnail lifecycle for one slot |
| `WindowInfo.cs` | Data class for window-list items |
| `MainWindow.xaml/.cs` | UI layout and main logic |

## Download

| Format | File | Notes |
|--------|------|-------|
| **ZIP** | `WindowThumbWall-v0.2-win-x64.zip` | Portable — extract and run |
| **MSI** | `WindowThumbWall-v0.2-win-x64.msi` | Traditional installer (Program Files + Start Menu) |
| **MSIX** | `WindowThumbWall-v0.2-win-x64.msix` | Modern package (requires sideloading or trusted cert) |

All packages are **self-contained** (no .NET runtime install required).

## Building Packages

```powershell
# Build all formats at once
.\packaging\build-all.ps1

# Or individually
.\packaging\build-zip.ps1
.\packaging\build-msi.ps1    # requires WiX v5 (auto-installed)
.\packaging\build-msix.ps1   # requires Windows SDK
```

## Requirements

- **Windows 10** or later (Desktop Window Manager required)
- **.NET 10** SDK
- **Visual Studio 2026** (recommended)

## License

This project is licensed under the
**GNU General Public License v3.0** — see [LICENSE](LICENSE) for details.

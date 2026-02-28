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

## Requirements

- **Windows 10** or later (Desktop Window Manager required)
- **.NET 10** SDK
- **Visual Studio 2026** (recommended)

## License

This project is licensed under the
**GNU General Public License v3.0** — see [LICENSE](LICENSE) for details.

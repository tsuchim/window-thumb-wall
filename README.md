# WindowThumbWall

Keep the right windows in view, all the time.

WindowThumbWall is a Windows desktop app for building a live thumbnail wall from your open windows. Instead of repeatedly switching between apps, you keep the windows that matter visible in one place while you continue working.

[Japanese README](README.ja.md) | [Download Releases](https://github.com/tsuchim/WindowThumbWall/releases)

![WindowThumbWall screenshot](packaging/Assets/screenshot-1-1280.png)

## Why People Use It
- Watch logs, dashboards, chats, browsers, and terminals without constant alt-tabbing.
- Keep reference windows visible while your main app stays in focus.
- Turn a spare monitor into a practical monitoring wall.
- Compare live windows side by side instead of relying on static screenshots.

## Why It Feels Different
- Uses the native Windows DWM thumbnail API for live previews.
- Avoids heavyweight screen capture, streaming, or overlay setups.
- Adapts from a few windows to a full wall layout.
- Supports always-on-top and fullscreen workflows.
- Highlights windows that demand attention with red or orange borders based on taskbar flashes or Windows notification matching.
- Shows a steady white border on the cell whose source window is currently active.
- Lets you click any thumbnail to activate the original window immediately.
- Stays local, private, and simple.

## Good Fit For
- Developers watching CI, logs, browsers, and terminals.
- Analysts and traders following multiple charts or tools.
- Operators monitoring dashboards, alerts, and status pages.
- Power users who want persistent visibility across several windows.

## What You Get
- Live thumbnails of external windows.
- A clean wall layout that stays readable as you add windows.
- Quick window selection from the built-in picker.
- One-click activation of the source window from its thumbnail.
- Visual attention cues when a monitored window flashes in the taskbar or is matched from a Windows notification.
- A white border that indicates which monitored source window is currently active.
- Fullscreen mode for dedicated monitoring.
- Automatic cleanup when source windows disappear.
- Optional automatic addition for registered applications. Windows created while WindowThumbWall is running are added in their observed creation order within each application; registered applications stay in their configured order.

## Automatic Addition Order

Automatic addition uses a stable order close to the Windows taskbar, not an exact taskbar-order readout. Windows that were already open when WindowThumbWall starts receive a best-effort initial order from available window state. Windows created afterwards retain their observed creation order. Already monitored windows are never automatically rearranged, and later manual taskbar rearrangement is not tracked.

## Quick Start
1. Launch WindowThumbWall.
2. Double-click a window from the list to add it to the wall.
3. Click a thumbnail to bring the original window to the foreground.
4. Right-click a thumbnail to remove it.
5. When a monitored app flashes on the taskbar or a Windows notification resolves to it uniquely, its thumbnail border flashes red in the wall.
6. When notification matching leaves several monitored candidates, those candidate borders flash orange, except candidates from an app that is already flashing red from a taskbar signal.
7. The cell whose source window is currently active shows a steady white border.
8. Press Enter to toggle fullscreen.
9. Press Esc to leave fullscreen.
10. Open the shortcut guide from the lower-left hint when you need the full control list.

## Download Formats
WindowThumbWall is officially distributed in three formats.

- ZIP: portable, extract and run.
- MSI: classic Windows installer.
- MSIX: modern Windows package with clean uninstall behavior.

## Privacy
- No telemetry.
- No cloud sync.
- No external data transmission.
- Local state only, stored on your machine so the app can restore your layout.

Details: [Privacy Policy](https://tsuchim.github.io/WindowThumbWall/PRIVACY.html)

## Compatibility Note
WindowThumbWall is provided as-is. Compatibility, continuous operation, and fitness for a particular purpose are not guaranteed for every Windows environment or workflow. If you plan to rely on it for important work, verify its behavior on your own hardware and window setup first.

## Microsoft Store Copy
The following text is written to be copied directly into a Store listing when needed.

### Product Name
Window Thumb Wall

### Short Description
Keep multiple windows visible at once with a fast, low-overhead live thumbnail wall for Windows.

### Full Description
Window Thumb Wall lets you monitor multiple windows at the same time by arranging live thumbnails in a clean, flexible wall layout.

Instead of constantly switching between apps, you can keep the windows that matter visible on one screen: build logs, dashboards, chats, browsers, terminals, charts, status pages, or other desktop windows supported by Windows.

Because the app uses the native Windows Desktop Window Manager thumbnail API, it shows live window content with very low overhead. You get a practical monitoring workspace without setting up screen capture tools, streaming software, or custom overlays.

Why people use Window Thumb Wall:
- Monitor several windows without alt-tabbing.
- Keep reference information visible while working in another app.
- Build a simple monitoring wall on a second display.
- Follow live content instead of static screenshots.
- Notice when a window needs attention through red or orange borders, and keep track of the currently active source window with a white border.
- Jump back to the source window with a single click on its thumbnail.

Key features:
- Live thumbnails for external windows.
- Flexible wall layout that adapts to your open slots.
- Window picker for quickly adding targets.
- Click a thumbnail to activate its source window.
- Red or orange borders based on taskbar flashes and notification candidate resolution.
- A white border for the currently active monitored source window.
- Fullscreen mode for dedicated monitoring.
- Always-on-top support.
- Local-only operation with no telemetry.

Window Thumb Wall is designed for people who want persistent visibility with minimal friction: developers, analysts, operators, power users, and anyone who works across multiple live windows.

Important Note:
The app is provided as-is. Compatibility and behavior can vary depending on your Windows version, display layout, and the apps you monitor. Please verify it in your own environment before relying on it for important work.

### What's New Template
Use the generated release metadata for the version you are publishing. The release process generates Store-ready "What's new" text for each tagged release.

### Promotional Text
Turn spare screen space into a live window wall.

### Search Keywords
window monitor, thumbnail wall, desktop dashboard, live preview, multitasking, productivity, windows utility

## For Developers
Source builds, packaging notes, and contribution guidance have been moved out of the user-facing README.

- [Developer Guide](docs/developer-guide.md)
- [Release Process](docs/releasing.md)
- [Project Invariants](docs/invariants.md)
- [Notification Attention Design](docs/notification-attention-design.md)

## License
GNU General Public License v3.0. See [LICENSE](LICENSE).

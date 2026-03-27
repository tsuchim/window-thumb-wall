### WindowThumbWall v0.6.5

#### Changes

- Expanded attention highlighting beyond taskbar flashes by monitoring Windows notifications.
- Added notification-to-window matching with red flashing borders for unique matches.
- Added orange flashing borders when notification matching leaves multiple monitored candidates.
- Kept unmonitored windows in the candidate pool for disambiguation without rendering wall highlights for them.
- Tightened notification matching so it narrows candidates by exact token and exact identity comparisons instead of broad substring-style matches.
- Added a persisted Settings window entry for whether OS notifications should affect attention highlighting, with the default set to off.
- Updated local MSIX packaging so `build-msix.ps1` produces a reusable self-signed test package for installation testing.
- Added an MSIX install helper that runs in the current terminal, imports the local test certificate, installs the newest bundle, and launches the app.
- Added `build-and-run-msix.ps1` as a one-step local entrypoint for build, install, and launch.
- Fixed the main taskbar icon so the running desktop window keeps the intended app icon instead of falling back to the generic document icon.

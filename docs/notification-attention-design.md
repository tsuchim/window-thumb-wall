# Notification Attention Matching

This document describes how WindowThumbWall expands attention signaling beyond the existing taskbar flash hook.

## Goals
- Keep the current `HSHELL_FLASH` path as the exact, HWND-based attention signal.
- Add Windows notification monitoring as a secondary signal source.
- Resolve a notification to a single window when the evidence is strong enough.
- If a notification cannot be resolved to a single monitored window, highlight every monitored candidate instead.
- Clear the notification-derived highlight when any candidate window is activated.

## User Setting
- `Reflect OS notifications` controls whether Windows notifications participate in attention highlighting.
- Default: off
- Persistence: stored alongside the normal app state in the same `state.json`
- UI: changed from the main pane through the dedicated Settings window opened by the `Settings` button under `Fullscreen`

## Design Principles
- Treat notification matching as candidate narrowing, not fallback expansion.
- Separate exact signals from inferred signals:
  - `HSHELL_FLASH` is exact because Windows gives an `HWND`.
  - notification matching is inferred because Windows does not give an `HWND`.
- Compare like with like:
  - notification text tokens only compare against window title tokens
  - `AppUserModelId` only compares against window `AppUserModelId`
  - app-display and identity hints only compare against process name or executable base name
- Prefer exact equality over substring matching.
- If the evidence does not narrow to one window, keep the result ambiguous.

## Signal Sources
- Taskbar flash:
  - Source: shell hook `HSHELL_FLASH`
  - Precision: exact HWND
  - Visual: red flashing border
- Windows notification listener:
  - Source: `UserNotificationListener`
  - Precision: app metadata plus notification text, not HWND
  - Visual:
    - red flashing border for a unique match
    - orange flashing border for ambiguous monitored candidates

## Candidate Pool
Candidate matching uses all visible top-level Alt+Tab windows, not just monitored slots.

Reasoning:
- an unmonitored window can still provide evidence that disambiguates a monitored one
- visual notification remains restricted to monitored slots only

## Match Order
The resolver evaluates notification evidence in this order.

1. If the notification exposes source-app identity, narrow candidates to the same app first:
   - exact `AppUserModelId` match
   - otherwise exact process-name / executable-base-name match from app-identity hints
2. If source-app identity exists and no candidate survives that reduction, do not match the notification to any window
3. Within the surviving candidate set, narrow by strong notification-text token to window-title token exact match, choosing the narrowest non-empty candidate set
4. If one candidate remains, treat it as unique
5. If multiple candidates remain, treat the result as ambiguous

The resolver intentionally does not use notification ID or app display name as a unique window identifier.
App display name is only a reduction hint and must not create substring matches against unrelated window titles or full paths.

## Comparison Semantics
- Notification text to title:
  - normalize both sides
  - split into tokens
  - compare by token equality only
  - do not use substring, prefix, suffix, or full-path contains matching
- `AppUserModelId`:
  - normalize both sides
  - compare by exact equality only
- Executable reduction:
  - derive hint tokens from app display name and `AppUserModelId`
  - compare those hints only to process name and executable base name
  - do not compare them to full executable paths or window titles
- Candidate selection:
  - each step may only shrink the candidate set
  - when app identity exists, title matching only runs inside the app-compatible candidate set
  - if no app-compatible candidates exist, the result is `None`
  - if multiple title tokens match, keep the smallest non-empty set

## Visual State Rules
- Red:
  - taskbar flash is active for the monitored window
  - or notification matching resolved that notification to one monitored window
- Orange:
  - notification matching left multiple monitored candidates
  - but only when the same source app does not already have a monitored window flashing red from the taskbar
- White:
  - the source window for that monitored slot is the current foreground window
  - this border does not flash
  - red and orange attention states take priority over white
- No border:
  - no active taskbar flash
  - and no active notification-derived attention group touching that slot

If a monitored window is both taskbar-flashing and notification-matched, red wins.
If an ambiguous notification belongs to an app that already has any monitored window flashing red, suppress the orange notification border for that notification instead of rendering both signals.

## Clearing Rules
- Taskbar flash clears on foreground activation, same as before.
- Notification-derived attention groups clear when any candidate window in that group becomes active.
- Flash-based suppression for an ambiguous notification persists until that notification leaves the current Windows toast list.
- Unmonitored windows participate in matching and clearing, but never render a flashing border in the wall.

## Packaging Requirements
- MSIX manifest declares `uap3:Capability Name="userNotificationListener"`.
- Local MSIX builds are signed with a reusable self-signed test certificate so they can be installed for testing.

## Known Limits
- Notification matching is deterministic narrowing over incomplete evidence, not a direct OS-provided window identifier.
- Multiple windows with the same app identity and weak notification text will remain ambiguous.
- Notification listener behavior depends on notification access being granted to the installed app package.

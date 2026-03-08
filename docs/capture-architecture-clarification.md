# Capture Architecture Clarification (DWM vs Windows Graphics Capture)

## Summary
- The current app architecture is DWM-thumbnail based, not Windows Graphics Capture based.
- Each monitored slot registers a DWM thumbnail (`DwmRegisterThumbnail`) and updates thumbnail properties (`DwmUpdateThumbnailProperties`).
- There is no `Direct3D11CaptureFramePool`, `GraphicsCaptureSession`, `Recreate(...)`, or `ContentSize` control path in this repository.

Because of this, a `Direct3D11CaptureFramePool.Recreate(...)`-style resize policy is not a local patch in this codebase. It would require an architectural change from DWM thumbnails to a capture + render pipeline.

## Where sizing is currently decided
- Grid/cell sizing is computed in [MainWindow.xaml.cs](../MainWindow.xaml.cs):
  - `CalcGridSize(int count)` chooses rows/columns based on wall dimensions and active source aspect ratios.
  - `RebuildGrid()` creates row/column definitions and places each slot in a cell.
- Actual thumbnail destination rectangles are applied in [ThumbnailSlot.cs](../ThumbnailSlot.cs):
  - `UpdateThumbnail()` reads the slot host client rect (`GetClientRect`), converts to owner-client coordinates (`ClientToScreen`), and sets `rcDestination`.
  - The DWM update uses `DWM_TNP_RECTDESTINATION | DWM_TNP_VISIBLE`.

## Where layout-driven changes are applied
- Layout and window-size changes trigger `UpdateAllThumbnails()` from [MainWindow.xaml.cs](../MainWindow.xaml.cs):
  - `OnSizeChanged(...)` and panel-size changes request grid rebuilds.
  - `RebuildGrid()` requests thumbnail updates after cell placement updates.
  - Fullscreen toggle requests thumbnail updates.
  - Thumbnail updates are requested through an invalidation queue (`RequestThumbnailUpdate()`), not unconditional timer pushes.
- `UpdateAllThumbnails()` iterates slots and calls `slot.UpdateThumbnail()`.

In short: current layout changes update DWM destination rectangles, not any capture buffer size.

## Existing low-frequency policy
- A periodic dispatcher timer exists in [MainWindow.xaml.cs](../MainWindow.xaml.cs):
  - `_timer.Interval = 1000ms`
  - `Timer_Tick(...)` handles maintenance tasks (window list/title refresh, auto-add, validation, flash checks).
- Timer ticks no longer unconditionally call `UpdateAllThumbnails()`.
- This remains a maintenance refresh loop, not a capture-pool resize cooldown mechanism.

## DWM control surface available today
- Via [NativeMethods.cs](../NativeMethods.cs) and [ThumbnailSlot.cs](../ThumbnailSlot.cs), the current implementation directly uses:
  - `DwmRegisterThumbnail`
  - `DwmUpdateThumbnailProperties`
  - `DwmUnregisterThumbnail`
  - property flags currently used: `DWM_TNP_RECTDESTINATION`, `DWM_TNP_VISIBLE`
- Practical knobs in the current approach:
  - Destination rectangle (`rcDestination`)
  - Visibility (`fVisible`)
  - Thumbnail slot assignment/lifecycle
  - De-duplicated property updates in `ThumbnailSlot.UpdateThumbnail()` (apply only when visibility or destination rect changed)

Not present as current knobs:
- Capture frame-pool pixel dimensions
- Frame buffer count/device swap via `Recreate(...)`
- Per-frame content region semantics like `ContentSize`

## Recommendation
- Keep the current DWM-thumbnail architecture for now.
- If Windows Graphics Capture behavior is desired (buffer sizing policies, frame-pool recreation, `ContentSize`-aware sampling), do that work in a separate design/implementation branch as an explicit scope change.
- Do not treat this as a small local resize-policy patch in the existing DWM codepath.
